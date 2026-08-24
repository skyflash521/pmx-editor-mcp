# 検証手順

変更を確定させる前に通す検査の一覧と合格条件の正本。

## 実行する検査

| 検査 | コマンド | 合格条件 |
|---|---|---|
| ビルド | `dotnet build PmxEditorMcp.sln -warnaserror` | 警告・エラー0 |
| 整形 | `dotnet format PmxEditorMcp.sln --verify-no-changes` | 差分0 |
| テスト | `dotnet test PmxEditorMcp.sln` | 全テスト成功 |
| スクリプト構文 | `node --check scripts/e2e-check.mjs` | エラー0 |
| 実機動作確認 | [実機動作確認](#実機動作確認)の手順 | 手順内の各確認が期待どおり |

- ソリューションはリポジトリ直下の [PmxEditorMcp.sln](../../PmxEditorMcp.sln)(sln形式)の1本とし、
  すべてのプロジェクトをここに集約する。
- net48 プロジェクトの参照アセンブリは `Microsoft.NETFramework.ReferenceAssemblies` 1.0.3 の
  PackageReference で解決し、.NET Framework Developer Pack の導入に依存させない。
- テストは xUnit を用いる。UIスレッドやエディタ実機に依存する部分は単体テストの対象外とし、
  [実機動作確認](#実機動作確認)で担保する。

## 必要環境

- .NET SDK: net48 のビルドと、上の検査表の各 `dotnet` コマンドが通るもの
- Node.js: 22以上。確認クライアントの実行に用いる
- PowerShell: `pwsh`。実機動作確認で用いる。Windows標準の `powershell.exe` は別物で、
  DACLの確認に使う型を持たない
- Windows x64 と、各自が導入した PMXエディタ配布物

## 実機動作確認

ホストはエディタ1つにつき名前付きパイプ `pmx-editor-mcp-<エディタのプロセスID>` を1本だけ
待受に使う。確認はこのパイプへ接続して行う。ホストは応答サイズ予算の環境変数
`PMX_EDITOR_MCP_BUDGET_CHARS` をエディタの起動時に一度だけ読むので、エディタを起動する時点で
設定していないこと(設定していると期待応答の `budgetChars` がその値になり、受理できない値なら
待受を開始しない)。以下のコマンドは `pwsh` で実行する。

1. エディタをすべて終了した状態で、デプロイコマンドを1回実行する。ビルドでDLLが作り直されて
   いる(配置先のファイルと更新時刻かサイズが異なる)と実際にコピーが走り、起動したままでは配置先が
   ロックされていて失敗する(`MSB3027`/`MSB3021`)。

   ```
   dotnet build src/HostPlugin/PmxEditorMcp.HostPlugin.csproj -t:Deploy
   ```

   配置先は `local.props` の `PmxEditorDir` が指す配布物の `_plugin\User` フォルダ。
   `_plugin` 直下にホストDLLの複製があると2つ読み込まれるため、あれば取り除く。
   `_plugin\user.path` で外部の参照先を指定している場合は、そこにも複製が無いことを確かめる。
2. `PmxEditor_x64.exe` を起動する。プラグインが起動時にロードされ、名前付きパイプの待受が始まる。
3. 確認クライアントを実行し、疎通を確認する。

   ```
   node scripts/e2e-check.mjs <エディタのプロセスID>
   ```

   書式は `<エディタのプロセスID> [--hold] [メソッド名 [params]] ...`。要求を省略すると
   `handshake` に `{"protocol":1}` を与えたものと `ping` を送る。期待する応答は
   `{"protocol":1,"hostVersion":"<ホストDLLのアセンブリバージョン>","budgetChars":100000}` と
   `"pong"` で、終了コードは0(`$LASTEXITCODE` で見る)。要求を省略したときは、確認
   クライアント自身がこの結果の中身まで確かめて合否を出すので、終了コードだけで判定してよい。
   要求を明示したときは課さない(拒まれること自体を確かめる使い方があるため、表示で確かめる)。
   `--hold` を置くと、応答を受け切ったあとも接続を保つ。対象エディタのプロセスIDは、プラグイン
   メニューの「PMX Editor MCP」を実行すると出る状態表示のパイプ名で確かめられる。状態表示は稼働中なら「停止しますか?」、
   停止済みなら「開始しますか?」の問いとして出るので、状態を読むだけのときは「いいえ」を選ぶ。
4. 次の各ケースを確認する。エディタを終了するケースのあとは、必要に応じて起動し直す。「停止済み」
   かどうかは、「停止処理中」と出たらメニューを再実行して確かめる。1つのパイプへ同時に接続
   できるのは1つだけなので、同じエディタで次のケースへ進む前に前の接続を閉じる。
   - ホストのログ `%TEMP%\pmx-editor-mcp-host-<エディタのプロセスID>.log` の
     `プラグインを起動した:` の行が、今回の起動の時刻で1回だけであること(ログはプロセスIDごとの
     ファイルへ追記するので、同じプロセスIDが再び割り当てられると前回の記録も残る)。
   - エディタを2つ起動し、それぞれのプロセスIDのパイプへ個別に接続できること。
   - `handshake` に異なる番号を与えると `-32001` を返して切断すること。
     `node scripts/e2e-check.mjs <エディタのプロセスID> handshake '{"protocol":2}'` と打つ。
     切断されたことは、確認クライアントが次を書いて終了コード0になることで分かる。
     「切断が要るエラー応答(-32001)のあと、ホストが契約どおり接続を切りました。」
   - パイプのDACLに現在ユーザーのFullControlだけがあること。`AccessControlType` が `Allow`、
     `IdentityReference` が現在ユーザー、`PipeAccessRights` が `FullControl` の規則が1件だけ
     出れば期待どおり。

     ```
     $name = "pmx-editor-mcp-<エディタのプロセスID>"
     $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", $name,
         [System.IO.Pipes.PipeAccessRights]"ReadWrite, ReadPermissions",
         [System.IO.Pipes.PipeOptions]::None,
         [System.Security.Principal.TokenImpersonationLevel]::None,
         [System.IO.HandleInheritability]::None)
     $pipe.Connect(5000)
     [System.IO.Pipes.PipesAclExtensions]::GetAccessControl($pipe).GetAccessRules(
         $true, $true, [System.Security.Principal.NTAccount])
     $pipe.Dispose()
     ```

   - エディタを終了するとパイプが消えること。`--hold` で接続を保ったまま終了しても、
     エディタがハング・クラッシュせず、確認クライアントが「ホストが接続を切りました。」で
     終了コード0になること。パイプの有無は次で見て、何も出なければ消えている。

     ```
     [System.IO.Directory]::GetFiles("\\.\pipe\") |
         Where-Object { $_ -like "*pmx-editor-mcp-<エディタのプロセスID>" }
     ```

   - プラグインメニューの「PMX Editor MCP」から停止するとパイプが消え(上と同じ方法で見る)、
     確認クライアントを実行し直すと「接続または送受信に失敗しました: 」で終了コード1になり、
     メニュー再実行の状態表示が「停止済み」へ到達すること。
   - 停止後にメニューから開始すると同じパイプ名で再び接続でき、期待する応答が返ること。
     エディタを閉じずに停止と開始を繰り返せること。
   - `--hold` で接続を保ったままメニューから停止しても、エディタがハング・クラッシュせず、
     確認クライアントが「ホストが接続を切りました。」で終了コード0になり、メニュー再実行の
     状態表示が「停止済み」へ到達すること。

## 不合格時の対応

- ビルド・整形・テスト・スクリプト構文の不合格: 修正して再実行するまで変更を確定させない。
- [実機動作確認](#実機動作確認)の不合格: ホストのログを確認し、原因を修正して手順を再実行する。
  ホストのログの所在は、エディタのプラグインメニューから「PMX Editor MCP」を実行すると表示される
  状態表示で確認できる。
