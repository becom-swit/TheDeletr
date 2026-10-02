namespace Thedeletr.Options;

public sealed record DeletionOptions
{
    public required DirectoryInfo Root { get; init; }
    public required DateTime Cutoff { get; init; }
    public bool DryRun { get; init; }
    public IReadOnlyList<string> Excluded { get; init; } = [];
    public FileInfo? ExcludedFile { get; init; }
    public bool Yes { get; init; }
    public bool RecycleBin { get; init; }
    public bool Verbose { get; init; }
    public required DirectoryInfo LogFolder { get; init; }
}
