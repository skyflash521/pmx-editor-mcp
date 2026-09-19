# 常設の検査の一覧と実機に触る検査の一覧が共に使う値と部品。
# 検査を走らせる仕組みは枠組み(node:test)と [checks.mjs](checks.mjs) が持つ。

<#
    出来上がりを要さない検査の印。
#>
$noArtifact = 'なし'

<#
    組み立てが作る出来上がりの印。
#>
$buildOutput = 'ビルド成果物'

<#
    除外一覧の導出が作る出来上がりの印。
#>
$exclusionList = '除外一覧'

<#
    実機の前置が作る出来上がりの印。配置済みのホストと、組み立て済みのブリッジを指す。
#>
$liveSetup = '実機の前置'

function Get-CheckWorkDirectory {
    <#
        .SYNOPSIS
        束をまたいで持ち越す出来上がりの置き場。入口が指していなければその場限りの置き場を作る。
    #>
    if ($env:PMX_EDITOR_MCP_WORK) { return $env:PMX_EDITOR_MCP_WORK }

    $made = Join-Path ([System.IO.Path]::GetTempPath()) ('pmx-editor-mcp-work-' + [guid]::NewGuid().ToString('N'))
    [void][System.IO.Directory]::CreateDirectory($made)

    $made
}

function Get-FellNumbers {
    <#
        .SYNOPSIS
        実行器が書き残した、落ちた項目の番号。何も書かれていなければ空。
    #>
    param([string]$Path)

    $raw = Get-Content $Path -Raw -ErrorAction Ignore
    if (-not $raw) { return @() }

    @($raw.Split(',') | Where-Object { $_.Trim() } | ForEach-Object { [int]$_.Trim() })
}
