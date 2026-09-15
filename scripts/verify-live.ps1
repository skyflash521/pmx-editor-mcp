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

# 出来上がりを作る検査の名前。どの検査がその出来上がりを要るかは、検査ごとの Needs が述べる。
$liveSetupCheck = '配置とブリッジのビルド'

# 前置も検査の1つとして並べる。外に置くと、その所要が配分の外で使われるうえ、落ちても実行が
# 止まらず、ホストの無い状態で残りが配分ぶんの時間を使ってから落ちる。
$checks = [ordered]@{}
$checks['配置とブリッジのビルド'] = @{
    Needs = $noArtifact
    Body = {
        # 配置とブリッジの組み立ては、この実行で1回だけ行う。検査ごとの前置が同じことを
        # 繰り返すと、時間が増えるうえに、配置が動いているエディタを閉じるので後の検査の足を
        # 引っ張る。
        & (Join-Path $PSScriptRoot 'deploy-host.ps1') | Out-Null
        dotnet build (
            Join-Path (Split-Path -Parent $PSScriptRoot) 'src/Bridge/PmxEditorMcp.Bridge.csproj'
        ) | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "ブリッジの組み立てに失敗した(終了コード $LASTEXITCODE)。" }
    }
}
$checks['実機動作確認'] = @{
    Needs = $liveSetup
    Body = { pwsh -NoProfile -File scripts/live-host.ps1 }
}
$checks['自動E2E検査'] = @{
    Needs = $liveSetup
    Body = { pwsh -NoProfile -File scripts/live-tools.ps1 }
}
$checks['参照クライアントの実機動作確認'] = @{
    Needs = $liveSetup
    Body = { node scripts/live-client.mjs }
}
$checks['受入シナリオ'] = @{
    Needs = $liveSetup
    Body = {
        node scripts/acceptance.mjs --cases catalog/authored/acceptance-scenarios.json `
            --setup scripts/acceptance-setup-dev.ps1
    }
}

Assert-ListedChecks -Path $procedure -Section $section -Names @($checks.Keys)

Start-CheckBudget

$env:PMX_EDITOR_MCP_PREPARED = '1'

$failed = @()
$skipped = @()
$ran = 0

# どの検査もUTF-8で書く相手を起こす。端末の設定のまま読むと、落ちた理由が読めなくなる。
$spoken = [Console]::OutputEncoding
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
try {
    $produced = @($noArtifact)
    foreach ($name in $checks.Keys) {
        # 検査ごとの配分はまだ置いていないので、実機の合計だけを見る。使い切ったら残りは
        # 始めない——始めれば超過がそのぶん伸びるだけで、結末は変わらない。
        if ((Get-CheckBudgetElapsed) -gt $LiveBudgetSeconds) {
            $skipped += $name
            continue
        }

        # 要る出来上がりが揃わない検査は始めない。前置が落ちた後に残りを走らせても、ホストの
        # 無い状態で配分ぶんの時間を使ってから落ちるだけである。
        if (-not (Test-CheckReady -Needs $checks[$name].Needs -Produced $produced)) {
            $skipped += $name
            continue
        }

        $ran++
        $result = Invoke-Check -Name $name -Body $checks[$name].Body
        Write-CheckResult -Result $result
        if ($result.Code -ne 0) {
            $failed += $result.Name
            continue
        }

        if ($name -eq $liveSetupCheck) { $produced += $liveSetup }
    }
} finally {
    [Console]::OutputEncoding = $spoken
    Remove-Item Env:PMX_EDITOR_MCP_PREPARED -ErrorAction Ignore
}

exit (Write-CheckSummary -Failed $failed -Skipped $skipped -Scope '実機に触る検査' `
    -Ran $ran -Listed $checks.Count -Limit $LiveBudgetSeconds)
