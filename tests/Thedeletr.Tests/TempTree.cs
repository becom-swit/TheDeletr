namespace Thedeletr.Tests;

/// <summary>Creates a temporary directory tree that is removed on dispose.</summary>
public sealed class TempTree : IDisposable
{
    public TempTree()
    {
        Root = Path.Combine(Path.GetTempPath(), "thedeletr-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string File(string relativePath, DateTime lastWrite, bool readOnly = false)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, "x");
        System.IO.File.SetLastWriteTime(path, lastWrite);
        if (readOnly)
        {
            System.IO.File.SetAttributes(path, FileAttributes.ReadOnly);
        }

        return path;
    }

    public string Folder(string relativePath) => Directory.CreateDirectory(Path.Combine(Root, relativePath)).FullName;

    public void Dispose()
    {
        if (!Directory.Exists(Root))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
        {
            System.IO.File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(Root, recursive: true);
    }
}
