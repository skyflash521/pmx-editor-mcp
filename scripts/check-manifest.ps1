# 検査の一覧を、走らせる側([verify.mjs](verify.mjs))が読める形で出す。値は持たない。
[CmdletBinding()]
param(
    # どちらの一覧を出すか。
    [Parameter(Mandatory)][ValidateSet('standing', 'live')][string]$Set
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 読む側は UTF-8 として解く。
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()

if ($Set -eq 'standing') {
    . (Join-Path $PSScriptRoot 'check-set.ps1')
    $scope = '常設の検査'
} else {
    . (Join-Path $PSScriptRoot 'live-checks.ps1')
    $scope = '実機に触る検査'
}

$listed = @()
foreach ($name in $checks.Keys) {
    $one = $checks[$name]

    # 外部の1本を呼ぶだけの検査は、その1本を直に起こす。PowerShell の筋が要る検査だけ、1件を
    # 走らせる側を通す。
    $given = if ($one.Contains('Run')) {
        $one.Run
    } else {
        @('pwsh', '-NoProfile', '-NonInteractive', '-File', 'scripts/run-check.ps1',
            '-Set', $Set, '-Name', $name)
    }

    # 形を持つ検査は、形1つが検査1件になる。走らせる側が名前を組み立てて束へ分ける。
    $forms = if ($one.Contains('Forms')) { @(& $one.Forms) } else { @() }

    $listed += [ordered]@{
        name = $name
        forms = $forms
        formsInOrder = [bool]$one.Contains('FormsInOrder')
        formArgument = if ($one.Contains('FormArgument')) { $one.FormArgument } else { '-Form' }
        resultsArgument = if ($one.Contains('ResultsArgument')) { $one.ResultsArgument } else { '-Results' }
        limitSeconds = $one.LimitSeconds
        needs = $one.Needs
        # 束を指していない検査は、自分だけの束に入る。
        bundle = if ($one.Contains('Bundle')) { $one.Bundle } else { $name }
        # 出来上がりを作る検査だけが1で、ほかは2である。
        stage = if ($one.Contains('Stage')) { $one.Stage } else { 2 }
        produces = if ($one.Contains('Produces')) { $one.Produces } else { $null }
        file = $given[0]
        args = @($given | Select-Object -Skip 1)
    }
}

$manifest = [ordered]@{
    scope = $scope
    checks = $listed
    makers = @($listed | Where-Object { $_.produces } |
        ForEach-Object { [ordered]@{ name = $_.name; produces = $_.produces } })
}

if (Get-Variable -Name checkGroups -Scope Script -ErrorAction Ignore) {
    $manifest.groups = $checkGroups
    $manifest.groupPaths = $groupPaths
    $manifest.ungrouped = $ungrouped
}

if (Get-Variable -Name conditional -Scope Script -ErrorAction Ignore) {
    $manifest.conditional = $conditional
}

if (Get-Variable -Name environment -Scope Script -ErrorAction Ignore) {
    $manifest.environment = $environment
}

if (Get-Variable -Name afterFailure -Scope Script -ErrorAction Ignore) {
    $manifest.afterFailure = [ordered]@{
        file = $afterFailure[0]
        args = @($afterFailure | Select-Object -Skip 1)
    }
}

$manifest | ConvertTo-Json -Depth 8 -Compress
