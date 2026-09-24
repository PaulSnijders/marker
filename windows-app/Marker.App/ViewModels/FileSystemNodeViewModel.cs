using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Marker.App.Services;
using Marker.Core.FileSystem;

namespace Marker.App.ViewModels;

/// <summary>
/// A node in the workspace file tree. Directories load their children lazily
/// on first expansion to keep large folders fast.
/// A long run of numbered or dated files ("0001-…", "2026-09-23-…") is trimmed
/// to its last few, with a clickable "(....)" node standing in for the rest.
/// </summary>
public sealed partial class FileSystemNodeViewModel : ObservableObject
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isSelected;

    public string Path { get; private set; }
    public bool IsDirectory { get; }
    public bool IsWorkspaceRoot { get; }
    public bool IsPlaceholder { get; }

    /// <summary>The "(....)" node standing in for hidden numbered files.</summary>
    public bool IsHiddenGroup { get; }
    public string? ToolTip { get; }

    public ObservableCollection<FileSystemNodeViewModel> Children { get; } = new();

    private bool _loaded;

    // More than GroupThreshold files matching NumberedName → only the last
    // GroupKeep are shown until the user clicks the "(....)" node.
    private const int GroupThreshold = 20;
    private const int GroupKeep = 10;
    private const int MaxGroupDots = 20;
    private static readonly Regex NumberedName = new(@"^\d+[A-Za-z]?-", RegexOptions.Compiled);

    private bool _showAllNumbered;
    private readonly FileSystemNodeViewModel? _owner;   // hidden group → its directory

    /// <summary>Real file/directory node.</summary>
    public FileSystemNodeViewModel(string path, bool isDirectory, bool isWorkspaceRoot = false)
    {
        Path = path;
        IsDirectory = isDirectory;
        IsWorkspaceRoot = isWorkspaceRoot;
        _name = isWorkspaceRoot ? DescribeRoot(path) : System.IO.Path.GetFileName(path);

        // A lazy directory shows an expander arrow via this dummy child.
        if (IsDirectory)
            Children.Add(CreatePlaceholder());
    }

    /// <summary>"Loading…" placeholder constructor.</summary>
    private FileSystemNodeViewModel()
    {
        Path = string.Empty;
        _name = "Loading…";
        IsPlaceholder = true;
    }

    /// <summary>Hidden-group constructor. Also a placeholder, so the file actions skip it.</summary>
    private FileSystemNodeViewModel(FileSystemNodeViewModel owner, int hiddenCount)
    {
        Path = string.Empty;
        _name = "(" + new string('.', Math.Min(hiddenCount, MaxGroupDots)) + ")";
        ToolTip = $"{hiddenCount} older files hidden — click to show";
        IsPlaceholder = true;
        IsHiddenGroup = true;
        _owner = owner;
    }

    private static FileSystemNodeViewModel CreatePlaceholder() => new();

    // --- icon ---------------------------------------------------------

    /// <summary>Segoe MDL2 glyph; folders vs. files, no heavy icon theme.</summary>
    public string Glyph => IsHiddenGroup ? "" : IsDirectory ? "" : ""; // ChevronDown / Folder / Document

    public string GlyphColor => IsHiddenGroup ? "#8C8C8C" : IsDirectory ? "#E3B341" : ExtensionColor();

    private string ExtensionColor() => System.IO.Path.GetExtension(Path).ToLowerInvariant() switch
    {
        ".md" or ".markdown" => "#4F86C6",
        ".json"              => "#C18401",
        ".xml" or ".html" or ".htm" => "#2AA198",
        ".yaml" or ".yml"    => "#A24B57",
        ".csv" or ".log"     => "#6A9955",
        _                    => "#8C8C8C",
    };

    // --- lazy loading -------------------------------------------------

    partial void OnIsExpandedChanged(bool value)
    {
        if (!IsDirectory)
            return;

        if (!value)
            _showAllNumbered = false;   // re-trim on the next expand
        else if (!_loaded)
            LoadChildren();
        else
            Refresh();                  // collapsed folders aren't watched, catch up now
    }

    /// <summary>
    /// Reveals the files behind this hidden-group node. Returns the node that
    /// now sits where the group was (the first revealed file), for selection.
    /// </summary>
    public FileSystemNodeViewModel? ExpandHiddenGroup()
    {
        if (_owner is null)
            return null;
        int index = _owner.Children.IndexOf(this);
        _owner._showAllNumbered = true;
        _owner.Refresh();
        return index >= 0 && index < _owner.Children.Count ? _owner.Children[index] : null;
    }

    /// <summary>
    /// The directory listing as shown: ignored entries dropped and, unless
    /// expanded, all but the last numbered files replaced by one null entry
    /// marking where the hidden group goes.
    /// </summary>
    private List<FileSystemEntry?> VisibleEntries(out int hiddenCount)
    {
        var entries = AppServices.Files.List(Path)
            .Where(e => !IsIgnored(e.Name))
            .ToList<FileSystemEntry?>();

        hiddenCount = 0;
        if (_showAllNumbered)
            return entries;

        var numbered = entries.Where(e => !e!.IsDirectory && NumberedName.IsMatch(e.Name)).ToList();
        if (numbered.Count <= GroupThreshold)
            return entries;

        var hidden = numbered.Take(numbered.Count - GroupKeep).ToHashSet();
        hiddenCount = hidden.Count;
        int groupIndex = entries.IndexOf(numbered[0]);
        entries.RemoveAll(hidden.Contains);
        entries.Insert(groupIndex, null);
        return entries;
    }

    /// <summary>Lists this directory and builds child nodes (one level deep).</summary>
    public void LoadChildren()
    {
        _loaded = true;
        Children.Clear();

        if (!IsDirectory || !AppServices.Files.DirectoryExists(Path))
            return;

        foreach (var entry in VisibleEntries(out int hiddenCount))
        {
            Children.Add(entry is null
                ? new FileSystemNodeViewModel(this, hiddenCount)
                : new FileSystemNodeViewModel(entry.Path, entry.IsDirectory));
        }
    }

    /// <summary>
    /// Re-reads this directory, merging changes while preserving the expansion
    /// state of nodes that still exist. Called when the watcher fires.
    /// </summary>
    public void Refresh()
    {
        if (!IsDirectory || !_loaded)
            return;

        if (!AppServices.Files.DirectoryExists(Path))
        {
            Children.Clear();
            _loaded = false;
            Children.Add(CreatePlaceholder());
            return;
        }

        var current = VisibleEntries(out int hiddenCount);
        var currentPaths = current.OfType<FileSystemEntry>()
            .Select(e => e.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Drop nodes that disappeared (or got hidden); the group is rebuilt below.
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (Children[i].IsHiddenGroup ||
                (!Children[i].IsPlaceholder && !currentPaths.Contains(Children[i].Path)))
                Children.RemoveAt(i);
        }

        var existing = Children.Where(c => !c.IsPlaceholder)
            .ToDictionary(c => c.Path, StringComparer.OrdinalIgnoreCase);

        // Add new entries; recurse into already-expanded directories.
        for (int i = 0; i < current.Count; i++)
        {
            var entry = current[i];
            if (entry is null)
            {
                Children.Insert(i, new FileSystemNodeViewModel(this, hiddenCount));
                continue;
            }
            if (existing.TryGetValue(entry.Path, out var node))
            {
                if (node.IsDirectory && node.IsExpanded)
                    node.Refresh();
            }
            else
            {
                Children.Insert(i, new FileSystemNodeViewModel(entry.Path, entry.IsDirectory));
            }
        }
    }

    private static bool IsIgnored(string name)
    {
        foreach (string pattern in AppServices.Settings.IgnorePatterns)
        {
            if (string.Equals(pattern, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string DescribeRoot(string path)
    {
        string name = new DirectoryInfo(path).Name;
        return string.IsNullOrEmpty(name) ? path : name;
    }

    /// <summary>Updates path/name after this node (or an ancestor) is renamed.</summary>
    public void Rebase(string newPath)
    {
        Path = newPath;
        Name = IsWorkspaceRoot ? DescribeRoot(newPath) : System.IO.Path.GetFileName(newPath);
    }
}
