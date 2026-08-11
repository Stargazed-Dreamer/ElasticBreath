using System.IO;
using ElasticBreath.App.Services;
using Xunit;

namespace ElasticBreath.Tests;

public class AppMetaServiceTests
{
    [Fact]
    public void Load_ValidJson_ReturnsAllFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"eb-meta-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path,
                """
                {
                  "version": "1.2.3",
                  "authors": "SomeAuthor",
                  "copyright": "Copyright © 2026 SomeAuthor",
                  "license": "MIT License · Copyright © 2026 SomeAuthor",
                  "repository": "https://example.com/repo",
                  "shortDescription": "A test description."
                }
                """);
            var svc = new AppMetaService(path);

            var meta = svc.Load();

            Assert.Equal("1.2.3", meta.Version);
            Assert.Equal("SomeAuthor", meta.Authors);
            Assert.Equal("Copyright © 2026 SomeAuthor", meta.Copyright);
            Assert.Equal("MIT License · Copyright © 2026 SomeAuthor", meta.License);
            Assert.Equal("https://example.com/repo", meta.Repository);
            Assert.Equal("A test description.", meta.ShortDescription);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// 文件缺失时，version/copyright/shortDescription 应从程序集属性回退，
    /// authors/repository/license 无对应程序集属性，保持空字符串。
    /// </summary>
    [Fact]
    public void Load_MissingFile_FallsBackToAssemblyAttributes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"eb-missing-{Guid.NewGuid():N}.json");
        var svc = new AppMetaService(path);

        var meta = svc.Load();

        // 回退字段：来自 ElasticBreath.App.dll 的程序集属性
        Assert.False(string.IsNullOrEmpty(meta.Version));
        Assert.False(string.IsNullOrEmpty(meta.Copyright));
        Assert.False(string.IsNullOrEmpty(meta.ShortDescription));
        // 无程序集属性对应的字段：保持空
        Assert.Equal(string.Empty, meta.Authors);
        Assert.Equal(string.Empty, meta.Repository);
        Assert.Equal(string.Empty, meta.License);
    }

    /// <summary>
    /// 文件存在但为 0 字节（曾发生的真实故障场景），应与文件缺失一样回退到程序集属性。
    /// </summary>
    [Fact]
    public void Load_EmptyFile_FallsBackToAssemblyAttributes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"eb-empty-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, string.Empty);
            var svc = new AppMetaService(path);

            var meta = svc.Load();

            Assert.False(string.IsNullOrEmpty(meta.Version));
            Assert.False(string.IsNullOrEmpty(meta.Copyright));
            Assert.Equal(string.Empty, meta.Authors);
            Assert.Equal(string.Empty, meta.Repository);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Load_MalformedJson_FallsBackToAssemblyAttributes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"eb-bad-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ this is not valid json ]");
            var svc = new AppMetaService(path);

            var meta = svc.Load();

            Assert.False(string.IsNullOrEmpty(meta.Version));
            Assert.Equal(string.Empty, meta.Repository);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// json 中部分字段缺失（反序列化为 null）时，对应字段从程序集属性回退。
    /// version 来自 json（9.9.9），copyright/description 从程序集回退。
    /// </summary>
    [Fact]
    public void Load_PartialJson_FillsMissingFieldsFromAssembly()
    {
        var path = Path.Combine(Path.GetTempPath(), $"eb-null-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"version": "9.9.9"}""");
            var svc = new AppMetaService(path);

            var meta = svc.Load();

            Assert.Equal("9.9.9", meta.Version);
            Assert.False(string.IsNullOrEmpty(meta.Copyright));
            Assert.False(string.IsNullOrEmpty(meta.ShortDescription));
            Assert.Equal(string.Empty, meta.Authors);
            Assert.Equal(string.Empty, meta.Repository);
            Assert.Equal(string.Empty, meta.License);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void CrashLogDirectory_IsUnderTempElasticBreathCrash()
    {
        var expected = Path.Combine(Path.GetTempPath(), "ElasticBreath", "crash");
        Assert.Equal(expected, AppMetaService.CrashLogDirectory);
    }
}
