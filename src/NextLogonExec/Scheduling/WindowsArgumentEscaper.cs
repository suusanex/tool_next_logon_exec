using System.Text;

namespace NextLogonExec.Scheduling;

public static class WindowsArgumentEscaper
{
    public static string Join(IEnumerable<string> arguments)
    {
        return string.Join(" ", arguments.Select(Escape));
    }

    public static string Escape(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        if (!argument.Any(static c => char.IsWhiteSpace(c) || c == '"'))
        {
            return argument;
        }

        StringBuilder builder = new();
        builder.Append('"');
        int backslashes = 0;

        foreach (char c in argument)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            if (c == '"')
            {
                builder.Append('\\', backslashes * 2 + 1);
                builder.Append('"');
                backslashes = 0;
                continue;
            }

            builder.Append('\\', backslashes);
            backslashes = 0;
            builder.Append(c);
        }

        builder.Append('\\', backslashes * 2);
        builder.Append('"');
        return builder.ToString();
    }
}
