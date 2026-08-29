# pmx-editor-mcp

PMXエディタをMCP経由で操作可能にするプラグイン。実体は2つのプロセスに分かれる。

| プロセス | 役割 |
|---|---|
| ホスト | エディタへ読み込ませるプラグイン。エディタのプロセス内に常駐して待ち受ける |
| ブリッジ | MCPクライアントからの要求を受け、ホストへ中継する外部プロセス |

設計の正本は次の3書。

| 対象 | 文書 |
|---|---|
| 構成・ライフサイクル・運用 | [アーキテクチャ仕様書](docs/specs/pmx-editor-mcp-architecture.md) |
| 両者が交わすプロトコル | [IPC仕様書](docs/specs/pmx-editor-mcp-ipc.md) |
| PEPlugin SDK の能力一覧とツール化の可否 | [能力台帳](docs/specs/pmx-editor-mcp-capability-ledger.md) |

## 開発環境

### 必要なもの

| 項目 | 条件 |
|---|---|
| .NET SDK | 10 以上。ブリッジが net10.0 を対象にする。net48 の参照アセンブリは `Microsoft.NETFramework.ReferenceAssemblies` で解決するので Developer Pack は要らない |
| Node.js | 22以上。確認クライアントの実行に用いる |
| PowerShell | `pwsh`。Windows標準の `powershell.exe` は別物で、DACLの確認に使う型を持たない |
| Claude Code CLI | ブリッジをMCPサーバーとして登録し、そこから呼び出して確認する |
| OS | Windows x64。表示言語は日本語([操作役のスクリプト](scripts/host-control.ps1)がメニューの文言と確認ボタンの表示名を手がかりにする) |
| PMXエディタ | 各自が導入したx64版の配布物。操作の対象は `PmxEditor_x64.exe` |
| セッション | ログオンした対話的なデスクトップ。実機動作確認はエディタの画面を操作して進める |

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

| 操作 | 正本 |
|---|---|
| ホストをエディタへ配置する | [実機動作確認](docs/conventions/verification.md#実機動作確認)の手順1 |
| ブリッジをMCPサーバーとして登録する | [ブリッジの実機動作確認](docs/conventions/verification.md#ブリッジの実機動作確認)の手順1 |
| エディタの起動・終了、ホストの停止・開始 | [エディタとホストの操作](docs/conventions/verification.md#エディタとホストの操作) |

登録は一度だけで、以後エディタを起動し直しても登録し直さない。エディタの操作は
[操作役のスクリプト](scripts/host-control.ps1)が行うので、画面を人手で操作する必要はない。

## リポジトリ構成

| パス | 中身 |
|---|---|
| `src/HostPlugin/` | ホスト |
| `src/Bridge/` | ブリッジ |
| `tests/HostPlugin.Tests/`・`tests/Bridge.Tests/` | xUnit。UIスレッドとエディタ実機に依存する部分は対象外で、実機動作確認が担保する |
| `docs/specs/` | 仕様の正本 |
| `docs/conventions/` | 規約 |
| `scripts/` | 実機動作確認で使う補助。用途と使い方は各スクリプト冒頭のコメントと検証手順 |
| `PmxEditorMcp.sln` | ソリューション。リポジトリ直下のこの1本にすべてのプロジェクトを集約する |

**個々のファイルはここに列挙しない**(増やすたびに古くなる)。何があるかは `git ls-files` で分かる。

## 検証

変更を確定させる前に通す検査と合格条件、実機動作確認の手順は
[検証手順](docs/conventions/verification.md)が正本。
