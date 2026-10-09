# 常設の検査を走らせる。変えたものの道から群を選ぶ。
[CmdletBinding()]
param(
    # 変えたものから選ばず、全件を走らせる。
    [switch]$All
)

Set-Location (Split-Path -Parent $PSScriptRoot)

$given = @('scripts/verify.mjs', '--set', 'standing')
if ($All) { $given += '--all' }

if (-not (Get-Command node -ErrorAction Ignore)) {
    Write-Error 'node が見つからない。' -ErrorAction Continue
    exit 1
}

node @given
exit $LASTEXITCODE
