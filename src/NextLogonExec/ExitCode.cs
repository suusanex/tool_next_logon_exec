namespace NextLogonExec;

public enum ExitCode
{
    Success = 0,
    InvalidArguments = 1,
    JobConflict = 2,
    JobNotFound = 3,
    TaskSchedulerError = 4,
    JobStoreError = 5,
    LaunchFailure = 6,
    ChildProcessFailed = 7,
    UnsupportedPlatform = 8
}
