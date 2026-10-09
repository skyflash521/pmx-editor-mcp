# ブリッジと参照クライアントの実機動作確認を確かめるための、エディタとホストを持たない操作役。
# 実物と同じ引数を受け、起こしたことにするプロセスIDを返すだけで、画面にも稼働状態にも触らない。
# 採番は応答を作る相手と同じ並びにする。
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("editors", "launch", "close", "status", "stop", "start")]
    [string]$Action,

    [int]$ProcessId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot 'stub-shared.ps1')

$state = Join-Path ([System.IO.Path]::GetTempPath()) $LiveStubEditorsName
$used = Join-Path ([System.IO.Path]::GetTempPath()) $LiveStubLaunchedName
$live = @()
if ([System.IO.File]::Exists($state)) {
    $live = @([System.IO.File]::ReadAllLines($state) | Where-Object { $_ } |
        ForEach-Object { [int]$_ })
}

switch ($Action) {
    "editors" {
        $live
    }
    "launch" {
        # 閉じた相手の番号は再び使わない。実物も、起こし直したエディタには別のプロセスIDを割り当てる。
        $next = $FirstStubEditorId
        if ([System.IO.File]::Exists($used)) {
            $next = [int]([System.IO.File]::ReadAllText($used)) + 1
        }

        [System.IO.File]::WriteAllText($used, "$next")
        [System.IO.File]::WriteAllLines($state, [string[]](@($live) + $next))
        $next
    }
    "close" {
        [System.IO.File]::WriteAllLines(
            $state, [string[]]@($live | Where-Object { $_ -ne $ProcessId }))
    }
    default {
    }
}
