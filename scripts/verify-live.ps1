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
$checks['参照クライアントの実機動作確認'] = { node scripts/live-client.mjs }
$checks['受入シナリオ'] = {
    node scripts/acceptance.mjs --cases catalog/authored/acceptance-scenarios.json `
        --setup scripts/acceptance-setup-dev.ps1
}

Assert-ListedChecks -Path $procedure -Section $section -Names @($checks.Keys)

# 時間の上限はこの実行の全体に掛かるので、前置より先に数え始める。前置を外に置くと、その所要の
# ぶんだけ上限を超えた実行が合格になる。
Start-CheckBudget

# 配置とブリッジのビルドは、この実行で1回だけ行う。検査ごとの前置が同じことを繰り返すと、
# 時間が増えるうえに、配置が動いているエディタを閉じるので後の検査の足を引っ張る。
# 「実機動作確認」の「配置」の件は、これとは別に配置そのものを確かめる。
& (Join-Path $PSScriptRoot 'deploy-host.ps1') | Out-Null
dotnet build (Join-Path (Split-Path -Parent $PSScriptRoot) 'src/Bridge/PmxEditorMcp.Bridge.csproj') |
    Out-Null
if ($LASTEXITCODE -ne 0) { throw "ブリッジのビルドに失敗した(終了コード $LASTEXITCODE)。" }
$env:PMX_EDITOR_MCP_PREPARED = '1'

$failed = @()
$skipped = @()
$ran = 0

# どの検査もUTF-8で書く相手を起こす。端末の設定のまま読むと、落ちた理由が読めなくなる。
$spoken = [Console]::OutputEncoding
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
try {
    foreach ($name in $checks.Keys) {
        # 上限を使い切ったら残りは始めない。始めれば超過がそのぶん伸びるだけで、結末は変わらない。
        if (Test-CheckBudgetSpent) {
            $skipped += $name
            continue
        }

        $ran++
        $result = Invoke-Check -Name $name -Body $checks[$name]
        if ($result) { $failed += $result }
    }
} finally {
    [Console]::OutputEncoding = $spoken
    Remove-Item Env:PMX_EDITOR_MCP_PREPARED -ErrorAction Ignore
}

exit (Write-CheckSummary -Failed $failed -Skipped $skipped -Scope '実機に触る検査' `
    -Ran $ran -Listed $checks.Count)
