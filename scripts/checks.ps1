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

class Check {
    # 合格の条件であって所要の見積りではない。一度決めた値は変更禁止とする——掛かる時間が上限へ
    # 届いたら、上限ではなく掛かる時間を減らす。
    [double]$LimitSeconds
    [string]$Needs
    # 群は、その検査が読む入力ごとに分ける。数えるのは引数に現れるファイルだけではない——Body が
    # 呼ぶスクリプトも、その呼び先が中で読むファイルも、その実行ファイルを作る場所も入力である。
    [string[]]$Groups
    [string]$Produces
    [int]$Stage = 2
    [string]$Bundle
    [string[]]$Run
    [scriptblock]$Body
    [scriptblock]$Forms
    [bool]$FormsInOrder
    [string]$FormArgument = '-Form'
    [string]$ResultsArgument = '-Results'

    Check([double]$limitSeconds, [string]$needs, [string[]]$groups) {
        if ($limitSeconds -le 0) { throw "上限の秒数が0以下の検査は作れない: $limitSeconds" }

        $this.LimitSeconds = $limitSeconds
        $this.Needs = $needs
        $this.Groups = $groups
    }
}

function New-Check {
    param(
        [Parameter(Mandatory)][double]$LimitSeconds,
        [Parameter(Mandatory)][string]$Needs,
        [Parameter(Mandatory)][string[]]$Groups,
        [string]$Produces,
        [int]$Stage = 2,
        [string]$Bundle,
        [string[]]$Run,
        [scriptblock]$Body,
        [scriptblock]$Forms,
        [switch]$FormsInOrder,
        [string]$FormArgument = '-Form',
        [string]$ResultsArgument = '-Results'
    )

    $one = [Check]::new($LimitSeconds, $Needs, $Groups)
    if ($Produces) { $one.Produces = $Produces }
    $one.Stage = $Stage
    if ($Bundle) { $one.Bundle = $Bundle }
    $one.Run = $Run
    $one.Body = $Body
    $one.Forms = $Forms
    $one.FormsInOrder = [bool]$FormsInOrder
    $one.FormArgument = $FormArgument
    $one.ResultsArgument = $ResultsArgument

    $one
}

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
