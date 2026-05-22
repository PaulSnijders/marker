using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Marker.Core.Models;

namespace Marker.Core.Settings;

/// <summary>
/// Stores settings as a single JSON file in <c>%APPDATA%\Marker\settings.json</c>.
/// Editing the file by hand is fully supported.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        // Window bounds default to NaN ("not positioned yet"); allow it through
        // instead of throwing, so settings can be saved before a real layout.
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() }
    };

    public string SettingsFilePath { get; }

    public JsonSettingsStore(string? overridePath = null)
    {
        if (overridePath is not null)
        {
            SettingsFilePath = overridePath;
            return;
        }

        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Marker");
        SettingsFilePath = Path.Combine(dir, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return new AppSettings();

            string json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch
        {
            // A corrupt file must never block startup — fall back to defaults.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        string dir = Path.GetDirectoryName(SettingsFilePath)!;
        Directory.CreateDirectory(dir);
        string json = JsonSerializer.Serialize(settings, Options);

        // Write to a temp file first, then swap it in. A crash or kill in the
        // middle of a write can then never leave a half-written, corrupt file
        // behind — the real settings file is only ever replaced atomically.
        string tempPath = SettingsFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        if (File.Exists(SettingsFilePath))
            File.Replace(tempPath, SettingsFilePath, destinationBackupFileName: null);
        else
            File.Move(tempPath, SettingsFilePath);
    }
}
