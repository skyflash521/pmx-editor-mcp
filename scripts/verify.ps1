# 常設の検査を走らせる。変えたものの道から群を選ぶので、変更を確定させる前もこの1本でよい。
# -All を渡すと、変えたものに関わらず全件を走らせる。
# 走らせるのは [verify.mjs](verify.mjs) で、この1本はそれを起こす。
[CmdletBinding()]
param(
    # 変えたものから選ばず、全件を走らせる。
    [switch]$All
)

Set-Location (Split-Path -Parent $PSScriptRoot)

$given = @('scripts/verify.mjs', '--set', 'standing')
if ($All) { $given += '--all' }

node @given
exit $LASTEXITCODE
