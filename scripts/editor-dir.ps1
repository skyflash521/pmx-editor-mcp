# 導入先の読み取りは検証の実行器と操作役の両方が要るので、採り方が分かれないようここへ置く。

function Get-EditorDirectory {
    <#
        .SYNOPSIS
        PMXエディタの導入先を local.props から読む。XMLとして読むのは、値に含まれる実体参照を
        元の文字へ戻すため。ビルドが採る値を一意に決められない書き方は、決められない旨で止める。
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

    # 条件や選択の構造の下にあると、ビルドが採る値はMSBuildの評価に依る。祖先まで遡って見る。
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

        # 複製し終える前の状態を導入先として見せないよう、別の名前で作ってから名前を変える。
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
    $running = @(Get-Process -Name "PmxEditor_x64" -ErrorAction Ignore | ForEach-Object {
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
