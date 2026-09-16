# 自動E2E検査の前置のうち、開発配置の経路。
# ブリッジのビルド成果物を、実行器が起こす相手として返す。ホストの配置は実行器の外で済ませてある
# ——配置は動いているエディタを閉じるので、検査の途中で行うと後の検査の分まで閉じにいく。
# 実行器はこのスクリプトの中身を解さず、最後の行へ書いた組だけを読む。
[CmdletBinding()]
param(
    # 行う前置。prepare は起こす相手を書き出す。
    [Parameter(Mandatory = $true)]
    [ValidateSet("prepare")]
    [string]$Action
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# 外部コマンドの非0終了は終了エラーにしない。終了コードを見て自分で失敗させる。
$PSNativeCommandUseErrorActionPreference = $false

$root = Split-Path -Parent $PSScriptRoot
$bridgeProject = Join-Path $root "src/Bridge/PmxEditorMcp.Bridge.csproj"
$bridgeExe = Join-Path $root "src/Bridge/bin/Debug/net10.0/PmxEditorMcp.Bridge.exe"

# 1回の実行の中で前置が何度も呼ばれる。実行器が先に組み立てを済ませていれば繰り返さない。
# 単独で走らせたときは印が無いので、これまでどおり自分で済ませる——実行ファイルが在ることだけで
# 省くと、中身を直した後の実行が前の版のブリッジを相手に通ってしまう。
if ($env:PMX_EDITOR_MCP_PREPARED -ne '1') {
    dotnet build $bridgeProject | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "ブリッジのビルドに失敗した(終了コード $LASTEXITCODE)。" }
}

if (-not (Test-Path $bridgeExe)) { throw "ブリッジの実行ファイルが無い: $bridgeExe" }

[pscustomobject]@{
    command   = (Resolve-Path $bridgeExe).Path
    arguments = @()
} | ConvertTo-Json -Compress
