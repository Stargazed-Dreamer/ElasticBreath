using System.Text.Json;
using System.Text.Json.Nodes;
using ElasticBreath.App.Domain;
using System.IO;

namespace ElasticBreath.App.Services;

/// <summary>
/// 管理应用程序设置的持久化存储，支持将数值字段的算术表达式保存在配置文件中。
/// 该类负责从 JSON 文件加载设置，并将设置保存回文件，同时处理算术表达式的转换。
/// </summary>
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

    // 配置文件 settings.json 的完整路径
    private readonly string _settingsPath;

    /// <summary>
    /// 初始化 SettingsStore 实例。
    /// 在应用程序的基础目录下创建 config 文件夹，并设置配置文件路径。
    /// </summary>
    public SettingsStore()
    {
        // 获取应用程序可执行文件的基础目录
        var baseDir = AppContext.BaseDirectory;
        // 构造配置文件夹的完整路径
        var configDir = Path.Combine(baseDir, "config");
        // 确保配置文件夹存在，如果不存在则创建
        Directory.CreateDirectory(configDir);
        // 设置配置文件的完整路径
        _settingsPath = Path.Combine(configDir, "settings.json");
    }

    /// <summary>
    /// 从配置文件加载设置。
    /// 如果文件不存在，则创建并保存默认设置后返回默认值。
    /// 加载过程中会处理 JSON 中的算术表达式，将其计算为数值以供反序列化，同时保留原始表达式文本。
    /// </summary>
    /// <returns>反序列化后的 ElasticBreathSettings 对象，或在出现任何异常时返回默认的、经过校验的设置。</returns>
    public ElasticBreathSettings Load()
    {
        try
        {
            // 检查配置文件是否存在
// 检查设置文件路径是否存在
            if (!File.Exists(_settingsPath))
            {
                // 文件不存在，创建默认设置并保存
                var defaults = new ElasticBreathSettings().Sanitize(); // 创建默认设置并进行清理
                Save(defaults); // 保存设置到文件
                return defaults; // 返回创建的默认设置
            }

            // 读取 JSON 文件内容
            var json = File.ReadAllText(_settingsPath);

            /* 从 JSON 中提取字符串类型的原始表达式，保存到 RawExpressions，
               并将字符串替换为计算后的数值以便反序列化成功 */
            // 用于存储从 JSON 中解析出的原始表达式文本（键为字段名，值为表达式字符串）
            var rawExprs = new Dictionary<string, string>();
            // 将 JSON 字符串解析为可操作的 JsonNode 对象
            var node = JsonNode.Parse(json);
            // 确保解析结果是一个 JSON 对象
            if (node is JsonObject obj)
            {
                // 遍历所有预定义的数值字段映射
                foreach (var mapping in NumericFieldToRawKey)
                {
                    // 尝试从 JSON 对象中获取对应字段名的值节点
                    if (obj.TryGetPropertyValue(mapping.Key, out var valueNode) && valueNode is JsonValue jv)
                    {
                        /* 如果值是字符串类型，说明是用户写的算术表达式（如 "35*60"） */
                        // 尝试将 JsonValue 转换为字符串
                        if (jv.TryGetValue(out string? strVal) && !string.IsNullOrWhiteSpace(strVal))
                        {
                            // 尝试将字符串解析为 double，如果失败，说明它是一个算术表达式
                            if (!double.TryParse(strVal, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                            {
                                // 将原始表达式文本存入字典，键为对应的 RawExpressions 字段名
                                rawExprs[mapping.Value] = strVal;
                                /* 将表达式计算为数值后替换回 JSON，确保反序列化不会因类型不匹配而失败 */
                                // 尝试计算表达式的值
                                if (ExpressionEvaluator.TryEvaluate(strVal, out var evalResult, out _))
                                {
                                    // 用计算结果替换 JSON 中的字符串值
                                    obj[mapping.Key] = evalResult;
                                }
                            }
                        }
                    }
                }
                // 将修改后的 JsonObject 重新序列化为 JSON 字符串，以便后续反序列化
                json = obj.ToJsonString();
            }

            // 使用配置的选项反序列化 JSON 字符串为设置对象，若失败则创建新实例
            var settings = JsonSerializer.Deserialize<ElasticBreathSettings>(json, SerializerOptions) ?? new ElasticBreathSettings();
            // 将解析到的原始表达式字典赋值给设置对象
            settings.RawExpressions = rawExprs;
            // 对设置进行校验和清理后返回
            return settings.Sanitize();
        }
        catch
        {
            // 发生任何异常时，返回默认的、经过校验的设置，确保程序能继续运行
// 创建一个新的 ElasticBreathSettings 实例并执行清理校验
            return new ElasticBreathSettings().Sanitize();
        }
    }

    /// <summary>
    /// 将设置保存到配置文件。
    /// 在序列化前会先对设置进行校验和清理。保存时，会将设置对象中保留的原始表达式文本替换回 JSON 中的对应数值字段。
    /// </summary>
    /// <param name="settings">要保存的 ElasticBreathSettings 对象。</param>
    public void Save(ElasticBreathSettings settings)
    {
        // 对设置进行校验和清理，确保数据有效性
        var normalized = settings.Sanitize();
        // 将清理后的设置对象序列化为 JSON 字符串
        var json = JsonSerializer.Serialize(normalized, SerializerOptions);

        /* 将原始表达式文本替换回 JSON 中的数值字段，方便用户阅读 */
        // 检查设置对象中是否保留了原始的算术表达式
        if (settings.RawExpressions.Count > 0)
        {
            // 将 JSON 字符串解析为可操作的 JsonNode 对象
            var node = JsonNode.Parse(json);
            // 确保解析结果是一个 JSON 对象
            if (node is JsonObject obj)
            {
                // 遍历所有预定义的数值字段映射
                foreach (var mapping in NumericFieldToRawKey)
                {
                    // 尝试从原始表达式字典中获取对应字段的原始表达式文本，并检查 JSON 对象中是否存在该字段
                    if (settings.RawExpressions.TryGetValue(mapping.Value, out var rawExpr) && obj.ContainsKey(mapping.Key))
                    {
                        /* 只有当原始表达式不是纯数字时才替换（纯数字无需替换） */
                        // 尝试将原始表达式解析为 double，如果成功，说明它是纯数字，无需替换回字符串
                        if (!double.TryParse(rawExpr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                        {
                            // 将 JSON 对象中对应的数值字段值替换为原始的算术表达式字符串
                            obj[mapping.Key] = rawExpr;
                        }
                    }
                }
                // 将修改后的 JsonObject 重新序列化为格式化的 JSON 字符串
                json = obj.ToJsonString(SerializerOptions);
            }
        }

        // 将最终的 JSON 字符串写入配置文件
        File.WriteAllText(_settingsPath, json);
    }
}
