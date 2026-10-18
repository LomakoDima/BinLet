using System.Windows;

namespace BinLet;

public partial class App : System.Windows.Application
{
    // One instance per user session; the second launch simply exits.
    private const string MutexName = @"Local\Binlet.SingleInstance.5C0F7B1E-6A3D-4B8F-9E52-0D4C7A91B3E8";

    private Mutex? _singleInstance;
    private TrayController? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(initiallyOwned: true, MutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            Dialogs.ShowError("Binlet hit an unexpected error", args.Exception.Message);
        };

        _tray = new TrayController();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _tray = null;

        if (_singleInstance is not null)
        {
            _singleInstance.ReleaseMutex();
            _singleInstance.Dispose();
            _singleInstance = null;
        }

        base.OnExit(e);
    }
}
