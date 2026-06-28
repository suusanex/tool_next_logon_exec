namespace NextLogonExec.Commands;

public abstract record CommandLine;

public sealed record ScheduleCommandLine(ScheduleOptions Options) : CommandLine;

public sealed record RunCommandLine(RunOptions Options) : CommandLine;

public sealed record CancelCommandLine(CancelOptions Options) : CommandLine;

public sealed record StatusCommandLine(StatusOptions Options) : CommandLine;

public sealed record ListCommandLine(ListOptions Options) : CommandLine;

public sealed record HelpCommandLine : CommandLine;

public sealed record ScheduleOptions(
    string? Id,
    string? WorkingDirectory,
    bool Elevated,
    int DelaySeconds,
    bool WaitForExit,
    int? TimeoutSeconds,
    string? StdoutPath,
    string? StderrPath,
    string? ResultPath,
    string? StoreDirectory,
    bool RequireNewBoot,
    bool Replace,
    string FileName,
    IReadOnlyList<string> Arguments);

public sealed record RunOptions(string Id, string? StoreDirectory);

public sealed record CancelOptions(string Id, string? StoreDirectory);

public sealed record StatusOptions(string Id, string? StoreDirectory);

public sealed record ListOptions(string? StoreDirectory);
