using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Marker.App.Services;

namespace Marker.App.ViewModels;

/// <summary>
/// A node in the workspace file tree. Directories load their children lazily
/// on first expansion to keep large folders fast.
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

    public ObservableCollection<FileSystemNodeViewModel> Children { get; } = new();

    private bool _loaded;

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

    private static FileSystemNodeViewModel CreatePlaceholder() => new();

    // --- icon ---------------------------------------------------------

    /// <summary>Segoe MDL2 glyph; folders vs. files, no heavy icon theme.</summary>
    public string Glyph => IsDirectory ? "" : ""; // Folder / Document

    public string GlyphColor => IsDirectory ? "#E3B341" : ExtensionColor();

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
        if (value && IsDirectory && !_loaded)
            LoadChildren();
    }

    /// <summary>Lists this directory and builds child nodes (one level deep).</summary>
    public void LoadChildren()
    {
        _loaded = true;
        Children.Clear();

        if (!IsDirectory || !AppServices.Files.DirectoryExists(Path))
            return;

        foreach (var entry in AppServices.Files.List(Path))
        {
            if (IsIgnored(entry.Name))
                continue;
            Children.Add(new FileSystemNodeViewModel(entry.Path, entry.IsDirectory));
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

        var current = AppServices.Files.List(Path)
            .Where(e => !IsIgnored(e.Name))
            .ToList();
        var currentPaths = current.Select(e => e.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Drop nodes that disappeared.
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (!Children[i].IsPlaceholder && !currentPaths.Contains(Children[i].Path))
                Children.RemoveAt(i);
        }

        var existing = Children.Where(c => !c.IsPlaceholder)
            .ToDictionary(c => c.Path, StringComparer.OrdinalIgnoreCase);

        // Add new entries; recurse into already-expanded directories.
        for (int i = 0; i < current.Count; i++)
        {
            var entry = current[i];
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
