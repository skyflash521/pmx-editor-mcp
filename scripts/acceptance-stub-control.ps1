# 受入の実行器を確かめるための、エディタとホストを持たない操作役。
# 実物と同じ引数を受け、実物と同じ形の戻り値を返すだけで、画面にも稼働状態にも触らない。
# 起動したエディタのプロセスIDは、応答を作る相手と同じ数え方で採番する。
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(
        "pipes", "editors", "launch", "close", "status", "stop", "start", "acl", "undo", "answer",
        "show", "click", "capture")]
    [string]$Action,

    [int]$ProcessId,

    [ValidateSet("pmx", "transform")]
    [string]$View,

    [string]$Path,

    [ValidateRange(1, 2147483)]
    [int]$TimeoutSeconds = 20
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot 'stub-shared.ps1')

$asked = $Action
if ($View) { $asked += ":" + $View }
Add-Content -Path (Join-Path ([System.IO.Path]::GetTempPath()) $StubOperationLogName) `
    -Value $asked -Encoding UTF8

$state = Join-Path ([System.IO.Path]::GetTempPath()) $StubLaunchStateName
$held = [pscustomobject]@{ Launched = 0; Live = @() }
if (Test-Path $state) { $held = Get-Content $state -Raw -Encoding UTF8 | ConvertFrom-Json }
$live = [System.Collections.ArrayList]@($held.Live | ForEach-Object { [int]$_ })

function Save-StubEditors {
    param([int]$Launched, $Live)

    [pscustomobject]@{ Launched = $Launched; Live = @($Live) } |
        ConvertTo-Json -Compress |
        Set-Content -Path $state -Encoding UTF8 -NoNewline
}

switch ($Action) {
    "editors" {
        $live
    }
    "launch" {
        $launched = [int]$held.Launched + 1
        $editor = $FirstStubEditorId + $launched - 1
        [void]$live.Add($editor)
        Save-StubEditors -Launched $launched -Live $live
        $editor
    }
    "close" {
        $live.Remove($ProcessId)
        Save-StubEditors -Launched ([int]$held.Launched) -Live $live
    }
    "capture" {
        if (-not $Path) { throw "この操作には -Path が要る: $Action" }

        Add-Type -AssemblyName System.Drawing
        $image = New-Object System.Drawing.Bitmap($StubViewWidth, $StubViewHeight)
        try {
            $image.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $image.Dispose()
        }

        "$StubViewWidth" + "x" + "$StubViewHeight"
    }
    default {
    }
}
