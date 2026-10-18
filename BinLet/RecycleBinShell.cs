using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BinLet;

/// <summary>Snapshot of the combined state of the Recycle Bins on all drives.</summary>
internal readonly record struct RecycleBinState(long ItemCount, long SizeBytes)
{
    public bool IsEmpty => ItemCount <= 0;
}

/// <summary>Thin wrapper over the native Shell Recycle Bin APIs. No files are touched directly.</summary>
internal static class RecycleBinShell
{
    /// <summary>
    /// Queries all drives at once (SHQueryRecycleBin with a null root path).
    /// Returns null if the Shell reports a failure.
    /// </summary>
    public static RecycleBinState? TryQuery()
    {
        var info = new NativeMethods.SHQUERYRBINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.SHQUERYRBINFO>() };
        int hr = NativeMethods.SHQueryRecycleBin(null, ref info);
        if (hr < 0)
        {
            Debug.WriteLine($"SHQueryRecycleBin failed: 0x{hr:X8}");
            return null;
        }
        return new RecycleBinState(info.i64NumItems, info.i64Size);
    }

    /// <summary>Opens the native Recycle Bin folder in File Explorer.</summary>
    public static void Open()
    {
        using var process = Process.Start(new ProcessStartInfo("explorer.exe", "shell:RecycleBinFolder")
        {
            UseShellExecute = true,
        });
    }

    /// <summary>
    /// Empties all Recycle Bins through the Shell. The caller must already have obtained the
    /// user's confirmation. Runs on a dedicated STA thread so the UI stays responsive; the
    /// Shell shows its own progress UI for large bins. Returns the HRESULT.
    /// </summary>
    public static Task<int> EmptyAsync()
    {
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                tcs.SetResult(NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null, NativeMethods.SHERB_NOCONFIRMATION));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "Binlet.EmptyRecycleBin",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }
}
