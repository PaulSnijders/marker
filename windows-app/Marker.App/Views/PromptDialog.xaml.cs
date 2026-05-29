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
    /// <param name="selectAll">
    /// When true, selects the whole initial text (handy when the caller wants
    /// the user to grab a copy of it). Otherwise selects only the name portion
    /// before the last dot — the typical "edit the name, keep the extension"
    /// behaviour for a rename.
    /// </param>
    public static string? Show(Window owner, string title, string message,
                               string initial = "", bool selectAll = false)
    {
        var dialog = new PromptDialog
        {
            Owner = owner,
            Title = title
        };
        dialog.MessageText.Text = message;
        dialog.InputBox.Text = initial;

        // Theme the OS title bar and suppress the first-paint flash.
        ThemeManager.PrepareDialog(dialog);

        dialog.Loaded += (_, _) =>
        {
            dialog.InputBox.Focus();
            int dot = initial.LastIndexOf('.');
            if (!selectAll && dot > 0)
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
