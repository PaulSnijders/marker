using Marker.Core.Models;

namespace Marker.Core.Settings;

/// <summary>
/// Loads and saves <see cref="Workspace"/> definitions — one JSON file each.
/// </summary>
public interface IWorkspaceStore
{
    /// <summary>Absolute path of the folder holding the workspace files.</summary>
    string WorkspacesDirectory { get; }

    /// <summary>Loads every workspace file in the directory; skips corrupt ones.</summary>
    IReadOnlyList<Workspace> LoadAll();

    /// <summary>Persists a workspace, deleting its old file if it was renamed.</summary>
    void Save(Workspace workspace);

    /// <summary>Deletes a workspace's backing file.</summary>
    void Delete(Workspace workspace);
}
