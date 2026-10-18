using System.Diagnostics;
using System.Windows.Forms;

namespace BinLet;

/// <summary>
/// Raises <see cref="Changed"/> when the Recycle Bin may have changed. Uses Shell change
/// notifications (debounced) plus a slow fallback timer in case notifications are missed.
/// </summary>
internal sealed class RecycleBinWatcher : IDisposable
{
    private const int WM_USER = 0x0400;
    private const int WM_BIN_CHANGED = WM_USER + 0x42;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private readonly MessageWindow _window;
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 300 };
    private readonly System.Windows.Forms.Timer _fallback = new() { Interval = 60_000 };
    private uint _registration;
    private IntPtr _pidl;

    public event Action? Changed;

    public RecycleBinWatcher()
    {
        _window = new MessageWindow(OnWindowMessage);
        _debounce.Tick += (_, _) => { _debounce.Stop(); Changed?.Invoke(); };
        _fallback.Tick += (_, _) => Changed?.Invoke();

        Register();
        _fallback.Start();
    }

    private void Register()
    {
        try
        {
            int hr = NativeMethods.SHGetKnownFolderIDList(NativeMethods.FOLDERID_RecycleBinFolder, 0, IntPtr.Zero, out _pidl);
            if (hr < 0 || _pidl == IntPtr.Zero)
            {
                Debug.WriteLine($"SHGetKnownFolderIDList failed: 0x{hr:X8}");
                return;
            }

            var entry = new NativeMethods.SHChangeNotifyEntry { pidl = _pidl, fRecursive = 1 };
            _registration = NativeMethods.SHChangeNotifyRegister(
                _window.Handle,
                NativeMethods.SHCNRF_ShellLevel | NativeMethods.SHCNRF_InterruptLevel,
                NativeMethods.SHCNE_ALLEVENTS,
                WM_BIN_CHANGED,
                1,
                in entry);

            if (_registration == 0)
                Debug.WriteLine("SHChangeNotifyRegister failed; relying on fallback refresh.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Shell change notification setup failed: {ex}");
        }
    }

    private void OnWindowMessage(ref Message m)
    {
        if (m.Msg != WM_BIN_CHANGED) return;
        // Bursts are common (one event per item); coalesce them.
        _debounce.Stop();
        _debounce.Start();
    }

    public void Dispose()
    {
        _debounce.Dispose();
        _fallback.Dispose();
        if (_registration != 0)
        {
            NativeMethods.SHChangeNotifyDeregister(_registration);
            _registration = 0;
        }
        if (_pidl != IntPtr.Zero)
        {
            NativeMethods.CoTaskMemFree(_pidl);
            _pidl = IntPtr.Zero;
        }
        _window.DestroyHandle();
    }

    /// <summary>Message-only window that receives the Shell notifications.</summary>
    private sealed class MessageWindow : NativeWindow
    {
        public delegate void MessageHandler(ref Message m);
        private readonly MessageHandler _handler;

        public MessageWindow(MessageHandler handler)
        {
            _handler = handler;
            CreateHandle(new CreateParams { Parent = HWND_MESSAGE });
        }

        protected override void WndProc(ref Message m)
        {
            _handler(ref m);
            base.WndProc(ref m);
        }
    }
}
