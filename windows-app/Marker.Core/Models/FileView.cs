namespace Marker.Core.Models;

/// <summary>
/// Which kind of host shows a file. Adding a format = a value here, a line in
/// <see cref="FileTypes.FileTypeRegistry"/> and a case where the app picks the host.
/// </summary>
public enum FileView
{
    /// <summary>Text editor only.</summary>
    Text,

    /// <summary>Text editor plus rich/read markdown views.</summary>
    Markdown,

    /// <summary>Text editor plus an editable grid (CSV/TSV).</summary>
    Table,

    /// <summary>Raster image, shown read-only.</summary>
    Image,

    /// <summary>PDF, shown read-only by the WebView2's built-in viewer.</summary>
    Pdf
}
