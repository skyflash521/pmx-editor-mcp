# 検査が起こしたまま残ったエディタを閉じる。相手はこの導入ディレクトリのものだけで、閉じられなくても
# 終了コードは0にする。
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()

$control = Join-Path $PSScriptRoot 'host-control.ps1'
foreach ($editor in @(& $control -Action editors)) {
    try {
        & $control -Action close -ProcessId $editor | Out-Null
    } catch {
        Write-Host ('残ったエディタを閉じられない(' + $editor + '): ' + $_)
    }
}

exit 0
