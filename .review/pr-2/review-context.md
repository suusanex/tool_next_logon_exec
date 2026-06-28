# PR Review Context

## Target

- Repository: suusanex/tool_next_logon_exec
- PR: #2
- Title: NextLogonExec の実装計画とREADMEを更新する
- URL: https://github.com/suusanex/tool_next_logon_exec/pull/2
- State: OPEN
- Draft: False
- Review decision: 
- Base: main
- Head: codex/issue-1

## GitHub Copilot Review Wait

- Status: reviewAndInline
- Timed out: False
- Started at: 2026-06-28T11:26:49.7852933+00:00
- Completed at: 2026-06-28T11:27:02.0687583+00:00
- Elapsed seconds: 12.283
- Head commit: d466b4428d5a7b62e3cc796124f3814bc9449b18
- Timeout seconds: 180
- Poll interval seconds: 10
- Stable samples observed: 2
- Review found: True
- Review state: COMMENTED
- Review submitted at: 2026-06-28T11:26:27Z
- Expected inline comments: 5
- Actual inline comments: 5

GitHub Copilot review body and inline comments were collected.

## PR Body

## Summary
- `NextLogonExec` のREADMEを、`schedule` / `run` / `cancel` / `status` / `list` を含む現在の動作仕様に合わせて整理
- 実装計画、契約、検証、移行用の各 plan を追加して、要求・リスク・残件の流れを明確化
- 主要な実装クラスとテスト入り口を追加し、ジョブ保存・起動・結果記録・Task Scheduler 連携の骨格を用意
- PRレビューでは、`CommandLineParser` と `WindowsArgumentEscaper` の引数解釈、`JobStore` / `JobLock` の排他と永続化、`ProcessLauncher` の起動条件、`ComTaskSchedulerClient` の登録・削除・エラー処理を重点確認してほしい

## Testing
- Unit tests を追加して、コマンドライン解析、ジョブ管理、結果書き込み、起動フローの主要分岐を確認
- 実OSの Task Scheduler やログオン状態は変更せず、スタブ前提の検証に限定

## Changed Files

- README.md
- plans/issue-1-next-logon-exec-change-risk-triage.md
- plans/issue-1-next-logon-exec-implementation-contract-kernel.md
- plans/issue-1-next-logon-exec-implementation-contract-review-kernel.md
- plans/issue-1-next-logon-exec-implementation-execution.md
- plans/issue-1-next-logon-exec-implementation-handoff-review.md
- plans/issue-1-next-logon-exec-residual-decision-gate.md
- plans/issue-1-next-logon-exec-runtime-contract-kernel.md
- plans/issue-1-next-logon-exec-test-design-kernel.md
- plans/issue-1-next-logon-exec-verification-kernel.md
- plans/issue-1-next-logon-exec.md
- src/NextLogonExec/Application.cs
- src/NextLogonExec/Commands/CommandLine.cs
- src/NextLogonExec/Commands/CommandLineParser.cs
- src/NextLogonExec/Commands/JobId.cs
- src/NextLogonExec/Diagnostics/ResultWriter.cs
- src/NextLogonExec/ExitCode.cs
- src/NextLogonExec/Jobs/JobLock.cs
- src/NextLogonExec/Jobs/JobStore.cs
- src/NextLogonExec/Jobs/ScheduledJob.cs
- src/NextLogonExec/Jobs/StoreDirectory.cs
- src/NextLogonExec/NextLogonExec.csproj
- src/NextLogonExec/NextLogonExecException.cs
- src/NextLogonExec/ProcessLaunching/IProcessLauncher.cs
- src/NextLogonExec/ProcessLaunching/LaunchResult.cs
- src/NextLogonExec/ProcessLaunching/ProcessLauncher.cs
- src/NextLogonExec/Program.cs
- src/NextLogonExec/Scheduling/ComTaskSchedulerClient.cs
- src/NextLogonExec/Scheduling/ITaskSchedulerClient.cs
- src/NextLogonExec/Scheduling/WindowsArgumentEscaper.cs
- src/NextLogonExec/Security/CurrentUserProvider.cs
- src/NextLogonExec/Security/ICurrentUserProvider.cs
- tests/NextLogonExec.Tests/NextLogonExec.Tests.csproj
- tests/NextLogonExec.Tests/Program.cs

## Latest Reviews

### copilot-pull-request-reviewer / COMMENTED

- Submitted at: 2026-06-28T11:26:27Z

## Pull request overview

`NextLogonExec` の現行仕様（schedule/run/cancel/status/list）に合わせて、Windows Task Scheduler を使った「次回対話ログオンで1回だけ実行」する CLI ツールの実装骨格・スタブ前提テスト・README/plan 群を追加する PR です。

**Changes:**
- CLI（schedule/run/cancel/status/list）、job 永続化（JSON）、job lock、実行結果記録の基礎実装を追加
- Task Scheduler 2.0 COM object model による production 登録クライアントと、Task action の `run --id` 経由起動（caller command を action に入れない）を実装
- スタブ scheduler/launcher を使ったテスト入口と、README + plan/verification artifacts を追加

### Reviewed changes

Copilot reviewed 34 out of 34 changed files in this pull request and generated 5 comments.

<details>
<summary>Show a summary per file</summary>

| File | Description |
| ---- | ----------- |
| README.md | CLI 仕様・例・制限・手動確認手順の整理 |
| src/NextLogonExec/NextLogonExec.csproj | NextLogonExec 本体プロジェクト追加 |
| src/NextLogonExec/Program.cs | アプリ起動エントリポイント追加 |
| src/NextLogonExec/Application.cs | CLI ルーティング、schedule/run/cancel/status/list の実装 |
| src/NextLogonExec/ExitCode.cs | 安定した exit code 定義追加 |
| src/NextLogonExec/NextLogonExecException.cs | exit code に紐づく例外型追加 |
| src/NextLogonExec/Commands/CommandLine.cs | コマンドラインモデル追加 |
| src/NextLogonExec/Commands/CommandLineParser.cs | v1 向け CLI パーサ追加 |
| src/NextLogonExec/Commands/JobId.cs | Job ID バリデーション追加 |
| src/NextLogonExec/Jobs/StoreDirectory.cs | job store ディレクトリ解決追加 |
| src/NextLogonExec/Jobs/ScheduledJob.cs | job JSON スキーマ追加 |
| src/NextLogonExec/Jobs/JobStore.cs | pending/history の保存・読込・列挙追加 |
| src/NextLogonExec/Jobs/JobLock.cs | job 排他用ファイルロック追加 |
| src/NextLogonExec/Diagnostics/ResultWriter.cs | result JSON 書き込み追加 |
| src/NextLogonExec/ProcessLaunching/IProcessLauncher.cs | プロセス起動抽象のインターフェース追加 |
| src/NextLogonExec/ProcessLaunching/ProcessLauncher.cs | ProcessStartInfo.ArgumentList ベースの起動実装追加 |
| src/NextLogonExec/ProcessLaunching/LaunchResult.cs | 起動/終了/timeout 等の結果モデル追加 |
| src/NextLogonExec/Scheduling/ITaskSchedulerClient.cs | Task Scheduler 操作の抽象化追加 |
| src/NextLogonExec/Scheduling/ComTaskSchedulerClient.cs | COM/dynamic を境界に閉じた production 実装追加 |
| src/NextLogonExec/Scheduling/WindowsArgumentEscaper.cs | Task action arguments の Windows 向けエスケープ追加 |
| src/NextLogonExec/Security/ICurrentUserProvider.cs | 現在ユーザー取得の抽象化追加 |
| src/NextLogonExec/Security/CurrentUserProvider.cs | WindowsIdentity による production 実装追加 |
| tests/NextLogonExec.Tests/NextLogonExec.Tests.csproj | テスト用（カスタムランナー）プロジェクト追加 |
| tests/NextLogonExec.Tests/Program.cs | スタブ scheduler/launcher による主要分岐テスト追加 |
| plans/issue-1-next-logon-exec.md | Plan Kernel 追加 |
| plans/issue-1-next-logon-exec-change-risk-triage.md | Change Risk Triage 追加 |
| plans/issue-1-next-logon-exec-implementation-contract-kernel.md | Implementation Contract Kernel 追加 |
| plans/issue-1-next-logon-exec-implementation-contract-review-kernel.md | Implementation Contract Review Kernel 追加 |
| plans/issue-1-next-logon-exec-runtime-contract-kernel.md | Runtime Contract Kernel 追加 |
| plans/issue-1-next-logon-exec-test-design-kernel.md | Test Design Kernel 追加 |
| plans/issue-1-next-logon-exec-implementation-handoff-review.md | 実装引き継ぎレビュー artifact 追加 |
| plans/issue-1-next-logon-exec-implementation-execution.md | Implementation Execution Result artifact 追加 |
| plans/issue-1-next-logon-exec-verification-kernel.md | Verification Kernel 結果 artifact 追加 |
| plans/issue-1-next-logon-exec-residual-decision-gate.md | Residual Decision Gate 結果 artifact 追加 |
</details>







---

💡 <a href="/suusanex/tool_next_logon_exec/new/main?filename=.github/instructions/*.instructions.md" class="Link--inTextBlock" target="_blank" rel="noopener noreferrer">Add Copilot custom instructions</a> for smarter, more guided reviews. <a href="https://docs.github.com/en/copilot/customizing-copilot/adding-repository-custom-instructions-for-github-copilot" class="Link--inTextBlock" target="_blank" rel="noopener noreferrer">Learn how to get started</a>.


## PR Comments


## Review Comments

### Copilot / src/NextLogonExec/Application.cs:45

- URL: https://github.com/suusanex/tool_next_logon_exec/pull/2#discussion_r3487789211
- Created at: 2026-06-28T11:26:26Z

`CreateDefault` の executablePath フォールバックに `AppContext.BaseDirectory` を使うと、ディレクトリパスが Task Scheduler の ExecAction.Path に入って起動不能になります（ProcessPath が null の環境で発生）。実行可能ファイルのフルパスを確実に取得するフォールバックに変更してください。

### Copilot / src/NextLogonExec/Diagnostics/ResultWriter.cs:23

- URL: https://github.com/suusanex/tool_next_logon_exec/pull/2#discussion_r3487789222
- Created at: 2026-06-28T11:26:26Z

`ResultWriter.Write` が I/O / JSON 例外を `NextLogonExecException` にマップしていないため、失敗時に `Application` の一般 catch に落ちて exit code が `TaskSchedulerError` になり、エラー分類が誤ります。ジョブ結果ファイルの書き込み失敗は `JobStoreException`（= exit code 5）などにラップして返してください。

### Copilot / src/NextLogonExec/Jobs/JobLock.cs:25

- URL: https://github.com/suusanex/tool_next_logon_exec/pull/2#discussion_r3487789235
- Created at: 2026-06-28T11:26:26Z

`JobLock.TryAcquire` の IOException を `JobConflictException` に変換する際に inner exception を保持していないため、`Application` の Trace に元例外の詳細（HResult/stack）が出ません。ガイドライン上も例外詳細の Trace が必要なので、ここで `ex.ToString()` を Trace してからラップしてください。

### Copilot / src/NextLogonExec/Application.cs:153

- URL: https://github.com/suusanex/tool_next_logon_exec/pull/2#discussion_r3487789243
- Created at: 2026-06-28T11:26:27Z

`--delay` が二重に適用されています。`schedule` で Task Scheduler の LogonTrigger.Delay（`ComTaskSchedulerClient.RegisterNextLogonTask`）を設定しているのに、`run` 側でも `job.DelaySeconds` で `Task.Delay` しているため、指定秒数の2倍待つ挙動になります。どちらか一方に寄せてください（ここでは run 側の待機を削除）。

### Copilot / README.md:13

- URL: https://github.com/suusanex/tool_next_logon_exec/pull/2#discussion_r3487789248
- Created at: 2026-06-28T11:26:27Z

README の Task Scheduler action 例が実装と一致していません。実装では action arguments に常に `--store-dir <resolved path>` を含めるため、ここも `--store-dir` を含む形に更新してください。


## Checks

Checks were not requested.

## GitHub Copilot Review Note

GitHub Copilot related review data was found in the collected PR reviews or comments.

