# Implementation Contract Kernel

## スコープ

issue #1 の bounded v1 実装について、Task Scheduler COM API、job store、CLI routing、process launch の production path を明示する。

## Plan が要求する実装要件

| Requirement | Expected by Plan | Evidence found | Status |
| --- | --- | --- | --- |
| Task Scheduler 2.0 object model | `Schedule.Service` と Task Scheduler COM object model を主経路にする | Microsoft official references in issue #1 | Confirmed |
| Task action isolation | Task action は `NextLogonExec run --id <job-id>` のみを起動する | FR-002 / AC-003 | Confirmed |
| Job persistence | caller command は JSON job に保存する | FR-001 / FR-004 | Confirmed |
| One-shot consume | `run` は task を先に削除し、lock で重複起動を避ける | FR-004 / AC-006 / AC-007 | Confirmed |
| Process launch | 保存済み `fileName` と `arguments` を `ProcessStartInfo.ArgumentList` へ渡す | FR-004 / AC-008 | Confirmed |
| OS-changing tests avoidance | Task Scheduler と process launch は interface 経由で stub 化する | AGENTS.md | Confirmed |

## Dependency と API surface の確認結果

| Dependency / API / symbol | Expected source | Found location | Status | Notes |
| --- | --- | --- | --- | --- |
| .NET SDK / C# | repo policy | local `dotnet --info` | Confirmed | net10.0-windows を使用する。 |
| Task Scheduler COM `Schedule.Service` | Windows COM API | production `ComTaskSchedulerClient` | Confirmed | COM 境界だけに隔離する。 |
| JSON serializer | BCL | `System.Text.Json` | Confirmed | 外部 package 不要。 |
| CLI parser | BCL/custom | local parser | Confirmed | `System.CommandLine` は dependency friction を避けるため採用しない。 |
| Process launch | BCL | `ProcessStartInfo.ArgumentList` | Confirmed | shell wrapping はしない。 |

## 選択した実装アプローチ

- `src/NextLogonExec` に console app を作成する。
- `ITaskSchedulerClient` に Task Scheduler 操作を閉じ込め、production 実装は `ComTaskSchedulerClient` とする。
- `ComTaskSchedulerClient` は Task Scheduler COM object model を直接使う。COM 型付き interop 定義を手書きすると interface 順序と optional VARIANT の保守リスクが高いため、COM 境界内に限って `dynamic` と COM activation を使う。
- CLI parser は小さく実装し、`--` 以降を caller command としてそのまま保存する。
- tests は custom test runner と stub scheduler/launcher を使い、実 OS の Task Scheduler を変更しない。

## 必要なコード変更

- `src/NextLogonExec/NextLogonExec.csproj`
- `src/NextLogonExec/Program.cs`
- `src/NextLogonExec/Commands/*`
- `src/NextLogonExec/Scheduling/*`
- `src/NextLogonExec/Jobs/*`
- `src/NextLogonExec/ProcessLaunching/*`
- `src/NextLogonExec/Diagnostics/*`
- `tests/NextLogonExec.Tests/*`
- `README.md`

## 禁止される代替実装

| Similar existing path | Why it is not sufficient | Allowed reuse, if any |
| --- | --- | --- |
| `schtasks.exe` subprocess | quoting, stdout/stderr parsing, CLI limitation が issue の非推奨事項 | diagnostics/manual inspection only |
| hand-authored XML primary path | XML brittleness を避けるため object model が指定されている | future export-xml only |
| caller command を Task Scheduler action に直接設定 | job file による quoting 回避と future diagnostics を壊す | none |
| implicit `cmd.exe /c` wrapping | caller が shell を明示する設計に反する | caller-specified `cmd.exe` only |

## 検証フック

- schedule parser と fake scheduler により action path/arguments が `run --id` になることを確認する。
- fake launcher により `ProcessStartInfo.ArgumentList` が保存済み arguments を保持することを確認する。
- job store の pending/history/lock behavior を temp directory で確認する。
- production source inspection で `ComTaskSchedulerClient` が `RegisterTaskDefinition`、`LogonTrigger`、`ExecAction` を使うことを確認する。

## 未解決の実装実現性項目

- ManualOnly: 実 Task Scheduler への登録と logon-triggered execution は手動確認が必要。
- Deferred: `--require-new-boot` boot-session guard。

## Handoff Packet

- Profile used: contract-kernel
- Source artifacts: plans/issue-1-next-logon-exec.md, plans/issue-1-next-logon-exec-change-risk-triage.md, issue #1
- Selected contracts / IDs: RC-001, RC-002, RC-003
- Files inspected: README.md, AGENTS.md
- Files intentionally not inspected: broad source files are absent
- Decisions made: COM/dynamic is allowed only inside `ComTaskSchedulerClient`; tests use stubs; no third-party scheduler wrapper.
- Do not redo unless new evidence appears: implementation approach and prohibited substitutions.
- Remaining work: create runtime-contract-kernel and test-design-kernel artifacts; implement production code and tests.
- Recommended next step: implementation-contract-review-kernel.agent.md
