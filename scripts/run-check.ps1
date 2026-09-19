# 検査を1件だけ走らせ、その終了コードで終わる。上限で止めるのも結果を集めるのも起こした側が持つ。
[CmdletBinding()]
param(
    # どちらの一覧の検査か。
    [Parameter(Mandatory)][ValidateSet('standing', 'live')][string]$Set,
    # 走らせる検査の名前。
    [Parameter(Mandatory)][string]$Name,
    # 走らせる形。形を持つ検査でだけ渡す。読点で区切ると順に走らせる。
    [string]$Form,
    # 形ごとの結末を書く置き場。形を渡したときだけ使う。
    [string]$Results
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 外部コマンドの非0終了は終了エラーにしない。ここでは出力と終了コードをそのまま見て合否にする。
$PSNativeCommandUseErrorActionPreference = $false

# 検査が起こす相手はどれもUTF-8で書く。
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()

# 一時領域をこの1件だけのものに差し替える。
$place = Join-Path ([System.IO.Path]::GetTempPath()) ('pmx-editor-mcp-place-' + [guid]::NewGuid().ToString('N'))
[void][System.IO.Directory]::CreateDirectory($place)
$env:TEMP = $place
$env:TMP = $place

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
    if ($one.Contains('Run')) {
        & $one.Run[0] @($one.Run | Select-Object -Skip 1)
        $code = $LASTEXITCODE
    } elseif ($one.Contains('Forms')) {
        $code = 0
        foreach ($named in ($Form -split ',')) {
            $global:LASTEXITCODE = 0
            try {
                & $one.Body -Form $named
                $fell = $LASTEXITCODE
                $said = ''
            } catch {
                $fell = 1
                $said = ([string]$_) -replace '[\r\n\t]', ' '
            }

            if ($fell -ne 0) { $code = 1 }
            Add-Content -LiteralPath $Results -Encoding UTF8 `
                -Value ($named + "`t" + $fell + "`t" + $said)
        }
    } else {
        & $one.Body
        $code = $LASTEXITCODE
    }
} catch {
    Write-Host $_
    $code = 1
}

Remove-Item $place -Recurse -Force -ErrorAction Ignore

exit $code
