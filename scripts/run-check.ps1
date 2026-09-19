[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('standing', 'live')][string]$Set,
    [Parameter(Mandatory)][string]$Name,
    [string]$Form,
    [string]$Results
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$PSNativeCommandUseErrorActionPreference = $false

[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()

$place = Join-Path ([System.IO.Path]::GetTempPath()) ('pmx-editor-mcp-place-' + [guid]::NewGuid().ToString('N'))
[void][System.IO.Directory]::CreateDirectory($place)
$env:TEMP = $place
$env:TMP = $place

if ($Set -eq 'standing') {
    . (Join-Path $PSScriptRoot 'check-set.ps1')
} else {
    . (Join-Path $PSScriptRoot 'live-checks.ps1')
    foreach ($named in $environment.Keys) {
        [System.Environment]::SetEnvironmentVariable($named, $environment[$named])
    }
}

if (-not $checks.Contains($Name)) { throw "知らない検査: $Name" }

$one = [Check]$checks[$Name]
$global:LASTEXITCODE = 0
try {
    if ($one.Run) {
        & $one.Run[0] @($one.Run | Select-Object -Skip 1)
        $code = $LASTEXITCODE
    } elseif ($one.Forms) {
        $code = 0
        foreach ($named in ($Form -split ',')) {
            $global:LASTEXITCODE = 0
            try {
                & $one.Body -Form $named
                $fell = $LASTEXITCODE
                $said = ''
            } catch {
                $fell = 1
                $said = ([string]$_) -replace '[\r\n\t]', ' '
            }

            if ($fell -ne 0) { $code = 1 }
            Add-Content -LiteralPath $Results -Encoding UTF8 `
                -Value ($named + "`t" + $fell + "`t" + $said)
        }
    } else {
        & $one.Body
        $code = $LASTEXITCODE
    }
} catch {
    Write-Host $_
    $code = 1
}

Remove-Item $place -Recurse -Force -ErrorAction Ignore

exit $code
