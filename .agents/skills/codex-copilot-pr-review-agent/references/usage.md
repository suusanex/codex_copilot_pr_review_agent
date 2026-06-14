# 使い方

## 前提

- 対象リポジトリでGitHub CLIが利用できること。
- `gh auth status` が成功すること。
- File-based appsを実行できる .NET SDK が利用できること。
- 対象リポジトリの `AGENTS.md`、README、ビルド手順、テスト手順を確認すること。

## 基本フロー

1. 対象リポジトリで作業状態を確認する。
2. PRが存在しない場合は、対象リポジトリのルールに従ってPRを作成する。
3. PRレビュー文脈を収集する。
4. Codexでローカルレビューを行う。
5. `review-planner` で統合修正計画を作成する。
6. `spark-implementer` で計画範囲を実装する。
7. テスト、lint、format、型チェックを実行する。
8. 結果レポートを作成し、必要に応じてcommit/pushする。

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

## commit/push

commit/push は自動実行前提ではなく、次を確認してから行う。

- 未コミット変更に無関係な差分が混ざっていないこと。
- 関連テストが成功していること。
- 対象リポジトリの `AGENTS.md` に反していないこと。
- 人手承認が必要なリポジトリでは承認があること。

