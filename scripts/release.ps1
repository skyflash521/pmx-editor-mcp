# 配布のバージョンの GitHub Release を作る。push 済みのタグ v<バージョン> が指すコミットを取り出した
# 作業ツリーで走らせる。変更履歴の先頭の節を本文にし、その場で組み立てた配布パッケージのzipを添える。
# 前提のどれかが欠けていれば、何も作らずに失敗させる。
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# 外部コマンドが0以外で終わっても例外にしない。終了コードを見て自分で失敗させる。
$PSNativeCommandUseErrorActionPreference = $false

. (Join-Path $PSScriptRoot "version.ps1")

$repository = Split-Path -Parent $PSScriptRoot
Set-Location $repository

$version = Get-Version
$tag = "v$version"

$dirty = git status --porcelain
if ($LASTEXITCODE -ne 0) { throw "作業ツリーの状態を読めない。" }
if ($dirty) { throw "RELEASE_DIRTY: 作業ツリーにコミットしていない変更か未追跡のファイルがある。" }

if ((git cat-file -t $tag 2>$null) -ne "tag") {
    throw "RELEASE_TAG: git tag -a で作ったタグ $tag が無い。"
}
if ((git rev-parse "$tag^{commit}") -ne (git rev-parse HEAD)) {
    throw "RELEASE_TAG: タグ $tag が作業ツリーのコミットを指していない。"
}

$remote = git ls-remote origin "refs/tags/$tag"
if ($LASTEXITCODE -ne 0) { throw "origin を読めない。" }
if (-not $remote -or ($remote -split "\s+")[0] -ne (git rev-parse $tag)) {
    throw "RELEASE_TAG: origin のタグ $tag が手元のタグと同じでない。push 済みか確かめる。"
}

git fetch --quiet origin main
if ($LASTEXITCODE -ne 0) { throw "origin の main を取れない。" }
git merge-base --is-ancestor $tag origin/main
if ($LASTEXITCODE -ne 0) { throw "RELEASE_TAG: タグ $tag が origin の main に含まれていない。" }

$url = git remote get-url origin
if ($LASTEXITCODE -ne 0 -or $url -notmatch '[:/]([^/:]+/[^/]+?)(\.git)?/?$') {
    throw "origin の URL から GitHub のリポジトリを読めない: $url"
}
$github = $Matches[1]

$released = gh release list -R $github --limit 1000 --json tagName --jq '.[].tagName'
if ($LASTEXITCODE -ne 0) { throw "$github の Release の一覧を読めない。" }
if (@($released) -contains $tag) { throw "RELEASE_EXISTS: $tag の Release は既に在る。" }

$changelog = Join-Path ([System.IO.Path]::GetTempPath()) ("changelog-" + [guid]::NewGuid().ToString("N") + ".md")
git show "${tag}:CHANGELOG.md" | Set-Content -Path $changelog -Encoding UTF8
if ($LASTEXITCODE -ne 0) { throw "RELEASE_CHANGELOG: タグ $tag のコミットに CHANGELOG.md が無い。" }
try {
    $notes = (& (Join-Path $PSScriptRoot "changelog.ps1") -Path $changelog -Version $version -Notes) -join "`n"
} finally {
    Remove-Item -Path $changelog -Force -ErrorAction Ignore
}

$archive = @(pwsh -NoProfile -File (Join-Path $PSScriptRoot "package.ps1"))[-1]
if ($LASTEXITCODE -ne 0) { throw "配布パッケージを組み立てられない。" }
if ((Split-Path -Leaf $archive) -ne "pmx-editor-mcp-$version.zip") {
    throw "RELEASE_VERSION: 組み立てた配布パッケージ $archive が $tag のものでない。"
}

$notesFile = Join-Path ([System.IO.Path]::GetTempPath()) ("release-notes-" + [guid]::NewGuid().ToString("N") + ".md")
Set-Content -Path $notesFile -Value $notes -Encoding UTF8
try {
    gh release create $tag $archive -R $github --verify-tag --title "pmx-editor-mcp $tag" --notes-file $notesFile
    if ($LASTEXITCODE -ne 0) { throw "Release を作れない。" }
} finally {
    Remove-Item -Path $notesFile -Force -ErrorAction Ignore
}

Write-Host ("Release を作った: " + $tag)
