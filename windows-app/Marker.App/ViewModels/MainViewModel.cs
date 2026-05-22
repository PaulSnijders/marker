using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Marker.Core.Models;

namespace Marker.App.ViewModels;

/// <summary>
/// Data context for the main window: the known workspaces, the active
/// workspace's tree roots, the open tabs and the current selection. Behaviour
/// (file I/O, editor wiring, workspace switching) lives in the window
/// code-behind, which keeps this view model a thin observable shell.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>All known workspaces — the items in the switcher.</summary>
    public ObservableCollection<Workspace> AllWorkspaces { get; } = new();

    /// <summary>The workspace currently shown.</summary>
    [ObservableProperty] private Workspace? _activeWorkspace;

    /// <summary>Root folders of the active workspace, shown in the file tree.</summary>
    public ObservableCollection<FileSystemNodeViewModel> RootFolders { get; } = new();

    /// <summary>Open editor tabs.</summary>
    public ObservableCollection<EditorTabViewModel> Tabs { get; } = new();

    [ObservableProperty] private EditorTabViewModel? _selectedTab;

    [ObservableProperty] private FileSystemNodeViewModel? _selectedNode;

    /// <summary>True while no file is open — used to show the empty-state hint.</summary>
    public bool HasNoTabs => Tabs.Count == 0;

    public MainViewModel()
    {
        Tabs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoTabs));
    }
}
