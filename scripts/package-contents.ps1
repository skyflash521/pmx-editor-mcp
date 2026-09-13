# 組み立てた配布パッケージの中身を確かめる。
# 並べたものが過不足なく在ること、再配布を禁じられている物が混じっていないこと、写しが元と
# バイトで一致すること、実行ファイルが名乗る版が渡された版と合うことを見る。
# 組み立てと別に呼べるようにしてあるのは、中身を違えたときに落ちることを確かめられるようにする
# ためである——落ちない検査は、通っても何も言っていない。
[CmdletBinding()]
param(
    # 確かめる中身の置き場。
    [Parameter(Mandatory = $true)]
    [string]$Staged,

    # 実行ファイルが名乗るはずの版。
    [Parameter(Mandatory = $true)]
    [string]$Version,

    # 在るはずのものの名前。
    [Parameter(Mandatory = $true)]
    [string[]]$Expected
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# 再配布を禁じられている物。PMXエディタ配布物の利用規約が、パッケージ内データの再配布を禁じる。
$Forbidden = @("PEPlugin.dll", "SlimDX.dll")

# 版を名乗るはずの実行ファイル。
$Versioned = @("PmxEditorMcp.dll", "PmxEditorMcp.Bridge.exe")

$license = Join-Path (Split-Path -Parent $PSScriptRoot) "LICENSE"

$found = @(Get-ChildItem -Path $Staged -Recurse -File | ForEach-Object { $_.Name } | Sort-Object)
$wanted = @($Expected | Sort-Object)
if (($found -join "/") -ne ($wanted -join "/")) {
    throw "内容物が違う。求めるもの: $($wanted -join '・') / 在るもの: $($found -join '・')"
}

foreach ($name in $Forbidden) {
    if ($found -contains $name) { throw "再配布できない物が混じっている: $name" }
}

$copied = Join-Path $Staged "LICENSE.txt"
if ((Get-FileHash $copied).Hash -ne (Get-FileHash $license).Hash) {
    throw "LICENSE.txt がリポジトリの LICENSE と一致しない。"
}

# 名乗る版は4つ組で、正本は3つ組で書く。書き出しが同じだけでは合ったことにならない
# ——末尾の数が桁を増やした成果物が、正本を前に置いた一致で通ってしまう。数の組にそろえてから
# 比べる。
$wanted = [version]$Version
foreach ($name in $Versioned) {
    $said = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $Staged $name)).FileVersion
    $told = $null
    $parsed = [version]::TryParse($said, [ref]$told)
    $same = $parsed -and $told.Major -eq $wanted.Major -and $told.Minor -eq $wanted.Minor `
        -and $told.Build -eq $wanted.Build
    if (-not $same) {
        throw "$name が名乗る版が $Version と合わない: $said"
    }
}

Write-Host ("内容物を確かめた: " + ($found -join "・"))
