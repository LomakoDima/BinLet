using System.Globalization;
using System.Windows.Forms;

namespace BinLet;

/// <summary>Native Windows task dialogs used for confirmation and errors.</summary>
internal static class Dialogs
{
    private const string Caption = "Binlet";

    /// <summary>Asks before permanently deleting. Cancel is the default button.</summary>
    public static bool ConfirmEmpty(RecycleBinState? state)
    {
        string text = state is { IsEmpty: false } s
            ? $"{s.ItemCount.ToString("N0", CultureInfo.CurrentCulture)} {(s.ItemCount == 1 ? "item" : "items")} " +
              $"({FormatSize(s.SizeBytes)}) will be permanently deleted. This can't be undone."
            : "All items in the Recycle Bin will be permanently deleted. This can't be undone.";

        var emptyButton = new TaskDialogButton("Empty Recycle Bin");
        var page = new TaskDialogPage
        {
            Caption = Caption,
            Heading = "Empty the Recycle Bin?",
            Text = text,
            Icon = TaskDialogIcon.Warning,
            AllowCancel = true,
            Buttons = { emptyButton, TaskDialogButton.Cancel },
            DefaultButton = TaskDialogButton.Cancel,
        };
        return TaskDialog.ShowDialog(page) == emptyButton;
    }

    public static void ShowError(string heading, string detail)
    {
        TaskDialog.ShowDialog(new TaskDialogPage
        {
            Caption = Caption,
            Heading = heading,
            Text = detail,
            Icon = TaskDialogIcon.Error,
            Buttons = { TaskDialogButton.OK },
        });
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["bytes", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0
            ? $"{bytes:N0} {units[0]}"
            : $"{value.ToString(value < 10 ? "0.0" : "0", CultureInfo.CurrentCulture)} {units[unit]}";
    }
}
