using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Search;
using Marker.App.Services;
using Marker.App.ViewModels;
using Marker.App.Views;
using Marker.Core.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace Marker.App;

/// <summary>
/// Main window and application controller. Owns the editor wiring (AvalonEdit
/// document swapping, WebView2 modes), the file tree and all menu actions.
/// The view models stay thin; behaviour lives here per the "no DI, keep it
/// simple" guidance in the requirements.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _watchTimer;

    private EditorTabViewModel? _currentTab;     // tab shown in the AvalonEdit host
    private EditorTabViewModel? _webTab;         // tab currently driving the WebView2
    private MarkdownMode _webMode;
    private bool _webReady;                      // CoreWebView2 initialized
    private bool _webAvailable = true;           // false if WebView2 runtime missing
    private bool _startupComplete;               // true once the window finished loading
    private ReplaceDialog? _replaceDialog;

    private Workspace? _activeWorkspace;         // the workspace currently shown
    private bool _switchingWorkspace;            // true while swapping workspaces
    private bool _suppressWorkspaceSelection;    // ignore ComboBox events we caused

    private EditorTabViewModel? _previewTab;     // current sneak-peek tab, if any
    private bool _suppressEditorFocus;           // skip auto-focusing the editor once

    private string ReadFilePath => Path.Combine(AppServices.WebRoot, "__read.html");

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        CheatsheetItems.ItemsSource = BuildCheatsheet();

        RestoreWindowBounds();

        _watchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _watchTimer.Tick += OnWatchTimerTick;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedTab))
                OnSelectedTabChanged();
        };

        Loaded += OnLoaded;
        Closing += OnWindowClosing;
        Deactivated += (_, _) =>
        {
            if (AppServices.Settings.AutoSave) SaveAllDirty(silent: true);
            SaveSettingsNow();
        };
        PreviewKeyDown += OnPreviewKeyDown;
        // Bubbling — fires only for an Escape no child control consumed.
        KeyDown += OnWindowKeyDown;
    }

    // ================================================================
    //  Title bar dark mode
    // ================================================================

    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyTitleBarTheme();
    }

    /// <summary>Paints the OS title bar to match the current light/dark theme.</summary>
    private void ApplyTitleBarTheme()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;
        int useDark = ThemeManager.CurrentTheme == "dark" ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
    }

    // ================================================================
    //  Startup / shutdown
    // ================================================================

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ConfigureEditor();

        try
        {
            await InitializeWebViewAsync();
        }
        catch
        {
            // No WebView2 runtime — rich/read modes degrade to source mode.
            _webAvailable = false;
        }

        InitializeWorkspaces();
        RebuildRecentMenu();

        // From here on, every settings change is written through immediately.
        _startupComplete = true;
    }

    private void RestoreWindowBounds()
    {
        var s = AppServices.Settings;
        Width = s.WindowWidth;
        Height = s.WindowHeight;

        if (!double.IsNaN(s.WindowLeft) && !double.IsNaN(s.WindowTop))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = s.WindowLeft;
            Top = s.WindowTop;
        }

        if (s.WindowMaximized)
            WindowState = WindowState.Maximized;

        TreeColumn.Width = new GridLength(Math.Max(120, s.TreePaneWidth));
    }

    private void ConfigureEditor()
    {
        var s = AppServices.Settings;
        try { Editor.FontFamily = new FontFamily(s.FontFamily); }
        catch { /* fall back to default font */ }
        Editor.FontSize = s.FontSize;
        Editor.WordWrap = s.WordWrap;
        Editor.Options.HighlightCurrentLine = true;
        Editor.Options.EnableEmailHyperlinks = false;
        Editor.Options.EnableHyperlinks = false;
        WordWrapItem.IsChecked = s.WordWrap;
        UpdateFontSizeMenu();

        // Small gap between the line-number margin and the text.
        Editor.TextArea.LeftMargins.Add(new Border { Width = 6 });

        SearchPanel.Install(Editor);
        Editor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            if (_currentTab is not null)
            {
                _currentTab.CaretLine = Editor.TextArea.Caret.Line;
                _currentTab.CaretColumn = Editor.TextArea.Caret.Column;
            }
        };
    }

    private async Task InitializeWebViewAsync()
    {
        string userData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Marker", "WebView2");

        Web.CreationProperties = new Microsoft.Web.WebView2.Wpf.CoreWebView2CreationProperties
        {
            UserDataFolder = userData
        };

        await Web.EnsureCoreWebView2Async();

        Web.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "marker.assets", AppServices.WebRoot, CoreWebView2HostResourceAccessKind.Allow);
        Web.CoreWebView2.Settings.AreDevToolsEnabled = false;
        Web.CoreWebView2.Settings.IsStatusBarEnabled = false;
        Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        Web.WebMessageReceived += OnWebMessageReceived;
        _webReady = true;
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmCloseAll())
        {
            e.Cancel = true;
            return;
        }

        PersistSession();
        SaveActiveWorkspace();
        AppServices.SaveSettings();

        foreach (var watcher in _watchers.Values)
            watcher.Dispose();
        _watchers.Clear();
    }

    /// <summary>Saves or discards dirty tabs before exit; false means "cancel close".</summary>
    private bool ConfirmCloseAll()
    {
        if (AppServices.Settings.AutoSave)
        {
            SaveAllDirty(silent: true);
            return true;
        }

        foreach (var tab in _vm.Tabs.Where(t => t.IsDirty && !t.IsBinary).ToList())
        {
            var answer = MessageBox.Show(this,
                $"Save changes to {tab.Title}?", "Marker",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (answer == MessageBoxResult.Cancel)
                return false;
            if (answer == MessageBoxResult.Yes)
                SaveTab(tab);
        }
        return true;
    }

    private void PersistSession()
    {
        var s = AppServices.Settings;

        s.WindowMaximized = WindowState == WindowState.Maximized;
        var bounds = WindowState == WindowState.Maximized ? RestoreBounds
                     : new Rect(Left, Top, Width, Height);
        s.WindowLeft = bounds.Left;
        s.WindowTop = bounds.Top;
        s.WindowWidth = bounds.Width;
        s.WindowHeight = bounds.Height;
        s.TreePaneWidth = TreeColumn.ActualWidth;
        // Open files are stored per workspace — see SaveActiveWorkspace.
    }

    /// <summary>
    /// Captures the current session and writes settings to disk right away.
    /// Called after every settings change so nothing is lost if the app is
    /// killed or crashes before a clean shutdown. The file is tiny and the
    /// write is atomic, so calling this often is cheap and safe.
    /// </summary>
    private void SaveSettingsNow()
    {
        if (!_startupComplete)
            return; // avoid redundant writes while restoring the session
        try
        {
            PersistSession();
            SaveActiveWorkspace();
            AppServices.SaveSettings();
        }
        catch
        {
            // A settings write must never interrupt the user.
        }
    }

    // ================================================================
    //  Workspaces & file tree
    // ================================================================

    // --- workspaces ---------------------------------------------------

    /// <summary>
    /// Loads every workspace from disk, migrating the legacy single-folder
    /// list on first run, then shows the last active one.
    /// </summary>
    private void InitializeWorkspaces()
    {
        var all = AppServices.Workspaces.LoadAll().ToList();

        if (all.Count == 0)
            all.Add(MigrateOrCreateDefaultWorkspace());

        foreach (var ws in all)
            _vm.AllWorkspaces.Add(ws);

        var active = all.FirstOrDefault(w => string.Equals(
                         w.Name, AppServices.Settings.ActiveWorkspace,
                         StringComparison.OrdinalIgnoreCase))
                     ?? all[0];

        LoadWorkspace(active);

        // Persist the resolved active workspace (and any migration result).
        AppServices.SaveSettings();
    }

    /// <summary>
    /// First run on the workspace model: fold any legacy flat folder list into
    /// a single "My Workspace", or create an empty one. The legacy fields are
    /// then cleared so they stay empty going forward.
    /// </summary>
    private Workspace MigrateOrCreateDefaultWorkspace()
    {
        var s = AppServices.Settings;
        var ws = new Workspace { Name = "My Workspace" };

        if (s.WorkspaceFolders.Count > 0)
        {
            ws.Folders = s.WorkspaceFolders.Where(Directory.Exists).ToList();
            ws.OpenFiles = s.OpenFiles.ToList();
        }

        AppServices.SaveWorkspace(ws);

        // Legacy fields are superseded; clear them so they persist empty.
        s.WorkspaceFolders.Clear();
        s.OpenFiles.Clear();
        return ws;
    }

    /// <summary>Shows a workspace: builds its tree, watchers and reopens its tabs.</summary>
    private void LoadWorkspace(Workspace ws)
    {
        _activeWorkspace = ws;
        AppServices.Settings.ActiveWorkspace = ws.Name;

        _suppressWorkspaceSelection = true;
        _vm.ActiveWorkspace = ws;
        _suppressWorkspaceSelection = false;

        foreach (string folder in ws.Folders.ToList())
        {
            if (Directory.Exists(folder))
                AddFolderNode(folder);
        }

        UpdateTreeHint();
        ReopenFiles(ws);
        ApplyTab();
    }

    /// <summary>Closes the current workspace and shows another one.</summary>
    private void SwitchToWorkspace(Workspace ws)
    {
        if (ReferenceEquals(ws, _activeWorkspace))
            return;

        SaveActiveWorkspace();   // remember the workspace we are leaving

        _switchingWorkspace = true;
        try
        {
            foreach (var tab in _vm.Tabs.ToList())
                CloseTab(tab);   // auto-saves dirty tabs, or prompts

            foreach (var watcher in _watchers.Values)
                watcher.Dispose();
            _watchers.Clear();
            _vm.RootFolders.Clear();

            LoadWorkspace(ws);
        }
        finally
        {
            _switchingWorkspace = false;
        }
        SaveSettingsNow();
    }

    /// <summary>Captures the active workspace's folders and tabs and saves it.</summary>
    private void SaveActiveWorkspace()
    {
        if (_activeWorkspace is null || _switchingWorkspace)
            return;
        _activeWorkspace.Folders = _vm.RootFolders.Select(n => n.Path).ToList();
        // Sneak-peeks are transient and the help document belongs to no
        // workspace — neither should reopen on the next launch.
        _activeWorkspace.OpenFiles = _vm.Tabs
            .Where(t => !t.IsPreview && !IsHelpFile(t.FilePath))
            .Select(t => t.FilePath)
            .ToList();
        try { AppServices.SaveWorkspace(_activeWorkspace); }
        catch { /* a workspace write must never interrupt the user */ }
    }

    private void OnWorkspaceSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressWorkspaceSelection || !_startupComplete)
            return;
        if (WorkspaceSelector.SelectedItem is Workspace ws)
            SwitchToWorkspace(ws);
    }

    private void OnSwitchWorkspace(object sender, RoutedEventArgs e) => OpenWorkspaceSwitcher();

    /// <summary>
    /// Opens the workspace switcher dropdown ready for the keyboard: arrow keys
    /// move through the workspaces, Enter switches to the highlighted one.
    /// </summary>
    private void OpenWorkspaceSwitcher()
    {
        WorkspaceSelector.Focus();
        WorkspaceSelector.IsDropDownOpen = true;
    }

    private void OnNewWorkspace(object sender, RoutedEventArgs e)
    {
        string? name = PromptDialog.Show(this, "New Workspace", "Workspace name:", "New Workspace");
        if (name is null)
            return;
        if (!IsWorkspaceNameAvailable(name))
        {
            MessageBox.Show(this, "A workspace with that name already exists.", "Marker");
            return;
        }

        var ws = new Workspace { Name = name };
        AppServices.SaveWorkspace(ws);
        _vm.AllWorkspaces.Add(ws);
        SwitchToWorkspace(ws);
    }

    private void OnRenameWorkspace(object sender, RoutedEventArgs e)
    {
        if (_activeWorkspace is not { } ws)
            return;

        string? name = PromptDialog.Show(this, "Rename Workspace", "Workspace name:", ws.Name);
        if (name is null || name == ws.Name)
            return;
        if (!IsWorkspaceNameAvailable(name, excluding: ws))
        {
            MessageBox.Show(this, "A workspace with that name already exists.", "Marker");
            return;
        }

        string oldScratch = ResolveScratchpadPath();
        ws.Name = name;
        AppServices.Settings.ActiveWorkspace = name;
        AppServices.SaveWorkspace(ws);   // store deletes the old-named file

        // Move the scratchpad file (and any open scratchpad tab) to the new name.
        try
        {
            string newScratch = ResolveScratchpadPath();
            if (File.Exists(oldScratch) && !File.Exists(newScratch))
            {
                File.Move(oldScratch, newScratch);
                RebaseOpenTabs(oldScratch, newScratch);
            }
        }
        catch { /* scratchpad rename is best-effort */ }

        // Re-insert so the ComboBox re-renders the changed name.
        int idx = _vm.AllWorkspaces.IndexOf(ws);
        _suppressWorkspaceSelection = true;
        _vm.AllWorkspaces.RemoveAt(idx);
        _vm.AllWorkspaces.Insert(idx, ws);
        _vm.ActiveWorkspace = ws;
        _suppressWorkspaceSelection = false;

        SaveSettingsNow();
    }

    private void OnDeleteWorkspace(object sender, RoutedEventArgs e)
    {
        if (_activeWorkspace is not { } ws)
            return;
        if (_vm.AllWorkspaces.Count <= 1)
        {
            MessageBox.Show(this, "You can't delete the only workspace.", "Marker");
            return;
        }

        if (MessageBox.Show(this,
                $"Delete the workspace '{ws.Name}'?\n\n" +
                "This only removes the workspace — no files or folders on disk are deleted.",
                "Marker", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            return;

        string scratch = ResolveScratchpadPath();
        var fallback = _vm.AllWorkspaces.First(w => !ReferenceEquals(w, ws));

        SwitchToWorkspace(fallback);   // saves & closes the workspace being deleted

        try { AppServices.Workspaces.Delete(ws); } catch { /* best effort */ }
        try { if (File.Exists(scratch)) File.Delete(scratch); } catch { /* best effort */ }
        _vm.AllWorkspaces.Remove(ws);
    }

    private bool IsWorkspaceNameAvailable(string name, Workspace? excluding = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        return !_vm.AllWorkspaces.Any(w => !ReferenceEquals(w, excluding) &&
            string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    // --- folders within a workspace -----------------------------------

    private void AddFolderNode(string folder)
    {
        var node = new FileSystemNodeViewModel(folder, isDirectory: true, isWorkspaceRoot: true)
        {
            IsExpanded = true
        };
        _vm.RootFolders.Add(node);
        CreateWatcher(folder);
    }

    private void OnAddWorkspace(object sender, RoutedEventArgs e)
    {
        if (_activeWorkspace is null)
            return;
        var dialog = new OpenFolderDialog { Title = "Add Folder to Workspace" };
        if (dialog.ShowDialog(this) == true)
            AddFolderToWorkspace(dialog.FolderName);
    }

    /// <summary>Adds a folder as a tree root of the active workspace.</summary>
    private void AddFolderToWorkspace(string folder)
    {
        if (_activeWorkspace is null)
            return;
        if (_vm.RootFolders.Any(
                n => string.Equals(n.Path, folder, StringComparison.OrdinalIgnoreCase)))
            return;

        AddFolderNode(folder);
        UpdateTreeHint();
        SaveActiveWorkspace();
    }

    private void OnRemoveFolder(object sender, RoutedEventArgs e)
    {
        var root = FindRootFolder(_vm.SelectedNode);
        if (root is null)
        {
            MessageBox.Show(this, "Select a folder in the workspace first.", "Marker");
            return;
        }

        _vm.RootFolders.Remove(root);
        if (_watchers.Remove(root.Path, out var watcher))
            watcher.Dispose();

        UpdateTreeHint();
        SaveActiveWorkspace();
    }

    private FileSystemNodeViewModel? FindRootFolder(FileSystemNodeViewModel? node)
    {
        if (node is null)
            return null;
        return _vm.RootFolders.FirstOrDefault(
            w => node.Path.StartsWith(w.Path, StringComparison.OrdinalIgnoreCase));
    }

    private void OnRefreshTree(object sender, RoutedEventArgs e) => RefreshTree();

    private void RefreshTree()
    {
        foreach (var root in _vm.RootFolders)
            root.Refresh();
    }

    private void UpdateTreeHint()
        => TreeEmptyHint.Visibility = _vm.RootFolders.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;

    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _vm.SelectedNode = e.NewValue as FileSystemNodeViewModel;

        // Moving the tree selection dismisses an open sneak-peek tab.
        if (_previewTab is not null &&
            !string.Equals(_vm.SelectedNode?.Path, _previewTab.FilePath,
                StringComparison.OrdinalIgnoreCase))
        {
            DiscardPreview();
        }
    }

    private void OnFocusTree(object sender, RoutedEventArgs e) => FocusTree();

    /// <summary>
    /// Moves keyboard focus into the file tree so the arrow keys navigate it.
    /// Selects the first folder when nothing is selected yet.
    /// </summary>
    private void FocusTree()
    {
        if (_vm.RootFolders.Count == 0)
        {
            TreeViewMain.Focus();
            return;
        }

        if (_vm.SelectedNode is null)
            _vm.RootFolders[0].IsSelected = true;

        var target = _vm.SelectedNode ?? _vm.RootFolders[0];
        if (TreeViewMain.ItemContainerGenerator.ContainerFromItem(target) is TreeViewItem item)
            item.Focus();
        else
            TreeViewMain.Focus();   // a nested node — the tree delegates focus
    }

    /// <summary>
    /// Keyboard actions inside the file tree: Enter/Space opens a file (or
    /// expands a folder); Right on a file opens a transient "sneak-peek" tab.
    /// </summary>
    private void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (_vm.SelectedNode is not { IsPlaceholder: false } node)
            return;

        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
                if (node.IsDirectory)
                    node.IsExpanded = !node.IsExpanded;
                else
                    OpenFile(node.Path);            // permanent — pins any peek
                e.Handled = true;
                break;
            case Key.Right when !node.IsDirectory:
                OpenFile(node.Path, preview: true); // sneak peek
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Drops the current sneak-peek tab. If the user has started editing it, it
    /// is kept as a normal tab instead of being thrown away.
    /// </summary>
    private void DiscardPreview()
    {
        if (_previewTab is not { } preview)
            return;
        _previewTab = null;

        if (preview.IsDirty)
        {
            preview.IsPreview = false;   // edited — keep it as a real tab
        }
        else
        {
            // Closing the peek must not pull focus out of the tree.
            if (ReferenceEquals(preview, _vm.SelectedTab))
                _suppressEditorFocus = true;
            CloseTab(preview);
        }
    }

    private void OnTreeItemClick(object sender, MouseButtonEventArgs e)
    {
        // Single click: open a file, or expand/collapse a folder. Clicks on the
        // expander arrow are handled by the ToggleButton itself (it marks the
        // event handled), so this only fires for clicks on the item row.
        if (sender is not TreeViewItem item ||
            item.DataContext is not FileSystemNodeViewModel node ||
            node.IsPlaceholder)
            return;

        if (node.IsDirectory)
            node.IsExpanded = !node.IsExpanded;
        else
            OpenFile(node.Path);

        e.Handled = true;
    }

    private void OnTreeItemRightClick(object sender, MouseButtonEventArgs e)
    {
        // Right-click should select the node so context actions target it.
        if (sender is TreeViewItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    // --- drag & drop from Windows Explorer ----------------------------

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        // Only intercept file/folder drops; leave text drags for the editor.
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
            return;

        e.Handled = true;
        foreach (string path in paths)
        {
            if (Directory.Exists(path))
                AddFolderToWorkspace(path);   // folder -> tree root of this workspace
            else if (File.Exists(path))
                OpenFile(path);               // file   -> open in editor
        }
    }

    // --- file watcher -------------------------------------------------

    private void CreateWatcher(string path)
    {
        try
        {
            var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };
            watcher.Created += OnFileSystemEvent;
            watcher.Deleted += OnFileSystemEvent;
            watcher.Renamed += OnFileSystemEvent;
            watcher.EnableRaisingEvents = true;
            _watchers[path] = watcher;
        }
        catch
        {
            // A watcher failure (e.g. permissions) is non-fatal.
        }
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        // Coalesce bursts of events; refresh runs once on the UI thread.
        Dispatcher.BeginInvoke(() =>
        {
            _watchTimer.Stop();
            _watchTimer.Start();
        });
    }

    private void OnWatchTimerTick(object? sender, EventArgs e)
    {
        _watchTimer.Stop();
        RefreshTree();
    }

    // ================================================================
    //  Tabs & opening files
    // ================================================================

    /// <summary>
    /// Opens a file in a tab. When <paramref name="preview"/> is true the tab is
    /// a transient "sneak-peek": shown in italic, not persisted with the
    /// workspace, and dismissed as the tree selection moves. Opening a file
    /// normally pins (promotes) its sneak-peek tab if one is showing it.
    /// </summary>
    private void OpenFile(string path, bool preview = false)
    {
        var existing = _vm.Tabs.FirstOrDefault(
            t => string.Equals(t.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            if (!preview && existing.IsPreview)
                PromotePreview(existing);          // a normal open pins the peek
            if (preview && !ReferenceEquals(existing, _vm.SelectedTab))
                _suppressEditorFocus = true;       // a peek keeps focus in the tree
            _vm.SelectedTab = existing;
            return;
        }

        // A normal open replaces any pending sneak-peek.
        if (!preview)
            DiscardPreview();

        try
        {
            var content = AppServices.Files.ReadText(path);
            var type = AppServices.FileTypes.Resolve(path);
            var mode = InitialModeFor(path, type);

            var tab = new EditorTabViewModel(path, content, type, mode);
            if (IsScratchpadFile(path))
                tab.Title = "Scratchpad";   // friendly name, not the keyed file name
            else if (IsHelpFile(path))
                tab.Title = "Help";
            tab.PropertyChanged += OnTabPropertyChanged;
            _vm.Tabs.Add(tab);

            if (preview)
            {
                tab.IsPreview = true;
                _previewTab = tab;
                _suppressEditorFocus = true;   // keep focus in the tree
            }
            _vm.SelectedTab = tab;

            if (preview)
                return;                     // sneak-peeks are never recorded
            if (IsScratchpadFile(path))
                SaveSettingsNow();          // persist the open-files list
            else if (!IsHelpFile(path))
                AddRecentFile(path);        // (also persists settings)
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not open file:\n\n{ex.Message}", "Marker",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Turns a sneak-peek tab into a permanent one.</summary>
    private void PromotePreview(EditorTabViewModel tab)
    {
        tab.IsPreview = false;
        if (ReferenceEquals(tab, _previewTab))
            _previewTab = null;
        AddRecentFile(tab.FilePath);        // (also persists settings)
    }

    // --- quick open ---------------------------------------------------

    /// <summary>Directory names skipped when gathering files for quick-open.</summary>
    private static readonly HashSet<string> QuickOpenSkip =
        new(StringComparer.OrdinalIgnoreCase) { ".git", ".vs", "bin", "obj", "node_modules" };

    private void OnQuickOpenFile(object sender, RoutedEventArgs e) => OnQuickOpen();

    /// <summary>Type-to-find file picker across the active workspace's folders.</summary>
    private void OnQuickOpen()
    {
        if (_vm.RootFolders.Count == 0)
        {
            MessageBox.Show(this, "Add a folder to the workspace first.", "Marker");
            return;
        }

        var files = CollectWorkspaceFiles();
        if (files.Count == 0)
        {
            MessageBox.Show(this, "No files found in this workspace.", "Marker");
            return;
        }

        string? chosen = QuickOpenDialog.Show(this, files);
        if (chosen is not null)
            OpenFile(chosen);
    }

    /// <summary>
    /// Walks the workspace folders gathering files for the picker. Skips noisy
    /// build/VCS directories and caps the total so a huge tree can't stall.
    /// </summary>
    private List<QuickOpenFile> CollectWorkspaceFiles()
    {
        const int cap = 20000;
        var result = new List<QuickOpenFile>();

        foreach (var root in _vm.RootFolders)
        {
            string rootPath = root.Path;
            var pending = new Stack<string>();
            pending.Push(rootPath);

            while (pending.Count > 0 && result.Count < cap)
            {
                string dir = pending.Pop();

                try
                {
                    foreach (string file in Directory.EnumerateFiles(dir))
                    {
                        if (result.Count >= cap)
                            break;
                        result.Add(new QuickOpenFile(
                            file, Path.GetFileName(file),
                            Path.GetRelativePath(rootPath, file)));
                    }
                }
                catch { continue; }   // unreadable folder — skip it

                try
                {
                    foreach (string sub in Directory.EnumerateDirectories(dir))
                    {
                        if (!QuickOpenSkip.Contains(Path.GetFileName(sub)))
                            pending.Push(sub);
                    }
                }
                catch { /* unreadable folder — its files are simply absent */ }
            }
        }
        return result;
    }

    private static MarkdownMode InitialModeFor(string path, FileTypeInfo type)
    {
        if (!type.IsMarkdown)
            return MarkdownMode.Source;

        // The scratchpad is a writing surface — always start it editable.
        if (IsScratchpadFile(path))
            return MarkdownMode.Source;

        var settings = AppServices.Settings;
        if (settings.RememberModePerFile &&
            settings.FileModes.TryGetValue(path, out var saved))
            return saved;

        return settings.DefaultMarkdownMode;
    }

    // --- scratchpad ---------------------------------------------------

    /// <summary>Folder holding scratchpad files, alongside the settings file.</summary>
    private static string ScratchDirectory => Path.Combine(
        Path.GetDirectoryName(AppServices.SettingsStore.SettingsFilePath)!, "scratch");

    private static bool IsScratchpadFile(string path) => path.StartsWith(
        ScratchDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>The scratchpad file for a workspace, keyed by its name.</summary>
    private static string ScratchpadPathFor(string workspaceName)
    {
        string key = workspaceName;
        foreach (char c in Path.GetInvalidFileNameChars())
            key = key.Replace(c, '_');
        key = key.Trim();
        if (key.Length == 0)
            key = "default";
        // Markdown so the scratchpad gets highlighting and the view modes.
        return Path.Combine(ScratchDirectory, key + ".md");
    }

    /// <summary>The scratchpad file for the active workspace.</summary>
    private string ResolveScratchpadPath()
        => ScratchpadPathFor(_activeWorkspace?.Name ?? "default");

    /// <summary>Opens (creating if needed) the current project's scratchpad.</summary>
    private void OnOpenScratchpad(object sender, RoutedEventArgs e)
    {
        try
        {
            string path = ResolveScratchpadPath();
            Directory.CreateDirectory(ScratchDirectory);
            // Carry over a legacy plain-text scratchpad to the markdown file.
            string legacy = Path.ChangeExtension(path, ".txt");
            if (!File.Exists(path) && File.Exists(legacy))
                File.Move(legacy, path);
            if (!File.Exists(path))
                AppServices.Files.CreateFile(path);
            OpenFile(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not open the scratchpad:\n\n{ex.Message}",
                "Marker", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // --- help ---------------------------------------------------------

    /// <summary>The bundled help document, copied next to the executable.</summary>
    private static string HelpFilePath
        => Path.Combine(AppContext.BaseDirectory, "Assets", "help.md");

    private static bool IsHelpFile(string path) => string.Equals(
        path, HelpFilePath, StringComparison.OrdinalIgnoreCase);

    /// <summary>Opens the bundled help document in a tab, rendered (read mode).</summary>
    private void OnHelp(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(HelpFilePath))
        {
            MessageBox.Show(this, "The help file could not be found.", "Marker");
            return;
        }
        OpenFile(HelpFilePath);
        SetMode(MarkdownMode.Read);
    }

    private void ReopenFiles(Workspace ws)
    {
        foreach (string path in ws.OpenFiles.ToList())
        {
            if (File.Exists(path))
                OpenFile(path);
        }
    }

    private void OnTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender == _vm.SelectedTab && e.PropertyName == nameof(EditorTabViewModel.IsDirty))
            UpdateTitle();
    }

    private void OnSelectedTabChanged()
    {
        // Auto-save the tab we are leaving, then show the newly selected one.
        if (AppServices.Settings.AutoSave &&
            _currentTab is { IsDirty: true, IsBinary: false } leaving &&
            leaving != _vm.SelectedTab)
        {
            SaveTab(leaving);
        }
        ApplyTab();
    }

    private void OnTabPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle &&
            sender is ListBoxItem { DataContext: EditorTabViewModel tab })
        {
            CloseTab(tab);
            e.Handled = true;
        }
    }

    private void OnCloseTabButton(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EditorTabViewModel tab })
            CloseTab(tab);
    }

    private void CloseTab(EditorTabViewModel tab)
    {
        if (tab.IsDirty && !tab.IsBinary)
        {
            if (AppServices.Settings.AutoSave)
            {
                SaveTab(tab);
            }
            else
            {
                var answer = MessageBox.Show(this,
                    $"Save changes to {tab.Title}?", "Marker",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (answer == MessageBoxResult.Cancel)
                    return;
                if (answer == MessageBoxResult.Yes)
                    SaveTab(tab);
            }
        }

        int index = _vm.Tabs.IndexOf(tab);
        tab.PropertyChanged -= OnTabPropertyChanged;
        if (_webTab == tab)
            _webTab = null;
        if (_previewTab == tab)
            _previewTab = null;
        _vm.Tabs.Remove(tab);

        if (_vm.SelectedTab == tab || _vm.SelectedTab is null)
            _vm.SelectedTab = _vm.Tabs.Count == 0
                ? null
                : _vm.Tabs[Math.Min(index, _vm.Tabs.Count - 1)];

        SaveSettingsNow();
    }

    // --- tab context menu --------------------------------------------

    /// <summary>The tab a tab-context-menu item belongs to.</summary>
    private static EditorTabViewModel? TabFromMenu(object sender)
        => (sender as FrameworkElement)?.DataContext as EditorTabViewModel;

    private void OnTabCtxClose(object sender, RoutedEventArgs e)
    {
        if (TabFromMenu(sender) is { } tab)
            CloseTab(tab);
    }

    private void OnTabCtxCloseOthers(object sender, RoutedEventArgs e)
    {
        if (TabFromMenu(sender) is not { } keep)
            return;
        foreach (var tab in _vm.Tabs.ToList())
        {
            if (tab != keep)
                CloseTab(tab);
        }
    }

    private void OnTabCtxCloseAll(object sender, RoutedEventArgs e)
    {
        foreach (var tab in _vm.Tabs.ToList())
            CloseTab(tab);
    }

    private void OnTabCtxReveal(object sender, RoutedEventArgs e)
    {
        if (TabFromMenu(sender) is not { } tab)
            return;
        try
        {
            Process.Start(new ProcessStartInfo(
                "explorer.exe", $"/select,\"{tab.FilePath}\"") { UseShellExecute = true });
        }
        catch
        {
            // Explorer failing to launch is not worth interrupting the user.
        }
    }

    private void OnTabCtxCopyPath(object sender, RoutedEventArgs e)
    {
        if (TabFromMenu(sender) is not { } tab)
            return;
        try { Clipboard.SetText(tab.FilePath); }
        catch { /* clipboard can be briefly locked by another app */ }
    }

    private void CycleTab(int direction)
    {
        if (_vm.Tabs.Count < 2)
            return;
        int index = _vm.SelectedTab is null ? 0 : _vm.Tabs.IndexOf(_vm.SelectedTab);
        index = (index + direction + _vm.Tabs.Count) % _vm.Tabs.Count;
        _vm.SelectedTab = _vm.Tabs[index];
    }

    // ================================================================
    //  Editor host: source / rich / read
    // ================================================================

    private void ApplyTab()
    {
        var tab = _vm.SelectedTab;
        _currentTab = tab;
        // Sneak-peeks keep focus in the tree; consume the flag for this pass.
        bool keepTreeFocus = _suppressEditorFocus;
        _suppressEditorFocus = false;
        UpdateTitle();
        UpdateModeMenu();

        if (tab is null)
        {
            ShowHost(EmptyState);
            return;
        }

        if (tab.IsBinary)
        {
            ShowHost(BinaryNotice);
            return;
        }

        // Keep the editor document in sync so returning to source is instant.
        Editor.Document = tab.Document;

        bool useWeb = tab.IsMarkdown && tab.Mode != MarkdownMode.Source && _webAvailable && _webReady;
        if (useWeb)
        {
            ShowHost(Web);
            if (tab.Mode == MarkdownMode.Rich)
                NavigateRich(tab);
            else
                NavigateRead(tab);
        }
        else
        {
            Editor.SyntaxHighlighting = ResolveHighlighting(tab.FileType.HighlightingName);
            ShowHost(Editor);
            if (!keepTreeFocus)
                Dispatcher.BeginInvoke(() => Editor.TextArea.Focus(), DispatcherPriority.Input);
        }
    }

    private void ShowHost(UIElement target)
    {
        Editor.Visibility = target == Editor ? Visibility.Visible : Visibility.Collapsed;
        Web.Visibility = target == Web ? Visibility.Visible : Visibility.Collapsed;
        BinaryNotice.Visibility = target == BinaryNotice ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = target == EmptyState ? Visibility.Visible : Visibility.Collapsed;
    }

    private static IHighlightingDefinition? ResolveHighlighting(string? name)
        => name is null ? null : HighlightingManager.Instance.GetDefinition(name);

    private void NavigateRich(EditorTabViewModel tab)
    {
        _webTab = tab;
        _webMode = MarkdownMode.Rich;
        Web.CoreWebView2.Navigate(
            $"https://marker.assets/editor.html?theme={ThemeManager.CurrentTheme}");
    }

    private void NavigateRead(EditorTabViewModel tab)
    {
        _webTab = tab;
        _webMode = MarkdownMode.Read;
        string html = BuildReadHtml(tab.Document.Text);
        File.WriteAllText(ReadFilePath, html, new UTF8Encoding(false));
        Web.CoreWebView2.Navigate($"https://marker.assets/__read.html?v={Environment.TickCount}");
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.TryGetWebMessageAsString());
            string type = doc.RootElement.GetProperty("type").GetString() ?? "";

            if (type == "ready" && _webMode == MarkdownMode.Rich && _webTab is not null)
            {
                PushMarkdownToEditor(_webTab.Document.Text);
            }
            else if (type == "change" && _webTab is not null)
            {
                string markdown = doc.RootElement.GetProperty("markdown").GetString() ?? "";
                // Updating the document raises TextChanged, which flags the tab dirty.
                if (_webTab.Document.Text != markdown)
                    _webTab.Document.Text = markdown;
            }
        }
        catch
        {
            // Ignore malformed messages.
        }
    }

    private void PushMarkdownToEditor(string markdown)
    {
        string payload = JsonSerializer.Serialize(new { type = "setMarkdown", markdown });
        Web.CoreWebView2.PostWebMessageAsString(payload);
    }

    // ================================================================
    //  Markdown mode switching
    // ================================================================

    private void OnModeSource(object sender, RoutedEventArgs e) => SetMode(MarkdownMode.Source);
    private void OnModeRich(object sender, RoutedEventArgs e) => SetMode(MarkdownMode.Rich);
    private void OnModeRead(object sender, RoutedEventArgs e) => SetMode(MarkdownMode.Read);

    private void OnCycleMode(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedTab is not { IsMarkdown: true } tab)
            return;
        var next = tab.Mode switch
        {
            MarkdownMode.Source => MarkdownMode.Rich,
            MarkdownMode.Rich => MarkdownMode.Read,
            _ => MarkdownMode.Source
        };
        SetMode(next);
    }

    private void SetMode(MarkdownMode mode)
    {
        if (_vm.SelectedTab is not { IsMarkdown: true } tab)
            return;

        tab.Mode = mode;

        var settings = AppServices.Settings;
        if (settings.RememberModePerFile)
            settings.FileModes[tab.FilePath] = mode;
        else
            settings.DefaultMarkdownMode = mode;

        ApplyTab();
        SaveSettingsNow();
    }

    private void UpdateModeMenu()
    {
        var tab = _vm.SelectedTab;
        bool markdown = tab is { IsMarkdown: true };

        ModeSourceItem.IsEnabled = ModeRichItem.IsEnabled = ModeReadItem.IsEnabled = markdown;
        ModeSourceItem.IsChecked = markdown && tab!.Mode == MarkdownMode.Source;
        ModeRichItem.IsChecked = markdown && tab!.Mode == MarkdownMode.Rich;
        ModeReadItem.IsChecked = markdown && tab!.Mode == MarkdownMode.Read;
    }

    // ================================================================
    //  Saving
    // ================================================================

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedTab is { IsBinary: false } tab)
            SaveTab(tab);
    }

    private void OnSaveAll(object sender, RoutedEventArgs e) => SaveAllDirty(silent: false);

    private void SaveAllDirty(bool silent)
    {
        foreach (var tab in _vm.Tabs.Where(t => t.IsDirty && !t.IsBinary).ToList())
            SaveTab(tab, silent);
    }

    private void SaveTab(EditorTabViewModel tab, bool silent = false)
    {
        try
        {
            AppServices.Files.WriteText(tab.FilePath, tab.ToContent());
            tab.IsDirty = false;
            if (tab == _vm.SelectedTab)
                UpdateTitle();
        }
        catch (Exception ex)
        {
            if (!silent)
                MessageBox.Show(this, $"Could not save {tab.Title}:\n\n{ex.Message}",
                    "Marker", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ================================================================
    //  Tree context-menu file operations
    // ================================================================

    private string? TargetDirectory()
    {
        var node = _vm.SelectedNode;
        if (node is null)
            return _vm.RootFolders.FirstOrDefault()?.Path;
        return node.IsDirectory ? node.Path : Path.GetDirectoryName(node.Path);
    }

    private void OnNewFile(object sender, RoutedEventArgs e)
    {
        string? dir = TargetDirectory();
        if (dir is null)
        {
            MessageBox.Show(this, "Add a workspace folder first.", "Marker");
            return;
        }

        string? name = PromptDialog.Show(this, "New File", "File name:", "untitled.md");
        if (name is null)
            return;

        string path = Path.Combine(dir, name);
        if (File.Exists(path))
        {
            MessageBox.Show(this, "A file with that name already exists.", "Marker");
            return;
        }

        AppServices.Files.CreateFile(path);
        RefreshTree();
        OpenFile(path);
    }

    private void OnNewFolder(object sender, RoutedEventArgs e)
    {
        string? dir = TargetDirectory();
        if (dir is null)
        {
            MessageBox.Show(this, "Add a workspace folder first.", "Marker");
            return;
        }

        string? name = PromptDialog.Show(this, "New Folder", "Folder name:", "New Folder");
        if (name is null)
            return;

        AppServices.Files.CreateDirectory(Path.Combine(dir, name));
        RefreshTree();
    }

    private void OnRename(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedNode is not { IsPlaceholder: false } node)
            return;

        string? name = PromptDialog.Show(this, "Rename", "New name:", node.Name);
        if (name is null || name == node.Name)
            return;

        try
        {
            string oldPath = node.Path;
            string newPath = AppServices.Files.Rename(oldPath, name);
            RebaseOpenTabs(oldPath, newPath);

            if (node.IsWorkspaceRoot)
            {
                // The workspace folder list is rebuilt from the tree roots on
                // save — just move the watcher and rebase the node.
                if (_watchers.Remove(oldPath, out var w))
                    w.Dispose();
                CreateWatcher(newPath);
                node.Rebase(newPath);
            }
            RefreshTree();
            SaveSettingsNow();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Rename failed:\n\n{ex.Message}", "Marker",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedNode is not { IsPlaceholder: false } node)
            return;

        if (MessageBox.Show(this, $"Move '{node.Name}' to the Recycle Bin?", "Marker",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            return;

        try
        {
            AppServices.Files.DeleteToRecycleBin(node.Path);
            CloseTabsUnder(node.Path);

            if (node.IsWorkspaceRoot)
            {
                _vm.RootFolders.Remove(node);
                if (_watchers.Remove(node.Path, out var w))
                    w.Dispose();
                UpdateTreeHint();
            }
            RefreshTree();
            SaveSettingsNow();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Delete failed:\n\n{ex.Message}", "Marker",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnRevealInExplorer(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedNode is not { IsPlaceholder: false } node)
            return;
        try
        {
            if (node.IsDirectory)
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{node.Path}\"") { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{node.Path}\"") { UseShellExecute = true });
        }
        catch
        {
            // Explorer failing to launch is not worth interrupting the user.
        }
    }

    private void RebaseOpenTabs(string oldPath, string newPath)
    {
        foreach (var tab in _vm.Tabs)
        {
            if (string.Equals(tab.FilePath, oldPath, StringComparison.OrdinalIgnoreCase))
            {
                tab.UpdatePath(newPath);
            }
            else if (tab.FilePath.StartsWith(oldPath + Path.DirectorySeparatorChar,
                         StringComparison.OrdinalIgnoreCase))
            {
                tab.UpdatePath(newPath + tab.FilePath[oldPath.Length..]);
            }
        }
        UpdateTitle();
    }

    private void CloseTabsUnder(string path)
    {
        foreach (var tab in _vm.Tabs.ToList())
        {
            bool match = string.Equals(tab.FilePath, path, StringComparison.OrdinalIgnoreCase)
                || tab.FilePath.StartsWith(path + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
            if (match)
            {
                tab.IsDirty = false; // already deleted — don't prompt to save
                CloseTab(tab);
            }
        }
    }

    // ================================================================
    //  Recent files
    // ================================================================

    private void AddRecentFile(string path)
    {
        var recent = AppServices.Settings.RecentFiles;
        recent.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        recent.Insert(0, path);
        if (recent.Count > 12)
            recent.RemoveRange(12, recent.Count - 12);
        RebuildRecentMenu();
        SaveSettingsNow();
    }

    private void RebuildRecentMenu()
    {
        RecentMenu.Items.Clear();
        var recent = AppServices.Settings.RecentFiles;

        if (recent.Count == 0)
        {
            RecentMenu.Items.Add(new MenuItem { Header = "(none)", IsEnabled = false });
            return;
        }

        foreach (string path in recent)
        {
            var item = new MenuItem { Header = path };
            item.Click += (_, _) =>
            {
                if (File.Exists(path))
                    OpenFile(path);
            };
            RecentMenu.Items.Add(item);
        }
    }

    // ================================================================
    //  Edit menu / theme
    // ================================================================

    private void OnUndo(object sender, RoutedEventArgs e)
    {
        if (Editor.CanUndo) Editor.Undo();
    }

    private void OnRedo(object sender, RoutedEventArgs e)
    {
        if (Editor.CanRedo) Editor.Redo();
    }

    private void OnFind(object sender, RoutedEventArgs e)
    {
        if (Editor.Visibility != Visibility.Visible)
            return;
        var panel = SearchPanel.Install(Editor);
        panel.Open();
        if (!string.IsNullOrEmpty(Editor.SelectedText))
            panel.SearchPattern = Editor.SelectedText;
        Dispatcher.BeginInvoke(() => panel.Reactivate(), DispatcherPriority.Input);
    }

    private void OnReplace(object sender, RoutedEventArgs e)
    {
        if (_replaceDialog is null || !_replaceDialog.IsLoaded)
        {
            _replaceDialog = new ReplaceDialog(() => Editor) { Owner = this };
            _replaceDialog.Closed += (_, _) => _replaceDialog = null;
        }
        _replaceDialog.Show();
        _replaceDialog.Activate();
    }

    private void OnToggleWordWrap(object sender, RoutedEventArgs e)
    {
        bool wrap = WordWrapItem.IsChecked;
        Editor.WordWrap = wrap;
        AppServices.Settings.WordWrap = wrap;
        SaveSettingsNow();
    }

    private void OnToggleTheme(object sender, RoutedEventArgs e)
    {
        string theme = ThemeManager.Toggle();
        AppServices.Settings.Theme = theme;
        ApplyTitleBarTheme();

        // Re-render the web view so rich/read modes pick up the new theme.
        if (Web.Visibility == Visibility.Visible && _webTab is not null)
            ApplyTab();

        SaveSettingsNow();
    }

    private void OnSetFontSize(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string tag } && double.TryParse(tag, out double size))
        {
            AppServices.Settings.FontSize = size;
            Editor.FontSize = size;
            UpdateFontSizeMenu();
            SaveSettingsNow();
        }
    }

    /// <summary>Ticks the font-size menu entry matching the current setting.</summary>
    private void UpdateFontSizeMenu()
    {
        foreach (object obj in FontSizeMenu.Items)
        {
            if (obj is MenuItem { Tag: string tag } item && double.TryParse(tag, out double size))
                item.IsChecked = Math.Abs(size - AppServices.Settings.FontSize) < 0.01;
        }
    }

    // ================================================================
    //  Misc
    // ================================================================

    private void OnCloseTab(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedTab is { } tab)
            CloseTab(tab);
    }

    private void OnExit(object sender, RoutedEventArgs e) => Close();

    private void UpdateTitle()
    {
        var tab = _vm.SelectedTab;
        Title = tab is null
            ? "Marker"
            : $"{(tab.IsDirty ? "● " : "")}{tab.Title} — Marker";
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        switch (e.Key)
        {
            case Key.S when ctrl && shift:
                SaveAllDirty(silent: false); e.Handled = true; break;
            case Key.S when ctrl:
                OnSave(sender, e); e.Handled = true; break;
            case Key.N when ctrl:
                OnNewFile(sender, e); e.Handled = true; break;
            case Key.P when ctrl:
                OnQuickOpen(); e.Handled = true; break;
            case Key.W when ctrl && shift:
                OpenWorkspaceSwitcher(); e.Handled = true; break;
            case Key.W when ctrl:
                OnCloseTab(sender, e); e.Handled = true; break;
            case Key.H when ctrl:
                OnReplace(sender, e); e.Handled = true; break;
            case Key.F when ctrl:
                OnFind(sender, e); e.Handled = true; break;
            case Key.Tab when ctrl:
                CycleTab(shift ? -1 : 1); e.Handled = true; break;
            case Key.M when ctrl:
                OnCycleMode(sender, e); e.Handled = true; break;
            case Key.T when ctrl:
                FocusTree(); e.Handled = true; break;
            case >= Key.D1 and <= Key.D9 when ctrl:
                SelectTabByNumber(e.Key - Key.D1); e.Handled = true; break;
        }
    }

    /// <summary>Selects the nth open tab (0-based); ignores out-of-range indices.</summary>
    private void SelectTabByNumber(int index)
    {
        if (index >= 0 && index < _vm.Tabs.Count)
            _vm.SelectedTab = _vm.Tabs[index];
    }

    // ================================================================
    //  Keyboard cheat sheet
    // ================================================================

    private sealed record Shortcut(string Key, string Action);

    private void OnShowCheatsheet(object sender, RoutedEventArgs e)
        => CheatsheetPopup.IsOpen = true;

    /// <summary>
    /// Bubbling key handler — only reached by an Escape that no child control
    /// consumed (no search bar or dialog to dismiss). That spare Esc toggles
    /// the cheat sheet.
    /// </summary>
    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;
        CheatsheetPopup.IsOpen = !CheatsheetPopup.IsOpen;
        e.Handled = true;
    }

    private static Shortcut[] BuildCheatsheet() =>
    [
        new("Ctrl+P", "Quick-open a file"),
        new("Ctrl+S", "Save  ·  Ctrl+Shift+S  save all"),
        new("Ctrl+N", "New file"),
        new("Ctrl+W", "Close tab"),
        new("Ctrl+Tab", "Next / previous tab"),
        new("Ctrl+1…9", "Jump to tab 1–9"),
        new("Ctrl+F", "Find  ·  Ctrl+H  replace"),
        new("Ctrl+M", "Cycle markdown mode"),
        new("Ctrl+T", "Focus the file tree"),
        new("Ctrl+Shift+W", "Switch workspace"),
        new("Alt", "Open the menu bar"),
        new("Esc", "Show / hide this cheat sheet"),
    ];

    /// <summary>Builds the standalone HTML document shown in read mode.</summary>
    private static string BuildReadHtml(string markdown)
    {
        string body = AppServices.Markdown.RenderToHtmlFragment(markdown);
        bool dark = ThemeManager.CurrentTheme == "dark";
        string hljs = dark ? "hljs-dark.css" : "hljs-light.css";
        string bg = dark ? "#1e1e1e" : "#ffffff";
        string fg = dark ? "#e4e4e4" : "#1e1e1e";
        string border = dark ? "#3c3c3c" : "#d6d6d6";
        string codeBg = dark ? "#2d2d2d" : "#f4f4f4";

        return ReadHtmlTemplate
            .Replace("{{HLJS}}", hljs)
            .Replace("{{BG}}", bg)
            .Replace("{{FG}}", fg)
            .Replace("{{BORDER}}", border)
            .Replace("{{CODEBG}}", codeBg)
            .Replace("{{BODY}}", body);
    }

    private const string ReadHtmlTemplate = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<link rel="stylesheet" href="{{HLJS}}" />
<style>
  html, body { margin: 0; background: {{BG}}; color: {{FG}}; }
  body {
    font-family: 'Segoe UI', system-ui, sans-serif;
    line-height: 1.6; padding: 32px 48px; max-width: 900px; margin: 0 auto;
  }
  h1, h2 { border-bottom: 1px solid {{BORDER}}; padding-bottom: .25em; }
  a { color: #4f8cff; }
  code {
    background: {{CODEBG}}; padding: .15em .35em; border-radius: 4px;
    font-family: 'Cascadia Mono', Consolas, monospace; font-size: .9em;
  }
  pre { background: {{CODEBG}}; padding: 14px; border-radius: 6px; overflow: auto; }
  pre code { background: none; padding: 0; }
  blockquote {
    margin: 0; padding: .2em 1em; border-left: 4px solid {{BORDER}};
    color: #888;
  }
  table { border-collapse: collapse; }
  th, td { border: 1px solid {{BORDER}}; padding: 6px 12px; }
  img { max-width: 100%; }
</style>
</head>
<body>
<article>{{BODY}}</article>
<script src="highlight.min.js"></script>
<script>hljs.highlightAll();</script>
</body>
</html>
""";
}
