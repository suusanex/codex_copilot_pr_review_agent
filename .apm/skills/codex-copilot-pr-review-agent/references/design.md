# 設計

## 目的

このパッケージは、実装完了後に繰り返し発生するPRレビュー反映作業を、Codexを入口に再利用可能なワークフローとして提供する。

対象は、ローカルCodexレビュー、GitHub Copilotレビュー、PRコメント、CI状態を統合し、修正計画、実装、検証、commit/push、結果レポートまで進めるMVPである。

## 構成

- `.agents/skills/codex-copilot-pr-review-agent/SKILL.md`
  - 利用者向け入口ワークフロー。
- `.github/agents/review-planner.agent.md`
  - 読み取り専用のレビュー統合計画agent。
- `.github/agents/spark-implementer.agent.md`
  - 計画範囲だけを実装するagent。
- `scripts/collect-pr-review-context.cs`
  - GitHub CLIからPR文脈を収集するFile-based app。
- `templates/review-plan.md`
  - 修正計画テンプレート。
- `templates/review-result-report.md`
  - 結果レポートテンプレート。

## データフロー

1. GitHub CLIでPR本文、レビュー、コメント、必要に応じてチェック状態を取得する。
2. `review-context.md` と `review-context.json` を生成する。
3. Codexローカルレビュー結果と収集結果を `review-planner` に渡す。
4. `review-planner` が `review-plan.md` を作成する。
5. `spark-implementer` が計画範囲を実装する。
6. 検証結果とcommit/push結果を `review-result-report.md` に記録する。

## 安全性

- 収集CLIは読み取り系GitHub CLI操作だけを実行する。
- 収集CLIはGitHub上に投稿済みのレビュー情報だけを読む。`@copilot` へのレビューリクエストは行わない。
- 取得失敗時にフォールバック推測を行わない。
- 実装agentは `review-plan.md` の範囲外を変更しない。
- commit/push は検証後のゲートとして扱う。

## MVP完了条件

- 別リポジトリまたはscratch rootへAPM導入できる。
- `codex-copilot-pr-review-agent` skillが展開される。
- skill配下の `scripts/`、`templates/`、`references/` が `SKILL.md` からの相対パスで解決できる。
- GitHub CLI前提チェックが期待通り成功または失敗する。
- 安全なテストPRでPR本文、レビュー、コメントの収集まで実行できる。
- Copilotレビューが未取得の場合は、`review-plan.md` に未取得時の判断が記録される。

## 初版で扱わないもの

- GitHub App開発
- Webサービス化
- DB利用
- 組織全体展開機能
- ダッシュボード
- 複雑なマルチPR制御
- `@copilot` レビューリクエストの自動送信

## 独自実装の理由

PR文脈収集はGitHub CLIで取得できる情報の整形が主目的であり、BCLの `System.Diagnostics.Process` と `System.Text.Json` で十分に実装できる。追加のOSSは導入せず、配布時の依存と認証面を単純に保つ。
