using NextLogonExec;
using NextLogonExec.Jobs;
using NextLogonExec.ProcessLaunching;
using NextLogonExec.Scheduling;
using NextLogonExec.Security;

List<(string Name, Func<Task> Test)> tests =
[
    ("schedule stores job and registers internal run action", ScheduleStoresJobAndRegistersInternalRunAction),
    ("run unregisters before launch and passes ArgumentList", RunUnregistersBeforeLaunchAndPassesArgumentList),
    ("run does not apply delay a second time", RunDoesNotApplyDelayASecondTime),
    ("cancel removes pending job and unregisters task", CancelRemovesPendingJobAndUnregistersTask),
    ("status returns missing when nothing exists", StatusReturnsMissingWhenNothingExists),
    ("require-new-boot returns invalid arguments", RequireNewBootReturnsInvalidArguments),
    ("schedule rolls back new job when registration fails", ScheduleRollsBackNewJobWhenRegistrationFails),
    ("replace restores previous job when registration fails", ReplaceRestoresPreviousJobWhenRegistrationFails),
    ("result writer failure returns job store exit code", ResultWriterFailureReturnsJobStoreExitCode),
    ("windows argument escaper handles spaces quotes and trailing slashes", WindowsArgumentEscaperHandlesSpecialCharacters)
];

int failures = 0;
foreach ((string name, Func<Task> test) in tests)
{
    try
    {
        await test();
        Console.WriteLine("PASS " + name);
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine("FAIL " + name);
        Console.Error.WriteLine(ex);
    }
}

return failures == 0 ? 0 : 1;

static async Task ScheduleStoresJobAndRegistersInternalRunAction()
{
    using TempDirectory temp = new();
    FakeScheduler scheduler = new();
    FakeLauncher launcher = new();
    StringWriter output = new();
    StringWriter error = new();
    Application app = CreateApp(scheduler, launcher, output, error);

    int exitCode = await app.RunAsync([
        "schedule",
        "--id", "Case123",
        "--cwd", "C:\\Tests",
        "--elevated",
        "--store-dir", temp.Path,
        "--",
        "C:\\Tests\\TestHost.exe",
        "--phase",
        "after-reboot"
    ]);

    AssertEqual((int)ExitCode.Success, exitCode);
    AssertTrue(File.Exists(System.IO.Path.Combine(temp.Path, "Case123.json")));
    ScheduledJob job = new JobStore(temp.Path).LoadPending("Case123");
    AssertEqual("C:\\Tests\\TestHost.exe", job.FileName);
    AssertSequenceEqual(["--phase", "after-reboot"], job.Arguments);
    AssertEqual(1, scheduler.Registrations.Count);
    ScheduledTaskRegistration registration = scheduler.Registrations[0];
    AssertEqual("Case123", registration.Id);
    AssertEqual("DOMAIN\\user", registration.UserId);
    AssertTrue(registration.Elevated);
    AssertTrue(registration.ActionArguments.Contains("run --id Case123", StringComparison.Ordinal));
    AssertTrue(registration.ActionArguments.Contains("--store-dir", StringComparison.Ordinal));
    AssertFalse(registration.ActionArguments.Contains("TestHost.exe", StringComparison.OrdinalIgnoreCase));
}

static async Task RunUnregistersBeforeLaunchAndPassesArgumentList()
{
    using TempDirectory temp = new();
    FakeScheduler scheduler = new();
    FakeLauncher launcher = new();
    StringWriter output = new();
    StringWriter error = new();
    Application app = CreateApp(scheduler, launcher, output, error);
    JobStore store = new(temp.Path);
    store.SavePending(new ScheduledJob
    {
        Id = "CaseRun",
        CreatedAtUtc = DateTimeOffset.UtcNow,
        CreatedByUser = "DOMAIN\\user",
        CreatedByUserSid = "S-1-5-21-test",
        WorkingDirectory = "C:\\Tests",
        FileName = "C:\\Tests\\Runner.exe",
        Arguments = ["continue", "--case", "CaseRun"],
        Elevated = false,
        ConsumePolicy = "BeforeLaunch"
    });

    int exitCode = await app.RunAsync(["run", "--id", "CaseRun", "--store-dir", temp.Path]);

    AssertEqual((int)ExitCode.Success, exitCode);
    AssertSequenceEqual(["unregister:CaseRun", "launch:CaseRun"], scheduler.Events.Concat(launcher.Events).OrderBy(static e => e.Order).Select(static e => e.Name).ToArray());
    AssertEqual("C:\\Tests\\Runner.exe", launcher.LastJob?.FileName);
    AssertSequenceEqual(["continue", "--case", "CaseRun"], launcher.LastJob?.Arguments ?? []);
    AssertFalse(File.Exists(System.IO.Path.Combine(temp.Path, "CaseRun.json")));
    AssertTrue(File.Exists(System.IO.Path.Combine(temp.Path, "history", "CaseRun.json")));
}

static async Task RunDoesNotApplyDelayASecondTime()
{
    using TempDirectory temp = new();
    FakeScheduler scheduler = new();
    FakeLauncher launcher = new();
    Application app = CreateApp(scheduler, launcher, new StringWriter(), new StringWriter());
    JobStore store = new(temp.Path);
    store.SavePending(new ScheduledJob
    {
        Id = "DelayRun",
        CreatedAtUtc = DateTimeOffset.UtcNow,
        CreatedByUser = "DOMAIN\\user",
        FileName = "runner.exe",
        Arguments = [],
        ConsumePolicy = "BeforeLaunch",
        DelaySeconds = 60
    });
    using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

    int exitCode = await app.RunAsync(["run", "--id", "DelayRun", "--store-dir", temp.Path], timeout.Token);

    AssertEqual((int)ExitCode.Success, exitCode);
    AssertEqual("runner.exe", launcher.LastJob?.FileName);
}

static async Task CancelRemovesPendingJobAndUnregistersTask()
{
    using TempDirectory temp = new();
    FakeScheduler scheduler = new();
    FakeLauncher launcher = new();
    Application app = CreateApp(scheduler, launcher, new StringWriter(), new StringWriter());
    JobStore store = new(temp.Path);
    store.SavePending(new ScheduledJob
    {
        Id = "CancelMe",
        CreatedAtUtc = DateTimeOffset.UtcNow,
        CreatedByUser = "DOMAIN\\user",
        FileName = "tool.exe",
        Arguments = [],
        ConsumePolicy = "BeforeLaunch"
    });

    int exitCode = await app.RunAsync(["cancel", "--id", "CancelMe", "--store-dir", temp.Path]);

    AssertEqual((int)ExitCode.Success, exitCode);
    AssertFalse(store.PendingExists("CancelMe"));
    AssertTrue(scheduler.UnregisteredIds.Contains("CancelMe"));
}

static async Task StatusReturnsMissingWhenNothingExists()
{
    using TempDirectory temp = new();
    Application app = CreateApp(new FakeScheduler(), new FakeLauncher(), new StringWriter(), new StringWriter());

    int exitCode = await app.RunAsync(["status", "--id", "Missing", "--store-dir", temp.Path]);

    AssertEqual((int)ExitCode.JobNotFound, exitCode);
}

static async Task RequireNewBootReturnsInvalidArguments()
{
    using TempDirectory temp = new();
    StringWriter error = new();
    Application app = CreateApp(new FakeScheduler(), new FakeLauncher(), new StringWriter(), error);

    int exitCode = await app.RunAsync([
        "schedule",
        "--id", "Boot",
        "--require-new-boot",
        "--store-dir", temp.Path,
        "--",
        "tool.exe"
    ]);

    AssertEqual((int)ExitCode.InvalidArguments, exitCode);
    AssertTrue(error.ToString().Contains("not implemented", StringComparison.OrdinalIgnoreCase));
}

static async Task ScheduleRollsBackNewJobWhenRegistrationFails()
{
    using TempDirectory temp = new();
    FakeScheduler scheduler = new() { RegisterException = new TaskSchedulerClientException("registration failed") };
    Application app = CreateApp(scheduler, new FakeLauncher(), new StringWriter(), new StringWriter());

    int exitCode = await app.RunAsync([
        "schedule",
        "--id", "RollbackNew",
        "--store-dir", temp.Path,
        "--",
        "tool.exe"
    ]);

    AssertEqual((int)ExitCode.TaskSchedulerError, exitCode);
    AssertFalse(File.Exists(System.IO.Path.Combine(temp.Path, "RollbackNew.json")));
}

static async Task ReplaceRestoresPreviousJobWhenRegistrationFails()
{
    using TempDirectory temp = new();
    FakeScheduler scheduler = new() { RegisterException = new TaskSchedulerClientException("registration failed") };
    Application app = CreateApp(scheduler, new FakeLauncher(), new StringWriter(), new StringWriter());
    JobStore store = new(temp.Path);
    store.SavePending(new ScheduledJob
    {
        Id = "ReplaceMe",
        CreatedAtUtc = DateTimeOffset.UtcNow,
        CreatedByUser = "DOMAIN\\user",
        FileName = "old.exe",
        Arguments = ["old"],
        ConsumePolicy = "BeforeLaunch"
    });

    int exitCode = await app.RunAsync([
        "schedule",
        "--id", "ReplaceMe",
        "--replace",
        "--store-dir", temp.Path,
        "--",
        "new.exe"
    ]);

    AssertEqual((int)ExitCode.TaskSchedulerError, exitCode);
    ScheduledJob restored = store.LoadPending("ReplaceMe");
    AssertEqual("old.exe", restored.FileName);
    AssertSequenceEqual(["old"], restored.Arguments);
}

static async Task ResultWriterFailureReturnsJobStoreExitCode()
{
    using TempDirectory temp = new();
    FakeScheduler scheduler = new();
    FakeLauncher launcher = new() { Result = LaunchResult.Exited(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1234, 0) };
    StringWriter error = new();
    Application app = CreateApp(scheduler, launcher, new StringWriter(), error);
    string resultDirectory = System.IO.Path.Combine(temp.Path, "result-as-directory");
    Directory.CreateDirectory(resultDirectory);
    JobStore store = new(temp.Path);
    store.SavePending(new ScheduledJob
    {
        Id = "BadResult",
        CreatedAtUtc = DateTimeOffset.UtcNow,
        CreatedByUser = "DOMAIN\\user",
        FileName = "runner.exe",
        Arguments = [],
        ConsumePolicy = "BeforeLaunch",
        WaitForExit = true,
        ResultPath = resultDirectory
    });

    int exitCode = await app.RunAsync(["run", "--id", "BadResult", "--store-dir", temp.Path]);

    AssertEqual((int)ExitCode.JobStoreError, exitCode);
    AssertTrue(error.ToString().Contains("Failed to write result", StringComparison.Ordinal));
}

static Task WindowsArgumentEscaperHandlesSpecialCharacters()
{
    string joined = WindowsArgumentEscaper.Join(["run", "--store-dir", "C:\\Path With Space\\", "quote\"value"]);

    AssertEqual("run --store-dir \"C:\\Path With Space\\\\\" \"quote\\\"value\"", joined);
    return Task.CompletedTask;
}

static Application CreateApp(FakeScheduler scheduler, FakeLauncher launcher, TextWriter output, TextWriter error)
{
    return new Application(scheduler, launcher, new FakeCurrentUserProvider(), output, error, "C:\\Tools\\NextLogonExec.exe");
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool condition)
{
    if (!condition)
    {
        throw new InvalidOperationException("Expected condition to be true.");
    }
}

static void AssertFalse(bool condition)
{
    if (condition)
    {
        throw new InvalidOperationException("Expected condition to be false.");
    }
}

static void AssertSequenceEqual<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException($"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
    }
}

sealed class FakeScheduler : ITaskSchedulerClient
{
    public List<ScheduledTaskRegistration> Registrations { get; } = [];

    public List<string> UnregisteredIds { get; } = [];

    public List<OrderedEvent> Events { get; } = [];

    public Exception? RegisterException { get; set; }

    public void RegisterNextLogonTask(ScheduledTaskRegistration registration)
    {
        if (RegisterException is not null)
        {
            throw RegisterException;
        }

        Registrations.Add(registration);
    }

    public void UnregisterTask(string id)
    {
        UnregisteredIds.Add(id);
        Events.Add(OrderedEvent.Next("unregister:" + id));
    }

    public bool TaskExists(string id)
    {
        return Registrations.Any(r => r.Id == id) && !UnregisteredIds.Contains(id);
    }
}

sealed class FakeLauncher : IProcessLauncher
{
    public ScheduledJob? LastJob { get; private set; }

    public List<OrderedEvent> Events { get; } = [];

    public LaunchResult? Result { get; set; }

    public Task<LaunchResult> LaunchAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        LastJob = job;
        Events.Add(OrderedEvent.Next("launch:" + job.Id));
        return Task.FromResult(Result ?? LaunchResult.Started(DateTimeOffset.UtcNow, 1234));
    }
}

sealed class FakeCurrentUserProvider : ICurrentUserProvider
{
    public UserInfo GetCurrentUser()
    {
        return new UserInfo("DOMAIN\\user", "S-1-5-21-test");
    }
}

sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NextLogonExec.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

sealed record OrderedEvent(int Order, string Name)
{
    private static int nextOrder;

    public static OrderedEvent Next(string name)
    {
        return new OrderedEvent(Interlocked.Increment(ref nextOrder), name);
    }
}
