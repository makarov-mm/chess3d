using System.Runtime.InteropServices;

namespace Chess3D;

internal static class Wgl
{
    public const uint PFD_DOUBLEBUFFER = 0x00000001;
    public const uint PFD_DRAW_TO_WINDOW = 0x00000004;
    public const uint PFD_SUPPORT_OPENGL = 0x00000020;
    public const byte PFD_TYPE_RGBA = 0;
    public const byte PFD_MAIN_PLANE = 0;
    public const int WGL_DRAW_TO_WINDOW_ARB = 0x2001;
    public const int WGL_SUPPORT_OPENGL_ARB = 0x2010;
    public const int WGL_DOUBLE_BUFFER_ARB = 0x2011;
    public const int WGL_PIXEL_TYPE_ARB = 0x2013;
    public const int WGL_TYPE_RGBA_ARB = 0x202B;
    public const int WGL_COLOR_BITS_ARB = 0x2014;
    public const int WGL_DEPTH_BITS_ARB = 0x2022;
    public const int WGL_STENCIL_BITS_ARB = 0x2023;
    public const int WGL_SAMPLE_BUFFERS_ARB = 0x2041;
    public const int WGL_SAMPLES_ARB = 0x2042;

    [StructLayout(LayoutKind.Sequential)]
    public struct PIXELFORMATDESCRIPTOR
    {
        public ushort nSize;
        public ushort nVersion;
        public uint dwFlags;
        public byte iPixelType;
        public byte cColorBits;
        public byte cRedBits;
        public byte cRedShift;
        public byte cGreenBits;
        public byte cGreenShift;
        public byte cBlueBits;
        public byte cBlueShift;
        public byte cAlphaBits;
        public byte cAlphaShift;
        public byte cAccumBits;
        public byte cAccumRedBits;
        public byte cAccumGreenBits;
        public byte cAccumBlueBits;
        public byte cAccumAlphaBits;
        public byte cDepthBits;
        public byte cStencilBits;
        public byte cAuxBuffers;
        public byte iLayerType;
        public byte bReserved;
        public uint dwLayerMask;
        public uint dwVisibleMask;
        public uint dwDamageMask;
    }

    [DllImport("gdi32.dll", CallingConvention = CallingConvention.Winapi)]
    public static extern int DescribePixelFormat(
        IntPtr hdc,
        int iPixelFormat,
        uint nBytes,
        ref PIXELFORMATDESCRIPTOR ppfd);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool WglChoosePixelFormatARB(
        IntPtr hdc,
        int[] piAttribIList,
        IntPtr pfAttribFList,
        uint nMaxFormats,
        int[] piFormats,
        out uint nNumFormats);

    private static WglChoosePixelFormatARB? _choosePixelFormatARB;

    [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

    [DllImport("gdi32.dll", CallingConvention = CallingConvention.Winapi)]
    public static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR ppfd);

    [DllImport("gdi32.dll", CallingConvention = CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetPixelFormat(IntPtr hdc, int format, ref PIXELFORMATDESCRIPTOR ppfd);

    [DllImport("gdi32.dll", CallingConvention = CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SwapBuffers(IntPtr hdc);

    [DllImport("opengl32.dll", CallingConvention = CallingConvention.Winapi)]
    public static extern IntPtr wglCreateContext(IntPtr hdc);

    [DllImport("opengl32.dll", CallingConvention = CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);

    [DllImport("opengl32.dll", CallingConvention = CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool wglDeleteContext(IntPtr hglrc);

    [DllImport("opengl32.dll", CallingConvention = CallingConvention.Winapi)]
    public static extern IntPtr wglGetProcAddress([MarshalAs(UnmanagedType.LPStr)] string lpszProc);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    public static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    public static void LoadWglExtensions()
    {
        if (_choosePixelFormatARB is not null)
            return;

        using Form dummy = new()
        {
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.FixedToolWindow,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            Size = new Size(1, 1)
        };

        dummy.Show();

        IntPtr dc = GetDC(dummy.Handle);

        var pfd = new PIXELFORMATDESCRIPTOR
        {
            nSize = (ushort)Marshal.SizeOf<PIXELFORMATDESCRIPTOR>(),
            nVersion = 1,
            dwFlags = PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER,
            iPixelType = PFD_TYPE_RGBA,
            cColorBits = 32,
            cDepthBits = 24,
            cStencilBits = 8,
            iLayerType = PFD_MAIN_PLANE
        };

        int pf = ChoosePixelFormat(dc, ref pfd);
        SetPixelFormat(dc, pf, ref pfd);

        IntPtr rc = wglCreateContext(dc);
        wglMakeCurrent(dc, rc);

        IntPtr proc = wglGetProcAddress("wglChoosePixelFormatARB");
        if (proc != IntPtr.Zero)
        {
            _choosePixelFormatARB =
                Marshal.GetDelegateForFunctionPointer<WglChoosePixelFormatARB>(proc);
        }

        wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
        wglDeleteContext(rc);
        ReleaseDC(dummy.Handle, dc);
    }

    public static int ChooseMultisamplePixelFormat(IntPtr hdc, int samples)
    {
        LoadWglExtensions();

        if (_choosePixelFormatARB is null)
            return 0;

        int[] attributes =
        {
            WGL_DRAW_TO_WINDOW_ARB, 1,
            WGL_SUPPORT_OPENGL_ARB, 1,
            WGL_DOUBLE_BUFFER_ARB, 1,
            WGL_PIXEL_TYPE_ARB, WGL_TYPE_RGBA_ARB,
            WGL_COLOR_BITS_ARB, 32,
            WGL_DEPTH_BITS_ARB, 24,
            WGL_STENCIL_BITS_ARB, 8,
            WGL_SAMPLE_BUFFERS_ARB, 1,
            WGL_SAMPLES_ARB, samples,
            0
        };

        int[] formats = new int[1];

        bool ok = _choosePixelFormatARB(
            hdc,
            attributes,
            IntPtr.Zero,
            1,
            formats,
            out uint count);

        if (!ok || count == 0)
            return 0;

        return formats[0];
    }
}
