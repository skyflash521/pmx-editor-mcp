# 自動E2E検査の前置のうち、代わりの相手を立てる経路。
# 実機のエディタもホストもブリッジも要らない相手を、実行器が起こす相手として返す。実行器そのものを
# 確かめる常設の検査がこれを使う。
# 実行器はこのスクリプトの中身を解さず、最後の行へ書いた組だけを読む。
[CmdletBinding()]
param(
    # 行う前置。prepare は起こす相手を書き出す。
    [Parameter(Mandatory = $true)]
    [ValidateSet("prepare")]
    [string]$Action,

    # 代わりの相手が結末を引く検査の定義。
    [Parameter(Mandatory = $true)]
    [string]$Cases,

    # 期待を違えさせる結末の形。空なら違えない。
    [string]$Broken = "",

    # 期待を違えさせる検査の番。
    [int]$At = -1
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$stub = Join-Path $PSScriptRoot "e2e-stub-bridge.mjs"
$arguments = @($stub, "--cases", $Cases, "--at", "$At")
if ($Broken) { $arguments += @("--broken", $Broken) }

[pscustomobject]@{
    command   = "node"
    arguments = $arguments
} | ConvertTo-Json -Compress
