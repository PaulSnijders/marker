namespace Marker.App.Views;

/// <summary>
/// Raised after a Replace All commit. The host reloads any open editor tab
/// whose file shows up in <see cref="FilePaths"/> so the on-screen content
/// matches what's now on disk.
/// </summary>
public sealed class FilesReplacedEventArgs : EventArgs
{
    public IReadOnlyList<string> FilePaths { get; }

    public FilesReplacedEventArgs(IReadOnlyList<string> filePaths)
    {
        FilePaths = filePaths;
    }
}
