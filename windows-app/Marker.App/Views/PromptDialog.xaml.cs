using System.Windows;
using Marker.App.Services;

namespace Marker.App.Views;

/// <summary>A minimal single-line text prompt used for new file/folder and rename.</summary>
public partial class PromptDialog : Window
{
    private PromptDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the prompt modally. Returns the entered text, or <c>null</c> if
    /// the user cancelled or left the box empty.
    /// </summary>
    public static string? Show(Window owner, string title, string message, string initial = "")
    {
        var dialog = new PromptDialog
        {
            Owner = owner,
            Title = title
        };
        dialog.MessageText.Text = message;
        dialog.InputBox.Text = initial;

        // Match the OS title bar to the current theme.
        dialog.SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(dialog);

        dialog.Loaded += (_, _) =>
        {
            dialog.InputBox.Focus();
            // Pre-select the name portion so a rename can overwrite quickly.
            int dot = initial.LastIndexOf('.');
            if (dot > 0)
                dialog.InputBox.Select(0, dot);
            else
                dialog.InputBox.SelectAll();
        };

        bool ok = dialog.ShowDialog() == true;
        string result = dialog.InputBox.Text.Trim();
        return ok && result.Length > 0 ? result : null;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
