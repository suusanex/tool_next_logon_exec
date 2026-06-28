# Change Risk Triage

## Plan readiness check

| Check | Result | Notes |
| --- | --- | --- |
| Expansion decision exists? | Yes | Plan に記録済み。 |
| Behavior spec exists when required? | N/A | Expansion required: No。 |
| Relevant source requirements have Case IDs? | N/A | Behavior spec 不要。 |
| Relevant Case IDs are mapped to FR / AC or explicit disposition? | N/A | Behavior spec 不要。 |
| Negative expectations are represented? | Yes | non-goals、`schtasks.exe` 非依存、caller command を task action に入れない、暗黙 shell wrap なし。 |
| Blocking requirement ambiguity remains? | No | v1 scope は issue に十分明示されている。 |
| Plan readiness status | ReadyForRiskTriage | risk/profile selection に進める。 |

## 推奨プロファイル

contract-kernel

## 理由

Task Scheduler service、JSON job store、内部 `run` process、caller command launch の境界があり、stub-only success と production wiring mismatch のリスクが高い。一方で v1 の範囲は単一 CLI tool 内に bounded で、full-coverage decomposition は不要。

## High-risk boundaries

| Boundary | Producer | Consumer | Mechanism | Risk type |
| --- | --- | --- | --- | --- |
| schedule to Task Scheduler | `NextLogonExec schedule` | Task Scheduler service | Task Scheduler 2.0 COM `RegisterTaskDefinition` | external COM API / production wiring |
| Task Scheduler action to run entrypoint | Task Scheduler service | `NextLogonExec run --id` | `ExecAction.Path` and `ExecAction.Arguments` | cross-process action contract |
| run entrypoint to caller command | `NextLogonExec run --id` | caller executable | `ProcessStartInfo.ArgumentList` | process launch / quoting avoidance |
| job store one-shot consume | `schedule`, `run`, `cancel`, `status` | JSON files and lock files | `%ProgramData%\NextLogonExec\jobs` or `--store-dir` | durable state / idempotency |

## 対象とする runtime contracts

| Contract ID | Boundary | What is at risk | Why selected | Triage status | Next action |
| --- | --- | --- | --- | --- | --- |
| RC-001 | `schedule` -> Task Scheduler | wrong trigger/principal/action/runlevel | v1 acceptance criteria の中核で production COM wiring が必要 | Deferred | implementation-contract-kernel, runtime-contract-kernel |
| RC-002 | Task Scheduler -> `run --id` -> job store | caller command が task action に入る、job id/store が不一致 | one-shot contract と quoting 回避の中心 | Deferred | runtime-contract-kernel, test-design-kernel |
| RC-003 | `run --id` -> caller process | duplicate launch、task 消費順序、ArgumentList 不使用 | reboot-continuation の観測 behavior を決める | Deferred | runtime-contract-kernel, test-design-kernel |

## 選択されなかった候補 runtime contracts

| Contract ID | Boundary | Why not selected | Candidate status | Suggested next action |
| --- | --- | --- | --- | --- |
| RC-N/A | `--require-new-boot` boot marker | v1 では未サポート error として扱う | OutOfScopeForThisPass | future issue |

## Risk trigger スキャン

| Risk trigger | Present / Absent / Unclear | Notes |
| --- | --- | --- |
| Cross-process or cross-service sequence | Present | schedule, Task Scheduler, run, caller process。 |
| Queue / event / webhook / background worker | Present | LogonTrigger。 |
| External API or SDK | Present | Task Scheduler COM API。 |
| Authentication or authorization | Present | current user principal and run level。 |
| Durable state / retry / replay / idempotency | Present | job JSON, lock, one-shot consume。 |
| Startup wiring / DI / configuration | Present | action path/arguments, store-dir propagation。 |
| Production implementation split from test substitute | Present | tests use stubs for scheduler/launcher。 |
| Multiple runtime participants coordinating state | Present | CLI commands, scheduler, job store。 |
| Observable behavior spanning more than one component | Present | schedule/run/status/cancel all combine scheduler and store state。 |

## 実装実現性リスク

| Trigger | Status | Evidence | Required next step |
| --- | --- | --- | --- |
| Plan names a specific external SDK or API | Present | Task Scheduler 2.0 COM API | implementation-contract-kernel |
| Plan names a package, release, binary artifact, or local lib folder | Absent | no external package required | none |
| Plan names a namespace, type, method, extension method, provider ID, or config section | Present | `Schedule.Service`, `RegisterTaskDefinition`, `LogonTrigger`, `ExecAction` | implementation-contract-kernel |
| Existing code contains a similar but different implementation path | Absent | repo has no source code | none |
| Implementation requires DI/startup/configuration wiring | Present | CLI action path and command routing | implementation-contract-kernel |
| The affected production address is not known from current evidence | Present | source tree is not implemented yet | implementation-contract-kernel |
| Plan contains remaining work about API surface inspection or dependency confirmation | Present | COM surface must be isolated and tested via interface | implementation-contract-kernel |

## 推奨する次の agent

Immediate next agent: `implementation-contract-kernel.agent.md`

Minimum required downstream flow:

1. `implementation-contract-kernel.agent.md`
2. `implementation-contract-review-kernel.agent.md`
3. `runtime-contract-kernel.agent.md`
4. `test-design-kernel.agent.md`
5. `implementation-handoff-review.agent.md`
6. `implementation-execution.agent.md`
7. `verification-kernel.agent.md`
8. `residual-decision-gate.agent.md`

## full-coverage 時の分割方針

該当なし。

## 今回の triage の対象外

- 実 Task Scheduler 登録の手動検証。
- future enhancement の `--require-new-boot` 実装。
- installer/packaging。

## Handoff Packet

- Profile used: triage-only
- Plan readiness: ReadyForRiskTriage
- Behavior spec artifact: N/A
- Recommended process profile: contract-kernel
- Source artifacts: plans/issue-1-next-logon-exec.md, GitHub issue #1
- Selected contracts / IDs: RC-001, RC-002, RC-003
- Files inspected: README.md, AGENTS.md
- Files intentionally not inspected: source files are absent
- Decisions made: contract-kernel is sufficient; full-coverage is not required.
- Implementation realization risk summary: Present due to Task Scheduler COM API, CLI action wiring, and production/test substitute split.
- Do not redo unless new evidence appears: selected runtime contracts RC-001..RC-003.
- Remaining work: implementation-contract-kernel must confirm implementation path and prohibited substitutions.
- Recommended next step: `implementation-contract-kernel.agent.md` with Plan and this triage artifact.
- Required downstream guardrails: runtime contract identification, participant/boundary mapping, test point mapping, stub/fake usage check, production implementation binding, production wiring/entrypoint verification, explicit unresolved status for RC-001..RC-003.
- Full-coverage handling: N/A
