using System.Text;
using System.IO;

namespace ElasticBreath.App.Services;

/// <summary>
/// 崩溃日志记录器，用于将异常信息记录到临时文件中，便于调试和错误追踪。
/// </summary>
public static class CrashLogger
{
/// <summary>
/// 将崩溃异常信息写入日志文件。
/// </summary>
    public static void Write(string source, Exception exception)
    {
        try
        {
            // 构建崩溃日志目录路径，结合临时目录、应用名称和子目录
            var dir = Path.Combine(Path.GetTempPath(), "ElasticBreath", "crash");
            // 创建目录，如果目录不存在则自动创建
            Directory.CreateDirectory(dir);
            // 生成带时间戳的日志文件名，格式为"crash-年月日-时分秒-毫秒.log"
            var file = Path.Combine(dir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");

            // 使用StringBuilder高效构建日志内容
            var sb = new StringBuilder();
            sb.AppendLine($"time={DateTime.Now:O}"); // 添加ISO 8601格式的时间戳
            sb.AppendLine($"source={source}"); // 添加错误来源标识
            sb.AppendLine($"os={Environment.OSVersion}"); // 添加操作系统版本信息
            sb.AppendLine($"framework={Environment.Version}"); // 添加.NET框架版本
            sb.AppendLine($"message={exception.Message}"); // 添加异常消息
            sb.AppendLine("stack:"); // 添加堆栈跟踪标题
            sb.AppendLine(exception.ToString()); // 添加完整异常堆栈信息

            // 将构建好的日志内容写入文件，使用UTF-8编码确保字符兼容性
            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            // 忽略次要的日志记录失败，避免日志记录过程本身引发异常
        }
    }
}
