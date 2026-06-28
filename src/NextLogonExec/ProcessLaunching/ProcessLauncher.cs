using System.Diagnostics;
using NextLogonExec.Jobs;

namespace NextLogonExec.ProcessLaunching;

public sealed class ProcessLauncher : IProcessLauncher
{
    public async Task<LaunchResult> LaunchAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = job.FileName,
            UseShellExecute = false
        };

        if (!string.IsNullOrWhiteSpace(job.WorkingDirectory))
        {
            startInfo.WorkingDirectory = job.WorkingDirectory;
        }

        foreach (string argument in job.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        bool redirectOutput = !string.IsNullOrWhiteSpace(job.StdoutPath);
        bool redirectError = !string.IsNullOrWhiteSpace(job.StderrPath);
        startInfo.RedirectStandardOutput = redirectOutput;
        startInfo.RedirectStandardError = redirectError;

        DateTimeOffset launchedAt = DateTimeOffset.UtcNow;

        using Process process = new() { StartInfo = startInfo };
        if (!process.Start())
        {
            return LaunchResult.Failed(launchedAt, DateTimeOffset.UtcNow, "Process.Start returned false.");
        }

        if (!job.WaitForExit)
        {
            return LaunchResult.Started(launchedAt, process.Id);
        }

        Task? stdoutCopy = redirectOutput ? CopyToFileAsync(process.StandardOutput.BaseStream, job.StdoutPath!, cancellationToken) : null;
        Task? stderrCopy = redirectError ? CopyToFileAsync(process.StandardError.BaseStream, job.StderrPath!, cancellationToken) : null;

        if (job.TimeoutSeconds is not null)
        {
            Task waitTask = process.WaitForExitAsync(cancellationToken);
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(job.TimeoutSeconds.Value), cancellationToken);
            Task completed = await Task.WhenAny(waitTask, timeoutTask);
            if (completed == timeoutTask)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationToken);
                await AwaitIfPresent(stdoutCopy);
                await AwaitIfPresent(stderrCopy);
                return LaunchResult.Timeout(launchedAt, DateTimeOffset.UtcNow, process.Id);
            }

            await waitTask;
        }
        else
        {
            await process.WaitForExitAsync(cancellationToken);
        }

        await AwaitIfPresent(stdoutCopy);
        await AwaitIfPresent(stderrCopy);
        return LaunchResult.Exited(launchedAt, DateTimeOffset.UtcNow, process.Id, process.ExitCode);
    }

    private static async Task CopyToFileAsync(Stream source, string path, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream fileStream = new(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await source.CopyToAsync(fileStream, cancellationToken);
    }

    private static async Task AwaitIfPresent(Task? task)
    {
        if (task is not null)
        {
            await task;
        }
    }
}
