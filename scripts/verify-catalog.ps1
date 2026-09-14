# 常設の検査のうち「定義」の群だけを走らせる。`catalog/` を変えたときに使う。
[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'check-set.ps1')

exit (Invoke-Checks -Groups @('定義'))
