namespace Marker.Core.Models;

/// <summary>
/// All persisted application state. Serialized as a single JSON file in
/// <c>%APPDATA%\Marker\settings.json</c>. Every property has a sensible
/// default so a missing or partial file still yields a usable app.
/// </summary>
public sealed class AppSettings
{
    // --- Workspaces ---------------------------------------------------

    /// <summary>Name of the workspace shown on startup. Workspaces themselves
    /// live in their own files under <c>%APPDATA%\Marker\workspaces\</c>.</summary>
    public string? ActiveWorkspace { get; set; }

    /// <summary>Legacy — migration only. The old flat folder list; folded into
    /// a workspace on first run with the new model, then left empty.</summary>
    public List<string> WorkspaceFolders { get; set; } = new();

    // --- Markdown -----------------------------------------------------
    public MarkdownMode DefaultMarkdownMode { get; set; } = MarkdownMode.Source;

    /// <summary>When true, each file remembers its last mode in <see cref="FileModes"/>.</summary>
    public bool RememberModePerFile { get; set; } = true;
    public Dictionary<string, MarkdownMode> FileModes { get; set; } = new();

    // --- Editor -------------------------------------------------------
    public bool AutoSave { get; set; } = true;
    public bool WordWrap { get; set; } = false;
    public string FontFamily { get; set; } = "Cascadia Mono";
    public double FontSize { get; set; } = 15;

    /// <summary>"light" or "dark".</summary>
    public string Theme { get; set; } = "light";

    // --- File tree ----------------------------------------------------
    public List<string> IgnorePatterns { get; set; } = new() { ".git", "node_modules", "bin", "obj" };

    // --- Recent / session --------------------------------------------
    public List<string> RecentFiles { get; set; } = new();

    /// <summary>Legacy — migration only. Open files are now stored per
    /// workspace; this is folded into a workspace on first run, then empty.</summary>
    public List<string> OpenFiles { get; set; } = new();

    // --- Window / layout ---------------------------------------------
    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 720;
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public bool WindowMaximized { get; set; }
    public double TreePaneWidth { get; set; } = 260;
}
