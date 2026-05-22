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
        [".md"]       = new("Markdown", "Markdown", IsMarkdown: true),
        [".markdown"] = new("Markdown", "Markdown", IsMarkdown: true),
        [".txt"]      = new("Plain Text", null, IsMarkdown: false),
        [".json"]     = new("JSON", "JSON", IsMarkdown: false),
        [".xml"]      = new("XML", "XML", IsMarkdown: false),
        [".html"]     = new("HTML", "HTML", IsMarkdown: false),
        [".htm"]      = new("HTML", "HTML", IsMarkdown: false),
        [".yaml"]     = new("YAML", "YAML", IsMarkdown: false),
        [".yml"]      = new("YAML", "YAML", IsMarkdown: false),
        [".csv"]      = new("CSV", null, IsMarkdown: false),
        [".log"]      = new("Log", null, IsMarkdown: false),
    };

    private static readonly FileTypeInfo Unknown = new("Text", null, IsMarkdown: false);

    public FileTypeInfo Resolve(string path)
    {
        string ext = Path.GetExtension(path);
        return Map.TryGetValue(ext, out FileTypeInfo? info) ? info : Unknown;
    }
}
