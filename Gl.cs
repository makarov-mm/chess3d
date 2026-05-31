using System.Runtime.InteropServices;

namespace Chess3D;

internal static class Gl
{
    public const uint GL_COLOR_BUFFER_BIT = 0x00004000;
    public const uint GL_DEPTH_BUFFER_BIT = 0x00000100;
    public const uint GL_TRIANGLES = 0x0004;
    public const uint GL_FLOAT = 0x1406;
    public const uint GL_ARRAY_BUFFER = 0x8892;
    public const uint GL_STATIC_DRAW = 0x88E4;
    public const uint GL_VERTEX_SHADER = 0x8B31;
    public const uint GL_FRAGMENT_SHADER = 0x8B30;
    public const uint GL_COMPILE_STATUS = 0x8B81;
    public const uint GL_LINK_STATUS = 0x8B82;
    public const uint GL_INFO_LOG_LENGTH = 0x8B84;
    public const uint GL_DEPTH_TEST = 0x0B71;
    public const uint GL_CULL_FACE = 0x0B44;
    public const uint GL_BACK = 0x0405;
    public const uint GL_BLEND = 0x0BE2;
    public const uint GL_SRC_ALPHA = 0x0302;
    public const uint GL_ONE_MINUS_SRC_ALPHA = 0x0303;
    public const uint GL_LEQUAL = 0x0203;
    public const uint GL_VERSION = 0x1F02;
    public const uint GL_MULTISAMPLE = 0x809D;

    [DllImport("opengl32.dll")] private static extern IntPtr glGetString(uint name);
    [DllImport("opengl32.dll")] private static extern void glViewport(int x, int y, int width, int height);
    [DllImport("opengl32.dll")] private static extern void glClearColor(float red, float green, float blue, float alpha);
    [DllImport("opengl32.dll")] private static extern void glClear(uint mask);
    [DllImport("opengl32.dll")] private static extern void glEnable(uint cap);
    [DllImport("opengl32.dll")] private static extern void glDisable(uint cap);
    [DllImport("opengl32.dll")] private static extern void glCullFace(uint mode);
    [DllImport("opengl32.dll")] private static extern void glBlendFunc(uint sfactor, uint dfactor);
    [DllImport("opengl32.dll")] private static extern void glDepthFunc(uint func);
    [DllImport("opengl32.dll")] private static extern void glDepthMask(byte flag);
    [DllImport("opengl32.dll")] private static extern void glDrawArrays(uint mode, int first, int count);

    private static GlCreateShader _createShader = null!;
    private static GlShaderSource _shaderSource = null!;
    private static GlCompileShader _compileShader = null!;
    private static GlGetShaderiv _getShaderiv = null!;
    private static GlGetShaderInfoLog _getShaderInfoLog = null!;
    private static GlCreateProgram _createProgram = null!;
    private static GlAttachShader _attachShader = null!;
    private static GlLinkProgram _linkProgram = null!;
    private static GlGetProgramiv _getProgramiv = null!;
    private static GlGetProgramInfoLog _getProgramInfoLog = null!;
    private static GlUseProgram _useProgram = null!;
    private static GlDeleteShader _deleteShader = null!;
    private static GlGetUniformLocation _getUniformLocation = null!;
    private static GlUniformMatrix4fv _uniformMatrix4fv = null!;
    private static GlUniform3f _uniform3f = null!;
    private static GlUniform1f _uniform1f = null!;
    private static GlUniform1i _uniform1i = null!;
    private static GlGenVertexArrays _genVertexArrays = null!;
    private static GlBindVertexArray _bindVertexArray = null!;
    private static GlGenBuffers _genBuffers = null!;
    private static GlBindBuffer _bindBuffer = null!;
    private static GlBufferData _bufferData = null!;
    private static GlEnableVertexAttribArray _enableVertexAttribArray = null!;
    private static GlVertexAttribPointer _vertexAttribPointer = null!;
    private static GlDeleteVertexArrays _deleteVertexArrays = null!;
    private static GlDeleteBuffers _deleteBuffers = null!;

    public static string GetString(uint name)
    {
        IntPtr ptr = glGetString(name);
        return ptr == IntPtr.Zero ? "unknown" : Marshal.PtrToStringAnsi(ptr) ?? "unknown";
    }

    public static void Viewport(int x, int y, int width, int height) => glViewport(x, y, width, height);
    public static void ClearColor(float r, float g, float b, float a) => glClearColor(r, g, b, a);
    public static void Clear(uint mask) => glClear(mask);
    public static void Enable(uint cap) => glEnable(cap);
    public static void Disable(uint cap) => glDisable(cap);
    public static void CullFace(uint mode) => glCullFace(mode);
    public static void BlendFunc(uint sfactor, uint dfactor) => glBlendFunc(sfactor, dfactor);
    public static void DepthFunc(uint func) => glDepthFunc(func);
    public static void DepthMask(bool flag) => glDepthMask(flag ? (byte)1 : (byte)0);
    public static void DrawArrays(uint mode, int first, int count) => glDrawArrays(mode, first, count);

    public static uint CreateShader(uint type) => _createShader(type);
    public static void ShaderSource(uint shader, string source)
    {
        IntPtr sourcePtr = Marshal.StringToHGlobalAnsi(source);
        IntPtr strings = Marshal.AllocHGlobal(IntPtr.Size);

        try
        {
            Marshal.WriteIntPtr(strings, sourcePtr);
            _shaderSource(shader, 1, strings, IntPtr.Zero);
        }
        finally
        {
            Marshal.FreeHGlobal(strings);
            Marshal.FreeHGlobal(sourcePtr);
        }
    }
    public static void CompileShader(uint shader) => _compileShader(shader);
    public static void GetShaderiv(uint shader, uint pname, out int param) => _getShaderiv(shader, pname, out param);
    public static void GetShaderInfoLog(uint shader, int maxLength, out int length, IntPtr infoLog) => _getShaderInfoLog(shader, maxLength, out length, infoLog);
    public static uint CreateProgram() => _createProgram();
    public static void AttachShader(uint program, uint shader) => _attachShader(program, shader);
    public static void LinkProgram(uint program) => _linkProgram(program);
    public static void GetProgramiv(uint program, uint pname, out int param) => _getProgramiv(program, pname, out param);
    public static void GetProgramInfoLog(uint program, int maxLength, out int length, IntPtr infoLog) => _getProgramInfoLog(program, maxLength, out length, infoLog);
    public static void UseProgram(uint program) => _useProgram(program);
    public static void DeleteShader(uint shader) => _deleteShader(shader);
    public static void DeleteVertexArrays(int n, ref uint arrays) => _deleteVertexArrays(n, ref arrays);
    public static void DeleteBuffers(int n, ref uint buffers) => _deleteBuffers(n, ref buffers);
    public static int GetUniformLocation(uint program, string name) => _getUniformLocation(program, name);
    public static void Uniform3f(int location, float x, float y, float z) { if (location >= 0) _uniform3f(location, x, y, z); }
    public static void Uniform1f(int location, float x) { if (location >= 0) _uniform1f(location, x); }
    public static void Uniform1i(int location, int x) { if (location >= 0) _uniform1i(location, x); }
    public static void GenVertexArrays(int n, out uint arrays) => _genVertexArrays(n, out arrays);
    public static void BindVertexArray(uint array) => _bindVertexArray(array);
    public static void GenBuffers(int n, out uint buffers) => _genBuffers(n, out buffers);
    public static void BindBuffer(uint target, uint buffer) => _bindBuffer(target, buffer);
    public static void EnableVertexAttribArray(uint index) => _enableVertexAttribArray(index);
    public static void VertexAttribPointer(uint index, int size, uint type, bool normalized, int stride, IntPtr pointer) => _vertexAttribPointer(index, size, type, normalized ? (byte)1 : (byte)0, stride, pointer);

    public static void UniformMatrix4fv(int location, Matrix4 matrix)
    {
        if (location < 0) return;

        var handle = GCHandle.Alloc(matrix.M, GCHandleType.Pinned);
        try
        {
            _uniformMatrix4fv(location, 1, 0, handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    public static void BufferData(uint target, float[] data, uint usage)
    {
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            _bufferData(target, new IntPtr(data.Length * sizeof(float)), handle.AddrOfPinnedObject(), usage);
        }
        finally
        {
            handle.Free();
        }
    }

    public static void LoadFunctions()
    {
        _createShader = Load<GlCreateShader>("glCreateShader");
        _shaderSource = Load<GlShaderSource>("glShaderSource");
        _compileShader = Load<GlCompileShader>("glCompileShader");
        _getShaderiv = Load<GlGetShaderiv>("glGetShaderiv");
        _getShaderInfoLog = Load<GlGetShaderInfoLog>("glGetShaderInfoLog");
        _createProgram = Load<GlCreateProgram>("glCreateProgram");
        _attachShader = Load<GlAttachShader>("glAttachShader");
        _linkProgram = Load<GlLinkProgram>("glLinkProgram");
        _getProgramiv = Load<GlGetProgramiv>("glGetProgramiv");
        _getProgramInfoLog = Load<GlGetProgramInfoLog>("glGetProgramInfoLog");
        _useProgram = Load<GlUseProgram>("glUseProgram");
        _deleteShader = Load<GlDeleteShader>("glDeleteShader");
        _deleteVertexArrays = Load<GlDeleteVertexArrays>("glDeleteVertexArrays");
        _deleteBuffers = Load<GlDeleteBuffers>("glDeleteBuffers");
        _getUniformLocation = Load<GlGetUniformLocation>("glGetUniformLocation");
        _uniformMatrix4fv = Load<GlUniformMatrix4fv>("glUniformMatrix4fv");
        _uniform3f = Load<GlUniform3f>("glUniform3f");
        _uniform1f = Load<GlUniform1f>("glUniform1f");
        _uniform1i = Load<GlUniform1i>("glUniform1i");
        _genVertexArrays = Load<GlGenVertexArrays>("glGenVertexArrays");
        _bindVertexArray = Load<GlBindVertexArray>("glBindVertexArray");
        _genBuffers = Load<GlGenBuffers>("glGenBuffers");
        _bindBuffer = Load<GlBindBuffer>("glBindBuffer");
        _bufferData = Load<GlBufferData>("glBufferData");
        _enableVertexAttribArray = Load<GlEnableVertexAttribArray>("glEnableVertexAttribArray");
        _vertexAttribPointer = Load<GlVertexAttribPointer>("glVertexAttribPointer");
    }

    private static T Load<T>(string name) where T : Delegate
    {
        IntPtr ptr = Wgl.wglGetProcAddress(name);
        long value = ptr.ToInt64();

        if (ptr == IntPtr.Zero || value == 1 || value == 2 || value == 3 || value == -1)
        {
            IntPtr module = Wgl.LoadLibrary("opengl32.dll");
            ptr = Wgl.GetProcAddress(module, name);
        }

        if (ptr == IntPtr.Zero)
            throw new InvalidOperationException($"OpenGL function not found: {name}");

        return Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate uint GlCreateShader(uint type);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlShaderSource(uint shader, int count, IntPtr strings, IntPtr lengths);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlCompileShader(uint shader);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlGetShaderiv(uint shader, uint pname, out int param);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlGetShaderInfoLog(uint shader, int maxLength, out int length, IntPtr infoLog);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate uint GlCreateProgram();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlAttachShader(uint program, uint shader);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlLinkProgram(uint program);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlGetProgramiv(uint program, uint pname, out int param);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlGetProgramInfoLog(uint program, int maxLength, out int length, IntPtr infoLog);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlUseProgram(uint program);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlDeleteShader(uint shader);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlDeleteVertexArrays(int n, ref uint arrays);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlDeleteBuffers(int n, ref uint buffers);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int GlGetUniformLocation(uint program, [MarshalAs(UnmanagedType.LPStr)] string name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlUniformMatrix4fv(int location, int count, byte transpose, IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlUniform3f(int location, float v0, float v1, float v2);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlUniform1f(int location, float v0);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlUniform1i(int location, int v0);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlGenVertexArrays(int n, out uint arrays);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlBindVertexArray(uint array);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlGenBuffers(int n, out uint buffers);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlBindBuffer(uint target, uint buffer);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlBufferData(uint target, IntPtr size, IntPtr data, uint usage);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlEnableVertexAttribArray(uint index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GlVertexAttribPointer(uint index, int size, uint type, byte normalized, int stride, IntPtr pointer);
}
