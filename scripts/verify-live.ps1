# 検証手順書が定める実機に触る検査を、この1本で走らせる。
# 走らせるのは [verify.mjs](verify.mjs) で、この1本はそれを起こす。
[CmdletBinding()]
param(
    # 変えたものから選ばず、実機の検査を全件走らせる。
    [switch]$All
)

Set-Location (Split-Path -Parent $PSScriptRoot)

$given = @('scripts/verify.mjs', '--set', 'live')
if ($All) { $given += '--all' }

node @given
exit $LASTEXITCODE
