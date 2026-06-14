# codex_copilot_pr_review_agent

Codexを入口として、GitHub PRの作成または取得、ローカルCodexレビュー、GitHub Copilotレビュー収集、修正計画、修正実装、検証、commit/push、結果レポート作成までを支援するAPMパッケージ。

初版MVPでは、GitHub App、Webサービス、DB、ダッシュボード、複雑な複数PR制御は扱わない。ローカルCodex、GitHub CLI、File-based appsで完結する再利用可能なワークフローを提供する。

## 提供物

- `.agents/skills/codex-copilot-pr-review-agent/SKILL.md`
  - Codexから短い依頼でPRレビュー反映ワークフローを始める入口。
- `.github/agents/review-planner.agent.md`
  - Codexレビュー、GitHub Copilotレビュー、PRコメント、CI状態を統合して修正計画を作る読み取り専用agent。
- `.github/agents/spark-implementer.agent.md`
  - `review-plan.md` の範囲だけを実装し、検証、commit/push、結果レポート作成まで進めるagent。
- `scripts/collect-pr-review-context.cs`
  - GitHub CLIでPR本文、レビュー、コメント、チェック状態を収集するFile-based app。
- `templates/review-plan.md`
  - 修正計画テンプレート。
- `templates/review-result-report.md`
  - 結果レポートテンプレート。

## 導入

対象リポジトリへAPMで導入する。

```powershell
apm install suusanex/codex_copilot_pr_review_agent
```

導入後、対象リポジトリの `AGENTS.md`、README、ビルド手順、テスト手順を優先して運用する。

## 使い方

PRレビュー文脈を収集する。

```powershell
dotnet run --file scripts/collect-pr-review-context.cs -- --repo owner/name --pr 123 --out .review/pr-123 --include-checks
```

生成物:

- `.review/pr-123/review-context.md`
- `.review/pr-123/review-context.json`

Codexへの依頼例:

```text
このPRをCodex/Copilotレビュー反映ワークフローで処理して。
repo: owner/name
pr: 123
out: .review/pr-123
```

## 必要な環境

- GitHub CLI
- GitHub CLIで対象リポジトリを読める認証状態
- File-based appsを実行できる .NET SDK
- APM CLI

確認:

```powershell
gh auth status
dotnet --list-sdks
apm --version
```

## 安全性

- PR文脈収集は読み取り系GitHub CLI操作だけを使う。
- GitHub CLIの取得に失敗した場合、フォールバック推測は行わない。
- commit/push は、未コミット変更、テスト結果、対象リポジトリのルール、上位指示を確認してから行う。
- GitHub Copilotレビューが取得できない場合は、未取得としてレポートする。

## ドキュメント

- [使い方](docs/usage.md)
- [設計](docs/design.md)
- [トラブルシューティング](docs/troubleshooting.md)
