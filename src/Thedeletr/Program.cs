using Serilog;
using Spectre.Console;
using Thedeletr.Cli;
using Thedeletr.Core;
using Thedeletr.Logging;
using Thedeletr.Options;
using Thedeletr.Ui;

namespace Thedeletr;

public static class Program
{
    public const int ExitSuccess = 0;
    public const int ExitInvalidArguments = 1;
    public const int ExitCompletedWithFailures = 2;
    public const int ExitCancelled = 3;

    public static async Task<int> Main(string[] args)
    {
        // UTF-8 is needed for the logo and non-ASCII paths; restore the console's code page on exit.
        var originalEncoding = Console.OutputEncoding;
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch (IOException)
        {
            // No console attached (e.g. redirected output) - keep default.
        }

        try
        {
            var command = CommandBuilder.Build((options, ct) => Task.Run(() => Run(options, ct), CancellationToken.None));
            return await command.Parse(args).InvokeAsync();
        }
        finally
        {
            try
            {
                Console.OutputEncoding = originalEncoding;
            }
            catch (IOException)
            {
            }
        }
    }

    private static int Run(DeletionOptions options, CancellationToken cancellationToken)
    {
        var ui = new ConsoleUi(AnsiConsole.Console);
        Logo.Write(AnsiConsole.Console);

        IReadOnlyList<string> exclusions;
        try
        {
            exclusions = ExclusionParser.Parse(options.Excluded, options.ExcludedFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ui.Error($"Cannot read exclusion file: {ex.Message}");
            return ExitInvalidArguments;
        }

        Serilog.Core.Logger logger;
        try
        {
            logger = AuditLog.Create(options.LogFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ui.Error($"Cannot create audit log in '{options.LogFolder.FullName}': {ex.Message}");
            return ExitInvalidArguments;
        }

        using (logger)
        {
            try
            {
                return Execute(options, exclusions, ui, logger, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                logger.Warning("Run cancelled by user");
                ui.Warning("Cancelled by user.");
                return ExitCancelled;
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, "Unexpected error");
                ui.Error(ex.Message);
                return ExitCompletedWithFailures;
            }
        }
    }

    private static int Execute(DeletionOptions options, IReadOnlyList<string> exclusions, ConsoleUi ui, ILogger logger,
        CancellationToken cancellationToken)
    {
        var root = options.Root.FullName;
        IFileRemover remover = options.RecycleBin && OperatingSystem.IsWindows() ? new RecycleBinRemover() : new PermanentRemover();

        logger.Information(
            "Run started by {User} on {Machine}: Root={Root} Cutoff={Cutoff:yyyy-MM-dd HH:mm:ss} Mode={Mode} DryRun={DryRun} Yes={Yes} Verbose={Verbose} Exclusions={Exclusions} ExclusionFile={ExclusionFile}",
            $@"{Environment.UserDomainName}\{Environment.UserName}", Environment.MachineName, root, options.Cutoff, remover.Mode,
            options.DryRun, options.Yes, options.Verbose, exclusions, options.ExcludedFile?.FullName);

        ui.PrintHeader(options, exclusions, remover.Mode);

        if (options.Cutoff > DateTime.Now)
        {
            ui.Warning("The cutoff date is in the future - all files will be candidates.");
            logger.Warning("Cutoff date is in the future");
        }

        if (options.RecycleBin && !options.DryRun && IsNetworkPath(root))
        {
            ui.Warning("Network shares have no recycle bin - files will be deleted PERMANENTLY.");
            logger.Warning("Recycle bin requested on network path; files will be deleted permanently");
        }

        var audit = new AuditObserver(logger, remover.Mode);
        var scanner = new CandidateScanner(new ExclusionMatcher(exclusions, root), options.Cutoff);
        var scan = ui.Scan(scanner, root, audit, cancellationToken);

        if (!options.Verbose)
        {
            ui.PrintCandidates(scan.Candidates);
        }

        ui.PrintScanSummary(scan);
        logger.Information(
            "Scan finished: {Candidates} candidates ({Bytes} bytes), {Files} files and {Folders} folders scanned, {Excluded} excluded, {Errors} errors",
            scan.Candidates.Count, scan.Candidates.Sum(c => c.Length), scan.FilesScanned, scan.FoldersScanned,
            scan.ExcludedPaths.Count, scan.Errors.Count);

        var scanExitCode = scan.Errors.Count > 0 ? ExitCompletedWithFailures : ExitSuccess;

        if (scan.Candidates.Count == 0)
        {
            ui.Info("Nothing to delete.");
            logger.Information("Nothing to delete");
            return scanExitCode;
        }

        if (options.DryRun)
        {
            ui.Info("Dry run - no files were deleted.");
            logger.Information("Dry run finished, no files deleted");
            return scanExitCode;
        }

        if (!options.Yes)
        {
            if (!ui.CanPrompt)
            {
                ui.Error("Console is not interactive - use --yes to delete without confirmation.");
                logger.Warning("Aborted: non-interactive console without --yes");
                return ExitInvalidArguments;
            }

            if (!ui.Confirm(scan.Candidates.Count))
            {
                ui.Info("Aborted - no files were deleted.");
                logger.Information("Aborted by user at confirmation, no files deleted");
                return ExitSuccess;
            }

            logger.Information("Deletion confirmed by user");
        }
        else
        {
            logger.Information("Deletion confirmed by --yes");
        }

        var deleter = new FileDeleter(remover, options.Cutoff);
        var result = ui.Delete(deleter, scan.Candidates, root, audit, cancellationToken);
        ui.PrintReport(result);

        logger.Information(
            "Run finished: {Deleted} deleted ({Bytes} bytes), {Failed} failed, {Skipped} skipped, {FoldersRemoved} folders removed, {FoldersFailed} folders failed, duration {Duration}, cancelled {Cancelled}",
            result.Deleted, result.BytesDeleted, result.Failed, result.Skipped, result.FoldersRemoved, result.FoldersFailed,
            DurationFormatter.Format(result.Duration), result.Cancelled);

        if (result.Cancelled)
        {
            return ExitCancelled;
        }

        return result.Failed > 0 || result.FoldersFailed > 0 ? ExitCompletedWithFailures : scanExitCode;
    }

    private static bool IsNetworkPath(string path)
    {
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return true;
        }

        try
        {
            var driveRoot = Path.GetPathRoot(path);
            return driveRoot is not null && new DriveInfo(driveRoot).DriveType == DriveType.Network;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
