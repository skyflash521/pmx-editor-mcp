# ブリッジが、共有ランタイムを解決できない環境で単独で動くことを確かめる。
# 受け取った側の実行環境には .NET が入っていないので、開発機で動いたことは何の保証にもならない。
# 解決できない環境をこの検査自身が作り、その環境が本当に成立していることを、フレームワーク依存で
# 発行した実行ファイルがそこで起動に失敗することで確かめる。
[CmdletBinding()]
param(
    # 発行と試しに使う置き場。省略すると一時領域へ作る。
    [string]$Root
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# 外部コマンドの非0終了は終了エラーにしない。終了コードを見て自分で失敗させる。
$PSNativeCommandUseErrorActionPreference = $false

# PEのヘッダが名乗る機械の種別。x64のもの。
$AmdMachine = 0x8664

# 置き場を渡されなければ自分で作る。作ったものは自分で片付ける——発行物は1回ぶんで百メガ単位に
# なるので、走らせるたびに残すと一時領域が埋まる。
$ours = -not $Root
if ($ours) {
    $Root = Join-Path ([System.IO.Path]::GetTempPath()) ("pmx-editor-mcp-standalone-" + [guid]::NewGuid().ToString("N"))
}

$contained = Join-Path $Root "contained"
$dependent = Join-Path $Root "dependent"
$alone = Join-Path $Root "alone"
$absent = Join-Path $Root "absent"

function Get-MachineKind {
    <#
        .SYNOPSIS
        実行ファイルのPEヘッダが名乗る機械の種別。どのランタイム識別子で発行したかは、成果物の
        側からはこれで分かる。
    #>
    param([string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $at = [System.BitConverter]::ToInt32($bytes, 0x3C)

    [System.BitConverter]::ToUInt16($bytes, $at + 4)
}

function Invoke-WithoutSharedRuntime {
    <#
        .SYNOPSIS
        共有ランタイムを解決できない環境で起こし、書き出したものと終了コードを返す。空の置き場を
        ランタイムの在り処として指し、`dotnet` を引ける場所を通り道から外す。
    #>
    param([string]$FilePath, [string[]]$Arguments)

    $previousRoot = $env:DOTNET_ROOT
    $previousPath = $env:PATH
    try {
        $env:DOTNET_ROOT = $absent

        # Node は要るので、その置き場だけを通り道に残す。
        $env:PATH = Split-Path -Parent (Get-Command node).Source

        # 診断は標準エラー出力へ出るので、合否の手がかりと混ぜない。起こすのは1回だけとする。
        $diagnostics = Join-Path $Root ("diagnostics-" + [guid]::NewGuid().ToString("N") + ".txt")
        $said = & $FilePath @Arguments 2>$diagnostics
        $code = $LASTEXITCODE
        $noise = if (Test-Path $diagnostics) { Get-Content $diagnostics -Raw } else { "" }

        [pscustomobject]@{
            Code = $code
            Said = ($said -join "`n")
            Noise = $noise
        }
    } finally {
        $env:DOTNET_ROOT = $previousRoot
        $env:PATH = $previousPath
    }
}

try {
    foreach ($each in @($contained, $dependent, $alone, $absent)) {
        New-Item -ItemType Directory -Force -Path $each | Out-Null
    }

    $publish = Join-Path $PSScriptRoot "publish-bridge.ps1"
    & $publish -Destination $contained -SelfContained $true | Out-Null
    & $publish -Destination $dependent -SelfContained $false | Out-Null

    $exe = Join-Path $contained "PmxEditorMcp.Bridge.exe"
    if (-not (Test-Path $exe)) { throw "発行した実行ファイルが無い: $exe" }

    $machine = Get-MachineKind -Path $exe
    if ($machine -ne $AmdMachine) {
        throw ("発行した実行ファイルの機械の種別が x64 ではない: 0x" + $machine.ToString("X"))
    }

    # 共有ランタイムを解決できない環境が成立していることを、フレームワーク依存版で確かめる。成立して
    # いなければ、このあとの単独起動は何も確かめていないことになる。
    $broken = Invoke-WithoutSharedRuntime `
        -FilePath (Join-Path $dependent "PmxEditorMcp.Bridge.exe") -Arguments @()
    if (($broken.Said + $broken.Noise) -notmatch "install .NET") {
        throw "共有ランタイムを解決できない環境になっていない: $($broken.Said)$($broken.Noise)"
    }

    # 発行先の残りにも、実行機に入っている共有ランタイムにも依らないことを見るため、単独で写す。
    Copy-Item -Path $exe -Destination $alone -Force
    $standalone = Join-Path $alone "PmxEditorMcp.Bridge.exe"

    $ran = Invoke-WithoutSharedRuntime `
        -FilePath (Get-Command node).Source `
        -Arguments @((Join-Path $PSScriptRoot "mcp-check.mjs"), $standalone)
    if ($ran.Code -ne 0) {
        throw ("共有ランタイムを解決できない環境で単独起動して応答しない: " +
            "$($ran.Said)$($ran.Noise)")
    }

    Write-Host ("単独で起動して応答した: " + ($ran.Said -split "`n")[-1])
} finally {
    if ($ours) { Remove-Item -Path $Root -Recurse -Force -ErrorAction Ignore }
}
