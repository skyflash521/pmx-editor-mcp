Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$PSNativeCommandUseErrorActionPreference = $false

. (Join-Path $PSScriptRoot 'checks.ps1')

Set-Location (Split-Path -Parent $PSScriptRoot)

# 束の中の並びは所要に効く。直前の検査が閉じたエディタの後始末と重なるぶん、後ろの検査は伸びる。
$liveBundle = '実機'

$checks = [ordered]@{}
$checks['配置と組み立て'] = New-Check `
    -Groups @('実機') `
    -LimitSeconds 17 <# 変更禁止 #> `
    -Needs $noArtifact `
    -Stage 1 `
    -Produces $liveSetup `
    -Bundle $liveBundle `
    -Body {
        & scripts/deploy-host.ps1 | Out-Null
        dotnet build src/Bridge/PmxEditorMcp.Bridge.csproj | Out-Null
        if ($LASTEXITCODE -ne 0) { return }

        dotnet build src/SignatureDump/PmxEditorMcp.SignatureDump.csproj | Out-Null
    }
$checks['自動E2E検査'] = New-Check `
    -Groups @('実機') `
    -LimitSeconds 36 <# 変更禁止 #> `
    -Needs $liveSetup `
    -Bundle $liveBundle `
    -Run @('pwsh', '-NoProfile', '-File', 'scripts/live-tools.ps1')
$checks['実機動作確認'] = New-Check `
    -Groups @('実機') `
    -LimitSeconds 10 <# 変更禁止 #> `
    -Needs $liveSetup `
    -Bundle $liveBundle `
    -Run @('pwsh', '-NoProfile', '-File', 'scripts/live-host.ps1')
$checks['参照クライアントの実機動作確認'] = New-Check `
    -Groups @('実機') `
    -LimitSeconds 30 <# 変更禁止 #> `
    -Needs $liveSetup `
    -Bundle $liveBundle `
    -Run @('node', 'scripts/live-client.mjs')
$checks['受入シナリオ'] = New-Check `
    -Groups @('実機') `
    -LimitSeconds 62 <# 変更禁止 #> `
    -Needs $liveSetup `
    -Bundle $liveBundle `
    -Run @('node', 'scripts/acceptance.mjs',
        '--cases', 'catalog/authored/acceptance-scenarios.json',
        '--setup', 'scripts/acceptance-setup-dev.ps1')

$environment = [ordered]@{ PMX_EDITOR_MCP_PREPARED = '1' }

$conditional = [ordered]@{
    '参照クライアントの実機動作確認' = @('docs/*', '.scratch/*', '*.md')
}

$afterFailure = @('pwsh', '-NoProfile', '-NonInteractive', '-File', 'scripts/close-editors.ps1')
