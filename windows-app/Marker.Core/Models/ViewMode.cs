namespace Marker.Core.Models;

/// <summary>
/// The ways a tab can be shown. Which ones apply depends on the file type
/// (see <see cref="FileTypeInfo.Modes"/>); every editable type has Source.
/// </summary>
public enum ViewMode
{
    /// <summary>Plain text with syntax highlighting (default).</summary>
    Source,

    /// <summary>Markdown: WYSIWYG editing via the embedded TOAST UI editor.</summary>
    Rich,

    /// <summary>Markdown: rendered, read-only HTML output.</summary>
    Read,

    /// <summary>CSV/TSV: editable spreadsheet-like grid.</summary>
    Grid
}
