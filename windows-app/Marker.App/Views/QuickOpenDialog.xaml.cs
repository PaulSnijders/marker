using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Marker.App.Views;

/// <summary>One workspace file offered by the quick-open picker.</summary>
public sealed class QuickOpenFile
{
    public string FullPath { get; }
    public string Name { get; }

    /// <summary>Path shown under the name and used for matching, root-relative.</summary>
    public string RelativePath { get; }

    public QuickOpenFile(string fullPath, string name, string relativePath)
    {
        FullPath = fullPath;
        Name = name;
        RelativePath = relativePath;
    }
}

/// <summary>
/// A type-to-find file picker, in the spirit of a code editor's "go to file".
/// Type to filter, arrow keys to move, Enter to open, Esc to cancel.
/// </summary>
public partial class QuickOpenDialog : Window
{
    private const int MaxResults = 100;

    private readonly IReadOnlyList<QuickOpenFile> _files;
    private bool _closing;

    /// <summary>The chosen file's full path, or null when cancelled.</summary>
    public string? Result { get; private set; }

    private QuickOpenDialog(IReadOnlyList<QuickOpenFile> files)
    {
        InitializeComponent();
        _files = files;
        Loaded += (_, _) => FilterBox.Focus();
        Closing += (_, _) => _closing = true;
        // Dismiss if the user clicks away to another window — but not while we
        // are already closing (accepting a file deactivates us in passing).
        Deactivated += (_, _) =>
        {
            if (!_closing)
                Close();
        };
        PreviewKeyDown += OnKeyDown;
        Filter();
    }

    /// <summary>Shows the picker modally; returns the chosen path or null.</summary>
    public static string? Show(Window owner, IReadOnlyList<QuickOpenFile> files)
    {
        var dialog = new QuickOpenDialog(files) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private void OnFilterChanged(object sender, TextChangedEventArgs e) => Filter();

    /// <summary>Re-ranks the file list against the current query.</summary>
    private void Filter()
    {
        string query = FilterBox.Text.Trim();

        IEnumerable<QuickOpenFile> matches = query.Length == 0
            ? _files.Take(MaxResults)
            : _files
                .Select(f => (file: f, score: Score(f, query)))
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .ThenBy(x => x.file.Name.Length)
                .Select(x => x.file)
                .Take(MaxResults);

        ResultList.ItemsSource = matches.ToList();
        if (ResultList.Items.Count > 0)
            ResultList.SelectedIndex = 0;

        EmptyHint.Visibility = ResultList.Items.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Higher is a better match: a name prefix beats a name substring, which
    /// beats a path substring, which beats a loose fuzzy (subsequence) hit.
    /// </summary>
    private static int Score(QuickOpenFile file, string query)
    {
        const StringComparison ci = StringComparison.OrdinalIgnoreCase;
        if (file.Name.StartsWith(query, ci)) return 100;
        if (file.Name.Contains(query, ci)) return 60;
        if (file.RelativePath.Contains(query, ci)) return 30;
        if (IsSubsequence(file.Name, query)) return 10;
        return 0;
    }

    /// <summary>True when every query character appears, in order, in text.</summary>
    private static bool IsSubsequence(string text, string query)
    {
        int t = 0;
        foreach (char qc in query)
        {
            bool found = false;
            while (t < text.Length)
            {
                if (char.ToLowerInvariant(text[t++]) == char.ToLowerInvariant(qc))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
                return false;
        }
        return true;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down: MoveSelection(1); e.Handled = true; break;
            case Key.Up: MoveSelection(-1); e.Handled = true; break;
            case Key.Enter: Accept(); e.Handled = true; break;
            case Key.Escape: Close(); e.Handled = true; break;
        }
    }

    private void MoveSelection(int delta)
    {
        int count = ResultList.Items.Count;
        if (count == 0)
            return;
        ResultList.SelectedIndex = Math.Clamp(ResultList.SelectedIndex + delta, 0, count - 1);
        ResultList.ScrollIntoView(ResultList.SelectedItem);
    }

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e) => Accept();

    private void Accept()
    {
        if (ResultList.SelectedItem is QuickOpenFile file)
        {
            Result = file.FullPath;
            DialogResult = true;
        }
    }
}
