using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Globalization;
using Spectre.Console;
using Thedeletr.Logging;
using Thedeletr.Options;

namespace Thedeletr.Cli;

internal sealed class LogoHelpAction(HelpAction inner) : SynchronousCommandLineAction
{
    public override int Invoke(ParseResult parseResult)
    {
        Ui.Logo.Write(AnsiConsole.Console);
        return inner.Invoke(parseResult);
    }
}

public static class CommandBuilder
{
    private static readonly string[] IsoDateFormats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm",
        "yyyy-MM-ddTHH:mm:ss",
    ];

    public static RootCommand Build(Func<DeletionOptions, CancellationToken, Task<int>> run)
    {
        var root = new Argument<DirectoryInfo>("root")
        {
            Description = "Root folder to search (local path or network share, e.g. \\\\server\\share\\folder).",
        }.AcceptExistingOnly();

        var deletion = new Option<DateTime>("--deletion", "-d")
        {
            Description = "Cutoff date. Files last modified on or before this date are deleted. "
                          + "Format yyyy-MM-dd [HH:mm[:ss]] or current culture. A date without time means 00:00:00.",
            HelpName = "date",
            Required = true,
            CustomParser = ParseDate,
        };

        var dryRun = new Option<bool>("--dryRun", "-dr")
        {
            Description = "Only list the files that would be deleted.",
        };

        var excluded = new Option<string[]>("--excluded", "-e")
        {
            Description = "Comma or semicolon separated folders/files to exclude. Supports full paths, "
                          + "paths relative to root, names and wildcards (*name, name*, *name*). Can be repeated.",
            HelpName = "patterns",
            AllowMultipleArgumentsPerToken = false,
        };

        var excludedFile = new Option<FileInfo>("--excludedFile", "-ef")
        {
            Description = "File with exclusion patterns separated by new line, comma or semicolon.",
            HelpName = "file",
        }.AcceptExistingOnly();

        var yes = new Option<bool>("--yes", "-y")
        {
            Description = "Delete without asking for confirmation.",
        };

        var bin = new Option<bool>("--bin", "-b")
        {
            Description = "Move files to the recycle bin instead of deleting them permanently (Windows only, "
                          + "not supported by network shares).",
        };
        bin.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<bool>() && !OperatingSystem.IsWindows())
            {
                result.AddError("--bin is only supported on Windows.");
            }
        });

        var verbose = new Option<bool>("--verbose", "-v")
        {
            Description = "Do not list every candidate on the console (only the summary). The audit log is always complete.",
        };

        var logFolder = new Option<DirectoryInfo>("--logFolder", "-l")
        {
            Description = $"Folder for the audit log files (kept {AuditLog.RetainedFileCount} days). Default: 'logs' next to the executable.",
            HelpName = "folder",
            DefaultValueFactory = _ => AuditLog.DefaultFolder,
        };

        var command = new RootCommand($"{Ui.Logo.AppName} - deletes files older than a given date from a folder and its subfolders.")
        {
            root, deletion, dryRun, excluded, excludedFile, yes, bin, verbose, logFolder,
        };

        foreach (var option in command.Options)
        {
            if (option is HelpOption { Action: HelpAction helpAction } helpOption)
            {
                helpOption.Action = new LogoHelpAction(helpAction);
            }
        }

        command.SetAction((parseResult, cancellationToken) => run(new DeletionOptions
        {
            Root = parseResult.GetValue(root)!,
            Cutoff = parseResult.GetValue(deletion),
            DryRun = parseResult.GetValue(dryRun),
            Excluded = parseResult.GetValue(excluded) ?? [],
            ExcludedFile = parseResult.GetValue(excludedFile),
            Yes = parseResult.GetValue(yes),
            RecycleBin = parseResult.GetValue(bin),
            Verbose = parseResult.GetValue(verbose),
            LogFolder = parseResult.GetValue(logFolder) ?? AuditLog.DefaultFolder,
        }, cancellationToken));

        return command;
    }

    internal static bool TryParseDate(string value, out DateTime date) =>
        DateTime.TryParseExact(value.Trim(), IsoDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date)
        || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out date);

    private static DateTime ParseDate(ArgumentResult result)
    {
        var value = result.Tokens.Count > 0 ? result.Tokens[0].Value : string.Empty;
        if (TryParseDate(value, out var date))
        {
            return date;
        }

        result.AddError($"'{value}' is not a valid date. Use e.g. 2024-12-31 or \"2024-12-31 18:00\".");
        return default;
    }
}
