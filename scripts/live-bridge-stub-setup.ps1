# ブリッジの実機動作確認の前置のうち、実行器自身を確かめる経路。
# 実機を導入せず、応答を作って返すだけのMCPサーバーを起こす相手として返す。
# 前置が差し替え点として分かれていることは、この経路が成り立つことで確かめられる。
[CmdletBinding()]
param(
    # 行う前置。prepare は起こす相手を書き出す。
    [Parameter(Mandatory = $true)]
    [ValidateSet("prepare")]
    [string]$Action,

    # 期待と違えるものの名前。空だと何も違えない。
    [Parameter(Mandatory = $true)]
    [AllowEmptyString()]
    [ValidateSet("", "code", "target", "pong", "moved", "order", "listed")]
    [string]$Broken
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot 'stub-shared.ps1')

$server = Join-Path $PSScriptRoot "live-bridge-stub-server.mjs"
if (-not (Test-Path $server)) { throw "応答を作る相手が無い: $server" }

# 起こしたエディタの並びと数は1回の実行の中だけのものなので、始める前に捨てる。
$temp = [System.IO.Path]::GetTempPath()
foreach ($name in @($LiveStubEditorsName, $LiveStubLaunchedName)) {
    Remove-Item -Path (Join-Path $temp $name) -Force -ErrorAction Ignore
}

[pscustomobject]@{
    command   = "node"
    arguments = @(
        (Resolve-Path $server).Path,
        "--first-editor", "$FirstStubEditorId",
        "--broken", $Broken)
} | ConvertTo-Json -Compress
