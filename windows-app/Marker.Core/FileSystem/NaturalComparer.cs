using System.Runtime.InteropServices;

namespace Marker.Core.FileSystem;

/// <summary>
/// Compares file names the way Windows Explorer sorts them
/// (shlwapi StrCmpLogicalW).
/// </summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y) => StrCmpLogicalW(x ?? "", y ?? "");

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string psz1, string psz2);
}
