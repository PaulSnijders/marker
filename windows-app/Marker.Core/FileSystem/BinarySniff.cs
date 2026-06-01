using System.IO;

namespace Marker.Core.FileSystem;

/// <summary>
/// Cheap "is this a text file?" check shared by the file repository and the
/// workspace searcher. A NUL byte in the first few KB is a strong binary
/// signal — UTF-8 text never contains one. We bail on the first NUL rather
/// than reading the whole file so a 4 GB video is rejected in microseconds.
/// </summary>
public static class BinarySniff
{
    /// <summary>Bytes to scan when guessing binary vs. text.</summary>
    public const int SniffLength = 8192;

    /// <summary>True if the buffer's leading bytes contain a NUL.</summary>
    public static bool LooksBinary(byte[] bytes)
    {
        int limit = Math.Min(bytes.Length, SniffLength);
        for (int i = 0; i < limit; i++)
        {
            if (bytes[i] == 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// True if the file's first <see cref="SniffLength"/> bytes look binary.
    /// Missing or unreadable files are reported as binary so callers skip them.
    /// </summary>
    public static bool LooksBinary(string path)
    {
        try
        {
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Span<byte> buffer = stackalloc byte[SniffLength];
            int read = stream.Read(buffer);
            for (int i = 0; i < read; i++)
            {
                if (buffer[i] == 0)
                    return true;
            }
            return false;
        }
        catch
        {
            return true;
        }
    }
}
