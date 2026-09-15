# 実機動作確認を確かめるときの配置。実機のエディタへ置くものが無いので、何もせずに戻る。
# 配置が差し替え点として分かれていることは、この経路が成り立つことで確かめられる。
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot 'stub-shared.ps1')

# 1回の実行の中だけの印を捨てる。配置は実行の先頭で1度だけ走るので、ここが始まりの目印になる。
Remove-Item -Path (Join-Path ([System.IO.Path]::GetTempPath()) $LiveHostStubIgnoredName) `
    -Force -ErrorAction Ignore
