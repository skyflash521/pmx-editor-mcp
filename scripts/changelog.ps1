# 変更履歴の形を確かめる。先頭の節が渡されたバージョンを名乗ることも見る。
# -Notes を渡すと、通ったときに先頭の節の本文(見出しの行を除く)を書き出す。Release の本文はこれを使う。
[CmdletBinding()]
param(
    # 確かめる変更履歴。
    [Parameter(Mandatory = $true)]
    [string]$Path,

    # 先頭の節が名乗るはずのバージョン。
    [Parameter(Mandatory = $true)]
    [string]$Version,

    # 先頭の節の本文を書き出す。
    [switch]$Notes
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Title = '# 変更履歴'
$SectionPattern = '^## (\d+\.\d+\.\d+) - \d{4}-\d{2}-\d{2}$'
$Categories = @('互換性の無い変更', '追加', '変更', '修正')

$sections = [System.Collections.Generic.List[object]]::new()
$current = $null
$titleSeen = $false
$number = 0

foreach ($line in Get-Content -Path $Path -Encoding UTF8) {
    $number++
    if ($line -match '^## ') {
        if (-not $titleSeen) { throw "CHANGELOG_TITLE: ${number}行目: 「$Title」より前にバージョンの見出しがある。" }
        if ($line -notmatch $SectionPattern) {
            throw "CHANGELOG_HEADING: ${number}行目: バージョンの見出しが「## <MAJOR.MINOR.PATCH> - <YYYY-MM-DD>」の形でない: $line"
        }
        $current = [pscustomobject]@{ Version = $Matches[1]; Line = $number; Body = [System.Collections.Generic.List[string]]::new() }
        $sections.Add($current)
    } elseif ($line -match '^### ') {
        if ($null -eq $current) { throw "CHANGELOG_STRAY: ${number}行目: 区分の見出しがバージョンの節の外にある: $line" }
        if ($Categories -notcontains $line.Substring(4)) {
            throw "CHANGELOG_CATEGORY: ${number}行目: 区分は $($Categories -join '・') のどれか: $line"
        }
        $current.Body.Add($line)
    } elseif ($line -match '^#') {
        if ($titleSeen -or $line -ne $Title) { throw "CHANGELOG_TITLE: ${number}行目: 見出しは先頭の「$Title」だけ: $line" }
        $titleSeen = $true
    } elseif ($null -ne $current) {
        $current.Body.Add($line)
    } elseif ($line.Trim()) {
        if (-not $titleSeen) { throw "CHANGELOG_TITLE: ${number}行目: 先頭が「$Title」でない: $line" }
        throw "CHANGELOG_STRAY: ${number}行目: バージョンの節の外に本文がある: $line"
    }
}

if (-not $titleSeen) { throw "CHANGELOG_TITLE: 「$Title」が無い。" }
if ($sections.Count -eq 0) { throw "CHANGELOG_EMPTY: バージョンの節が1つも無い。" }

$previous = $null
foreach ($section in $sections) {
    $at = [version]$section.Version
    if ($null -ne $previous -and $at -ge $previous) {
        throw "CHANGELOG_ORDER: $($section.Line)行目: $($section.Version) が新しい順に並んでいない。"
    }
    $previous = $at

    $filled = @($section.Body | Where-Object { $_.Trim() -and -not $_.StartsWith('###') })
    if ($filled.Count -eq 0) { throw "CHANGELOG_EMPTY: $($section.Version) の節に本文が無い。" }

    $category = $null
    $lines = 0
    foreach ($text in @($section.Body) + @('### ')) {
        if ($text.StartsWith('###')) {
            if ($null -ne $category -and $lines -eq 0) {
                throw "CHANGELOG_EMPTY: $($section.Version) の「$category」に変更の行が無い。"
            }
            $category = $text.Substring(4)
            $lines = 0
        } elseif ($text.Trim()) {
            $lines++
        }
    }
}

if ($sections[0].Version -ne $Version) {
    throw "CHANGELOG_VERSION: 先頭の節 $($sections[0].Version) が $Version と合わない。"
}

if ($Notes) {
    ($sections[0].Body -join "`n").Trim()
}
