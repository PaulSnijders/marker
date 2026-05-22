namespace Marker.Core.Models;

/// <summary>The three view modes available for markdown files.</summary>
public enum MarkdownMode
{
    /// <summary>Plain markdown text with syntax highlighting (default).</summary>
    Source,

    /// <summary>WYSIWYG editing via the embedded TOAST UI editor.</summary>
    Rich,

    /// <summary>Rendered, read-only HTML output.</summary>
    Read
}
