using System.Text.Json;
using NextLogonExec.ProcessLaunching;

namespace NextLogonExec.Diagnostics;

public static class ResultWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static void Write(string path, string jobId, LaunchResult result)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, JsonSerializer.Serialize(new ResultRecord(jobId, result), JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            throw new JobStoreException($"Failed to write result for job '{jobId}'.", ex);
        }
    }

    private sealed record ResultRecord(string JobId, LaunchResult Result);
}
