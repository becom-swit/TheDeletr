using Thedeletr.Core;

namespace Thedeletr.Tests;

public class FileDeleterTests : IDisposable
{
    private static readonly DateTime Cutoff = new(2024, 1, 1);
    private readonly TempTree _tree = new();

    public void Dispose() => _tree.Dispose();

    private DeletionResult DeleteAllCandidates()
    {
        var candidates = new CandidateScanner(new ExclusionMatcher([], _tree.Root), Cutoff).Scan(_tree.Root).Candidates;
        return new FileDeleter(new PermanentRemover(), Cutoff).Delete(candidates, _tree.Root);
    }

    [Fact]
    public void DeletesCandidates_AndRemovesFoldersThatBecameEmpty()
    {
        var old = _tree.File(@"a\b\old.txt", Cutoff.AddDays(-1));
        var newer = _tree.File(@"c\new.txt", Cutoff.AddDays(1));
        _tree.File(@"c\d\old.txt", Cutoff.AddDays(-1));

        var result = DeleteAllCandidates();

        Assert.Equal(2, result.Deleted);
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(newer));
        Assert.False(Directory.Exists(Path.Combine(_tree.Root, "a")));
        Assert.False(Directory.Exists(Path.Combine(_tree.Root, "c", "d")));
        Assert.True(Directory.Exists(Path.Combine(_tree.Root, "c")));
        Assert.True(Directory.Exists(_tree.Root));
        Assert.Equal(3, result.FoldersRemoved);
    }

    [Fact]
    public void KeepsFoldersThatWereAlreadyEmpty()
    {
        _tree.File(@"a\old.txt", Cutoff.AddDays(-1));
        var empty = _tree.Folder(@"a\empty");
        var otherEmpty = _tree.Folder("otherEmpty");

        var result = DeleteAllCandidates();

        Assert.Equal(1, result.Deleted);
        Assert.True(Directory.Exists(empty));
        Assert.True(Directory.Exists(otherEmpty));
        Assert.Equal(0, result.FoldersRemoved);
    }

    [Fact]
    public void ReadOnlyFile_CountsAsFailed()
    {
        var readOnly = _tree.File(@"a\ro.txt", Cutoff.AddDays(-1), readOnly: true);

        var result = DeleteAllCandidates();

        Assert.Equal(0, result.Deleted);
        Assert.Equal(1, result.Failed);
        Assert.True(File.Exists(readOnly));
    }

    [Fact]
    public void FileModifiedAfterScan_IsSkipped()
    {
        var path = _tree.File("old.txt", Cutoff.AddDays(-1));
        var candidates = new CandidateScanner(new ExclusionMatcher([], _tree.Root), Cutoff).Scan(_tree.Root).Candidates;
        File.SetLastWriteTime(path, Cutoff.AddDays(1));

        var result = new FileDeleter(new PermanentRemover(), Cutoff).Delete(candidates, _tree.Root);

        Assert.Equal(1, result.Skipped);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void MissingFile_IsSkipped()
    {
        var path = _tree.File("old.txt", Cutoff.AddDays(-1));
        var candidates = new CandidateScanner(new ExclusionMatcher([], _tree.Root), Cutoff).Scan(_tree.Root).Candidates;
        File.Delete(path);

        var result = new FileDeleter(new PermanentRemover(), Cutoff).Delete(candidates, _tree.Root);

        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public void GetFoldersToCheck_ReturnsAncestorsBelowRoot_DeepestFirst()
    {
        var root = Path.Combine(Path.GetTempPath(), "r");

        var folders = FileDeleter.GetFoldersToCheck(
            [Path.Combine(root, "a", "b", "c"), Path.Combine(root, "x"), root], root).ToList();

        Assert.Equal(
            [Path.Combine(root, "a", "b", "c"), Path.Combine(root, "a", "b"), Path.Combine(root, "a"), Path.Combine(root, "x")],
            folders);
    }
}
