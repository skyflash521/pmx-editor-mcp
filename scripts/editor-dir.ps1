# PMXエディタの導入先を決める関数の置き場。dot-source して使う。

function Get-EditorDirectory {
    <#
        .SYNOPSIS
        PMXエディタの導入先を local.props から読む。ビルドが採る値を一意に決められない書き方は、
        決められない旨で止める。
    #>
    $propsPath = Join-Path (Split-Path -Parent $PSScriptRoot) "local.props"
    if (-not (Test-Path $propsPath)) {
        throw "local.props が無い。PmxEditorDir を定義する必要がある: $propsPath"
    }

    $document = New-Object System.Xml.XmlDocument
    $document.Load((Resolve-Path $propsPath))
    $nodes = @($document.GetElementsByTagName("PmxEditorDir"))
    if ($nodes.Count -eq 0) { throw "local.props に PmxEditorDir が無い: $propsPath" }
    if ($nodes.Count -gt 1) {
        throw "local.props の PmxEditorDir が $($nodes.Count) 個ある。ビルドがどれを採る" +
            "かはMSBuildの評価に依るので、ここでは決められない: $propsPath"
    }

    # 条件や選択の構造の下にあると、ビルドが採る値はMSBuildの評価に依る。
    $node = $nodes[0]
    for ($ancestor = $node; $ancestor -is [System.Xml.XmlElement]; $ancestor = $ancestor.ParentNode) {
        if ($ancestor.HasAttribute("Condition")) {
            throw "PmxEditorDir が条件付きの $($ancestor.Name) の下にあって、ここでは" +
                "解決できない: $propsPath"
        }
        if (@("Choose", "When", "Otherwise") -contains $ancestor.Name) {
            throw "PmxEditorDir が $($ancestor.Name) の下にあって、ここでは解決できない: $propsPath"
        }
    }

    $value = $node.InnerText.Trim()
    if ($value -match "\`$\(") {
        throw "PmxEditorDir がMSBuildの式を含んでいて、ここでは解決できない: $value"
    }

    $value
}

$EditorFileNames = @("PmxEditor.exe", "PmxEditor_*.exe")

$EditorProcessNames = @("PmxEditor", "PmxEditor_*")

# PEのヘッダが名乗る機械の種別。x64のもの。
$EditorAmdMachine = 0x8664

function Test-EditorExecutable {
    <#
        .SYNOPSIS
        そのファイルがエディタの実行ファイルか。名前が $EditorFileNames のどれかに当たり、x64 の
        実行形式であるものをエディタとみなす。読めなければ偽を返す。
    #>
    param([string]$Path)

    $name = [System.IO.Path]::GetFileName($Path)
    if (-not ($EditorFileNames | Where-Object { $name -like $_ })) {
        return $false
    }

    try {
        $stream = [System.IO.File]::OpenRead($Path)
    }
    catch { return $false }

    try {
        $reader = New-Object System.IO.BinaryReader($stream)
        if ($stream.Length -lt 0x40) { return $false }
        $stream.Position = 0x3C
        $header = [long]$reader.ReadInt32()
        if ($header -lt 0 -or $header + 6 -gt $stream.Length) { return $false }
        $stream.Position = $header
        ($reader.ReadUInt32() -eq 0x00004550) -and ($reader.ReadUInt16() -eq $EditorAmdMachine)
    }
    finally { $stream.Dispose() }
}

function Get-EditorExecutable {
    <#
        .SYNOPSIS
        導入フォルダの直下にあるエディタの実行ファイルの道を返す。1つに決まらなければ止める。
    #>
    param([string]$Directory)

    $found = @(Get-ChildItem -LiteralPath $Directory -File -ErrorAction Ignore |
        Where-Object { Test-EditorExecutable -Path $_.FullName })
    if ($found.Count -ne 1) {
        throw "導入フォルダ直下の x64 の $($EditorFileNames -join " か ") が1つに決まらない($($found.Count) 件): $Directory"
    }

    $found[0].FullName
}

# セッションごとの導入先を並べる親。
$SessionEditorRoot = Join-Path $env:LOCALAPPDATA "pmx-editor-mcp-dev-editors"

# 複製の元を書き留めるファイルの名前。書き換えた時刻が、そのセッションが最後に使った時刻になる。
$SessionEditorSourceFileName = ".source"

# 使われなくなった複製を消すまでの日数。
$SessionEditorKeepDays = 7

function Get-SessionEditorDirectory {
    <#
        .SYNOPSIS
        エディタを起動しホストを配置する導入先を返す。Claude Code のセッションの中では、local.props
        の導入先をセッションごとの置き場へ複製したものを返し、無ければ作る。セッションの外では
        local.props の導入先をそのまま返す。
    #>
    $shared = Get-EditorDirectory
    $session = $env:CLAUDE_CODE_SESSION_ID
    if ([string]::IsNullOrEmpty($session)) { return $shared }

    $sharedFull = [System.IO.Path]::GetFullPath($shared)
    $directory = Join-Path $SessionEditorRoot $session
    $sourceFile = Join-Path $directory $SessionEditorSourceFileName

    if (Test-Path -LiteralPath $directory) {
        $recorded = if (Test-Path -LiteralPath $sourceFile) {
            (Get-Content -LiteralPath $sourceFile -Raw -Encoding UTF8).Trim()
        } else { "" }
        if (-not [string]::Equals($recorded, $sharedFull, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "このセッションの導入先 $directory は $recorded の複製で、local.props の " +
                "$sharedFull と違う。エディタを閉じてからそのフォルダを消す。"
        }
    }
    else {
        Remove-UnusedSessionEditorDirectories

        $partial = "$directory.part"
        if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial -Recurse -Force }
        New-Item -ItemType Directory -Path $partial -Force | Out-Null
        Copy-Item -Path (Join-Path $sharedFull "*") -Destination $partial -Recurse -Force
        Set-Content -LiteralPath (Join-Path $partial $SessionEditorSourceFileName) `
            -Value $sharedFull -Encoding UTF8 -NoNewline
        Rename-Item -LiteralPath $partial -NewName (Split-Path -Leaf $directory)
    }

    (Get-Item -LiteralPath $sourceFile).LastWriteTime = Get-Date
    $directory
}

function Remove-UnusedSessionEditorDirectories {
    <#
        .SYNOPSIS
        ほかのセッションの導入先のうち、決めた日数のあいだ使われておらず、そこからエディタも
        動いていないものを消す。消せないものは残す。
    #>
    if (-not (Test-Path -LiteralPath $SessionEditorRoot)) { return }

    $limit = (Get-Date).AddDays(-$SessionEditorKeepDays)
    $running = @(Get-Process -Name $EditorProcessNames -ErrorAction Ignore | ForEach-Object {
        try { $_.Path } catch { $null }
    } | Where-Object { $_ })

    foreach ($candidate in @(Get-ChildItem -LiteralPath $SessionEditorRoot -Directory)) {
        $sourceFile = Join-Path $candidate.FullName $SessionEditorSourceFileName
        $used = if (Test-Path -LiteralPath $sourceFile) {
            (Get-Item -LiteralPath $sourceFile).LastWriteTime
        } else { $candidate.LastWriteTime }
        if ($used -gt $limit) { continue }

        $prefix = $candidate.FullName.TrimEnd('\') + '\'
        if ($running | Where-Object { $_.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase) }) {
            continue
        }

        Remove-Item -LiteralPath $candidate.FullName -Recurse -Force -ErrorAction Ignore
    }
}
