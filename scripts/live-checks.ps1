# 実機に触る検査の中身。[check-manifest.ps1](check-manifest.ps1) が一覧として出し、
# [run-check.ps1](run-check.ps1) が1件ずつ走らせる。
# どれもログオンした対話的なデスクトップを要し、常設の検査とは分けて走らせる。

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 外部コマンドの非0終了は終了エラーにしない。ここでは出力と終了コードをそのまま見て合否にする。
$PSNativeCommandUseErrorActionPreference = $false

. (Join-Path $PSScriptRoot 'checks.ps1')

Set-Location (Split-Path -Parent $PSScriptRoot)

# 実機に触る検査は、同じエディタとその導入先を取り合う。1つの束へ入れて順に走らせる。
# **上限に近い検査を前に置く。** 直前の検査が閉じたエディタの後始末と重なると、その分だけ伸びる。
$liveBundle = '実機'

# 前置も検査の1つとして並べる。
$checks = [ordered]@{}
$checks['配置と組み立て'] = @{
    # ブリッジとホストと生成器のソースを変えて組み立てが走る回を含む。
    LimitSeconds = 17
    Needs = $noArtifact
    # 配置と組み立ては、この実行で1回だけ行う。
    Stage = 1
    Produces = $liveSetup
    Bundle = $liveBundle
    Body = {
        & scripts/deploy-host.ps1 | Out-Null
        dotnet build src/Bridge/PmxEditorMcp.Bridge.csproj | Out-Null
        if ($LASTEXITCODE -ne 0) { return }

        dotnet build src/SignatureDump/PmxEditorMcp.SignatureDump.csproj | Out-Null
    }
}
$checks['自動E2E検査'] = @{
    LimitSeconds = 30
    Needs = $liveSetup
    Bundle = $liveBundle
    Run = @('pwsh', '-NoProfile', '-File', 'scripts/live-tools.ps1')
}
$checks['実機動作確認'] = @{
    LimitSeconds = 10
    Needs = $liveSetup
    Bundle = $liveBundle
    Run = @('pwsh', '-NoProfile', '-File', 'scripts/live-host.ps1')
}
$checks['参照クライアントの実機動作確認'] = @{
    LimitSeconds = 30
    Needs = $liveSetup
    Bundle = $liveBundle
    Run = @('node', 'scripts/live-client.mjs')
}
$checks['受入シナリオ'] = @{
    LimitSeconds = 62
    Needs = $liveSetup
    Bundle = $liveBundle
    Run = @('node', 'scripts/acceptance.mjs',
        '--cases', 'catalog/authored/acceptance-scenarios.json',
        '--setup', 'scripts/acceptance-setup-dev.ps1')
}

# この一覧の検査が前提にする環境。走らせる側が渡す。
$environment = [ordered]@{ PMX_EDITOR_MCP_PREPARED = '1' }

# 変えたものが答えを変えない検査と、答えを変えない道の形。
$conditional = [ordered]@{
    '参照クライアントの実機動作確認' = @('docs/*', '.issues/*', '.scratch/*', '*.md')
}

# 落ちた検査が起こしたまま残したエディタを閉じる後始末。走らせる側が、上限で打ち切った回も含めて
# 落ちた検査のたびに起こす。
$afterFailure = @('pwsh', '-NoProfile', '-NonInteractive', '-File', 'scripts/close-editors.ps1')
