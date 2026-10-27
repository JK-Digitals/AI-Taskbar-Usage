#nullable enable
using System.Runtime.InteropServices;
using System.Text;

namespace AiTaskbarUsage;

internal static class Win32
{
    public static readonly IntPtr HwndTop = IntPtr.Zero;
    public static readonly IntPtr HwndTopmost = new(-1);
    public const uint SwpNoSize = 0x0001;
    public const uint SwpNoMove = 0x0002;
    public const uint SwpNoActivate = 0x0010;
    public const uint SwpFrameChanged = 0x0020;
    public const uint SwpShowWindow = 0x0040;
    public const uint SwpNoOwnerZOrder = 0x0200;
    public const uint SwpNoSendChanging = 0x0400;
    public const int SwHide = 0;
    public const int SwShowNoActivate = 4;
    public const uint MonitorDefaultToNearest = 2;
    public const int GwlStyle = -16;
    public const int GwlExstyle = -20;
    public const long WsPopup = 0x80000000L;
    public const long WsChild = 0x40000000L;
    public const long WsVisible = 0x10000000L;
    public const long WsClipChildren = 0x02000000L;
    public const long WsClipSiblings = 0x04000000L;
    public const long WsExNoActivate = 0x08000000L;
    public const long WsExToolWindow = 0x00000080L;
    public const long WsExLayered = 0x00080000L;
    public const uint UlwAlpha = 0x00000002;
    public const byte AcSrcOver = 0;
    public const byte AcSrcAlpha = 1;
    public const uint DibRgbColors = 0;
    public const uint BiRgb = 0;
    public const uint WmLButtonUp = 0x0202;
    public const uint WmRButtonUp = 0x0205;
    public const uint WmPaint = 0x000F;
    public const uint WmEraseBackground = 0x0014;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string? className, string? windowTitle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr handle, out Rect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr handle);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr handle, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetParent(IntPtr childHandle, IntPtr newParentHandle);

    [DllImport("user32.dll")]
    public static extern IntPtr GetParent(IntPtr handle);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr handle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool InvalidateRect(IntPtr handle, IntPtr rect, [MarshalAs(UnmanagedType.Bool)] bool erase);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UpdateWindow(IntPtr handle);

    [DllImport("user32.dll")]
    public static extern IntPtr BeginPaint(IntPtr handle, out PaintStruct paint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EndPaint(IntPtr handle, ref PaintStruct paint);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr handle);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr handle, IntPtr deviceContext);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UpdateLayeredWindow(
        IntPtr handle,
        IntPtr destinationDc,
        IntPtr destinationPoint,
        ref Size size,
        IntPtr sourceDc,
        ref Point sourcePoint,
        uint colorKey,
        ref BlendFunction blend,
        uint flags);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr CreateDIBSection(
        IntPtr deviceContext,
        ref BitmapInfo bitmapInfo,
        uint usage,
        out IntPtr bits,
        IntPtr section,
        uint offset);

    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr gdiObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr gdiObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteDC(IntPtr deviceContext);

    [DllImport("gdi32.dll")]
    public static extern int SetDIBitsToDevice(
        IntPtr deviceContext,
        int destinationX,
        int destinationY,
        uint width,
        uint height,
        int sourceX,
        int sourceY,
        uint startScan,
        uint scanLines,
        byte[] bits,
        ref BitmapInfo bitmapInfo,
        uint colorUse);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr handle, int index, IntPtr value);

    public static IntPtr GetWindowLongPtr(IntPtr handle, int index) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(handle, index) : new IntPtr(GetWindowLong32(handle, index));

    public static IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr value) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(handle, index, value) : new IntPtr(SetWindowLong32(handle, index, value.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr handle, int index, int value);

    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr monitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);

    public delegate bool EnumWindowsProc(IntPtr handle, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public bool IsEmpty => Width <= 0 || Height <= 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Size
    {
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RgbQuad
    {
        public byte Blue;
        public byte Green;
        public byte Red;
        public byte Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public RgbQuad Colors;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PaintStruct
    {
        public IntPtr DeviceContext;
        [MarshalAs(UnmanagedType.Bool)] public bool Erase;
        public Rect PaintRect;
        [MarshalAs(UnmanagedType.Bool)] public bool Restore;
        [MarshalAs(UnmanagedType.Bool)] public bool IncrementalUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MonitorInfo
    {
        public int CbSize;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    public enum MonitorDpiType
    {
        Effective = 0,
        Angular = 1,
        Raw = 2
    }
}
