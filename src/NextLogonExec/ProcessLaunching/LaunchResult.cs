namespace NextLogonExec.ProcessLaunching;

public sealed record LaunchResult(
    DateTimeOffset LaunchedAtUtc,
    DateTimeOffset? ExitedAtUtc,
    int? ProcessId,
    int? ChildExitCode,
    bool TimedOut,
    string Status,
    string? ErrorMessage)
{
    public bool Success => ErrorMessage is null && !TimedOut && (ChildExitCode is null or 0);

    public static LaunchResult Started(DateTimeOffset launchedAtUtc, int processId)
    {
        return new LaunchResult(launchedAtUtc, null, processId, null, false, "Started", null);
    }

    public static LaunchResult Exited(DateTimeOffset launchedAtUtc, DateTimeOffset exitedAtUtc, int processId, int exitCode)
    {
        return new LaunchResult(launchedAtUtc, exitedAtUtc, processId, exitCode, false, exitCode == 0 ? "Exited" : "ChildFailed", null);
    }

    public static LaunchResult Timeout(DateTimeOffset launchedAtUtc, DateTimeOffset exitedAtUtc, int processId)
    {
        return new LaunchResult(launchedAtUtc, exitedAtUtc, processId, null, true, "TimedOut", null);
    }

    public static LaunchResult Failed(DateTimeOffset launchedAtUtc, DateTimeOffset exitedAtUtc, string errorMessage)
    {
        return new LaunchResult(launchedAtUtc, exitedAtUtc, null, null, false, "LaunchFailed", errorMessage);
    }
}
