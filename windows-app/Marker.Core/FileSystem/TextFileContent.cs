using System.Text;

namespace Marker.Core.FileSystem;

/// <summary>
/// The decoded contents of a text file plus the metadata needed to write it
/// back unchanged: encoding, BOM presence and the dominant line ending.
/// </summary>
public sealed class TextFileContent
{
    /// <summary>Decoded text. Line endings are preserved as found on disk.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Encoding to use when writing back. Defaults to UTF-8.</summary>
    public Encoding Encoding { get; init; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>True when the file started with a byte-order mark.</summary>
    public bool HasBom { get; init; }

    /// <summary>Dominant line ending: "\r\n" (CRLF) or "\n" (LF).</summary>
    public string LineEnding { get; init; } = "\r\n";

    /// <summary>True when the file looks like binary and should not be edited.</summary>
    public bool IsBinary { get; init; }

    /// <summary>Friendly encoding label for the status bar.</summary>
    public string EncodingLabel => Encoding.WebName.ToUpperInvariant() + (HasBom ? " BOM" : string.Empty);

    /// <summary>Friendly line-ending label for the status bar.</summary>
    public string LineEndingLabel => LineEnding == "\n" ? "LF" : "CRLF";
}
