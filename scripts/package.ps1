# 配布パッケージを組み立てる。ホストの組み立て・ブリッジの発行・zipの作成・内容物の検査を、
# この1本で通す。検査に落ちたら失敗させる——中身を確かめていないものを配布物として残さない。
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# 外部コマンドの非0終了は終了エラーにしない。終了コードを見て自分で失敗させる。
$PSNativeCommandUseErrorActionPreference = $false

$repository = Split-Path -Parent $PSScriptRoot
$hostProject = Join-Path $repository "src/HostPlugin/PmxEditorMcp.HostPlugin.csproj"
$license = Join-Path $repository "LICENSE"
$distribution = Join-Path $repository "dist"

function Get-Version {
    <#
        .SYNOPSIS
        配布の版。正本は追跡下の Directory.Build.props で、綴りから読むのではなく MSBuild に
        評価させて取る——条件や継承で決まる値を、こちらで組み立て直さない。
    #>
    $said = dotnet msbuild $hostProject -getProperty:Version
    if ($LASTEXITCODE -ne 0) { throw "版を読めない。" }

    $version = $said.Trim()
    if (-not $version) { throw "版が空である。" }

    $version
}

function Build-Host {
    <#
        .SYNOPSIS
        ホストを組み立て、出来たDLLの在り処を返す。配布物のDLLは参照するだけで、成果物へは
        写さない——再配布を禁じられているためである。
    #>
    param([string]$Version)

    dotnet build $hostProject -c Release -warnaserror | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "ホストの組み立てに失敗した。" }

    $said = dotnet msbuild $hostProject -p:Configuration=Release -getProperty:TargetPath
    if ($LASTEXITCODE -ne 0) { throw "ホストの成果物の在り処を読めない。" }

    $said.Trim()
}

$version = Get-Version
$staged = Join-Path $distribution "pmx-editor-mcp-$version"
$archive = "$staged.zip"

# 前の実行の成果を先に捨てる。落ちた回の後に古いものが同じ名前で残ると、確かめていない物を
# 確かめた物として渡してしまう。
if (Test-Path $staged) { Remove-Item -Path $staged -Recurse -Force }
if (Test-Path $archive) { Remove-Item -Path $archive -Force }
New-Item -ItemType Directory -Force -Path $staged | Out-Null

Copy-Item -Path (Build-Host -Version $version) -Destination $staged -Force
& (Join-Path $PSScriptRoot "publish-bridge.ps1") -Destination (Join-Path $staged "bridge") | Out-Null
Move-Item -Path (Join-Path $staged "bridge/PmxEditorMcp.Bridge.exe") -Destination $staged -Force
Remove-Item -Path (Join-Path $staged "bridge") -Recurse -Force
Copy-Item -Path $license -Destination (Join-Path $staged "LICENSE.txt") -Force

& (Join-Path $PSScriptRoot "package-contents.ps1") -Staged $staged -Version $version `
    -Expected @("PmxEditorMcp.dll", "PmxEditorMcp.Bridge.exe", "LICENSE.txt") | Out-Null

Compress-Archive -Path (Join-Path $staged "*") -DestinationPath $archive

Write-Host ("配布パッケージを組み立てた: " + $archive)
