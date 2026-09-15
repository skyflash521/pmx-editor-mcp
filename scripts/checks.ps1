# 検証手順書の検査の表を読み、1件ずつ走らせて合否を書く仕組み。
# 常設の検査の実行器と実機に触る検査の実行器が共に使う——表の読み方が分かれると、
# どちらかの実行器だけが手順書とずれる。

<#
    常設の検査と実機に触る検査を合算した上限の秒数。検査ごとの配分の合計がこの値であることは、
    配分の照合が見る。
#>
$TotalBudgetSeconds = 360

<#
    実機に触る検査の全体へ与える秒数。検査ごとの配分は実機を測ってから置くので、いまはこの
    合計だけを持つ。合算の上限から常設の配分を引いた残りである。
#>
$LiveBudgetSeconds = 233

$script:CheckBudgetWatch = $null

<#
    出来上がりを要さない検査の印。どちらの実行器も、この印の検査は何も待たずに走らせる。
#>
$noArtifact = 'なし'

<#
    組み立てが作る出来上がりの印。
#>
$buildOutput = 'ビルド成果物'

<#
    除外一覧の導出が作る出来上がりの印。
#>
$exclusionList = '除外一覧'

<#
    実機の前置が作る出来上がりの印。配置済みのホストと、組み立て済みのブリッジを指す。
#>
$liveSetup = '実機の前置'

function Start-CheckBudget {
    <#
        .SYNOPSIS
        実行の時間を数え始める。実行器は1件目の検査より先にこれを呼ぶ。
    #>
    $script:CheckBudgetWatch = [System.Diagnostics.Stopwatch]::StartNew()
}

function Get-CheckBudgetElapsed {
    <#
        .SYNOPSIS
        数え始めてからの秒数。数え始めていなければ0。
    #>
    if ($null -eq $script:CheckBudgetWatch) { return 0.0 }

    $script:CheckBudgetWatch.Elapsed.TotalSeconds
}

function Get-ListedChecks {
    <#
        .SYNOPSIS
        検証手順書の、その節に並ぶ検査の名前。
    #>
    param([string]$Path, [string]$Section)

    $lines = Get-Content $Path
    $from = [array]::IndexOf($lines, $Section)
    if ($from -lt 0) { throw "$Path に $Section の節が無い。" }

    $rest = $lines[($from + 1)..($lines.Count - 1)]
    $to = ($rest | Select-String -Pattern '^## ' | Select-Object -First 1).LineNumber
    if ($to) { $rest = $rest[0..($to - 2)] }

    $rest |
        Select-String -Pattern '^\| ([^|]+?) \| ' |
        ForEach-Object { $_.Matches[0].Groups[1].Value } |
        Where-Object { $_ -ne '検査' }
}

function Assert-ListedChecks {
    <#
        .SYNOPSIS
        実行器が持つ検査と、手順書の表に並ぶ検査が一致することを確かめる。片方だけへ足せば
        ここで落ちる。
    #>
    param([string]$Path, [string]$Section, [string[]]$Names)

    $listed = @(Get-ListedChecks -Path $Path -Section $Section)
    $missing = @($Names | Where-Object { $listed -notcontains $_ })
    $extra = @($listed | Where-Object { $Names -notcontains $_ })
    if ($missing.Count -eq 0 -and $extra.Count -eq 0) { return }

    throw ("$Path の $Section の一覧とこの実行器の検査がずれている。手順書に無い: " +
        (($missing -join '・'), '(無し)')[$missing.Count -eq 0] +
        ' / この実行器に無い: ' +
        (($extra -join '・'), '(無し)')[$extra.Count -eq 0])
}

function Assert-GroupedChecks {
    <#
        .SYNOPSIS
        どの検査も少なくとも1つの群に属し、群の側に知らない名前が無いことを確かめる。入れ忘れた
        検査は誰も走らせないまま合格が出る。2つの入力を読む検査は両方の群に入れてよい。
    #>
    param($Grouped, [string[]]$Names)

    $listed = @($Grouped.Values | ForEach-Object { $_ })
    $missing = @($Names | Where-Object { $listed -notcontains $_ })
    $unknown = @($listed | Where-Object { $Names -notcontains $_ })
    if ($missing.Count -eq 0 -and $unknown.Count -eq 0) { return }

    throw ('群の割り当てがずれている。どの群にも無い: ' +
        (($missing -join '・'), '(無し)')[$missing.Count -eq 0] +
        ' / 検査に無い: ' + (($unknown -join '・'), '(無し)')[$unknown.Count -eq 0])
}

function Test-CheckReady {
    <#
        .SYNOPSIS
        その検査が要る出来上がりが揃っているか。揃っていない検査は始めない——作る側が落ちた後に
        走らせても、入力の無い状態で配分ぶんの時間を使ってから落ちるだけである。
    #>
    param([string]$Needs, [string[]]$Produced)

    $Produced -contains $Needs
}

function Invoke-Check {
    <#
        .SYNOPSIS
        検査を1件走らせ、名前と終了コードと所要と書き出したものを返す。**ここでは書かない**
        ——並列に走る検査が書いた先から出すと、どの行がどの検査のものか読み手に決められなくなる。
        書くのは Write-CheckResult で、呼び出し側が検査ごとにまとめて呼ぶ。
    #>
    param([string]$Name, [scriptblock]$Body)

    $global:LASTEXITCODE = 0
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $log = & $Body 2>&1
        $code = $LASTEXITCODE
    } catch {
        $log = $_
        $code = 1
    }

    # 誤りの記録をそのまま持ち回すと、書き出す側が停止の設定で終了エラーになる。文字列にして返す。
    [pscustomobject]@{
        Name = $Name
        Code = $code
        Seconds = $watch.Elapsed.TotalSeconds
        Log = @(@($log) | ForEach-Object { [string]$_ })
        Skipped = $false
    }
}

function New-SkippedCheck {
    <#
        .SYNOPSIS
        走らせなかった検査の結果。要る出来上がりが揃わなかった検査がこれになる。
    #>
    param([string]$Name)

    [pscustomobject]@{
        Name = $Name
        Code = 0
        Seconds = 0.0
        Log = @()
        Skipped = $true
    }
}

function New-StoppedCheck {
    <#
        .SYNOPSIS
        列が配分の和を超えて止められたときの、結果を返していない検査の結果。列の外からは
        どの検査が長引いたかを言えないので、止まった時点で結果の出ていない先頭をこれにする。
        所要は分からないので0とし、止めた理由だけを書き出す。
    #>
    param([string]$Name, [int]$Limit)

    [pscustomobject]@{
        Name = $Name
        Code = 124
        Seconds = 0.0
        Log = @("この検査が属する列が、配分の和($Limit 秒)を超えたので止められた。")
        Skipped = $false
    }
}

function Split-LaneResults {
    <#
        .SYNOPSIS
        列が返した結果と、その列に並べた検査の名前から、報告する結果の並びを作る。止められた
        列では結果の出ていない検査が残るので、その先頭を止められたものとし、後ろは走らせて
        いないものとして数える。
    #>
    param($Done, [string[]]$Queued, [int]$Limit)

    @($Done)
    $rest = @($Queued | Where-Object { @($Done).Name -notcontains $_ })
    if ($rest.Count -eq 0) { return }

    New-StoppedCheck -Name $rest[0] -Limit $Limit
    foreach ($name in @($rest | Select-Object -Skip 1)) { New-SkippedCheck -Name $name }
}

function Write-CheckResult {
    <#
        .SYNOPSIS
        1件の結果を書く。走らせた順ではなく、検査ごとにまとめて書くための入口である。
    #>
    param($Result)

    if ($Result.Skipped) { return }

    $took = '{0,5:0.0}秒' -f $Result.Seconds
    if ($Result.Code -eq 0) {
        Write-Host ("OK   $took  " + $Result.Name)
        return
    }

    Write-Host ("NG   $took  " + $Result.Name + " (終了コード " + $Result.Code + ")")
    foreach ($line in $Result.Log) { Write-Host ('     ' + $line) }
}

function Write-CheckSummary {
    <#
        .SYNOPSIS
        走らせた結末を書き、終わらせる終了コードを返す。落ちた検査も走らせていない検査も
        無ければ0、あれば1とする。**合格の行には、何を何件走らせたかを書く**——一部だけを走らせた
        結末が全部を通した結末と同じ文面になると、確かめていない検査を確かめたものとして読める。
        呼び名は呼び出し側が渡す。この部品は常設の検査の実行器も実機に触る検査の実行器も使うので、
        片方の語彙をここへ綴ると、もう片方が事実と違う文面を出す。
    #>
    param([string[]]$Failed, [string[]]$Skipped, [string]$Scope, [int]$Ran, [int]$Listed,
        [int]$Limit)

    # 持ち分を超えた実行は合格にしない。検査ごと・列ごとに止める仕掛けは、始める前と列の途中
    # でしか働かないので、最後に始めた1件が伸びた実行と、止める仕掛けを通らない検査が伸びた
    # 実行は、ここでしか捕まえられない。
    $elapsed = Get-CheckBudgetElapsed
    $took = '{0:0.0}秒 / 持ち分 {1}秒' -f $elapsed, $Limit
    $over = $elapsed -gt $Limit

    Write-Host ''
    if ($over) { Write-Host ("持ち分を超えた: $took") }
    if ($Skipped.Count -gt 0) { Write-Host ('走らせていない: ' + ($Skipped -join '・')) }
    if ($Failed.Count -gt 0) { Write-Host ('不合格: ' + ($Failed -join '・')) }
    if ($Failed.Count -gt 0 -or $Skipped.Count -gt 0 -or $over) { return 1 }

    Write-Host ("$Scope の $Ran 件をすべて合格($took・全部で $Listed 件)")

    0
}
