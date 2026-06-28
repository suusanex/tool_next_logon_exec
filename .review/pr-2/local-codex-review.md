# Local Codex Review

## 対象PR

- Repository: suusanex/tool_next_logon_exec
- PR: #2
- base: main
- head: codex/issue-1
- head commit: d466b4428d5a7b62e3cc796124f3814bc9449b18

## 入力資料

- `.review/pr-2/review-context.md`
- `.review/pr-2/review-context.json`
- `git diff origin/main...HEAD`
- `AGENTS.md`
- `README.md`
- `plans/issue-1-next-logon-exec*.md`

## レビュー指摘一覧

### LC-001 / High

- file/line: `src/NextLogonExec/Application.cs:95`, `src/NextLogonExec/Application.cs:123`, `src/NextLogonExec/Application.cs:126`
- summary: `schedule` の job store 更新と Task Scheduler 登録が非原子的で、登録失敗時に pending job だけ残る、または `--replace` 時に既存 job を失う。
- 根拠: `store.SavePending(job)` の後に `scheduler.RegisterNextLogonTask(...)` を呼ぶため、Task Scheduler 登録が失敗すると task のない pending job が残る。さらに `--replace` では先に `scheduler.UnregisterTask(id)` と `store.DeletePending(id)` を実行してから新規保存・登録するため、新規登録失敗時に既存の予約も消える。
- recommended fix: Task Scheduler 登録失敗時は新規 pending job を rollback する。`--replace` は既存 job を破壊する前に新規登録が成功する順序へ寄せるか、一時ファイル/backup を使って失敗時に復元する。登録失敗・復元失敗の trace と exit code も明確にする。

### LC-002 / High

- file/line: `src/NextLogonExec/Application.cs:125`, `src/NextLogonExec/Application.cs:132`, `src/NextLogonExec/Application.cs:150`, `src/NextLogonExec/Scheduling/ComTaskSchedulerClient.cs:35`
- summary: `--delay` が Task Scheduler trigger と `run` 側の `Task.Delay` で二重適用される。
- 根拠: `schedule` は `ScheduledTaskRegistration(..., options.DelaySeconds)` を渡し、COM client は `trigger.Delay` を設定する。その後 `run` でも `job.DelaySeconds` により待機するため、指定秒数の2倍遅延する。
- recommended fix: delay は Task Scheduler trigger 側だけに寄せ、`run` 側の `Task.Delay` を削除する。テストに `--delay` 指定時でも launcher 呼び出し前に追加待機しないことを追加する。

### LC-003 / High

- file/line: `src/NextLogonExec/Application.cs:44`
- summary: `Environment.ProcessPath` が null の場合、Task Scheduler の `ExecAction.Path` に実行ファイルではなくディレクトリが入る。
- 根拠: fallback が `AppContext.BaseDirectory` で、これは実行ファイルパスではなくディレクトリパス。`ComTaskSchedulerClient` はこの値を `action.Path` に設定するため、該当環境では logon trigger から起動できない。
- recommended fix: 実行可能ファイルのフルパスだけを採用する。取得できない場合は明確な `TaskSchedulerClientException` または `UnsupportedPlatformException` として失敗させ、directory fallback は使わない。

### LC-004 / Medium

- file/line: `src/NextLogonExec/Diagnostics/ResultWriter.cs:22`, `src/NextLogonExec/Jobs/JobStore.cs:81`, `src/NextLogonExec/Application.cs:70`
- summary: result/job store 系 I/O 失敗の一部が `JobStoreError` ではなく一般 catch に落ち、exit code が `TaskSchedulerError` になる。
- 根拠: `ResultWriter.Write` と `JobStore.DeletePending` は I/O / JSON / UnauthorizedAccess を `NextLogonExecException` に変換していない。`Application.RunAsync` の一般 catch は exit code `4` を返すため、README の exit code `5 Job store read/write error` と不整合になる。
- recommended fix: result 書き込みと pending 削除を `JobStoreException` などに wrap する。`PendingExists` / `HistoryExists` / `ListPendingIds` などの読み取り系も、権限エラーや列挙エラーを安定した exit code に分類する。

### LC-005 / Medium

- file/line: `src/NextLogonExec/Jobs/JobLock.cs:22`
- summary: lock 取得時の `IOException` を捨てており、AGENTS.md が求める `Exception.ToString()` の trace 詳細が失われる。
- 根拠: `JobLock.TryAcquire` は `IOException ex` を catch して `JobConflictException` に変換するが、inner exception を保持せず、その場で trace も出していない。
- recommended fix: `JobConflictException` に inner exception を渡せる constructor を追加するか、wrap 前に `Trace.TraceError(ex.ToString())` を出す。

### LC-006 / Low

- file/line: `README.md:13`, `src/NextLogonExec/Application.cs:125`
- summary: README の Task Scheduler action 例が実装と一致していない。
- 根拠: 実装は常に `run --id <id> --store-dir <resolved path>` を action arguments に含めるが、README は `NextLogonExec.exe run --id <job-id>` とだけ記載している。
- recommended fix: README の action 例に `--store-dir <resolved path>` を含める。

### LC-007 / Low

- file/line: `tests/NextLogonExec.Tests/Program.cs:65`, `src/NextLogonExec/Scheduling/WindowsArgumentEscaper.cs:13`
- summary: Task Scheduler action arguments の重要ケースにテスト不足がある。
- 根拠: テストは `run --id Case123` と caller exe が含まれないことだけを確認しており、`--store-dir` の伝播、空白を含む store path、quote/backslash の escaping、`--delay` の単一適用を検証していない。
- recommended fix: `WindowsArgumentEscaper` の直接テストと、`schedule` が `--store-dir` を含む action arguments を生成するテストを追加する。store dir に空白・末尾 backslash・quote を含むケースも入れる。

## 追加確認が必要な事項

- 実 Task Scheduler / logon smoke は plan 上 ManualOnly で、今回も実行していない。
- `review-context.json` では Copilot inline comment は expected 5 / actual 5、timeout なし。
- Checks は収集対象外だった。
- ローカルでは `git diff --check origin/main...HEAD` のみ実行し、指摘なし。
