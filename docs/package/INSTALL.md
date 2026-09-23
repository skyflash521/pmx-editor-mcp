# pmx-editor-mcp インストール指示書

PMXエディタをMCP経由で操作できるようにする。

## 実行できるか

この手順は、Windows上でコマンドを実行し、次のすべてに届くことを要する。

- 実行中のプロセスの列挙・終了・起動
- レジストリの読み取り
- 名前付きパイプの列挙
- `%LOCALAPPDATA%` と、PMXエディタの導入フォルダの `_plugin\User` への書き込み

一つでも満たさないときは、次の順に落とす。

1. **コマンドは実行できないが、ファイルは書けるとき。** [導入する](#導入する)の各手順を行うスクリプトを
   書いて配布物と同じ場所へ置き、エンドユーザーには実行だけ頼む。スクリプトには、手順ごとの合否と
   その判定に使った値をファイルへ書かせる。
2. **どちらもできないとき。** この文書をエンドユーザーへ渡して導入を頼み、結果を受け取る。

どちらの場合も、判定と次の手順はエージェントが決める。エンドユーザーの自己申告を確かめるに
代えない。

## はじめに決めること

着手の前に3つとも決める。手順の途中でエンドユーザーへ訊きに戻らない。**尋ねるものは一度に
まとめて尋ねる。**

| 決めること | 決め方 |
|---|---|
| PMXエディタの導入フォルダ | 起動中の `PmxEditor_x64` のプロセスの実行ファイルのある場所。エディタが起動していなければ `PmxEditor_x64.exe` をディスクから探し、1件だけ見つかったならそれを候補としてエンドユーザーへ示して確かめる。0件か2件以上、またはコマンドを実行できないならエンドユーザーへ尋ねる |
| 登録先のMCPクライアント | エンドユーザーへ尋ねる。[登録する](#登録する)の表から選ぶ |
| PMXエディタを終了してよいか | エンドユーザーへ確かめる。[止める](#止める)で終了させる |

## 前提

| 項目 | 確かめる |
|---|---|
| x64 の Windows | OSが64ビットであること |
| .NET Framework 4.8 以上 | レジストリ `HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full` の `Version` が `4.8` 以上(文字列でなくバージョン番号として比べる) |
| PMXエディタ(x64版) 0.2.7.3 以上 | 導入フォルダに `PmxEditor_x64.exe` が在り、ファイルバージョンが `0.2.7.3` 以上。導入フォルダの `Lib\PEPlugin\PEPlugin.dll` のファイルバージョンが `0.0.8.9` 以上(いずれも文字列でなくバージョン番号として比べる) |
| 書き込み | `_plugin\User` が `C:\Program Files` の下にあるなら、管理者権限で実行する |

## 置き場

| 物 | 置き場 |
|---|---|
| 配布物5点(`INSTALL.md`・`LICENSE.txt`・`PmxEditorMcp.Bridge.exe`・`PmxEditorMcp.dll`・`ThirdPartyNotices.txt`) | `%LOCALAPPDATA%\pmx-editor-mcp` |
| ホスト | PMXエディタの導入フォルダの `_plugin\User\PmxEditorMcp.dll` |

置き場が無ければ作る。`_plugin\User`・導入先・クライアントの設定ファイルのフォルダのいずれも同じ。

## 導入する

次の順に行う。更新も同じ順に行う。

1. [止める](#止める)
2. [配布物を導入先へ複写する](#配布物を導入先へ複写する)
3. [ホストを配置する](#ホストを配置する)
4. [登録する](#登録する)
5. [エディタを起動する](#エディタを起動する)
6. [疎通を確かめる](#疎通を確かめる)

2 か 3 へ戻ってやり直すときは、先に[止める](#止める)を行う。4 で起きた Bridge がファイルを掴んで
いる。

## 止める

次の順に止める。

1. **登録済みのMCPクライアントを終了させる。** クライアントは登録があるだけで Bridge を起こす。
   起こす側を止めないうちに Bridge だけを終了させても、掴み直されうる。
2. **残っている `PmxEditorMcp.Bridge` のプロセスを終了させる。**
3. **[エディタを終了する](#エディタを終了する)。**

クライアントを終了できないとき(そのクライアントの上でエージェントが動いているときを含む)は、
1 の代わりに[登録する](#登録する)の機構で登録を消す。導入・更新を終えてから登録し直す。

確かめる: `PmxEditorMcp.Bridge` のプロセスが無く、[対象のエディタ](#エディタを終了する)のプロセスも
無く、クライアントを終了させたのならそのクライアントのプロセスも無い。残るうちは1秒おきに見直し、
30秒で打ち切って、保存の確認が画面に出ていないかをエンドユーザーへ確かめる。

## エディタを終了する

**対象は、決めた導入フォルダの `PmxEditor_x64.exe` から起動したプロセスに限る。** 同じ名前の
エディタは別の導入フォルダからも動く。実行ファイルのパスまで確かめずに終了させると、無関係な
編集作業を巻き込む。

**PMXエディタは編集とビューのウィンドウを別々に持ち、閉じ残すとプロセスが終わらない。**
メインウィンドウだけを閉じる手立て(`Process.CloseMainWindow` など)では終わらないのに、
戻り値は成功を返す。これを終了と受け取らない。**強制終了もしない**——プラグインの後始末を
通らない。

対象のプロセスがデスクトップ直下に持つウィンドウを**すべて**列挙し、各々へ `WM_CLOSE` を送る。

対象は数え直すたびに列挙する。一度取った一覧は、プロセスが終わっても件数が減らないので、
終わったかどうかの判定には使えない。

同じ実体を指すかどうかは、パスの文字列では決まらない。ジャンクション・シンボリックリンク・
8.3形式の短い名前を経由すると綴りが変わる。開いたハンドルから最終パスを取り直して比べる。

```powershell
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

# 同じセッションで二度目を走らせても落ちないよう、型は無いときだけ作る。
if (-not ("Win.Native" -as [type])) {
    Add-Type -Namespace Win -Name Native -UsingNamespace System.Text -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(
    IntPtr window, uint message, IntPtr wparam, IntPtr lparam);

[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
private static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr security,
    uint disposition, uint flags, IntPtr template);
[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
private static extern uint GetFinalPathNameByHandleW(IntPtr file, StringBuilder path, uint length,
    uint flags);
[DllImport("kernel32.dll", SetLastError = true)]
private static extern bool CloseHandle(IntPtr handle);

public static string FinalPath(string path) {
    IntPtr handle = CreateFileW(path, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
    if (handle == new IntPtr(-1)) { return null; }
    try {
        StringBuilder buffer = new StringBuilder(32768);
        uint written = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
        if (written == 0 || written >= buffer.Capacity) { return null; }
        return buffer.ToString();
    } finally { CloseHandle(handle); }
}
'@
}

# 決めた導入フォルダを入れる。
$editorDirectory = "C:\path\to\PmxEditor"

$target = [Win.Native]::FinalPath((Join-Path $editorDirectory "PmxEditor_x64.exe"))
if ($null -eq $target) {
    throw "導入フォルダのエディタの実行ファイルを開けない: $editorDirectory"
}

function Get-TargetEditor {
    # 実行ファイルを読めないプロセスは、別の利用者のものなので対象から外す。
    param([string]$FinalPath)

    @(Get-Process -Name PmxEditor_x64 -ErrorAction Ignore | Where-Object {
        $actual = $null
        try { $actual = $_.Path } catch { }
        if ($null -eq $actual) { return $false }

        $resolved = [Win.Native]::FinalPath($actual)
        $null -ne $resolved -and [string]::Equals(
            $resolved, $FinalPath, [System.StringComparison]::OrdinalIgnoreCase)
    })
}

foreach ($editor in (Get-TargetEditor -FinalPath $target)) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $editor.Id)
    foreach ($window in $root.FindAll(
        [System.Windows.Automation.TreeScope]::Children, $condition)) {
        [void][Win.Native]::PostMessage([IntPtr]$window.Current.NativeWindowHandle, 0x0010,
            [IntPtr]::Zero, [IntPtr]::Zero)
    }

    [void]$editor.WaitForExit(30000)
}
```

**送るのは一巡だけにし、あとは終了を待つ。** 答えを待っている確認の表示へ送り直すと、その確認は
取り消されて出直すので、送り直すほど終わらなくなる。

待っても終わらなければ、送り直さずに画面を見る。**確認の表示が出ていたら、こちらで答えず
エンドユーザーへ回す**——保存の可否はエンドユーザーが決める。画面を見られないならエンドユーザーへ
頼む。表示が閉じてもプロセスが残っているなら、上のコードをもう一度そのまま走らせる。

確かめる: `Get-TargetEditor -FinalPath $target` が0件を返す。

## 配布物を導入先へ複写する

展開した場所から `%LOCALAPPDATA%\pmx-editor-mcp` へ5点とも複写し、複写した全ファイルのブロック
属性を解除する。上書きするのはこの5点だけとする。

確かめる: 導入先に5点が揃っており、5点ともハッシュが展開した場所のそれと一致する。ファイルバージョンでは
足りない——バージョンを持つのは `.exe` と `.dll` だけで、複写が途中で切れても同じバージョンなら一致する。

## ホストを配置する

導入先の `PmxEditorMcp.dll` を `_plugin\User` へ複写し、ブロック属性を解除する。

確かめる: `_plugin\User\PmxEditorMcp.dll` が在り、そのハッシュが導入先の `PmxEditorMcp.dll` と
一致する。

## 登録する

登録先は、コマンドで起動するMCPサーバー(stdio)を扱うMCPクライアントとする。そこへ次を登録する。

| 項目 | 値 |
|---|---|
| サーバー名 | `pmx-editor-mcp` |
| コマンド | 導入先の `PmxEditorMcp.Bridge.exe` |
| 引数・環境変数 | 無し。空の値を書かず、項目ごと省く |

コマンドの値は、登録の機構で形が変わる。登録のコマンドへ渡すなら `%LOCALAPPDATA%` を含むままで
よい。設定ファイルへ直に書くときは、環境変数を展開した絶対パスにする。

設定ファイルを直に書き換えるときは、控えを取り、ほかの項目を残す。どのクライアントでも同じ。

`pmx-editor-mcp` の名前の登録が既に在るときは、消してから登録し直す。上書きを受け付けない機構が
あり、更新でここが止まる。

| クライアント | 登録先 |
|---|---|
| Claude Code CLI | MCPサーバー登録のコマンド。**ユーザースコープ**で登録する |
| Codex CLI | MCPサーバー登録のコマンド。`%USERPROFILE%\.codex\config.toml` へ書かれる |
| Claude デスクトップ | `%APPDATA%\Claude\claude_desktop_config.json` の `mcpServers`。JSONとして読み書きし、BOM無しのUTF-8で書く |
| そのほか | そのクライアントの登録の機構 |

確かめる: そのクライアントが出す登録の内容で、名前とコマンドが上の値と一致する。

## エディタを起動する

PMXエディタを起動する。

確かめる: [待受](#待受)が1つ在る。0なら2秒おきに数え直し、40秒で打ち切る。打ち切ったら、エディタの
画面にダイアログが出ていないかを確かめ、出ていれば閉じてから数え直す(画面を見られないなら
エンドユーザーへ頼む)。それでも0なら[うまくいかないとき](#うまくいかないとき)。2つ以上なら
[エディタが複数のとき](#エディタが複数のとき)。

## 待受

ホストは待受のパイプを `\\.\pipe\pmx-editor-mcp-<エディタのプロセスID>` の名前で開く。`\\.\pipe\`
をディレクトリとして列挙し、この形の名前だけを数える。大文字小文字は区別し、プロセスIDの部分は
0で始まらない数字列とする。ディレクトリ列挙が通らない実装なら、ファイル列挙のAPIで読む。

## Bridge

Bridge のプロセスは、登録したMCPクライアントが起こす。**いくつ起きるかはクライアントの起こし方で
決まる。** 同じクライアントを複数開けばその数だけ増え、1つしか開いていなくても
複数動くことがある。ツールの呼び出しを一度も受けていない Bridge はエディタへ接続しないので、
動いている Bridge の数と[待受](#待受)の数は一致しない。

対象のエディタを決めるのは[待受](#待受)であって Bridge の数ではない。[止める](#止める)の
「残っている `PmxEditorMcp.Bridge` のプロセスを終了させる」は、見つかったものを全部終了させる。

## 疎通を確かめる

登録したクライアントから `pmx-editor-mcp` の `ping` ツールを呼ぶ。登録は、そのクライアントを起こし
直してから効く。

| クライアント | 呼ぶ主体 |
|---|---|
| Claude Code CLI・Codex CLI | エージェントが、そのCLIを新しいプロセスで非対話に起動して呼ぶ |
| Claude デスクトップ・そのほかの常駐するクライアント | エンドユーザーへ頼む。通知領域のアイコンからアプリを終了させ、起動し直して `ping` を呼び、応答を返してもらう |

確かめる: 応答が `pong` を含み、`BRIDGE_` で始まらない。`BRIDGE_` で始まるときは次を行う。

| コード | 対処 |
|---|---|
| `BRIDGE_NO_EDITOR` | [エディタを起動する](#エディタを起動する) |
| `BRIDGE_NO_HOST` | [うまくいかないとき](#うまくいかないとき) |
| `BRIDGE_MULTIPLE_HOSTS` | [エディタが複数のとき](#エディタが複数のとき) |
| 上の表に無い `BRIDGE_` | [止める](#止める)から[疎通を確かめる](#疎通を確かめる)までを順にやり直す。同じコードが返るなら、そのコードと応答の本文を添えてエンドユーザーへ報告する |

## エディタが複数のとき

対象にしないエディタを[終了する](#エディタを終了する)か、そのエディタのプラグインメニュー
「PMX Editor MCP」を開いて停止を選ぶ。

確かめる: [待受](#待受)が1つだけになる。

## アンインストールする

1. [止める](#止める)
2. **登録を消す。** [登録する](#登録する)で使った機構で消す。Claude デスクトップは `mcpServers`
   から `pmx-editor-mcp` の項目を消す。
3. **ファイルを消す。** `_plugin\User\PmxEditorMcp.dll` と `%LOCALAPPDATA%\pmx-editor-mcp` を消す。
4. **クライアントを起こし直す。** 登録を消しただけでは、動いているクライアントからツールは消えない。

確かめる: `_plugin\User\PmxEditorMcp.dll` と `%LOCALAPPDATA%\pmx-editor-mcp` が無く、そのクライアントの
登録の一覧に `pmx-editor-mcp` が無く、起こし直したクライアントに `pmx-editor-mcp` のツールが無い。

## うまくいかないとき

### 待受が0、または待受が在るのに `BRIDGE_NO_HOST`

待受が0のときは、エディタのプラグインメニュー「PMX Editor MCP」を開き、停止していれば開始を選ぶ。
メニューに項目が無いなら、`_plugin\User\PmxEditorMcp.dll` が在るかを見る。無ければ
[ホストを配置する](#ホストを配置する)からやり直す。在るならブロック属性を解除してエディタを起動し
直す。

待受が在るのに `BRIDGE_NO_HOST` が返るときは、`_plugin\User\PmxEditorMcp.dll` のハッシュが導入先の
`PmxEditorMcp.dll` と一致するかを見る。違えば[ホストを配置する](#ホストを配置する)からやり直す。
一致するなら[エディタを終了する](#エディタを終了する)を行って起動し直す。

### ツールが出てこない

そのクライアントの登録に `pmx-editor-mcp` が在り、コマンドが導入先の `PmxEditorMcp.Bridge.exe` を
指すかを見る。値が違うか登録が無いなら[登録する](#登録する)。合っているならクライアントを完全に
終了して起動し直す。

### ファイルを上書き・削除できない

[止める](#止める)を行う。`_plugin\User` が `C:\Program Files` の下にあるなら、管理者権限で実行し
直す。

### 入っているバージョンを知る

`%LOCALAPPDATA%\pmx-editor-mcp\PmxEditorMcp.Bridge.exe` のファイルバージョンを見る。
