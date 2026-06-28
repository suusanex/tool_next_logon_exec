using NextLogonExec.Jobs;

namespace NextLogonExec.ProcessLaunching;

public interface IProcessLauncher
{
    Task<LaunchResult> LaunchAsync(ScheduledJob job, CancellationToken cancellationToken);
}
