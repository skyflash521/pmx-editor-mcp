# 配布パッケージを組み立てる。ホストの発行・ブリッジの発行・第三者ライセンス表示の組み立て・
# zipの作成・内容物の検査を、この1本で通す。検査に落ちたら失敗させる——中身を確かめていない
# ものを配布物として残さない。
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# 外部コマンドの非0終了は終了エラーにしない。終了コードを見て自分で失敗させる。
$PSNativeCommandUseErrorActionPreference = $false

$repository = Split-Path -Parent $PSScriptRoot
$hostProject = Join-Path $repository "src/HostPlugin/PmxEditorMcp.HostPlugin.csproj"
$generatorProject = Join-Path $repository "src/SignatureDump/PmxEditorMcp.SignatureDump.csproj"
$generator = Join-Path $repository "src/SignatureDump/bin/Release/net48/PmxEditorMcp.SignatureDump.exe"
$ledgerTargets = Join-Path $PSScriptRoot "shipping-ledger.targets"
$license = Join-Path $repository "LICENSE"
$licenses = Join-Path $repository "catalog/observed/licenses"
$distribution = Join-Path $repository "dist"

function Get-Version {
    <#
        .SYNOPSIS
        配布の版。追跡下の Directory.Build.props が定める。綴りから読むのではなく MSBuild に
        評価させて取る——条件や継承で決まる値を、こちらで組み立て直さない。
    #>
    $said = dotnet msbuild $hostProject -getProperty:Version
    if ($LASTEXITCODE -ne 0) { throw "版を読めない。" }

    $version = $said.Trim()
    if (-not $version) { throw "版が空である。" }

    $version
}

function Publish-Host {
    <#
        .SYNOPSIS
        ホストを発行し、出来たDLLの在り処を返す。あわせて、この発行が解決した資産を出荷台帳へ
        書き出す。配布物のDLLは参照するだけで、成果物へは写さない——再配布を禁じられている
        ためである。
    #>
    param([string]$Into, [string]$Ledger)

    dotnet publish $hostProject -c Release -warnaserror -o $Into `
        "-p:CustomAfterMicrosoftCommonTargets=$ledgerTargets" `
        "-p:ShippingLedgerPath=$Ledger" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "ホストの発行に失敗した。" }

    Join-Path $Into "PmxEditorMcp.dll"
}

$version = Get-Version
$staged = Join-Path $distribution "pmx-editor-mcp-$version"
$archive = "$staged.zip"

# 前の実行の成果を先に捨てる。落ちた回の後に古いものが同じ名前で残ると、確かめていない物を
# 確かめた物として渡してしまう。
if (Test-Path $staged) { Remove-Item -Path $staged -Recurse -Force }
if (Test-Path $archive) { Remove-Item -Path $archive -Force }
New-Item -ItemType Directory -Force -Path $staged | Out-Null

$work = Join-Path ([System.IO.Path]::GetTempPath()) ('package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $work | Out-Null

try {
    dotnet build $generatorProject -c Release -warnaserror | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "組み立て器の用意に失敗した。" }

    # 出荷台帳は、出荷する実行ファイルを作ったその発行が書き出す。数えた物と出荷した物が同じ
    # 解決結果であることを、別に解決を走らせない形で確かめる。
    $hostLedger = Join-Path $work "host.txt"
    $bridgeLedger = Join-Path $work "bridge.txt"

    Copy-Item -Path (Publish-Host -Into (Join-Path $work "host") -Ledger $hostLedger) `
        -Destination $staged -Force
    & (Join-Path $PSScriptRoot "publish-bridge.ps1") `
        -Destination (Join-Path $work "bridge") -Ledger $bridgeLedger | Out-Null
    Copy-Item -Path (Join-Path $work "bridge/PmxEditorMcp.Bridge.exe") `
        -Destination $staged -Force
    Copy-Item -Path $license -Destination (Join-Path $staged "LICENSE.txt") -Force

    & $generator thirdparty (Join-Path $staged "ThirdPartyNotices.txt") $licenses `
        $hostLedger $bridgeLedger | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "第三者ライセンス表示を組み立てられない。" }
} finally {
    Remove-Item -Path $work -Recurse -Force -ErrorAction Ignore
}

& (Join-Path $PSScriptRoot "package-contents.ps1") -Staged $staged -Version $version `
    -Expected @("PmxEditorMcp.dll", "PmxEditorMcp.Bridge.exe", "LICENSE.txt",
        "ThirdPartyNotices.txt") | Out-Null

Compress-Archive -Path (Join-Path $staged "*") -DestinationPath $archive

Write-Host ("配布パッケージを組み立てた: " + $archive)
