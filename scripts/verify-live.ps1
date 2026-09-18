# 検証手順書が定める実機に触る検査を、この1本で走らせる。
# どれも実機のエディタを起こして操作するので、常設の検査とは分けてある——ログオンした対話的な
# デスクトップが要り、1本あたり分単位で掛かる。
[CmdletBinding()]
param(
    # 変えたものから選ばず、実機の検査を全件走らせる。
    [switch]$All
)

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

# 前置も検査の1つとして並べる。外に置くと、その所要が上限の外で使われるうえ、落ちても実行が
# 止まらず、ホストの無い状態で残りが上限ぶんの時間を使ってから落ちる。
$checks = [ordered]@{}
$checks['配置とブリッジのビルド'] = @{
    # ブリッジとホストのソースを変えて組み立てが走る回を含む。
    LimitSeconds = 17
    Needs = $noArtifact
    # 配置とブリッジの組み立ては、この実行で1回だけ行う。検査ごとの前置が同じことを繰り返すと、
    # 時間が増えるうえに、配置が動いているエディタを閉じるので後の検査の足を引っ張る。
    Command = '& scripts/deploy-host.ps1 | Out-Null; ' +
        'dotnet build src/Bridge/PmxEditorMcp.Bridge.csproj | Out-Null; exit $LASTEXITCODE'
}
$checks['実機動作確認'] = @{
    LimitSeconds = 10
    Needs = $liveSetup
    Command = 'pwsh -NoProfile -File scripts/live-host.ps1; exit $LASTEXITCODE'
}
$checks['自動E2E検査'] = @{
    LimitSeconds = 32
    Needs = $liveSetup
    Command = 'pwsh -NoProfile -File scripts/live-tools.ps1; exit $LASTEXITCODE'
}
$checks['参照クライアントの実機動作確認'] = @{
    LimitSeconds = 30
    Needs = $liveSetup
    Command = 'node scripts/live-client.mjs; exit $LASTEXITCODE'
}
$checks['受入シナリオ'] = @{
    LimitSeconds = 75
    Needs = $liveSetup
    Command = 'node scripts/acceptance.mjs --cases catalog/authored/acceptance-scenarios.json' +
        ' --setup scripts/acceptance-setup-dev.ps1; exit $LASTEXITCODE'
}

Assert-ListedChecks -Path $procedure -Section $section -Names @($checks.Keys)

$clientCheck = '参照クライアントの実機動作確認'
$clientUnrelated = @('docs/*', '.issues/*', '.scratch/*', '*.md')

$changed = @(@(git diff --name-only HEAD) + @(git ls-files --others --exclude-standard) |
    Where-Object { $_ })
$forClient = $All -or $changed.Count -eq 0 -or @($changed |
    Where-Object { -not (Test-PathsTouch -Paths @($_) -Patterns $clientUnrelated) }).Count -gt 0
if (-not $forClient) {
    Write-Host "$clientCheck は走らせない(見ている綴りを組み立てるものを変えていない)。"
}

$queued = @($checks.Keys | Where-Object { $_ -ne $clientCheck -or $forClient })

function Close-LeftEditors {
    <#
        .SYNOPSIS
        検査が起こしたまま残ったエディタを閉じる。相手はこの導入ディレクトリのものだけで、
        閉じられなくても実行は続ける。
    #>
    $control = Join-Path $PSScriptRoot 'host-control.ps1'
    foreach ($editor in @(& $control -Action editors)) {
        try {
            & $control -Action close -ProcessId $editor | Out-Null
        } catch {
            Write-Host ('     残ったエディタを閉じられない(' + $editor + '): ' + $_)
        }
    }
}

Start-CheckClock

$env:PMX_EDITOR_MCP_PREPARED = '1'

$failed = @()
$skipped = @()
$ran = 0

# どの検査もUTF-8で書く相手を起こす。端末の設定のまま読むと、落ちた理由が読めなくなる。
$spoken = [Console]::OutputEncoding
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
try {
    $produced = @($noArtifact)
    foreach ($name in $queued) {
        # 要る出来上がりが揃わない検査は始めない。前置が落ちた後に残りを走らせても、ホストの
        # 無い状態で上限ぶんの時間を使ってから落ちるだけである。
        if (-not (Test-CheckReady -Needs $checks[$name].Needs -Produced $produced)) {
            $skipped += $name
            continue
        }

        $ran++

        $result = Invoke-CappedCheck -Name $name -Command $checks[$name].Command `
            -LimitSeconds $checks[$name].LimitSeconds
        # 子を止めるのは待ちの分だけなので、起こしとログの読みを含めた所要は上限を
        # 越えうる。常設の実行器と同じ規則で合否を出す。
        $result = Deny-OverLimitCheck -Result $result `
            -LimitSeconds $checks[$name].LimitSeconds
        Write-CheckResult -Result $result
        if ($result.Code -ne 0) {
            Close-LeftEditors

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
    -Ran $ran -Listed $checks.Count `
    -Limit (@($queued | ForEach-Object { $checks[$_].LimitSeconds }) | Measure-Object -Sum).Sum)
