using System.Windows;
using ICSharpCode.AvalonEdit;
using Marker.App.Services;

namespace Marker.App.Views;

/// <summary>
/// Modeless find-and-replace dialog. It always acts on whatever editor the
/// supplied accessor returns, so it keeps working as the user switches tabs.
/// </summary>
public partial class ReplaceDialog : Window
{
    private readonly Func<TextEditor?> _editorAccessor;

    public ReplaceDialog(Func<TextEditor?> editorAccessor)
    {
        InitializeComponent();
        _editorAccessor = editorAccessor;
        // Match the OS title bar to the current theme.
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        Loaded += (_, _) => FindBox.Focus();
    }

    private StringComparison Comparison => MatchCaseBox.IsChecked == true
        ? StringComparison.Ordinal
        : StringComparison.OrdinalIgnoreCase;

    private void OnFindNext(object sender, RoutedEventArgs e) => FindNext();

    /// <summary>Selects the next match after the caret; wraps to the top.</summary>
    private bool FindNext()
    {
        var editor = _editorAccessor();
        string term = FindBox.Text;
        if (editor is null || term.Length == 0)
            return false;

        string text = editor.Document.Text;
        int start = editor.SelectionLength > 0
            ? editor.SelectionStart + 1
            : editor.CaretOffset;

        int index = text.IndexOf(term, Math.Min(start, text.Length), Comparison);
        if (index < 0)
            index = text.IndexOf(term, 0, Comparison); // wrap

        if (index < 0)
        {
            SystemSounds_Beep();
            return false;
        }

        editor.Select(index, term.Length);
        editor.ScrollToLine(editor.Document.GetLineByOffset(index).LineNumber);
        return true;
    }

    private void OnReplace(object sender, RoutedEventArgs e)
    {
        var editor = _editorAccessor();
        if (editor is null)
            return;

        // Replace the current selection only when it is the search term.
        if (editor.SelectionLength > 0 &&
            string.Equals(editor.SelectedText, FindBox.Text, Comparison))
        {
            editor.SelectedText = ReplaceBox.Text;
        }
        FindNext();
    }

    private void OnReplaceAll(object sender, RoutedEventArgs e)
    {
        var editor = _editorAccessor();
        string term = FindBox.Text;
        if (editor is null || term.Length == 0)
            return;

        string text = editor.Document.Text;
        int count = 0;
        int index = text.IndexOf(term, 0, Comparison);
        var sb = new System.Text.StringBuilder();
        int cursor = 0;

        while (index >= 0)
        {
            sb.Append(text, cursor, index - cursor);
            sb.Append(ReplaceBox.Text);
            cursor = index + term.Length;
            count++;
            index = text.IndexOf(term, cursor, Comparison);
        }
        sb.Append(text, cursor, text.Length - cursor);

        if (count > 0)
            editor.Document.Text = sb.ToString();

        MessageBox.Show(this, $"Replaced {count} occurrence(s).", "Marker",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private static void SystemSounds_Beep() => System.Media.SystemSounds.Beep.Play();
}
