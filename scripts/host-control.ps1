# 実機動作確認で使うホストの操作役。
# PMXエディタの起動と終了、プラグインメニューからの稼働状態の確認・停止・開始、
# 待ち受けているホストのパイプの一覧を、画面を人手で操作せずに行う。
# メニューはWinFormsのToolStripでWin32のHMENUではないため、UI Automationで辿る。
# メニューの文言と確認ボタンの表示名を手がかりにするので、Windowsの表示言語が日本語である
# ことを前提にする。
#
# 状態を変える操作は、その結果が観測できるようになるまで待ってから戻る。待受の公開も停止手順も
# エディタ側の別スレッドで進むので、待たずに次へ進むと状態が落ち着く前の一瞬を見てしまう。
#
# 停止・開始は、いま押そうとしている問いが要求した操作のものであることを確かめてから押す。
# ホストは稼働中なら停止を、停止済みなら開始を問う同じ形の表示を出すので、確かめずに肯定を
# 押すと逆の操作をしたまま正常終了しうる。
[CmdletBinding()]
param(
    # 行う操作。
    #   pipes  待ち受けているホストのパイプ名を一覧する
    #   launch PMXエディタを起動し、そのホストの待受が現れるまで待ってプロセスIDを返す
    #   close  指定したエディタを通常の手順で終了し、終了と待受の消失を待つ
    #   status プラグインメニューの稼働状態を表示させ、本文を読んで閉じる
    #   stop   稼働中のホストを停止し、待受の消失と状態区分が停止済みになるまで待つ
    #   start  停止済みのホストを開始し、待受が現れるまで待つ
    [Parameter(Mandatory = $true)]
    [ValidateSet("pipes", "launch", "close", "status", "stop", "start")]
    [string]$Action,

    # 操作の対象にするエディタのプロセスID。pipes と launch では使わない。
    [int]$ProcessId,

    # 状態が変わるのを待つ上限の秒数。0以下だと、状態を変えておきながら一度も観測しないまま
    # 失敗しうるので受け付けない。上限は、終了待ちへミリ秒で渡せる範囲に収める。
    [ValidateRange(1, 2147483)]
    [int]$TimeoutSeconds = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class HostControlWindow {
  [DllImport("user32.dll")] public static extern IntPtr PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
}
"@

# ホストが待受に使うパイプ名。接頭辞の後ろはエディタのプロセスIDで、ホストは十進で書くだけ
# なので、先頭の0や数字以外は現れない。試験用の待受など紛らわしい名前を一覧へ混ぜないために、
# ブリッジ側の絞り込みと同じ形で見る。ブリッジは大文字小文字を区別して照合するので、
# ここでも区別する演算子を使う。
$HostPipePattern = "^pmx-editor-mcp-([1-9][0-9]*)$"

# 待ち受けているパイプが並ぶディレクトリ。
$PipeDirectory = "\\.\pipe\"

# プラグインのメニュー文言。状態表示の表題にも同じ文言が出る。
$PluginName = "PMX Editor MCP"

# 状態表示のウィンドウクラス。標準のメッセージボックスのもの。
$DialogClassName = "#32770"

# メニューを開くと現れる影のウィンドウクラス。影の専用クラスで、他の用途には使われない。
$ShadowClassName = "SysShadow"

# 状態表示が出す問いと、その問いが現れる状態区分。要求した操作と食い違っていないかを見る。
$OperationPrompts = @{
    stop  = @{ Question = "停止しますか?"; StatusKind = "稼働中" }
    start = @{ Question = "開始しますか?"; StatusKind = "停止済み" }
}

# 状態を見に行く間隔。
$PollIntervalMs = 500

# 閉じるためのウィンドウメッセージ(WM_CLOSE)。
$WindowMessageClose = 0x0010

function Get-HostPipeNames {
    <#
        .SYNOPSIS
        待ち受けているホストのパイプ名を返す。ホストが名乗る形のものだけに絞る。
    #>
    [System.IO.Directory]::GetFiles($PipeDirectory) |
        ForEach-Object { Split-Path -Leaf $_ } |
        Where-Object { $_ -cmatch $HostPipePattern -and [int]::TryParse($Matches[1], [ref]$null) }
}

function Test-HostPipe {
    param([int]$OwnerProcessId)

    @(Get-HostPipeNames) -ccontains "pmx-editor-mcp-$OwnerProcessId"
}

function Wait-HostPipe {
    <#
        .SYNOPSIS
        指定したエディタの待受が、現れる(Present)か消える(Absent)まで待つ。
    #>
    param(
        [int]$OwnerProcessId,
        [ValidateSet("Present", "Absent")][string]$Until
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ($true) {
        if (((Test-HostPipe -OwnerProcessId $OwnerProcessId)) -eq ($Until -eq "Present")) { return }
        if ((Get-Date) -ge $deadline) { break }
        Start-Sleep -Milliseconds $PollIntervalMs
    }

    $state = if ($Until -eq "Present") { "現れなかった" } else { "消えなかった" }
    throw "待受 pmx-editor-mcp-$OwnerProcessId が $TimeoutSeconds 秒以内に$state。"
}

function Get-EditorProcess {
    <#
        .SYNOPSIS
        対象がPMXエディタであることを確かめてプロセスを返す。プロセスIDは使い回されるので、
        名前を確かめずに終了させると無関係なプロセスを巻き込む。
    #>
    param([int]$OwnerProcessId)

    $process = Get-Process -Id $OwnerProcessId
    if ($process.ProcessName -ne "PmxEditor_x64") {
        throw "プロセスIDがPMXエディタのものではない: $OwnerProcessId ($($process.ProcessName))"
    }

    $process
}

function Get-EditorDirectory {
    <#
        .SYNOPSIS
        PMXエディタの導入先を local.props から読む。ビルドが参照するのと同じ定義を使う。
        XMLとして読むのは、値に含まれる実体参照を元の文字へ戻すため。
    #>
    $propsPath = Join-Path (Split-Path -Parent $PSScriptRoot) "local.props"
    if (-not (Test-Path $propsPath)) {
        throw "local.props が無い。PmxEditorDir を定義する必要がある: $propsPath"
    }

    $document = New-Object System.Xml.XmlDocument
    $document.Load((Resolve-Path $propsPath))
    $nodes = @($document.GetElementsByTagName("PmxEditorDir"))
    if ($nodes.Count -eq 0) { throw "local.props に PmxEditorDir が無い: $propsPath" }
    if ($nodes.Count -gt 1) {
        throw "local.props の PmxEditorDir が $($nodes.Count) 個ある。ビルドがどれを採る" +
            "かはMSBuildの評価に依るので、この操作役では決められない: $propsPath"
    }

    # 条件や選択の構造の下にあると、ビルドが採る値はMSBuildの評価に依る。祖先まで遡って見る。
    $node = $nodes[0]
    for ($ancestor = $node; $ancestor -is [System.Xml.XmlElement]; $ancestor = $ancestor.ParentNode) {
        if ($ancestor.HasAttribute("Condition")) {
            throw "PmxEditorDir が条件付きの $($ancestor.Name) の下にあって、この操作役では" +
                "解決できない: $propsPath"
        }
        if (@("Choose", "When", "Otherwise") -contains $ancestor.Name) {
            throw "PmxEditorDir が $($ancestor.Name) の下にあって、この操作役では解決できない: $propsPath"
        }
    }

    $value = $node.InnerText.Trim()
    if ($value -match "\`$\(") {
        throw "PmxEditorDir がMSBuildの式を含んでいて、この操作役では解決できない: $value"
    }

    $value
}

function Get-ProcessElements {
    <#
        .SYNOPSIS
        指定したプロセスの要素を返す。子孫まで辿る——プラグインが出す状態表示はデスクトップ
        直下ではなくエディタのウィンドウの配下に現れるので、直下だけを見ると取りこぼす。
        Match を与えると、その条件も満たすものだけを返す。
    #>
    param([int]$OwnerProcessId, $Match = $null)

    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $OwnerProcessId)
    if ($Match) {
        $condition = New-Object System.Windows.Automation.AndCondition($condition, $Match)
    }

    $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Get-StatusDialogs {
    <#
        .SYNOPSIS
        プラグインが出した状態表示を返す。表題まで見るのは、エディタが出す別の確認と
        取り違えて肯定を押さないため。
    #>
    param([int]$OwnerProcessId)

    @(Get-ProcessElements -OwnerProcessId $OwnerProcessId |
        Where-Object {
            $_.Current.ClassName -eq $DialogClassName -and $_.Current.Name -eq $PluginName
        })
}

function Get-MenuShadows {
    <#
        .SYNOPSIS
        対象のエディタがデスクトップ直下に持つ影のウィンドウのハンドルを返す。
    #>
    param([int]$OwnerProcessId)

    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $OwnerProcessId)
    @($root.FindAll([System.Windows.Automation.TreeScope]::Children, $condition) |
        Where-Object { $_.Current.ClassName -eq $ShadowClassName } |
        ForEach-Object { $_.Current.NativeWindowHandle })
}

function Close-MenuShadow {
    <#
        .SYNOPSIS
        メニューを開いた跡に残る影のウィンドウを閉じる。開いた側が片付けないと画面へ残る。
        閉じるのは影の専用クラスを持つもののうち、メニューを開いてから現れたものだけに絞る
        ——他のウィンドウも、こちらが出したのではない影も巻き込まないため。
    #>
    param([int]$OwnerProcessId, $Existing)

    foreach ($handle in Get-MenuShadows -OwnerProcessId $OwnerProcessId) {
        if ($Existing -contains $handle) { continue }

        [void][HostControlWindow]::PostMessage(
            [IntPtr]$handle, $WindowMessageClose, [IntPtr]::Zero, [IntPtr]::Zero)
    }
}

function Close-OpenMenu {
    <#
        .SYNOPSIS
        開いたメニューを畳む。開いたままだとメニューが入力待ちを続け、エディタは終了要求も
        受け付けなくなる。畳む手段が無ければ、黙って見逃さずに失敗させる——残ったメニューは
        後の操作を塞ぐので、残ったこと自体が伝わらないと原因に辿り着けない。
    #>
    param($Menu)

    $pattern = $null
    if (-not $Menu.TryGetCurrentPattern(
            [System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pattern)) {
        throw "メニューを畳む手段が無い: $($Menu.Current.Name)"
    }

    $pattern.Collapse()
}

function Close-OpenMenuSafely {
    <#
        .SYNOPSIS
        開いたメニューを畳む。畳めなかったことは警告として知らせるだけにして、元の失敗を
        置き換えない。警告を終了させる設定で呼ばれても置き換えが起きないよう、継続に固定する。
    #>
    param($Menu)

    try {
        Close-OpenMenu -Menu $Menu
    }
    catch {
        Write-Warning "開いたメニューを閉じられなかった: $($_.Exception.Message)" -WarningAction Continue
    }
}

function Invoke-Element {
    param($Element)

    $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Find-EditMenu {
    <#
        .SYNOPSIS
        プラグインの項目が属する編集メニューを探す。エディタは複数のウィンドウを持ち、編集の
        メニューを持つメニューバーも1つとは限らないので、メニューの文言だけでは決められない。
        目当ての項目そのものを配下に持つことを条件にして選ぶ。まだ組み上がっていなければ空を
        返す。該当が複数あるときは選ばずに失敗させる——取り違えると別のメニューを操作してしまう。
    #>
    param([int]$OwnerProcessId)

    $barCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::MenuBar)
    $pluginCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $PluginName)

    $found = @()
    foreach ($bar in Get-ProcessElements -OwnerProcessId $OwnerProcessId -Match $barCondition) {
        foreach ($item in $bar.FindAll(
                [System.Windows.Automation.TreeScope]::Children,
                [System.Windows.Automation.Condition]::TrueCondition)) {
            if ($item.Current.Name -notlike "編集*") { continue }

            $plugin = @($item.FindAll(
                [System.Windows.Automation.TreeScope]::Descendants, $pluginCondition))
            if ($plugin.Count -ge 1) { $found += $item }
        }
    }

    if ($found.Count -gt 1) {
        throw "「$PluginName」を含む編集メニューが $($found.Count) 個ある: プロセスID $OwnerProcessId"
    }
    if ($found.Count -eq 0) { return $null }

    $found[0]
}

function Get-EditMenu {
    <#
        .SYNOPSIS
        編集メニューが現れるまで待って返す。待受の公開はプラグインの読み込みで済むが、ウィンドウと
        メニューが揃うのはそれとは別に進むので、起動直後はまだ辿れないことがある。
    #>
    param([int]$OwnerProcessId, $Deadline)

    while ($true) {
        $menu = Find-EditMenu -OwnerProcessId $OwnerProcessId
        if ($menu) { return $menu }
        if ((Get-Date) -ge $Deadline) {
            throw "「$PluginName」を含む編集メニューが現れない: プロセスID $OwnerProcessId"
        }
        Start-Sleep -Milliseconds $PollIntervalMs
    }
}

function Get-Deadline {
    <#
        .SYNOPSIS
        待ちの期限を返す。呼び出し元から期限を渡されていればそれを引き継ぐ——待ちが入れ子に
        なるとき、内側が独自に期限を取り直すと、外側の上限を何倍にも越えてしまう。
    #>
    param($Deadline)

    if ($Deadline) { return $Deadline }

    (Get-Date).AddSeconds($TimeoutSeconds)
}

function Show-StatusDialog {
    <#
        .SYNOPSIS
        プラグインメニューを実行して稼働状態の表示を出し、その要素を返す。編集メニューを開いて
        から配下のプラグイン項目を押す。項目も表示も辿れるようになるまで待ってから次へ進む。
    #>
    param([int]$OwnerProcessId, $Deadline)

    $deadline = Get-Deadline -Deadline $Deadline
    $edit = Get-EditMenu -OwnerProcessId $OwnerProcessId -Deadline $deadline
    $shadows = @(Get-MenuShadows -OwnerProcessId $OwnerProcessId)

    $nameCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $PluginName)

    # メニューを開く操作そのものも含めて、以降の失敗では影を片付ける。開く呼び出しが例外を
    # 返しても、画面には既に開いた跡が残りうる。
    $pressError = $null
    $pressing = $false
    try {
        Invoke-Element -Element $edit

        $target = $null
        while ($true) {
            $target = $edit.FindFirst(
                [System.Windows.Automation.TreeScope]::Descendants, $nameCondition)
            if ($target) { break }
            if ((Get-Date) -ge $deadline) { throw "メニュー項目が見つからない: $PluginName" }
            Start-Sleep -Milliseconds $PollIntervalMs
        }

        # ここから先の失敗は、押す操作が届いた後かもしれない。表示が出ている可能性を残したまま
        # 抜けないよう、失敗を控えて表示の待ちへ進む。
        $pressing = $true
        Invoke-Element -Element $target
    }
    catch {
        # 開いたままのメニューを残さない。押せたときは押した側が畳むので、畳むのは失敗のときだけ。
        Close-OpenMenuSafely -Menu $edit
        if (-not $pressing) { throw }

        $pressError = $_
    }
    finally {
        # 後始末の失敗で元の失敗を上書きしない。影が残ることより、何が起きたかを伝えることを採る。
        # 警告を終了させる設定で呼ばれても置き換えが起きないよう、この警告は継続に固定する。
        try {
            Close-MenuShadow -OwnerProcessId $OwnerProcessId -Existing $shadows
        }
        catch {
            Write-Warning "メニューの影を片付けられなかった: $($_.Exception.Message)" -WarningAction Continue
        }
    }

    if ($pressError) {
        # 押す操作は失敗したが、届いていれば表示は出る。出ていれば閉じてから元の失敗を伝える。
        # 片付けの側がさらに失敗しても、伝えるのは元の失敗である——後から起きたことで原因を
        # 覆い隠さない。
        try {
            $late = Wait-StatusDialog -OwnerProcessId $OwnerProcessId `
                -Deadline (Get-Date).AddSeconds($TimeoutSeconds)
            if ($late) { Close-StatusDialogSafely -Dialog $late }
        }
        catch {
            Write-Warning "押す操作の失敗後に表示を片付けられなかった: $($_.Exception.Message)" -WarningAction Continue
        }

        throw $pressError
    }

    # 押した後は、期限が尽きていても表示が出るまで待つ。ここで諦めると、閉じ手のないモーダル
    # 表示が残る。越えるのはこの1回分だけで、待ちが積み上がることはない。
    $dialog = Wait-StatusDialog -OwnerProcessId $OwnerProcessId `
        -Deadline (Get-Date).AddSeconds($TimeoutSeconds)
    if (-not $dialog) { throw "稼働状態の表示が $TimeoutSeconds 秒以内に出なかった。" }

    $dialog
}

function Wait-StatusDialog {
    <#
        .SYNOPSIS
        稼働状態の表示が現れるまで待って返す。期限までに現れなければ空を返す。
    #>
    param([int]$OwnerProcessId, $Deadline)

    while ($true) {
        # 関数の戻り値は1件だと単体へ畳まれるので、件数を数える前に配列へ入れ直す。
        $dialogs = @(Get-StatusDialogs -OwnerProcessId $OwnerProcessId)
        if ($dialogs.Count -ge 1) { return $dialogs[0] }
        if ((Get-Date) -ge $Deadline) { return $null }
        Start-Sleep -Milliseconds $PollIntervalMs
    }
}

function Get-DialogText {
    param($Dialog)

    @($Dialog.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition) |
        Where-Object { $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Text } |
        ForEach-Object { $_.Current.Name })
}

function Get-DialogButton {
    param($Dialog, [string]$Label)

    $buttonCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    @($Dialog.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants, $buttonCondition) |
        Where-Object { $_.Current.Name -like "$Label*" })
}

function Close-StatusDialog {
    <#
        .SYNOPSIS
        状態表示を、何も起こさない側の選択で閉じる。問いの形なら否定、確認だけの形なら了解。
    #>
    param($Dialog)

    foreach ($label in @("いいえ", "OK")) {
        $buttons = @(Get-DialogButton -Dialog $Dialog -Label $label)
        if ($buttons.Count -ge 1) {
            Invoke-Element -Element $buttons[0]
            return
        }
    }

    throw "状態表示を閉じるボタンが見つからない。"
}

function Test-ElementAvailable {
    <#
        .SYNOPSIS
        要素がまだ辿れるかを返す。閉じた画面の要素は失効し、触れると例外になる。
    #>
    param($Element)

    try {
        [void]$Element.Current.Name
        $true
    }
    catch [System.Windows.Automation.ElementNotAvailableException] {
        $false
    }
    catch {
        # 判じられないときは、まだ辿れる側に倒す。呼び出し側は失敗を握り潰さずに知らせる。
        $true
    }
}

function Close-StatusDialogSafely {
    <#
        .SYNOPSIS
        受け取った状態表示がまだ出ていれば閉じる。既に閉じていれば何もしない。閉じられなかった
        ことは警告として知らせるだけにして、元の失敗を置き換えない——後から起きたことで原因を
        覆い隠さない。
        閉じる相手は受け取った要素そのものに限る。ウィンドウのハンドルは閉じた後に使い回される
        ので、ハンドルの一致で選び直すと、後から出た別の表示を元の表示と取り違えうる。
    #>
    param($Dialog)

    try {
        Close-StatusDialog -Dialog $Dialog
    }
    catch {
        # 既に閉じているかは、失敗の種類ではなく表示そのものが辿れるかで判じる。ボタンだけが
        # 失効して押せなかった場合を、閉じた証拠と取り違えないため。
        if (-not (Test-ElementAvailable -Element $Dialog)) { return }

        Write-Warning "状態表示を閉じられなかった: $($_.Exception.Message)" -WarningAction Continue
    }
}

function Get-StatusKind {
    <#
        .SYNOPSIS
        状態表示の本文から状態区分を取り出す。区分が読めない表示(ホストが常駐していない等)は
        空を返す。
    #>
    param([string[]]$Text)

    foreach ($line in ($Text -split "`n")) {
        if ($line -match "^状態:\s*(.+?)\s*$") { return $Matches[1] }
    }

    ""
}

function Wait-StatusKind {
    <#
        .SYNOPSIS
        状態区分が期待の値になるまで、状態表示を出しては閉じて待つ。
        停止はサーバースレッドの終了を待たずに戻るので、待受が消えても暫くは停止処理中で、
        その状態からの開始は受け付けられない。
    #>
    param([int]$OwnerProcessId, [string]$Expected)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $seen = ""
    while ($true) {
        $dialog = Show-StatusDialog -OwnerProcessId $OwnerProcessId -Deadline $deadline
        try {
            $seen = Get-StatusKind -Text (Get-DialogText -Dialog $dialog)
        }
        finally {
            Close-StatusDialogSafely -Dialog $dialog
        }
        if ($seen -eq $Expected) { return }
        if ((Get-Date) -ge $deadline) { break }
        Start-Sleep -Milliseconds $PollIntervalMs
    }

    throw "状態区分が $TimeoutSeconds 秒以内に「$Expected」にならなかった。最後に見たのは「$seen」。"
}

function Invoke-HostOperation {
    <#
        .SYNOPSIS
        稼働状態の表示から停止または開始を選ぶ。要求した操作の問いが出ていることを確かめてから
        押し、食い違っていれば何もせずに失敗させる。
    #>
    param([int]$OwnerProcessId, [string]$Operation)

    $expected = $OperationPrompts[$Operation]
    $dialog = Show-StatusDialog -OwnerProcessId $OwnerProcessId

    # どの失敗でも表示を残さない。モーダルなので、残すと以後の操作を塞ぐ。
    $pressed = $false
    try {
        $text = @(Get-DialogText -Dialog $dialog)
        $text

        $kind = Get-StatusKind -Text $text
        $asked = ($text -join "`n").Contains($expected.Question)
        if ($kind -ne $expected.StatusKind -or -not $asked) {
            throw "$Operation を行える状態ではない。状態区分は「$kind」で、「$($expected.Question)」の問いが出ていない。"
        }

        $buttons = @(Get-DialogButton -Dialog $dialog -Label "はい")
        if ($buttons.Count -eq 0) { throw "肯定のボタンが見つからない。" }

        Invoke-Element -Element $buttons[0]
        $pressed = $true
    }
    finally {
        # 肯定を押せていれば、閉じるのは押された側の仕事である。ここで閉じにいくと、押した内容が
        # 処理される前に否定を重ねて、操作を取り消しかねない。
        if (-not $pressed) { Close-StatusDialogSafely -Dialog $dialog }
    }
}

function Assert-ProcessId {
    <#
        .SYNOPSIS
        対象のエディタが指定されていることを確かめる。プロセスIDに0以下は割り当てられないので、
        既定値のままかどうかは値で判別できる。
    #>
    if ($ProcessId -le 0) {
        throw "この操作には -ProcessId が要る: $Action"
    }
}

switch ($Action) {
    "pipes" {
        Get-HostPipeNames
    }
    "launch" {
        $editorPath = Join-Path (Get-EditorDirectory) "PmxEditor_x64.exe"
        if (-not (Test-Path $editorPath)) { throw "エディタの実行ファイルが無い: $editorPath" }

        $started = Start-Process -FilePath $editorPath -PassThru
        Wait-HostPipe -OwnerProcessId $started.Id -Until Present
        $started.Id
    }
    "close" {
        Assert-ProcessId
        # 強制終了はプラグインの後始末を通らないので、通常の終了と同じ経路で閉じる。
        # 直前の操作の名残でウィンドウが終了要求を受け付けないことがあるので、受け付けるまで
        # 求め直す。ウィンドウの取り直しが要るため、そのつど最新の状態を読み込む。
        $process = Get-EditorProcess -OwnerProcessId $ProcessId
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        $lastError = $null
        while ($true) {
            $process.Refresh()
            # 求め直している間に終わっていることがある。終わっていれば要求はもう要らない。
            if ($process.HasExited) { break }

            $requested = $false
            try {
                $requested = $process.CloseMainWindow()
            }
            catch {
                # 要求を出すまでの間に終わっていると、これも失敗になる。次の観測で判ずる。
                $lastError = $_
            }
            if ($requested) { break }

            $process.Refresh()
            if ($process.HasExited) { break }
            if ((Get-Date) -ge $deadline) {
                $detail = if ($lastError) { " 最後の失敗: $($lastError.Exception.Message)" } else { "" }
                throw "エディタが $TimeoutSeconds 秒のあいだ終了要求を受け付けなかった: $ProcessId$detail"
            }
            Start-Sleep -Milliseconds $PollIntervalMs
        }

        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            throw "エディタが $TimeoutSeconds 秒以内に終了しなかった: $ProcessId"
        }

        Wait-HostPipe -OwnerProcessId $ProcessId -Until Absent
    }
    "status" {
        Assert-ProcessId
        [void](Get-EditorProcess -OwnerProcessId $ProcessId)
        $dialog = Show-StatusDialog -OwnerProcessId $ProcessId
        try {
            Get-DialogText -Dialog $dialog
        }
        finally {
            Close-StatusDialogSafely -Dialog $dialog
        }
    }
    "stop" {
        Assert-ProcessId
        [void](Get-EditorProcess -OwnerProcessId $ProcessId)
        Invoke-HostOperation -OwnerProcessId $ProcessId -Operation "stop"
        Wait-HostPipe -OwnerProcessId $ProcessId -Until Absent
        Wait-StatusKind -OwnerProcessId $ProcessId -Expected "停止済み"
    }
    "start" {
        Assert-ProcessId
        [void](Get-EditorProcess -OwnerProcessId $ProcessId)
        Invoke-HostOperation -OwnerProcessId $ProcessId -Operation "start"
        Wait-HostPipe -OwnerProcessId $ProcessId -Until Present
    }
}
