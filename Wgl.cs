using System.Runtime.InteropServices;

namespace Chess3D;

/// <summary>The Win32, GDI and WGL calls that put an OpenGL 4 context on a window.</summary>
internal static unsafe partial class Wgl
{
    private const uint PfdDoubleBuffer = 0x01, PfdDrawToWindow = 0x04, PfdSupportOpenGl = 0x20;

    // WGL_ARB_pixel_format, WGL_ARB_multisample
    private const int DrawToWindowArb = 0x2001, SupportOpenGlArb = 0x2010, DoubleBufferArb = 0x2011;
    private const int PixelTypeArb = 0x2013, TypeRgbaArb = 0x202B, ColorBitsArb = 0x2014;
    private const int DepthBitsArb = 0x2022, StencilBitsArb = 0x2023, SampleBuffersArb = 0x2041, SamplesArb = 0x2042;

    // WGL_ARB_create_context, WGL_ARB_create_context_profile
    private const int ContextMajorVersionArb = 0x2091, ContextMinorVersionArb = 0x2092;
    private const int ContextProfileMaskArb = 0x9126, ContextCoreProfileBitArb = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    private struct PixelFormatDescriptor
    {
        public ushort Size;
        public ushort Version;
        public uint Flags;
        public byte PixelType;
        public byte ColorBits;
        public byte RedBits, RedShift, GreenBits, GreenShift, BlueBits, BlueShift;
        public byte AlphaBits, AlphaShift;
        public byte AccumBits, AccumRedBits, AccumGreenBits, AccumBlueBits, AccumAlphaBits;
        public byte DepthBits, StencilBits, AuxBuffers;
        public byte LayerType, Reserved;
        public uint LayerMask, VisibleMask, DamageMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public nint Window;
        public uint Id;
        public nint WParam, LParam;
        public uint Time;
        public int X, Y;
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint window);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint window, nint dc);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateWindowEx(uint exStyle, string className, string? windowName, uint style,
                                               int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint window);

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PeekMessage(out Message message, nint window, uint filterMin, uint filterMax, uint remove);

    [LibraryImport("gdi32.dll")]
    private static partial int ChoosePixelFormat(nint dc, in PixelFormatDescriptor descriptor);

    [LibraryImport("gdi32.dll")]
    private static partial int DescribePixelFormat(nint dc, int format, uint size, out PixelFormatDescriptor descriptor);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetPixelFormat(nint dc, int format, in PixelFormatDescriptor descriptor);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SwapBuffers(nint dc);

    [LibraryImport("opengl32.dll")]
    private static partial nint wglCreateContext(nint dc);

    [LibraryImport("opengl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool wglMakeCurrent(nint dc, nint context);

    [LibraryImport("opengl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool wglDeleteContext(nint context);

    [LibraryImport("opengl32.dll", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint wglGetProcAddress(string name);

    private static delegate* unmanaged[Stdcall]<nint, int*, float*, uint, int*, uint*, int> choosePixelFormatArb;
    private static delegate* unmanaged[Stdcall]<nint, nint, int*, nint> createContextAttribsArb;
    private static bool extensionsLoaded;

    /// <summary>
    /// Gives the window a double-buffered pixel format, with 8x or 4x multisampling where the driver
    /// has it, and makes an OpenGL 4.0 core context current on it, with vertical sync where the driver allows.
    /// </summary>
    public static OpenGlContext CreateContext(nint window)
    {
        LoadExtensions();

        nint dc = GetDC(window);
        if (dc == 0)
            throw new InvalidOperationException("The window has no device context.");

        nint context = 0;
        try
        {
            int format = ChooseMultisampleFormat(dc, 8);
            if (format == 0)
                format = ChooseMultisampleFormat(dc, 4);
            if (format == 0)
                format = ChoosePixelFormat(dc, BasicFormat());
            if (format == 0 || DescribePixelFormat(dc, format, (uint)sizeof(PixelFormatDescriptor), out var descriptor) == 0 ||
                !SetPixelFormat(dc, format, descriptor))
            {
                throw new InvalidOperationException("No OpenGL pixel format is available for the window.");
            }

            if (createContextAttribsArb != null)
            {
                int* attributes = stackalloc int[]
                {
                    ContextMajorVersionArb, 4,
                    ContextMinorVersionArb, 0,
                    ContextProfileMaskArb, ContextCoreProfileBitArb,
                    0
                };
                context = createContextAttribsArb(dc, 0, attributes);
            }
            else
            {
                context = wglCreateContext(dc); // the driver's default version; the shaders tell if it is too old
            }

            if (context == 0 || !wglMakeCurrent(dc, context))
                throw new InvalidOperationException("OpenGL 4.0 is not available. Update the graphics driver.");

            // Vertical sync: swapping the buffers waits for the screen refresh.
            var swapInterval = (delegate* unmanaged[Stdcall]<int, int>)Resolve("wglSwapIntervalEXT");
            if (swapInterval != null)
                swapInterval(1);
            return new OpenGlContext(window, dc, context);
        }
        catch
        {
            if (context != 0)
            {
                wglMakeCurrent(0, 0);
                wglDeleteContext(context);
            }
            _ = ReleaseDC(window, dc);
            throw;
        }
    }

    /// <summary>Whether no message waits in the thread's queue.</summary>
    public static bool IsMessageQueueEmpty() => !PeekMessage(out _, 0, 0, 0, 0);

    /// <summary>Resolves an OpenGL or WGL extension function of the current context; null if there is none.</summary>
    public static void* Resolve(string name)
    {
        nint address = wglGetProcAddress(name);
        // Some drivers return small values instead of null for the functions they lack.
        return address is 0 or 1 or 2 or 3 or -1 ? null : (void*)address;
    }

    private static PixelFormatDescriptor BasicFormat() => new()
    {
        Size = (ushort)sizeof(PixelFormatDescriptor),
        Version = 1,
        Flags = PfdDrawToWindow | PfdSupportOpenGl | PfdDoubleBuffer,
        ColorBits = 32,
        DepthBits = 24,
        StencilBits = 8
    };

    private static int ChooseMultisampleFormat(nint dc, int samples)
    {
        if (choosePixelFormatArb == null)
            return 0;

        int* attributes = stackalloc int[]
        {
            DrawToWindowArb, 1,
            SupportOpenGlArb, 1,
            DoubleBufferArb, 1,
            PixelTypeArb, TypeRgbaArb,
            ColorBitsArb, 32,
            DepthBitsArb, 24,
            StencilBitsArb, 8,
            SampleBuffersArb, 1,
            SamplesArb, samples,
            0
        };

        int format;
        uint count;
        return choosePixelFormatArb(dc, attributes, null, 1, &format, &count) != 0 && count > 0 ? format : 0;
    }

    // The functions that choose a multisampled format and create a core context are extensions, found
    // through a current context; and a window keeps the first pixel format it gets, so the context for
    // looking them up lives on a throwaway hidden window.
    private static void LoadExtensions()
    {
        if (extensionsLoaded)
            return;
        extensionsLoaded = true;

        nint window = CreateWindowEx(0, "STATIC", null, 0, 0, 0, 1, 1, 0, 0, 0, 0);
        if (window == 0)
            return;

        nint dc = GetDC(window);
        try
        {
            PixelFormatDescriptor descriptor = BasicFormat();
            int format = ChoosePixelFormat(dc, descriptor);
            if (format == 0 || !SetPixelFormat(dc, format, descriptor))
                return;

            nint context = wglCreateContext(dc);
            if (context == 0)
                return;

            if (wglMakeCurrent(dc, context))
            {
                choosePixelFormatArb = (delegate* unmanaged[Stdcall]<nint, int*, float*, uint, int*, uint*, int>)Resolve("wglChoosePixelFormatARB");
                createContextAttribsArb = (delegate* unmanaged[Stdcall]<nint, nint, int*, nint>)Resolve("wglCreateContextAttribsARB");
                wglMakeCurrent(0, 0);
            }
            wglDeleteContext(context);
        }
        finally
        {
            _ = ReleaseDC(window, dc);
            DestroyWindow(window);
        }
    }

    /// <summary>An OpenGL context current on a window's device context.</summary>
    public sealed class OpenGlContext(nint window, nint dc, nint context) : IDisposable
    {
        private nint _context = context;

        public void SwapBuffers() => Wgl.SwapBuffers(dc);

        public void Dispose()
        {
            if (_context == 0)
                return;
            wglMakeCurrent(0, 0);
            wglDeleteContext(_context);
            _ = ReleaseDC(window, dc);
            _context = 0;
        }
    }
}
