using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Marker.App.ViewModels;

namespace Marker.App.Views;

/// <summary>
/// Find + Replace panel hosted in the sidebar's "Replace" tab. Mirrors
/// <see cref="FindInFilesView"/>: single click on a result jumps to the
/// file, the only extra is the Replace box and the Replace All button.
/// </summary>
public partial class ReplaceInFilesView : UserControl
{
    /// <summary>Same contract as the Find view — host opens the file + jumps.</summary>
    public event EventHandler<MatchActivatedEventArgs>? MatchActivated;

    /// <summary>Raised after a Replace All commit so the host can reload open tabs.</summary>
    public event EventHandler<FilesReplacedEventArgs>? FilesReplaced;

    private ReplaceInFilesViewModel? _vm;

    public ReplaceInFilesView()
    {
        InitializeComponent();
    }

    public void Attach(ReplaceInFilesViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
    }

    public void FocusSearchBox()
    {
        QueryBox.Focus();
        QueryBox.SelectAll();
    }

    private void OnQueryKeyDown(object sender, KeyEventArgs e)
    {
        // Enter in the search box jumps focus to the Replace box, matching
        // the natural top-down typing flow.
        if (e.Key == Key.Enter)
        {
            ReplaceBox.Focus();
            ReplaceBox.SelectAll();
            e.Handled = true;
        }
    }

    private void OnTreeItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TreeViewItem item)
            return;

        if (item.DataContext is ReplacePreviewItem preview)
        {
            MatchActivated?.Invoke(this, new MatchActivatedEventArgs(
                preview.Match.FilePath, preview.LineNumber,
                preview.ColumnStart, preview.Length));
            e.Handled = true;
        }
        else if (item.DataContext is FileReplacesViewModel group)
        {
            group.IsExpanded = !group.IsExpanded;
            e.Handled = true;
        }
    }

    private void OnReplaceBoxKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+Enter — commit Replace All from the keyboard. Bare Enter is
        // ignored so the user can paste a multi-line replacement if they want.
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            RequestReplaceAll();
            e.Handled = true;
        }
    }

    private void OnResultsKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ResultsTree.SelectedItem is ReplacePreviewItem preview)
        {
            MatchActivated?.Invoke(this, new MatchActivatedEventArgs(
                preview.Match.FilePath, preview.LineNumber,
                preview.ColumnStart, preview.Length));
            e.Handled = true;
        }
    }

    /// <summary>Public so the host can fire Replace All from a keyboard shortcut.</summary>
    public void RequestReplaceAll() => _ = ReplaceAllAsync();

    private void OnReplaceAll(object sender, RoutedEventArgs e) => _ = ReplaceAllAsync();

    private async Task ReplaceAllAsync()
    {
        if (_vm is null || !_vm.CanReplaceAll)
            return;

        int matches = _vm.Files.Sum(f => f.Items.Count);
        int files = _vm.Files.Count;

        var owner = Window.GetWindow(this);
        bool confirmed = ConfirmDialog.Show(owner!,
            "Replace All",
            $"Replace {matches:N0} match{(matches == 1 ? "" : "es")} " +
            $"in {files:N0} file{(files == 1 ? "" : "s")}?\n\nThis cannot be undone.",
            okLabel: "Replace All");
        if (!confirmed)
            return;

        var (written, failed, paths) = await _vm.CommitReplaceAllAsync();

        FilesReplaced?.Invoke(this, new FilesReplacedEventArgs(paths));

        if (failed > 0)
        {
            ConfirmDialog.Alert(owner!, "Replace All",
                $"Replaced in {written:N0} file{(written == 1 ? "" : "s")}.\n" +
                $"{failed:N0} file{(failed == 1 ? "" : "s")} could not be written.");
        }
    }
}
