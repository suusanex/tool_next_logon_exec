namespace NextLogonExec.Commands;

public static class JobId
{
    public static void Validate(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidArgumentsException("Job ID must not be empty.");
        }

        if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || id.Contains('\\') || id.Contains('/'))
        {
            throw new InvalidArgumentsException("Job ID contains characters that cannot be used in a file name.");
        }
    }
}
