# ブリッジを発行する。発行の指定を持つのはここだけとする。
# 受け取った側の実行環境には .NET のランタイムが無いので、同梱して単一のexeにまとめ、ネイティブの
# DLLも中へ入れる——exe1ファイルだけで動くのが配布の契約である。指定をプロジェクトへ置くと、
# 参照するテストの出力までランタイムを同梱した形に変わり、そこからは起動できなくなる。
[CmdletBinding()]
param(
    # 発行先。
    [Parameter(Mandatory = $true)]
    [string]$Destination,

    # ランタイムを同梱するか。偽にできるのは、同梱しない実行ファイルが共有ランタイムを
    # 解決できない環境で起動に失敗することを確かめる検査のためだけである。
    [bool]$SelfContained = $true,

    # 出荷台帳の書き出し先。渡すと、この発行が解決した資産をそのまま書き出す——別に解決を
    # 走らせると、数えた物と出荷した物が別の結果になりうる。
    [string]$Ledger
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# 外部コマンドの非0終了は終了エラーにしない。終了コードを見て自分で失敗させる。
$PSNativeCommandUseErrorActionPreference = $false

$project = Join-Path (Split-Path -Parent $PSScriptRoot) "src/Bridge/PmxEditorMcp.Bridge.csproj"

$ledgering = @()
if ($Ledger) {
    $ledgering = @(
        "-p:CustomAfterMicrosoftCommonTargets=$(Join-Path $PSScriptRoot 'shipping-ledger.targets')",
        "-p:ShippingLedgerPath=$Ledger")
}

dotnet publish $project `
    -c Release `
    -r win-x64 `
    -o $Destination `
    "-p:SelfContained=$($SelfContained.ToString().ToLowerInvariant())" `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true @ledgering | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "ブリッジの発行に失敗した(ランタイムの同梱=$SelfContained)。"
}

Join-Path $Destination "PmxEditorMcp.Bridge.exe"
