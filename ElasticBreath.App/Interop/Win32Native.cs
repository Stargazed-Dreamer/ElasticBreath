using System.Runtime.InteropServices;
using System.Drawing;

namespace ElasticBreath.App.Interop;

/// <summary>
/// 封装常用 Win32 API 调用，用于窗口操作、输入检测和分层窗口支持。
/// </summary>
internal static class Win32Native
{
    // 窗口扩展样式 (GWL_EXSTYLE) 的索引值
    private const int GwlExStyle = -20;
    // 使窗口透明，鼠标事件穿透到下层窗口
    private const int WsExTransparent = 0x20;
    // 工具窗口样式，不会在任务栏或 Alt+Tab 列表中显示
    private const int WsExToolWindow = 0x80;
    // 窗口激活时不成为前台窗口，不接收键盘输入焦点
    private const int WsExNoActivate = 0x08000000;
    // SetWindowPos 函数标志：保持当前 Z 序（不改变窗口在堆栈中的前后位置）
    private const uint SwpNoZOrder = 0x0004;
    // SetWindowPos 函数标志：不激活窗口
    private const uint SwpNoActivate = 0x0010;
    // SetWindowPos 函数标志：显示窗口（如果之前隐藏）
    private const uint SwpShowWindow = 0x0040;
    // 特殊的窗口句柄值，用于将窗口置于 Z 序顶部（置顶）
    private static readonly IntPtr HwndTopmost = new(-1);
    // 特殊的窗口句柄值，用于将窗口移出置顶状态
    private static readonly IntPtr HwndNoTopmost = new(-2);

    // 用于获取系统最后输入信息的结构体
    [StructLayout(LayoutKind.Sequential)]
    /// <summary>
    /// 用于获取系统最后一次输入事件信息的结构体。
    /// 通常配合 P/Invoke 调用 Windows API (如 GetLastInputInfo) 使用，
    /// 以检测用户不活动（空闲）的时间。
    /// </summary>
    public struct LastInputInfo
    {
        public uint cbSize; // 结构体的大小（字节）
        public uint dwTime; // 最后一次输入事件的时间戳（毫秒）
    }

    // 表示屏幕上的一个点（X, Y坐标）
    [StructLayout(LayoutKind.Sequential)]
    /// <summary>
    /// 表示一个二维点，具有X和Y坐标。
    /// </summary>
    public struct Point
    {
        public int X;
        public int Y;
    }

    // 表示一个矩形区域（左上角和右下角坐标）
    [StructLayout(LayoutKind.Sequential)]
/// <summary>
    /// 表示一个矩形结构，定义了矩形的左、上、右、下边界。
    /// </summary>
    public struct Rect
    {
        public int Left; // 左边界
        public int Top; // 上边界
        public int Right; // 右边界
        public int Bottom; // 下边界
    }

    [DllImport("user32.dll")]
    /// <summary>
    /// 从 user32.dll 导入的函数，用于获取系统最后一次输入事件的信息。
    /// </summary>
    /// <param name="plii">指向一个 LastInputInfo 结构体的引用，该结构体将被填充最后输入信息。</param>
    /// <returns>如果函数调用成功，则返回 true；否则返回 false。</returns>
    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo plii);

    /// <summary>
    /// 从 user32.dll 导入的函数，用于获取鼠标光标的屏幕坐标。
    /// </summary>
    /// <param name="point">一个 Point 结构体，用于接收光标的坐标。这是一个输出参数。</param>
    /// <returns>如果函数调用成功，则返回 true；否则返回 false。</returns>
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    /// <summary>
    /// 从 user32.dll 导入的函数，用于获取当前前台窗口（用户正在交互的窗口）的句柄。
    /// </summary>
    /// <returns>前台窗口的句柄（IntPtr）。如果没有前台窗口，则返回 NULL。</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// 从 user32.dll 导入的函数，用于获取指定窗口的边界矩形（以屏幕坐标表示）。
    /// </summary>
    /// <param name="hwnd">目标窗口的句柄。</param>
    /// <param name="rect">一个 Rect 结构体，用于接收窗口的矩形位置和大小。这是一个输出参数。</param>
    /// <returns>如果函数调用成功，则返回 true；否则返回 false。</returns>
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    /// <summary>
    /// 获取指定窗口所属的线程和进程的标识符。
    /// </summary>
    /// <param name="hWnd">目标窗口的句柄。</param>
    /// <param name="lpdwProcessId">一个无符号整型变量，用于接收窗口所属的进程标识符。这是一个输出参数。</param>
    /// <returns>返回值是创建指定窗口的线程的线程标识符。</returns>
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>
    /// 获取窗口指定偏移量的属性值（此处用于获取扩展样式）。
    /// </summary>
    /// <param name="hWnd">目标窗口的句柄。</param>
    /// <param name="nIndex">要获取的属性值的偏移量。例如，GWL_EXSTYLE (-20) 表示窗口扩展样式。</param>
    /// <returns>返回请求的窗口属性值。如果函数失败，返回值为0。要获得扩展的错误信息，请调用 GetLastError。</returns>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    /// <summary>
    /// 修改窗口指定偏移量的属性值（此处用于设置扩展样式）。
    /// </summary>
    /// <param name="hWnd">目标窗口的句柄。</param>
    /// <param name="nIndex">要设置的属性值的偏移量。例如，GWL_EXSTYLE (-20) 表示窗口扩展样式。</param>
    /// <param name="dwNewLong">指定的新属性值。</param>
    /// <returns>如果函数调用成功，返回值是指定偏移量的前一个值。如果函数失败，返回值为0。要获得扩展的错误信息，请调用 GetLastError。</returns>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary>
    /// 改变窗口的位置、大小和Z序（即窗口在栈中的前后顺序）。
    /// </summary>
    /// <param name="hWnd">要改变的窗口的句柄。</param>
    /// <param name="hWndInsertAfter">在Z序中位于被置位的窗口前的窗口句柄。此参数可以是一个窗口句柄或特定值（如HWND_TOP, HWND_BOTTOM等）。</param>
    /// <param name="x">窗口左上角的新X坐标。</param>
    /// <param name="y">窗口左上角的新Y坐标。</param>
    /// <param name="cx">窗口的新宽度（以像素为单位）。</param>
    /// <param name="cy">窗口的新高度（以像素为单位）。</param>
    /// <param name="uFlags>窗口尺寸和定位的标志。例如，SWP_NOMOVE, SWP_NOSIZE, SWP_NOZORDER等。</param>
    /// <returns>如果函数调用成功，则返回 true；否则返回 false。</returns>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    /// <summary>
    /// 获取系统最后一次输入事件的时间戳。
    /// </summary>
    /// <returns>返回最后一次输入事件的时间戳（以毫秒为单位）。如果调用失败则返回0。</returns>
    public static uint GetLastInputTick()
    {
        // 初始化结构体并设置其大小字段
        var info = new LastInputInfo
        {
            cbSize = (uint)Marshal.SizeOf<LastInputInfo>()
        };

        // 调用系统API获取最后输入信息
        if (!GetLastInputInfo(ref info))
        {
            // 如果调用失败，返回0
            return 0;
        }

        // 返回结构体中存储的最后输入时间戳
        return info.dwTime;
    }

    /// <summary>
    /// 尝试获取当前鼠标光标的屏幕坐标。
    /// </summary>
    public static bool TryGetCursorPosition(out Point point) => GetCursorPos(out point);

    /// <summary>
    /// 计算自上次用户输入（键盘、鼠标）以来的系统空闲时间。
    /// </summary>
    /// <returns>表示空闲时间的 TimeSpan 对象。</returns>
    public static TimeSpan GetIdleDuration()
    {
        var lastTick = GetLastInputTick();
        // 获取当前系统运行时间（毫秒），使用 unchecked 防止在溢出时抛出异常
        var nowTick = unchecked((uint)Environment.TickCount);
        // 计算时间差，同样使用 unchecked 处理可能的环绕（wrap-around）情况
        var elapsed = unchecked(nowTick - lastTick);
        return TimeSpan.FromMilliseconds(elapsed);
    }

    /// <summary>
    /// 将窗口设置为鼠标点击穿透且不激活的样式（常用于透明覆盖层）。
    /// </summary>
    public static void SetClickThroughNoActivate(IntPtr hwnd)
    {
        // 获取窗口当前的扩展样式值
        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        // 通过按位或操作添加新的样式标志
        style |= WsExTransparent | WsExToolWindow | WsExNoActivate;
        // 将修改后的样式值设置回窗口
        _ = SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style));
    }

    /// <summary>
    /// 设置窗口的位置和大小（以像素为单位）。
    /// </summary>
    public static void SetWindowBoundsPixels(IntPtr hwnd, Rectangle bounds)
    {
        _ = SetWindowPos(
            hwnd,
            IntPtr.Zero, // 不改变窗口的Z序位置
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            // 组合标志：不改变Z序、不激活窗口、显示窗口
            SwpNoZOrder | SwpNoActivate | SwpShowWindow);
    }

    /// <summary>强制将窗口置顶（先移除再重新声明 TOPMOST），防止被 Win+D 等操作推到后面</summary>
    public static void ForceTopmost(IntPtr hwnd)
    {
        /* 先移除 TOPMOST 标志，再重新设置，强制 Windows 重新评估 Z 序 */
        // 第一步：将窗口移出置顶状态（设为非置顶）
        _ = SetWindowPos(
            hwnd,
            HwndNoTopmost,
            0, 0, 0, 0,
            SwpNoActivate | 0x0001 /* SWP_NOMOVE */ | 0x0002 /* SWP_NOSIZE */);
        // 第二步：立即将窗口重新设为置顶
        _ = SetWindowPos(
            hwnd,
            HwndTopmost,
            0, 0, 0, 0,
            SwpNoActivate | 0x0001 /* SWP_NOMOVE */ | 0x0002 /* SWP_NOSIZE */);
    }

    #region UpdateLayeredWindow - 原生 Layered Window 渲染

    // 分层窗口扩展样式标志
    private const int WsExLayered = 0x80000;

    // 控制分层窗口混合操作的结构体（通常用于 Alpha 混合）
    [StructLayout(LayoutKind.Sequential)]
    /// <summary>
    /// 定义alpha混合函数的结构，用于指定混合操作、标志、常量alpha和alpha格式。
    /// </summary>
    public struct BlendFunction
    {
        public byte BlendOp;       // AC_SRC_OVER = 0
        public byte BlendFlags;    // 必须为 0
        public byte SourceConstantAlpha; // 0-255，通常 255（完全由源 alpha 决定）
        public byte AlphaFormat;   // AC_SRC_ALPHA = 1
    }

    // 位图信息头结构体，用于描述DIB（设备无关位图）的尺寸和格式
    [StructLayout(LayoutKind.Sequential)]
    /// <summary>
    /// 位图信息头结构体，用于描述位图图像的属性，如尺寸、颜色深度等。
    /// </summary>
    public struct BitmapInfoHeader
    {
        public int biSize;         // 此结构体的大小（字节）
        public int biWidth;        // 位图宽度（像素）
        public int biHeight;       // 位图高度（像素），负值 = 自顶向下
        public short biPlanes;     // 目标设备的平面数，必须为1
        public short biBitCount;   // 每像素的位数
        public int biCompression;  // 压缩类型（0 表示不压缩，即 BI_RGB）
        public int biSizeImage;    // 图像的大小（字节），对于 BI_RGB 可为0
        public int biXPelsPerMeter;// 水平分辨率（像素/米）
        public int biYPelsPerMeter;// 垂直分辨率（像素/米）
        public int biClrUsed;      // 颜色表中实际使用的颜色索引数
        public int biClrImportant; // 显示图像所需的重要颜色索引数
    }

    // 表示尺寸（宽度和高度）
    [StructLayout(LayoutKind.Sequential)]
    /// <summary>
    /// 表示尺寸的结构体，包含宽度和高度。
    /// </summary>
    public struct Size
    {
        public int cx; // 宽度
        public int cy; // 高度
    }

    // 更新分层窗口的位置、大小、形状、内容和透明度
    [DllImport("user32.dll", SetLastError = true)]
/// <summary>
/// 更新分层窗口的位置、大小、形状和内容。
/// </summary>
/// <param name="hwnd">分层窗口的句柄</param>
/// <param name="hdcDst">指向屏幕DC的句柄。如果为NULL，则使用默认的屏幕DC</param>
/// <param name="pptDst">指向窗口新位置的POINT结构的指针。如果为NULL，则使用当前窗口位置</param>
/// <param name="psize">指向窗口新大小的SIZE结构的指针。如果为NULL，则使用当前窗口大小</param>
/// <param name="hdcSrc">指向表面DC的句柄，用于定义窗口内容。如果为NULL，则使用之前指定的DC</param>
/// <param name="pptSrc">指向表面DC中像素偏移的POINT结构的指针。如果为NULL，则使用默认值(0,0)</param>
/// <param name="crKey">用于透明度的颜色键。如果为0，则不使用颜色键</param>
/// <param name="pblend">指向BlendFunction结构的指针，指定用于分层窗口的透明度信息</param>
/// <param name="dwFlags">标志，指示如何更新窗口。可以是ULW_ALPHA、ULW_COLORKEY、ULW_OPAQUE的组合</param>
/// <returns>如果函数成功，则返回非零值；如果失败，则返回零。要获取扩展错误信息，请调用Marshal.GetLastWin32Error</returns>
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

    // 创建一个应用程序可以直接写入的、与设备无关的位图（DIB），并返回指向位图像素值的指针
    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(
        IntPtr hdc,
        ref BitmapInfoHeader pbmi,
        uint iUsage,
        out IntPtr ppvBits,
        IntPtr hSection,
        uint dwOffset);

    // 创建与指定设备兼容的内存设备上下文（DC）
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    // 选择一个对象到指定的设备上下文中，并返回之前对象的句柄
    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    // 从内存中删除一个逻辑画笔、字体、位图、区域或调色板，释放相关系统资源
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    // 删除指定的设备上下文（DC）
    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    // UpdateLayeredWindow 的标志：使用 Alpha 混合通道
    private const uint UlwAlpha = 0x02;

    /// <summary>创建一个 32 位 ARGB DIB Section，返回内存指针和位图句柄</summary>
    /// <param name="width">位图宽度</param>
    /// <param name="height">位图高度</param>
    /// <param name="pixels">返回指向位图像素数据的内存指针</param>
    /// <returns>返回创建的DIB位图句柄</returns>
    public static IntPtr CreateArgbBitmap(int width, int height, out IntPtr pixels)
    {
        // 填充位图信息头
        var bmi = new BitmapInfoHeader
        {
            biSize = Marshal.SizeOf<BitmapInfoHeader>(),
            biWidth = width,
            biHeight = -height, // 负值 = 自顶向下，行序与像素数组一致
            biPlanes = 1,
            biBitCount = 32,    // 32位色深
            biCompression = 0   // BI_RGB，不压缩
        };
        // 创建DIB Section，pixels 将获得指向实际像素数据的指针
        return CreateDIBSection(IntPtr.Zero, ref bmi, 0, out pixels, IntPtr.Zero, 0);
    }

    /// <summary>使用 UpdateLayeredWindow 将 ARGB 位图渲染到 Layered Window</summary>
    public static void RenderLayeredWindow(IntPtr hwnd, IntPtr hBitmap, IntPtr pixels, int width, int height)
    {
        // 创建一个与屏幕兼容的内存设备上下文
        var hdcSrc = CreateCompatibleDC(IntPtr.Zero);
        // 将位图选入内存DC，并保存之前的对象以便恢复
        var oldBmp = SelectObject(hdcSrc, hBitmap);

        // 设置目标窗口位置和大小参数（通常为原点，因为窗口位置由SetWindowPos等单独控制）
        var ptDst = new Point { X = 0, Y = 0 };
        var size = new Size { cx = width, cy = height };
        // 设置源图像在内存DC中的起始点
        var ptSrc = new Point { X = 0, Y = 0 };
        // 设置混合函数，用于Alpha混合渲染
        var blend = new BlendFunction
        {
            BlendOp = 0, // AC_SRC_OVER
            BlendFlags = 0,
            SourceConstantAlpha = 255, // 源图像的总体不透明度（255为不透明）
            AlphaFormat = 1 // AC_SRC_ALPHA，表示位图本身包含Alpha通道
        };

        // 执行分层窗口更新，将内存DC中的位图内容渲染到指定窗口
// 调用UpdateLayeredWindow函数，以Alpha混合方式更新分层窗口的外观
        UpdateLayeredWindow(hwnd, IntPtr.Zero, ref ptDst, ref size, hdcSrc, ref ptSrc, 0, ref blend, UlwAlpha);

        // 恢复内存DC的原始位图对象，并清理资源
        SelectObject(hdcSrc, oldBmp);
        DeleteDC(hdcSrc);
    }

    /// <summary>将窗口设置为 Layered Window（WS_EX_LAYERED）</summary>
    public static void SetLayered(IntPtr hwnd)
    {
        // 获取窗口当前的扩展样式值
        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        // 添加分层窗口样式以及其他用于透明覆盖的样式
        style |= WsExLayered | WsExTransparent | WsExToolWindow | WsExNoActivate;
        // 将修改后的样式值设置回窗口
        _ = SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style));
    }

    /// <summary>销毁 GDI 对象（如位图、画刷、字体等）以释放系统资源</summary>
    public static void DestroyGdiObject(IntPtr hObject)
    {
        // 仅当对象句柄有效时才尝试删除
        if (hObject != IntPtr.Zero)
            DeleteObject(hObject);
    }

    #endregion
}
