---
name: codex-copilot-pr-review-agent
description: >
  Codexを入口として、GitHub PRを作成または取得し、ローカルCodexレビューとGitHub Copilotレビューを収集し、
  統合修正計画、実装、検証、commit/push、結果レポートまで進める再利用ワークフロー。
argument-hint: "[repo owner/name] [PR number or current branch] [任意: 出力ディレクトリ]"
user-invokable: true
disable-model-invocation: false
# Copyright (c) 2026 suusanex (GitHub UserName)
# SPDX-License-Identifier: CC-BY-4.0
# License: https://creativecommons.org/licenses/by/4.0/
# Source: https://github.com/suusanex/codex_copilot_pr_review_agent
---

# Codex/Copilot PR Review Agent Skill

## 目的

このスキルは、対象リポジトリのPRに対して、CodexレビューとGitHub Copilotレビューをまとめて扱い、修正計画から実装・検証・push・結果報告までを安全に進めるための入口です。

初版MVPでは、GitHub App、Webサービス、DB、ダッシュボード、複雑な複数PR制御は扱いません。ローカルCodex、GitHub CLI、File-based appsで完結する運用を前提にします。

## 前提

- 対象リポジトリで `gh auth status` が成功すること。
- 対象ブランチがGitHubへpush可能であること。
- 対象リポジトリの `AGENTS.md`、README、ビルド手順、テスト手順を必ず優先すること。
- このパッケージは対象リポジトリ固有のビルド手順を固定しないこと。

## 標準ワークフロー

1. 対象リポジトリ、ブランチ、PR番号、未コミット変更の有無を確認する。
2. PRがない場合は、対象リポジトリのルールに従ってPRを作成または作成手順を確認する。
3. `scripts/collect-pr-review-context.cs` を使い、PR本文、レビュー、コメント、必要に応じてチェック状態を収集する。
4. ローカルCodexレビューを実施し、収集結果と合わせて `review-planner` に渡す。
5. `review-planner` はファイルを変更せず、適用可否、重複コメント、修正順序、検証方針を含む `review-plan.md` を作成する。
6. `spark-implementer` は `review-plan.md` の範囲だけを実装する。
7. 対象リポジトリの関連テスト、lint、format、型チェックを可能な範囲で実行する。
8. 変更内容、検証結果、人手で必要な作業を `review-result-report.md` にまとめる。
9. commit/push は、未コミット変更、テスト結果、対象リポジトリのルール、上位指示を確認してから実施する。

## 補助CLI

```powershell
dotnet run --file scripts/collect-pr-review-context.cs -- --repo owner/name --pr 123 --out .review/pr-123 --include-checks
```

生成物:

- `review-context.md`
- `review-context.json`

## 同梱ファイル

APMパッケージとして導入された場合、このスキル配下にも同じ補助ファイルを同梱します。

- `scripts/collect-pr-review-context.cs`
- `templates/review-plan.md`
- `templates/review-result-report.md`
- `references/usage.md`
- `references/design.md`
- `references/troubleshooting.md`

## 安全ルール

- 収集処理は読み取り専用のGitHub CLI操作だけを使う。
- GitHub CLIの取得に失敗した場合、フォールバックで別情報を推測しない。
- commit/push は実装・検証後の明示ゲートとして扱う。
- レビューコメントを適用しない場合は、理由を `review-plan.md` または `review-result-report.md` に記録する。
- 人手の操作が必要な場合は、「人手での作業が必要: ...」として明記する。
