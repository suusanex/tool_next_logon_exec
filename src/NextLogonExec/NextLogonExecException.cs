namespace NextLogonExec;

public abstract class NextLogonExecException : Exception
{
    protected NextLogonExecException(string message, ExitCode exitCode, Exception? innerException = null)
        : base(message, innerException)
    {
        ExitCode = exitCode;
    }

    public ExitCode ExitCode { get; }
}

public sealed class InvalidArgumentsException : NextLogonExecException
{
    public InvalidArgumentsException(string message)
        : base(message, ExitCode.InvalidArguments)
    {
    }
}

public sealed class JobConflictException : NextLogonExecException
{
    public JobConflictException(string message, Exception? innerException = null)
        : base(message, ExitCode.JobConflict, innerException)
    {
    }
}

public sealed class JobNotFoundException : NextLogonExecException
{
    public JobNotFoundException(string message)
        : base(message, ExitCode.JobNotFound)
    {
    }
}

public sealed class TaskSchedulerClientException : NextLogonExecException
{
    public TaskSchedulerClientException(string message, Exception? innerException = null)
        : base(message, ExitCode.TaskSchedulerError, innerException)
    {
    }
}

public sealed class JobStoreException : NextLogonExecException
{
    public JobStoreException(string message, Exception? innerException = null)
        : base(message, ExitCode.JobStoreError, innerException)
    {
    }
}

public sealed class LaunchFailedException : NextLogonExecException
{
    public LaunchFailedException(string message)
        : base(message, ExitCode.LaunchFailure)
    {
    }
}

public sealed class ChildProcessFailedException : NextLogonExecException
{
    public ChildProcessFailedException(string message)
        : base(message, ExitCode.ChildProcessFailed)
    {
    }
}

public sealed class UnsupportedPlatformException : NextLogonExecException
{
    public UnsupportedPlatformException(string message)
        : base(message, ExitCode.UnsupportedPlatform)
    {
    }
}
