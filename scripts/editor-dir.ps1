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
