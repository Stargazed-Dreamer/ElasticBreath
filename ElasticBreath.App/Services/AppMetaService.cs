using System.IO;
using System.Reflection;
using System.Text.Json;

namespace ElasticBreath.App.Services;

/// <summary>
/// 应用元数据读取服务：从应用基目录下的 <c>app.meta.json</c> 读取版本、作者、仓库链接等信息。
/// 该服务为纯逻辑实现，仅依赖 <see cref="System.IO"/> 与 <see cref="System.Text.Json"/>，
/// 不引入 WPF / WinForms / Win32 依赖，便于未来跨平台复用与单元测试。
/// </summary>
public sealed class AppMetaService
{
    /// <summary>
    /// 元数据文件名，位于 <see cref="AppContext.BaseDirectory"/> 下。
    /// </summary>
    public const string MetaFileName = "app.meta.json";

    private static readonly AppMeta Empty = new(
        Version: string.Empty,
        Authors: string.Empty,
        Copyright: string.Empty,
        License: string.Empty,
        Repository: string.Empty,
        ShortDescription: string.Empty);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _metaPath;

    /// <summary>
    /// 构造函数，使用默认元数据路径（<see cref="AppContext.BaseDirectory"/>/<see cref="MetaFileName"/>）。
    /// </summary>
    public AppMetaService()
        : this(Path.Combine(AppContext.BaseDirectory, MetaFileName))
    {
    }

    /// <summary>
    /// 构造函数，允许指定元数据文件路径。用于测试或自定义部署场景。
    /// </summary>
    /// <param name="metaPath">元数据文件绝对路径。</param>
    public AppMetaService(string metaPath)
    {
        _metaPath = metaPath;
    }

    /// <summary>
    /// 读取并返回应用元数据。文件缺失、解析失败或字段为 null 时，对应字段返回空字符串而非抛异常。
    /// 在 app.meta.json 读取失败或字段为空时，<see cref="AppMeta.Version"/>、<see cref="AppMeta.Copyright"/>、
    /// <see cref="AppMeta.ShortDescription"/> 三个字段会回退到程序集属性（csproj 中的
    /// &lt;InformationalVersion&gt;/&lt;Copyright&gt;/&lt;Description&gt;），保证即使元数据文件被破坏
    /// 也能在关于界面显示版本号而非“版本未知”。authors/repository/license 无对应程序集属性，
    /// 缺失时由 UI 折叠对应行。
    /// </summary>
    /// <returns>不可变的 <see cref="AppMeta"/> 实例；文件不可用时回退到程序集属性。</returns>
    public AppMeta Load()
    {
        var meta = LoadFromFile();
        return ApplyAssemblyFallback(meta);
    }

    private AppMeta LoadFromFile()
    {
        if (!File.Exists(_metaPath))
        {
            return Empty;
        }

        try
        {
            var json = File.ReadAllText(_metaPath);
            // 文件存在但为空（0 字节）时直接返回 Empty，避免 JsonSerializer 抛异常。
            if (string.IsNullOrWhiteSpace(json))
            {
                return Empty;
            }

            var dto = JsonSerializer.Deserialize<MetaDto>(json, JsonOptions);
            if (dto is null)
            {
                return Empty;
            }

            return new AppMeta(
                Version: dto.Version ?? string.Empty,
                Authors: dto.Authors ?? string.Empty,
                Copyright: dto.Copyright ?? string.Empty,
                License: dto.License ?? string.Empty,
                Repository: dto.Repository ?? string.Empty,
                ShortDescription: dto.ShortDescription ?? string.Empty);
        }
        catch
        {
            // 元数据读取失败不应导致关于界面崩溃；返回空值实例由程序集回退兜底。
            return Empty;
        }
    }

    /// <summary>
    /// 对 version/copyright/shortDescription 三个字段做程序集属性回退。
    /// 仅当 json 字段为空时才查询对应程序集属性，避免无谓反射。
    /// </summary>
    private static AppMeta ApplyAssemblyFallback(AppMeta meta)
    {
        if (!string.IsNullOrEmpty(meta.Version)
            && !string.IsNullOrEmpty(meta.Copyright)
            && !string.IsNullOrEmpty(meta.ShortDescription))
        {
            return meta;
        }

        var asm = typeof(AppMetaService).Assembly;
        var version = meta.Version;
        var copyright = meta.Copyright;
        var description = meta.ShortDescription;

        if (string.IsNullOrEmpty(version))
        {
            var attr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attr is not null && !string.IsNullOrWhiteSpace(attr.InformationalVersion))
            {
                version = attr.InformationalVersion.Trim();
            }
        }

        if (string.IsNullOrEmpty(copyright))
        {
            var attr = asm.GetCustomAttribute<AssemblyCopyrightAttribute>();
            if (attr is not null && !string.IsNullOrWhiteSpace(attr.Copyright))
            {
                copyright = attr.Copyright;
            }
        }

        if (string.IsNullOrEmpty(description))
        {
            var attr = asm.GetCustomAttribute<AssemblyDescriptionAttribute>();
            if (attr is not null && !string.IsNullOrWhiteSpace(attr.Description))
            {
                description = attr.Description;
            }
        }

        return meta with
        {
            Version = version,
            Copyright = copyright,
            ShortDescription = description
        };
    }

    /// <summary>
    /// 崩溃日志目录，与 <see cref="CrashLogger"/> 写入路径保持一致。
    /// 暴露为静态属性供关于界面“打开崩溃日志文件夹”按钮复用，避免路径字符串散落多处。
    /// </summary>
    public static string CrashLogDirectory
        => Path.Combine(Path.GetTempPath(), "ElasticBreath", "crash");

    private sealed class MetaDto
    {
        public string? Version { get; set; }
        public string? Authors { get; set; }
        public string? Copyright { get; set; }
        public string? License { get; set; }
        public string? Repository { get; set; }
        public string? ShortDescription { get; set; }
    }
}

/// <summary>
/// 不可变的应用元数据记录。
/// </summary>
public sealed record AppMeta(
    string Version,
    string Authors,
    string Copyright,
    string License,
    string Repository,
    string ShortDescription);
