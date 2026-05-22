namespace Marker.Core.Models;

/// <summary>
/// Describes how Marker treats a particular file extension: which AvalonEdit
/// highlighting to apply and whether markdown view modes are available.
/// </summary>
/// <param name="DisplayName">Human-readable type name shown in the status bar.</param>
/// <param name="HighlightingName">
/// Name of the AvalonEdit highlighting definition, or <c>null</c> for plain text.
/// Built-in names ("XML") or custom ones ("Markdown", "JSON", "YAML").
/// </param>
/// <param name="IsMarkdown">True when the Source/Rich/Read mode switch applies.</param>
public sealed record FileTypeInfo(
    string DisplayName,
    string? HighlightingName,
    bool IsMarkdown);
