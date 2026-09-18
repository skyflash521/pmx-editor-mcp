# 自動E2E検査の実行器を確かめるための、画像を見比べる相手の代わり。
# 実際の明るさは測らず、写しと同じ中身かどうかだけを見て、明るさの差として0か1を組ごとに返す。
[CmdletBinding()]
param(
    # 写し取ったビューの置き場。-Candidate と同じ数を同じ並びで渡す。
    [Parameter(Mandatory = $true)]
    [string[]]$Reference,

    # 見比べる画像の置き場。実行器が応答の中身をそのまま書き出す。
    [Parameter(Mandatory = $true)]
    [string[]]$Candidate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Reference.Count -ne $Candidate.Count) {
    throw "写しと比べる画像の数が違う: $($Reference.Count) 対 $($Candidate.Count)"
}

for ($pair = 0; $pair -lt $Reference.Count; $pair++) {
    $written = (Get-Content -Path $Reference[$pair] -Raw -Encoding UTF8).Trim()
    $given = (Get-Content -Path $Candidate[$pair] -Raw -Encoding UTF8).Trim()

    # 同じ中身なら差は無い。違えば、合うと見なす上限を超える差を返す。
    if ($given -eq $written) { Write-Output '0' } else { Write-Output '1' }
}
