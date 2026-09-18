# 検証手順書の検査の表を読み、1件ずつ走らせて合否を書く仕組み。
# 常設の検査の実行器と実機に触る検査の実行器が共に使う——表の読み方が分かれると、
# どちらかの実行器だけが手順書とずれる。

$script:CheckClockWatch = $null

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

function Start-CheckClock {
    <#
        .SYNOPSIS
        実行の時間を数え始める。実行器は1件目の検査より先にこれを呼ぶ。
    #>
    $script:CheckClockWatch = [System.Diagnostics.Stopwatch]::StartNew()
}

function Get-CheckElapsed {
    <#
        .SYNOPSIS
        数え始めてからの秒数。数え始めていなければ0。
    #>
    if ($null -eq $script:CheckClockWatch) { return 0.0 }

    $script:CheckClockWatch.Elapsed.TotalSeconds
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

function Get-ListedCheckGap {
    <#
        .SYNOPSIS
        実行器が持つ検査と手順書の表の食い違い。手順書に無い名前(Unlisted)と、この実行器に
        無い名前(Unowned)を返す。
    #>
    param([string]$Path, [string]$Section, [string[]]$Names)

    $listed = @(Get-ListedChecks -Path $Path -Section $Section)

    [pscustomobject]@{
        Unlisted = @($Names | Where-Object { $listed -notcontains $_ })
        Unowned = @($listed | Where-Object { $Names -notcontains $_ })
    }
}

function Get-GroupedCheckGap {
    <#
        .SYNOPSIS
        検査と群の割り当ての食い違い。どの群にも無い検査(Ungrouped)と、検査として在らない
        名前(Unknown)を返す。2つの入力を読む検査は両方の群に入れてよい。
    #>
    param($Grouped, [string[]]$Names)

    $listed = @($Grouped.Values | ForEach-Object { $_ })

    [pscustomobject]@{
        Ungrouped = @($Names | Where-Object { $listed -notcontains $_ })
        Unknown = @($listed | Where-Object { $Names -notcontains $_ })
    }
}

function Assert-ListedChecks {
    <#
        .SYNOPSIS
        実行器が持つ検査と、手順書の表に並ぶ検査が一致することを確かめる。片方だけへ足せば
        ここで落ちる。
    #>
    param([string]$Path, [string]$Section, [string[]]$Names)

    $gap = Get-ListedCheckGap -Path $Path -Section $Section -Names $Names
    if ($gap.Unlisted.Count -eq 0 -and $gap.Unowned.Count -eq 0) { return }

    throw ("$Path の $Section の一覧とこの実行器の検査がずれている。手順書に無い: " +
        (($gap.Unlisted -join '・'), '(無し)')[$gap.Unlisted.Count -eq 0] +
        ' / この実行器に無い: ' +
        (($gap.Unowned -join '・'), '(無し)')[$gap.Unowned.Count -eq 0])
}

function Assert-GroupedChecks {
    <#
        .SYNOPSIS
        どの検査も少なくとも1つの群に属し、群の側に知らない名前が無いことを確かめる。入れ忘れた
        検査は誰も走らせないまま合格が出る。
    #>
    param($Grouped, [string[]]$Names)

    $gap = Get-GroupedCheckGap -Grouped $Grouped -Names $Names
    if ($gap.Ungrouped.Count -eq 0 -and $gap.Unknown.Count -eq 0) { return }

    throw ('群の割り当てがずれている。どの群にも無い: ' +
        (($gap.Ungrouped -join '・'), '(無し)')[$gap.Ungrouped.Count -eq 0] +
        ' / 検査に無い: ' + (($gap.Unknown -join '・'), '(無し)')[$gap.Unknown.Count -eq 0])
}

function Test-CheckReady {
    <#
        .SYNOPSIS
        その検査が要る出来上がりが揃っているか。揃っていない検査は始めない——作る側が落ちた後に
        走らせても、入力の無い状態で上限ぶんの時間を使ってから落ちるだけである。
    #>
    param([string]$Needs, [string[]]$Produced)

    $Produced -contains $Needs
}

function Test-PathsTouch {
    <#
        .SYNOPSIS
        変えたものの道のどれかが、渡した形のどれかに当たるか。
    #>
    param([string[]]$Paths, [string[]]$Patterns)

    foreach ($path in $Paths) {
        foreach ($pattern in $Patterns) {
            if ($path -like $pattern) { return $true }
        }
    }

    $false
}

function Get-FellNumbers {
    <#
        .SYNOPSIS
        実行器が書き残した、落ちた項目の番号。何も書かれていなければ空。
    #>
    param([string]$Path)

    $raw = Get-Content $Path -Raw -ErrorAction Ignore
    if (-not $raw) { return @() }

    @($raw.Split(',') | Where-Object { $_.Trim() } | ForEach-Object { [int]$_.Trim() })
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

function Invoke-CappedCheck {
    <#
        .SYNOPSIS
        検査を1件、別のプロセスで走らせて結果を返す。上限を超えた回は子孫ごと終わらせて
        終了コード124で返し、上限が0の回は止めない。
    #>
    param([string]$Name, [string]$Command, [double]$LimitSeconds)

    $said = [System.IO.Path]::GetTempFileName()
    $cried = [System.IO.Path]::GetTempFileName()
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $capped = $false
    try {
        $running = Start-Process pwsh -PassThru -NoNewWindow `
            -ArgumentList @('-NoProfile', '-NonInteractive', '-Command', $Command) `
            -RedirectStandardOutput $said -RedirectStandardError $cried
        $capped = $LimitSeconds -gt 0 -and -not $running.WaitForExit([int]($LimitSeconds * 1000))
        if ($capped) {
            & taskkill /T /F /PID $running.Id 2>&1 | Out-Null
        }

        $running.WaitForExit()
        $code = if ($capped) { 124 } else { $running.ExitCode }
        $log = @(@(Get-Content $said -ErrorAction Ignore) +
            @(Get-Content $cried -ErrorAction Ignore) | Where-Object { $_ })
        if ($capped) {
            $log += "この検査が上限($LimitSeconds 秒)を超えたので、子孫ごと終わらせた。"
        }
    } finally {
        Remove-Item $said, $cried -ErrorAction Ignore
    }

    [pscustomobject]@{
        Name = $Name
        Code = $code
        Seconds = $watch.Elapsed.TotalSeconds
        Log = @($log | ForEach-Object { [string]$_ })
        Skipped = $false
    }
}

function Deny-OverLimitCheck {
    <#
        .SYNOPSIS
        上限に達した検査を不合格にする。上限は合格の条件なので、走り切っても
        超えた回は通さない——列ごとに止める仕掛けだけでは、列の始まりが遅れた回に
        超えた検査が通ってしまう。
    #>
    param($Result, [double]$LimitSeconds)

    if ($Result.Skipped -or $LimitSeconds -le 0 -or $Result.Seconds -le $LimitSeconds) { return $Result }

    $code = if ($Result.Code -ne 0) { $Result.Code } else { 124 }

    [pscustomobject]@{
        Name = $Result.Name
        Code = $code
        Seconds = $Result.Seconds
        Log = @(@($Result.Log) + "この検査が上限($LimitSeconds 秒)に達した。")
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
        列が上限の和を超えて止められたときの、結果を返していない検査の結果。列の外からは
        どの検査が長引いたかを言えないので、止まった時点で結果の出ていない先頭をこれにする。
        所要は分からないので0とし、止めた理由だけを書き出す。
    #>
    param([string]$Name, [int]$Limit)

    [pscustomobject]@{
        Name = $Name
        Code = 124
        Seconds = 0.0
        Log = @("この検査が属する列が、上限の和($Limit 秒)を超えたので止められた。")
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

    $done = @($Done | Where-Object { $_ })

    $done
    $names = @($done | ForEach-Object { $_.Name })
    $rest = @($Queued | Where-Object { $names -notcontains $_ })
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

    # 上限を超えた実行は合格にしない。検査ごと・列ごとに止める仕掛けは、始める前と列の途中
    # でしか働かないので、最後に始めた1件が伸びた実行と、止める仕掛けを通らない検査が伸びた
    # 実行は、ここでしか捕まえられない。
    $elapsed = Get-CheckElapsed
    $took = '{0:0.0}秒 / 上限 {1}秒' -f $elapsed, $Limit
    $over = $elapsed -gt $Limit

    Write-Host ''
    if ($over) { Write-Host ("上限を超えた: $took") }
    if ($Skipped.Count -gt 0) { Write-Host ('走らせていない: ' + ($Skipped -join '・')) }
    if ($Failed.Count -gt 0) { Write-Host ('不合格: ' + ($Failed -join '・')) }
    if ($Failed.Count -gt 0 -or $Skipped.Count -gt 0 -or $over) { return 1 }

    Write-Host ("$Scope の $Ran 件をすべて合格($took・全部で $Listed 件)")

    0
}
