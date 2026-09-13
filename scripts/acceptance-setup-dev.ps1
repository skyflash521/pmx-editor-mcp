# 受入の導入の前置のうち、開発配置の経路。
# ホストをデプロイターゲットで配置し、ブリッジのビルド成果物を受入の実行器が起こす相手として返す。
# 受入の実行器はこのスクリプトの中身を解さず、最後の行へ書いた組だけを読む。
[CmdletBinding()]
param(
    # 行う前置。prepare は導入を済ませ、起こす相手を書き出す。
    [Parameter(Mandatory = $true)]
    [ValidateSet("prepare")]
    [string]$Action
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# 外部コマンドの非0終了は終了エラーにしない。終了コードを見て自分で失敗させる。
$PSNativeCommandUseErrorActionPreference = $false

$root = Split-Path -Parent $PSScriptRoot
$hostProject = Join-Path $root "src/HostPlugin/PmxEditorMcp.HostPlugin.csproj"
$bridgeProject = Join-Path $root "src/Bridge/PmxEditorMcp.Bridge.csproj"
$bridgeExe = Join-Path $root "src/Bridge/bin/Debug/net10.0/PmxEditorMcp.Bridge.exe"

# 配置先は起動中のエディタがロックしている。開いたままだとコピーに失敗するので先に閉じる。
# 数えるのは動いているエディタで、待受ではない——ホストを停止させたエディタはパイプを持たないが、
# 配置先のDLLは掴んだままである。
& (Join-Path $PSScriptRoot "host-control.ps1") -Action editors |
    ForEach-Object { [int]$_ } |
    ForEach-Object {
        & (Join-Path $PSScriptRoot "host-control.ps1") -Action close -ProcessId $_ | Out-Null
    }

dotnet build $hostProject -t:Deploy | Out-Null
if ($LASTEXITCODE -ne 0) { throw "ホストの配置に失敗した(終了コード $LASTEXITCODE)。" }

dotnet build $bridgeProject | Out-Null
if ($LASTEXITCODE -ne 0) { throw "ブリッジのビルドに失敗した(終了コード $LASTEXITCODE)。" }

if (-not (Test-Path $bridgeExe)) { throw "ブリッジの実行ファイルが無い: $bridgeExe" }

[pscustomobject]@{
    command   = (Resolve-Path $bridgeExe).Path
    arguments = @()
} | ConvertTo-Json -Compress
