using System.Text.Json.Serialization;

namespace Marker.Core.Models;

/// <summary>
/// A named set of folders shown together in the file tree, with its own open
/// tabs and scratchpad. Each workspace is persisted as one JSON file under
/// <c>%APPDATA%\Marker\workspaces\</c>. Room is left for more per-workspace
/// options later.
/// </summary>
public sealed class Workspace
{
    /// <summary>Display name; also the basis for the file name and scratchpad key.</summary>
    public string Name { get; set; } = "";

    /// <summary>Root directories shown in the file tree.</summary>
    public List<string> Folders { get; set; } = new();

    /// <summary>Files that were open in tabs when this workspace was last active.</summary>
    public List<string> OpenFiles { get; set; } = new();

    /// <summary>
    /// Absolute path of the JSON file this workspace was loaded from / last
    /// saved to. Not serialized; set by the store so a rename can delete the
    /// file under the old name.
    /// </summary>
    [JsonIgnore] public string? FilePath { get; set; }

    /// <summary>So the workspace shows its name when bound directly (e.g. the switcher).</summary>
    public override string ToString() => Name;
}
