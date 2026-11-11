namespace Marker.Core.Models;

/// <summary>
/// Describes how Marker treats a particular file extension: which AvalonEdit
/// highlighting to apply and which host / view modes show it.
/// </summary>
/// <param name="DisplayName">Human-readable type name shown in the status bar.</param>
/// <param name="HighlightingName">
/// Name of the AvalonEdit highlighting definition, or <c>null</c> for plain text.
/// Built-in names ("XML") or custom ones ("Markdown", "JSON", "YAML").
/// </param>
/// <param name="View">Which host shows the file.</param>
public sealed record FileTypeInfo(
    string DisplayName,
    string? HighlightingName,
    FileView View = FileView.Text)
{
    private static readonly ViewMode[] SourceOnly = [ViewMode.Source];
    private static readonly ViewMode[] MarkdownModes = [ViewMode.Source, ViewMode.Rich, ViewMode.Read];
    private static readonly ViewMode[] TableModes = [ViewMode.Source, ViewMode.Grid];

    /// <summary>The view modes this type can switch between (Ctrl+M cycles them).</summary>
    public IReadOnlyList<ViewMode> Modes => View switch
    {
        FileView.Markdown => MarkdownModes,
        FileView.Table => TableModes,
        _ => SourceOnly
    };

    /// <summary>True when the file's text is loaded and can be edited/saved.</summary>
    public bool IsText => View is FileView.Text or FileView.Markdown or FileView.Table;
}
