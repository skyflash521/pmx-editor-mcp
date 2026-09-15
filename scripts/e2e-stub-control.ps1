# 自動E2E検査の実行器を確かめるための、画面を触らない操作役の代わり。
# 実機のエディタが無くても実行器が通しで走れるよう、頼まれた操作へ決まった返事を返す。
[CmdletBinding()]
param(
    # 頼む操作。実行器が使うものだけを受け付ける。
    [Parameter(Mandatory = $true)]
    [ValidateSet("answer", "capture")]
    [string]$Action,

    # 操作の相手を指す名前。代わりでは使わないが、実行器が必ず渡す。実機のプロセスIDと紛れない
    # 名前を使うので、数に限らない。
    [string]$ProcessId,

    # 写し取るビューの名前。capture で使う。
    [string]$View,

    # 写し取った画像の書き出し先。capture で使う。
    [string]$Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 待受の代わりが「写したすがた」と同じと答える中身。見比べる相手の代わりがこの文字列を見る。
$SameView = 'うつしたすがた'

switch ($Action) {
    'answer' {
        # 応答待ちの表示は出ないので、閉じたものは無い。
    }
    'capture' {
        if (-not $Path) { throw "この操作には -Path が要る: $Action" }

        Set-Content -Path $Path -Value $SameView -Encoding UTF8
        Write-Output '1x1'
    }
}
