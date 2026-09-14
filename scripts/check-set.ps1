# 常設の検査の中身と、群を選んで走らせる仕組み。読み込む側は群を指定して Invoke-Checks を呼ぶ。
# 検査ごとの実行器がこれを共有する——中身がここ1か所なので、群を分けても検査の定義は割れない。

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 外部コマンドの非0終了は終了エラーにしない。ここでは出力と終了コードをそのまま見て合否にする。
$PSNativeCommandUseErrorActionPreference = $false

. (Join-Path $PSScriptRoot 'editor-dir.ps1')
. (Join-Path $PSScriptRoot 'acceptance-stub-shared.ps1')
. (Join-Path $PSScriptRoot 'checks.ps1')

Set-Location (Split-Path -Parent $PSScriptRoot)

$editorDir = Get-EditorDirectory
$dump = 'src/SignatureDump/bin/Debug/net48/PmxEditorMcp.SignatureDump.exe'
$hostDll = 'src/HostPlugin/bin/Debug/net48/PmxEditorMcp.dll'
$observed = 'catalog/observed'
$authored = 'catalog/authored'
$ledger = "$observed/capability-ledger.json"
$outOfScope = "$observed/ledger-out-of-scope.json"
$roles = "$authored/type-roles.json"
$names = "$authored/property-names.json"
$assignments = "$authored/common-assignments.json"
$toolMap = "$authored/tool-map.json"
$toolSchemas = "$authored/tool-schemas.json"
$sampleValues = "$authored/sample-values.json"
$discoveryTasks = "$authored/discovery-tasks.json"
$contract = "$authored/common-contract.json"
$acceptance = "$authored/acceptance-scenarios.json"
$requirements = 'docs/specs/requirements.md'
$procedure = 'docs/conventions/verification.md'

$baseline = [System.IO.Path]::GetTempFileName()
$excluded = [System.IO.Path]::GetTempFileName()

function Invoke-AcceptanceRunner {
    <#
        .SYNOPSIS
        応答を作る相手と操作役の代わりを立てて受入の実行器を通しで走らせ、終了コードと書き出した
        ものを返す。Broken を与えると、At が指すツールの呼び出しで、その形の期待だけを違えさせる。
    #>
    param([string]$Cases, [string]$Broken, [int]$At)

    $said = node scripts/acceptance.mjs --cases $Cases `
        --setup scripts/acceptance-setup-stub.ps1 `
        --control scripts/acceptance-stub-control.ps1 `
        --setup-arg -Cases --setup-arg $Cases `
        --setup-arg -Broken --setup-arg $Broken `
        --setup-arg -At --setup-arg $At
    $code = $LASTEXITCODE
    $global:LASTEXITCODE = 0

    [pscustomobject]@{ Code = $code; Said = ($said -join "`n") }
}

function Get-AcceptanceExpectationForms {
    <#
        .SYNOPSIS
        期待の形ごとに、それが初めて現れるツールの呼び出しの番を返す。形の名前は定義から拾うので、
        形を足しても拾い直しは要らない。接続先の知らせだけは、名乗る形と移った形を別の形と見る。
    #>
    param($Defined)

    $forms = [ordered]@{}
    $at = 0
    foreach ($step in ($Defined.scenarios.steps | Where-Object { $_.kind -eq 'tool' })) {
        $at++
        foreach ($name in $step.expect.PSObject.Properties.Name) {
            $form = $name
            $told = $name -eq 'notice' -and
                $step.expect.notice.PSObject.Properties.Name -notcontains 'editor'
            if ($told) { $form = 'notice.changed' }

            if (-not $forms.Contains($form)) { $forms[$form] = $at }
        }
    }

    $forms
}

function Get-AcceptanceOperations {
    <#
        .SYNOPSIS
        定義が求めるエディタとホストの操作を、頼まれる順に並べる。
    #>
    param($Defined)

    $live = 0
    foreach ($step in ($Defined.scenarios.steps | Where-Object { $_.kind -eq 'control' })) {
        if ($step.action -eq 'closeAll') {
            'editors'
            for ($at = 0; $at -lt $live; $at++) { 'close' }
            $live = 0
            continue
        }

        $asked = $step.action
        if ($step.PSObject.Properties.Name -contains 'view') { $asked += ':' + $step.view }
        $asked

        if ($step.action -eq 'launch') { $live++ }
        if ($step.action -eq 'close') { $live-- }
    }

    'editors'
    for ($at = 0; $at -lt $live; $at++) { 'close' }
}

function Assert-NoEditorLeft {
    param([string]$Editors, [string]$What)

    $left = @((Get-Content $Editors -Raw -Encoding UTF8 | ConvertFrom-Json).Live)
    if ($left.Count -ne 0) {
        throw "${What}のあと、起こしたエディタが閉じられずに残っている: $($left -join '・')"
    }
}

function Test-AcceptanceRunner {
    <#
        .SYNOPSIS
        受入の実行器が、定義どおりに段をこなし、返った結果を期待と突き合わせて合否を出すことを
        確かめる。期待どおりの応答を与えた通しの実行は、全シナリオを合格で終え、定義に並ぶツールの
        呼び出し・エディタとホストの操作・サーバーの起こし直しを1件残らずこなさなければならない
        ——こなさない実行器はここで落ちる。数えるのはいずれも応答を作った側と操作役の代わりで、
        実行器の自己申告ではない。そのうえで、期待の形ごとにその形だけを違えた実行が不合格に
        なることを見る——その形を突き合わせない実行器はここで落ちる。
    #>
    param([string]$Cases, [string]$Progress, [string]$Operations, [string]$Editors)

    $defined = Get-Content $Cases -Raw | ConvertFrom-Json
    $calls = @($defined.scenarios.steps | Where-Object { $_.kind -eq 'tool' }).Count
    $restarts = @($defined.scenarios.steps | Where-Object { $_.kind -eq 'server' }).Count
    $asked = @(Get-AcceptanceOperations -Defined $defined)

    $ran = Invoke-AcceptanceRunner -Cases $Cases -Broken '' -At 0
    if ($ran.Code -ne 0) {
        throw "期待どおりの応答で通して走らせて合格しない: $($ran.Said)"
    }

    $held = Get-Content $Progress -Raw | ConvertFrom-Json
    if ($held.calls -ne $calls) {
        throw "定義に並ぶ $calls 件の呼び出しのうち $($held.calls) 件しか呼んでいない。"
    }

    # 起こし直す段のぶんだけ、応答を作る相手は起こし直される。最初の1回はその段に依らない。
    if ($held.starts -ne ($restarts + 1)) {
        throw ("サーバーを起こした回数が " + ($restarts + 1) + " ではない: $($held.starts)")
    }

    $done = @(Get-Content $Operations -Encoding UTF8)
    if (($done -join '/') -ne ($asked -join '/')) {
        throw "頼んだ操作が定義と違う。定義: $($asked -join '/') / 実際: $($done -join '/')"
    }

    Assert-NoEditorLeft -Editors $Editors -What '期待どおりの応答で通した実行'

    foreach ($form in (Get-AcceptanceExpectationForms -Defined $defined).GetEnumerator()) {
        $ran = Invoke-AcceptanceRunner -Cases $Cases -Broken $form.Key -At $form.Value
        if ($ran.Code -ne 1) {
            throw ("$($form.Key) の期待を $($form.Value) 件目の呼び出しで違えても不合格に" +
                "ならない: $($ran.Said)")
        }

        Assert-NoEditorLeft -Editors $Editors -What "$($form.Key) の期待を違えた実行"
    }

    # 置き場が作られなければ、その実在を確かめる段が落とすはずである。
    $ran = Invoke-AcceptanceRunner -Cases $Cases -Broken 'file' -At 0
    if ($ran.Code -ne 1) {
        throw "書き込んだはずの置き場が無くても不合格にならない: $($ran.Said)"
    }

    Assert-NoEditorLeft -Editors $Editors -What '置き場を作らせなかった実行'

    # 画像を画像でなく本文の文字列で返させる。期待の形から導けない違え方なので、ここで名指しする
    # ——本文から読んでいる実行器は、文字列で返っても通してしまう。
    $ran = Invoke-AcceptanceRunner -Cases $Cases -Broken 'imageAsText' -At 0
    if ($ran.Code -ne 1) {
        throw "画像が文字列で返っても不合格にならない: $($ran.Said)"
    }

    Assert-NoEditorLeft -Editors $Editors -What '画像を文字列で返させた実行'

    $spoiled = [System.IO.Path]::GetTempFileName()
    try {
        $defined = Get-Content $Cases -Raw -Encoding UTF8 | ConvertFrom-Json
        $defined.scenarios[-1].steps = $null
        $defined | ConvertTo-Json -Depth 100 |
            Set-Content -Path $spoiled -Encoding UTF8 -NoNewline

        $ran = Invoke-AcceptanceRunner -Cases $spoiled -Broken '' -At 0
        if ($ran.Code -ne 3) {
            throw "段の並びが壊れていても実行不能で終わらない: $($ran.Code) $($ran.Said)"
        }

        Assert-NoEditorLeft -Editors $Editors -What '段の並びが壊れた定義での実行'
    } finally {
        Remove-Item $spoiled -ErrorAction SilentlyContinue
    }
}

function Test-PackageContents {
    <#
        .SYNOPSIS
        内容物を確かめる側が、中身を違えたときに落ちることを見る。組み立てたものが通ることだけを
        見ても、確かめる側が何も見ていない場合と区別できない。写しへ違えを入れて確かめる——
        組み立てた本体は配布物なので、こちらで傷つけない。
    #>
    $expected = @('PmxEditorMcp.dll', 'PmxEditorMcp.Bridge.exe', 'INSTALL.md', 'LICENSE.txt',
        'ThirdPartyNotices.txt')
    $version = (Get-Content Directory.Build.props -Raw -Encoding UTF8 |
        Select-String -Pattern '<Version>([^<]+)</Version>').Matches[0].Groups[1].Value
    $staged = Join-Path 'dist' "pmx-editor-mcp-$version"
    $copy = Join-Path ([System.IO.Path]::GetTempPath()) ('contents-' + [guid]::NewGuid().ToString('N'))
    Copy-Item -Path $staged -Destination $copy -Recurse

    try {
        foreach ($spoiled in @(
                @{ Name = '再配布できない物'; Do = {
                    Copy-Item (Join-Path $copy 'LICENSE.txt') (Join-Path $copy 'PEPlugin.dll') } },
                @{ Name = '写しの書き換え'; Do = {
                    Add-Content -Path (Join-Path $copy 'LICENSE.txt') -Value 'x' } },
                @{ Name = '内容物の欠落'; Do = {
                    Remove-Item (Join-Path $copy 'PmxEditorMcp.dll') -Force } })) {
            & $spoiled.Do
            $broke = $false
            try {
                & scripts/package-contents.ps1 -Staged $copy -Version $version `
                    -Expected $expected | Out-Null
            } catch {
                $broke = $true
            }

            if (-not $broke) { throw "$($spoiled.Name)を入れても落ちない。" }
            Remove-Item -Path $copy -Recurse -Force
            Copy-Item -Path $staged -Destination $copy -Recurse
        }

        $broke = $false
        try {
            & scripts/package-contents.ps1 -Staged $copy -Version '9.9.9' `
                -Expected $expected | Out-Null
        } catch {
            $broke = $true
        }

        if (-not $broke) { throw "版が合わなくても落ちない。" }

        # 版を固定値で持つ実装は、Directory.Build.props を変えても追随しない。組み立てる側に
        # その綴りが1つも現れないことで見る——現れないなら、名前も照合もそこを読んで決めている。
        $literal = @(Get-ChildItem scripts/package*.ps1 |
            Select-String -Pattern $version -SimpleMatch)
        if ($literal.Count -ne 0) {
            throw ("組み立てる側が版を綴りで持っている: " +
                (($literal | ForEach-Object { $_.Path + ':' + $_.LineNumber }) -join '・'))
        }
    } finally {
        Remove-Item -Path $copy -Recurse -Force -ErrorAction Ignore
    }
}

$build = 'ビルド'
$derivation = '除外一覧の導出'

$noArtifact = 'なし'
$buildOutput = 'ビルド成果物'
$exclusionList = '除外一覧'

$checks = [ordered]@{}
$checks[$build] = @{
    Needs = $noArtifact
    Body = { dotnet build PmxEditorMcp.sln -warnaserror }
}
$checks['スクリプト構文'] = @{
    Needs = $noArtifact
    Body = {
        $bad = @()
        foreach ($file in Get-ChildItem scripts/*.mjs) {
            $said = node --check $file.FullName 2>&1
            if ($LASTEXITCODE -ne 0) { $bad += ($file.Name + ': ' + (@($said) -join '; ')) }
        }
        $global:LASTEXITCODE = 0
        if ($bad) { throw ($bad -join "`n") }
    }
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
$checks['文書のリンク'] = @{
    Needs = $noArtifact
    Body = {
        lychee --offline --no-progress --include-fragments `
            --exclude-path .scratch --exclude-path docs/.scratch --exclude-path dist '**/*.md'
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
$checks['実行時リフレクション'] = @{
    Needs = $buildOutput
    Body = { & $dump reflection-free $editorDir $hostDll }
}
$checks['整形'] = @{
    Needs = $buildOutput
    Body = { dotnet format PmxEditorMcp.sln --verify-no-changes }
}
$checks['テスト'] = @{
    Needs = $buildOutput
    Body = { dotnet test PmxEditorMcp.sln }
}
$checks['台帳とSDKの照合'] = @{
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
$checks['提供対象の網羅'] = @{
    Needs = $exclusionList
    Body = { & $dump map-coverage $editorDir $ledger $excluded $roles $toolMap }
}
$checks['スキーマ定義の照合'] = @{
    Needs = $buildOutput
    Body = { & $dump tool-schemas $contract $toolMap $toolSchemas }
}
$checks['ツールの説明文の照合'] = @{
    Needs = $buildOutput
    Body = {
        & $dump tool-descriptions $editorDir $ledger $contract $roles $names `
            $assignments $toolMap
    }
}
$checks['サンプル値の照合'] = @{
    Needs = $buildOutput
    Body = { & $dump sample-values $editorDir $contract $sampleValues }
}
$checks['発見可能性の照合'] = @{
    Needs = $buildOutput
    Body = {
        & $dump discovery $editorDir $ledger $contract $roles $names `
            $assignments $toolMap $discoveryTasks
    }
}
$checks['スキーマ対応の照合'] = @{
    Needs = $buildOutput
    Body = {
        & $dump schema-correspondence $editorDir $ledger $roles $assignments `
            $toolMap $toolSchemas
    }
}
$checks['行の検査の網羅'] = @{
    Needs = $buildOutput
    Body = {
        & $dump row-coverage $editorDir $ledger $contract $roles $names `
            $assignments $toolMap $toolSchemas $sampleValues
    }
}
$checks['規則適合検査'] = @{
    Needs = $buildOutput
    Body = {
        & $dump tool-mapping $editorDir $ledger $contract $roles $assignments `
            $toolMap $toolSchemas
    }
}
$checks['受入シナリオの照合'] = @{
    Needs = $buildOutput
    Body = {
        & $dump acceptance-cases $editorDir $ledger $contract $roles $names `
            $assignments $toolMap $toolSchemas $acceptance $requirements
    }
}
$checks['ブリッジの単独起動'] = @{
    Needs = $noArtifact
    Body = { pwsh -NoProfile -File scripts/bridge-standalone.ps1 }
}
$checks['配布パッケージの生成'] = @{
    Needs = $noArtifact
    Body = {
        # 1コマンドで組み立てられること。中身を違えたときに落ちることは、確かめる側を
        # 直に呼んで見る——落ちない検査は、通っても何も言っていない。
        pwsh -NoProfile -File scripts/package.ps1
        if ($LASTEXITCODE -ne 0) { throw "配布パッケージを組み立てられない。" }

        Test-PackageContents
    }
}
$checks['受入の実行器の照合'] = @{
    Needs = $noArtifact
    Body = {
        # 実行器が書くのはUTF-8なので、端末の設定のまま読むと合否の手がかりが崩れる。
        $spoken = [Console]::OutputEncoding
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
        try {
            $temp = [System.IO.Path]::GetTempPath()
            Test-AcceptanceRunner -Cases $acceptance `
                -Progress (Join-Path $temp $StubProgressStateName) `
                -Operations (Join-Path $temp $StubOperationLogName) `
                -Editors (Join-Path $temp $StubLaunchStateName)
        } finally {
            [Console]::OutputEncoding = $spoken
        }
    }
}

# 検査を、それが読む入力ごとに分ける。何を変えたかで、走らせる群が決まる。数えるのは引数に現れる
# ファイルだけではない。Body が呼ぶスクリプトも、その呼び先が中で読むファイルも、その実行ファイルを
# 作る場所も入力である——そこまで辿らずに群を決めると、変えた当のコードを動かす検査が1件も走らない
# まま合格が出る。読む入力が複数の種類にまたがる検査は、そのすべての群に入れる。
$checkGroups = [ordered]@{
    'ドキュメント' = @('文書のリンク', '受入シナリオの照合')
    '定義' = @($derivation, '台帳とSDKの照合', '日本語名の照合', '型役割の照合',
        '共通契約割当の照合', '値の表現の照合', '危険操作の照合', '能力対応表の照合',
        '提供対象の網羅', 'スキーマ定義の照合', 'ツールの説明文の照合', 'サンプル値の照合',
        '発見可能性の照合', 'スキーマ対応の照合', '行の検査の網羅', '規則適合検査',
        '受入シナリオの照合', '受入の実行器の照合',
        '配布パッケージの生成',
        'テスト', 'ブリッジの単独起動', '文書のリンク')
    # 上の2群のどちらにも入らない検査をここへ並べる。全件を走らせるときにしか出番が無いという
    # 申告で、`@($checks.Keys)` のような一括の指定にはしない——一括にすると、新しい検査を上の2群へ
    # 入れ忘れても全件の側が黙って拾い、入れ忘れを落とす検査が素通りになる。
    '全件のみ' = @($build, 'スクリプト構文', 'スクリプト構文(PowerShell)', '実行時リフレクション',
        '整形')
}

function Invoke-Checks {
    <#
        .SYNOPSIS
        指す群の検査を走らせ、終わらせる終了コードを返す。出来上がりが要る検査を選んだときは、
        それを作る検査も一緒に走らせる——選んだ群だけでは入力が揃わず、走らせられないままになる。
    #>
    param([string[]]$Groups)

    $unknown = @($Groups | Where-Object { -not $checkGroups.Contains($_) })
    if ($unknown) { throw ('知らない群: ' + ($unknown -join '・')) }

    $wanted = @()
    foreach ($group in $Groups) { $wanted += $checkGroups[$group] }

    if (@($wanted | Where-Object { $checks[$_].Needs -eq $exclusionList })) {
        $wanted += $derivation
    }
    if (@($wanted | Where-Object { $checks[$_].Needs -ne $noArtifact })) {
        $wanted += $build
    }

    $failed = @()
    $skipped = @()
    $ran = 0
    $produced = @($noArtifact)

    try {
        foreach ($name in $checks.Keys) {
            if ($wanted -notcontains $name) { continue }

            if ($produced -notcontains $checks[$name].Needs) {
                $skipped += $name
                continue
            }

            $ran++
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

    Write-CheckSummary -Failed $failed -Skipped $skipped -Scope ($Groups -join '・') `
        -Ran $ran -Listed $checks.Count
}

Assert-ListedChecks -Path $procedure -Section '## 常設の検査' -Names @($checks.Keys)
Assert-GroupedChecks -Grouped $checkGroups -Names @($checks.Keys)
