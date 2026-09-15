# 実機動作確認。ホストを配置したうえで、待受と接続の振る舞いを実機のエディタで確かめる。
# 合否は、実行したコマンドとクライアントが返す出力と終了コードだけで決める——画面の見た目は
# 材料にしない。
#
# エディタの状態を変える件は、件ごとに起こして閉じる。持ち越すと、落ちた件の後始末が次の件の
# 開始条件を崩し、どの件が何を確かめたのかが実行ごとに変わる。
#
# 読むだけで変えない件は1つを共有する。崩す後始末が起きないので持ち越す状態が無く、起こし直しは
# 待ち時間を増やすだけである。共有する側は Get-SharedEditor、自分で起こす側は Start-Editor を
# 使い、どちらであるかを件ごとに決める。
[CmdletBinding()]
param(
    # エディタとホストの操作役。差し替えられるのは、この実行器そのものを実機のエディタ無しで
    # 確かめるためである——既定は実物で、開くのは実行時の引数に限る。
    [string]$Control = 'scripts/host-control.ps1',

    # 待受へ繋いで応答を確かめる確認クライアント。
    [string]$Client = 'scripts/e2e-check.mjs',

    # ホストを配置する手順。
    [string]$Deploy = 'scripts/deploy-host.ps1'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 外部コマンドの非0終了は終了エラーにしない。終了コードを見て自分で合否にする。
$PSNativeCommandUseErrorActionPreference = $false

Set-Location (Split-Path -Parent $PSScriptRoot)

$control = $Control
$client = $Client
$deploy = $Deploy

# ホストが待受に使うパイプ名の付け方。ホスト側の実装が定める。
$PipePrefix = 'pmx-editor-mcp-'

# 版が合わないハンドシェイクへホストが返すエラーコード。共通契約が定める。
$ProtocolMismatchCode = -32001

# 確認クライアントが、契約どおりの切断を見届けたときに書く文。
$ClosedAfterDisconnectingError =
    "切断が要るエラー応答($ProtocolMismatchCode)のあと、ホストが契約どおり接続を切りました。"

# 確認クライアントが、接続を保ったままホスト側から切られたときに書く文。
$ClosedByHost = 'ホストが接続を切りました。'

# 確認クライアントが、接続を保ったまま待ちに入ったときに書く文。この文より前にエディタを
# 触ると、接続が確立する前の一瞬を見ることになる。
$Holding = '接続を保持しています。'

# 待受が無いパイプへ繋ごうとしたときに、確認クライアントが書く断りの書き出し。
$CannotConnect = '接続または送受信に失敗しました:'

# デバッグ用の入口を開く環境変数。エディタの起動時に読まれる。
$DebugHooksName = 'PMX_EDITOR_MCP_DEBUG_HOOKS'

# 応答サイズ予算の環境変数。設定したまま起動すると、期待する既定値が返らなくなる。
$BudgetName = 'PMX_EDITOR_MCP_BUDGET_CHARS'

# 常駐コネクタを失効させる入口。デバッグ用の入口を開いたときだけ受け付ける。
$ExpireMethod = 'debug_expire_connector'

# ホストのログのうち、起動を1回ぶん記す行の書き出し。
$StartedMark = 'プラグインを起動した:'

# 常駐コネクタの取得と失効を記す行に共通する書き出し。
$ConnectorMark = 'Cプラグインコネクタの'

# 取り直しが起きたときに、その書き出しへ続いて並ぶ言葉。
$RenewalMarks = @('取得', '失効', '取得')

# ホストのログが1行の頭に置く時刻の書き方。
$LogTimeFormat = 'yyyy-MM-dd HH:mm:ss.fff'

# 確認クライアントが待ちへ入るのと終わるのを待つ上限の秒数。操作役の既定と同じ値を採る——
# どちらも正常な動作を刻む値ではなく、応答しなくなった相手を諦めるための値である。
$WaitSeconds = 40

function Get-PipeName {
    param([int]$EditorProcessId)

    $PipePrefix + $EditorProcessId
}

function Test-PipePresent {
    param([int]$EditorProcessId)

    $wanted = Get-PipeName -EditorProcessId $EditorProcessId

    @(Get-ChildItem '\\.\pipe\' | Where-Object { $_.Name -eq $wanted }).Count -gt 0
}

function Invoke-Control {
    <#
        .SYNOPSIS
        操作役を呼ぶ。状態を変える操作は、結果が観測できるようになるまで待って戻る。
    #>
    param([string]$Action, [int]$EditorProcessId, [switch]$Global)

    if ($Global) { return & $control -Action $Action }

    & $control -Action $Action -ProcessId $EditorProcessId
}

function Set-EnvironmentValue {
    <#
        .SYNOPSIS
        環境変数を与えた値にする。値が無ければ変数ごと取り除く——**空の値を置くのは、設定して
        いないことにはならない**。ホストは空を受理できない値として読み、待受を始めない。
    #>
    param([string]$Name, $Value)

    if ([string]::IsNullOrEmpty($Value)) {
        Remove-Item -Path "Env:$Name" -ErrorAction Ignore
        return
    }

    Set-Item -Path "Env:$Name" -Value $Value
}

function Start-Editor {
    <#
        .SYNOPSIS
        エディタを1つ起こし、そのプロセスIDを返す。応答サイズ予算の環境変数は、ホストが起動時に
        一度だけ読むので、設定したまま起こさないよう外してから起こす。
    #>
    param([switch]$WithDebugHooks)

    $budget = [System.Environment]::GetEnvironmentVariable($BudgetName)
    $hooks = [System.Environment]::GetEnvironmentVariable($DebugHooksName)
    try {
        Set-EnvironmentValue -Name $BudgetName -Value $null
        $opened = $null
        if ($WithDebugHooks) { $opened = '1' }
        Set-EnvironmentValue -Name $DebugHooksName -Value $opened

        [int](Invoke-Control -Action 'launch' -Global)
    } finally {
        Set-EnvironmentValue -Name $BudgetName -Value $budget
        Set-EnvironmentValue -Name $DebugHooksName -Value $hooks
    }
}

$script:SharedEditor = 0

function Get-SharedEditor {
    <#
        .SYNOPSIS
        読むだけの件が共有するエディタ。初めて要るときに起こし、実行の終わりまで開いたままに
        する。起こし方は Start-Editor と同じで、既定の設定で動くものである。
    #>
    if ($script:SharedEditor -eq 0) { $script:SharedEditor = Start-Editor }

    $script:SharedEditor
}

function Clear-SharedEditor {
    <#
        .SYNOPSIS
        共有しているものを閉じた件が、閉じたことを知らせる。閉じ終えた相手をもう一度閉じにいかない
        ようにする。
    #>
    $script:SharedEditor = 0
}

function Close-SharedEditor {
    <#
        .SYNOPSIS
        共有したエディタを閉じる。起こしていなければ何もしない。
    #>
    if ($script:SharedEditor -eq 0) { return }

    Stop-Editor -EditorProcessId $script:SharedEditor
    $script:SharedEditor = 0
}

function Stop-Editor {
    param([int]$EditorProcessId)

    if (-not (Get-Process -Id $EditorProcessId -ErrorAction Ignore)) { return }

    Invoke-Control -Action 'close' -EditorProcessId $EditorProcessId | Out-Null
}

function Invoke-Client {
    <#
        .SYNOPSIS
        確認クライアントを走らせ、書き出したものと終了コードを返す。要求を省略したときは
        クライアント自身が応答の中身まで確かめるので、終了コードだけで合否を判じてよい。
    #>
    param([int]$EditorProcessId, [string[]]$Requests = @())

    $said = node $client $EditorProcessId @Requests 2>&1
    $code = $LASTEXITCODE
    $global:LASTEXITCODE = 0

    [pscustomobject]@{ Code = $code; Said = (@($said) -join "`n") }
}

function Read-HoldingClient {
    param($Held)

    (Get-Content $Held.Said -Raw -Encoding UTF8) + (Get-Content $Held.Noise -Raw -Encoding UTF8)
}

function Stop-HoldingClient {
    param($Held)

    if (-not $Held.Process.HasExited) { $Held.Process.Kill() }
    Remove-Item $Held.Said, $Held.Noise -ErrorAction Ignore
}

function Start-HoldingClient {
    <#
        .SYNOPSIS
        接続を保ったままの確認クライアントを起こし、待ちに入るまで見届けて返す。接続が確立する
        前にエディタを触ると、切断を待つ側が居ないまま状態が変わってしまう。
    #>
    param([int]$EditorProcessId)

    $said = [System.IO.Path]::GetTempFileName()
    $noise = [System.IO.Path]::GetTempFileName()
    $running = Start-Process -FilePath 'node' -NoNewWindow -PassThru `
        -ArgumentList @($client, $EditorProcessId, '--hold') `
        -RedirectStandardOutput $said -RedirectStandardError $noise

    $held = [pscustomobject]@{ Process = $running; Said = $said; Noise = $noise }
    $deadline = (Get-Date).AddSeconds($WaitSeconds)
    while ((Get-Date) -lt $deadline) {
        if ((Read-HoldingClient -Held $held) -match [regex]::Escape($Holding)) { return $held }
        if ($running.HasExited) { break }
        Start-Sleep -Milliseconds 200
    }

    $seen = Read-HoldingClient -Held $held
    Stop-HoldingClient -Held $held
    throw "接続を保つクライアントが $WaitSeconds 秒以内に待ちへ入らなかった: $seen"
}

function Wait-HoldingClient {
    <#
        .SYNOPSIS
        接続を保つクライアントが終わるのを待ち、書き出したものと終了コードを返す。終わらない
        ことも結果なので、待ちきれなければそうと分かる形で失敗させる。
    #>
    param($Held)

    try {
        if (-not $Held.Process.WaitForExit($WaitSeconds * 1000)) {
            throw ("接続を保つクライアントが $WaitSeconds 秒以内に終わらなかった: " +
                (Read-HoldingClient -Held $Held))
        }

        [pscustomobject]@{ Code = $Held.Process.ExitCode; Said = (Read-HoldingClient -Held $Held) }
    } finally {
        Stop-HoldingClient -Held $Held
    }
}

function Get-HostLogLines {
    <#
        .SYNOPSIS
        今回の起動以降に書かれた、その印を持つログの行。ログはプロセスIDごとのファイルへ追記する
        ので、同じプロセスIDが再び割り当てられると前回の記録も残る。起動の時刻で切って数える。
    #>
    param([int]$EditorProcessId, [string]$Mark)

    $path = Join-Path $env:TEMP "pmx-editor-mcp-host-$EditorProcessId.log"
    if (-not (Test-Path $path)) { throw "ホストのログが無い: $path" }

    $since = (Get-Process -Id $EditorProcessId).StartTime
    Get-Content $path -Encoding UTF8 |
        Where-Object { $_ -match [regex]::Escape($Mark) } |
        Where-Object {
            $at = [datetime]::MinValue
            $head = $_.Substring(0, [Math]::Min($_.Length, $LogTimeFormat.Length))
            [datetime]::TryParseExact($head, $LogTimeFormat, [cultureinfo]::InvariantCulture,
                [System.Globalization.DateTimeStyles]::None, [ref]$at) -and $at -ge $since
        }
}

function Assert-Client {
    <#
        .SYNOPSIS
        確認クライアントの結果が期待どおりであることを確かめる。
    #>
    param($Ran, [int]$Code, [string]$Says, [string]$What)

    if ($Ran.Code -ne $Code) {
        throw "${What}: 終了コードが $Code ではなく $($Ran.Code)。$($Ran.Said)"
    }
    if ($Says -and $Ran.Said -notmatch [regex]::Escape($Says)) {
        throw "${What}: 「$Says」を言っていない。$($Ran.Said)"
    }
}

function Assert-StoppedKind {
    <#
        .SYNOPSIS
        稼働状態の表示が停止済みを名乗ることを確かめる。
    #>
    param([int]$EditorProcessId, [string]$What)

    $state = (Invoke-Control -Action 'status' -EditorProcessId $EditorProcessId) -join "`n"
    if ($state -notmatch '状態:\s*停止済み') {
        throw "${What}: 状態区分が停止済みではない。$state"
    }
}

$cases = [ordered]@{}

$cases['配置'] = {
    # 配置そのものが、動いているエディタを閉じるところから始まる。閉じ残しがあれば落ちる。
    & $deploy | Out-Null
}

$cases['疎通'] = {
    # 繋いでパイプ名を確かめるだけで、エディタもホストも変えない。
    $editor = Get-SharedEditor
    Assert-Client -Ran (Invoke-Client -EditorProcessId $editor) -Code 0 -What '疎通'
}

$cases['起動の記録'] = {
    $editor = Start-Editor
    try {
        $started = @(Get-HostLogLines -EditorProcessId $editor -Mark $StartedMark)
        if ($started.Count -ne 1) {
            throw "起動の記録が1行ではなく $($started.Count) 行: $($started -join ' / ')"
        }
    } finally {
        Stop-Editor -EditorProcessId $editor
    }
}

$cases['エディタ2つ'] = {
    # 2つへ同時に繋がることを見る。1つ目は共有のもので足りる——どちらも変えない。
    $editors = @()
    $own = 0
    try {
        $editors += Get-SharedEditor
        $own = Start-Editor
        $editors += $own
        foreach ($editor in $editors) {
            Assert-Client -Ran (Invoke-Client -EditorProcessId $editor) -Code 0 `
                -Says (Get-PipeName -EditorProcessId $editor) -What "2つのうち $editor への疎通"
        }
    } finally {
        if ($own -ne 0) { Stop-Editor -EditorProcessId $own }
    }
}

$cases['版の食い違い'] = {
    # 断られるのは繋ぎに来た側だけで、待受もホストの状態も変わらない。
    $editor = Get-SharedEditor

    # 版が合わなければホストは断って接続を切る。切ったことは、こちらから閉じずに待てば分かる。
    # 求める言い分はコードを名指ししているので、コードが違えばこの1つで落ちる。
    $ran = Invoke-Client -EditorProcessId $editor -Requests @('handshake', '{"protocol":2}')
    Assert-Client -Ran $ran -Code 0 -Says $ClosedAfterDisconnectingError -What '版の食い違い'
}

$cases['パイプの権限'] = {
    # 待受に掛かっている規則を読むだけで、エディタもホストも変えない。
    $editor = Get-SharedEditor
    $rules = @(Invoke-Control -Action 'acl' -EditorProcessId $editor)
    $ours = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    $described = ($rules | ForEach-Object {
            "$($_.IdentityReference)/$($_.AccessControlType)/$($_.PipeAccessRights)"
        }) -join '・'
    if ($rules.Count -ne 1) { throw "権限の規則が1件ではなく $($rules.Count) 件: $described" }

    $only = $rules[0]
    if ($only.AccessControlType -ne 'Allow' -or
        $only.IdentityReference.Value -ne $ours -or
        $only.PipeAccessRights -ne 'FullControl') {
        throw "権限の規則が現在ユーザー($ours)のFullControlの許可ではない: $described"
    }
}

$cases['エディタの終了'] = {
    # 終わらせる相手はどのエディタでもよい。共有しているものを使う——これより後の件は自分で
    # 起こすので、ここで閉じても持ち越すものが無い。
    $editor = Get-SharedEditor
    try {
        $held = Start-HoldingClient -EditorProcessId $editor
        Stop-Editor -EditorProcessId $editor
        Assert-Client -Ran (Wait-HoldingClient -Held $held) -Code 0 -Says $ClosedByHost `
            -What '接続を保ったままの終了'
        if (Test-PipePresent -EditorProcessId $editor) { throw '終了してもパイプが残っている。' }
    } finally {
        Stop-Editor -EditorProcessId $editor
        Clear-SharedEditor
    }
}

$cases['コネクタの取り直し'] = {
    $editor = Start-Editor -WithDebugHooks
    try {
        $ran = Invoke-Client -EditorProcessId $editor `
            -Requests @('handshake', '{"protocol":1}', $ExpireMethod)
        Assert-Client -Ran $ran -Code 0 -Says '"renewed":true' -What 'コネクタの失効'

        # 取得・失効・取得の順に3行だけ並ぶ。取り直していなければ、末尾の取得が現れない。
        $marks = @(Get-HostLogLines -EditorProcessId $editor -Mark $ConnectorMark)
        $seen = @($marks | ForEach-Object {
                if ($_ -match ($ConnectorMark + '(..)')) { $Matches[1] } else { '不明' } })
        if (($seen -join '') -ne ($RenewalMarks -join '')) {
            throw ("コネクタの記録が " + ($RenewalMarks -join '・') +
                " の3行ではない: $($marks -join ' / ')")
        }
    } finally {
        Stop-Editor -EditorProcessId $editor
    }
}

$cases['停止と開始'] = {
    $editor = Start-Editor
    try {
        # 閉じずに停止と開始を繰り返せること。1回では、2度目の開始を受け付けない実装が通る。
        foreach ($round in 1..2) {
            Invoke-Control -Action 'stop' -EditorProcessId $editor | Out-Null
            if (Test-PipePresent -EditorProcessId $editor) {
                throw "$round 回目の停止でパイプが残っている。"
            }

            Assert-Client -Ran (Invoke-Client -EditorProcessId $editor) -Code 1 `
                -Says $CannotConnect -What "$round 回目の停止後の接続"
            Assert-StoppedKind -EditorProcessId $editor -What "$round 回目の停止"

            Invoke-Control -Action 'start' -EditorProcessId $editor | Out-Null
            Assert-Client -Ran (Invoke-Client -EditorProcessId $editor) -Code 0 `
                -Says (Get-PipeName -EditorProcessId $editor) -What "$round 回目の開始後の接続"
        }
    } finally {
        Stop-Editor -EditorProcessId $editor
    }
}

$cases['停止時の接続'] = {
    $editor = Start-Editor
    try {
        $held = Start-HoldingClient -EditorProcessId $editor
        Invoke-Control -Action 'stop' -EditorProcessId $editor | Out-Null
        Assert-Client -Ran (Wait-HoldingClient -Held $held) -Code 0 -Says $ClosedByHost `
            -What '接続を保ったままの停止'
        Assert-StoppedKind -EditorProcessId $editor -What '接続を保ったままの停止'
    } finally {
        Stop-Editor -EditorProcessId $editor
    }
}

$failed = @()

# 確認クライアントが書くのはUTF-8なので、端末の設定のまま読むと合否の手がかりが崩れる。
$spoken = [Console]::OutputEncoding
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
try {
    foreach ($name in $cases.Keys) {
        try {
            & $cases[$name]
            Write-Host "OK   $name"
        } catch {
            Write-Host "NG   $name"
            Write-Host ('     ' + [string]$_)
            $failed += $name
        }
    }
} finally {
    [Console]::OutputEncoding = $spoken
    # 共有したエディタは実行の終わりまで開いたままなので、ここで閉じる。閉じ残すと、次の実行の
    # 「配置」が落ちる——配置は動いているエディタを閉じるところから始まる。
    Close-SharedEditor
}

Write-Host ''
if ($failed.Count -gt 0) {
    Write-Host ('不合格: ' + ($failed -join '・'))
    exit 1
}

Write-Host 'すべて合格'
