[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('standing', 'live')][string]$Set
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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
    $one = [Check]$checks[$name]

    $given = if ($one.Run) {
        $one.Run
    } else {
        @('pwsh', '-NoProfile', '-NonInteractive', '-File', 'scripts/run-check.ps1',
            '-Set', $Set, '-Name', $name)
    }

    $forms = if ($one.Forms) { @(& $one.Forms) } else { @() }

    $listed += [ordered]@{
        name = $name
        forms = $forms
        formsInOrder = $one.FormsInOrder
        formArgument = $one.FormArgument
        resultsArgument = $one.ResultsArgument
        limitSeconds = $one.LimitSeconds
        needs = $one.Needs
        bundle = if ($one.Bundle) { $one.Bundle } else { $name }
        stage = $one.Stage
        produces = $one.Produces
        groups = @($one.Groups)
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

if (Get-Variable -Name groupPaths -Scope Script -ErrorAction Ignore) {
    $grouped = [ordered]@{}
    foreach ($named in $groupPaths.Keys) { $grouped[$named] = @() }
    if (Get-Variable -Name pathlessGroups -Scope Script -ErrorAction Ignore) {
        foreach ($named in $pathlessGroups) { $grouped[$named] = @() }
    }

    foreach ($one in $listed) {
        foreach ($named in $one.groups) {
            if (-not $grouped.Contains($named)) {
                throw "道の表に無い群を名乗る検査がある: $($one.name) の $named"
            }

            $grouped[$named] += $one.name
        }
    }

    $manifest.groups = $grouped
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
