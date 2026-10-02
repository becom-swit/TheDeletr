using Serilog;
using Thedeletr.Core;

namespace Thedeletr.Logging;

public sealed class AuditObserver(ILogger logger, string mode) : IScanObserver, IDeletionObserver
{
    public void CandidateFound(Candidate candidate) =>
        logger.Information("Candidate {Path} (modified {LastWriteTime:yyyy-MM-dd HH:mm:ss}, {Size} bytes)",
            candidate.FullPath, candidate.LastWriteTime, candidate.Length);

    public void Excluded(string path, bool isDirectory) =>
        logger.Information("Excluded {Kind} {Path}", isDirectory ? "folder" : "file", path);

    public void Error(ScanError error) =>
        logger.Warning("Scan error {Path}: {Message}", error.Path, error.Message);

    public void FileDeleted(Candidate candidate) =>
        logger.Information("Deleted ({Mode}) {Path} (modified {LastWriteTime:yyyy-MM-dd HH:mm:ss}, {Size} bytes)",
            mode, candidate.FullPath, candidate.LastWriteTime, candidate.Length);

    public void FileFailed(Candidate candidate, string reason) =>
        logger.Error("Delete failed {Path}: {Reason}", candidate.FullPath, reason);

    public void FileSkipped(Candidate candidate, string reason) =>
        logger.Warning("Skipped {Path}: {Reason}", candidate.FullPath, reason);

    public void FolderRemoved(string path) =>
        logger.Information("Removed empty folder ({Mode}) {Path}", mode, path);

    public void FolderFailed(string path, string reason) =>
        logger.Error("Removing empty folder failed {Path}: {Reason}", path, reason);
}
