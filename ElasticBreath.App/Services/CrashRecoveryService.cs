using System.IO;

namespace ElasticBreath.App.Services;

/// <summary>
/// 崩溃日志条目，对应 %TEMP%\ElasticBreath\crash\ 下的一份日志文件。
/// </summary>
public sealed class CrashLogEntry
{
    /// <summary>日志文件完整路径</summary>
    public string FilePath { get; init; }
    /// <summary>从文件名解析出的崩溃时间</summary>
    public DateTime Timestamp { get; init; }
    /// <summary>来源标识（DispatcherUnhandledException 等）</summary>
    public string Source { get; init; }
    /// <summary>异常消息摘要</summary>
    public string Message { get; init; }
    /// <summary>完整日志文本</summary>
    public string FullContent { get; init; }

    public CrashLogEntry(string filePath, DateTime timestamp, string source, string message, string fullContent)
    {
        FilePath = filePath;
        Timestamp = timestamp;
        Source = source;
        Message = message;
        FullContent = fullContent;
    }
}

/// <summary>
/// 崩溃恢复服务。
/// 在应用启动时扫描 <see cref="CrashLogger"/> 写入的崩溃日志目录，
/// 列出上次（及更早）未处理的崩溃记录，供启动提示 UI 展示。
/// 设计参考：design.md §7（"自身崩溃时在临时目录写入日志，下次启动提示"）。
/// </summary>
public static class CrashRecoveryService
{
    private static readonly string CrashDir = Path.Combine(Path.GetTempPath(), "ElasticBreath", "crash");

    /// <summary>崩溃日志目录是否存在</summary>
    public static bool HasCrashDir => Directory.Exists(CrashDir);

    /// <summary>
    /// 列出所有未处理的崩溃日志，按时间升序排列（最新的在最后）。
    /// 读取失败或无日志时返回空列表。
    /// </summary>
    public static IReadOnlyList<CrashLogEntry> ListPendingCrashes()
    {
        var result = new List<CrashLogEntry>();
        if (!HasCrashDir)
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(CrashDir, "crash-*.log"))
        {
            try
            {
                var content = File.ReadAllText(file);
                var entry = ParseEntry(file, content);
                result.Add(entry);
            }
            catch
            {
                // 单个日志读取失败不影响其它日志的展示
            }
        }

        result.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        return result;
    }

    /// <summary>删除指定崩溃日志文件。</summary>
    public static void Delete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // 忽略删除失败
        }
    }

    /// <summary>清空所有崩溃日志文件，并尝试删除目录。</summary>
    public static void DeleteAll()
    {
        if (!HasCrashDir)
        {
            return;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(CrashDir, "*.log"))
            {
                try { File.Delete(file); }
                catch { /* 忽略单个文件删除失败 */ }
            }
            if (!Directory.EnumerateFileSystemEntries(CrashDir).Any())
            {
                Directory.Delete(CrashDir);
            }
        }
        catch
        {
            // 忽略
        }
    }

    private static CrashLogEntry ParseEntry(string filePath, string content)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        // 文件名格式：crash-YYYYMMDD-HHmmss-fff
        var timestamp = TryParseTimestampFromName(name) ?? DateTime.MinValue;

        var source = ExtractField(content, "source=");
        var message = ExtractField(content, "message=");
        return new CrashLogEntry(filePath, timestamp, source, message, content);
    }

    private static DateTime? TryParseTimestampFromName(string name)
    {
        // 期望形如 "crash-20260806-143005-123"
        const string prefix = "crash-";
        if (!name.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }
        var rest = name[prefix.Length..]; // 20260806-143005-123
        if (rest.Length < 17)
        {
            return null;
        }
        // 20260806-143005-123
        if (DateTime.TryParseExact(
                rest,
                "yyyyMMdd-HHmmss-fff",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeLocal,
                out var dt))
        {
            return dt;
        }
        return null;
    }

    private static string ExtractField(string content, string fieldName)
    {
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith(fieldName, StringComparison.Ordinal))
            {
                return trimmed[fieldName.Length..];
            }
        }
        return string.Empty;
    }
}
