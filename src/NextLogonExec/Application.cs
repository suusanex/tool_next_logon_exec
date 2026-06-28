using System.Diagnostics;
using NextLogonExec.Commands;
using NextLogonExec.Diagnostics;
using NextLogonExec.Jobs;
using NextLogonExec.ProcessLaunching;
using NextLogonExec.Scheduling;
using NextLogonExec.Security;

namespace NextLogonExec;

public sealed class Application
{
    private readonly ITaskSchedulerClient scheduler;
    private readonly IProcessLauncher processLauncher;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly TextWriter output;
    private readonly TextWriter error;
    private readonly string executablePath;

    public Application(
        ITaskSchedulerClient scheduler,
        IProcessLauncher processLauncher,
        ICurrentUserProvider currentUserProvider,
        TextWriter output,
        TextWriter error,
        string executablePath)
    {
        this.scheduler = scheduler;
        this.processLauncher = processLauncher;
        this.currentUserProvider = currentUserProvider;
        this.output = output;
        this.error = error;
        this.executablePath = executablePath;
    }

    public static Application CreateDefault()
    {
        string executablePath = Environment.ProcessPath
            ?? throw new TaskSchedulerClientException("Executable path is not available for Task Scheduler action.");
        if (Directory.Exists(executablePath))
        {
            throw new TaskSchedulerClientException("Executable path resolved to a directory.");
        }

        return new Application(
            new ComTaskSchedulerClient(),
            new ProcessLauncher(),
            new CurrentUserProvider(),
            Console.Out,
            Console.Error,
            executablePath);
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        try
        {
            CommandLine commandLine = CommandLineParser.Parse(args);

            return commandLine switch
            {
                ScheduleCommandLine schedule => Schedule(schedule.Options),
                RunCommandLine run => await RunJobAsync(run.Options, cancellationToken),
                CancelCommandLine cancel => Cancel(cancel.Options),
                StatusCommandLine status => Status(status.Options),
                ListCommandLine list => List(list.Options),
                HelpCommandLine => Help(),
                _ => throw new InvalidArgumentsException("Unknown command.")
            };
        }
        catch (NextLogonExecException ex)
        {
            Trace.TraceError(ex.ToString());
            error.WriteLine(ex.Message);
            return (int)ex.ExitCode;
        }
        catch (Exception ex)
        {
            Trace.TraceError(ex.ToString());
            error.WriteLine("Unexpected error: " + ex.Message);
            return (int)ExitCode.TaskSchedulerError;
        }
    }

    private int Schedule(ScheduleOptions options)
    {
        if (options.RequireNewBoot)
        {
            throw new InvalidArgumentsException("--require-new-boot is not implemented in v1.");
        }

        string storeDirectory = StoreDirectory.Resolve(options.StoreDirectory);
        JobStore store = new(storeDirectory);
        string id = string.IsNullOrWhiteSpace(options.Id) ? Guid.NewGuid().ToString("N") : options.Id;
        JobId.Validate(id);

        ScheduledJob? existingJob = store.TryLoadPending(id);
        if (existingJob is not null && !options.Replace)
        {
            throw new JobConflictException($"Job '{id}' already exists. Use --replace to replace it.");
        }

        UserInfo userInfo = currentUserProvider.GetCurrentUser();
        ScheduledJob job = new()
        {
            Id = id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedByUser = userInfo.Name,
            CreatedByUserSid = userInfo.Sid,
            WorkingDirectory = options.WorkingDirectory,
            FileName = options.FileName,
            Arguments = options.Arguments,
            Elevated = options.Elevated,
            ConsumePolicy = "BeforeLaunch",
            RequireNewBoot = false,
            CreatedBootId = null,
            DelaySeconds = options.DelaySeconds,
            WaitForExit = options.WaitForExit,
            TimeoutSeconds = options.TimeoutSeconds,
            StdoutPath = options.StdoutPath,
            StderrPath = options.StderrPath,
            ResultPath = options.ResultPath
        };

        store.SavePending(job);

        string actionArguments = WindowsArgumentEscaper.Join(new[] { "run", "--id", id, "--store-dir", storeDirectory });
        try
        {
            scheduler.RegisterNextLogonTask(new ScheduledTaskRegistration(
                id,
                executablePath,
                actionArguments,
                userInfo.Name,
                options.Elevated,
                options.DelaySeconds));
        }
        catch
        {
            RollBackPendingJob(store, job.Id, existingJob);
            throw;
        }

        output.WriteLine($"Scheduled job '{id}'.");
        output.WriteLine($"Job store: {storeDirectory}");
        return (int)ExitCode.Success;
    }

    private static void RollBackPendingJob(JobStore store, string id, ScheduledJob? existingJob)
    {
        try
        {
            if (existingJob is null)
            {
                store.DeletePending(id);
                return;
            }

            store.SavePending(existingJob);
        }
        catch (Exception ex)
        {
            Trace.TraceError(ex.ToString());
        }
    }

    private async Task<int> RunJobAsync(RunOptions options, CancellationToken cancellationToken)
    {
        string storeDirectory = StoreDirectory.Resolve(options.StoreDirectory);
        JobStore store = new(storeDirectory);
        ScheduledJob job = store.LoadPending(options.Id);

        await using JobLock jobLock = JobLock.TryAcquire(storeDirectory, options.Id);

        scheduler.UnregisterTask(options.Id);
        store.DeletePending(options.Id);

        LaunchResult result;
        try
        {
            result = await processLauncher.LaunchAsync(job, cancellationToken);
        }
        catch (Exception ex) when (ex is not NextLogonExecException)
        {
            Trace.TraceError(ex.ToString());
            result = LaunchResult.Failed(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, ex.Message);
            WriteResultArtifacts(store, job, result, "LaunchFailed");
            throw new LaunchFailedException($"Failed to launch job '{options.Id}': {ex.Message}");
        }

        WriteResultArtifacts(store, job, result, result.Success ? "Completed" : "Failed");

        if (!result.Success)
        {
            if (result.ChildExitCode is not null || result.TimedOut)
            {
                throw new ChildProcessFailedException($"Job '{options.Id}' completed with child status '{result.Status}'.");
            }

            throw new LaunchFailedException($"Job '{options.Id}' failed: {result.ErrorMessage}");
        }

        output.WriteLine($"Ran job '{options.Id}'.");
        return (int)ExitCode.Success;
    }

    private void WriteResultArtifacts(JobStore store, ScheduledJob job, LaunchResult result, string status)
    {
        if (!string.IsNullOrWhiteSpace(job.ResultPath))
        {
            ResultWriter.Write(job.ResultPath, job.Id, result);
        }

        store.SaveHistory(job, status, result);
    }

    private int Cancel(CancelOptions options)
    {
        string storeDirectory = StoreDirectory.Resolve(options.StoreDirectory);
        JobStore store = new(storeDirectory);

        scheduler.UnregisterTask(options.Id);
        store.DeletePending(options.Id);

        output.WriteLine($"Cancelled job '{options.Id}'.");
        return (int)ExitCode.Success;
    }

    private int Status(StatusOptions options)
    {
        string storeDirectory = StoreDirectory.Resolve(options.StoreDirectory);
        JobStore store = new(storeDirectory);
        bool pending = store.PendingExists(options.Id);
        bool history = store.HistoryExists(options.Id);
        bool task = scheduler.TaskExists(options.Id);

        output.WriteLine($"Job ID: {options.Id}");
        output.WriteLine($"Pending job: {(pending ? "yes" : "no")}");
        output.WriteLine($"Scheduled task: {(task ? "yes" : "no")}");
        output.WriteLine($"History: {(history ? "yes" : "no")}");

        if (!pending && !history && !task)
        {
            return (int)ExitCode.JobNotFound;
        }

        return (int)ExitCode.Success;
    }

    private int List(ListOptions options)
    {
        string storeDirectory = StoreDirectory.Resolve(options.StoreDirectory);
        JobStore store = new(storeDirectory);
        IReadOnlyList<string> ids = store.ListPendingIds();

        if (ids.Count == 0)
        {
            output.WriteLine("No pending jobs.");
            return (int)ExitCode.Success;
        }

        foreach (string id in ids)
        {
            output.WriteLine(id);
        }

        return (int)ExitCode.Success;
    }

    private int Help()
    {
        output.WriteLine("NextLogonExec schedule [options] -- <exe> [args...]");
        output.WriteLine("NextLogonExec run --id <id> [--store-dir <path>]");
        output.WriteLine("NextLogonExec cancel --id <id> [--store-dir <path>]");
        output.WriteLine("NextLogonExec status --id <id> [--store-dir <path>]");
        output.WriteLine("NextLogonExec list [--store-dir <path>]");
        return (int)ExitCode.Success;
    }
}
