namespace Marker.Core.FileSystem;

/// <summary>
/// All file-system access goes through this seam. v1 ships
/// <see cref="LocalFileRepository"/>; a future RemoteFileRepository can slot
/// in here so sync is added without touching the UI layer.
/// </summary>
public interface IFileRepository
{
    bool DirectoryExists(string path);
    bool FileExists(string path);

    /// <summary>Lists immediate children of a directory (non-recursive).</summary>
    IEnumerable<FileSystemEntry> List(string directoryPath);

    /// <summary>Reads a file, detecting encoding, BOM, line endings and binary content.</summary>
    TextFileContent ReadText(string path);

    /// <summary>Writes text back, preserving the encoding and line endings of <paramref name="content"/>.</summary>
    void WriteText(string path, TextFileContent content);

    void CreateFile(string path);
    void CreateDirectory(string path);

    /// <summary>Renames a file or directory in place; returns the new full path.</summary>
    string Rename(string path, string newName);

    /// <summary>Deletes a file or directory to the Windows recycle bin.</summary>
    void DeleteToRecycleBin(string path);
}
