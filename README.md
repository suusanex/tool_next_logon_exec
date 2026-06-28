# tool_next_logon_exec

Windows での連続テスト等に使用する想定のツール。タスクスケジューラを使用して、指定したコマンドラインを次回ログオン時に一度だけ実行する。自動ログオン、再起動、テストフェーズ判定は呼び出し元が行い、このツールは持たない。

## 概要

`NextLogonExec` は、呼び出し元が指定したコマンドを JSON job として保存し、Task Scheduler のログオントリガーで次回対話ログオン時に内部 `run --id` エントリポイントを起動する Windows コンソールアプリケーション。

Task Scheduler に登録する action は呼び出し元コマンドではなく、常に次の形になる。

```text
NextLogonExec.exe run --id <job-id> --store-dir <resolved path>
```

呼び出し元コマンドは job JSON に保存し、起動時は `ProcessStartInfo.ArgumentList` で渡す。暗黙の `cmd.exe /c` ラップは行わない。shell 動作が必要な場合、呼び出し元が `cmd.exe` または PowerShell を明示的に指定する。

## ビルド

```text
dotnet build src/NextLogonExec/NextLogonExec.csproj
```

## schedule

```text
NextLogonExec schedule [options] -- <exe> [args...]
```

主な options:

```text
--id <id>           Job ID。省略時は GUID を生成する。
--cwd <dir>         起動するコマンドの作業ディレクトリ。
--elevated          Task Scheduler の RunLevel を HighestAvailable にする。
--delay <seconds>   ログオン後、起動前に待つ秒数。
--wait              起動したプロセスの終了を待つ。
--timeout <seconds> --wait 時のタイムアウト。
--stdout <path>     --wait 時に stdout をファイルへ保存する。
--stderr <path>     --wait 時に stderr をファイルへ保存する。
--result <path>     起動結果 JSON を保存する。
--store-dir <path>  job 保存ディレクトリを上書きする。
--replace           同じ ID の pending job と task を置き換える。
```

例:

```text
NextLogonExec schedule --id RebootCase01 --cwd C:\Tests -- C:\Tests\Runner.exe continue --case RebootCase01
```

shell が必要な場合:

```text
NextLogonExec schedule --id CaseWithShell -- C:\Windows\System32\cmd.exe /d /s /c "cd /d C:\Tests && run-after-reboot.cmd"
```

## run

Task Scheduler から呼ばれる内部エントリポイント。手動診断にも使える。

```text
NextLogonExec run --id <id> [--store-dir <path>]
```

既定の consume policy は `BeforeLaunch`。`run` は job lock を取得し、登録 task を削除してから保存済みコマンドを起動する。これによりログオンが不安定な場合の重複起動を抑止する。

## cancel

```text
NextLogonExec cancel --id <id> [--store-dir <path>]
```

指定した task と pending job を削除する。対象が既に存在しない場合も成功として扱う。

## status

```text
NextLogonExec status --id <id> [--store-dir <path>]
```

pending job、Task Scheduler task、history の有無を表示する。どれも存在しない場合は exit code `3` を返す。

## list

```text
NextLogonExec list [--store-dir <path>]
```

job store 内の pending job ID を列挙する。

## 保存場所と権限

既定の保存場所:

```text
%ProgramData%\NextLogonExec\jobs
```

非管理者ユーザーで `%ProgramData%` への書き込みが難しい環境では、`--store-dir <path>` で書き込み可能なディレクトリを指定する。

## exit code

```text
0  Success
1  Invalid arguments
2  Job already exists / conflict
3  Job not found
4  Task Scheduler registration error
5  Job store read/write error
6  Launch failure
7  Child process failed when --wait is used
8  Unsupported platform or not Windows
```

`--wait` を指定し、子プロセスが非 0 で終了した場合、ツールの exit code は `7` になる。実際の子プロセス exit code は `--result` の JSON に記録する。

## 制限

- Windows 専用。
- v1 では `--require-new-boot` は未実装で、指定すると明確なエラーを返す。
- stdout/stderr リダイレクトは v1 では `--wait` 指定時のみ対応する。
- 実 Task Scheduler service、ログオン、再起動を伴う検証は自動テストでは行わない。
- `schtasks.exe` は主実装では使用しない。手動確認や診断には使用してよい。

## 手動確認手順

1. ツールをビルドする。
2. `C:\Temp\write-marker.cmd` のような、時刻と引数をファイルへ書く小さなスクリプトを用意する。
3. 次のように schedule する。

```text
NextLogonExec schedule --id ManualSmoke01 --cwd C:\Temp -- C:\Temp\write-marker.cmd hello after-logon
```

4. Task Scheduler で `\NextLogonExec\ManualSmoke01` が存在することを確認する。
5. サインアウトしてサインインする、または AutoLogon 前提で再起動する。
6. marker file が書かれたことを確認する。
7. `\NextLogonExec\ManualSmoke01` が削除されていることを確認する。
8. `NextLogonExec status --id ManualSmoke01` で pending job が残っていないことを確認する。

## 開発時の検証

自動テストは Task Scheduler や OS 設定を変更しない。stub scheduler / launcher を使う。

```text
dotnet build src/NextLogonExec/NextLogonExec.csproj
dotnet run --project tests/NextLogonExec.Tests/NextLogonExec.Tests.csproj
```
