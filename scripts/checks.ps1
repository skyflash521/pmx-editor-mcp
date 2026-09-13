# 検証手順書の検査の表を読み、1件ずつ走らせて合否を書く仕組み。
# 常設の検査の実行器と実機に触る検査の実行器が共に使う——表の読み方が分かれると、
# どちらかの実行器だけが手順書とずれる。

function Get-ListedChecks {
    <#
        .SYNOPSIS
        検証手順書の、その節に並ぶ検査の名前。
    #>
    param([string]$Path, [string]$Section)

    $lines = Get-Content $Path
    $from = [array]::IndexOf($lines, $Section)
    if ($from -lt 0) { throw "$Path に $Section の節が無い。" }

    $rest = $lines[($from + 1)..($lines.Count - 1)]
    $to = ($rest | Select-String -Pattern '^## ' | Select-Object -First 1).LineNumber
    if ($to) { $rest = $rest[0..($to - 2)] }

    $rest |
        Select-String -Pattern '^\| ([^|]+?) \| ' |
        ForEach-Object { $_.Matches[0].Groups[1].Value } |
        Where-Object { $_ -ne '検査' }
}

function Assert-ListedChecks {
    <#
        .SYNOPSIS
        実行器が持つ検査と、手順書の表に並ぶ検査が一致することを確かめる。片方だけへ足せば
        ここで落ちる。
    #>
    param([string]$Path, [string]$Section, [string[]]$Names)

    $listed = @(Get-ListedChecks -Path $Path -Section $Section)
    $missing = @($Names | Where-Object { $listed -notcontains $_ })
    $extra = @($listed | Where-Object { $Names -notcontains $_ })
    if ($missing.Count -eq 0 -and $extra.Count -eq 0) { return }

    throw ("$Path の $Section の一覧とこの実行器の検査がずれている。手順書に無い: " +
        (($missing -join '・'), '(無し)')[$missing.Count -eq 0] +
        ' / この実行器に無い: ' +
        (($extra -join '・'), '(無し)')[$extra.Count -eq 0])
}

function Invoke-Check {
    <#
        .SYNOPSIS
        検査を1件走らせ、落ちたらその名前を返す。通れば何も返さない。
    #>
    param([string]$Name, [scriptblock]$Body)

    $global:LASTEXITCODE = 0
    try {
        $log = & $Body 2>&1
        $code = $LASTEXITCODE
    } catch {
        $log = $_
        $code = 1
    }

    if ($code -eq 0) {
        Write-Host "OK   $Name"
        return $null
    }

    Write-Host "NG   $Name (終了コード $code)"
    # 誤りの記録をパイプへ流すと、停止の設定の下では書き出す側で終了エラーになる。文字列にして出す。
    foreach ($line in @($log)) { Write-Host ('     ' + [string]$line) }

    return $Name
}

function Write-CheckSummary {
    <#
        .SYNOPSIS
        走らせた結末を書き、終わらせる終了コードを返す。落ちた検査も走らせていない検査も
        無ければ0、あれば1とする。
    #>
    param([string[]]$Failed, [string[]]$Skipped)

    Write-Host ''
    if ($Skipped.Count -gt 0) { Write-Host ('走らせていない: ' + ($Skipped -join '・')) }
    if ($Failed.Count -gt 0) { Write-Host ('不合格: ' + ($Failed -join '・')) }
    if ($Failed.Count -gt 0 -or $Skipped.Count -gt 0) { return 1 }

    Write-Host 'すべて合格'

    0
}
