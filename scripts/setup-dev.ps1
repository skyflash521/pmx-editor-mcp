# 実機の検査の前置のうち、開発配置の経路。
# ブリッジのビルド成果物を、実行器が起こす相手として返す。
# 実行器はこのスクリプトの中身を解さず、最後の行へ書いた組だけを読む。
[CmdletBinding()]
param(
    # 行う前置。prepare は起こす相手を書き出す。
    [Parameter(Mandatory = $true)]
    [ValidateSet("prepare")]
    [string]$Action,

    # ホストの配置も済ませる。配置は動いているエディタを閉じる。
    [switch]$DeployHost
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$PSNativeCommandUseErrorActionPreference = $false

$root = Split-Path -Parent $PSScriptRoot
$bridgeProject = Join-Path $root "src/Bridge/PmxEditorMcp.Bridge.csproj"
$bridgeExe = Join-Path $root "src/Bridge/bin/Debug/net10.0/PmxEditorMcp.Bridge.exe"

if ($env:PMX_EDITOR_MCP_PREPARED -ne '1') {
    if ($DeployHost) {
        & (Join-Path $PSScriptRoot "deploy-host.ps1") | Out-Null
    }

    dotnet build $bridgeProject | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "ブリッジのビルドに失敗した(終了コード $LASTEXITCODE)。" }
}

if (-not (Test-Path $bridgeExe)) { throw "ブリッジの実行ファイルが無い: $bridgeExe" }

[pscustomobject]@{
    command   = (Resolve-Path $bridgeExe).Path
    arguments = @()
} | ConvertTo-Json -Compress
