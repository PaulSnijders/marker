using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Marker.App.ViewModels;

/// <summary>
/// One file's worth of replace previews — parent node in the Replace tree,
/// with <see cref="ReplacePreviewItem"/> children. Mirrors
/// <see cref="FileMatchesViewModel"/> so the two tabs feel consistent.
/// </summary>
public sealed partial class FileReplacesViewModel : ObservableObject
{
    [ObservableProperty] private bool _isExpanded = true;

    public string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath);
    public string RelativePath { get; }

    public ObservableCollection<ReplacePreviewItem> Items { get; } = new();

    public FileReplacesViewModel(string filePath, string relativePath)
    {
        FilePath = filePath;
        RelativePath = relativePath;
    }
}
