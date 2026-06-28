# Test Design Kernel

## スコープ

Runtime Contract Kernel の RC-001, RC-002, RC-003 を対象にする。OS の Task Scheduler を変更しない tests と、manual validation を分ける。

## Test Design Kernel

| Test Point ID | Runtime Contract ID | What to verify | Stub / fake allowed? | Production binding required? | Expected observation | Status |
| --- | --- | --- | --- | --- | --- | --- |
| TP-001 | RC-001 | `schedule` が job JSON を保存し、fake scheduler に現在ユーザー、logon action、elevated flag を渡す | Yes | Yes | fake scheduler registration contains job id and action path/args for internal run | Done |
| TP-002 | RC-002 | Task action arguments が caller command ではなく `run --id <job-id>` を指す | Yes | Yes | action arguments include `run --id Job`, and do not include caller executable | Done |
| TP-003 | RC-003 | `run --id` が task unregister の後に saved command を `ArgumentList` で起動し、duplicate lock を防ぐ | Yes | Yes | fake scheduler unregister occurs before fake launcher start; launcher receives fileName and individual args | Done |

## 必須 production binding 確認事項

| Test Point ID | Runtime Contract ID | Substitute used / expected | Production implementation to check | Production wiring / entrypoint to check | Notes |
| --- | --- | --- | --- | --- | --- |
| TP-001 | RC-001 | fake `ITaskSchedulerClient` | `ComTaskSchedulerClient` | `Application.CreateDefault` uses `ComTaskSchedulerClient` | verification-kernel で source inspection |
| TP-002 | RC-002 | fake `ITaskSchedulerClient` | `WindowsArgumentEscaper` and schedule action construction | schedule command uses current executable path and `run --id` args | caller command must not appear in action |
| TP-003 | RC-003 | fake `IProcessLauncher` | `ProcessLauncher` | run command uses default launcher and job lock/store | `ArgumentList` must be used |

## 手動確認のみの項目

- ManualOnly: 実 Task Scheduler service に対する登録、logon trigger 発火、sign out/in または reboot 後の実起動確認。

## Behavior case test mapping

N/A

## 注記 / 前提

- custom test runner を使う場合でも OS 変更をしないため、Task Scheduler production binding は source inspection と manual validation に分ける。
- `--require-new-boot` は v1 の unsupported path として CLI test を置く。

## Handoff Packet

- Profile used: contract-kernel
- Source artifacts: Plan, triage, runtime contract
- Selected contracts / IDs: RC-001, RC-002, RC-003
- Files inspected: plans/issue-1-next-logon-exec-runtime-contract-kernel.md
- Files intentionally not inspected: source files not yet created
- Decisions made: TP-001..TP-003 established; all require production binding verification.
- Behavior case coverage: N/A
- Do not redo unless new evidence appears: selected test points and expected observations.
- Remaining work: implement tests and production binding.
- Recommended next step: implementation-handoff-review.agent.md
