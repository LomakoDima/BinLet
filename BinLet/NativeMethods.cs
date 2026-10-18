using System.Runtime.InteropServices;

namespace BinLet;

/// <summary>P/Invoke declarations for the Shell APIs Binlet uses.</summary>
internal static partial class NativeMethods
{
    // SHEmptyRecycleBin flags
    public const uint SHERB_NOCONFIRMATION = 0x00000001;

    // SHChangeNotifyRegister sources / events
    public const int SHCNRF_InterruptLevel = 0x0001;
    public const int SHCNRF_ShellLevel = 0x0002;
    public const int SHCNE_ALLEVENTS = 0x7FFFFFFF;

    // HRESULTs we treat specially
    public const int E_UNEXPECTED = unchecked((int)0x8000FFFF);
    public const int HRESULT_ERROR_CANCELLED = unchecked((int)0x800704C7);

    /// <summary>FOLDERID_RecycleBinFolder</summary>
    public static readonly Guid FOLDERID_RecycleBinFolder = new("B7534046-3ECB-4C18-BE4E-64CD4CB7D6AC");

    // Natural alignment: 24 bytes on x64 (SHQueryRecycleBin rejects a 20-byte packed layout with E_INVALIDARG).
    [StructLayout(LayoutKind.Sequential)]
    public struct SHQUERYRBINFO
    {
        public uint cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SHChangeNotifyEntry
    {
        public IntPtr pidl;
        public int fRecursive; // BOOL
    }

    [LibraryImport("shell32.dll", EntryPoint = "SHQueryRecycleBinW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [LibraryImport("shell32.dll", EntryPoint = "SHEmptyRecycleBinW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    [LibraryImport("shell32.dll")]
    public static partial int SHGetKnownFolderIDList(in Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppidl);

    [LibraryImport("shell32.dll")]
    public static partial uint SHChangeNotifyRegister(IntPtr hwnd, int fSources, int fEvents, uint wMsg, int cEntries, in SHChangeNotifyEntry pshcne);

    [LibraryImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SHChangeNotifyDeregister(uint hNotify);

    [LibraryImport("ole32.dll")]
    public static partial void CoTaskMemFree(IntPtr pv);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(IntPtr hWnd);
}
