using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Marker.Core.Search;

namespace Marker.App.ViewModels;

/// <summary>
/// One file's worth of search hits in the Find-in-Files panel — the parent
/// node in the results tree, with the individual line matches as children.
/// Pure presentation; populated by <see cref="FindInFilesViewModel"/>.
/// </summary>
public sealed partial class FileMatchesViewModel : ObservableObject
{
    [ObservableProperty] private bool _isExpanded = true;

    public string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath);
    public string RelativePath { get; }

    public ObservableCollection<SearchMatch> Matches { get; } = new();

    public FileMatchesViewModel(string filePath, string relativePath)
    {
        FilePath = filePath;
        RelativePath = relativePath;
    }
}
