# 検査の集計を確かめるための一続きの実行。落ちる本体・通る本体・非0で終わる本体を渡し、返る
# 名前と、結末ごとの終了コードと、一覧と群の食い違いを向きごとに咎めるかを1行で書き出す。
# 集計そのものを確かめるので、集計の外から直に呼ぶ——集計を通して呼ぶと、壊れた集計が自分の
# 落ちたことまで飲み込む。
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 外部コマンドの非0終了は終了エラーにしない。集計が終了コードを見て落とすことを確かめるので、
# その手前で止まっては確かめられない。
$PSNativeCommandUseErrorActionPreference = $false

. (Join-Path $PSScriptRoot 'checks.ps1')

# 投げて落ちる本体と、非0で終わる外部コマンドの本体は、集計の別の道を通る。常設の検査の多くは
# 後者なので、前者だけでは大半の検査の落ちが拾われることを確かめられない。
$failed = Invoke-Check -Name '落ちる題材' -Body { throw '作った失敗である。' }
$nonzero = Invoke-Check -Name '非0で終わる題材' -Body { pwsh -NoProfile -Command 'exit 3' }
$passed = Invoke-Check -Name '通る題材' -Body { }

$withFailure = Write-CheckSummary -Failed @('落ちる題材') -Skipped @() -Scope '題材' -Ran 1 -Listed 1
$clean = Write-CheckSummary -Failed @() -Skipped @() -Scope '題材' -Ran 1 -Listed 1
$withSkip = Write-CheckSummary -Failed @() -Skipped @('走らせない題材') -Scope '題材' -Ran 0 -Listed 1

# 上限を0にして数え始めると、どの実行も使い切った側になる。時間を超えた実行が合格で終わらない
# ことは、これでしか確かめられない。
$CheckBudgetSeconds = 0
Start-CheckBudget
$overBudget = Write-CheckSummary -Failed @() -Skipped @() -Scope '題材' -Ran 1 -Listed 1

function Test-Complaint {
    <#
        .SYNOPSIS
        渡した本体が、求める向きを名指しして投げるかどうかを綴りで返す。投げたことだけを見ると、
        引数の束縛で投げた回まで同じ綴りになり、狙った照合が一度も通らないまま揃ってしまう。
    #>
    param([string]$Wanted, [scriptblock]$Body)

    try {
        & $Body
    } catch {
        if ([string]$_ -match [regex]::Escape($Wanted)) { return 'とがめる' }

        return 'ちがう理由'
    }

    'とがめない'
}

# 手順書の一覧と群の割り当ては、食い違いの向きが2つある。実物の並びを起点に片側だけを崩して
# 渡す——両側を同時に崩すと、片方の照合を落としてももう片方が投げ続けて気づけない。
$doc = Join-Path $PSScriptRoot '../docs/conventions/verification.md'
$section = '## 常設の検査'
$names = @(Get-ListedChecks -Path $doc -Section $section)
$fewer = @($names[0..($names.Count - 2)])
$dropped = $names[-1]

# 手順書に無い名前を実行器が持つとき。
$listedMissing = Test-Complaint -Wanted '手順書に無い: 手順書に無い題材' -Body {
    Assert-ListedChecks -Path $doc -Section $section -Names (@($names) + '手順書に無い題材')
}

# 手順書に並ぶだけで実行器が持たない名前があるとき。
$listedExtra = Test-Complaint -Wanted ("この実行器に無い: " + $dropped) -Body {
    Assert-ListedChecks -Path $doc -Section $section -Names $fewer
}

# どの群にも入っていない検査があるとき。
$groupedMissing = Test-Complaint -Wanted 'どの群にも無い: 群に無い題材' -Body {
    Assert-GroupedChecks -Grouped ([ordered]@{ '題材の群' = $names }) `
        -Names (@($names) + '群に無い題材')
}

# 群の側に、検査として在らない名前があるとき。
$groupedUnknown = Test-Complaint -Wanted ("検査に無い: " + $dropped) -Body {
    Assert-GroupedChecks -Grouped ([ordered]@{ '題材の群' = $names }) -Names $fewer
}

# 走らせた結果は、名前と終了コードの組で綴る。書き出しは別の入口が持つので、ここで見るのは
# 返った値だけである。
$ran = ($failed.Name + ':' + $failed.Code) + '|' +
    ($nonzero.Name + ':' + $nonzero.Code) + '|' + ($passed.Name + ':' + $passed.Code)

"結果: $ran|$withFailure|$clean|$withSkip|$overBudget|" +
    "$listedMissing|$listedExtra|$groupedMissing|$groupedUnknown"
