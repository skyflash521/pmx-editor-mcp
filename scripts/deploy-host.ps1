# ホストを、このセッションの導入先(editor-dir.ps1 の Get-SessionEditorDirectory)へ配置する。
# 配置先は起動中のエディタがロックしている。その導入先から動いているエディタを閉じてから配置し、
# ほかの導入先から動いているエディタには触れない。
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$PSNativeCommandUseErrorActionPreference = $false

. (Join-Path $PSScriptRoot 'editor-dir.ps1')

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src/HostPlugin/PmxEditorMcp.HostPlugin.csproj"
$control = Join-Path $PSScriptRoot "host-control.ps1"

# ホストのアセンブリ名。配置先に置かれるファイルの名前でもある。
$HostAssemblyName = "PmxEditorMcp.dll"

# 導入物がプラグインを読む場所。配置先はこの下の User で、直下は別の読み込み元になる。
$PluginDirectoryName = "_plugin"

# プラグインの読み込み元を外部へ振り替える指定のファイル。置かれていれば、その先も読み込み元になる。
$PathFileName = "user.path"

# 振り替えの指定のファイルで、行頭に置くと注記になる文字。導入物が既定で置いていく説明と例が
# この形で並ぶ。
$NoteMark = ";"

function Get-LoadDirectories {
    <#
        .SYNOPSIS
        配置先のほかにホストが読み込まれうる場所。ここに複製があると2つ読み込まれる。
    #>
    param([string]$PluginDirectory)

    $PluginDirectory

    $pathFile = Join-Path $PluginDirectory $PathFileName
    if (-not (Test-Path $pathFile)) { return }

    foreach ($line in (Get-Content $pathFile -Encoding UTF8)) {
        $named = $line.Trim()
        if ($named -and -not $named.StartsWith($NoteMark)) { $named }
    }
}

# 配置先を開いたままだとコピーに失敗する(MSB3027/MSB3021)。ホストを停止させたエディタはパイプを
# 持たないが、配置先のDLLは掴んだままである。
& $control -Action editors |
    ForEach-Object { [int]$_ } |
    ForEach-Object { & $control -Action close -ProcessId $_ | Out-Null }

$editorDirectory = Get-SessionEditorDirectory
dotnet build $project -t:Deploy "-p:PmxEditorDeployDir=$editorDirectory" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "ホストの配置に失敗した(終了コード $LASTEXITCODE)。" }

$pluginDirectory = Join-Path $editorDirectory $PluginDirectoryName
$deployed = Join-Path (Join-Path $pluginDirectory "User") $HostAssemblyName
if (-not (Test-Path $deployed)) { throw "配置したはずのホストが無い: $deployed" }

$duplicates = @(Get-LoadDirectories -PluginDirectory $pluginDirectory |
    ForEach-Object { [System.IO.Path]::Combine($_, $HostAssemblyName) } |
    Where-Object { Test-Path -LiteralPath $_ -ErrorAction Ignore })
if ($duplicates.Count -ne 0) {
    throw ("配置先のほかにもホストがあって2つ読み込まれる。取り除くこと: " +
        ($duplicates -join "・"))
}

Write-Output $deployed
