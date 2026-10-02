using System.Diagnostics;

namespace Thedeletr.Core;

public sealed record DeletionResult(
    int Deleted,
    int Failed,
    int Skipped,
    long BytesDeleted,
    int FoldersRemoved,
    int FoldersFailed,
    TimeSpan Duration,
    bool Cancelled);

public interface IDeletionObserver
{
    void FileDeleted(Candidate candidate) { }
    void FileFailed(Candidate candidate, string reason) { }
    void FileSkipped(Candidate candidate, string reason) { }
    void FolderRemoved(string path) { }
    void FolderFailed(string path, string reason) { }
}

public sealed class FileDeleter(IFileRemover remover, DateTime cutoff)
{
    public DeletionResult Delete(
        IReadOnlyList<Candidate> candidates,
        string rootFolder,
        IDeletionObserver? observer = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        int deleted = 0, failed = 0, skipped = 0, foldersRemoved = 0, foldersFailed = 0;
        long bytes = 0;
        var cancelled = false;
        var touchedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            try
            {
                var file = new FileInfo(candidate.FullPath);
                if (!file.Exists)
                {
                    skipped++;
                    observer?.FileSkipped(candidate, "file no longer exists");
                    continue;
                }

                // The file may have changed between scan and deletion (e.g. while waiting for confirmation).
                if (file.LastWriteTime > cutoff)
                {
                    skipped++;
                    observer?.FileSkipped(candidate, $"modified after scan ({file.LastWriteTime:yyyy-MM-dd HH:mm:ss})");
                    continue;
                }

                if (file.IsReadOnly)
                {
                    failed++;
                    observer?.FileFailed(candidate, "file is read-only");
                    continue;
                }

                remover.DeleteFile(file.FullName);
                deleted++;
                bytes += candidate.Length;
                touchedFolders.Add(file.DirectoryName!);
                observer?.FileDeleted(candidate);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                failed++;
                observer?.FileFailed(candidate, ex.Message);
            }
        }

        if (!cancelled)
        {
            foreach (var folder in GetFoldersToCheck(touchedFolders, rootFolder))
            {
                try
                {
                    if (!Directory.Exists(folder) || Directory.EnumerateFileSystemEntries(folder).Any())
                    {
                        continue;
                    }

                    remover.DeleteDirectory(folder);
                    foldersRemoved++;
                    observer?.FolderRemoved(folder);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    foldersFailed++;
                    observer?.FolderFailed(folder, ex.Message);
                }
            }
        }

        stopwatch.Stop();
        return new DeletionResult(deleted, failed, skipped, bytes, foldersRemoved, foldersFailed, stopwatch.Elapsed, cancelled);
    }

    /// <summary>
    /// Folders that contained deleted files plus their ancestors (root excluded), deepest first.
    /// </summary>
    internal static IEnumerable<string> GetFoldersToCheck(IEnumerable<string> touchedFolders, string rootFolder)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootFolder));
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var touched in touchedFolders)
        {
            var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(touched));
            while (current.Length > root.Length
                   && current.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                   && result.Add(current))
            {
                current = Path.GetDirectoryName(current)!;
            }
        }

        return result.OrderByDescending(f => f.Count(c => c == Path.DirectorySeparatorChar))
            .ThenBy(f => f, StringComparer.OrdinalIgnoreCase);
    }
}
