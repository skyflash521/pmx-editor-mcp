# 常設の検査を走らせる。変えたものの道から群を選ぶので、変更を確定させる前もこの1本でよい。
# -All を渡すと、変えたものに関わらず全件を走らせる。
[CmdletBinding()]
param(
    # 変えたものから選ばず、全件を走らせる。
    [switch]$All
)

. (Join-Path $PSScriptRoot 'check-set.ps1')

# 群の名前をここへ書き写さない。書き写すと、群が増えたときこの1本だけが古いままになり、
# 全件と言いながら走らせない検査が出る。
if ($All) { exit (Invoke-Checks -Groups @($checkGroups.Keys)) }

# 追跡下の変更と、追跡外のファイルの両方を見る。新しく置いたファイルは差分に現れないが、
# それを読む検査は走らせなければならない。
$changed = @(git diff --name-only HEAD) + @(git ls-files --others --exclude-standard)
$groups = @(Select-CheckGroups -Paths @($changed | Where-Object { $_ }))
if ($groups.Count -eq 0) {
    Write-Host '変えたものが無いので、走らせる検査も無い。'
    exit 0
}

Write-Host ('変えたものから選んだ群: ' + ($groups -join '・'))
exit (Invoke-Checks -Groups $groups)
