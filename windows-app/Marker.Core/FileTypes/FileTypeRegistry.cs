using System.IO;
using Marker.Core.Models;

namespace Marker.Core.FileTypes;

/// <summary>
/// Static extension-to-type table for v1. Adding a file type later is a
/// one-line entry here — the seam that keeps new formats cheap.
/// </summary>
public sealed class FileTypeRegistry : IFileTypeRegistry
{
    // HighlightingName values are AvalonEdit definition names: built-in "XML",
    // or custom ones loaded from Assets/Syntax (*.xshd).
    private static readonly Dictionary<string, FileTypeInfo> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        [".md"]       = new("Markdown", "Markdown", FileView.Markdown),
        [".markdown"] = new("Markdown", "Markdown", FileView.Markdown),
        [".txt"]      = new("Plain Text", null),
        [".json"]     = new("JSON", "JSON"),
        [".xml"]      = new("XML", "XML"),
        [".html"]     = new("HTML", "HTML"),
        [".htm"]      = new("HTML", "HTML"),
        [".yaml"]     = new("YAML", "YAML"),
        [".yml"]      = new("YAML", "YAML"),
        [".csv"]      = new("CSV", null, FileView.Table),
        [".tsv"]      = new("TSV", null, FileView.Table),
        [".log"]      = new("Log", null),
        [".sql"]      = new("SQL", "SQL"),

        // Images — shown inline in a dedicated host instead of the text editor.
        // .svg is intentionally absent: it stays text/binary (it's XML).
        [".png"]      = new("PNG image",  null, FileView.Image),
        [".jpg"]      = new("JPEG image", null, FileView.Image),
        [".jpeg"]     = new("JPEG image", null, FileView.Image),
        [".gif"]      = new("GIF image",  null, FileView.Image),
        [".bmp"]      = new("Bitmap",     null, FileView.Image),
        [".webp"]     = new("WebP image", null, FileView.Image),
        [".ico"]      = new("Icon",       null, FileView.Image),

        // Shown by the WebView2's built-in PDF viewer — no extra dependency.
        [".pdf"]      = new("PDF", null, FileView.Pdf),
    };

    private static readonly FileTypeInfo Unknown = new("Text", null);

    public FileTypeInfo Resolve(string path)
    {
        string ext = Path.GetExtension(path);
        return Map.TryGetValue(ext, out FileTypeInfo? info) ? info : Unknown;
    }
}
