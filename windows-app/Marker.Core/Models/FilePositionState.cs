namespace Marker.Core.Models;

/// <summary>
/// Per-file caret + scroll snapshot remembered with a workspace so re-opening
/// a tab (after a workspace switch or app restart) lands on the same line and
/// scroll offset instead of jumping to the top of the document.
/// </summary>
public sealed class FilePositionState
{
    /// <summary>1-based caret line as reported by AvalonEdit.</summary>
    public int CaretLine { get; set; } = 1;

    /// <summary>1-based caret column as reported by AvalonEdit.</summary>
    public int CaretColumn { get; set; } = 1;

    /// <summary>Vertical scroll offset in device units.</summary>
    public double VerticalOffset { get; set; }

    /// <summary>Horizontal scroll offset in device units.</summary>
    public double HorizontalOffset { get; set; }
}
