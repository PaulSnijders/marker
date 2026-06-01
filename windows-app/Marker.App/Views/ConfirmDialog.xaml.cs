using System.Windows;
using Marker.App.Services;

namespace Marker.App.Views;

/// <summary>
/// Themed yes/no (or OK-only) modal — drop-in for <c>MessageBox.Show</c>
/// when the surrounding flow already follows the light/dark theme. The
/// stock MessageBox uses the OS dialog chrome and ignores our resources,
/// which shows up as white-on-white in dark mode.
/// </summary>
public partial class ConfirmDialog : Window
{
    private ConfirmDialog()
    {
        InitializeComponent();
        ThemeManager.PrepareDialog(this);
    }

    /// <summary>Modal confirm. Returns true when the user picked OK.</summary>
    public static bool Show(Window owner, string title, string message,
                            string okLabel = "OK", string cancelLabel = "Cancel")
    {
        var dialog = new ConfirmDialog { Owner = owner, Title = title };
        dialog.MessageText.Text = message;
        dialog.OkButton.Content = okLabel;
        dialog.CancelButton.Content = cancelLabel;
        return dialog.ShowDialog() == true;
    }

    /// <summary>Single-button themed alert (no cancel).</summary>
    public static void Alert(Window owner, string title, string message)
    {
        var dialog = new ConfirmDialog { Owner = owner, Title = title };
        dialog.MessageText.Text = message;
        dialog.OkButton.Content = "OK";
        dialog.CancelButton.Visibility = Visibility.Collapsed;
        dialog.ShowDialog();
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
