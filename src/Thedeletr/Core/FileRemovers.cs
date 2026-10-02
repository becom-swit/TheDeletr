using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Thedeletr.Core;

public interface IFileRemover
{
    string Mode { get; }
    void DeleteFile(string path);
    void DeleteDirectory(string path);
}

public sealed class PermanentRemover : IFileRemover
{
    public string Mode => "permanent";

    public void DeleteFile(string path) => File.Delete(path);

    public void DeleteDirectory(string path) => Directory.Delete(path, recursive: false);
}

/// <summary>
/// Moves files/folders to the recycle bin via SHFileOperation without showing any UI.
/// Note: on network shares Windows has no recycle bin and deletes permanently.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RecycleBinRemover : IFileRemover
{
    private const uint FoDelete = 0x0003;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoErrorUi = 0x0400;

    public string Mode => "recycle bin";

    public void DeleteFile(string path) => Recycle(path);

    public void DeleteDirectory(string path) => Recycle(path);

    private static void Recycle(string path)
    {
        var operation = new ShFileOpStruct
        {
            wFunc = FoDelete,
            pFrom = path + '\0' + '\0',
            fFlags = (ushort)(FofSilent | FofNoConfirmation | FofAllowUndo | FofNoErrorUi),
        };

        var result = SHFileOperation(ref operation);
        if (result != 0)
        {
            throw new IOException($"Moving to recycle bin failed (SHFileOperation error 0x{result:X}).");
        }

        if (operation.fAnyOperationsAborted)
        {
            throw new IOException("Moving to recycle bin was aborted.");
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref ShFileOpStruct lpFileOp);
}
