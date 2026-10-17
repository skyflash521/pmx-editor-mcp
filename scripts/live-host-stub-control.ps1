# 実機動作確認を確かめるための、実機のエディタを持たない操作役。
# 実物と同じ引数を受け、待受だけを持つエディタの代わりを起こして、その稼働状態を動かす。
# 返す形も実物と同じにする。
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("launch", "close", "status", "stop", "start", "acl", "editors", "pipes")]
    [string]$Action,

    [int]$ProcessId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot 'stub-shared.ps1')

$broken = [System.Environment]::GetEnvironmentVariable($LiveHostStubBrokenName)
$temp = [System.IO.Path]::GetTempPath()

function Get-StubStatePath {
    param([int]$Editor)

    Join-Path $temp ($LiveHostStubStatePrefix + $Editor + ".txt")
}

function Test-StubPipe {
    param([int]$Editor)

    $wanted = $LiveHostStubPipePrefix + $Editor

    @([System.IO.Directory]::GetFileSystemEntries("\\.\pipe\") |
        ForEach-Object { Split-Path -Leaf $_ }) -contains $wanted
}

function Wait-StubPipeState {
    <#
        .SYNOPSIS
        待受が求める状態になるまで待つ。諦めるまでの秒数は、実物の既定と同じ値を採る。
    #>
    param([int]$Editor, [bool]$Present)

    $until = [datetime]::Now.AddSeconds($LiveStubWaitSeconds)
    while ([datetime]::Now -lt $until) {
        if ((Test-StubPipe -Editor $Editor) -eq $Present) { return }
        Start-Sleep -Milliseconds 50
    }

    throw "待受が $LiveStubWaitSeconds 秒以内に $Present にならない: $Editor"
}

function Set-StubState {
    param([int]$Editor, [string]$Wanted)

    [System.IO.File]::WriteAllText((Get-StubStatePath -Editor $Editor), $Wanted)
}

switch ($Action) {
    "launch" {
        $editor = Start-Process -FilePath 'node' -PassThru -WindowStyle Hidden `
            -ArgumentList @((Join-Path $PSScriptRoot 'live-host-stub-editor.mjs'))
        Wait-StubPipeState -Editor $editor.Id -Present $true
        $editor.Id
    }
    "close" {
        if (Test-Path (Get-StubStatePath -Editor $ProcessId)) {
            Set-StubState -Editor $ProcessId -Wanted $LiveHostStubClosed
        }

        $running = Get-Process -Id $ProcessId -ErrorAction Ignore
        if ($running) { $running.WaitForExit($LiveStubWaitSeconds * 1000) | Out-Null }
    }
    "stop" {
        Set-StubState -Editor $ProcessId -Wanted $LiveHostStubStopped
        Wait-StubPipeState -Editor $ProcessId -Present $false
    }
    "start" {
        Set-StubState -Editor $ProcessId -Wanted $LiveHostStubRunning
        Wait-StubPipeState -Editor $ProcessId -Present $true
    }
    "status" {
        $stopped = -not (Test-StubPipe -Editor $ProcessId)
        "状態: " + $(if ($stopped) { "停止済み" } else { "稼働中" })
    }
    "acl" {
        # 実物は待受に掛かっている規則を読んで返す。ここは同じ形の組を作って返す。
        $ours = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
        $rule = [pscustomobject]@{
            IdentityReference = [pscustomobject]@{ Value = $ours }
            AccessControlType = 'Allow'
            PipeAccessRights  = 'FullControl'
        }
        if ($broken -eq 'acl') {
            $rule
            [pscustomobject]@{
                IdentityReference = [pscustomobject]@{ Value = 'BUILTIN\Administrators' }
                AccessControlType = 'Allow'
                PipeAccessRights  = 'FullControl'
            }
        } else {
            $rule
        }
    }
    default {
    }
}
