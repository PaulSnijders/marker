using Marker.Core.Models;

namespace Marker.Core.Settings;

/// <summary>Loads and saves <see cref="AppSettings"/>. v1 uses a JSON file.</summary>
public interface ISettingsStore
{
    /// <summary>Loads settings, returning defaults if the file is missing or invalid.</summary>
    AppSettings Load();

    /// <summary>Persists settings to disk.</summary>
    void Save(AppSettings settings);

    /// <summary>Absolute path of the backing settings file.</summary>
    string SettingsFilePath { get; }
}
