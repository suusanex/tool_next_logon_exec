using System.Text.Json;
using NextLogonExec.ProcessLaunching;

namespace NextLogonExec.Jobs;

public sealed class JobStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string storeDirectory;

    public JobStore(string storeDirectory)
    {
        this.storeDirectory = storeDirectory;
    }

    public void SavePending(ScheduledJob job)
    {
        try
        {
            Directory.CreateDirectory(storeDirectory);
            File.WriteAllText(PendingPath(job.Id), JsonSerializer.Serialize(job, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new JobStoreException($"Failed to save job '{job.Id}'.", ex);
        }
    }

    public ScheduledJob LoadPending(string id)
    {
        return TryLoadPending(id) ?? throw new JobNotFoundException($"Job '{id}' was not found.");
    }

    public ScheduledJob? TryLoadPending(string id)
    {
        string path = PendingPath(id);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            ScheduledJob? job = JsonSerializer.Deserialize<ScheduledJob>(File.ReadAllText(path), JsonOptions);
            return job ?? throw new JobStoreException($"Job '{id}' is empty or invalid.");
        }
        catch (NextLogonExecException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new JobStoreException($"Failed to load job '{id}'.", ex);
        }
    }

    public bool PendingExists(string id)
    {
        try
        {
            return File.Exists(PendingPath(id));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new JobStoreException($"Failed to query pending job '{id}'.", ex);
        }
    }

    public bool HistoryExists(string id)
    {
        try
        {
            return File.Exists(HistoryPath(id));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new JobStoreException($"Failed to query history for job '{id}'.", ex);
        }
    }

    public IReadOnlyList<string> ListPendingIds()
    {
        try
        {
            if (!Directory.Exists(storeDirectory))
            {
                return Array.Empty<string>();
            }

            return Directory.EnumerateFiles(storeDirectory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(static id => !string.IsNullOrWhiteSpace(id))
                .Select(static id => id!)
                .OrderBy(static id => id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new JobStoreException("Failed to list pending jobs.", ex);
        }
    }

    public void DeletePending(string id)
    {
        try
        {
            string path = PendingPath(id);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new JobStoreException($"Failed to delete pending job '{id}'.", ex);
        }
    }

    public void SaveHistory(ScheduledJob job, string status, LaunchResult? result)
    {
        try
        {
            Directory.CreateDirectory(HistoryDirectory);
            JobHistoryRecord record = new(job, status, DateTimeOffset.UtcNow, result);
            File.WriteAllText(HistoryPath(job.Id), JsonSerializer.Serialize(record, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new JobStoreException($"Failed to write history for job '{job.Id}'.", ex);
        }
    }

    private string PendingPath(string id)
    {
        return Path.Combine(storeDirectory, id + ".json");
    }

    private string HistoryPath(string id)
    {
        return Path.Combine(HistoryDirectory, id + ".json");
    }

    private string HistoryDirectory => Path.Combine(storeDirectory, "history");

    private sealed record JobHistoryRecord(ScheduledJob Job, string Status, DateTimeOffset RecordedAtUtc, LaunchResult? Result);
}
