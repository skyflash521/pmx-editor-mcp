[CmdletBinding()]
param(
    [string]$Since
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$PSNativeCommandUseErrorActionPreference = $false

. (Join-Path $PSScriptRoot 'editor-dir.ps1')

Set-Location (Split-Path -Parent $PSScriptRoot)

$control = 'scripts/host-control.ps1'
$generator = 'src/SignatureDump/PmxEditorMcp.SignatureDump.csproj'
$dump = 'src/SignatureDump/bin/Debug/net48/PmxEditorMcp.SignatureDump.exe'

$cases = Join-Path ([System.IO.Path]::GetTempPath()) (
    'pmx-editor-mcp-e2e-cases-' + [guid]::NewGuid().ToString('N') + '.json')

$map = 'catalog/authored/tool-map.json'

$told = Join-Path ([System.IO.Path]::GetTempPath()) (
    'pmx-editor-mcp-e2e-editor-' + [guid]::NewGuid().ToString('N') + '.txt')
$unfiltered = 4

function Get-ChangedRows {
    param([string]$Ref)

    $named = Join-Path ([System.IO.Path]::GetTempPath()) (
        'pmx-editor-mcp-tool-map-' + [guid]::NewGuid().ToString('N') + '-')
    $before = $named + 'before.json'
    $rows = $named + 'rows.txt'

    try {
        $spoken = [Console]::OutputEncoding
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
        try {
            git show "${Ref}:$map" | Set-Content -Path $before -Encoding utf8
            if ($LASTEXITCODE -ne 0) { throw "指した版の能力対応表を取り出せない: $Ref" }
        } finally {
            [Console]::OutputEncoding = $spoken
        }

        $changed = & $dump changed-rows $before $map
        if ($LASTEXITCODE -ne 0) { throw "変わった行を選べない(終了コード $LASTEXITCODE)。" }

        Set-Content -Path $rows -Value (@($changed) -join [Environment]::NewLine) -Encoding utf8
    } finally {
        Remove-Item $before -ErrorAction Ignore
    }

    return $rows
}

$editor = 0
$rising = $null
$building = $null
$rows = $null
try {
    if ($env:PMX_EDITOR_MCP_PREPARED -ne '1') { & scripts/deploy-host.ps1 | Out-Null }

    if ($env:PMX_EDITOR_MCP_PREPARED -ne '1') {
        dotnet build $generator | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "生成器のビルドに失敗した(終了コード $LASTEXITCODE)。" }
    }

    if ($Since) { $rows = Get-ChangedRows -Ref $Since }

    if ($rows -and -not (Get-Content $rows)) {
        Write-Host "指した版から中身の変わった行が無い。"
        return
    }

    $rising = Start-ThreadJob -ArgumentList (Get-Location).Path, $control, $told -ScriptBlock {
        param([string]$Root, [string]$Control, [string]$Told)

        Set-Location $Root
        $started = [int](& $Control -Action launch)
        Set-Content -LiteralPath $Told -Value $started -Encoding UTF8 -NoNewline

        $started
    }

    $building = Start-ThreadJob -ArgumentList (Get-Location).Path, $dump,
        (Get-EditorDirectory), $map, $cases -ScriptBlock {
        param([string]$Root, [string]$Dump, [string]$Editor, [string]$Map, [string]$Cases)

        Set-Location $Root
        $part = $Cases + '.part'
        & $Dump e2e-cases $Editor `
            catalog/observed/capability-ledger.json `
            catalog/authored/common-contract.json `
            catalog/authored/type-roles.json `
            catalog/authored/property-names.json `
            catalog/authored/common-assignments.json `
            $Map `
            catalog/authored/tool-schemas.json `
            catalog/authored/sample-values.json `
            $part | Out-Null
        if ($LASTEXITCODE -ne 0) { return $LASTEXITCODE }

        Move-Item -LiteralPath $part -Destination $Cases -Force

        return 0
    }

    if ($rows) {
        node scripts/e2e-tools.mjs $told $cases --rows $rows
    } else {
        node scripts/e2e-tools.mjs $told $cases
    }
    $ran = $LASTEXITCODE

    $global:LASTEXITCODE = 0

    $built = [int](Receive-Job -Job $building -Wait -AutoRemoveJob)
    $building = $null
    if ($built -ne 0) { throw "検査を組み立てられない(終了コード $built)。" }

    if ($ran -eq $unfiltered) { throw "絞った実行では確かめられない行がある。" }
    if ($ran -ne 0) { throw "不合格の検査がある(終了コード $ran)。" }
} finally {
    if ($building) { Remove-Job -Job $building -Force -ErrorAction Ignore }

    if ($rising) {
        try { $editor = [int](Receive-Job -Job $rising -Wait -AutoRemoveJob) }
        catch {
            Write-Warning "起こしたエディタを引き取れなかった: $($_.Exception.Message)" `
                -WarningAction Continue
        }
    }

    if ($editor -ne 0 -and (Get-Process -Id $editor -ErrorAction Ignore)) {
        & $control -Action close -ProcessId $editor | Out-Null
    }
    Remove-Item $cases, ($cases + '.part') -ErrorAction Ignore
    Remove-Item $told -ErrorAction Ignore
    if ($rows) { Remove-Item $rows -ErrorAction Ignore }
}
