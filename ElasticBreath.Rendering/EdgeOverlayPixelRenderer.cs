namespace ElasticBreath.Rendering;

/// <summary>
/// 边框覆盖状态枚举，用于表示各种覆盖状态。
/// </summary>
public enum EdgeOverlayState
{
    // 隐藏状态
    Hidden,
    // 警告状态（工作预警：橙色）
    Warning,
    // 硬状态（工作硬性：红色）
    Hard,
    // 休息基础状态（绿色）
    RestBase,
    // 休息弹性状态（亮绿）
    RestElastic,
    // 休息超时状态（亮绿 + 闪烁）
    RestOvertime,
    // 暂停状态（灰色）
    Paused
}

/// <summary>
/// 边缘覆盖层的视觉参数（颜色、透明度因子、脉冲周期、是否闪烁）。
/// 由 <see cref="EdgeOverlayPixelRenderer.ResolveVisual"/> 根据状态产出。
/// </summary>
public readonly record struct EdgeOverlayVisual(
    byte R,
    byte G,
    byte B,
    double AlphaFactor,
    double PulsePeriod,
    bool Blink);

/// <summary>
/// 边缘光晕 + 顶部进度条的纯像素渲染逻辑。
/// 与 HWND / Win32 无关：既被主应用的 Layered Window（UpdateLayeredWindow）使用，
/// 也被离线渲染工具（PNG / GIF 截图生成）共用，保证截图与实机像素级一致。
/// 本类不依赖 WPF / WinForms / Win32，可被任意 net8.0 项目引用。
/// </summary>
public static class EdgeOverlayPixelRenderer
{
    /// <summary>
    /// 根据覆盖状态解析出对应的视觉参数（颜色、透明度因子、脉冲周期、闪烁）。
    /// 颜色表与主应用原 <c>EdgeOverlayWindow.ResolveVisual</c> 完全一致。
    /// </summary>
    public static EdgeOverlayVisual ResolveVisual(EdgeOverlayState state)
    {
        return state switch
        {
            EdgeOverlayState.Hidden => default,
            EdgeOverlayState.Warning => new EdgeOverlayVisual(245, 138, 56, 1.0, 3.0, false),
            EdgeOverlayState.Hard => new EdgeOverlayVisual(230, 58, 58, 1.0, 1.0, false),
            EdgeOverlayState.RestBase => new EdgeOverlayVisual(67, 183, 108, 1.0, 3.0, false),
            EdgeOverlayState.RestElastic => new EdgeOverlayVisual(47, 206, 103, 1.1, 1.8, false),
            EdgeOverlayState.RestOvertime => new EdgeOverlayVisual(47, 206, 103, 1.1, 1.0, true),
            EdgeOverlayState.Paused => new EdgeOverlayVisual(102, 102, 102, 0.75, 0.0, false),
            _ => default
        };
    }

    /// <summary>
    /// 根据脉冲周期与闪烁标记，计算某一时刻的不透明度因子。
    /// 与主应用原 <c>EdgeOverlayWindow.RenderFrame</c> 中的脉冲/闪烁计算一致。
    /// </summary>
    /// <param name="baseAlphaFactor">基础因子（来自 <see cref="ResolveVisual"/> 的 <see cref="EdgeOverlayVisual.AlphaFactor"/>）。</param>
    /// <param name="pulsePeriod">脉冲周期（秒），&lt;=0 表示无脉冲。</param>
    /// <param name="blink">是否启用 500ms 步进闪烁。</param>
    /// <param name="elapsedSinceStart">自动画开始以来的时长。</param>
    /// <returns>应用到基础不透明度上的乘数因子。</returns>
    public static double ComputeOpacityFactor(double baseAlphaFactor, double pulsePeriod, bool blink, TimeSpan elapsedSinceStart)
    {
        var factor = baseAlphaFactor;
        if (pulsePeriod > 0)
        {
            var phase = elapsedSinceStart.TotalSeconds / pulsePeriod;
            var wave = 0.5 + (0.5 * Math.Sin(phase * Math.PI * 2));
            factor *= 0.35 + (0.65 * wave);
        }
        if (blink)
        {
            var step = (int)(elapsedSinceStart.TotalMilliseconds / 500) % 2;
            factor *= step == 0 ? 1 : 0.2;
        }
        return factor;
    }

    /// <summary>
    /// 在 BGRA 像素缓冲区中绘制四边渐变光晕与（可选）顶部进度条。
    /// 缓冲会被先清空为全透明，再绘制光晕与进度条。
    /// </summary>
    /// <param name="pixels">像素缓冲指针（BGRA，从左上角开始，stride = <paramref name="width"/>*4）。</param>
    /// <param name="width">缓冲宽度（像素）。</param>
    /// <param name="height">缓冲高度（像素）。</param>
    /// <param name="r">光晕红色分量。</param>
    /// <param name="g">光晕绿色分量。</param>
    /// <param name="b">光晕蓝色分量。</param>
    /// <param name="alpha">光晕最终 alpha（应已乘以脉冲/闪烁因子）。</param>
    /// <param name="glowThickness">光晕最大渗透厚度（像素）。</param>
    /// <param name="enableEdgeGlow">是否绘制四边光晕。</param>
    /// <param name="showTopProgress">是否绘制顶部进度条。</param>
    /// <param name="topProgressRatio">顶部进度条填充比例（0~1）。</param>
    public static unsafe void Render(
        byte* pixels, int width, int height,
        byte r, byte g, byte b, byte alpha,
        int glowThickness, bool enableEdgeGlow,
        bool showTopProgress, double topProgressRatio)
    {
        var stride = width * 4;
        var totalBytes = stride * height;

        /* 清空为全透明：用 native memset 替代逐字节循环，
         * 1920x1080 缓冲约 8MB，逐字节循环是每帧 13ms 耗时的主要来源。 */
        System.Runtime.CompilerServices.Unsafe.InitBlock(ref pixels[0], 0, (uint)totalBytes);

        if (alpha == 0)
            return;

        var t = glowThickness;

        if (enableEdgeGlow)
        {
            /* 上边缘：从上到下渐变，alpha 从 alpha 渐变到 0。
             * UpdateLayeredWindow 使用 AC_SRC_ALPHA（premultiplied）模式，
             * 因此 RGB 必须乘以 alpha/255，否则 alpha 变化对最终显示颜色几乎无影响，
             * 导致"透明度一样、渐变消失"的视觉 bug。 */
            for (var y = 0; y < t && y < height; y++)
            {
                var ratio = 1.0 - (double)y / t;
                var a = (byte)(alpha * ratio);
                var pr = (byte)(r * a / 255);
                var pg = (byte)(g * a / 255);
                var pb = (byte)(b * a / 255);
                var rowOffset = y * stride;
                for (var x = 0; x < width; x++)
                {
                    var offset = rowOffset + x * 4;
                    pixels[offset] = pb;
                    pixels[offset + 1] = pg;
                    pixels[offset + 2] = pr;
                    pixels[offset + 3] = a;
                }
            }

            /* 下边缘：从下到上渐变（premultiplied） */
            for (var y = Math.Max(0, height - t); y < height; y++)
            {
                var ratio = (double)(y - (height - t)) / t;
                var a = (byte)(alpha * ratio);
                var pr = (byte)(r * a / 255);
                var pg = (byte)(g * a / 255);
                var pb = (byte)(b * a / 255);
                var rowOffset = y * stride;
                for (var x = 0; x < width; x++)
                {
                    var offset = rowOffset + x * 4;
                    pixels[offset] = pb;
                    pixels[offset + 1] = pg;
                    pixels[offset + 2] = pr;
                    pixels[offset + 3] = a;
                }
            }

            /* 左边缘：从左到右渐变（与上下边缘在角落叠加） */
            for (var y = 0; y < height; y++)
            {
                var rowOffset = y * stride;
                for (var x = 0; x < t && x < width; x++)
                {
                    var ratio = 1.0 - (double)x / t;
                    var a = (byte)(alpha * ratio);
                    var offset = rowOffset + x * 4;
                    BlendPixel(pixels, offset, r, g, b, a);
                }
            }

            /* 右边缘：从右到左渐变 */
            for (var y = 0; y < height; y++)
            {
                var rowOffset = y * stride;
                for (var x = Math.Max(0, width - t); x < width; x++)
                {
                    var ratio = (double)(x - (width - t)) / t;
                    var a = (byte)(alpha * ratio);
                    var offset = rowOffset + x * 4;
                    BlendPixel(pixels, offset, r, g, b, a);
                }
            }
        }

        if (showTopProgress)
        {
            DrawProgressBar(pixels, width, height, r, g, b, alpha, topProgressRatio);
        }
    }

    /// <summary>
    /// 在像素缓冲区中绘制顶部进度条（居中、360x10、距顶 8px）。
    /// 与主应用原 <c>EdgeOverlayWindow.DrawProgressBar</c> 像素级一致。
    /// </summary>
    public static unsafe void DrawProgressBar(
        byte* pixels, int width, int height,
        byte r, byte g, byte b, byte alpha, double topProgressRatio)
    {
        var barWidth = 360;
        var barHeight = 10;
        var barX = (width - barWidth) / 2;
        var barY = 8;

        /* 背景：半透明深色（premultiplied：0x10 * 0x30/255 ≈ 3） */
        const byte bgAlpha = 0x30;
        const byte bgPremul = (byte)(0x10 * bgAlpha / 255);
        for (var y = barY; y < barY + barHeight && y < height; y++)
        {
            var rowOffset = y * width * 4;
            for (var x = barX; x < barX + barWidth && x < width; x++)
            {
                var offset = rowOffset + x * 4;
                pixels[offset] = bgPremul;
                pixels[offset + 1] = bgPremul;
                pixels[offset + 2] = bgPremul;
                pixels[offset + 3] = bgAlpha;
            }
        }

        /* 填充部分（premultiplied） */
        var fillWidth = (int)(barWidth * Math.Clamp(topProgressRatio, 0, 1));
        var pr = (byte)(r * alpha / 255);
        var pg = (byte)(g * alpha / 255);
        var pb = (byte)(b * alpha / 255);
        for (var y = barY; y < barY + barHeight && y < height; y++)
        {
            var rowOffset = y * width * 4;
            for (var x = barX; x < barX + fillWidth && x < width; x++)
            {
                var offset = rowOffset + x * 4;
                pixels[offset] = pb;
                pixels[offset + 1] = pg;
                pixels[offset + 2] = pr;
                pixels[offset + 3] = alpha;
            }
        }
    }

    /// <summary>将新像素与已有像素做 premultiplied alpha 叠加混合（用于角落重叠区域）。
    /// 输入与输出均为 premultiplied BGRA，匹配 UpdateLayeredWindow 的 AC_SRC_ALPHA 模式。</summary>
    private static unsafe void BlendPixel(byte* ptr, int offset, byte r, byte g, byte b, byte a)
    {
        if (a == 0) return;

        /* premultiplied 源 RGB */
        var srcR = (r * a) / 255;
        var srcG = (g * a) / 255;
        var srcB = (b * a) / 255;

        var dstA = ptr[offset + 3];
        if (dstA == 0)
        {
            ptr[offset] = (byte)srcB;
            ptr[offset + 1] = (byte)srcG;
            ptr[offset + 2] = (byte)srcR;
            ptr[offset + 3] = a;
            return;
        }

        var dstB = ptr[offset];
        var dstG = ptr[offset + 1];
        var dstR = ptr[offset + 2];

        /* premultiplied over 操作：outA = srcA + dstA*(1-srcA/255)；
         * outRGB = srcRGB_premul + dstRGB_premul*(1-srcA/255) */
        var inv = 255 - a;
        var outA = a + (dstA * inv) / 255;
        if (outA == 0) return;

        ptr[offset] = (byte)Math.Min(255, srcB + (dstB * inv) / 255);
        ptr[offset + 1] = (byte)Math.Min(255, srcG + (dstG * inv) / 255);
        ptr[offset + 2] = (byte)Math.Min(255, srcR + (dstR * inv) / 255);
        ptr[offset + 3] = (byte)outA;
    }
}
