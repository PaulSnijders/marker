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
    /// Path of the tab that was selected when this workspace was last active,
    /// or <c>null</c> when nothing eligible was selected. Re-selected on the
    /// next visit so the user lands on the file they were last working in.
    /// </summary>
    public string? LastActiveFile { get; set; }

    /// <summary>
    /// Per-file caret and scroll snapshot keyed by absolute path, so reopening
    /// the workspace puts each tab back on the line it was scrolled to instead
    /// of resetting to the top. Path keys are compared case-insensitively
    /// (rebuilt with <see cref="StringComparer.OrdinalIgnoreCase"/> after load).
    /// </summary>
    public Dictionary<string, FilePositionState> FilePositions { get; set; } = new();

    /// <summary>
    /// Absolute paths of every directory that was expanded in the tree when
    /// this workspace was last active, so the tree restores to the same
    /// open/closed shape on the next visit. Empty for a brand-new workspace —
    /// in which case roots default to expanded and children to collapsed.
    /// </summary>
    public List<string> ExpandedFolders { get; set; } = new();

    /// <summary>
    /// Absolute path of the JSON file this workspace was loaded from / last
    /// saved to. Not serialized; set by the store so a rename can delete the
    /// file under the old name.
    /// </summary>
    [JsonIgnore] public string? FilePath { get; set; }

    /// <summary>So the workspace shows its name when bound directly (e.g. the switcher).</summary>
    public override string ToString() => Name;
}
