# 実機に触る検査を走らせる。
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
