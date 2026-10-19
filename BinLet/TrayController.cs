using System.IO;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using Wpf = System.Windows.Controls;

namespace BinLet;

/// <summary>
/// Owns the notification-area icon, its context menu and the actions behind it.
/// Must be created, used and disposed on the UI thread.
/// </summary>
internal sealed class TrayController : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _emptyIcon;
    private readonly Icon _fullIcon;
    private readonly RecycleBinWatcher _watcher;

    private readonly Wpf.ContextMenu _menu;
    private readonly Wpf.MenuItem _openItem;
    private readonly Wpf.MenuItem _emptyItem;
    private readonly Wpf.MenuItem _startupItem;

    private RecycleBinState? _state;
    private bool _refreshing;
    private bool _refreshQueued;
    private bool _emptying;
    private int _lastOpenTick = Environment.TickCount - 10_000;
    private bool _disposed;

    public TrayController()
    {
        _emptyIcon = LoadIcon("bin-empty");
        _fullIcon = LoadIcon("bin-full");

        _openItem = new Wpf.MenuItem { Header = "Open Recycle Bin" };
        _openItem.Click += (_, _) => OpenRecycleBin();
        _emptyItem = new Wpf.MenuItem { Header = "Empty Recycle Bin" };
        _emptyItem.Click += async (_, _) => await EmptyRecycleBinAsync();
        // IsCheckable is what makes the Fluent theme draw the check mark.
        _startupItem = new Wpf.MenuItem { Header = "Start with Windows", IsCheckable = true };
        _startupItem.Click += (_, _) => ToggleStartup();
        var exitItem = new Wpf.MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();

        _menu = new Wpf.ContextMenu { Placement = PlacementMode.MousePoint };
        _menu.Items.Add(_openItem);
        _menu.Items.Add(_emptyItem);
        _menu.Items.Add(new Wpf.Separator());
        _menu.Items.Add(_startupItem);
        _menu.Items.Add(new Wpf.Separator());
        _menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = _emptyIcon,
            Text = "Binlet",
            Visible = true,
        };
        _notifyIcon.MouseUp += OnTrayMouseUp;

        _watcher = new RecycleBinWatcher();
        _watcher.Changed += OnBinMaybeChanged;

        OnBinMaybeChanged();
    }

    private static Icon LoadIcon(string name)
    {
        try
        {
            using var stream = typeof(TrayController).Assembly.GetManifestResourceStream($"Assets.{name}.ico");
            if (stream is not null)
                return new Icon(stream, SystemInformation.SmallIconSize);
        }
        catch (Exception ex) when (ex is ArgumentException or Win32Exception or IOException)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load icon '{name}': {ex.Message}");
        }
        return (Icon)SystemIcons.Application.Clone();
    }

    // ---- Tray interaction -------------------------------------------------------------

    private void OnTrayMouseUp(object? sender, MouseEventArgs e)
    {
        switch (e.Button)
        {
            case MouseButtons.Left:
                OpenRecycleBin();
                break;
            case MouseButtons.Right:
                ShowMenu();
                break;
        }
    }

    private void ShowMenu()
    {
        _emptyItem.IsEnabled = _state is not { IsEmpty: true };
        _startupItem.IsChecked = StartupRegistration.IsEnabled();

        _menu.IsOpen = true;

        // A popup opened from a tray icon is not dismissed by clicking elsewhere unless
        // it owns the foreground, so activate its window.
        if (PresentationSource.FromVisual(_menu) is HwndSource source)
            NativeMethods.SetForegroundWindow(source.Handle);

        OnBinMaybeChanged(); // make sure the next open reflects reality
    }

    private void OpenRecycleBin()
    {
        // A double-click raises two clicks; don't open two Explorer windows.
        int now = Environment.TickCount;
        if (unchecked(now - _lastOpenTick) < SystemInformation.DoubleClickTime) return;
        _lastOpenTick = now;

        try
        {
            RecycleBinShell.Open();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Dialogs.ShowError("Couldn't open the Recycle Bin", ex.Message);
        }
    }

    private async Task EmptyRecycleBinAsync()
    {
        if (_emptying) return;
        _emptying = true;
        try
        {
            // Re-check right now rather than trusting the cached state.
            var current = await Task.Run(RecycleBinShell.TryQuery);
            if (_disposed) return;
            Apply(current);

            if (current is { IsEmpty: true })
            {
                ShowBalloon("The Recycle Bin is already empty.");
                return;
            }

            if (!Dialogs.ConfirmEmpty(current)) return;

            int hr;
            try
            {
                hr = await RecycleBinShell.EmptyAsync();
            }
            catch (Exception ex)
            {
                Dialogs.ShowError("Couldn't empty the Recycle Bin", ex.Message);
                return;
            }
            if (_disposed) return;

            switch (hr)
            {
                case >= 0:
                    break; // success: the refresh below updates the icon
                case NativeMethods.HRESULT_ERROR_CANCELLED:
                    break; // cancelled in the Shell's progress dialog
                case NativeMethods.E_UNEXPECTED:
                    // The Shell returns this when there was nothing to delete.
                    if (await Task.Run(RecycleBinShell.TryQuery) is not { IsEmpty: true })
                        Dialogs.ShowError("Couldn't empty the Recycle Bin", DescribeHResult(hr));
                    break;
                default:
                    Dialogs.ShowError("Couldn't empty the Recycle Bin", DescribeHResult(hr));
                    break;
            }
        }
        finally
        {
            _emptying = false;
            if (!_disposed) OnBinMaybeChanged();
        }
    }

    private void ToggleStartup()
    {
        // A checkable item has already flipped IsChecked by the time Click is raised.
        bool enable = _startupItem.IsChecked;
        try
        {
            StartupRegistration.SetEnabled(enable);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            Dialogs.ShowError("Couldn't change the startup setting", ex.Message);
        }
        finally
        {
            // Show what the registry actually says, never what we hoped for.
            _startupItem.IsChecked = StartupRegistration.IsEnabled();
        }
    }

    // ---- State refresh ----------------------------------------------------------------

    private async void OnBinMaybeChanged()
    {
        if (_disposed) return;
        if (_refreshing)
        {
            _refreshQueued = true;
            return;
        }

        _refreshing = true;
        try
        {
            do
            {
                _refreshQueued = false;
                // The Shell query can block briefly (e.g. sleeping disks), so keep it off the UI thread.
                var state = await Task.Run(RecycleBinShell.TryQuery);
                if (_disposed) return;
                Apply(state);
            }
            while (_refreshQueued && !_disposed);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Refresh failed: {ex}");
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void Apply(RecycleBinState? state)
    {
        // On a failed query keep the last known state; only fall back to "unavailable" if we never had one.
        if (state is not null) _state = state;

        string text;
        Icon icon;
        switch (_state)
        {
            case { IsEmpty: true }:
                text = "Binlet — Recycle Bin empty";
                icon = _emptyIcon;
                break;
            case { } s:
                text = s.ItemCount == 1
                    ? "Binlet — Recycle Bin contains 1 item"
                    : $"Binlet — Recycle Bin contains {s.ItemCount:N0} items";
                icon = _fullIcon;
                break;
            default:
                text = "Binlet — Recycle Bin status unavailable";
                icon = _emptyIcon;
                break;
        }

        if (!ReferenceEquals(_notifyIcon.Icon, icon)) _notifyIcon.Icon = icon;
        if (_notifyIcon.Text != text) _notifyIcon.Text = text;
    }

    private void ShowBalloon(string message) =>
        _notifyIcon.ShowBalloonTip(3000, "Binlet", message, ToolTipIcon.Info);

    private static string DescribeHResult(int hr) =>
        Marshal.GetExceptionForHR(hr)?.Message ?? $"The Shell returned error 0x{hr:X8}.";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _menu.IsOpen = false;
        _watcher.Changed -= OnBinMaybeChanged;
        _watcher.Dispose();

        _notifyIcon.MouseUp -= OnTrayMouseUp;
        _notifyIcon.Visible = false; // removes the icon from the tray immediately
        _notifyIcon.Dispose();

        _emptyIcon.Dispose();
        _fullIcon.Dispose();
    }
}
