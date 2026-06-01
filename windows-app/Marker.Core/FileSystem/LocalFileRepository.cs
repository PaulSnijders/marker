using System.IO;
using System.Text;
using Microsoft.VisualBasic.FileIO;
// Alias avoids a clash between this namespace (…FileSystem) and the VB class.
using VbFileSystem = Microsoft.VisualBasic.FileIO.FileSystem;

namespace Marker.Core.FileSystem;

/// <summary>
/// <see cref="IFileRepository"/> backed by the local Windows file system.
/// </summary>
public sealed class LocalFileRepository : IFileRepository
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public IEnumerable<FileSystemEntry> List(string directoryPath)
    {
        // Directories first, then files; both alphabetical, case-insensitive.
        var dirs = Directory.EnumerateDirectories(directoryPath)
            .Select(p => new FileSystemEntry(p, Path.GetFileName(p), true))
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase);

        var files = Directory.EnumerateFiles(directoryPath)
            .Select(p => new FileSystemEntry(p, Path.GetFileName(p), false))
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase);

        return dirs.Concat(files).ToList();
    }

    public TextFileContent ReadText(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);

        if (BinarySniff.LooksBinary(bytes))
        {
            return new TextFileContent { IsBinary = true };
        }

        Encoding encoding = DetectEncoding(bytes, out bool hasBom);
        string text = encoding.GetString(StripBom(bytes, hasBom, encoding));
        string lineEnding = DetectLineEnding(text);

        return new TextFileContent
        {
            Text = text,
            Encoding = encoding,
            HasBom = hasBom,
            LineEnding = lineEnding,
            IsBinary = false
        };
    }

    public void WriteText(string path, TextFileContent content)
    {
        // Normalize to the file's original line ending before writing.
        string normalized = NormalizeLineEndings(content.Text, content.LineEnding);

        Encoding encoding = content.HasBom
            ? content.Encoding
            : new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        byte[] payload = encoding.GetBytes(normalized);

        if (content.HasBom)
        {
            byte[] preamble = content.Encoding.GetPreamble();
            if (preamble.Length > 0)
            {
                byte[] combined = new byte[preamble.Length + payload.Length];
                Buffer.BlockCopy(preamble, 0, combined, 0, preamble.Length);
                Buffer.BlockCopy(payload, 0, combined, preamble.Length, payload.Length);
                payload = combined;
            }
        }

        File.WriteAllBytes(path, payload);
    }

    public void CreateFile(string path)
    {
        if (!File.Exists(path))
            File.WriteAllText(path, string.Empty, new UTF8Encoding(false));
    }

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public string Rename(string path, string newName)
    {
        string parent = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Cannot rename a root path.");
        string target = Path.Combine(parent, newName);

        if (Directory.Exists(path))
            Directory.Move(path, target);
        else
            File.Move(path, target);

        return target;
    }

    public void DeleteToRecycleBin(string path)
    {
        if (Directory.Exists(path))
            VbFileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        else if (File.Exists(path))
            VbFileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
    }

    // --- helpers ------------------------------------------------------

    private static Encoding DetectEncoding(byte[] bytes, out bool hasBom)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            hasBom = true;
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            hasBom = true;
            return Encoding.Unicode; // UTF-16 LE
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            hasBom = true;
            return Encoding.BigEndianUnicode; // UTF-16 BE
        }

        hasBom = false;
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }

    private static byte[] StripBom(byte[] bytes, bool hasBom, Encoding encoding)
    {
        if (!hasBom)
            return bytes;

        int skip = encoding.GetPreamble().Length;
        return bytes[skip..];
    }

    private static string DetectLineEnding(string text)
    {
        int crlf = 0, lf = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                if (i > 0 && text[i - 1] == '\r') crlf++;
                else lf++;
            }
        }
        return lf > crlf ? "\n" : "\r\n";
    }

    private static string NormalizeLineEndings(string text, string lineEnding)
    {
        // Collapse everything to \n first, then expand to the target ending.
        string lf = text.Replace("\r\n", "\n").Replace("\r", "\n");
        return lineEnding == "\n" ? lf : lf.Replace("\n", "\r\n");
    }
}
