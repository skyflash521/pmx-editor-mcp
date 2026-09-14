# 常設の検査のうち「ドキュメント」の群だけを走らせる。Markdown を変えたときに使う。
[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'check-set.ps1')

exit (Invoke-Checks -Groups @('ドキュメント'))
