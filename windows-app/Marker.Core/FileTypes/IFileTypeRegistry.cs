using Marker.Core.Models;

namespace Marker.Core.FileTypes;

/// <summary>Maps file extensions to highlighting and supported view modes.</summary>
public interface IFileTypeRegistry
{
    /// <summary>Resolves the <see cref="FileTypeInfo"/> for a given file path.</summary>
    FileTypeInfo Resolve(string path);
}
