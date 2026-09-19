# 検査を1件だけ走らせ、その終了コードで終わる。上限で止めるのも結果を集めるのも起こした側が持つ。
[CmdletBinding()]
param(
    # どちらの一覧の検査か。
    [Parameter(Mandatory)][ValidateSet('standing', 'live')][string]$Set,
    # 走らせる検査の名前。
    [Parameter(Mandatory)][string]$Name
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 外部コマンドの非0終了は終了エラーにしない。ここでは出力と終了コードをそのまま見て合否にする。
$PSNativeCommandUseErrorActionPreference = $false

# 検査が起こす相手はどれもUTF-8で書く。
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()

if ($Set -eq 'standing') {
    . (Join-Path $PSScriptRoot 'check-set.ps1')
} else {
    . (Join-Path $PSScriptRoot 'live-checks.ps1')
    foreach ($named in $environment.Keys) {
        [System.Environment]::SetEnvironmentVariable($named, $environment[$named])
    }
}

if (-not $checks.Contains($Name)) { throw "知らない検査: $Name" }

$one = $checks[$Name]
$global:LASTEXITCODE = 0
try {
    if ($one.Contains('Run')) { & $one.Run[0] @($one.Run | Select-Object -Skip 1) }
    else { & $one.Body }
    $code = $LASTEXITCODE
} catch {
    Write-Host $_
    $code = 1
}

exit $code
