using System.Diagnostics;

namespace NextLogonExec.Jobs;

public sealed class JobLock : IAsyncDisposable, IDisposable
{
    private readonly FileStream fileStream;

    private JobLock(FileStream fileStream)
    {
        this.fileStream = fileStream;
    }

    public static JobLock TryAcquire(string storeDirectory, string id)
    {
        try
        {
            string lockDirectory = Path.Combine(storeDirectory, "locks");
            Directory.CreateDirectory(lockDirectory);
            string path = Path.Combine(lockDirectory, id + ".lock");
            FileStream stream = new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new JobLock(stream);
        }
        catch (IOException ex)
        {
            Trace.TraceError(ex.ToString());
            throw new JobConflictException($"Job '{id}' is already running or locked: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new JobStoreException($"Failed to acquire lock for job '{id}'.", ex);
        }
    }

    public void Dispose()
    {
        fileStream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await fileStream.DisposeAsync();
    }
}
