using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Marker.App.ViewModels;
using Marker.Core.Search;

namespace Marker.App.Views;

/// <summary>
/// Find-in-Files panel hosted in the sidebar's "Find" tab. The view stays
/// dumb: it binds to a <see cref="FindInFilesViewModel"/> and raises
/// <see cref="MatchActivated"/> when the user double-clicks (or presses
/// Enter on) a result row. <see cref="MainWindow"/> opens the file and
/// jumps the editor.
/// </summary>
public partial class FindInFilesView : UserControl
{
    /// <summary>Raised when the user picks a result and wants to jump to it.</summary>
    public event EventHandler<MatchActivatedEventArgs>? MatchActivated;

    public FindInFilesView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Wires up the VM. Called by the host once the workspace is loaded so
    /// the searcher can ask for current root folders each time it runs.
    /// </summary>
    public void Attach(FindInFilesViewModel vm)
    {
        DataContext = vm;
    }

    /// <summary>Puts keyboard focus on the search box.</summary>
    public void FocusSearchBox()
    {
        QueryBox.Focus();
        QueryBox.SelectAll();
    }

    private void OnQueryKeyDown(object sender, KeyEventArgs e)
    {
        // Enter in the search box moves focus into the results so the user
        // can scroll with the arrow keys without taking a hand off the keyboard.
        if (e.Key == Key.Enter && ResultsTree.HasItems)
        {
            ResultsTree.Focus();
            if (ResultsTree.Items.Count > 0 &&
                ResultsTree.ItemContainerGenerator.ContainerFromIndex(0) is TreeViewItem first)
            {
                first.IsSelected = true;
                first.Focus();
            }
            e.Handled = true;
        }
    }

    /// <summary>
    /// Single click on a row. Match leaves jump to the file; file groups
    /// expand/collapse. The chevron handles its own click and marks the
    /// event handled, so we only see clicks on the row itself.
    /// </summary>
    private void OnTreeItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TreeViewItem item)
            return;

        if (item.DataContext is SearchMatch match)
        {
            RaiseMatch(match);
            e.Handled = true;
        }
        else if (item.DataContext is FileMatchesViewModel group)
        {
            group.IsExpanded = !group.IsExpanded;
            e.Handled = true;
        }
    }

    private void OnResultsKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ResultsTree.SelectedItem is SearchMatch match)
        {
            RaiseMatch(match);
            e.Handled = true;
        }
    }

    private void RaiseMatch(SearchMatch match)
    {
        MatchActivated?.Invoke(this, new MatchActivatedEventArgs(
            match.FilePath, match.LineNumber, match.ColumnStart, match.Length));
    }
}
