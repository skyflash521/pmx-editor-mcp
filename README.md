# pmx-editor-mcp

PMXエディタをMCP経由で操作可能にするプラグイン。実体は2つのプロセスに分かれる。

| プロセス | 役割 |
|---|---|
| ホスト | エディタへ読み込ませるプラグイン。エディタのプロセス内に常駐して待ち受ける |
| ブリッジ | MCPクライアントからの要求を受け、ホストへ中継する外部プロセス |

置き場は次のとおり。

| 対象 | 置き場 |
|---|---|
| 仕様書 | [docs/specs/](docs/specs/) |
| 規約 | [docs/conventions/](docs/conventions/) |
| 検査と生成が読むデータ | [data/](data/) |

## 開発環境

### 必要なもの

| 項目 | 条件 |
|---|---|
| .NET SDK | 10 以上。ブリッジが net10.0 を対象にする。net48 の参照アセンブリは `Microsoft.NETFramework.ReferenceAssemblies` で解決するので Developer Pack は要らない |
| Node.js | 22以上。確認クライアントの実行に用いる |
| PowerShell | `pwsh` 7.6以上。Windows標準の `powershell.exe` は別物で、スクリプトはこれでは動かない |
| lychee | 文書のリンク検査に用いる。`winget install lycheeverse.lychee` |
| OS | Windows x64。表示言語は日本語([操作役のスクリプト](scripts/host-control.ps1)がメニューの文言と確認ボタンの表示名を手がかりにする) |
| PMXエディタ | 各自が導入したx64版の配布物。操作の対象は `PmxEditor_x64.exe` |
| セッション | ログオンした対話的なデスクトップ。実機に触る検査はエディタの画面を操作する |

### 構築手順

1. リポジトリを取得する。
2. PMXエディタ配布物を用意する。置き場所は任意でよい。
3. リポジトリ直下に `local.props` を作り、配布物の場所を定義する。Git管理外で、ホストのビルドは
   この定義が無いと明示エラーで止まる。ビルドの配置先も操作役スクリプトの起動先もここを読む。

   ```xml
   <Project>
     <PropertyGroup>
       <PmxEditorDir>C:\path\to\PmxEditor</PmxEditorDir>
     </PropertyGroup>
   </Project>
   ```

   定義は1つだけにし、条件付きにしない。操作役スクリプトはMSBuildの評価を再現せず拒否する。
4. ビルドが通ることを確認する。

   ```
   dotnet build PmxEditorMcp.sln -warnaserror
   ```

### 動かす

ホストをエディタの導入物へ配置する。配置先は起動中のエディタがロックしているので、動いている
エディタはこのスクリプトが先に閉じる。

```
pwsh -File scripts/deploy-host.ps1
```

ブリッジをMCPサーバーとして登録する。登録は一度だけで、以後エディタを起動し直しても登録し直さない。
パスは空白を含みうるので引用符で囲む。

```
pwsh -File scripts/publish-bridge.ps1 -Destination <発行先>
claude mcp add pmx-editor-mcp -- "<発行先の PmxEditorMcp.Bridge.exe の絶対パス>"
```

開発中は発行せず、ビルド成果物 `src/Bridge/bin/Debug/net10.0/PmxEditorMcp.Bridge.exe` を同じように
登録してよい。登録を解くのは `claude mcp remove pmx-editor-mcp`。

エディタの起動・終了、ホストの停止・開始、画面への操作は[操作役のスクリプト](scripts/host-control.ps1)が
行うので、画面を人手で操作する必要はない。受け付ける操作はそのスクリプトの冒頭が並べる。

## リポジトリ構成

| パス | 中身 |
|---|---|
| `src/HostPlugin/` | ホスト |
| `src/Bridge/` | ブリッジ |
| `src/SignatureDump/` | SDKの公開APIを列挙し、台帳と機械可読の定義群をそれへ突き合わせる実行器。検査からだけ走らせる |
| `tests/HostPlugin.Tests/`・`tests/Bridge.Tests/`・`tests/SignatureDump.Tests/` | xUnit。UIスレッドとエディタ実機に依存する部分は対象外で、実機に触る検査が担保する |
| `docs/` | 規約と仕様書 |
| `scripts/` | 検証の実行器と、それが使う補助。用途と使い方は各スクリプト冒頭のコメント |
| `PmxEditorMcp.sln` | ソリューション。リポジトリ直下のこの1本にすべてのプロジェクトを集約する |

**個々のファイルはここに列挙しない**(増やすたびに古くなる)。何があるかは `git ls-files` で分かる。

## 検証

変更を確定させる前に通す検査と合格条件は[検証手順](docs/conventions/verification.md)が定める。
