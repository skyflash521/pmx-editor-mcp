# 検証手順

変更を確定させる前に通す検査の一覧と合格条件の正本。

## 実行する検査

| 検査 | コマンド | 合格条件 |
|---|---|---|
| ビルド | `dotnet build PmxEditorMcp.sln -warnaserror` | 警告・エラー0 |
| 整形 | `dotnet format PmxEditorMcp.sln --verify-no-changes` | 差分0 |
| テスト | `dotnet test PmxEditorMcp.sln` | 全テスト成功 |
| スクリプト構文 | `node --check scripts/e2e-check.mjs` | エラー0 |
| スクリプト構文(PowerShell) | [PowerShellスクリプトの構文検査](#powershellスクリプトの構文検査)のコマンド | エラー0 |
| SDK公開APIの列挙 | [SDK公開APIの列挙](#sdk公開apiの列挙)のコマンド | 終了コード0 |
| 台帳と正本の照合 | [台帳と正本の照合](#台帳と正本の照合)のコマンド | 終了コード0 |
| 日本語名の照合 | [日本語名の照合](#日本語名の照合)のコマンド | 終了コード0 |
| 型役割の照合 | [型役割の照合](#型役割の照合)のコマンド | 終了コード0 |
| 実機動作確認 | [実機動作確認](#実機動作確認)の手順 | 手順内の各確認が期待どおり |
| ブリッジの実機動作確認 | [ブリッジの実機動作確認](#ブリッジの実機動作確認)の手順 | 手順内の各確認が期待どおり |

- ソリューションはリポジトリ直下の [PmxEditorMcp.sln](../../PmxEditorMcp.sln)(sln形式)の1本とし、
  すべてのプロジェクトをここに集約する。
- net48 プロジェクトの参照アセンブリは `Microsoft.NETFramework.ReferenceAssemblies` 1.0.3 の
  PackageReference で解決し、.NET Framework Developer Pack の導入に依存させない。
- テストは xUnit を用いる。UIスレッドやエディタ実機に依存する部分は単体テストの対象外とし、
  [実機動作確認](#実機動作確認)で担保する。

### PowerShellスクリプトの構文検査

`scripts/` の PowerShell スクリプトを解析して構文の誤りを見る。`pwsh` で実行する。

```
$errors = $null
Get-ChildItem scripts/*.ps1 | ForEach-Object {
    [void][System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$null, [ref]$errors)
    if ($errors) { throw ($_.Name + ": " + ($errors.Message -join "; ")) }
}
```

## SDK公開APIの列挙

PEPlugin SDK の公開APIをシグネチャ1件=1行として書き出す。ツールへの写像を確かめる後段の照合は、
この出力を母集合にする。下位コマンド `signatures` の引数は
[READMEの構築手順](../../README.md#構築手順) で定義する
`PmxEditorDir` が指す導入ディレクトリと、書き出し先のパスの2つ。導入ディレクトリのパスは
空白を含みうるので引用符で囲む。書き出し先は使い捨てなので `.scratch/` へ置く。このフォルダは
追跡していないため、取得した直後には無い。無ければ作ってから実行する。

```
src/SignatureDump/bin/Debug/net48/PmxEditorMcp.SignatureDump.exe signatures "<PmxEditorDir>" .scratch/signatures.json
```

対象アセンブリは配布物の `Lib\PEPlugin\PEPlugin.dll` で、これが参照する描画ライブラリは
`Lib\SlimDX\x64` から解決する。終了コード0は、対象アセンブリを読み込んで列挙まで通ったことを表す。
描画ライブラリを解決できなければ列挙が失敗して終了コード3になるので、この検査は依存の解決も
併せて確かめる。ただし、その描画ライブラリを実行環境の別の場所(実行ファイルの隣や共有の
格納先)から解決できる環境では、導入ディレクトリを探す経路を通らずに0になりうる。書き出しに
失敗した場合は終了コード4になる。

## 除外の凍結

能力台帳がすでに非対応と記していた能力と、その能力が指す公開シグネチャの集合を
[凍結した除外の正本](../specs/pmx-editor-mcp-excluded-baseline.json)へ書き出す。この正本は
提供対象から除くシグネチャの一覧を整備するときの根拠になる。下位コマンド `excluded-baseline` の
引数は導入ディレクトリ・能力台帳のパス・書き出し先のパスの3つ。

```
src/SignatureDump/bin/Debug/net48/PmxEditorMcp.SignatureDump.exe excluded-baseline "<PmxEditorDir>" docs/specs/pmx-editor-mcp-capability-ledger.md docs/specs/pmx-editor-mcp-excluded-baseline.json
```

台帳とその時点のSDKの公開シグネチャの両方を読んで確定するので、どちらかが欠けても読めなくても
読み解けなくても書き出さず終了コード3になる。読み解けたうえで台帳の記載と列挙結果が食い違う
ときは終了コード5になる。

**このコマンドは常設の検査に入れない。** 書き出し先は追跡する正本で、実行すると上書きする。
型や名前空間でまとめて指す記載は、その時点のSDKにある該当メンバーをすべて取り込むので、別の版の
SDKで実行し直すと、凍結した時点には無かったシグネチャまで正本へ入りうる。凍結は一覧を整備する前の
時点を固定するためのものなので、取得は一度きりとし、取り直すのは台帳の記載を直したときだけに
する。取り直したときは、差分の行から台帳とSDKのどちらが動いたのかを確かめる。

## 除外一覧の書き出し

提供対象から除く公開シグネチャを
[除外一覧の正本](../specs/pmx-editor-mcp-excluded-signatures.json)へ書き出す。生成側も対応表側も
この一覧だけを見るので、除外の判断が二重にならない。下位コマンド `excluded-signatures` の引数は
導入ディレクトリ・ベースライン正本のパス・書き出し先のパスの3つ。

```
src/SignatureDump/bin/Debug/net48/PmxEditorMcp.SignatureDump.exe excluded-signatures "<PmxEditorDir>" docs/specs/pmx-editor-mcp-excluded-baseline.json docs/specs/pmx-editor-mcp-excluded-signatures.json
```

凍結した組とその時点のSDKの公開シグネチャの両方を読んで確定するので、どちらかが欠けても読めなくても
読み解けなくても書き出さず終了コード3になる。凍結した組の行キーが列挙に無いときは終了コード5になる。
ベースライン正本に無いStreamシグネチャを見つけたときも同じく終了コード5で、これは形式が一次資料でしか
決まらず機械では除外の可否を判断できないという合図なので、止まった行キーを見てユーザーへ諮る。

**このコマンドは常設の検査に入れない。** 書き出し先は追跡する正本で、実行すると上書きする。
SDKの公開シグネチャが変わると結果も変わりうるので、取り直すのはベースライン正本かSDKを意図して更新した
ときだけにする。取り直したときは、差分の行からどちらが動いたのかを確かめる。

## 台帳と正本の照合

SDKの公開型と公開シグネチャが、能力台帳が指す集合か
[対象外一覧の正本](../specs/pmx-editor-mcp-ledger-out-of-scope.json)のどちらかに過不足なく
現れることと、[除外一覧の正本](../specs/pmx-editor-mcp-excluded-signatures.json)が算出した期待
集合と一致することを照合する。下位コマンド `ledger-coverage` の引数は導入ディレクトリ・能力台帳・
ベースライン正本・除外一覧・対象外一覧のパスの5つ。

```
src/SignatureDump/bin/Debug/net48/PmxEditorMcp.SignatureDump.exe ledger-coverage "<PmxEditorDir>" docs/specs/pmx-editor-mcp-capability-ledger.md docs/specs/pmx-editor-mcp-excluded-baseline.json docs/specs/pmx-editor-mcp-excluded-signatures.json docs/specs/pmx-editor-mcp-ledger-out-of-scope.json
```

**このコマンドはファイルを書き出さないので常設の検査に入れる。** 入力のどれかが欠けても読めなくても
読み解けなくても終了コード3、照合が合わなければ終了コード5になる。合わなかったときは、どの集合の
どの識別子が余ったか足りないかが標準エラー出力に出る。

台帳へ能力を足したとき、SDKを更新したとき、除外一覧や対象外一覧を取り直したときは、この照合が
通ることで台帳と正本が公開APIを覆い切っていることを確かめる。

## 日本語名の照合

[日本語名の正本](../specs/pmx-editor-mcp-property-names.json)が、
[日本語名仕様書](../specs/pmx-editor-mcp-property-names.md)の規則どおりに付いていることを照合する。
下位コマンド `property-names` の引数は導入ディレクトリ・能力台帳・除外一覧・日本語名の正本のパスの
4つ。ドキュメントXMLと根拠の資料は導入ディレクトリからの相対で解決する。

```
src/SignatureDump/bin/Debug/net48/PmxEditorMcp.SignatureDump.exe property-names "<PmxEditorDir>" docs/specs/pmx-editor-mcp-capability-ledger.md docs/specs/pmx-editor-mcp-excluded-signatures.json docs/specs/pmx-editor-mcp-property-names.json
```

**このコマンドはファイルを書き出さないので常設の検査に入れる。** 入力のどれかが欠けても読めなくても
読み解けなくても終了コード3、規則に合わなければ終了コード5になる。台帳とSDKが食い違って母集合を
決められないときも終了コード5になる。合わなかったときは、どちらで止まったかと、どの項目がどの条件に
反したかが標準エラー出力に出る。

## 型役割の照合

[型役割表の正本](../specs/pmx-editor-mcp-type-roles.json)が、
[型役割仕様書](../specs/pmx-editor-mcp-type-roles.md)の規則どおりに割り当てられていることを照合する。
下位コマンド `type-roles` の引数は導入ディレクトリ・能力台帳・除外一覧・型役割表の正本のパスの4つ。

```
src/SignatureDump/bin/Debug/net48/PmxEditorMcp.SignatureDump.exe type-roles "<PmxEditorDir>" docs/specs/pmx-editor-mcp-capability-ledger.md docs/specs/pmx-editor-mcp-excluded-signatures.json docs/specs/pmx-editor-mcp-type-roles.json
```

**このコマンドはファイルを書き出さないので常設の検査に入れる。** 入力のどれかが欠けても読めなくても
読み解けなくても終了コード3、規則に合わなければ終了コード5になる。役割の根拠を決められないときも
終了コード5で、これに当たるのは台帳とSDKが食い違って母集合を決められないとき、接続の根がSDKの列挙に
無いとき、接続の経路の一つの段で名前から一つの先を選べないとき、引数の型を取り出せないハンドラの
イベントが在るときである。合わなかったときは、どちらで止まったかと、どの型がどの条件に反したかが
標準エラー出力に出る。

## 必要環境

[READMEの開発環境](../../README.md#開発環境)が正本。検査も実機動作確認も、そこが挙げる環境が
揃っていることを前提にする。

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

   配置先は [READMEの構築手順](../../README.md#構築手順) で定義する `PmxEditorDir` が指す
   配布物の `_plugin\User` フォルダ。
   `_plugin` 直下にホストDLLの複製があると2つ読み込まれるため、あれば取り除く。
   `_plugin\user.path` で外部の参照先を指定している場合は、そこにも複製が無いことを確かめる。
2. エディタを起動する([エディタとホストの操作](#エディタとホストの操作)の `launch`)。
   プラグインが起動時にロードされ、名前付きパイプの待受が始まる。
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
   `--hold` を置くと、応答を受け切ったあとも接続を保つ。対象エディタのプロセスIDは `launch` の
   戻り値か、[エディタとホストの操作](#エディタとホストの操作)の `status` が表示するパイプ名で
   確かめられる。
4. 次の各ケースを確認する。エディタを終了するケースのあとは、必要に応じて起動し直す。1つのパイプ
   へ同時に接続できるのは1つだけなので、同じエディタで次のケースへ進む前に前の接続を閉じる。
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

   - エディタを終了するとパイプが消えること(`close` が待受の消失を待って戻る)。`--hold` で
     接続を保ったまま終了しても、エディタがハング・クラッシュせず、確認クライアントが
     「ホストが接続を切りました。」で終了コード0になること。パイプの有無は `pipes` で見る。
   - `stop` でパイプが消え、確認クライアントを実行し直すと
     「接続または送受信に失敗しました: 」で終了コード1になり、`status` の状態区分が
     「停止済み」であること。
   - 停止後に `start` すると同じパイプ名で再び接続でき、期待する応答が返ること。
     エディタを閉じずに停止と開始を繰り返せること。
   - `--hold` で接続を保ったまま `stop` しても、エディタがハング・クラッシュせず、
     確認クライアントが「ホストが接続を切りました。」で終了コード0になり、`status` の
     状態区分が「停止済み」であること。

## ブリッジの実機動作確認

ブリッジはMCPサーバーとしてClaude Codeから起動されるので、確認もClaude Code越しに行う。登録は
一度だけで、以後エディタを起動し直しても登録し直さない。

1. ブリッジをMCPサーバーとして登録する。パスは空白を含みうるので引用符で囲む。

   ```
   dotnet publish src/Bridge/PmxEditorMcp.Bridge.csproj
   claude mcp add pmx-editor-mcp -- "<発行先の PmxEditorMcp.Bridge.exe の絶対パス>"
   ```

   開発中は発行せず、ビルド成果物
   `src/Bridge/bin/Debug/net10.0/PmxEditorMcp.Bridge.exe` を同じように登録してよい。
   確認する内容は同じで、どちらの実行ファイルを指しても手順2以降は変わらない。
   登録を解くのは `claude mcp remove pmx-editor-mcp`。

2. 次の順で `ping` ツールを呼び、返る本文を見る。手順に出てくるエディタとホストの操作は
   [エディタとホストの操作](#エディタとホストの操作)で行う。状態が落ち着くのを待つのは
   その操作の側なので、手順の側で待ち時間を置く必要はない。

   **ブリッジは成功した接続を保ち、接続先を決め直すのは未接続のときだけである。** 繋いでいる
   相手が生きている限り、何回呼んでも同じ相手へ送られる。したがって接続先の決まり方を確かめる
   には、その前に接続を切る操作(繋いでいるホストをプラグインメニューから停止する、または
   そのエディタを終了する)を挟み、`ping` を1回呼んで切断を検出させる必要がある。その1回は
   `BRIDGE_CONNECTION_LOST:` で始まる本文になる。以下はこの性質を織り込んだ操作列で、
   接続を保っていない状態(セッションを開始した直後)から始める。

   1. エディタを1つも起動していない状態で `ping`。期待: `BRIDGE_NO_EDITOR:` で始まり、
      エディタの起動を促す。
   2. エディタを1つ起動して `ping`。期待:
      `接続先: pmx-editor-mcp-<そのエディタのプロセスID>` の行に続いて `pong`。ここで接続が
      確立する。
   3. そのホストを停止して `ping` を2回。期待: 一度目は
      `BRIDGE_CONNECTION_LOST:`、二度目は `BRIDGE_NO_HOST:` で始まり、プラグインメニューでの
      稼働状態の確認を促す。
   4. そのホストを開始して `ping`。期待: 手順2と同じパイプ名を名乗って `pong`(接続は手順3で
      捨てられているので、切断検出の1回は要らない)。
   5. 2つ目のエディタを起動する。この時点では1つ目への接続が生きているので、1つ目のホストを
      停止してから開始し、接続を切ったうえで両方が待ち受けている状態に戻す。`ping` を2回。
      期待: 一度目は `BRIDGE_CONNECTION_LOST:`、二度目は `BRIDGE_MULTIPLE_HOSTS:` で始まり、
      候補のパイプ名がプロセスIDの昇順で並ぶ。
   6. 1つ目のホストを停止して `ping`。期待:
      2つ目のパイプ名を名乗って `pong` を返し、先頭行が
      `接続先が変わった: <1つ目のパイプ名> から <2つ目のパイプ名> へ。` で始まる(手順5で
      接続は捨てられているので、切断検出の1回は要らない)。
   7. 2つ目のエディタを終了してから起動し直し(プロセスIDが変わる)、`ping` を2回。期待:
      一度目は `BRIDGE_CONNECTION_LOST:`、二度目が新しいプロセスIDのパイプ名を名乗って `pong`
      を返す。この間 `claude mcp` の操作は何も行わない。

### エディタとホストの操作

手順に出てくるエディタの起動・終了と、プラグインメニューからの稼働状態の確認・停止・開始は
[操作役のスクリプト](../../scripts/host-control.ps1)で行う。`pwsh` で実行する。

```
pwsh -File scripts/host-control.ps1 -Action pipes
pwsh -File scripts/host-control.ps1 -Action launch
pwsh -File scripts/host-control.ps1 -Action status -ProcessId <エディタのプロセスID>
pwsh -File scripts/host-control.ps1 -Action stop   -ProcessId <エディタのプロセスID>
pwsh -File scripts/host-control.ps1 -Action start  -ProcessId <エディタのプロセスID>
pwsh -File scripts/host-control.ps1 -Action close  -ProcessId <エディタのプロセスID>
```

`pipes` は待ち受けているホストのパイプ名を一覧する。`launch` は起動したエディタのプロセスIDを
返す。`status`・`stop`・`start` は稼働状態の本文(状態区分・パイプ名・接続・応答サイズ予算・
ログの所在)を表示してから操作する。

**状態を変える操作は、その結果が観測できるようになるまで待ってから戻る。** `start` と `launch`
は待受のパイプが現れるまで、`stop` と `close` はそれが消えるまで待つ。待受の公開も停止処理も
エディタ側の別スレッドで進むので、待たずに次の呼び出しへ進むと、状態が落ち着く前の一瞬を見て
期待と違う結果になる。この待ちはスクリプトが行うので、手順の側で待ち時間を置く必要はない。

## 不合格時の対応

- ビルド・整形・テスト・スクリプト構文の不合格: 修正して再実行するまで変更を確定させない。
- [実機動作確認](#実機動作確認)の不合格: ホストのログを確認し、原因を修正して手順を再実行する。
  ホストのログの所在は、[エディタとホストの操作](#エディタとホストの操作)の `status` が表示する
  状態表示で確認できる。
- [ブリッジの実機動作確認](#ブリッジの実機動作確認)の不合格: 原因を修正したうえで、全エディタを
  終了し、登録は変えずにClaude Codeのセッションを開始し直してから操作列の先頭へ戻る。**セッションを
  開始し直すのは、ブリッジが保っている接続を確実に捨てさせるためである**——操作列は未接続の状態を
  開始条件にしているので、接続が残っていると先頭の呼び出しが切断の検出になって開始条件が崩れる。
