# 検証手順書の検査の表を読み、1件ずつ走らせて合否を書く仕組み。
# 常設の検査の実行器と実機に触る検査の実行器が共に使う——表の読み方が分かれると、
# どちらかの実行器だけが手順書とずれる。

<#
    1回の実行へ与える時間の上限の秒数。
#>
$CheckBudgetSeconds = 360

$script:CheckBudgetWatch = $null

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

function Test-CheckBudgetSpent {
    <#
        .SYNOPSIS
        この実行が上限を使い切ったかどうか。実行器はこれが真になった時点で、残りの検査を
        始めない——始めれば、上限を超えたぶんがさらに伸びる。
    #>
    (Get-CheckBudgetElapsed) -gt $CheckBudgetSeconds
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

function Invoke-Check {
    <#
        .SYNOPSIS
        検査を1件走らせ、落ちたらその名前を返す。通れば何も返さない。
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
    $took = '{0,5:0.0}秒' -f $watch.Elapsed.TotalSeconds

    if ($code -eq 0) {
        Write-Host "OK   $took  $Name"
        return $null
    }

    Write-Host "NG   $took  $Name (終了コード $code)"
    # 誤りの記録をパイプへ流すと、停止の設定の下では書き出す側で終了エラーになる。文字列にして出す。
    foreach ($line in @($log)) { Write-Host ('     ' + [string]$line) }

    return $Name
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
    param([string[]]$Failed, [string[]]$Skipped, [string]$Scope, [int]$Ran, [int]$Listed)

    $elapsed = Get-CheckBudgetElapsed
    $took = '{0:0.0}秒 / 上限 {1}秒' -f $elapsed, $CheckBudgetSeconds
    $spent = Test-CheckBudgetSpent

    Write-Host ''
    if ($spent) { Write-Host ("時間の上限を超えた: $took") }
    if ($Skipped.Count -gt 0) { Write-Host ('走らせていない: ' + ($Skipped -join '・')) }
    if ($Failed.Count -gt 0) { Write-Host ('不合格: ' + ($Failed -join '・')) }
    if ($Failed.Count -gt 0 -or $Skipped.Count -gt 0 -or $spent) { return 1 }

    Write-Host ("$Scope の $Ran 件をすべて合格($took・全部で $Listed 件)")

    0
}
