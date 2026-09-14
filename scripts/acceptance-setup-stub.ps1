# 受入の導入の前置のうち、実行器自身を確かめる経路。
# 実機を導入せず、応答を作って返すだけのMCPサーバーを起こす相手として返す。
# 前置が差し替え点として分かれていることは、この経路が成り立つことで確かめられる。
[CmdletBinding()]
param(
    # 行う前置。prepare は起こす相手を書き出す。
    [Parameter(Mandatory = $true)]
    [ValidateSet("prepare")]
    [string]$Action,

    # 応答を作る相手に読ませる定義。
    [Parameter(Mandatory = $true)]
    [string]$Cases,

    # 期待と違えるものの名前。空だと何も違えない。
    [Parameter(Mandatory = $true)]
    [AllowEmptyString()]
    [ValidateSet(
        "", "ok", "notice", "notice.changed", "body", "values", "code", "image", "imageAsText",
        "events", "file")]
    [string]$Broken,

    # 期待と違えるツールの呼び出しの番。
    [int]$At = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot 'acceptance-stub-shared.ps1')

$server = Join-Path $PSScriptRoot "acceptance-stub-server.mjs"
if (-not (Test-Path $server)) { throw "応答を作る相手が無い: $server" }

# 持ち越しは1回の実行の中だけのものなので、始める前に捨てる。
$temp = [System.IO.Path]::GetTempPath()
foreach ($name in @($StubLaunchStateName, $StubProgressStateName, $StubOperationLogName)) {
    Remove-Item -Path (Join-Path $temp $name) -Force -ErrorAction Ignore
}

[pscustomobject]@{
    command   = "node"
    arguments = @(
        (Resolve-Path $server).Path,
        "--cases", (Resolve-Path $Cases).Path,
        "--broken", $Broken,
        "--at", "$At",
        "--first-editor", "$FirstStubEditorId",
        "--view", ("$StubViewWidth" + "x" + "$StubViewHeight"),
        "--progress", (Join-Path $temp $StubProgressStateName))
} | ConvertTo-Json -Compress
