using System.Text.Json;
using System.Text.Json.Serialization;

namespace AfterPub.Core.Settings;

/// <summary>
/// Reads and writes <see cref="AppSettings"/> as a plain, human-readable JSON file
/// (CLAUDE.md section 8: stored in a plain file next to the executable, so the app
/// stays portable). No database, no hidden state.
/// </summary>
public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Full path to the settings file this store reads from and writes to.</summary>
    public string FilePath { get; }

    public AppSettingsStore(string filePath)
    {
        this.FilePath = filePath;
    }

    /// <summary>The default location: "afterpub.settings.json" next to the running executable.</summary>
    public static string GetDefaultFilePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "afterpub.settings.json");
    }

    /// <summary>
    /// Loads settings from <see cref="FilePath"/>. Returns defaults (never throws) if
    /// the file does not exist or cannot be parsed, matching CLAUDE.md's graceful-
    /// degradation rule for the app's own generated files.
    /// </summary>
    public AppSettings Load()
    {
        if (!File.Exists(this.FilePath))
        {
            return new AppSettings();
        }

        try
        {
            string json = File.ReadAllText(this.FilePath);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            return settings ?? new AppSettings();
        }
        catch (JsonException)
        {
            // Corrupt or hand-edited settings file: fall back to defaults rather than crash.
            return new AppSettings();
        }
    }

    /// <summary>Writes <paramref name="settings"/> to <see cref="FilePath"/>, creating its folder if needed.</summary>
    public void Save(AppSettings settings)
    {
        string? folder = Path.GetDirectoryName(this.FilePath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(this.FilePath, json);
    }
}
