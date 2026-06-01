namespace Marker.Core.Search;

/// <summary>
/// One match the searcher found in one file. <see cref="LineText"/> is the
/// raw line (without the line terminator) so the UI can show context;
/// <see cref="ColumnStart"/> is a 0-based offset into that line.
/// </summary>
public sealed record SearchMatch(
    string FilePath,
    int LineNumber,
    int ColumnStart,
    int Length,
    string LineText);
