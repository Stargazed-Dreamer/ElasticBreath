using System.Text.Json;
using ElasticBreath.App.Domain;
using System.IO;

namespace ElasticBreath.App.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _settingsPath;

    public SettingsStore()
    {
        var baseDir = AppContext.BaseDirectory;
        var configDir = Path.Combine(baseDir, "config");
        Directory.CreateDirectory(configDir);
        _settingsPath = Path.Combine(configDir, "settings.json");
    }

    public ElasticBreathSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                var defaults = new ElasticBreathSettings().Sanitize();
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<ElasticBreathSettings>(json, SerializerOptions) ?? new ElasticBreathSettings();
            return settings.Sanitize();
        }
        catch
        {
            return new ElasticBreathSettings().Sanitize();
        }
    }

    public void Save(ElasticBreathSettings settings)
    {
        var normalized = settings.Sanitize();
        var json = JsonSerializer.Serialize(normalized, SerializerOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
