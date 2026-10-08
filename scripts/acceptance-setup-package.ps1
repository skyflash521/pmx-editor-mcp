# 受入の導入の前置のうち、配布パッケージから導入した状態の経路。
# 導入そのものは行わない。受入の実行器はこのスクリプトの中身を解さず、最後の行へ書いた組だけを読む。
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("prepare")]
    [string]$Action
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "editor-dir.ps1")

$installed = Join-Path $env:LOCALAPPDATA "pmx-editor-mcp"
$bridgeExe = Join-Path $installed "PmxEditorMcp.Bridge.exe"
$hostDll = Join-Path $installed "PmxEditorMcp.dll"
$placed = Join-Path (Get-SessionEditorDirectory) "_plugin\User\PmxEditorMcp.dll"

foreach ($required in @($bridgeExe, $hostDll, $placed)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "導入されていない: $required" }
}
if ((Get-FileHash -LiteralPath $placed).Hash -ne (Get-FileHash -LiteralPath $hostDll).Hash) {
    throw "配置したホスト $placed が導入先の $hostDll と同じでない。"
}

[pscustomobject]@{
    command   = (Resolve-Path -LiteralPath $bridgeExe).Path
    arguments = @()
} | ConvertTo-Json -Compress
