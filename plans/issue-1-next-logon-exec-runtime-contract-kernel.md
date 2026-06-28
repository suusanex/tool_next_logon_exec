# Runtime Contract Kernel

## スコープ

`contract-kernel` profile で選定された RC-001, RC-002, RC-003 を対象にする。Plan は `plans/issue-1-next-logon-exec.md`、triage は `plans/issue-1-next-logon-exec-change-risk-triage.md`。

## Runtime Contract Kernel

| Contract ID | Scenario | Producer | Consumer | Message / API / Event | Required fields | Error / timeout behavior | Production implementation address | Verification hook |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| RC-001 | schedule registers next-logon task | `ScheduleCommand` / `Application` | `ComTaskSchedulerClient` -> Task Scheduler service | COM `RegisterTaskDefinition` with `LogonTrigger` and `ExecAction` | job id, current user, elevated flag, delay seconds, action path, action args | registration failure maps to TaskSchedulerError | `src/NextLogonExec/Scheduling/ComTaskSchedulerClient.cs` | TP-001 |
| RC-002 | Task Scheduler invokes internal run entrypoint | Task Scheduler service | `RunCommand` / `Application` | `ExecAction.Path = current executable`, `ExecAction.Arguments = run --id <id> [--store-dir <dir>]` | job id, optional store dir | missing job maps to JobNotFound; invalid args maps to InvalidArguments | `src/NextLogonExec/Commands/ScheduleOptions.cs`, `src/NextLogonExec/Scheduling/WindowsArgumentEscaper.cs` | TP-002 |
| RC-003 | run consumes job and launches caller command | `RunCommand` / `Application` | caller process | `ProcessStartInfo.FileName` and `ArgumentList` | job id, workingDirectory, fileName, arguments, wait/timeout/stdout/stderr/result | duplicate lock maps to conflict; launch failure maps to LaunchFailure; child non-zero with `--wait` maps to ChildProcessFailed | `src/NextLogonExec/ProcessLaunching/ProcessLauncher.cs`, `src/NextLogonExec/Jobs/JobStore.cs`, `src/NextLogonExec/Jobs/JobLock.cs` | TP-003 |

## Plan / implementation contract 適合性

| Runtime Contract ID | Plan requirement | Implementation contract decision | Runtime contract address | Conformance |
| --- | --- | --- | --- | --- |
| RC-001 | FR-001, FR-003 / AC-004, AC-005 | Task Scheduler COM object model | `ComTaskSchedulerClient` | Conformant |
| RC-002 | FR-002 / AC-003 | task action isolation and job storage | schedule options + argument escaper | Conformant |
| RC-003 | FR-004 / AC-006..AC-008 | lock, before-launch consume, `ArgumentList` | job store, lock, process launcher | Conformant |

## 注記 / 前提

- `ExecutionTimeLimit` は disabled 相当として扱う。
- stdout/stderr redirection は `--wait` と組み合わせた場合のみ v1 で扱う。非 wait redirect は安全な stream drain が必要なため invalid args とする。
- 実 Task Scheduler service の起動動作は manual validation に残す。

## Handoff Packet

- Profile used: contract-kernel
- Source artifacts: Plan, triage, implementation contract, implementation contract review
- Selected contracts / IDs: RC-001, RC-002, RC-003
- Files inspected: artifacts only; source files not yet created
- Files intentionally not inspected: production source absent
- Decisions made: RC-001..RC-003 contract rows established.
- Do not redo unless new evidence appears: participant/boundary mapping for RC-001..RC-003.
- Remaining work: test-design-kernel, implementation, verification.
- Recommended next step: test-design-kernel.agent.md
