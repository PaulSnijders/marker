using Marker.Core.Search;

namespace Marker.App.ViewModels;

/// <summary>
/// One row in the Replace preview tree: the original match plus the text
/// the matched substring would become if the user committed Replace All.
/// </summary>
public sealed record ReplacePreviewItem(SearchMatch Match, string Replacement)
{
    // Convenience accessors so the XAML stays clean.
    public int LineNumber => Match.LineNumber;
    public string LineText => Match.LineText;
    public int ColumnStart => Match.ColumnStart;
    public int Length => Match.Length;
}
