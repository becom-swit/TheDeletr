namespace Thedeletr.Core;

public sealed record Candidate(string FullPath, DateTime LastWriteTime, long Length);

public sealed record ScanResult(
    IReadOnlyList<Candidate> Candidates,
    int FilesScanned,
    int FoldersScanned,
    IReadOnlyList<string> ExcludedPaths,
    IReadOnlyList<ScanError> Errors);

public sealed record ScanError(string Path, string Message);

public interface IScanObserver
{
    void FolderEntered(string path) { }
    void CandidateFound(Candidate candidate) { }
    void Excluded(string path, bool isDirectory) { }
    void Error(ScanError error) { }
}

public sealed class CandidateScanner(ExclusionMatcher exclusions, DateTime cutoff)
{
    private static readonly EnumerationOptions Enumeration = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = false,
        AttributesToSkip = 0,
        ReturnSpecialDirectories = false,
    };

    public ScanResult Scan(string rootFolder, IScanObserver? observer = null, CancellationToken cancellationToken = default)
    {
        var candidates = new List<Candidate>();
        var excluded = new List<string>();
        var errors = new List<ScanError>();
        var filesScanned = 0;
        var foldersScanned = 0;

        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(rootFolder));

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var folder = pending.Pop();
            foldersScanned++;
            observer?.FolderEntered(folder);

            List<FileSystemInfo> entries;
            try
            {
                entries = new DirectoryInfo(folder).EnumerateFileSystemInfos("*", Enumeration).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                AddError(new ScanError(folder, ex.Message));
                continue;
            }

            var subFolders = new List<string>();
            foreach (var entry in entries)
            {
                var isDirectory = entry is DirectoryInfo;

                if (exclusions.IsExcluded(entry.FullName))
                {
                    excluded.Add(entry.FullName);
                    observer?.Excluded(entry.FullName, isDirectory);
                    continue;
                }

                if (isDirectory)
                {
                    // Never follow junctions/symlinks: avoids loops and deleting outside the root folder.
                    if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        excluded.Add(entry.FullName);
                        observer?.Excluded(entry.FullName, true);
                        continue;
                    }

                    subFolders.Add(entry.FullName);
                    continue;
                }

                filesScanned++;
                try
                {
                    var file = (FileInfo)entry;
                    if (file.LastWriteTime <= cutoff)
                    {
                        var candidate = new Candidate(file.FullName, file.LastWriteTime, file.Length);
                        candidates.Add(candidate);
                        observer?.CandidateFound(candidate);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    AddError(new ScanError(entry.FullName, ex.Message));
                }
            }

            // Push in reverse so folders are processed in alphabetical order.
            foreach (var sub in subFolders.OrderByDescending(s => s, StringComparer.OrdinalIgnoreCase))
            {
                pending.Push(sub);
            }
        }

        return new ScanResult(candidates, filesScanned, foldersScanned, excluded, errors);

        void AddError(ScanError error)
        {
            errors.Add(error);
            observer?.Error(error);
        }
    }
}
