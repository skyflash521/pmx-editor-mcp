# 検査の集計を確かめる一続きの実行。求める値と出た値をここで突き合わせ、合否を終了コードで
# 返す。集計そのものを確かめるので、集計の外から直に呼ぶ——集計を通して呼ぶと、壊れた集計が
# 自分の落ちたことまで飲み込む。
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
$nonzero = Invoke-Check -Name '非0で終わる題材' -Body { cmd /c exit 3 }
$passed = Invoke-Check -Name '通る題材' -Body { }

$withFailure = Write-CheckSummary -Failed @('落ちる題材') -Skipped @() -Scope '題材' -Ran 1 `
    -Listed 1 -Limit 9
$clean = Write-CheckSummary -Failed @() -Skipped @() -Scope '題材' -Ran 1 -Listed 1 -Limit 9
$withSkip = Write-CheckSummary -Failed @() -Skipped @('走らせない題材') -Scope '題材' -Ran 0 `
    -Listed 1 -Limit 9

# 上限を0にして数え始めると、どの実行も超えた側になる。上限を超えた実行が合格で終わらない
# ことは、これでしか確かめられない——止める仕掛けは、最後に始めた1件が伸びた実行を捕まえない。
Start-CheckClock
$overLimit = Write-CheckSummary -Failed @() -Skipped @() -Scope '題材' -Ran 1 -Listed 1 -Limit 0

# 出来上がりを作る検査が落ちたとき、それを要る検査は始まらない。門が働かなければ、入力の
# 無い状態で走って別の理由で落ちる。
$readyWhenMade = Test-CheckReady -Needs '作った出来上がり' -Produced @('なし', '作った出来上がり')
$readyWhenNot = Test-CheckReady -Needs '作った出来上がり' -Produced @('なし')

# 列が上限の和を超えて止められたとき、結果の出ていない検査の先頭が止められたものになり、
# 後ろは走らせていないものとして数えられる。止める仕組みはこの割り出しに掛かっている。
$queued = @('走った題材', '止められた題材', '始まらない題材')
$stopped = @(Split-LaneResults -Done @(Invoke-Check -Name $queued[0] -Body { }) `
    -Queued $queued -Limit 9)

$rest = @($queued[1], $queued[2])
$empty = @(Split-LaneResults -Done @() -Queued $rest -Limit 9)

# 上限の効き方は3とおりで、どれも別の子プロセスを起こす。互いに依らないので同時に走らせる
# ——順に待つと、検査を並列で走らせている間は子の立ち上がりだけでこの題材が伸びる。
$asked = @(
    @{ Name = '長引く題材'; Command = 'Start-Sleep -Seconds 9'; LimitSeconds = 0.1 }
    @{ Name = '短い題材'; Command = 'exit 3'; LimitSeconds = 9 }
    @{ Name = '上限の無い題材'; Command = 'exit 5'; LimitSeconds = 0 }
)
$ran = @($asked | ForEach-Object {
    $one = $_
    Start-ThreadJob -ArgumentList $PSScriptRoot, $one -ScriptBlock {
        param($Root, $One)

        . (Join-Path $Root 'checks.ps1')
        Invoke-CappedCheck -Name $One.Name -Command $One.Command `
            -LimitSeconds $One.LimitSeconds
    }
} | Receive-Job -Wait -AutoRemoveJob)

$capped = $ran | Where-Object { $_.Name -eq '長引く題材' }
$uncapped = $ran | Where-Object { $_.Name -eq '短い題材' }
$unlimited = $ran | Where-Object { $_.Name -eq '上限の無い題材' }

# 上限は合格の条件である。列ごとに止める仕掛けをすり抜けて走り切った回も、
# 上限に達していれば通さない。
$over = Deny-OverLimitCheck -LimitSeconds 1 -Result ([pscustomobject]@{
    Name = '上限を超えて走り切った題材'; Code = 0; Seconds = 1.5; Log = @(); Skipped = $false })
$within = Deny-OverLimitCheck -LimitSeconds 1 -Result ([pscustomobject]@{
    Name = '上限の中で走り切った題材'; Code = 0; Seconds = 0.5; Log = @(); Skipped = $false })
$leftOver = Deny-OverLimitCheck -LimitSeconds 1 -Result ([pscustomobject]@{
    Name = '走らせていない題材'; Code = 0; Seconds = 9.0; Log = @(); Skipped = $true })

$touching = Test-PathsTouch -Paths @('docs/conventions/verification.md', 'scripts/題材.mjs') `
    -Patterns @('src/*.cs', 'scripts/題材*')
$untouching = Test-PathsTouch -Paths @('docs/conventions/verification.md') `
    -Patterns @('src/*.cs', 'scripts/題材*')

# 手順書の一覧と群の割り当ては、食い違いの向きが2つある。実物の並びを起点に片側だけを崩して
# 渡す——両側を同時に崩すと、片方の割り出しを落としてももう片方が食い違いを返し続ける。
$doc = Join-Path $PSScriptRoot '../docs/conventions/verification.md'
$section = '## 常設の検査'
$names = @(Get-ListedChecks -Path $doc -Section $section)
$fewer = @($names[0..($names.Count - 2)])
$added = @('足した題材')
$grouped = [ordered]@{ '題材の群' = $names }

$unlisted = Get-ListedCheckGap -Path $doc -Section $section -Names (@($names) + $added)
$unowned = Get-ListedCheckGap -Path $doc -Section $section -Names $fewer
$ungrouped = Get-GroupedCheckGap -Grouped $grouped -Names (@($names) + $added)
$unknown = Get-GroupedCheckGap -Grouped $grouped -Names $fewer

$wrong = @()
foreach ($item in @(
    @{ About = '投げて落ちた検査'; Wanted = 1; Got = $failed.Code }
    @{ About = '非0で終わった検査'; Wanted = 3; Got = $nonzero.Code }
    @{ About = '通った検査'; Wanted = 0; Got = $passed.Code }
    @{ About = '落ちた検査がある実行'; Wanted = 1; Got = $withFailure }
    @{ About = 'すべて通った実行'; Wanted = 0; Got = $clean }
    @{ About = '走らせていない検査がある実行'; Wanted = 1; Got = $withSkip }
    @{ About = '上限を超えた実行'; Wanted = 1; Got = $overLimit }
    @{ About = '出来上がりが揃った検査の門'; Wanted = $true; Got = $readyWhenMade }
    @{ About = '出来上がりが揃わない検査の門'; Wanted = $false; Got = $readyWhenNot }
    @{ About = '止められた列が返す件数'; Wanted = 3; Got = $stopped.Count }
    @{ About = '止められた列の1件目'; Wanted = 0; Got = $stopped[0].Code }
    @{ About = '止められた列の2件目'; Wanted = 124; Got = $stopped[1].Code }
    @{ About = '止められた列の3件目'; Wanted = $true; Got = $stopped[2].Skipped }
    @{ About = '何も返さない列が返す件数'; Wanted = 2; Got = $empty.Count }
    @{ About = '何も返さない列の1件目'; Wanted = 124; Got = $empty[0].Code }
    @{ About = '何も返さない列の2件目'; Wanted = $true; Got = $empty[1].Skipped }
    @{ About = '上限を超えた検査'; Wanted = 124; Got = $capped.Code }
    @{ About = '上限の中で終わった検査'; Wanted = 3; Got = $uncapped.Code }
    @{ About = '上限を持たない検査'; Wanted = 5; Got = $unlimited.Code }
    @{ About = '上限に達して走り切った検査'; Wanted = 124; Got = $over.Code }
    @{ About = '上限の中で走り切った検査'; Wanted = 0; Got = $within.Code }
    @{ About = '走らせていない検査'; Wanted = 0; Got = $leftOver.Code }
    @{ About = '入力に当たる道'; Wanted = $true; Got = $touching }
    @{ About = '入力に当たらない道'; Wanted = $false; Got = $untouching }
    @{ About = '手順書に無い名前'; Wanted = 1; Got = $unlisted.Unlisted.Count }
    @{ About = '手順書に無い名前だけを挙げること'; Wanted = 0; Got = $unlisted.Unowned.Count }
    @{ About = 'この実行器に無い名前'; Wanted = 1; Got = $unowned.Unowned.Count }
    @{ About = 'この実行器に無い名前だけを挙げること'; Wanted = 0; Got = $unowned.Unlisted.Count }
    @{ About = 'どの群にも無い検査'; Wanted = 1; Got = $ungrouped.Ungrouped.Count }
    @{ About = 'どの群にも無い検査だけを挙げること'; Wanted = 0; Got = $ungrouped.Unknown.Count }
    @{ About = '検査として在らない名前'; Wanted = 1; Got = $unknown.Unknown.Count }
    @{ About = '検査として在らない名前だけを挙げること'; Wanted = 0
        Got = $unknown.Ungrouped.Count }
)) {
    if ([string]$item.Got -ceq [string]$item.Wanted) { continue }

    $wrong += ($item.About + ': ' + $item.Wanted + ' ではなく ' + $item.Got)
}

if ($wrong.Count -eq 0) { exit 0 }

Write-Host ('集計の結末が違う: ' + ($wrong -join ' / '))
exit 1
