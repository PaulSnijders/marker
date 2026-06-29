using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Marker.Core.Models;

namespace Marker.Core.Settings;

/// <summary>
/// Stores each workspace as its own JSON file in
/// <c>%APPDATA%\Marker\workspaces\</c>. The file name is derived from the
/// workspace name; the authoritative name lives inside the file, so a backup
/// or hand edit of a single workspace is straightforward.
/// </summary>
public sealed class JsonWorkspaceStore : IWorkspaceStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };

    public string WorkspacesDirectory { get; }

    public JsonWorkspaceStore(string workspacesDirectory)
        => WorkspacesDirectory = workspacesDirectory;

    public IReadOnlyList<Workspace> LoadAll()
    {
        var result = new List<Workspace>();
        if (!Directory.Exists(WorkspacesDirectory))
            return result;

        foreach (string file in Directory.GetFiles(WorkspacesDirectory, "*.json"))
        {
            try
            {
                var ws = JsonSerializer.Deserialize<Workspace>(File.ReadAllText(file), Options);
                if (ws is not null && !string.IsNullOrWhiteSpace(ws.Name))
                {
                    ws.FilePath = file;
                    // System.Text.Json cannot round-trip a custom IEqualityComparer
                    // on a Dictionary, so rebuild it case-insensitively — Windows
                    // paths compare that way and the in-memory lookup relies on it.
                    ws.FilePositions = new Dictionary<string, FilePositionState>(
                        ws.FilePositions, StringComparer.OrdinalIgnoreCase);
                    result.Add(ws);
                }
            }
            catch
            {
                // A corrupt workspace file is skipped — it never blocks the others.
            }
        }
        return result;
    }

    public void Save(Workspace workspace)
    {
        Directory.CreateDirectory(WorkspacesDirectory);
        string target = Path.Combine(
            WorkspacesDirectory, SanitizeFileName(workspace.Name) + ".json");

        // A rename changes the derived file name — drop the file under the old
        // name so a stale duplicate is not left behind.
        if (workspace.FilePath is { Length: > 0 } old &&
            !string.Equals(old, target, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(old))
        {
            try { File.Delete(old); }
            catch { /* best effort — a stale file is harmless next load */ }
        }

        string json = JsonSerializer.Serialize(workspace, Options);

        // Atomic write: temp file then swap, so a crash mid-write can never
        // leave a half-written, corrupt workspace file behind.
        string tempPath = target + ".tmp";
        File.WriteAllText(tempPath, json);
        if (File.Exists(target))
            File.Replace(tempPath, target, destinationBackupFileName: null);
        else
            File.Move(tempPath, target);

        workspace.FilePath = target;
    }

    public void Delete(Workspace workspace)
    {
        if (workspace.FilePath is { Length: > 0 } path && File.Exists(path))
            File.Delete(path);
    }

    /// <summary>Turns a workspace name into a safe file name.</summary>
    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Trim();
        return name.Length == 0 ? "workspace" : name;
    }
}
