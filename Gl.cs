using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Chess3D;

/// <summary>
/// The OpenGL calls the game makes. The OpenGL 1.1 ones are exports of opengl32.dll; the newer ones are
/// looked up once a context is current and called through function pointers.
/// </summary>
internal static unsafe partial class Gl
{
    public const uint DepthBufferBit = 0x0100, ColorBufferBit = 0x4000;
    public const uint Triangles = 0x0004;
    public const uint Float = 0x1406;
    public const uint DepthTest = 0x0B71, CullFace = 0x0B44, Blend = 0x0BE2, Multisample = 0x809D;
    public const uint Back = 0x0405, LessOrEqual = 0x0203;
    public const uint SrcAlpha = 0x0302, OneMinusSrcAlpha = 0x0303;
    public const uint ArrayBuffer = 0x8892, StaticDraw = 0x88E4;
    public const uint FragmentShader = 0x8B30, VertexShader = 0x8B31;
    public const uint CompileStatus = 0x8B81, LinkStatus = 0x8B82;

    [LibraryImport("opengl32.dll", EntryPoint = "glViewport")] public static partial void Viewport(int x, int y, int width, int height);
    [LibraryImport("opengl32.dll", EntryPoint = "glClearColor")] public static partial void ClearColor(float red, float green, float blue, float alpha);
    [LibraryImport("opengl32.dll", EntryPoint = "glClear")] public static partial void Clear(uint mask);
    [LibraryImport("opengl32.dll", EntryPoint = "glEnable")] public static partial void Enable(uint capability);
    [LibraryImport("opengl32.dll", EntryPoint = "glDisable")] public static partial void Disable(uint capability);
    [LibraryImport("opengl32.dll", EntryPoint = "glCullFace")] public static partial void CullFaces(uint mode);
    [LibraryImport("opengl32.dll", EntryPoint = "glBlendFunc")] public static partial void BlendFunc(uint source, uint destination);
    [LibraryImport("opengl32.dll", EntryPoint = "glDepthFunc")] public static partial void DepthFunc(uint function);
    [LibraryImport("opengl32.dll", EntryPoint = "glDepthMask")] private static partial void glDepthMask(byte flag);
    [LibraryImport("opengl32.dll", EntryPoint = "glDrawArrays")] public static partial void DrawArrays(uint mode, int first, int count);

    private static delegate* unmanaged[Stdcall]<uint, uint> createShader;
    private static delegate* unmanaged[Stdcall]<uint, int, byte**, int*, void> shaderSource;
    private static delegate* unmanaged[Stdcall]<uint, void> compileShader;
    private static delegate* unmanaged[Stdcall]<uint, uint, int*, void> getShaderiv;
    private static delegate* unmanaged[Stdcall]<uint, int, int*, byte*, void> getShaderInfoLog;
    private static delegate* unmanaged[Stdcall]<uint, void> deleteShader;
    private static delegate* unmanaged[Stdcall]<uint> createProgram;
    private static delegate* unmanaged[Stdcall]<uint, uint, void> attachShader;
    private static delegate* unmanaged[Stdcall]<uint, void> linkProgram;
    private static delegate* unmanaged[Stdcall]<uint, uint, int*, void> getProgramiv;
    private static delegate* unmanaged[Stdcall]<uint, int, int*, byte*, void> getProgramInfoLog;
    private static delegate* unmanaged[Stdcall]<uint, void> useProgram;
    private static delegate* unmanaged[Stdcall]<uint, void> deleteProgram;
    private static delegate* unmanaged[Stdcall]<uint, byte*, int> getUniformLocation;
    private static delegate* unmanaged[Stdcall]<int, int, void> uniform1i;
    private static delegate* unmanaged[Stdcall]<int, float, void> uniform1f;
    private static delegate* unmanaged[Stdcall]<int, float, float, float, void> uniform3f;
    private static delegate* unmanaged[Stdcall]<int, int, byte, float*, void> uniformMatrix4fv;
    private static delegate* unmanaged[Stdcall]<int, uint*, void> genVertexArrays;
    private static delegate* unmanaged[Stdcall]<int, uint*, void> deleteVertexArrays;
    private static delegate* unmanaged[Stdcall]<uint, void> bindVertexArray;
    private static delegate* unmanaged[Stdcall]<int, uint*, void> genBuffers;
    private static delegate* unmanaged[Stdcall]<int, uint*, void> deleteBuffers;
    private static delegate* unmanaged[Stdcall]<uint, uint, void> bindBuffer;
    private static delegate* unmanaged[Stdcall]<uint, nint, void*, uint, void> bufferData;
    private static delegate* unmanaged[Stdcall]<uint, int, uint, byte, int, nint, void> vertexAttribPointer;
    private static delegate* unmanaged[Stdcall]<uint, void> enableVertexAttribArray;

    /// <summary>Looks up the functions newer than OpenGL 1.1; a context must be current.</summary>
    public static void Load()
    {
        createShader = (delegate* unmanaged[Stdcall]<uint, uint>)Require("glCreateShader");
        shaderSource = (delegate* unmanaged[Stdcall]<uint, int, byte**, int*, void>)Require("glShaderSource");
        compileShader = (delegate* unmanaged[Stdcall]<uint, void>)Require("glCompileShader");
        getShaderiv = (delegate* unmanaged[Stdcall]<uint, uint, int*, void>)Require("glGetShaderiv");
        getShaderInfoLog = (delegate* unmanaged[Stdcall]<uint, int, int*, byte*, void>)Require("glGetShaderInfoLog");
        deleteShader = (delegate* unmanaged[Stdcall]<uint, void>)Require("glDeleteShader");
        createProgram = (delegate* unmanaged[Stdcall]<uint>)Require("glCreateProgram");
        attachShader = (delegate* unmanaged[Stdcall]<uint, uint, void>)Require("glAttachShader");
        linkProgram = (delegate* unmanaged[Stdcall]<uint, void>)Require("glLinkProgram");
        getProgramiv = (delegate* unmanaged[Stdcall]<uint, uint, int*, void>)Require("glGetProgramiv");
        getProgramInfoLog = (delegate* unmanaged[Stdcall]<uint, int, int*, byte*, void>)Require("glGetProgramInfoLog");
        useProgram = (delegate* unmanaged[Stdcall]<uint, void>)Require("glUseProgram");
        deleteProgram = (delegate* unmanaged[Stdcall]<uint, void>)Require("glDeleteProgram");
        getUniformLocation = (delegate* unmanaged[Stdcall]<uint, byte*, int>)Require("glGetUniformLocation");
        uniform1i = (delegate* unmanaged[Stdcall]<int, int, void>)Require("glUniform1i");
        uniform1f = (delegate* unmanaged[Stdcall]<int, float, void>)Require("glUniform1f");
        uniform3f = (delegate* unmanaged[Stdcall]<int, float, float, float, void>)Require("glUniform3f");
        uniformMatrix4fv = (delegate* unmanaged[Stdcall]<int, int, byte, float*, void>)Require("glUniformMatrix4fv");
        genVertexArrays = (delegate* unmanaged[Stdcall]<int, uint*, void>)Require("glGenVertexArrays");
        deleteVertexArrays = (delegate* unmanaged[Stdcall]<int, uint*, void>)Require("glDeleteVertexArrays");
        bindVertexArray = (delegate* unmanaged[Stdcall]<uint, void>)Require("glBindVertexArray");
        genBuffers = (delegate* unmanaged[Stdcall]<int, uint*, void>)Require("glGenBuffers");
        deleteBuffers = (delegate* unmanaged[Stdcall]<int, uint*, void>)Require("glDeleteBuffers");
        bindBuffer = (delegate* unmanaged[Stdcall]<uint, uint, void>)Require("glBindBuffer");
        bufferData = (delegate* unmanaged[Stdcall]<uint, nint, void*, uint, void>)Require("glBufferData");
        vertexAttribPointer = (delegate* unmanaged[Stdcall]<uint, int, uint, byte, int, nint, void>)Require("glVertexAttribPointer");
        enableVertexAttribArray = (delegate* unmanaged[Stdcall]<uint, void>)Require("glEnableVertexAttribArray");
    }

    private static void* Require(string name) =>
        Wgl.Resolve(name) is var address && address != null
            ? address
            : throw new InvalidOperationException($"The OpenGL driver lacks {name}.");

    public static void DepthMask(bool write) => glDepthMask(write ? (byte)1 : (byte)0);

    // Programs ---------------------------------------------------------------------------------

    public static uint BuildProgram(string vertexSource, string fragmentSource)
    {
        uint vertexShader = CompileShader(VertexShader, vertexSource);
        uint fragmentShader = CompileShader(FragmentShader, fragmentSource);
        uint program = createProgram();
        attachShader(program, vertexShader);
        attachShader(program, fragmentShader);
        linkProgram(program);
        deleteShader(vertexShader);
        deleteShader(fragmentShader);

        int linked;
        getProgramiv(program, LinkStatus, &linked);
        if (linked == 0)
        {
            string log = ReadLog(program, getProgramInfoLog);
            deleteProgram(program);
            throw new InvalidOperationException("The shaders do not link:\n" + log);
        }
        return program;
    }

    private static uint CompileShader(uint type, string source)
    {
        uint shader = createShader(type);
        byte[] text = Encoding.UTF8.GetBytes(source + '\0');
        fixed (byte* start = text)
        {
            byte* lines = start;
            shaderSource(shader, 1, &lines, null);
        }
        compileShader(shader);

        int compiled;
        getShaderiv(shader, CompileStatus, &compiled);
        if (compiled == 0)
        {
            string log = ReadLog(shader, getShaderInfoLog);
            deleteShader(shader);
            string stage = type == VertexShader ? "vertex" : "fragment";
            throw new InvalidOperationException($"The {stage} shader does not compile:\n{log}");
        }
        return shader;
    }

    private static string ReadLog(uint handle, delegate* unmanaged[Stdcall]<uint, int, int*, byte*, void> getInfoLog)
    {
        const int Capacity = 4096;
        byte* log = stackalloc byte[Capacity];
        int length = 0;
        getInfoLog(handle, Capacity, &length, log);
        return Encoding.UTF8.GetString(log, Math.Clamp(length, 0, Capacity)).Trim();
    }

    public static void UseProgram(uint program) => useProgram(program);
    public static void DeleteProgram(uint program) => deleteProgram(program);

    public static int UniformLocation(uint program, string name)
    {
        byte[] text = Encoding.ASCII.GetBytes(name + '\0');
        fixed (byte* start = text)
            return getUniformLocation(program, start);
    }

    // A location of -1 (a uniform the compiler dropped) is ignored by OpenGL itself.
    public static void Uniform(int location, int value) => uniform1i(location, value);
    public static void Uniform(int location, float value) => uniform1f(location, value);
    public static void Uniform(int location, Vector3 value) => uniform3f(location, value.X, value.Y, value.Z);

    /// <summary>
    /// Sets a matrix uniform. System.Numerics matrices multiply row vectors and are stored by rows,
    /// which is the same layout as an OpenGL matrix for column vectors stored by columns.
    /// </summary>
    public static void Uniform(int location, in Matrix4x4 value)
    {
        fixed (float* elements = &value.M11)
            uniformMatrix4fv(location, 1, 0, elements);
    }

    // Vertex arrays and buffers --------------------------------------------------------------------

    public static uint GenVertexArray()
    {
        uint array;
        genVertexArrays(1, &array);
        return array;
    }

    public static void DeleteVertexArray(uint array) => deleteVertexArrays(1, &array);
    public static void BindVertexArray(uint array) => bindVertexArray(array);

    public static uint GenBuffer()
    {
        uint buffer;
        genBuffers(1, &buffer);
        return buffer;
    }

    public static void DeleteBuffer(uint buffer) => deleteBuffers(1, &buffer);
    public static void BindBuffer(uint target, uint buffer) => bindBuffer(target, buffer);

    public static void BufferData(uint target, ReadOnlySpan<float> data, uint usage)
    {
        fixed (float* start = data)
            bufferData(target, data.Length * sizeof(float), start, usage);
    }

    public static void VertexAttribPointer(uint index, int size, uint type, int stride, int offset) =>
        vertexAttribPointer(index, size, type, 0, stride, offset);

    public static void EnableVertexAttribArray(uint index) => enableVertexAttribArray(index);
}
