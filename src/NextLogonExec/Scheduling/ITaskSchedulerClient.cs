namespace NextLogonExec.Scheduling;

public interface ITaskSchedulerClient
{
    void RegisterNextLogonTask(ScheduledTaskRegistration registration);

    void UnregisterTask(string id);

    bool TaskExists(string id);
}

public sealed record ScheduledTaskRegistration(
    string Id,
    string ActionPath,
    string ActionArguments,
    string UserId,
    bool Elevated,
    int DelaySeconds);
