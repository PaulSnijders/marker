using System.IO;
using Marker.Core.FileSystem;
using Marker.Core.FileTypes;
using Marker.Core.Markdown;
using Marker.Core.Models;
using Marker.Core.Settings;

namespace Marker.App.Services;

/// <summary>
/// Hand-rolled service locator. The requirements call for no DI container in
/// v1; this static class is the single composition point for Core services.
/// </summary>
public static class AppServices
{
    public static ISettingsStore SettingsStore { get; private set; } = null!;
    public static AppSettings Settings { get; private set; } = null!;
    public static IWorkspaceStore Workspaces { get; private set; } = null!;
    public static IFileRepository Files { get; private set; } = null!;
    public static IMarkdownRenderer Markdown { get; private set; } = null!;
    public static IFileTypeRegistry FileTypes { get; private set; } = null!;

    /// <summary>Folder holding the bundled web assets (TOAST UI, highlight.js).</summary>
    public static string WebRoot { get; private set; } = null!;

    public static void Initialize()
    {
        // MARKER_SETTINGS_DIR redirects all config away from %APPDATA%
        // (used by automated tests so they never disturb real user config;
        // also the seam for a future portable mode).
        string? overrideDir = Environment.GetEnvironmentVariable("MARKER_SETTINGS_DIR");
        string baseDir = overrideDir is { Length: > 0 }
            ? overrideDir
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Marker");

        SettingsStore = new JsonSettingsStore(Path.Combine(baseDir, "settings.json"));
        Workspaces = new JsonWorkspaceStore(Path.Combine(baseDir, "workspaces"));
        Settings = SettingsStore.Load();
        Files = new LocalFileRepository();
        Markdown = new MarkdigRenderer();
        FileTypes = new FileTypeRegistry();
        WebRoot = Path.Combine(AppContext.BaseDirectory, "web");
    }

    public static void SaveSettings() => SettingsStore.Save(Settings);

    public static void SaveWorkspace(Workspace workspace) => Workspaces.Save(workspace);
}
