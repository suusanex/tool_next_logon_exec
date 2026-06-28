# Review Plan

## 対象PR

- Repository: suusanex/tool_next_logon_exec
- PR: #2
- Branch: base `main` / head `codex/issue-1`
- Context files:
  - `.review/pr-2/review-context.md`
  - `.review/pr-2/review-context.json`
  - `.review/pr-2/local-codex-review.md`

## 入力資料

- PR本文: `.review/pr-2/review-context.md`
- ローカルCodexレビュー: `.review/pr-2/local-codex-review.md`
- GitHub Copilotレビュー: `.review/pr-2/review-context.md`, `.review/pr-2/review-context.json`
- PRコメント: Copilot inline comments 5件
- CI/checks: `includeChecks=false` のため未収集。`review-context.md` 上も Checks were not requested.

## GitHub Copilotレビュー未取得時の判断

- Wait status: `reviewAndInline`
- Timed out: `false`
- Elapsed seconds: `12.283`
- Review found: `true`
- Expected inline comments: `5`
- Actual inline comments: `5`
- Status: 取得済み
- Reason: GitHub Copilot review body と inline comments が収集済み。timeout なし。
- Decision: ローカルCodexレビュー 7件と Copilot inline 5件を統合して進める。
- Human action: なし。

## レビュー指摘一覧

| ID | Source | Location | Summary | Decision | Reason |
| --- | --- | --- | --- | --- | --- |
| RP-001 | LC-001 | `src/NextLogonExec/Application.cs:95`, `:123`, `:126` | `schedule` の job store 更新と Task Scheduler 登録が非原子的で、登録失敗時に pending job だけ残る。また `--replace` 時に既存 job を失う可能性がある。 | Apply | 登録・永続化の整合性が崩れる High 指摘。PRの主要契約に関わる。 |
| RP-002 | LC-002, Copilot discussion_r3487789243 | `src/NextLogonExec/Application.cs:150`, `src/NextLogonExec/Scheduling/ComTaskSchedulerClient.cs:35` | `--delay` が Task Scheduler trigger と `run` 側 `Task.Delay` で二重適用される。 | Apply | 同一原因の重複指摘。実挙動が指定秒数の2倍になる。 |
| RP-003 | LC-003, Copilot discussion_r3487789211 | `src/NextLogonExec/Application.cs:44` | `Environment.ProcessPath` null 時に `AppContext.BaseDirectory` が ExecAction.Path に入り、実行不能になる。 | Apply | ディレクトリを executable path として渡すのは実行時不具合。fallbackせず明確に失敗させる。 |
| RP-004 | LC-004, Copilot discussion_r3487789222 | `src/NextLogonExec/Diagnostics/ResultWriter.cs:22`, `src/NextLogonExec/Jobs/JobStore.cs:56`, `:66`, `:81` | result/job store 系 I/O 失敗の一部が `JobStoreError` ではなく一般 catch に落ちる。 | Apply | README の exit code 契約と不整合。Copilotの ResultWriter 指摘に加え、JobStore の同種箇所も同一原因として扱う。 |
| RP-005 | LC-005, Copilot discussion_r3487789235 | `src/NextLogonExec/Jobs/JobLock.cs:22` | lock 取得時の `IOException` 詳細が trace に残らない。 | Apply | AGENTS.md の例外 trace 要件に反する。inner exception を保持する方針で対応する。 |
| RP-006 | LC-006, Copilot discussion_r3487789248 | `README.md:13` | README の Task Scheduler action 例に `--store-dir <resolved path>` がなく、実装と一致しない。 | Apply | UI/CLI変更時のREADME更新ルールにも該当する。 |
| RP-007 | LC-007 | `tests/NextLogonExec.Tests/Program.cs:65`, `src/NextLogonExec/Scheduling/WindowsArgumentEscaper.cs:13` | action arguments、store-dir伝播、escaping、delay単一適用、rollback/error mapping のテスト不足。 | Apply | RP-001からRP-006の回帰防止に必要。実OS変更なしのスタブテストに限定する。 |

## 適用計画

| Step | Review IDs | Change | Files | Validation |
| --- | --- | --- | --- | --- |
| 1 | RP-003 | `Application.CreateDefault` で `AppContext.BaseDirectory` fallback を廃止し、`Environment.ProcessPath` が実行可能ファイルパスとして使えない場合は `TaskSchedulerClientException` などの `NextLogonExecException` で明確に失敗させる。 | `src/NextLogonExec/Application.cs` | CreateDefault系の単体テストまたは既存テストで build 確認。 |
| 2 | RP-002 | delay は Task Scheduler `LogonTrigger.Delay` 側だけに寄せ、`run` 側の `Task.Delay` を削除する。 | `src/NextLogonExec/Application.cs` | `DelaySeconds > 0` の pending job を `run` しても launcher が追加待機なしで呼ばれるテストを追加。 |
| 3 | RP-004 | `ResultWriter.Write` の I/O/JSON/UnauthorizedAccess を `JobStoreException` に wrapする。`JobStore.DeletePending`, `PendingExists`, `HistoryExists`, `ListPendingIds` も job store 系例外として分類する。 | `src/NextLogonExec/Diagnostics/ResultWriter.cs`, `src/NextLogonExec/Jobs/JobStore.cs` | result path 書き込み失敗、pending削除失敗または列挙失敗の exit code が `5` になるスタブ/一時ディレクトリテスト。 |
| 4 | RP-005 | `JobConflictException` に inner exception を保持できる constructor を追加し、`JobLock.TryAcquire` の `IOException` を inner exception 付きで wrapする。 | `src/NextLogonExec/NextLogonExecException.cs`, `src/NextLogonExec/Jobs/JobLock.cs` | build と既存テスト。必要なら lock conflict テストを追加。 |
| 5 | RP-001 | `schedule` 登録失敗時に新規 pending job を rollbackする。`--replace` は既存 task/pending を先に破壊せず、既存 pending を復元できる順序にする。rollback失敗時は `Trace.TraceError(ex.ToString())` を残す。 | `src/NextLogonExec/Application.cs`, 必要最小限で `src/NextLogonExec/Jobs/JobStore.cs` | scheduler 登録失敗時に pending が残らないテスト、`--replace` 登録失敗時に旧 pending が復元されるテスト。 |
| 6 | RP-006 | README の action 例を `NextLogonExec.exe run --id <job-id> --store-dir <resolved path>` に更新する。 | `README.md` | markdown目視、`git diff --check`。 |
| 7 | RP-007 | `WindowsArgumentEscaper` の直接テスト、`schedule` action arguments に `--store-dir` が含まれるテスト、空白/quote/backslash を含む store path のテスト、delay単一適用、rollback、error mapping の回帰テストを追加する。 | `tests/NextLogonExec.Tests/Program.cs` | `dotnet run --project tests/NextLogonExec.Tests/NextLogonExec.Tests.csproj`。 |

## 非適用または保留

| Review ID | Decision | Reason | Human action |
| --- | --- | --- | --- |
| なし | - | Copilot 5件、Local Codex 7件はすべて Apply。重複は RP-002 から RP-006 に統合済み。 | なし |

## 実装境界

- In scope:
  - PRレビュー指摘に直接対応する `Application`, `JobStore`, `ResultWriter`, `JobLock`, `NextLogonExecException`, `WindowsArgumentEscaper` 周辺の最小修正。
  - README の実装不一致修正。
  - スタブ/一時ディレクトリのみを使う自動テスト追加。
- Out of scope:
  - 実 Task Scheduler service、レジストリ、サービス、ドライバ、ログオン状態、再起動を変更するテスト。
  - `--require-new-boot` の実装。
  - CLI仕様拡張、保存スキーマの大規模変更、別スケジューラ実装の追加。
  - unrelated refactor。
- Prohibited changes:
  - PRレビュー修正に無関係な production/test ファイル変更。
  - 実OS環境を変更する UnitTest / CI IntegrationTest。
  - 失敗時に黙って成功扱いする fallback。
  - 例外を握りつぶして trace しない処理。
  - 新規 reflection の導入。既存の Task Scheduler COM/dynamic 境界を広げない。

## 検証計画

- Tests:
  - `dotnet build src/NextLogonExec/NextLogonExec.csproj`
  - `dotnet run --project tests/NextLogonExec.Tests/NextLogonExec.Tests.csproj`
- Lint/format/typecheck:
  - `dotnet build` を typecheck として扱う。
  - `git diff --check`
  - repoに明示的な formatter/linter 設定が見つかった場合のみ追加実行する。
- Manual checks:
  - 実 Task Scheduler / logon smoke は自動実行しない。
  - 必要に応じて人手で README の「手動確認手順」に従う。

## Commit/Pushゲート

- Working tree:
  - `git status --short` で PRレビュー対応以外の変更が混入していないこと。
  - `.review/pr-2/review-plan.md`、修正対象 production/test/doc のみであること。
- Tests:
  - build、custom test runner、`git diff --check` が成功していること。
  - テストは実OS状態を変更していないこと。
- Target repository rules:
  - ドキュメント、commit message、PR更新文は日本語。
  - ソースコードのログ出力は英語、コメント/XMLコメントは日本語。
  - 例外詳細は `Exception.ToString()` が trace に残ること。
  - fallbackで失敗を隠さないこと。
- Human approval:
  - 実装後の commit/push は検証結果と差分を確認してから実施する。
  - CI未収集のため、push後にGitHub上の checks 確認が必要。

## 人手での作業が必要

- 人手での作業が必要: 実 Task Scheduler、ログオン、再起動を伴う smoke test を実施する場合は、Windows実機でREADMEの手順に従って確認する。
- 人手での作業が必要: push後にGitHub上の CI/checks と PR review thread の状態を確認する。
