# 自動E2E検査。ツール個別の確認はこれが担う。
# 能力対応表とスキーマ定義から検査を機械生成し、実機のエディタの待受へ投げて、行キー・編集の
# 流れ・接続の経路ごとに合否を出す。
# 生成した検査は一時領域へ書く——入力から導ける値なので、追跡下に置かない。
[CmdletBinding()]
param(
    # 突き合わせる能力対応表の版。指すと、その版から中身の変わった行の検査だけを走らせる。
    [string]$Since
)

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

$map = 'catalog/authored/tool-map.json'

# 指した行に当たる検査を引けなかったときに実行器が返す終了コード。実行器の実装が定める。
$unfiltered = 4

function Get-ChangedRows {
    <#
        .SYNOPSIS
        指した版といまの能力対応表を突き合わせ、中身の変わった行のキーを並べたファイルを返す。
    #>
    param([string]$Ref)

    $named = Join-Path ([System.IO.Path]::GetTempPath()) (
        'pmx-editor-mcp-tool-map-' + [guid]::NewGuid().ToString('N') + '-')
    $before = $named + 'before.json'
    $rows = $named + 'rows.txt'

    # 対応表は日本語を含む。端末の設定のまま読むと、取り出した版が別物になり全行が変わって見える。
    try {
        $spoken = [Console]::OutputEncoding
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
        try {
            git show "${Ref}:$map" | Set-Content -Path $before -Encoding utf8
            if ($LASTEXITCODE -ne 0) { throw "指した版の能力対応表を取り出せない: $Ref" }
        } finally {
            [Console]::OutputEncoding = $spoken
        }

        $changed = & $dump changed-rows $before $map
        if ($LASTEXITCODE -ne 0) { throw "変わった行を選べない(終了コード $LASTEXITCODE)。" }

        # 受け取ったものを書く。流し込みで書くと、1行も返らなかったときにファイルごと現れない。
        Set-Content -Path $rows -Value (@($changed) -join [Environment]::NewLine) -Encoding utf8
    } finally {
        Remove-Item $before -ErrorAction Ignore
    }

    return $rows
}

$editor = 0
$rows = $null
try {
    dotnet build $generator | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "生成器のビルドに失敗した(終了コード $LASTEXITCODE)。" }

    & $dump e2e-cases (Get-EditorDirectory) `
        catalog/observed/capability-ledger.json `
        catalog/authored/common-contract.json `
        catalog/authored/type-roles.json `
        catalog/authored/property-names.json `
        catalog/authored/common-assignments.json `
        $map `
        catalog/authored/tool-schemas.json `
        catalog/authored/sample-values.json `
        $cases
    if ($LASTEXITCODE -ne 0) { throw "検査を組み立てられない(終了コード $LASTEXITCODE)。" }

    # 実行器が先に配置を済ませていれば繰り返さない。単独で走らせたときは印が無いので自分で行う。
    if ($env:PMX_EDITOR_MCP_PREPARED -ne '1') { & scripts/deploy-host.ps1 | Out-Null }

    if ($Since) { $rows = Get-ChangedRows -Ref $Since }

    if ($rows -and -not (Get-Content $rows)) {
        Write-Host "指した版から中身の変わった行が無い。"
        return
    }

    $editor = [int](& $control -Action launch)

    if ($rows) {
        node scripts/e2e-tools.mjs $editor $cases --rows $rows
    } else {
        node scripts/e2e-tools.mjs $editor $cases
    }
    $ran = $LASTEXITCODE
    $global:LASTEXITCODE = 0
    if ($ran -eq $unfiltered) { throw "絞った実行では確かめられない行がある。" }
    if ($ran -ne 0) { throw "不合格の検査がある(終了コード $ran)。" }
} finally {
    if ($editor -ne 0 -and (Get-Process -Id $editor -ErrorAction Ignore)) {
        & $control -Action close -ProcessId $editor | Out-Null
    }
    Remove-Item $cases -ErrorAction Ignore
    if ($rows) { Remove-Item $rows -ErrorAction Ignore }
}
