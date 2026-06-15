# codex_copilot_pr_review_agent

Codexを入口として、GitHub PRの作成または取得、ローカルCodexレビュー、GitHub Copilotレビュー収集、修正計画、修正実装、検証、commit/push、結果レポート作成までを支援するAPMパッケージ。

初版MVPでは、GitHub App、Webサービス、DB、ダッシュボード、複雑な複数PR制御は扱わない。ローカルCodex、GitHub CLI、File-based appsで完結する再利用可能なワークフローを提供する。

## 提供物

- `.agents/skills/codex-copilot-pr-review-agent/SKILL.md`
  - Codexから短い依頼でPRレビュー反映ワークフローを始める入口。
- `.github/agents/local-reviewer.agent.md`
  - `model: "gpt-5.5"` を必須指定し、PRのbase/head差分と収集済み文脈からローカルCodexレビュー結果を作る読み取り専用agent。
- `.github/agents/review-planner.agent.md`
  - `model: "gpt-5.5"` を必須指定し、Codexレビュー、GitHub Copilotレビュー、PRコメント、CI状態を統合して修正計画を作る読み取り専用agent。
- `.github/agents/spark-implementer.agent.md`
  - `model: "gpt-5.3-codex-spark"` を必須指定し、`review-plan.md` の範囲だけを実装して検証、commit/push、結果レポート作成まで進めるagent。
- `scripts/collect-pr-review-context.cs`
  - GitHub CLIでPR本文、レビュー、コメント、チェック状態を収集するFile-based app。
- `templates/review-plan.md`
  - 修正計画テンプレート。
- `templates/review-result-report.md`
  - 結果レポートテンプレート。
- `scripts/install-codex-copilot-pr-review-agent-local.cs`
  - Codex向けAPM導入を実行し、`.apm/agents/*.agent.md` を原本として対象リポジトリへ `.codex/config.toml` と `.codex/agents/*.toml` を補完するインストーラ。

## 導入

インストーラを実行すると、まず次のAPMコマンドを内部で実行し、その後にCodex設定の補完を行う。

```powershell
apm install --update --target codex suusanex/codex_copilot_pr_review_agent --root <target-repo-root>
```

通常は次だけでよい。

```powershell
dotnet run --file scripts/install-codex-copilot-pr-review-agent-local.cs -- <target-repo-root>
```

別のカレントディレクトリから実行する場合は、スクリプトをフルパスで指定できる。

```powershell
dotnet run --file D:\path\to\codex_copilot_pr_review_agent\scripts\install-codex-copilot-pr-review-agent-local.cs -- <target-repo-root>
```

`--dry-run` / `--check-only` を付けると、APM dry-runとCodex設定補完予定を事前確認できる。

導入後、対象リポジトリの `AGENTS.md`、README、ビルド手順、テスト手順を優先して運用する。

## 使い方

レビュー開始前に、対象リポジトリでPRを成立させる。

1. 作業ブランチがない場合は作成する。
2. 未コミット変更がある場合は、PRに含める範囲を確認してcommitする。
3. head branchがremoteへpushされていない場合はpushする。
4. PRが存在しない場合は、merge先base branchとhead branchを確認してPRを作成する。
5. PR番号、base branch、head branchを確定してからレビュー文脈を収集する。

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

## 必須モデル指定

このパッケージのagentは、次のモデル指定を必須とする。

- `local-reviewer`: `GPT 5.5 Medium`
- `review-planner`: `GPT 5.5 Medium`
- `spark-implementer`: `GPT-5.3-Codex-Spark High`

Codex agentでは、`GPT 5.5 Medium` を `model = "gpt-5.5"` と `model_reasoning_effort = "medium"` で指定する。`GPT-5.3-Codex-Spark High` は `model = "gpt-5.3-codex-spark"` と `model_reasoning_effort = "high"` で指定する。

このリポジトリでは `.github/agents/*.agent.md` と `.apm/agents/*.agent.md` のfront matterをモデル指定の原本とする。`.codex/config.toml` と `.codex/agents/*.toml` は、インストーラが対象リポジトリへ生成・更新する配布先設定である。

標準ワークフローでは、`local-reviewer` でPRのbase/head差分だけを対象にローカルCodexレビューを作成し、`review-planner` で統合修正計画を作成し、`spark-implementer` で計画範囲を実装する。

## GitHub Copilotレビューの前提

このMVPは、GitHub上に投稿済みのPRレビュー、PRコメント、レビューコメントを読み取る。スクリプトから `@copilot` へのレビューリクエストは行わない。

- Copilot自動レビューが有効なリポジトリでは、レビュー完了を待ってから収集する。
- 自動レビューが無効な場合は、ユーザーが事前にCopilotレビューをリクエストする。
- Copilotレビューが見つからない場合は「未取得」として扱い、`review-plan.md` でローカルCodexレビューのみで進めるか、人間判断へ戻すかを明記する。

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
- レビュー前のcommit/pushは、PRを成立させるための必須準備として扱う。
- 実装修正後のcommit/pushは、未コミット変更、テスト結果、対象リポジトリのルール、上位指示を確認してから行う。
- GitHub Copilotレビューが取得できない場合は、未取得としてレポートする。

## MVP完了条件

- 別リポジトリまたはscratch rootへAPM導入できる。
- `codex-copilot-pr-review-agent` skillが展開される。
- `local-reviewer`、`review-planner`、`spark-implementer` がCodexで利用でき、インストーラ実行後の `.codex/agents/*.toml` に必須の `model` が指定される。
- skill配下の `scripts/`、`templates/`、`references/` が `SKILL.md` からの相対パスで解決できる。
- GitHub CLI前提チェックが期待通り成功または失敗する。
- 安全なテストPRでPR情報収集まで実行できる。

## ドキュメント

- [使い方](docs/usage.md)
- [設計](docs/design.md)
- [トラブルシューティング](docs/troubleshooting.md)
