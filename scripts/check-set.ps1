# 常設の検査の中身と、群の割り当て。[check-manifest.ps1](check-manifest.ps1) が一覧として出し、
# [run-check.ps1](run-check.ps1) が1件ずつ走らせる。

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 外部コマンドの非0終了は終了エラーにしない。ここでは出力と終了コードをそのまま見て合否にする。
$PSNativeCommandUseErrorActionPreference = $false

. (Join-Path $PSScriptRoot 'editor-dir.ps1')
. (Join-Path $PSScriptRoot 'stub-shared.ps1')
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
$uncoveredTools = "$authored/uncovered-tools.json"

# 受入の実行器の照合が使う題材。実物の定義で走らせると、突き合わせに要らない段まで通すことに
# なる。突き合わせが見るのは期待の形と、操作・置き場・起こし直しの各段が頼む行いの種類で、
# そのどれも1段ずつあれば足りる。題材がそれらを漏れなく持つことは受入シナリオの照合が見る。
$acceptanceStub = 'scripts/acceptance-stub-cases.json'
$acceptanceStubForms = 'scripts/acceptance-stub-forms.json'
$e2eStub = 'scripts/e2e-stub-cases.json'
$e2eStubForms = 'scripts/e2e-stub-forms.json'
$requirements = 'docs/specs/requirements.md'

<#
    形を1つも壊さない通しの実行を指す綴り。どの照合もこの綴りで通しを走らせる。
#>
$wholeForm = '通し'


# 除外一覧の導出が書き、それを読む検査が読む置き場。束をまたぐので、道は実行のあいだ変わらない。
$work = Get-CheckWorkDirectory
$baseline = Join-Path $work 'excluded-baseline'
$excluded = Join-Path $work 'excluded-signatures'

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

function Get-FreeStubPipe {
    <#
        .SYNOPSIS
        いま誰も待ち受けていない名前の番号を選ぶ。確認クライアントは番号でしか相手を指せないので、
        待ち受けている実機のエディタと同じ番号を選ぶと、開けなかった題材の代わりに実物のホストへ
        繋いでしまう——そのとき結末は題材と関わりのない理由で決まる。
    #>
    foreach ($try in 1..20) {
        $pipe = Get-Random -Minimum 900000 -Maximum 999999
        $opened = @([System.IO.Directory]::GetFileSystemEntries("\\.\pipe\") |
            ForEach-Object { Split-Path -Leaf $_ })
        if ($opened -notcontains ("pmx-editor-mcp-" + $pipe)) { return $pipe }
    }

    throw "空いている待受の名前を選べない。"
}

function Invoke-CheckClient {
    <#
        .SYNOPSIS
        応答を作る待受の代わりを立てて確認クライアントを走らせ、終了コードと書き出したものを返す。
        Broken を与えると、その項目だけを違えた応答を返させる。
    #>
    param([string]$Broken)

    $pipe = Get-FreeStubPipe
    $given = @('scripts/e2e-check-stub-host.mjs', '--pipe', "$pipe")
    if ($Broken) { $given += @('--broken', $Broken) }

    $told = Join-Path ([System.IO.Path]::GetTempPath()) ("pmx-editor-mcp-stub-" + $pipe + ".log")
    $fell = [System.IO.Path]::GetTempFileName()
    $stub = Start-Process -FilePath 'node' -PassThru -WindowStyle Hidden `
        -RedirectStandardError $told -ArgumentList $given
    try {
        Wait-StubPipe -Name ("pmx-editor-mcp-" + $pipe) -Stub $stub -Said $told
        $env:PMX_EDITOR_MCP_FELL_PATH = $fell
        $said = node scripts/e2e-check.mjs $pipe 2>&1
        $code = $LASTEXITCODE
        $global:LASTEXITCODE = 0
        $numbers = @(Get-FellNumbers -Path $fell)
    } finally {
        Remove-Item Env:PMX_EDITOR_MCP_FELL_PATH -ErrorAction Ignore
        Stop-Process -Id $stub.Id -Force -ErrorAction SilentlyContinue
        Remove-Item $told, $fell -Force -ErrorAction SilentlyContinue
    }

    [pscustomobject]@{ Code = $code; Said = (@($said) -join "`n"); Fell = $numbers }
}

function Get-CheckClientForms {
    <#
        .SYNOPSIS
        確認クライアントが見る応答の項目と、それを違えたときに咎めるはずの項目の番号。
        前半は応答の中身を見る層、後半はその手前で行そのものを見る層。
    #>
    [ordered]@{
        'handshake.error' = 0
        'handshake.result' = 1
        'handshake.protocol' = 2
        'handshake.hostVersion' = 3
        'handshake.budgetChars' = 4
        'handshake.session' = 5
        'ping.error' = 6
        'ping.value' = 7
        'wire.oversize' = 8
        'wire.utf8' = 9
        'wire.bom' = 10
        'wire.cr' = 11
        'wire.json' = 12
        'wire.object' = 13
        'wire.jsonrpc' = 14
        'wire.id.missing' = 15
        'wire.id.other' = 16
        'wire.both' = 17
        'wire.unidentified' = 18
        'wire.error.shape' = 19
        'wire.error.code' = 20
        'wire.error.message' = 21
    }
}

function Test-CheckClient {
    <#
        .SYNOPSIS
        確認クライアントが、返った応答を契約と突き合わせて合否を出すことを確かめる。通しでは契約
        どおりの応答で合格することを、形を指したときはその項目だけを違えた実行が当の項目を咎めて
        落ちることを見る——その項目を見ていないクライアントはここで落ちる。
    #>
    param([string]$Form)

    if ($Form -eq $wholeForm) {
        $ran = Invoke-CheckClient -Broken ''
        if ($ran.Code -ne 0) { throw "契約どおりの応答で走らせて合格しない: $($ran.Said)" }

        return
    }

    $forms = Get-CheckClientForms
    if (-not $forms.Contains($Form)) { throw "知らない形: $Form" }

    $ran = Invoke-CheckClient -Broken $Form
    if ($ran.Code -eq 0) { throw "$Form を違えても不合格にならない: $($ran.Said)" }

    if ($ran.Fell -notcontains $forms[$Form]) {
        throw "$Form を違えたのに $($forms[$Form]) 番の項目を咎めていない: $($ran.Said)"
    }
}

function Invoke-LiveHostRunner {
    <#
        .SYNOPSIS
        エディタと待受と確認クライアントの代わりを立てて実機動作確認を走らせ、終了コードと
        書き出したものを返す。Broken を与えると、その観測だけを違えさせる。
    #>
    param([string]$Broken)

    $spoken = [System.Environment]::GetEnvironmentVariable($LiveHostStubBrokenName)
    [System.Environment]::SetEnvironmentVariable($LiveHostStubBrokenName, $Broken)
    $fell = [System.IO.Path]::GetTempFileName()
    $ran = [System.IO.Path]::GetTempFileName()
    try {
        $env:PMX_EDITOR_MCP_FELL_PATH = $fell
        $env:PMX_EDITOR_MCP_RAN_PATH = $ran
        $said = pwsh -NoProfile -File scripts/live-host.ps1 `
            -Control scripts/live-host-stub-control.ps1 `
            -Client scripts/live-host-stub-client.mjs `
            -Deploy scripts/live-host-stub-deploy.ps1 2>&1
        $code = $LASTEXITCODE
        $global:LASTEXITCODE = 0
        $numbers = @(Get-FellNumbers -Path $fell)
        $walked = @(Get-FellNumbers -Path $ran)
    } finally {
        Remove-Item Env:PMX_EDITOR_MCP_FELL_PATH, Env:PMX_EDITOR_MCP_RAN_PATH -ErrorAction Ignore
        Remove-Item $fell, $ran -Force -ErrorAction SilentlyContinue
        [System.Environment]::SetEnvironmentVariable($LiveHostStubBrokenName, $spoken)
    }

    [pscustomobject]@{ Code = $code; Said = (@($said) -join "`n"); Fell = $numbers; Ran = $walked }
}

function Get-LiveHostRunnerForms {
    <#
        .SYNOPSIS
        実機動作確認が突き合わせる観測の種類と、それを違えたときに落ちるはずの件の番号。
    #>
    [ordered]@{
        'client.code' = 1
        'client.says' = 4
        'acl' = 2
        'log.started' = 0
        'log.renewal' = 3
    }
}

function Test-LiveHostRunner {
    <#
        .SYNOPSIS
        実機動作確認が、観測したものを期待と突き合わせて合否を出すことを確かめる。通しでは期待
        どおりの観測で全件が合格し、違える形が覆う件と走った件が揃うことを、形を指したときは
        その観測だけを違えた実行で当の件が落ちることを見る。
    #>
    param([string]$Form)

    $forms = Get-LiveHostRunnerForms
    if ($Form -eq $wholeForm) {
        $ran = Invoke-LiveHostRunner -Broken ''
        if ($ran.Code -ne 0) { throw "期待どおりの観測で走らせて合格しない: $($ran.Said)" }

        $uncovered = @(Compare-Object -ReferenceObject @($forms.Values) `
            -DifferenceObject @($ran.Ran)).Count
        if ($uncovered -ne 0) {
            throw "違える形が覆う件と、走った件が揃っていない: $($ran.Said)"
        }

        return
    }

    if (-not $forms.Contains($Form)) { throw "知らない形: $Form" }

    $ran = Invoke-LiveHostRunner -Broken $Form
    if ($ran.Code -eq 0) { throw "$Form を違えても不合格にならない: $($ran.Said)" }

    if ($ran.Fell -notcontains $forms[$Form]) {
        throw "$Form を違えたのに $($forms[$Form]) 番の件が落ちていない: $($ran.Said)"
    }
}

function Invoke-LiveClientRunner {
    <#
        .SYNOPSIS
        呼び出しの記録を作るクライアントと操作役の代わりを立てて参照クライアントの実機動作確認を
        走らせ、終了コードと書き出したものを返す。Broken を与えると、その項目だけを違えさせる。
    #>
    param([string]$Broken)

    $client = 'node scripts/live-client-stub.mjs'
    if ($Broken) { $client += ' --broken ' + $Broken }

    $fell = [System.IO.Path]::GetTempFileName()
    $ran = [System.IO.Path]::GetTempFileName()
    try {
        $env:PMX_EDITOR_MCP_FELL_PATH = $fell
        $env:PMX_EDITOR_MCP_RAN_PATH = $ran
        $said = node scripts/live-client.mjs `
            --control scripts/live-stub-control.ps1 `
            --setup scripts/live-client-stub-setup.ps1 `
            --client $client 2>&1
        $code = $LASTEXITCODE
        $global:LASTEXITCODE = 0
        $numbers = @(Get-FellNumbers -Path $fell)
        $walked = @(Get-FellNumbers -Path $ran)
    } finally {
        Remove-Item Env:PMX_EDITOR_MCP_FELL_PATH, Env:PMX_EDITOR_MCP_RAN_PATH -ErrorAction Ignore
        Remove-Item $fell, $ran -Force -ErrorAction SilentlyContinue
    }

    [pscustomobject]@{ Code = $code; Said = (@($said) -join "`n"); Fell = $numbers; Ran = $walked }
}

function Get-LiveClientRunnerForms {
    <#
        .SYNOPSIS
        参照クライアントの実機動作確認が突き合わせる記録の項目と、それを違えたときに落ちるはずの
        観点の番号。
    #>
    [ordered]@{
        'call' = 0
        'arguments' = 0
        'result' = 1
        'refused' = 2
        'image.missing' = 3
        'image.extra' = 4
    }
}

function Test-LiveClientRunner {
    <#
        .SYNOPSIS
        参照クライアントの実機動作確認が、呼び出しの記録を期待と突き合わせて合否を出すことを
        確かめる。通しでは期待どおりの記録で合格し、定義の件数だけ歩くことを、形を指したときは
        その項目だけを違えた実行が当の咎めで落ちることを見る。
    #>
    param([string]$Form)

    if ($Form -eq $wholeForm) {
        $ran = Invoke-LiveClientRunner -Broken ''
        if ($ran.Code -ne 0) { throw "期待どおりの記録で走らせて合格しない: $($ran.Said)" }

        $counted = [int](node -e "import('./scripts/live-client-cases.mjs').then(m => console.log(m.CASES.length))")
        if ($ran.Ran.Count -ne $counted) {
            throw "定義の $counted 件のうち $($ran.Ran.Count) 件しか歩いていない: $($ran.Said)"
        }

        return
    }

    $forms = Get-LiveClientRunnerForms
    if (-not $forms.Contains($Form)) { throw "知らない形: $Form" }

    $ran = Invoke-LiveClientRunner -Broken $Form
    if ($ran.Code -eq 0) { throw "$Form を違えても不合格にならない: $($ran.Said)" }

    if ($ran.Fell -notcontains $forms[$Form]) {
        throw "$Form を違えたのに $($forms[$Form]) 番の観点が落ちていない: $($ran.Said)"
    }
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
        foreach ($form in (Get-ExpectationForms -Expect $step.expect)) {
            if (-not $forms.Contains($form)) { $forms[$form] = $at }
        }
    }

    $forms
}

function Get-FormsBeyondCases {
    <#
        .SYNOPSIS
        生成の入力が名指しする、期待の形からは導けない形と、その扱い。
    #>
    param([string]$Path)

    $named = [ordered]@{}
    $held = Get-Content $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($form in $held.PSObject.Properties) { $named[$form.Name] = $form.Value }

    $named
}

function Get-ExpectationForms {
    <#
        .SYNOPSIS
        その段の期待が立てている形を並べる。1つの期待が2つの形を立てることがあるので、期待の
        名前ではなく中身で決める——名前ごとに1つへ決めると、2つ立てた段の片方が導出から落ち、
        その形を違えた実行が一度も走らないまま合格が出る。
    #>
    param($Expect)

    foreach ($name in $Expect.PSObject.Properties.Name) {
        switch ($name) {
            'notice' {
                if ($Expect.notice.PSObject.Properties.Name -contains 'editor') { 'notice' }
                else { 'notice.changed' }
            }
            'image' {
                # 画像であることだけを求める段は、大きさを違えても落ちない。その段を受け持つのは
                # 画像を文字列で返させる違え方で、形からは導けないので名指しで違えさせている。
                if ($null -ne $Expect.image.PSObject.Properties['capturedAs']) { 'image' }
                if ($null -ne $Expect.image.PSObject.Properties['differsFrom']) {
                    'image.differsFrom'
                }
            }
            'values' {
                if (@($Expect.values | Where-Object {
                        $null -ne $_.PSObject.Properties['equals'] })) { 'values' }
                if (@($Expect.values | Where-Object {
                        $null -ne $_.PSObject.Properties['absent'] })) { 'values.absent' }
                if (@($Expect.values | Where-Object {
                        $null -ne $_.PSObject.Properties['present'] })) { 'values.present' }
            }
            default { $name }
        }
    }
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

# 待受の代わりがパイプを開くまでの上限の秒数。この上限に達した待ちは失敗とする。
# 落ちた相手はその場で分かるので、この上限は待たずに抜ける。
$StubPipeLimitSeconds = 10

function Wait-StubPipe {
    <#
        .SYNOPSIS
        待受の代わりが名前付きパイプを開くまで待つ。開く前に投げると、実行器は接続できずに
        落ちる——待たずに始めると、この検査の合否が起動の速さで揺れる。
    #>
    param([string]$Name, $Stub, [string]$Said)

    $until = (Get-Date).AddSeconds($StubPipeLimitSeconds)
    while ((Get-Date) -lt $until) {
        # 名前付きパイプは Test-Path では見つからないので、待受の一覧から名前で探す。
        $opened = @([System.IO.Directory]::GetFileSystemEntries("\\.\pipe\") |
            ForEach-Object { Split-Path -Leaf $_ })
        if ($opened -contains $Name) { return }
        if ($Stub.HasExited) {
            # 落ちた理由は待受の代わりが述べる。隠れた窓の中へは届かないので、受け取った先から
            # 読んで載せる——理由の無い「開かなかった」だけでは、手で起こし直すところから
            # やり直すことになる。
            $told = if (Test-Path $Said) { (Get-Content $Said -Raw -Encoding UTF8).Trim() } else { '' }
            throw ("待受の代わりが開く前に終わった: $Name " + $told)
        }

        Start-Sleep -Milliseconds 50
    }

    throw "待受の代わりが $StubPipeLimitSeconds 秒以内にパイプを開かない: $Name"
}

function Invoke-E2eRunner {
    <#
        .SYNOPSIS
        応答を作る相手と操作役の代わりを立てて自動E2E検査の実行器を走らせ、終了コードと
        書き出したものを返す。Broken を与えると、At が指す検査でその形の期待だけを違えさせる。
    #>
    param([string]$Cases, [string]$Broken, [int]$At)

    # 代わりの相手は実行器が前置を通して起こす。名前の突き合わせが見るスキーマ正本も、その相手が
    # 公開する名前と同じ元から作る——どちらも検査の定義が名乗るツールの名前である。
    $tag = [guid]::NewGuid().ToString('N')
    $defined = Get-Content $Cases -Raw -Encoding UTF8 | ConvertFrom-Json
    $named = @($defined.cases | ForEach-Object { $_.tool } | Sort-Object -Unique)
    $schemas = Join-Path ([System.IO.Path]::GetTempPath()) ("pmx-editor-mcp-schemas-$tag.json")
    [pscustomobject]@{
        tools = @($named | ForEach-Object { [pscustomobject]@{ tool = $_ } })
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $schemas -Encoding UTF8

    $fell = Join-Path ([System.IO.Path]::GetTempPath()) ("pmx-editor-mcp-fell-$tag.txt")
    $ran = Join-Path ([System.IO.Path]::GetTempPath()) ("pmx-editor-mcp-ran-$tag.txt")
    try {
        $env:PMX_EDITOR_MCP_FELL_PATH = $fell
        $env:PMX_EDITOR_MCP_RAN_PATH = $ran
        $said = node scripts/e2e-tools.mjs 0 $Cases `
            --control scripts/e2e-stub-control.ps1 `
            --compare scripts/e2e-stub-compare.ps1 `
            --schemas $schemas `
            --setup scripts/e2e-setup-stub.ps1 `
            --setup-arg -Cases --setup-arg $Cases `
            --setup-arg -Broken --setup-arg $Broken `
            --setup-arg -At --setup-arg $At
        $code = $LASTEXITCODE
        $global:LASTEXITCODE = 0
        $numbers = @(Get-FellNumbers -Path $fell)
        $walked = @(Get-FellNumbers -Path $ran)
    } finally {
        Remove-Item Env:PMX_EDITOR_MCP_FELL_PATH, Env:PMX_EDITOR_MCP_RAN_PATH -ErrorAction Ignore
        Remove-Item $schemas, $fell, $ran -Force -ErrorAction SilentlyContinue
    }

    [pscustomobject]@{ Code = $code; Said = ($said -join "`n"); Fell = $numbers; Ran = $walked }
}

function Get-E2eExpectationForms {
    <#
        .SYNOPSIS
        結末の形ごとに、それが初めて現れる検査の番を返す。形の名前は定義から拾うので、形を足しても
        拾い直しは要らない。ビューの画像は、名乗るビューごとに別の形と見る。
        書き込んだ置き場を確かめる段は結末の名前を持たないので、その名前を足す。
    #>
    param($Defined)

    $forms = [ordered]@{}
    $at = -1
    foreach ($one in $Defined.cases) {
        $at++
        $form = $one.expect
        if ($form -eq 'viewImage') { $form = 'viewImage.' + $one.view }
        if (-not $forms.Contains($form)) { $forms[$form] = $at }

        # 呼び先まで届く段は、断られたときだけでなく確認の表示で止まったときも落ちる。止まった
        # 応答を通す実行器は、表示が出たことを宣言した振る舞いの代わりに数えてしまう。
        if ($form -eq 'called' -and -not $forms.Contains('called.prompt')) {
            $forms['called.prompt'] = $at
        }

        $writes = $one.PSObject.Properties.Name -contains 'writes'
        if ($writes -and -not $forms.Contains('file')) { $forms['file'] = $at }
    }

    $forms
}

function Test-E2eRunner {
    <#
        .SYNOPSIS
        自動E2E検査の実行器が、返った応答を期待と突き合わせて合否を出すことを確かめる。期待
        どおりの応答を与えた通しの実行は全件を合格で終え、そのうえで、期待の形ごとにその形だけを
        違えた実行が不合格になる——その形を突き合わせない実行器はここで落ちる。
    #>
    param([string]$Cases, [string]$Form)

    $defined = Get-Content $Cases -Raw -Encoding UTF8 | ConvertFrom-Json

    if ($Form -eq $wholeForm) {
        $ran = Invoke-E2eRunner -Cases $Cases -Broken '' -At -1
        if ($ran.Code -ne 0) {
            throw "期待どおりの応答で通して走らせて合格しない: $($ran.Said)"
        }

        $counted = @($defined.cases).Count
        if ($ran.Ran.Count -ne $counted) {
            throw "定義の $counted 件のうち $($ran.Ran.Count) 件しか走らせていない: $($ran.Said)"
        }

        return
    }

    # 検査1件ごとの結末ではなく、走らせる前と後に見る事柄。名前の集合がずれていても、中継を
    # 作れなかった行や無効にした行が残っていても、呼んだ検査はすべて通りうる——通したまま終える
    # 実行器はここで落ちる。
    $beyond = Get-FormsBeyondCases -Path $e2eStubForms
    if ($beyond.Contains($Form) -and $beyond[$Form] -eq 'wholeRun') {
        $ran = Invoke-E2eRunner -Cases $Cases -Broken $Form -At -1
        if ($ran.Code -ne 1) { throw "$Form を違えても不合格にならない: $($ran.Said)" }

        return
    }

    # ブリッジ自身が返す誤りは接続先を名乗らない。名乗りを1行目と決め打つ実行器は、この誤りの
    # 中身を丸ごと落として、落ちた理由の残らない不合格を並べる。
    if ($beyond.Contains($Form) -and $beyond[$Form] -eq 'bridgeError') {
        $ran = Invoke-E2eRunner -Cases $Cases -Broken $Form -At 0
        if ($ran.Code -ne 1) {
            throw "ブリッジ自身の誤りを返しても不合格にならない: $($ran.Said)"
        }

        if ($ran.Said -notmatch 'BRIDGE_TIMEOUT') {
            throw "ブリッジ自身の誤りの中身が結末に残っていない: $($ran.Said)"
        }

        return
    }

    $forms = Get-E2eExpectationForms -Defined $defined
    if (-not $forms.Contains($Form)) { throw "知らない形: $Form" }

    $at = $forms[$Form]
    $broken = $Form
    if ($broken -like 'viewImage.*') { $broken = 'viewImage' }
    if ($broken -eq 'called.prompt') { $broken = 'prompt' }
    $ran = Invoke-E2eRunner -Cases $Cases -Broken $broken -At $at
    if ($ran.Code -ne 1) { throw "$Form の期待を違えても不合格にならない: $($ran.Said)" }

    if ($ran.Fell -notcontains $at) {
        throw "$Form の期待を違えたのに $at 番の検査が落ちていない: $($ran.Said)"
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
    param([string]$Cases, [string]$Progress, [string]$Operations, [string]$Editors,
        [string]$Form)

    $defined = Get-Content $Cases -Raw | ConvertFrom-Json

    if ($Form -eq $wholeForm) {
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

        return
    }

    # 置き場が作られなければ、その実在を確かめる段が落とすはずである。画像を本文の文字列で返す
    # 違え方は期待の形から導けないので、どちらもここで名指しする。
    $beyond = Get-FormsBeyondCases -Path $acceptanceStubForms
    if ($beyond.Contains($Form) -and $beyond[$Form] -eq 'broken') {
        $ran = Invoke-AcceptanceRunner -Cases $Cases -Broken $Form -At 0
        if ($ran.Code -ne 1) { throw "$Form を違えても不合格にならない: $($ran.Said)" }

        Assert-NoEditorLeft -Editors $Editors -What "$Form を違えた実行"

        return
    }

    if ($beyond.Contains($Form) -and $beyond[$Form] -eq 'definition') {
        $spoiled = [System.IO.Path]::GetTempFileName()
        try {
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

        return
    }

    $forms = Get-AcceptanceExpectationForms -Defined $defined
    if (-not $forms.Contains($Form)) { throw "知らない形: $Form" }

    $at = $forms[$Form]
    $ran = Invoke-AcceptanceRunner -Cases $Cases -Broken $Form -At $at
    if ($ran.Code -ne 1) {
        throw "$Form の期待を $at 件目の呼び出しで違えても不合格にならない: $($ran.Said)"
    }

    Assert-NoEditorLeft -Editors $Editors -What "$Form の期待を違えた実行"
}

function Assert-DerivedForms {
    <#
        .SYNOPSIS
        導出元から立つはずの形と、検査になった形が同じ集合であることを見る。片側だけに在る名前が
        1つでもあれば落ちる——導出が規則を落としても、規則を足して述べ忘れても、ここで残る。
    #>
    param([string]$What, [string[]]$Named, [string[]]$Forms)

    $apart = @(Compare-Object -ReferenceObject @($Named | Select-Object -Unique) `
        -DifferenceObject @($Forms | Select-Object -Unique))
    if ($apart.Count -ne 0) {
        throw ("$What の立つはずの形と検査の形が食い違う: " +
            (($apart | ForEach-Object { $_.SideIndicator + $_.InputObject }) -join ' / '))
    }
}

function Get-StandingForms {
    <#
        .SYNOPSIS
        題材の中身から、立つはずの形を並べる。形を導く側とは別にここへ述べる——導出から出せば
        両側がいつも同じになり、突き合わせが何も言わなくなる。
    #>
    param($Defined, [string]$Beyond, [switch]$Acceptance)

    $wholeForm
    (Get-FormsBeyondCases -Path $Beyond).Keys

    if (-not $Acceptance) {
        foreach ($one in $Defined.cases) {
            if ($one.expect -eq 'viewImage') { 'viewImage.' + $one.view } else { $one.expect }
            if ($one.expect -eq 'called') { 'called.prompt' }
            if ($one.PSObject.Properties.Name -contains 'writes') { 'file' }
        }

        return
    }

    foreach ($step in ($Defined.scenarios.steps | Where-Object { $_.kind -eq 'tool' })) {
        foreach ($named in $step.expect.PSObject.Properties.Name) {
            switch ($named) {
                'values' {
                    foreach ($held in @('equals', 'absent', 'present')) {
                        if (@($step.expect.values |
                            Where-Object { $null -ne $_.PSObject.Properties[$held] })) {
                            if ($held -eq 'equals') { 'values' } else { 'values.' + $held }
                        }
                    }
                }
                'image' {
                    if ($null -ne $step.expect.image.PSObject.Properties['capturedAs']) { 'image' }
                    if ($null -ne $step.expect.image.PSObject.Properties['differsFrom']) {
                        'image.differsFrom'
                    }
                }
                'notice' {
                    if ($step.expect.notice.PSObject.Properties.Name -contains 'editor') { 'notice' }
                    else { 'notice.changed' }
                }
                default { $named }
            }
        }
    }
}

function Test-FormDerivation {
    <#
        .SYNOPSIS
        形を導いている3つの導出元それぞれについて、題材の中身から立つはずの形と、検査になった形が
        同じ集合であることを見る。突き合わせる相手は検査の一覧そのもので、導出を組み直したもので
        はない。
    #>
    param($Checks)

    $defined = Get-Content $e2eStub -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-DerivedForms -What 'E2Eのケース定義' -Forms @(& $Checks['E2Eの実行器の照合'].Forms) `
        -Named @(Get-StandingForms -Defined $defined -Beyond $e2eStubForms)

    $defined = Get-Content $acceptanceStub -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-DerivedForms -What '受入のケース定義' -Forms @(& $Checks['受入の実行器の照合'].Forms) `
        -Named @(Get-StandingForms -Defined $defined -Beyond $acceptanceStubForms -Acceptance)

    $tables = Get-PackageTables
    Assert-DerivedForms -What '配布パッケージの表' -Forms @(& $Checks['配布パッケージの生成'].Forms) `
        -Named (@($wholeForm, '版の食い違い') +
            @($tables.forbidden | ForEach-Object { '再配布できない物.' + $_ }) +
            @($tables.copies | ForEach-Object { '写しの書き換え.' + $_ }) +
            @($tables.contents | ForEach-Object { '内容物の欠落.' + $_ }))
}

$packageTables = $null

function Get-PackageTables {
    <#
        .SYNOPSIS
        内容物を確かめる側が突き合わせる表。違え方の形はここから導く。正本は確かめる側が持つ。
        1つの実行で何度も引くので、最初の1回だけ相手を起こす。
    #>
    if ($null -eq $script:packageTables) {
        $script:packageTables =
            pwsh -NoProfile -File scripts/package-contents.ps1 -List | ConvertFrom-Json
    }

    $script:packageTables
}

function Get-PackageSpoils {
    <#
        .SYNOPSIS
        内容物へ入れる違え方と、その入れ方。欠落は内容物1つにつき1つ作る。
    #>
    param([string]$Copy)

    $tables = Get-PackageTables
    $spoils = [ordered]@{
        '版の食い違い' = @{ Spoil = { }; Code = 'PACKAGE_VERSION' }
    }

    foreach ($named in $tables.forbidden) {
        $spoils['再配布できない物.' + $named] = @{
            Spoil = { Copy-Item (Join-Path $Copy 'LICENSE.txt') (Join-Path $Copy $named) }.GetNewClosure()
            Code = 'PACKAGE_FORBIDDEN'
        }
    }

    foreach ($named in $tables.copies) {
        $spoils['写しの書き換え.' + $named] = @{
            Spoil = { Add-Content -Path (Join-Path $Copy $named) -Value 'x' }.GetNewClosure()
            Code = 'PACKAGE_COPY'
        }
    }

    foreach ($named in $tables.contents) {
        $spoils['内容物の欠落.' + $named] = @{
            Spoil = { Remove-Item (Join-Path $Copy $named) -Force }.GetNewClosure()
            Code = 'PACKAGE_CONTENTS'
        }
    }

    $spoils
}

function Test-PackageContents {
    <#
        .SYNOPSIS
        内容物を確かめる側が、中身を違えたときに落ちることを見る。通しでは組み立てたものが通り、
        組み立てる側が版を綴りで持っていないことを見る。形を指したときは、その違えを写しへ入れた
        実行が落ちることを見る——組み立てた本体は配布物なので、こちらで傷つけない。
    #>
    param([string]$Form)

    $version = (Get-Content Directory.Build.props -Raw -Encoding UTF8 |
        Select-String -Pattern '<Version>([^<]+)</Version>').Matches[0].Groups[1].Value
    $staged = Join-Path 'dist' "pmx-editor-mcp-$version"

    if ($Form -eq $wholeForm) {
        # 1コマンドで組み立てられること。組み立ては内容物の照合を内で呼ぶ。
        pwsh -NoProfile -File scripts/package.ps1
        if ($LASTEXITCODE -ne 0) { throw "配布パッケージを組み立てられない。" }

        # 版を固定値で持つ実装は、Directory.Build.props を変えても追随しない。組み立てる側に
        # その綴りが1つも現れないことで見る——現れないなら、名前も照合もそこを読んで決めている。
        $literal = @(Get-ChildItem scripts/package*.ps1 |
            Select-String -Pattern $version -SimpleMatch)
        if ($literal.Count -ne 0) {
            throw ("組み立てる側が版を綴りで持っている: " +
                (($literal | ForEach-Object { $_.Path + ':' + $_.LineNumber }) -join '・'))
        }

        return
    }

    $copy = Join-Path ([System.IO.Path]::GetTempPath()) ('contents-' + [guid]::NewGuid().ToString('N'))
    Copy-Item -Path $staged -Destination $copy -Recurse
    try {
        $spoils = Get-PackageSpoils -Copy $copy
        if (-not $spoils.Contains($Form)) { throw "知らない形: $Form" }

        & $spoils[$Form].Spoil
        $asked = if ($Form -eq '版の食い違い') { '9.9.9' } else { $version }

        # 落ちたことだけでは足りない。違えた当の判定が落としたことを、その判定が名乗る綴りで見る
        # ——別の判定が先に落とすなら、名前の指す判定は一度も通っていない。
        $said = ''
        try {
            & scripts/package-contents.ps1 -Staged $copy -Version $asked | Out-Null
        } catch {
            $said = [string]$_
        }

        if (-not $said) { throw "$Form を入れても落ちない。" }
        if (-not $said.StartsWith($spoils[$Form].Code)) {
            throw "$Form を入れて落ちたのは $($spoils[$Form].Code) ではない: $said"
        }
    } finally {
        Remove-Item -Path $copy -Recurse -Force -ErrorAction Ignore
    }
}

$build = 'ビルド'
$derivation = '除外一覧の導出'

# 同じ中間出力へ書く検査を並べる束の名前。MSBuild の束は obj・bin・dist を、除外一覧の束は
# 導出が書く置き場を共有する。
$msbuildBundle = 'MSBuild'
$exclusionBundle = '除外一覧'


$checks = [ordered]@{}
$checks[$build] = @{
    LimitSeconds = 7
    Needs = $noArtifact
    Stage = 1
    Produces = $buildOutput
    Run = @('dotnet', 'build', 'PmxEditorMcp.sln', '-warnaserror')
}
$checks['スクリプト構文'] = @{
    LimitSeconds = 5
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
    LimitSeconds = 3
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
    LimitSeconds = 3
    Needs = $noArtifact
    Run = @('lychee', '--offline', '--no-progress', '--include-fragments',
        '--exclude-path', '.scratch', '--exclude-path', 'docs/.scratch',
        '--exclude-path', 'dist', '**/*.md')
}
$checks[$derivation] = @{
    LimitSeconds = 3
    Needs = $buildOutput
    Bundle = $exclusionBundle
    Produces = $exclusionList
    Body = {
        # 凍結が落ちたらその終了コードのまま返したいので、続きを走らせずに抜ける。
        & $dump excluded-baseline $editorDir $ledger $baseline
        if ($LASTEXITCODE -eq 0) { & $dump excluded-signatures $editorDir $baseline $excluded }
    }
}
$checks['要約の持ち主'] = @{
    LimitSeconds = 3
    Needs = $noArtifact
    Body = {
        $orphans = @()
        foreach ($file in Get-ChildItem -Path src, tests -Recurse -Filter *.cs -File |
            Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }) {
            $lines = @(Get-Content -LiteralPath $file.FullName -Encoding utf8)
            for ($at = 0; $at -lt $lines.Count - 1; $at++) {
                $here = $lines[$at].Trim()
                $ends = $here -eq '/// </summary>' -or
                    ($here -like '/// <summary>*' -and $here -like '*</summary>')
                if (-not $ends) { continue }
                if ($lines[$at + 1].Trim() -notlike '/// <summary>*') { continue }

                $orphans += ('{0}:{1}' -f $file.FullName, ($at + 2))
            }
        }

        if ($orphans.Count -ne 0) {
            throw ('要約が持ち主から離れている: ' + ($orphans -join '・'))
        }
    }
}
$checks['実行時リフレクション'] = @{
    LimitSeconds = 3
    Needs = $buildOutput
    Run = @($dump, 'reflection-free', $editorDir, $hostDll)
}
$checks['整形'] = @{
    LimitSeconds = 53
    Needs = $buildOutput
    Bundle = $msbuildBundle
    Run = @('dotnet', 'format', 'PmxEditorMcp.sln', '--verify-no-changes')
}
$checks['テスト'] = @{
    LimitSeconds = 62
    Needs = $buildOutput
    Run = @('dotnet', 'test', 'PmxEditorMcp.sln', '--no-build')
}
$checks['台帳とSDKの照合'] = @{
    LimitSeconds = 3
    Needs = $exclusionList
    Bundle = $exclusionBundle
    Run = @($dump, 'ledger-coverage', $editorDir, $ledger, $excluded, $outOfScope)
}
$checks['日本語名の照合'] = @{
    LimitSeconds = 3
    Needs = $exclusionList
    Bundle = $exclusionBundle
    Run = @($dump, 'property-names', $editorDir, $ledger, $excluded, $names)
}
$checks['型役割の照合'] = @{
    LimitSeconds = 3
    Needs = $exclusionList
    Bundle = $exclusionBundle
    Run = @($dump, 'type-roles', $editorDir, $ledger, $excluded, $roles)
}
$checks['共通契約割当の照合'] = @{
    LimitSeconds = 3
    Needs = $exclusionList
    Bundle = $exclusionBundle
    Run = @($dump, 'common-assignments', $editorDir, $ledger, $excluded, $roles, $assignments)
}
$checks['値の表現の照合'] = @{
    LimitSeconds = 3
    Needs = $exclusionList
    Bundle = $exclusionBundle
    Run = @($dump, 'value-shapes', $editorDir, $ledger, $excluded, $contract)
}
$checks['危険操作の照合'] = @{
    LimitSeconds = 3
    Needs = $exclusionList
    Bundle = $exclusionBundle
    Run = @($dump, 'dangerous-operations', $editorDir, $ledger, $excluded)
}
$checks['能力対応表の照合'] = @{
    LimitSeconds = 3
    Needs = $exclusionList
    Bundle = $exclusionBundle
    Run = @($dump, 'tool-map', $editorDir, $ledger, $excluded, $roles, $assignments, $toolMap)
}
$checks['提供対象の網羅'] = @{
    LimitSeconds = 3
    Needs = $exclusionList
    Bundle = $exclusionBundle
    Run = @($dump, 'map-coverage', $editorDir, $ledger, $excluded, $roles, $toolMap)
}
$checks['スキーマ定義の照合'] = @{
    LimitSeconds = 3
    Needs = $buildOutput
    Run = @($dump, 'tool-schemas', $contract, $toolMap, $toolSchemas)
}
$checks['ツールの説明文の照合'] = @{
    LimitSeconds = 3
    Needs = $buildOutput
    Run = @($dump, 'tool-descriptions', $editorDir, $ledger, $contract, $roles, $names,
        $assignments, $toolMap)
}
$checks['サンプル値の照合'] = @{
    LimitSeconds = 3
    Needs = $buildOutput
    Run = @($dump, 'sample-values', $editorDir, $contract, $sampleValues)
}
$checks['発見可能性の照合'] = @{
    LimitSeconds = 3
    Needs = $buildOutput
    Run = @($dump, 'discovery', $editorDir, $ledger, $contract, $roles, $names,
        $assignments, $toolMap, $discoveryTasks)
}
$checks['スキーマ対応の照合'] = @{
    LimitSeconds = 3
    Needs = $buildOutput
    Run = @($dump, 'schema-correspondence', $editorDir, $ledger, $roles, $assignments,
        $toolMap, $toolSchemas)
}
$checks['ツールの検査の網羅'] = @{
    LimitSeconds = 5
    Needs = $buildOutput
    Run = @($dump, 'tool-coverage', $editorDir, $ledger, $contract, $roles, $names,
        $assignments, $toolMap, $toolSchemas, $sampleValues, $acceptance, $uncoveredTools)
}
$checks['規則適合検査'] = @{
    LimitSeconds = 3
    Needs = $buildOutput
    Run = @($dump, 'tool-mapping', $editorDir, $ledger, $contract, $roles, $assignments,
        $toolMap, $toolSchemas)
}
$checks['受入シナリオの照合'] = @{
    LimitSeconds = 5
    Needs = $buildOutput
    Run = @($dump, 'acceptance-cases', $editorDir, $ledger, $contract, $roles, $names,
        $assignments, $toolMap, $toolSchemas, $acceptance, $requirements, $acceptanceStub)
}
$checks['ブリッジの単独起動'] = @{
    LimitSeconds = 17
    Needs = $noArtifact
    Bundle = $msbuildBundle
    Run = @('pwsh', '-NoProfile', '-File', 'scripts/bridge-standalone.ps1')
}
$checks['形の導出の照合'] = @{
    LimitSeconds = 3
    Needs = $noArtifact
    Body = { Test-FormDerivation -Checks $checks }
}
$checks['配布パッケージの生成'] = @{
    LimitSeconds = 21
    Needs = $noArtifact
    Bundle = $msbuildBundle
    # 違えを入れる形は、通しが組み立てた出来上がりを写して使う。組を分けずに順に走らせる。
    FormsInOrder = $true
    Forms = { @($wholeForm) + @((Get-PackageSpoils -Copy '').Keys) }
    Body = { param([string]$Form)

        Test-PackageContents -Form $Form
    }
}
$checks['E2Eの実行器の照合'] = @{
    LimitSeconds = 63
    Needs = $noArtifact
    Forms = {
        $defined = Get-Content $e2eStub -Raw -Encoding UTF8 | ConvertFrom-Json

        @($wholeForm) + @((Get-E2eExpectationForms -Defined $defined).Keys) +
            @((Get-FormsBeyondCases -Path $e2eStubForms).Keys)
    }
    Body = { param([string]$Form)

        Test-E2eRunner -Cases $e2eStub -Form $Form
    }
}
$checks['検査の集計の照合'] = @{
    LimitSeconds = 5
    Needs = $noArtifact
    # 形どうしは同じ題材の中で並ぶ。組を分けず、1回の実行で形ごとの合否を書かせる。
    FormsInOrder = $true
    Forms = { @(node scripts/checks-stub-run.mjs --list) }
    # 形と結末の置き場の渡し方。走らせる側がこの綴りの後ろへ値を足す。
    FormArgument = '--forms'
    ResultsArgument = '--results'
    Run = @('node', 'scripts/checks-stub-run.mjs')
}
$checks['確認クライアントの照合'] = @{
    LimitSeconds = 32
    Needs = $noArtifact
    Forms = { @($wholeForm) + @((Get-CheckClientForms).Keys) }
    Body = { param([string]$Form)

        Test-CheckClient -Form $Form
    }
}
$checks['実機動作確認の実行器の照合'] = @{
    LimitSeconds = 37
    Needs = $noArtifact
    Forms = { @($wholeForm) + @((Get-LiveHostRunnerForms).Keys) }
    Body = { param([string]$Form)

        Test-LiveHostRunner -Form $Form
    }
}
$checks['参照クライアントの実行器の照合'] = @{
    LimitSeconds = 46
    Needs = $noArtifact
    Forms = { @($wholeForm) + @((Get-LiveClientRunnerForms).Keys) }
    Body = { param([string]$Form)

        Test-LiveClientRunner -Form $Form
    }
}
$checks['受入の実行器の照合'] = @{
    LimitSeconds = 120
    Needs = $noArtifact
    Forms = {
        $defined = Get-Content $acceptanceStub -Raw | ConvertFrom-Json

        @($wholeForm) + @((Get-AcceptanceExpectationForms -Defined $defined).Keys) +
            @((Get-FormsBeyondCases -Path $acceptanceStubForms).Keys)
    }
    Body = { param([string]$Form)

        $temp = [System.IO.Path]::GetTempPath()
        Test-AcceptanceRunner -Cases $acceptanceStub `
            -Progress (Join-Path $temp $StubProgressStateName) `
            -Operations (Join-Path $temp $StubOperationLogName) `
            -Editors (Join-Path $temp $StubLaunchStateName) `
            -Form $Form
    }
}

# 検査を、それが読む入力ごとに分ける。何を変えたかで、走らせる群が決まる。数えるのは引数に現れる
# ファイルだけではない。Body が呼ぶスクリプトも、その呼び先が中で読むファイルも、その実行ファイルを
# 作る場所も入力である——そこまで辿らずに群を決めると、変えた当のコードを動かす検査が1件も走らない
# まま合格が出る。読む入力が複数の種類にまたがる検査は、そのすべての群に入れる。
$checkGroups = [ordered]@{
    'ドキュメント' = @('文書のリンク', '受入シナリオの照合')
    '定義' = @($derivation, '台帳とSDKの照合', '日本語名の照合', '型役割の照合',
        '形の導出の照合',
        '共通契約割当の照合', '値の表現の照合', '危険操作の照合', '能力対応表の照合',
        '提供対象の網羅', 'スキーマ定義の照合', 'ツールの説明文の照合', 'サンプル値の照合',
        '発見可能性の照合', 'スキーマ対応の照合', 'ツールの検査の網羅', '規則適合検査',
        '受入シナリオの照合', 'テスト', '文書のリンク')
    'コード' = @('整形', '実行時リフレクション', '要約の持ち主', 'テスト')
    # 実行器そのものと、その代わりを立てる題材だけを入力にする検査。実行器の照合は、突き合わせる
    # 相手をすべて自分で立てるので、製品のコードや正本が変わっても答えが変わらない。文書のリンクを
    # 入れるのは、追跡下の文書がここの実行器を名指しで指しており、名前を変えれば指し先が消える
    # ためである。
    'スクリプト' = @('スクリプト構文', 'スクリプト構文(PowerShell)', '検査の集計の照合',
        '形の導出の照合',
        'E2Eの実行器の照合', '確認クライアントの照合', '実機動作確認の実行器の照合',
        '参照クライアントの実行器の照合', '受入の実行器の照合', '文書のリンク')
    'ブリッジ配布' = @('ブリッジの単独起動', '配布パッケージの生成')
    # 上の群のどれにも入らない検査をここへ並べる。全件を走らせるときにしか出番が無いという
    # 申告で、`@($checks.Keys)` のような一括の指定にはしない——一括にすると、新しい検査を上の群へ
    # 入れ忘れても全件の側が黙って拾い、入れ忘れを落とす検査が素通りになる。
    '全件のみ' = @($build)
}

# 変えたものの道から、走らせる群を選ぶ表。1つの道が複数の群に当たるなら、当たった群をすべて
# 走らせる。**当たらない道が1つでもあれば全件へ倒す**——表に無い道は、その変更をどの検査が見るのか
# を誰も決めていないということで、決めていないまま一部だけ走らせると、見る検査が1件も走らない
# ままになる。
$groupPaths = [ordered]@{
    'ドキュメント' = @('*.md')
    # 定義の群はどれも生成器の実行ファイルを走らせるので、それを作る場所も定義の入力である。
    # 受入の題材は、実物と揃っていることを受入シナリオの照合が見るので、同じ群へ入れる。
    '定義' = @('catalog/*', 'src/SignatureDump/*', 'scripts/acceptance-stub-cases.json')
    'コード' = @('src/*', 'tests/*')
    'スクリプト' = @('scripts/*')
    # 配布の組み立ては、ホストと生成器を Release で作り直し、同梱する手引きを写し、第三者
    # ライセンスの本文を読む。単独起動もその実行器そのものを走らせる。どれもこの群の入力である。
    'ブリッジ配布' = @('src/Bridge/*', 'src/HostPlugin/*', 'src/SignatureDump/*',
        'scripts/package*', 'scripts/bridge-standalone.ps1', 'docs/package/*',
        'catalog/observed/licenses/*')
}

# 道からは群を決められないもの。どの検査の中身もこの1本が持つので、ここが変われば、影響する
# 群を道の形では言えない。
$ungrouped = @('scripts/check-set.ps1')

