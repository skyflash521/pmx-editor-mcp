# 常設の検査を全件走らせる。変更を確定させる前の1回はこれを通す。
# 途中は、Markdown だけなら verify-docs.ps1、catalog/ だけなら verify-catalog.ps1 で足りる——
# 入力が変わっていない検査は、走らせても前と同じ答えが出るだけである。
[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'check-set.ps1')

# 群の名前をここへ書き写さない。書き写すと、群が増えたときこの1本だけが古いままになり、
# 全件と言いながら走らせない検査が出る。
exit (Invoke-Checks -Groups @($checkGroups.Keys))
