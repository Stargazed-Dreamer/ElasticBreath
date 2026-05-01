using System.Text;
using System.IO;

namespace ElasticBreath.App.Services;

public static class CrashLogger
{
    public static void Write(string source, Exception exception)
    {
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "ElasticBreath", "crash");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");

            var sb = new StringBuilder();
            sb.AppendLine($"time={DateTime.Now:O}");
            sb.AppendLine($"source={source}");
            sb.AppendLine($"os={Environment.OSVersion}");
            sb.AppendLine($"framework={Environment.Version}");
            sb.AppendLine($"message={exception.Message}");
            sb.AppendLine("stack:");
            sb.AppendLine(exception.ToString());

            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            // ignore secondary logging failure
        }
    }
}
