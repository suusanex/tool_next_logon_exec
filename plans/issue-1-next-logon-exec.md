# Plan Kernel

## 目的

Windows の再起動継続テストで利用できる `NextLogonExec` コンソールツールを実装する。呼び出し元が指定したコマンドを JSON job として保存し、次回の対話ログオン時に Task Scheduler から内部 `run --id` エントリポイントを一度だけ起動する。

## 非目標

- AutoLogon の設定、再起動、シャットダウンは扱わない。
- テストランナー、テスト phase 判定、結果集約は実装しない。
- リモートマシン、別ユーザーのパスワード登録、`schtasks.exe` 依存は v1 の対象外。
- `--require-new-boot` の boot-session 判定は v1 では実装しない。

## 機能要件

- FR-001: `schedule -- <exe> [args...]` は job JSON を保存し、`\NextLogonExec\<job-id>` の Task Scheduler task を登録する。
- FR-002: Task Scheduler action は呼び出し元コマンドを直接保持せず、`NextLogonExec.exe run --id <job-id>` を起動する。
- FR-003: 登録 task は現在ユーザーの `LogonTrigger`、`InteractiveToken` principal、既定 `LeastPrivilege`、`--elevated` 指定時 `HighestAvailable` を使う。
- FR-004: `run --id` は job JSON を読み、job lock を取得し、登録 task を先に削除してから保存済みコマンドを `ProcessStartInfo.ArgumentList` で起動する。
- FR-005: `cancel --id` は task と pending job を削除し、対象が既に存在しなくても成功する。
- FR-006: `status --id` は pending job、Task Scheduler task、history/result の状態を人間が読める形で表示する。
- FR-007: CLI は安定した exit code と有用な error message を返す。
- FR-008: README は用途、非目標、例、制限、手動確認手順を記載する。

## 受け入れ条件

- AC-001: Windows console app として build できる。
- AC-002: `schedule -- <exe> [args...]` 実行後、job JSON に `fileName` と `arguments` が保存される。
- AC-003: 登録 action は内部 `run --id` を指し、呼び出し元コマンドを直接 Task Scheduler action に入れない。
- AC-004: 登録 task は現在ユーザーの logon trigger と interactive-token principal を使う。
- AC-005: `--elevated` 指定時、登録 task の run level が highest available になる。
- AC-006: `run --id` は job lock により重複起動を防ぐ。
- AC-007: `run --id` は task を削除してから保存済みコマンドを起動する。
- AC-008: 保存済みコマンドの起動は `ProcessStartInfo.ArgumentList` を使い、暗黙の `cmd.exe /c` ラップをしない。
- AC-009: `cancel --id` は task と pending job を削除する。
- AC-010: `status --id` は job/task/result の状態を表示する。
- AC-011: README に reboot-continuation の利用例、非目標、制限、手動確認手順がある。

## Black-box behavior coverage

- Expansion required: No
- Behavior spec artifact: N/A
- Plan readiness: ReadyForRiskTriage
- Expansion decision reason: 要求は複数 CLI と一度だけの消費 semantics を含むが、issue 本文に状態遷移、negative expectation、非目標、acceptance criteria が十分に明示されている。別 artifact による case expansion がなくても bounded Plan と受け入れ条件を安全に作れる。
- Blocking requirement-elaboration items: none

### Case-to-Plan mapping

| Case ID | Source IDs | FR / AC | Disposition | Notes |
| --- | --- | --- | --- | --- |
| N/A | issue #1 | FR-001..FR-008 / AC-001..AC-011 | MappedToPlan | Behavior spec 不要のため Case ID は作成しない。 |

## 影響コンポーネント / モジュール

- `src/NextLogonExec`: CLI、job store、Task Scheduler COM client、process launcher、result writer。
- `tests/NextLogonExec.Tests`: OS を変更しない unit/integration-style tests。Task Scheduler と process launch は stub を使用する。
- `README.md`: CLI 利用方法、制限、手動検証手順。

## 実装スコープ

- .NET / C# の Windows console app を新規作成する。
- Task Scheduler 2.0 COM object model を C# から直接呼び出す production client を実装する。
- Task Scheduler 依存を `ITaskSchedulerClient` で抽象化し、テストでは stub を注入する。
- JSON job store、job lock、run/cancel/status/list の CLI を実装する。
- `--require-new-boot` は明確な未サポート error として扱う。

## 既知の high-risk boundaries

| Risk trigger | Present / Absent / Unclear | Notes |
| --- | --- | --- |
| Cross-process or cross-service sequence | Present | CLI process、Task Scheduler service、次回 logon で起動される `run` process、caller command が連携する。 |
| Queue / event / webhook / background worker | Present | `LogonTrigger` による Task Scheduler 起動。 |
| External API or SDK | Present | Task Scheduler 2.0 COM API。 |
| Authentication or authorization | Present | 現在ユーザー、InteractiveToken、RunLevel。 |
| Durable state / retry / replay / idempotency | Present | JSON job、one-shot consume、job lock、history。 |
| Startup wiring / DI / configuration | Present | Task Scheduler action が現在の executable と `run --id` に正しく配線される必要がある。 |
| Production implementation split from test substitute | Present | tests は stub scheduler/launcher を使うため production binding verification が必要。 |
| Multiple runtime participants coordinating state | Present | schedule/run/cancel/status と Task Scheduler task/job file が状態を共有する。 |
| Observable behavior spanning more than one component | Present | job store、scheduler、process launcher、CLI output が一連の動作を構成する。 |

## 今回の対象外

- boot-session marker による `--require-new-boot` 実行判定。
- `AfterSuccess` consume policy。
- XML export、EventLog/ETW、installer/packaging、PowerShell completion。
- 実 Task Scheduler を変更する自動テスト。

## change-risk-triage への引き継ぎ

Task Scheduler COM、job JSON、one-shot run、process launch を中心に runtime boundary を選定する。特に Task Scheduler action が caller command ではなく `run --id` を起動すること、`run` が task を消費してから `ProcessStartInfo.ArgumentList` で caller command を起動すること、tests が stub に寄り過ぎないことを Guardrail Focus として扱う。

## 実装実現性の残留事項

| Item | Status | Notes |
| --- | --- | --- |
| Task Scheduler COM API surface | Confirmed | issue の公式参照と Windows Task Scheduler 2.0 object model を使う。 |
| CLI parser dependency | Confirmed | v1 は外部依存を避け、小さな parser を実装する。 |
| Unit tests における OS 変更回避 | Confirmed | `ITaskSchedulerClient` と `IProcessLauncher` の stub を使う。 |
| `--require-new-boot` behavior | Deferred | v1 は unsupported error。 |

## Handoff Packet

- Profile used: plan-kernel
- Plan artifact: plans/issue-1-next-logon-exec.md
- Plan readiness: ReadyForRiskTriage
- Behavior spec artifact: N/A
- Source artifacts: GitHub issue #1, README.md, AGENTS.md
- Selected contracts / IDs: このエージェントでは選択しない。最終選択は change-risk-triage が行う
- Implementation-realization residuals: Task Scheduler COM API surface は production client 内で直接使用する。tests は stub を使う。`--require-new-boot` は Deferred。
- Files inspected: README.md, AGENTS.md, apm.yml, apm.lock.yaml
- Files intentionally not inspected: source tree は未存在のためなし
- Decisions made: v1 は `--require-new-boot` を未サポート error とする。Task Scheduler action は内部 `run` のみを起動する。Task Scheduler 依存は interface 化する。
- Do not redo unless new evidence appears: issue #1 の non-goals と v1 acceptance criteria の抽出。
- Remaining work: Consumed: Plan 作成。DeferredWithReason: boot-session guard は future enhancement。
- Recommended next step: change-risk-triage.agent.md with this Plan and issue #1.
