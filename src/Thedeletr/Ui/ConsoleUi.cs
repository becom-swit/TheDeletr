using System.Diagnostics;
using Spectre.Console;
using Thedeletr.Core;
using Thedeletr.Options;

namespace Thedeletr.Ui;

public sealed class ConsoleUi(IAnsiConsole console)
{
    public void PrintHeader(DeletionOptions options, IReadOnlyList<string> exclusions, string mode)
    {
        var grid = new Grid().AddColumn(new GridColumn().NoWrap()).AddColumn();
        grid.AddRow("[grey]Root[/]", Markup.Escape(options.Root.FullName));
        grid.AddRow("[grey]Delete files modified on/before[/]", $"{options.Cutoff:yyyy-MM-dd HH:mm:ss}");
        grid.AddRow("[grey]Mode[/]", options.DryRun ? "[yellow]dry run[/]" : Markup.Escape(mode));
        grid.AddRow("[grey]Exclusions[/]", exclusions.Count == 0 ? "-" : Markup.Escape(string.Join(", ", exclusions)));
        grid.AddRow("[grey]Audit log[/]", Markup.Escape(options.LogFolder.FullName));
        console.Write(new Panel(grid).Header($"[bold]{Logo.AppName}[/]").RoundedBorder());
    }

    public void Warning(string message) => console.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(message)}");

    public void Error(string message) => console.MarkupLine($"[red]Error:[/] {Markup.Escape(message)}");

    public void Info(string message) => console.MarkupLine(Markup.Escape(message));

    public ScanResult Scan(CandidateScanner scanner, string root, IScanObserver auditObserver, CancellationToken cancellationToken) =>
        console.Status().Spinner(Spinner.Known.Dots).Start("Scanning...", ctx =>
            scanner.Scan(root, new ScanStatusObserver(ctx, auditObserver), cancellationToken));

    public void PrintCandidates(IReadOnlyList<Candidate> candidates)
    {
        // Raw writer: Spectre would wrap long paths at console width, which breaks piping/copying.
        var writer = console.Profile.Out.Writer;
        foreach (var candidate in candidates)
        {
            writer.WriteLine(candidate.FullPath);
        }
    }

    public void PrintScanSummary(ScanResult result)
    {
        var size = FormatBytes(result.Candidates.Sum(c => c.Length));
        console.MarkupLine(
            $"[bold]{result.Candidates.Count}[/] candidate(s) ({size}) found in {result.FilesScanned} file(s) / {result.FoldersScanned} folder(s), "
            + $"{result.ExcludedPaths.Count} excluded.");

        foreach (var error in result.Errors)
        {
            Warning($"{error.Path}: {error.Message}");
        }
    }

    public bool CanPrompt => console.Profile.Capabilities.Interactive;

    public bool Confirm(int count) =>
        console.Confirm($"[red]Delete {count} file(s)?[/]", defaultValue: false);

    public DeletionResult Delete(FileDeleter deleter, IReadOnlyList<Candidate> candidates, string root,
        IDeletionObserver auditObserver, CancellationToken cancellationToken)
    {
        var observer = new ProgressObserver(auditObserver);
        var result = console.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new ElapsedTimeColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .Start(ctx =>
            {
                observer.Task = ctx.AddTask("Deleting", maxValue: Math.Max(1, candidates.Count));
                var deletionResult = deleter.Delete(candidates, root, observer, cancellationToken);
                observer.Task.Value = observer.Task.MaxValue;
                return deletionResult;
            });

        foreach (var problem in observer.Problems)
        {
            Warning(problem);
        }

        return result;
    }

    public void PrintReport(DeletionResult result)
    {
        var color = result.Failed > 0 || result.FoldersFailed > 0 ? "yellow" : "green";
        console.MarkupLine(
            $"[{color}]{result.Deleted} file(s) deleted ({FormatBytes(result.BytesDeleted)}) in {DurationFormatter.Format(result.Duration)}.[/]");

        if (result.FoldersRemoved > 0)
        {
            console.MarkupLine($"{result.FoldersRemoved} empty folder(s) removed.");
        }

        if (result.Skipped > 0)
        {
            console.MarkupLine($"[yellow]{result.Skipped} file(s) skipped.[/]");
        }

        if (result.Failed > 0 || result.FoldersFailed > 0)
        {
            console.MarkupLine($"[red]{result.Failed} file(s) and {result.FoldersFailed} folder(s) failed. See audit log for details.[/]");
        }

        if (result.Cancelled)
        {
            console.MarkupLine("[yellow]Cancelled by user.[/]");
        }
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.##} {units[unit]}";
    }

    private sealed class ScanStatusObserver(StatusContext ctx, IScanObserver inner) : IScanObserver
    {
        private readonly Stopwatch _throttle = Stopwatch.StartNew();
        private int _folders;
        private int _candidates;

        public void FolderEntered(string path)
        {
            _folders++;
            inner.FolderEntered(path);
            if (_throttle.ElapsedMilliseconds >= 100)
            {
                _throttle.Restart();
                ctx.Status($"Scanning... {_folders} folder(s), {_candidates} candidate(s) [grey]{Markup.Escape(Truncate(path, 60))}[/]");
            }
        }

        public void CandidateFound(Candidate candidate)
        {
            _candidates++;
            inner.CandidateFound(candidate);
        }

        public void Excluded(string path, bool isDirectory) => inner.Excluded(path, isDirectory);

        public void Error(ScanError error) => inner.Error(error);

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : "..." + value[^(max - 3)..];
    }

    private sealed class ProgressObserver(IDeletionObserver inner) : IDeletionObserver
    {
        public ProgressTask? Task { get; set; }

        public List<string> Problems { get; } = [];

        public void FileDeleted(Candidate candidate)
        {
            inner.FileDeleted(candidate);
            Task?.Increment(1);
        }

        public void FileFailed(Candidate candidate, string reason)
        {
            inner.FileFailed(candidate, reason);
            Problems.Add($"Failed {candidate.FullPath}: {reason}");
            Task?.Increment(1);
        }

        public void FileSkipped(Candidate candidate, string reason)
        {
            inner.FileSkipped(candidate, reason);
            Problems.Add($"Skipped {candidate.FullPath}: {reason}");
            Task?.Increment(1);
        }

        public void FolderRemoved(string path) => inner.FolderRemoved(path);

        public void FolderFailed(string path, string reason)
        {
            inner.FolderFailed(path, reason);
            Problems.Add($"Removing folder failed {path}: {reason}");
        }
    }
}
