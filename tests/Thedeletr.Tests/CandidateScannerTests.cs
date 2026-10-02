using Thedeletr.Core;

namespace Thedeletr.Tests;

public class CandidateScannerTests : IDisposable
{
    private static readonly DateTime Cutoff = new(2024, 1, 1);
    private readonly TempTree _tree = new();

    public void Dispose() => _tree.Dispose();

    private ScanResult Scan(params string[] exclusions) =>
        new CandidateScanner(new ExclusionMatcher(exclusions, _tree.Root), Cutoff).Scan(_tree.Root);

    private static string[] Paths(ScanResult result) => result.Candidates.Select(c => c.FullPath).Order().ToArray();

    [Fact]
    public void IncludesFilesOlderOrEqualToCutoff_Recursively()
    {
        var old = _tree.File("old.txt", Cutoff.AddDays(-1));
        var equal = _tree.File(@"sub\deep\equal.txt", Cutoff);
        _tree.File(@"sub\new.txt", Cutoff.AddSeconds(1));

        var result = Scan();

        Assert.Equal(new[] { old, equal }.Order(), Paths(result));
        Assert.Equal(3, result.FilesScanned);
        Assert.Equal(3, result.FoldersScanned);
    }

    [Fact]
    public void ExcludedFolder_SkipsWholeSubtree()
    {
        var kept = _tree.File(@"a\old.txt", Cutoff.AddDays(-1));
        _tree.File(@"node_modules\old.txt", Cutoff.AddDays(-1));
        _tree.File(@"node_modules\x\old.txt", Cutoff.AddDays(-1));

        var result = Scan("node_modules");

        Assert.Equal([kept], Paths(result));
        Assert.Single(result.ExcludedPaths);
        Assert.Equal(2, result.FoldersScanned);
    }

    [Fact]
    public void ExcludedFile_SkipsOnlyThatFile()
    {
        var kept = _tree.File(@"a\old.txt", Cutoff.AddDays(-1));
        _tree.File(@"a\old.log", Cutoff.AddDays(-1));

        var result = Scan("*.log");

        Assert.Equal([kept], Paths(result));
    }

    [Fact]
    public void FullPathExclusion_SkipsFolder()
    {
        var kept = _tree.File(@"b\keep\old.txt", Cutoff.AddDays(-1));
        _tree.File(@"a\keep\old.txt", Cutoff.AddDays(-1));

        var result = Scan(Path.Combine(_tree.Root, "a", "keep"));

        Assert.Equal([kept], Paths(result));
    }

    [Fact]
    public void Cancellation_Throws()
    {
        _tree.File("old.txt", Cutoff.AddDays(-1));
        var scanner = new CandidateScanner(new ExclusionMatcher([], _tree.Root), Cutoff);

        Assert.Throws<OperationCanceledException>(() => scanner.Scan(_tree.Root, null, new CancellationToken(true)));
    }
}
