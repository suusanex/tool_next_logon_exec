namespace NextLogonExec.Jobs;

public sealed class ScheduledJob
{
    public int SchemaVersion { get; set; } = 1;

    public required string Id { get; set; }

    public required DateTimeOffset CreatedAtUtc { get; set; }

    public required string CreatedByUser { get; set; }

    public string? CreatedByUserSid { get; set; }

    public string? WorkingDirectory { get; set; }

    public required string FileName { get; set; }

    public required IReadOnlyList<string> Arguments { get; set; }

    public bool Elevated { get; set; }

    public required string ConsumePolicy { get; set; }

    public bool RequireNewBoot { get; set; }

    public string? CreatedBootId { get; set; }

    public int DelaySeconds { get; set; }

    public bool WaitForExit { get; set; }

    public int? TimeoutSeconds { get; set; }

    public string? StdoutPath { get; set; }

    public string? StderrPath { get; set; }

    public string? ResultPath { get; set; }
}
