# 自動E2E検査。ツール個別の確認はこれが担う。
# 能力対応表とスキーマ定義から検査を機械生成し、実機のエディタの待受へ投げて、行キー・編集の
# 流れ・接続の経路ごとに合否を出す。
# 生成した検査は一時領域へ書く——入力から導ける値なので、追跡下に置かない。
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 外部コマンドの非0終了は終了エラーにしない。終了コードを見て自分で失敗させる。
$PSNativeCommandUseErrorActionPreference = $false

. (Join-Path $PSScriptRoot 'editor-dir.ps1')

Set-Location (Split-Path -Parent $PSScriptRoot)

$control = 'scripts/host-control.ps1'
$generator = 'src/SignatureDump/PmxEditorMcp.SignatureDump.csproj'
$dump = 'src/SignatureDump/bin/Debug/net48/PmxEditorMcp.SignatureDump.exe'

$cases = Join-Path ([System.IO.Path]::GetTempPath()) (
    'pmx-editor-mcp-e2e-cases-' + [guid]::NewGuid().ToString('N') + '.json')

$editor = 0
try {
    dotnet build $generator | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "生成器のビルドに失敗した(終了コード $LASTEXITCODE)。" }

    & $dump e2e-cases (Get-EditorDirectory) `
        catalog/observed/capability-ledger.json `
        catalog/authored/common-contract.json `
        catalog/authored/type-roles.json `
        catalog/authored/property-names.json `
        catalog/authored/common-assignments.json `
        catalog/authored/tool-map.json `
        catalog/authored/tool-schemas.json `
        catalog/authored/sample-values.json `
        $cases
    if ($LASTEXITCODE -ne 0) { throw "検査を組み立てられない(終了コード $LASTEXITCODE)。" }

    & scripts/deploy-host.ps1 | Out-Null

    $editor = [int](& $control -Action launch)

    node scripts/e2e-tools.mjs $editor $cases
    $ran = $LASTEXITCODE
    $global:LASTEXITCODE = 0
    if ($ran -ne 0) { throw "不合格の検査がある(終了コード $ran)。" }

    # 編集を伴う検査が実際に編集を起こしたことを、1回分を戻せることで見る。取り消せる編集が
    # 無ければこの操作は失敗するので、成功は1回の取り消しが起きたことを意味する。
    & $control -Action undo -ProcessId $editor | Out-Null
} finally {
    if ($editor -ne 0 -and (Get-Process -Id $editor -ErrorAction Ignore)) {
        & $control -Action close -ProcessId $editor | Out-Null
    }
    Remove-Item $cases -ErrorAction Ignore
}
