using System.Text.Json;
using System.IO;

namespace ElasticBreath.App.Services;

/// <summary>
/// 本地化服务类，用于管理多语言文本资源。
/// 支持加载不同语言的 JSON 格式本地化文件，并提供翻译方法。
/// </summary>
public sealed class LocalizationService
{
    // 存储当前语言消息的字典，键值对不区分大小写
    private readonly Dictionary<string, string> _messages = new(StringComparer.OrdinalIgnoreCase);
    // 本地化资源文件目录路径
    private readonly string _resourceDir;

    /// <summary>
    /// 构造函数，初始化本地化服务的资源目录。
    /// 资源目录默认为应用程序基目录下的 "i18n" 文件夹。
    /// </summary>
    public LocalizationService()
    {
        // 设置资源目录路径为应用程序基目录下的 "i18n" 文件夹
        _resourceDir = Path.Combine(AppContext.BaseDirectory, "i18n");
    }

    /// <summary>
    /// 获取或设置当前语言，默认为 "zh-CN"（简体中文）。
    /// </summary>
    public string CurrentLanguage { get; private set; } = "zh-CN";

    /// <summary>
    /// 获取可用的语言列表，基于资源目录中的 JSON 文件。
    /// 返回文件名（不含扩展名）的数组，按字母顺序排序。
    /// </summary>
    public IReadOnlyList<string> AvailableLanguages
        => Directory.Exists(_resourceDir)
            // 如果资源目录存在，则获取所有 .json 文件
            ? Directory.GetFiles(_resourceDir, "*.json")
                .Select(Path.GetFileNameWithoutExtension) // 提取文件名（不含扩展名）
                .Where(x => !string.IsNullOrWhiteSpace(x)) // 过滤空值
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase) // 不区分大小写排序
                .Cast<string>()
                .ToArray()
            : Array.Empty<string>(); // 如果目录不存在，返回空数组

    /// <summary>
    /// 加载指定语言的本地化资源。
    /// 如果语言为空或文件不存在，回退到默认语言 "zh-CN"。
    /// </summary>
    /// <param name="language">要加载的语言代码，例如 "zh-CN" 或 "en-US"。</param>
    public void Load(string language)
    {
        // 解析语言参数，如果为空则使用默认语言
        var resolved = string.IsNullOrWhiteSpace(language) ? "zh-CN" : language;
        // 构造资源文件路径
        var path = Path.Combine(_resourceDir, $"{resolved}.json");
        // 检查文件是否存在，如果不存在则回退到默认语言文件
        // 如果指定的文件路径不存在
        if (!File.Exists(path))
        {
            // 使用默认的中文资源文件路径
            path = Path.Combine(_resourceDir, "zh-CN.json");
            // 设置解析语言为中文
            resolved = "zh-CN";
        }

        // 如果默认语言文件也不存在，清空消息字典并设置当前语言
/// <summary>
/// 检查指定路径的文件是否存在，如果文件不存在，则清空消息列表并设置当前语言后返回。
/// </summary>
        // 检查语言文件是否存在
        if (!File.Exists(path))
        {
            // 文件不存在时，清空已加载的消息列表
            _messages.Clear();
            // 设置当前语言为解析后的语言代码
            CurrentLanguage = resolved;
            // 直接返回，不执行后续文件加载逻辑
            return;
        }

        // 读取 JSON 文件内容并反序列化为字典
        var json = File.ReadAllText(path);
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? new Dictionary<string, string>();

        // 清空现有消息并填充新数据
        _messages.Clear();
        /// <summary>
        /// 此方法用于遍历指定字典，并将每个键值对存储到内部的_messages集合中。
        /// </summary>
        foreach (var pair in dict) // 遍历字典中的每个键值对
        {
            _messages[pair.Key] = pair.Value; // 将当前键值对的值赋给_messages中对应的键
        }

        // 更新当前语言设置
        CurrentLanguage = resolved;
    }

    /// <summary>
    /// 根据键获取翻译文本。
    /// 如果键不存在，则返回键本身作为回退。
    /// </summary>
    /// <param name="message key">翻译键。</param>
    /// <returns>翻译后的文本，或键本身。</returns>
    public string T(string key)
        // 尝试从字典中获取值，如果失败则返回键
        => _messages.TryGetValue(key, out var value) ? value : key;

    /// <summary>
    /// 根据键获取翻译文本，并使用指定参数格式化字符串。
    /// 如果键不存在，则使用键本身进行格式化。
    /// </summary>
    /// <param name="key">翻译键。</param>
    /// <param name="args">用于格式化的参数。</param>
    /// <returns>格式化后的翻译文本。</returns>
    public string Tf(string key, params object[] args)
        // 先获取翻译文本，然后使用字符串格式化
        => string.Format(T(key), args);
}
