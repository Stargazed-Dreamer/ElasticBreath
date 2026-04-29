using System.Text.Json;
using System.IO;

namespace ElasticBreath.App.Services;

public sealed class LocalizationService
{
    private readonly Dictionary<string, string> _messages = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _resourceDir;

    public LocalizationService()
    {
        _resourceDir = Path.Combine(AppContext.BaseDirectory, "i18n");
    }

    public string CurrentLanguage { get; private set; } = "zh-CN";

    public IReadOnlyList<string> AvailableLanguages
        => Directory.Exists(_resourceDir)
            ? Directory.GetFiles(_resourceDir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToArray()
            : Array.Empty<string>();

    public void Load(string language)
    {
        var resolved = string.IsNullOrWhiteSpace(language) ? "zh-CN" : language;
        var path = Path.Combine(_resourceDir, $"{resolved}.json");
        if (!File.Exists(path))
        {
            path = Path.Combine(_resourceDir, "zh-CN.json");
            resolved = "zh-CN";
        }

        if (!File.Exists(path))
        {
            _messages.Clear();
            CurrentLanguage = resolved;
            return;
        }

        var json = File.ReadAllText(path);
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? new Dictionary<string, string>();

        _messages.Clear();
        foreach (var pair in dict)
        {
            _messages[pair.Key] = pair.Value;
        }

        CurrentLanguage = resolved;
    }

    public string T(string key)
        => _messages.TryGetValue(key, out var value) ? value : key;

    public string Tf(string key, params object[] args)
        => string.Format(T(key), args);
}
