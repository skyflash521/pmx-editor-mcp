# 受入の実行器が使う、操作役を常駐させて呼ぶ中継。
# 標準入力の1行を、操作役へ渡す引数の並び(文字列のJSON配列)として受け、同じプロセスの中で
# 操作役を呼ぶ。呼ぶたびに操作役の書き出した行を返し、最後に次の1行で結果を閉じる。
#
#   <Token> <0か1> <失敗の説明をUTF-8で符号化したBase64。成功では空>
#
# 標準入力が閉じたら終わる。
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Target,

    [Parameter(Mandatory = $true)]
    [string]$Token
)

Set-StrictMode -Version Latest

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

while ($null -ne ($line = [Console]::In.ReadLine())) {
    $failure = ''
    try {
        [string[]]$arguments = @($line | ConvertFrom-Json)
        $named = @{}
        for ($at = 0; $at -lt $arguments.Count; $at += 2) {
            if (-not $arguments[$at].StartsWith('-') -or $at + 1 -ge $arguments.Count) {
                throw "引数は名前と値の組で並べる: $line"
            }

            $named[$arguments[$at].Substring(1)] = $arguments[$at + 1]
        }

        $written = & $Target @named
        foreach ($said in @($written | Out-String -Stream)) {
            [Console]::Out.WriteLine($said)
        }
    }
    catch {
        $failure = ($_ | Out-String).Trim()
        if ($failure.Length -eq 0) { $failure = '(何も言いませんでした)' }
    }

    $encoded = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($failure))
    [Console]::Out.WriteLine("$Token $(if ($failure) { 1 } else { 0 }) $encoded")
    [Console]::Out.Flush()
}
