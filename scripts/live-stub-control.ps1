# ブリッジと参照クライアントの実機動作確認を確かめるための、エディタとホストを持たない操作役。
# 実物と同じ引数を受け、起こしたことにするプロセスIDを返すだけで、画面にも稼働状態にも触らない。
# 採番は応答を作る相手と同じ並びにする——どちらもこの並びを前提に、名乗る相手を突き合わせる。
#
# 呼ばれるたびにこの1本が起こされるので、読み書きは行の読み書きだけに留める。組を綴る道具は
# 読み込みに時間が掛かり、実行器の1回ぶんがそのぶん伸びる。
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
        # 起こした数はプロセスIDの続きから数える。閉じた相手の番号は再び使わない——実物も、
        # 起こし直したエディタには別のプロセスIDを割り当てる。
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
        # 残りの操作は、実物と同じく何も書き出さずに戻る。
    }
}
