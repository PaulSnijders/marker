namespace Marker.App.Views;

/// <summary>
/// Carried by <see cref="FindInFilesView.MatchActivated"/> when the user
/// double-clicks (or presses Enter on) a result row. The host opens the
/// file and jumps the editor to the match.
/// </summary>
public sealed class MatchActivatedEventArgs : EventArgs
{
    public string FilePath { get; }
    public int LineNumber { get; }
    public int ColumnStart { get; }
    public int Length { get; }

    public MatchActivatedEventArgs(string filePath, int lineNumber, int columnStart, int length)
    {
        FilePath = filePath;
        LineNumber = lineNumber;
        ColumnStart = columnStart;
        Length = length;
    }
}
