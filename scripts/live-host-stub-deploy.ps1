# 実機動作確認を確かめるときの配置。実機のエディタへ置くものが無く、何もせずに戻る。
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot 'stub-shared.ps1')

# 1回の実行の中だけの印を捨てる。配置は、実行の先頭で1度だけ走る。
Remove-Item -Path (Join-Path ([System.IO.Path]::GetTempPath()) $LiveHostStubIgnoredName) `
    -Force -ErrorAction Ignore
