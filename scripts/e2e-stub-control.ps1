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

    # 写し取るビューの名前。capture で使う。実物と同じく、並べて渡された分をまとめて相手にする。
    [string[]]$View,

    # 写し取った画像の書き出し先。capture で使う。-View と同じ数を同じ並びで渡す。
    [string[]]$Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 写しの中身の書き出し。これにビューの名を続けたものを写しとして書く。待受の代わりは、行が
# 名乗るビューを続けた同じ文字列を返す。見比べる相手の代わりは、この2つが同じかだけを見る。
$SameView = 'うつしたすがた'

switch ($Action) {
    'answer' {
        # 応答待ちの表示は出ないので、閉じたものは無い。
    }
    'capture' {
        $names = @($View)
        $places = @($Path)
        if ($places.Count -ne $names.Count) {
            throw "この操作には -View と同じ数の -Path が要る: $Action"
        }

        for ($at = 0; $at -lt $names.Count; $at++) {
            Set-Content -Path $places[$at] -Value "$SameView $($names[$at])" -Encoding UTF8
            Write-Output '1x1'
        }
    }
}
