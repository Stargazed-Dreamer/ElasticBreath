using System.Runtime.InteropServices;
using System.Drawing;

namespace ElasticBreath.App.Interop;

internal static class Win32Native
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNoTopmost = new(-2);

    [StructLayout(LayoutKind.Sequential)]
    public struct LastInputInfo
    {
        public uint cbSize;
        public uint dwTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo plii);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    public static uint GetLastInputTick()
    {
        var info = new LastInputInfo
        {
            cbSize = (uint)Marshal.SizeOf<LastInputInfo>()
        };

        if (!GetLastInputInfo(ref info))
        {
            return 0;
        }

        return info.dwTime;
    }

    public static bool TryGetCursorPosition(out Point point) => GetCursorPos(out point);

    public static TimeSpan GetIdleDuration()
    {
        var lastTick = GetLastInputTick();
        var nowTick = unchecked((uint)Environment.TickCount);
        var elapsed = unchecked(nowTick - lastTick);
        return TimeSpan.FromMilliseconds(elapsed);
    }

    public static void SetClickThroughNoActivate(IntPtr hwnd)
    {
        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        style |= WsExTransparent | WsExToolWindow | WsExNoActivate;
        _ = SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style));
    }

    public static void SetWindowBoundsPixels(IntPtr hwnd, Rectangle bounds)
    {
        _ = SetWindowPos(
            hwnd,
            IntPtr.Zero,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            SwpNoZOrder | SwpNoActivate | SwpShowWindow);
    }

    /// <summary>强制将窗口置顶（先移除再重新声明 TOPMOST），防止被 Win+D 等操作推到后面</summary>
    public static void ForceTopmost(IntPtr hwnd)
    {
        /* 先移除 TOPMOST 标志，再重新设置，强制 Windows 重新评估 Z 序 */
        _ = SetWindowPos(
            hwnd,
            HwndNoTopmost,
            0, 0, 0, 0,
            SwpNoActivate | 0x0001 /* SWP_NOMOVE */ | 0x0002 /* SWP_NOSIZE */);
        _ = SetWindowPos(
            hwnd,
            HwndTopmost,
            0, 0, 0, 0,
            SwpNoActivate | 0x0001 /* SWP_NOMOVE */ | 0x0002 /* SWP_NOSIZE */);
    }

    #region UpdateLayeredWindow - 原生 Layered Window 渲染

    private const int WsExLayered = 0x80000;

    [StructLayout(LayoutKind.Sequential)]
    public struct BlendFunction
    {
        public byte BlendOp;       // AC_SRC_OVER = 0
        public byte BlendFlags;    // 必须为 0
        public byte SourceConstantAlpha; // 0-255，通常 255（完全由源 alpha 决定）
        public byte AlphaFormat;   // AC_SRC_ALPHA = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapInfoHeader
    {
        public int biSize;
        public int biWidth;
        public int biHeight;  // 负值 = 自顶向下
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Size
    {
        public int cx;
        public int cy;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(
        IntPtr hwnd,
        IntPtr hdcDst,
        ref Point pptDst,
        ref Size psize,
        IntPtr hdcSrc,
        ref Point pptSrc,
        int crKey,
        ref BlendFunction pblend,
        uint dwFlags);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(
        IntPtr hdc,
        ref BitmapInfoHeader pbmi,
        uint iUsage,
        out IntPtr ppvBits,
        IntPtr hSection,
        uint dwOffset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    private const uint UlwAlpha = 0x02;

    /// <summary>创建一个 32 位 ARGB DIB Section，返回内存指针和位图句柄</summary>
    public static IntPtr CreateArgbBitmap(int width, int height, out IntPtr pixels)
    {
        var bmi = new BitmapInfoHeader
        {
            biSize = Marshal.SizeOf<BitmapInfoHeader>(),
            biWidth = width,
            biHeight = -height, // 负值 = 自顶向下，行序与像素数组一致
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0 // BI_RGB
        };
        return CreateDIBSection(IntPtr.Zero, ref bmi, 0, out pixels, IntPtr.Zero, 0);
    }

    /// <summary>使用 UpdateLayeredWindow 将 ARGB 位图渲染到 Layered Window</summary>
    public static void RenderLayeredWindow(IntPtr hwnd, IntPtr hBitmap, IntPtr pixels, int width, int height)
    {
        var hdcSrc = CreateCompatibleDC(IntPtr.Zero);
        var oldBmp = SelectObject(hdcSrc, hBitmap);

        var ptDst = new Point { X = 0, Y = 0 };
        var size = new Size { cx = width, cy = height };
        var ptSrc = new Point { X = 0, Y = 0 };
        var blend = new BlendFunction
        {
            BlendOp = 0, // AC_SRC_OVER
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = 1 // AC_SRC_ALPHA
        };

        UpdateLayeredWindow(hwnd, IntPtr.Zero, ref ptDst, ref size, hdcSrc, ref ptSrc, 0, ref blend, UlwAlpha);

        SelectObject(hdcSrc, oldBmp);
        DeleteDC(hdcSrc);
    }

    /// <summary>将窗口设置为 Layered Window（WS_EX_LAYERED）</summary>
    public static void SetLayered(IntPtr hwnd)
    {
        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        style |= WsExLayered | WsExTransparent | WsExToolWindow | WsExNoActivate;
        _ = SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style));
    }

    /// <summary>销毁 GDI 对象</summary>
    public static void DestroyGdiObject(IntPtr hObject)
    {
        if (hObject != IntPtr.Zero)
            DeleteObject(hObject);
    }

    #endregion
}
