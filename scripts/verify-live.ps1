# 検証手順書が定める実機に触る検査を、この1本で走らせる。
# どれも実機のエディタを起こして操作するので、常設の検査とは分けてある——ログオンした対話的な
# デスクトップが要り、1本あたり分単位で掛かる。
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 外部コマンドの非0終了は終了エラーにしない。ここでは出力と終了コードをそのまま見て合否にする。
$PSNativeCommandUseErrorActionPreference = $false

. (Join-Path $PSScriptRoot 'checks.ps1')

Set-Location (Split-Path -Parent $PSScriptRoot)

$procedure = 'docs/conventions/verification.md'
$section = '## 実機に触る検査'

$checks = [ordered]@{}
$checks['実機動作確認'] = { pwsh -NoProfile -File scripts/live-host.ps1 }
$checks['自動E2E検査'] = { pwsh -NoProfile -File scripts/live-tools.ps1 }
$checks['ブリッジの実機動作確認'] = { node scripts/live-bridge.mjs }
$checks['受入シナリオ'] = {
    node scripts/acceptance.mjs --cases data/authored/acceptance-scenarios.json `
        --setup scripts/acceptance-setup-dev.ps1
}

Assert-ListedChecks -Path $procedure -Section $section -Names @($checks.Keys)

$failed = @()
$ran = 0

# どの検査もUTF-8で書く相手を起こす。端末の設定のまま読むと、落ちた理由が読めなくなる。
$spoken = [Console]::OutputEncoding
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
try {
    foreach ($name in $checks.Keys) {
        $ran++
        $result = Invoke-Check -Name $name -Body $checks[$name]
        if ($result) { $failed += $result }
    }
} finally {
    [Console]::OutputEncoding = $spoken
}

exit (Write-CheckSummary -Failed $failed -Skipped @() -Scope '実機に触る検査' `
    -Ran $ran -Listed $checks.Count)
