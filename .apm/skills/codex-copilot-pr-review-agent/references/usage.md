# 使い方

## 前提

- 対象リポジトリでGitHub CLIが利用できること。
- `gh auth status` が成功すること。
- File-based appsを実行できる .NET SDK が利用できること。
- 対象リポジトリの `AGENTS.md`、README、ビルド手順、テスト手順を確認すること。

## 基本フロー

1. 対象リポジトリで作業状態を確認する。
2. PRが存在しない場合は、対象リポジトリのルールに従ってPRを作成する。
3. Copilot自動レビューが有効な場合は完了を待つ。無効な場合は、ユーザーが事前にCopilotレビューをリクエストする。
4. PRレビュー文脈を収集する。
5. `local-reviewer` でローカルCodexレビューを行う。
6. `review-planner` で統合修正計画を作成する。
7. `spark-implementer` で計画範囲を実装する。
8. テスト、lint、format、型チェックを実行する。
9. 結果レポートを作成し、必要に応じてcommit/pushする。

## 必須モデル指定

各agentは次のモデル指定を必須とする。

- `local-reviewer`: `GPT 5.5 Medium`
- `review-planner`: `GPT 5.5 Medium`
- `spark-implementer`: `GPT-5.3-Codex-Spark`

Codex agentでは、`GPT 5.5 Medium` を `model = "gpt-5.5"` と `model_reasoning_effort = "medium"` で指定する。`GPT-5.3-Codex-Spark` は `model = "gpt-5.3-codex-spark"` で指定する。

モデル指定の原本は `.github/agents/*.agent.md` と `.apm/agents/*.agent.md` のfront matterである。`.codex/config.toml` と `.codex/agents/*.toml` は、インストーラが対象リポジトリに生成・更新する。

## Copilotレビューの扱い

このMVPは、GitHub上に投稿済みのレビュー情報を収集する。スクリプトから `@copilot` へのレビューリクエストは行わない。

- Copilotレビューが取得できた場合は、ローカルCodexレビューと合わせて `review-planner` に渡す。
- Copilotレビューが見つからない場合は「未取得」として扱う。
- 「未取得」の場合、ローカルCodexレビューのみで進めるか、人間判断へ戻すかを `review-plan.md` に記録する。

## PRレビュー文脈の収集

```powershell
dotnet run --file scripts/collect-pr-review-context.cs -- --repo owner/name --pr 123 --out .review/pr-123 --include-checks
```

出力:

- `.review/pr-123/review-context.md`
- `.review/pr-123/review-context.json`

`--include-checks` を指定すると、`gh pr checks` によるチェック状態も収集する。

## Codexへの依頼例

```text
このPRをCodex/Copilotレビュー反映ワークフローで処理して。
repo: owner/name
pr: 123
out: .review/pr-123
```

## APM導入後の確認

別リポジトリまたはscratch rootへ導入した後、次を実施・確認する。

```powershell
dotnet run --file scripts/install-codex-copilot-pr-review-agent-local.cs -- <target-repo-root> --dry-run
dotnet run --file scripts/install-codex-copilot-pr-review-agent-local.cs -- <target-repo-root> --check-only
```

`--dry-run` で反映予定を、`--check-only` で不足を確認した後、通常実行を行う。

```powershell
dotnet run --file scripts/install-codex-copilot-pr-review-agent-local.cs -- <target-repo-root>
```

```powershell
apm install suusanex/codex_copilot_pr_review_agent --root <scratch> --target codex,agent-skills
```

- `<scratch>/.agents/skills/codex-copilot-pr-review-agent/SKILL.md` が存在する。
- `<scratch>/.github/agents/local-reviewer.agent.md` または `<scratch>/.apm/agents/local-reviewer.agent.md` のfront matterに `model: gpt-5.5` が存在する。
- `<scratch>/.github/agents/review-planner.agent.md` または `<scratch>/.apm/agents/review-planner.agent.md` のfront matterに `model: gpt-5.5` が存在する。
- `<scratch>/.github/agents/spark-implementer.agent.md` または `<scratch>/.apm/agents/spark-implementer.agent.md` のfront matterに `model: gpt-5.3-codex-spark` が存在する。
- インストーラ実行後、対象リポジトリの `.codex/config.toml` に `model = "gpt-5.5"` と `model_reasoning_effort = "medium"` が存在する。
- インストーラ実行後、対象リポジトリの `.codex/agents/local-reviewer.toml` に `model = "gpt-5.5"`、`model_reasoning_effort = "medium"`、`sandbox_mode = "read-only"` が存在する。
- インストーラ実行後、対象リポジトリの `.codex/agents/review-planner.toml` に `model = "gpt-5.5"`、`model_reasoning_effort = "medium"`、`sandbox_mode = "read-only"` が存在する。
- インストーラ実行後、対象リポジトリの `.codex/agents/spark-implementer.toml` に `model = "gpt-5.3-codex-spark"`、`sandbox_mode = "workspace-write"` が存在する。
- 同じskill配下に `scripts/collect-pr-review-context.cs` が存在する。
- 同じskill配下に `templates/review-plan.md` と `templates/review-result-report.md` が存在する。
- 同じskill配下に `references/usage.md`、`references/design.md`、`references/troubleshooting.md` が存在する。
- 対象リポジトリで `gh auth status` が期待通り成功または失敗する。

## commit/push

commit/push は自動実行前提ではなく、次を確認してから行う。

- 未コミット変更に無関係な差分が混ざっていないこと。
- 関連テストが成功していること。
- 対象リポジトリの `AGENTS.md` に反していないこと。
- 人手承認が必要なリポジトリでは承認があること。
