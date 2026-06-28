namespace NextLogonExec.Commands;

public static class CommandLineParser
{
    public static CommandLine Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || args[0] is "-h" or "--help" or "help")
        {
            return new HelpCommandLine();
        }

        string command = args[0].ToLowerInvariant();
        string[] rest = args.Skip(1).ToArray();

        return command switch
        {
            "schedule" => ParseSchedule(rest),
            "run" => new RunCommandLine(ParseIdAndStore<RunOptions>(rest, static (id, store) => new RunOptions(id, store))),
            "cancel" => new CancelCommandLine(ParseIdAndStore<CancelOptions>(rest, static (id, store) => new CancelOptions(id, store))),
            "status" => new StatusCommandLine(ParseIdAndStore<StatusOptions>(rest, static (id, store) => new StatusOptions(id, store))),
            "list" => new ListCommandLine(ParseList(rest)),
            _ => throw new InvalidArgumentsException($"Unknown command '{args[0]}'.")
        };
    }

    private static ScheduleCommandLine ParseSchedule(IReadOnlyList<string> args)
    {
        int separator = FindSeparator(args);
        if (separator < 0)
        {
            throw new InvalidArgumentsException("schedule requires '--' before the command to run.");
        }

        string[] optionArgs = args.Take(separator).ToArray();
        string[] commandArgs = args.Skip(separator + 1).ToArray();
        if (commandArgs.Length == 0)
        {
            throw new InvalidArgumentsException("schedule requires an executable after '--'.");
        }

        string? id = null;
        string? cwd = null;
        string? stdout = null;
        string? stderr = null;
        string? result = null;
        string? storeDir = null;
        int delay = 0;
        int? timeout = null;
        bool elevated = false;
        bool wait = false;
        bool requireNewBoot = false;
        bool replace = false;

        for (int i = 0; i < optionArgs.Length; i++)
        {
            string arg = optionArgs[i];
            switch (arg)
            {
                case "--id":
                    id = ReadValue(optionArgs, ref i, arg);
                    break;
                case "--cwd":
                    cwd = ReadValue(optionArgs, ref i, arg);
                    break;
                case "--elevated":
                    elevated = true;
                    break;
                case "--delay":
                    delay = ParseNonNegativeInt(ReadValue(optionArgs, ref i, arg), arg);
                    break;
                case "--wait":
                    wait = true;
                    break;
                case "--timeout":
                    timeout = ParsePositiveInt(ReadValue(optionArgs, ref i, arg), arg);
                    break;
                case "--stdout":
                    stdout = ReadValue(optionArgs, ref i, arg);
                    break;
                case "--stderr":
                    stderr = ReadValue(optionArgs, ref i, arg);
                    break;
                case "--result":
                    result = ReadValue(optionArgs, ref i, arg);
                    break;
                case "--store-dir":
                    storeDir = ReadValue(optionArgs, ref i, arg);
                    break;
                case "--require-new-boot":
                    requireNewBoot = true;
                    break;
                case "--replace":
                    replace = true;
                    break;
                default:
                    throw new InvalidArgumentsException($"Unknown schedule option '{arg}'.");
            }
        }

        if (!wait && timeout is not null)
        {
            throw new InvalidArgumentsException("--timeout requires --wait.");
        }

        if (!wait && (!string.IsNullOrWhiteSpace(stdout) || !string.IsNullOrWhiteSpace(stderr)))
        {
            throw new InvalidArgumentsException("--stdout and --stderr require --wait in v1.");
        }

        ScheduleOptions options = new(
            id,
            cwd,
            elevated,
            delay,
            wait,
            timeout,
            stdout,
            stderr,
            result,
            storeDir,
            requireNewBoot,
            replace,
            commandArgs[0],
            commandArgs.Skip(1).ToArray());

        return new ScheduleCommandLine(options);
    }

    private static T ParseIdAndStore<T>(IReadOnlyList<string> args, Func<string, string?, T> factory)
    {
        string? id = null;
        string? storeDir = null;

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--id":
                    id = ReadValue(args, ref i, arg);
                    break;
                case "--store-dir":
                    storeDir = ReadValue(args, ref i, arg);
                    break;
                default:
                    throw new InvalidArgumentsException($"Unknown option '{arg}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidArgumentsException("--id is required.");
        }

        JobId.Validate(id);
        return factory(id, storeDir);
    }

    private static ListOptions ParseList(IReadOnlyList<string> args)
    {
        string? storeDir = null;
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i] != "--store-dir")
            {
                throw new InvalidArgumentsException($"Unknown list option '{args[i]}'.");
            }

            storeDir = ReadValue(args, ref i, args[i]);
        }

        return new ListOptions(storeDir);
    }

    private static int FindSeparator(IReadOnlyList<string> args)
    {
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i] == "--")
            {
                return i;
            }
        }

        return -1;
    }

    private static string ReadValue(IReadOnlyList<string> args, ref int index, string option)
    {
        if (index + 1 >= args.Count)
        {
            throw new InvalidArgumentsException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }

    private static int ParseNonNegativeInt(string value, string option)
    {
        if (!int.TryParse(value, out int parsed) || parsed < 0)
        {
            throw new InvalidArgumentsException($"{option} must be a non-negative integer.");
        }

        return parsed;
    }

    private static int ParsePositiveInt(string value, string option)
    {
        if (!int.TryParse(value, out int parsed) || parsed <= 0)
        {
            throw new InvalidArgumentsException($"{option} must be a positive integer.");
        }

        return parsed;
    }
}
