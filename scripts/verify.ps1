# 検証手順書が定める常設の検査を、この1本で走らせる。
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 外部コマンドの非0終了は終了エラーにしない。ここでは出力と終了コードをそのまま見て合否にする。
$PSNativeCommandUseErrorActionPreference = $false

. (Join-Path $PSScriptRoot 'editor-dir.ps1')

Set-Location (Split-Path -Parent $PSScriptRoot)

$editorDir = Get-EditorDirectory
$dump = 'src/SignatureDump/bin/Debug/net48/PmxEditorMcp.SignatureDump.exe'
$specs = 'docs/specs'
$ledger = "$specs/pmx-editor-mcp-capability-ledger.md"
$outOfScope = "$specs/pmx-editor-mcp-ledger-out-of-scope.json"
$roles = "$specs/pmx-editor-mcp-type-roles.json"
$names = "$specs/pmx-editor-mcp-property-names.json"
$assignments = "$specs/pmx-editor-mcp-common-assignments.json"
$toolMap = "$specs/pmx-editor-mcp-tool-map.json"
$contract = "$specs/pmx-editor-mcp-common-contract.md"
$procedure = 'docs/conventions/verification.md'

$baseline = [System.IO.Path]::GetTempFileName()
$excluded = [System.IO.Path]::GetTempFileName()

function Invoke-Check {
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

function Get-ListedChecks {
    <#
        .SYNOPSIS
        常設の検査の節に並ぶ検査の名前。
    #>
    $lines = Get-Content $procedure
    $from = [array]::IndexOf($lines, '## 常設の検査')
    if ($from -lt 0) { throw "$procedure に常設の検査の節が無い。" }
    $rest = $lines[($from + 1)..($lines.Count - 1)]
    $to = ($rest | Select-String -Pattern '^## ' | Select-Object -First 1).LineNumber
    if ($to) { $rest = $rest[0..($to - 2)] }

    $rest |
        Select-String -Pattern '^\| ([^|]+?) \| ' |
        ForEach-Object { $_.Matches[0].Groups[1].Value } |
        Where-Object { $_ -ne '検査' }
}

$build = 'ビルド'
$derivation = '除外一覧の導出'

$noArtifact = 'なし'
$buildOutput = 'ビルド成果物'
$exclusionList = '除外一覧'

try {
    $checks = [ordered]@{}
    $checks[$build] = @{
        Needs = $noArtifact
        Body = { dotnet build PmxEditorMcp.sln -warnaserror }
    }
    $checks['スクリプト構文'] = @{
        Needs = $noArtifact
        Body = { node --check scripts/e2e-check.mjs }
    }
    $checks['スクリプト構文(PowerShell)'] = @{
        Needs = $noArtifact
        Body = {
            $bad = @()
            foreach ($file in Get-ChildItem scripts/*.ps1) {
                $errors = $null
                [void][System.Management.Automation.Language.Parser]::ParseFile(
                    $file.FullName, [ref]$null, [ref]$errors)
                if ($errors) { $bad += ($file.Name + ': ' + ($errors.Message -join '; ')) }
            }
            if ($bad) { throw ($bad -join "`n") }
        }
    }
    $checks[$derivation] = @{
        Needs = $buildOutput
        Body = {
            # 凍結が落ちたらその終了コードのまま返したいので、続きを走らせずに抜ける。
            & $dump excluded-baseline $editorDir $ledger $baseline
            if ($LASTEXITCODE -eq 0) { & $dump excluded-signatures $editorDir $baseline $excluded }
        }
    }
    $checks['整形'] = @{
        Needs = $buildOutput
        Body = { dotnet format PmxEditorMcp.sln --verify-no-changes }
    }
    $checks['テスト'] = @{
        Needs = $buildOutput
        Body = { dotnet test PmxEditorMcp.sln }
    }
    $checks['台帳と正本の照合'] = @{
        Needs = $exclusionList
        Body = { & $dump ledger-coverage $editorDir $ledger $excluded $outOfScope }
    }
    $checks['日本語名の照合'] = @{
        Needs = $exclusionList
        Body = { & $dump property-names $editorDir $ledger $excluded $names }
    }
    $checks['型役割の照合'] = @{
        Needs = $exclusionList
        Body = { & $dump type-roles $editorDir $ledger $excluded $roles }
    }
    $checks['共通契約割当の照合'] = @{
        Needs = $exclusionList
        Body = { & $dump common-assignments $editorDir $ledger $excluded $roles $assignments }
    }
    $checks['値の表現の照合'] = @{
        Needs = $exclusionList
        Body = { & $dump value-shapes $editorDir $ledger $excluded $contract }
    }
    $checks['危険操作の照合'] = @{
        Needs = $exclusionList
        Body = { & $dump dangerous-operations $editorDir $ledger $excluded }
    }
    $checks['能力対応表の照合'] = @{
        Needs = $exclusionList
        Body = { & $dump tool-map $editorDir $ledger $excluded $roles $assignments $toolMap }
    }

    $listed = @(Get-ListedChecks)
    $missing = @($checks.Keys | Where-Object { $listed -notcontains $_ })
    $extra = @($listed | Where-Object { $checks.Keys -notcontains $_ })
    if ($missing.Count -gt 0 -or $extra.Count -gt 0) {
        throw ("$procedure の一覧とこのスクリプトの検査がずれている。手順書に無い: " +
            (($missing -join '・'), '(無し)')[$missing.Count -eq 0] +
            ' / このスクリプトに無い: ' +
            (($extra -join '・'), '(無し)')[$extra.Count -eq 0])
    }

    $failed = @()
    $skipped = @()
    $produced = @($noArtifact)

    foreach ($name in $checks.Keys) {
        if ($produced -notcontains $checks[$name].Needs) {
            $skipped += $name
            continue
        }

        $result = Invoke-Check -Name $name -Body $checks[$name].Body
        if ($result) {
            $failed += $result
            continue
        }

        if ($name -eq $build) { $produced += $buildOutput }
        if ($name -eq $derivation) { $produced += $exclusionList }
    }
} finally {
    Remove-Item $baseline, $excluded -ErrorAction SilentlyContinue
}

Write-Host ''
if ($skipped.Count -gt 0) {
    Write-Host ('走らせていない: ' + ($skipped -join '・'))
}

if ($failed.Count -gt 0) {
    Write-Host ('不合格: ' + ($failed -join '・'))
}

if ($failed.Count -gt 0 -or $skipped.Count -gt 0) {
    exit 1
}

Write-Host 'すべて合格'
