using System.Text.Json;
using System.Text.Json.Nodes;
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

    /* 数值字段名到 RawExpressions 键名的映射，用于保存时将原始表达式写回 JSON */
    private static readonly Dictionary<string, string> NumericFieldToRawKey = new()
    {
        { "minWorkSeconds", "minWorkSeconds" },
        { "maxWorkSeconds", "maxWorkSeconds" },
        { "defaultRestSeconds", "defaultRestSeconds" },
        { "restOvertimeSeconds", "restOvertimeSeconds" },
        { "minEffectiveRestSeconds", "minEffectiveRestSeconds" },
        { "awayThresholdSeconds", "awayThresholdSeconds" },
        { "autoRestAfterIdleSeconds", "autoRestAfterIdleSeconds" },
        { "idleToWorkDetectSeconds", "idleToWorkDetectSeconds" },
        { "restToWorkDetectSeconds", "restToWorkDetectSeconds" },
        { "smartDetectGapSeconds", "smartDetectGapSeconds" },
        { "autoTransitionCountdownSeconds", "autoTransitionCountdownSeconds" },
        { "cornerHoverSeconds", "cornerHoverSeconds" },
        { "glowMaxThicknessPixels", "glowMaxThicknessPixels" },
        { "overlayOpacity", "overlayOpacity" },
        { "reminderVolumePercent", "reminderVolumePercent" },
        { "reTopmostIntervalSeconds", "reTopmostIntervalSeconds" }
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

            /* 从 JSON 中提取字符串类型的原始表达式，保存到 RawExpressions，
               并将字符串替换为计算后的数值以便反序列化成功 */
            var rawExprs = new Dictionary<string, string>();
            var node = JsonNode.Parse(json);
            if (node is JsonObject obj)
            {
                foreach (var mapping in NumericFieldToRawKey)
                {
                    if (obj.TryGetPropertyValue(mapping.Key, out var valueNode) && valueNode is JsonValue jv)
                    {
                        /* 如果值是字符串类型，说明是用户写的算术表达式（如 "35*60"） */
                        if (jv.TryGetValue(out string? strVal) && !string.IsNullOrWhiteSpace(strVal))
                        {
                            if (!double.TryParse(strVal, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                            {
                                rawExprs[mapping.Value] = strVal;
                                /* 将表达式计算为数值后替换回 JSON，确保反序列化不会因类型不匹配而失败 */
                                if (ExpressionEvaluator.TryEvaluate(strVal, out var evalResult, out _))
                                {
                                    obj[mapping.Key] = evalResult;
                                }
                            }
                        }
                    }
                }
                json = obj.ToJsonString();
            }

            var settings = JsonSerializer.Deserialize<ElasticBreathSettings>(json, SerializerOptions) ?? new ElasticBreathSettings();
            settings.RawExpressions = rawExprs;
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

        /* 将原始表达式文本替换回 JSON 中的数值字段，方便用户阅读 */
        if (settings.RawExpressions.Count > 0)
        {
            var node = JsonNode.Parse(json);
            if (node is JsonObject obj)
            {
                foreach (var mapping in NumericFieldToRawKey)
                {
                    if (settings.RawExpressions.TryGetValue(mapping.Value, out var rawExpr) && obj.ContainsKey(mapping.Key))
                    {
                        /* 只有当原始表达式不是纯数字时才替换（纯数字无需替换） */
                        if (!double.TryParse(rawExpr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                        {
                            obj[mapping.Key] = rawExpr;
                        }
                    }
                }
                json = obj.ToJsonString(SerializerOptions);
            }
        }

        File.WriteAllText(_settingsPath, json);
    }
}
