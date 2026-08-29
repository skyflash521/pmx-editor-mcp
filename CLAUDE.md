# CLAUDE.md

この文書はエージェント専用。人も読む情報は [README.md](README.md) へ書く。

## 会話ルール

- ユーザーとの会話、進捗報告、最終回答は日本語で行う。
- ユーザーの指示を無批判に実行しない。間違いが混入している前提で批判的に検証すること。

## 正本

| 対象 | 文書 |
|---|---|
| 構成・ライフサイクル・運用 | [アーキテクチャ仕様書](docs/specs/pmx-editor-mcp-architecture.md) |
| ホストとブリッジが交わすプロトコル | [IPC仕様書](docs/specs/pmx-editor-mcp-ipc.md) |
| PEPlugin SDK の能力一覧とツール化の可否 | [能力台帳](docs/specs/pmx-editor-mcp-capability-ledger.md) |
| 通す検査・実機動作確認 | [検証手順](docs/conventions/verification.md) |
| 開発環境と動かし方 | [README.md](README.md) |

**設計値・手順をこの文書へ書き写さない。** 用があるものだけを開く。

## 一時ファイル

使い捨てのスクリプト・ドキュメントは `.scratch/` に置き、不要になった時点で削除する。
