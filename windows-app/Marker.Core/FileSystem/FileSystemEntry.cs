namespace Marker.Core.FileSystem;

/// <summary>A single file or directory entry returned when listing a folder.</summary>
/// <param name="Path">Absolute path.</param>
/// <param name="Name">Display name (file/folder name only).</param>
/// <param name="IsDirectory">True for a directory.</param>
public sealed record FileSystemEntry(string Path, string Name, bool IsDirectory);
