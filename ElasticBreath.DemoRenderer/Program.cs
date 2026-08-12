using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using ElasticBreath.Rendering;

namespace ElasticBreath.DemoRenderer;

/// <summary>
/// 离线渲染工具：复用 <see cref="EdgeOverlayPixelRenderer"/> 生成项目展示用图。
/// 输出 6 种状态 PNG、顶部进度条五联对比 PNG，以及一张 Warning 脉冲 GIF。
/// 支持两种运行方式：
///   - 交互模式（双击 exe / 无参数运行）：提示输入背景图片路径（或回车用合成渐变壁纸），
///     输入后一次性生成全部展示图。
///   - 命令行模式（供脚本使用）：--bg &lt;path&gt; / --out &lt;dir&gt; / --thickness &lt;px&gt; / --help。
/// 自定义背景输出文件名会追加 <c>--&lt;basename&gt;</c> 后缀，不会覆盖默认合成背景版本，便于多图对比。
/// </summary>
internal static class Program
{
    // 演示图尺寸：960x540（1920x1080 的 1/4 面积）。纯色合成壁纸无需原始大尺寸演示，
    // 缩小后 README 加载更快、仓库体积更小。thickness 为实机默认（160）的一半，保持视觉比例。
    private const int Width = 960;
    private const int Height = 540;
    private const int DefaultGlowThickness = 80;
    private const double BaseOpacity = 0.3;

    private static void Main(string[] args)
    {
        // 无参数（双击 exe）：进入交互模式，提示输入背景图路径后生成全部展示图
        if (args.Length == 0)
        {
            RunInteractive();
            return;
        }

        var opts = ParseArgs(args);
        if (opts is null)
            return;

        Directory.CreateDirectory(opts.OutDir);
        Console.WriteLine($"Output directory: {opts.OutDir}");
        Console.WriteLine($"Background:       {opts.BackgroundDescription}");
        Console.WriteLine($"Glow thickness:   {opts.Thickness}px");

        // 加载/生成背景一次，复用给所有渲染调用
        var bgBytes = opts.IsSynthetic
            ? GenerateSyntheticWallpaper()
            : LoadBackgroundBytes(opts.BgPath!);

        GenerateStatePngs(opts.OutDir, bgBytes, opts.FileSuffix, opts.Thickness);
        GenerateTopProgressGrid(opts.OutDir, bgBytes, opts.FileSuffix, opts.Thickness);
        GeneratePulseGif(opts.OutDir, bgBytes, opts.FileSuffix, opts.Thickness);

        Console.WriteLine("All assets generated.");
    }

    /// <summary>
    /// 交互模式：提示用户输入背景图片路径，输入后生成全部展示图。
    /// 输出到仓库 docs/screenshots/（命令行模式用 --out 可覆盖）。
    /// </summary>
    private static void RunInteractive()
    {
        Console.WriteLine("ElasticBreath 演示图生成工具");
        Console.WriteLine("================================");
        Console.WriteLine("将生成：6 种状态 PNG、顶部进度条五联对比 PNG、Warning 脉冲 GIF");
        Console.WriteLine();
        Console.Write("请输入背景图片路径（jpg/png/bmp；直接回车使用合成渐变壁纸）：");
        var input = Console.ReadLine()?.Trim();

        var outDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "docs", "screenshots");
        byte[] bgBytes;
        string suffix;
        string bgDesc;

        if (string.IsNullOrWhiteSpace(input))
        {
            bgBytes = GenerateSyntheticWallpaper();
            suffix = "";
            bgDesc = "合成渐变壁纸（默认）";
        }
        else if (!File.Exists(input))
        {
            Console.Error.WriteLine($"错误：文件不存在：{input}");
            WaitForExit();
            return;
        }
        else
        {
            bgBytes = LoadBackgroundBytes(input);
            suffix = "--" + SanitizeSuffix(Path.GetFileNameWithoutExtension(input));
            bgDesc = Path.GetFullPath(input);
        }

        Console.WriteLine($"背景：{bgDesc}");
        Console.WriteLine($"输出目录：{outDir}");
        Console.WriteLine("生成中...");
        Console.WriteLine();

        Directory.CreateDirectory(outDir);
        GenerateStatePngs(outDir, bgBytes, suffix, DefaultGlowThickness);
        GenerateTopProgressGrid(outDir, bgBytes, suffix, DefaultGlowThickness);
        GeneratePulseGif(outDir, bgBytes, suffix, DefaultGlowThickness);

        Console.WriteLine();
        Console.WriteLine("全部展示图生成完毕。");
        WaitForExit();
    }

    /// <summary>交互模式下暂停，避免双击打开的控制台窗口闪退。</summary>
    private static void WaitForExit()
    {
        Console.WriteLine();
        Console.WriteLine("按回车键退出...");
        Console.ReadLine();
    }

    // ----------------------------------------------------------------
    // 参数解析
    // ----------------------------------------------------------------

    private sealed class Options
    {
        public required string OutDir { get; init; }
        public required bool IsSynthetic { get; init; }
        public string? BgPath { get; init; }
        public required string BackgroundDescription { get; init; }
        public required string FileSuffix { get; init; } // "" 或 "--<basename>"
        public required int Thickness { get; init; }
    }

    /// <summary>
    /// 解析命令行参数。
    /// 用法：
    ///   ElasticBreath.DemoRenderer [--bg &lt;path&gt;] [--out &lt;dir&gt;] [-h|--help]
    /// </summary>
    private static Options? ParseArgs(string[] args)
    {
        string? bgPath = null;
        string? outDir = null;
        var thickness = DefaultGlowThickness;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "-h":
                case "--help":
                    PrintUsage();
                    return null;
                case "--bg":
                case "--background":
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine($"error: {a} 需要一个参数（背景图路径）");
                        return null;
                    }
                    bgPath = args[++i];
                    break;
                case "--out":
                case "--output":
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine($"error: {a} 需要一个参数（输出目录）");
                        return null;
                    }
                    outDir = args[++i];
                    break;
                case "--thickness":
                case "--thick":
                    if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out var th) || th < 1)
                    {
                        Console.Error.WriteLine($"error: {a} 需要一个正整数参数（光晕厚度像素）");
                        return null;
                    }
                    thickness = th;
                    i++;
                    break;
                default:
                    Console.Error.WriteLine($"error: 未知参数 '{a}'（用 --help 查看用法）");
                    return null;
            }
        }

        // 解析背景
        bool isSynthetic;
        string bgDesc;
        string suffix;
        if (string.IsNullOrWhiteSpace(bgPath))
        {
            isSynthetic = true;
            bgDesc = "synthetic gradient (default)";
            suffix = "";
        }
        else
        {
            if (!File.Exists(bgPath))
            {
                Console.Error.WriteLine($"error: 背景图不存在: {bgPath}");
                return null;
            }
            isSynthetic = false;
            bgDesc = $"custom: {Path.GetFullPath(bgPath)}";
            // 文件名后缀用 basename（去扩展名），保证多次测试不同背景不会互相覆盖
            suffix = "--" + SanitizeSuffix(Path.GetFileNameWithoutExtension(bgPath));
        }

        // 解析输出目录
        outDir = string.IsNullOrWhiteSpace(outDir)
            ? Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "docs", "screenshots")
            : Path.GetFullPath(outDir);

        return new Options
        {
            OutDir = outDir,
            IsSynthetic = isSynthetic,
            BgPath = bgPath,
            BackgroundDescription = bgDesc,
            FileSuffix = suffix,
            Thickness = thickness
        };
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
        ElasticBreath.DemoRenderer - 离线生成项目展示用截图

        用法:
          （无参数 / 双击 exe）进入交互模式，提示输入背景图路径后生成全部展示图
          dotnet run --project ElasticBreath.DemoRenderer -c Release -- [--bg <图片路径>] [--out <输出目录>] [--thickness <像素>]

        参数:
          --bg, --background <路径>   自定义背景图（jpg/png/bmp 等）。未指定时用合成渐变壁纸。
          --out, --output <目录>      输出目录。默认 docs/screenshots/。
          --thickness, --thick <像素> 光晕渐变厚度（像素），默认 80。960x540 下为实机默认（160）的一半。
          -h, --help                  显示本帮助。

        示例:
          # 交互模式（推荐）：输入背景图路径 → 生成全部展示图
          dotnet run --project ElasticBreath.DemoRenderer -c Release

          # 用自定义背景图生成（命令行模式）
          dotnet run --project ElasticBreath.DemoRenderer -c Release -- --bg D:\wallpapers\1.jpg

          # 指定输出目录
          dotnet run --project ElasticBreath.DemoRenderer -c Release -- --bg bg.jpg --out D:\out

          # 复现 1920x1080 实机厚度（演示图默认 80 是按 960x540 比例减半）
          dotnet run --project ElasticBreath.DemoRenderer -c Release -- --thickness 160

        输出:
          state-<状态><后缀>.png          6 种状态截图（960x540）
          top-progress-grid<后缀>.png     顶部进度条五联对比
          pulse-warning<后缀>.gif         Warning 脉冲动画（960x540, 15帧）
          后缀: 默认无；自定义背景时为 --<图片basename>
        """);
    }

    /// <summary>把文件名片段转为安全的后缀（只保留字母数字和连字符）。</summary>
    private static string SanitizeSuffix(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '-')
                sb.Append(c);
            else if (c is '_' or ' ')
                sb.Append('-');
        }
        var s = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(s) ? "custom" : s;
    }

    private static string FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            if (Directory.EnumerateFiles(dir.FullName, "ElasticBreath.sln").Any())
                return dir.FullName;
            dir = dir.Parent;
        }
        return start;
    }

    // ----------------------------------------------------------------
    // 1. 6 种状态 PNG
    // ----------------------------------------------------------------

    private static void GenerateStatePngs(string outDir, byte[] bgBytes, string suffix, int thickness)
    {
        var states = new[]
        {
            EdgeOverlayState.Warning,
            EdgeOverlayState.Hard,
            EdgeOverlayState.RestBase,
            EdgeOverlayState.RestElastic,
            EdgeOverlayState.RestOvertime,
            EdgeOverlayState.Paused
        };

        foreach (var state in states)
        {
            var visual = EdgeOverlayPixelRenderer.ResolveVisual(state);
            // 选取脉冲峰值相位（wave = 1.0），让截图呈现该状态最具代表性的强度
            var elapsed = visual.PulsePeriod > 0
                ? TimeSpan.FromSeconds(visual.PulsePeriod * 0.25)
                : TimeSpan.Zero;
            var factor = EdgeOverlayPixelRenderer.ComputeOpacityFactor(
                visual.AlphaFactor, visual.PulsePeriod, visual.Blink, elapsed);
            var alpha = (byte)Math.Clamp(BaseOpacity * factor * 255, 0, 255);

            using var bmp = RenderCompositedFrame(bgBytes, visual.R, visual.G, visual.B, alpha, showTopProgress: false, topProgressRatio: 0, thickness);
            var path = Path.Combine(outDir, $"state-{StateToKebab(state)}{suffix}.png");
            bmp.Save(path, ImageFormat.Png);
            Console.WriteLine($"  -> {Path.GetFileName(path)}  ({state}, alpha={alpha})");
        }
    }

    // ----------------------------------------------------------------
    // 2. 顶部进度条五联对比 PNG
    // ----------------------------------------------------------------

    private static void GenerateTopProgressGrid(string outDir, byte[] bgBytes, string suffix, int thickness)
    {
        var ratios = new[] { 0.0, 0.25, 0.5, 0.75, 1.0 };
        var panelHeight = 60;        // 只截顶部一段，足以呈现进度条与上边缘光晕（按 960x540 比例缩放）
        var gap = 12;
        var labelHeight = 18;
        var totalHeight = ratios.Length * panelHeight + (ratios.Length - 1) * gap + labelHeight * ratios.Length;
        var grid = new Bitmap(Width, totalHeight, PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(grid))
        {
            g.Clear(Color.FromArgb(20, 20, 28));

            using var font = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
            var labelBrush = new SolidBrush(Color.FromArgb(220, 220, 230));
            var labelFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            for (var i = 0; i < ratios.Length; i++)
            {
                var y = i * (panelHeight + gap + labelHeight);

                // 渲染一帧：上边缘光晕 + 进度条，截取顶部 panelHeight 像素
                var visual = EdgeOverlayPixelRenderer.ResolveVisual(EdgeOverlayState.Warning);
                var factor = EdgeOverlayPixelRenderer.ComputeOpacityFactor(
                    visual.AlphaFactor, visual.PulsePeriod, visual.Blink, TimeSpan.FromSeconds(visual.PulsePeriod * 0.25));
                var alpha = (byte)Math.Clamp(BaseOpacity * factor * 255, 0, 255);

                using var frame = RenderCompositedFrame(bgBytes, visual.R, visual.G, visual.B, alpha, showTopProgress: true, topProgressRatio: ratios[i], thickness);
                // 从 frame 中截取顶部 strip
                using var strip = new Bitmap(Width, panelHeight, PixelFormat.Format32bppArgb);
                using (var gs = Graphics.FromImage(strip))
                {
                    gs.DrawImage(frame, new Rectangle(0, 0, Width, panelHeight), new Rectangle(0, 0, Width, panelHeight), GraphicsUnit.Pixel);
                }
                g.DrawImageUnscaled(strip, 0, y);

                // 文本标签
                var labelRect = new Rectangle(0, y + panelHeight, Width, labelHeight);
                g.DrawString($"top progress = {ratios[i] * 100:F0}%", font, labelBrush, labelRect, labelFormat);
            }
        }

        var path = Path.Combine(outDir, $"top-progress-grid{suffix}.png");
        grid.Save(path, ImageFormat.Png);
        grid.Dispose();
        Console.WriteLine($"  -> {Path.GetFileName(path)}");
    }

    // ----------------------------------------------------------------
    // 3. Warning 脉冲 GIF
    // ----------------------------------------------------------------

    private static void GeneratePulseGif(string outDir, byte[] bgBytes, string suffix, int thickness)
    {
        var state = EdgeOverlayState.Warning;
        var visual = EdgeOverlayPixelRenderer.ResolveVisual(state);
        var period = visual.PulsePeriod > 0 ? visual.PulsePeriod : 3.0;
        var frameCount = 15;             // 一个周期 15 帧（4fps），平衡流畅度与体积
        var frameDelayCentiseconds = 20; // 每帧 200ms → 总时长 3s = 一个周期
        // 主尺寸已为 960x540，GIF 直接用同尺寸，无需再缩小
        var frames = new List<Bitmap>(frameCount);
        for (var i = 0; i < frameCount; i++)
        {
            var elapsed = TimeSpan.FromSeconds(period * i / frameCount);
            var factor = EdgeOverlayPixelRenderer.ComputeOpacityFactor(
                visual.AlphaFactor, visual.PulsePeriod, visual.Blink, elapsed);
            var alpha = (byte)Math.Clamp(BaseOpacity * factor * 255, 0, 255);
            using var full = RenderCompositedFrame(bgBytes, visual.R, visual.G, visual.B, alpha, showTopProgress: false, topProgressRatio: 0, thickness);
            // 复制一份作为 GIF 帧（RenderCompositedFrame 返回的 Bitmap 后续会被 Dispose）
            var frame = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(frame))
            {
                g.DrawImage(full, 0, 0, Width, Height);
            }
            frames.Add(frame);
        }

        var path = Path.Combine(outDir, $"pulse-warning{suffix}.gif");
        SaveAnimatedGif(path, frames, frameDelayCentiseconds);
        foreach (var f in frames) f.Dispose();
        Console.WriteLine($"  -> {Path.GetFileName(path)}  ({frameCount} frames, {period:F1}s period, {Width}x{Height})");
    }

    // ----------------------------------------------------------------
    // 帧合成：背景 + 渲染覆盖层 + alpha 合成
    // ----------------------------------------------------------------

    /// <summary>
    /// 渲染一帧完整的合成图：背景 → EdgeOverlayPixelRenderer 绘制覆盖层 → alpha 合成到背景。
    /// 返回的 Bitmap 像素与实机 Layered Window 输出像素级一致（除背景不同）。
    /// 背景字节数组会被复制一份，避免多次调用间互相污染。
    /// </summary>
    private static Bitmap RenderCompositedFrame(byte[] bgBytes, byte r, byte g, byte b, byte alpha,
        bool showTopProgress, double topProgressRatio, int thickness)
    {
        // 复制一份背景，因为 CompositeOnto 会就地修改
        var background = (byte[])bgBytes.Clone();
        var overlay = new byte[Width * Height * 4];

        unsafe
        {
            fixed (byte* p = overlay)
            {
                EdgeOverlayPixelRenderer.Render(
                    p, Width, Height,
                    r, g, b, alpha,
                    thickness, enableEdgeGlow: true,
                    showTopProgress, topProgressRatio);
            }
        }

        CompositeOnto(background, overlay);
        return BytesToBitmap(background);
    }

    /// <summary>
    /// 生成一张纯合成渐变壁纸（深蓝→深紫，无图标、无任务栏、无任何窗口）。
    /// </summary>
    private static byte[] GenerateSyntheticWallpaper()
    {
        var bytes = new byte[Width * Height * 4]; // BGRA
        // 顶部深蓝 #0a1838 → 底部深紫 #2a1248，纵向渐变
        const double r0 = 0x0a, g0 = 0x18, b0 = 0x38;
        const double r1 = 0x2a, g1 = 0x12, b1 = 0x48;

        for (var y = 0; y < Height; y++)
        {
            var t = (double)y / (Height - 1);
            var r = (byte)Math.Round(r0 + (r1 - r0) * t);
            var gg = (byte)Math.Round(g0 + (g1 - g0) * t);
            var bb = (byte)Math.Round(b0 + (b1 - b0) * t);
            var rowOffset = y * Width * 4;
            for (var x = 0; x < Width; x++)
            {
                var i = rowOffset + x * 4;
                bytes[i] = bb;     // B
                bytes[i + 1] = gg; // G
                bytes[i + 2] = r;  // R
                bytes[i + 3] = 255; // A
            }
        }
        return bytes;
    }

    /// <summary>
    /// 加载自定义背景图，cover 模式缩放裁剪到 Width×Height，返回 BGRA 字节数组。
    /// cover 模式：按比例放大到完全覆盖目标，居中裁剪，保证不留黑边。
    /// </summary>
    private static byte[] LoadBackgroundBytes(string path)
    {
        using var src = Image.FromFile(path);
        var srcW = src.Width;
        var srcH = src.Height;

        // 计算覆盖缩放比例
        var scale = Math.Max((double)Width / srcW, (double)Height / srcH);
        var scaledW = (int)Math.Round(srcW * scale);
        var scaledH = (int)Math.Round(srcH * scale);

        // 缩放后居中裁剪到 Width×Height
        using var scaled = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            // 先填充黑色避免边缘缝隙
            g.Clear(Color.Black);
            var dx = (Width - scaledW) / 2;
            var dy = (Height - scaledH) / 2;
            g.DrawImage(src, dx, dy, scaledW, scaledH);
        }

        // 转为 BGRA byte[]
        var bytes = new byte[Width * Height * 4];
        var rect = new Rectangle(0, 0, Width, Height);
        var data = scaled.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
        }
        finally
        {
            scaled.UnlockBits(data);
        }

        // Format32bppArgb 在 Windows 上是 BGRA，但 alpha 通道可能不是 255。
        // 强制设为 255，因为背景需要完全不透明。
        for (var i = 3; i < bytes.Length; i += 4)
            bytes[i] = 255;

        return bytes;
    }

    /// <summary>
    /// 将覆盖层（BGRA，带 alpha）alpha-composite 到背景（BGRA，alpha=255）上。
    /// </summary>
    private static void CompositeOnto(byte[] background, byte[] overlay)
    {
        /* overlay 现为 premultiplied BGRA（与实机 UpdateLayeredWindow AC_SRC_ALPHA 一致）。
         * premultiplied over：out = src_premul + dst*(1-a/255) */
        for (var i = 0; i < background.Length; i += 4)
        {
            var a = overlay[i + 3];
            if (a == 0) continue;
            var inv = 255 - a;
            background[i] = (byte)Math.Min(255, overlay[i] + (background[i] * inv) / 255);
            background[i + 1] = (byte)Math.Min(255, overlay[i + 1] + (background[i + 1] * inv) / 255);
            background[i + 2] = (byte)Math.Min(255, overlay[i + 2] + (background[i + 2] * inv) / 255);
            // background[i + 3] 已经是 255，保持不变
        }
    }

    /// <summary>
    /// BGRA 字节数组 → System.Drawing.Bitmap（Format32bppArgb）。
    /// Windows 下 Format32bppArgb 的内存布局即为 BGRA，可直接 memcpy。
    /// </summary>
    private static Bitmap BytesToBitmap(byte[] bgra)
    {
        var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, Width, Height);
        var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(bgra, 0, data.Scan0, bgra.Length);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
        return bmp;
    }

    // ----------------------------------------------------------------
    // GIF 编码：GDI+ 多帧时间维度
    // ----------------------------------------------------------------

    /// <summary>
    /// 将多帧 Bitmap 保存为动画 GIF。
    /// 利用 GDI+ 的 MultiFrame / FrameDimensionTime / Flush 编码流程。
    /// 关键：FrameDelay（0x5100）与 LoopCount（0x5101）属性必须在第一次 Save 之前设置，
    /// 否则 GDI+ 不会将它们写入文件，导致 GIF 以最快速度播放且只播一次。
    /// </summary>
    private static void SaveAnimatedGif(string path, List<Bitmap> frames, int delayCentiseconds)
    {
        if (frames.Count == 0) return;

        var gifEncoder = ImageCodecInfo.GetImageEncoders()
            .First(c => c.FormatID == ImageFormat.Gif.Guid);

        var first = frames[0];

        // FrameDelay（0x5100）：每帧延迟，单位 1/100 秒，类型 Short=3，值需为 frames.Count 个 short 的小端数组
        var delayBytes = new short[frames.Count];
        for (var i = 0; i < frames.Count; i++)
            delayBytes[i] = (short)delayCentiseconds;
        first.SetPropertyItem(CreatePropertyItem(0x5100, 3, ShortToByteArray(delayBytes)));

        // LoopCount（0x5101）：0 表示无限循环，类型 Long=7，4 字节小端
        first.SetPropertyItem(CreatePropertyItem(0x5101, 7, new byte[] { 0, 0, 0, 0 }));

        // 第一帧
        var firstParams = new EncoderParameters(1);
        firstParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
        first.Save(path, gifEncoder, firstParams);

        // 后续帧
        for (var i = 1; i < frames.Count; i++)
        {
            var addParams = new EncoderParameters(1);
            addParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);
            first.SaveAdd(frames[i], addParams);
        }

        // 收尾
        var flushParams = new EncoderParameters(1);
        flushParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.Flush);
        first.SaveAdd(flushParams);
    }

    /// <summary>
    /// GDI+ 的 <see cref="PropertyItem"/> 没有公开构造函数，<c>new Bitmap(1,1).PropertyItems</c>
    /// 在新版本 GDI+ 上常返回空数组。改用反射调用其非公开无参构造函数。
    /// </summary>
    private static PropertyItem CreatePropertyItem(int id, short type, byte[] value)
    {
        var ctor = typeof(PropertyItem).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null, Type.EmptyTypes, null)
            ?? throw new InvalidOperationException("PropertyItem has no accessible parameterless constructor.");
        var item = (PropertyItem)ctor.Invoke(null);
        item.Id = id;
        item.Type = type;
        item.Len = value.Length;
        item.Value = value;
        return item;
    }

    /// <summary>short[] → little-endian byte[]（GDI+ 要求小端）。</summary>
    private static byte[] ShortToByteArray(short[] values)
    {
        var bytes = new byte[values.Length * 2];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    // ----------------------------------------------------------------
    // 工具
    // ----------------------------------------------------------------

    private static string StateToKebab(EdgeOverlayState state) => state switch
    {
        EdgeOverlayState.Hidden => "hidden",
        EdgeOverlayState.Warning => "warning",
        EdgeOverlayState.Hard => "hard",
        EdgeOverlayState.RestBase => "rest-base",
        EdgeOverlayState.RestElastic => "rest-elastic",
        EdgeOverlayState.RestOvertime => "rest-overtime",
        EdgeOverlayState.Paused => "paused",
        _ => state.ToString().ToLowerInvariant()
    };
}
