# 参照クライアントの実機動作確認の前置のうち、実行器自身を確かめる経路。
# 実機を導入せず、登録の綴りに書く相手だけを返す。呼ばせる相手の代わりはこの登録を読まないので、
# ここで返すものは起こされない——前置が差し替え点として分かれていることだけを、この経路が使う。
[CmdletBinding()]
param(
    # 行う前置。prepare は起こす相手を書き出す。
    [Parameter(Mandatory = $true)]
    [ValidateSet("prepare")]
    [string]$Action
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot 'stub-shared.ps1')

# 起こしたエディタの並びと数は1回の実行の中だけのものなので、始める前に捨てる。
$temp = [System.IO.Path]::GetTempPath()
foreach ($name in @($LiveStubEditorsName, $LiveStubLaunchedName)) {
    Remove-Item -Path (Join-Path $temp $name) -Force -ErrorAction Ignore
}

[pscustomobject]@{ command = "node"; arguments = @("--version") } | ConvertTo-Json -Compress
