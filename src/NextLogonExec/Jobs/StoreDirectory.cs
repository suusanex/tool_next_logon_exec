namespace NextLogonExec.Jobs;

public static class StoreDirectory
{
    public static string Resolve(string? overrideDirectory)
    {
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            return Path.GetFullPath(overrideDirectory);
        }

        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(programData))
        {
            programData = Environment.GetEnvironmentVariable("ProgramData")
                ?? throw new JobStoreException("ProgramData path is not available.");
        }

        return Path.Combine(programData, "NextLogonExec", "jobs");
    }
}
