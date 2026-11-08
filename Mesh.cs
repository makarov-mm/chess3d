using System.Numerics;

namespace Chess3D;

/// <summary>
/// Triangles in a vertex buffer. A vertex is 8 floats: the position, the normal and the texture
/// coordinates, which the shaders read at locations 0, 1 and 2.
/// </summary>
internal sealed class Mesh : IDisposable
{
    public const int VertexSize = 8;

    private readonly uint _vertexArray;
    private readonly uint _buffer;
    private readonly int _vertexCount;
    private bool _disposed;

    public Mesh(ReadOnlySpan<float> vertices)
    {
        _vertexCount = vertices.Length / VertexSize;
        _vertexArray = Gl.GenVertexArray();
        _buffer = Gl.GenBuffer();

        Gl.BindVertexArray(_vertexArray);
        Gl.BindBuffer(Gl.ArrayBuffer, _buffer);
        Gl.BufferData(Gl.ArrayBuffer, vertices, Gl.StaticDraw);

        const int Stride = VertexSize * sizeof(float);
        Gl.EnableVertexAttribArray(0);
        Gl.VertexAttribPointer(0, 3, Gl.Float, Stride, 0);
        Gl.EnableVertexAttribArray(1);
        Gl.VertexAttribPointer(1, 3, Gl.Float, Stride, 3 * sizeof(float));
        Gl.EnableVertexAttribArray(2);
        Gl.VertexAttribPointer(2, 2, Gl.Float, Stride, 6 * sizeof(float));
        Gl.BindVertexArray(0);
    }

    public void Draw()
    {
        Gl.BindVertexArray(_vertexArray);
        Gl.DrawArrays(Gl.Triangles, 0, _vertexCount);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Gl.DeleteVertexArray(_vertexArray);
        Gl.DeleteBuffer(_buffer);
    }

    /// <summary>The cube from -0.5 to 0.5, with its faces counterclockwise from outside.</summary>
    public static Mesh CreateCube()
    {
        var vertices = new List<float>();
        AddQuad(vertices, new(-0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, 0.5f), new(0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f), Vector3.UnitZ);
        AddQuad(vertices, new(0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), -Vector3.UnitZ);
        AddQuad(vertices, new(0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, 0.5f), Vector3.UnitX);
        AddQuad(vertices, new(-0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, -0.5f), -Vector3.UnitX);
        AddQuad(vertices, new(-0.5f, 0.5f, 0.5f), new(0.5f, 0.5f, 0.5f), new(0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f), Vector3.UnitY);
        AddQuad(vertices, new(-0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, 0.5f), new(-0.5f, -0.5f, 0.5f), -Vector3.UnitY);
        return new Mesh(vertices.ToArray());
    }

    /// <summary>The square from -0.5 to 0.5 in the XZ plane, facing up.</summary>
    public static Mesh CreatePlane()
    {
        var vertices = new List<float>();
        AddQuad(vertices, new(-0.5f, 0, 0.5f), new(0.5f, 0, 0.5f), new(0.5f, 0, -0.5f), new(-0.5f, 0, -0.5f), Vector3.UnitY);
        return new Mesh(vertices.ToArray());
    }

    /// <summary>A rectangle on the screen, in pixels from the top left corner.</summary>
    public static Mesh CreateScreenRectangle(float x, float y, float width, float height)
    {
        var vertices = new List<float>();
        AddScreenRectangle(vertices, x, y, width, height);
        return new Mesh(vertices.ToArray());
    }

    /// <summary>
    /// Lines of text in a 5x7 pixel font, each font pixel a square of <paramref name="pixelSize"/>, from
    /// (<paramref name="x"/>, <paramref name="y"/>) on the screen down.
    /// </summary>
    public static Mesh CreateText(IEnumerable<string> lines, float x, float y, float pixelSize, float lineGap)
    {
        var vertices = new List<float>();
        float top = y;

        foreach (string line in lines)
        {
            float left = x;
            foreach (char character in line)
            {
                if (!Font.TryGetValue(char.ToUpperInvariant(character), out string[]? glyph))
                {
                    left += 4 * pixelSize; // a space, or a character the font lacks
                    continue;
                }

                for (int row = 0; row < glyph.Length; row++)
                {
                    for (int column = 0; column < glyph[row].Length; column++)
                    {
                        if (glyph[row][column] == '1')
                            AddScreenRectangle(vertices, left + column * pixelSize, top + row * pixelSize, pixelSize, pixelSize);
                    }
                }
                left += 6 * pixelSize;
            }
            top += 7 * pixelSize + lineGap;
        }

        return new Mesh(vertices.ToArray());
    }

    private static void AddQuad(List<float> vertices, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
    {
        AddVertex(vertices, a, normal, new Vector2(0, 0));
        AddVertex(vertices, b, normal, new Vector2(1, 0));
        AddVertex(vertices, c, normal, new Vector2(1, 1));
        AddVertex(vertices, a, normal, new Vector2(0, 0));
        AddVertex(vertices, c, normal, new Vector2(1, 1));
        AddVertex(vertices, d, normal, new Vector2(0, 1));
    }

    private static void AddScreenRectangle(List<float> vertices, float x, float y, float width, float height) =>
        AddQuad(vertices, new(x, y, 0), new(x + width, y, 0), new(x + width, y + height, 0), new(x, y + height, 0), Vector3.UnitZ);

    public static void AddVertex(List<float> vertices, Vector3 position, Vector3 normal, Vector2 uv)
    {
        vertices.Add(position.X);
        vertices.Add(position.Y);
        vertices.Add(position.Z);
        vertices.Add(normal.X);
        vertices.Add(normal.Y);
        vertices.Add(normal.Z);
        vertices.Add(uv.X);
        vertices.Add(uv.Y);
    }

    private static readonly Dictionary<char, string[]> Font = new()
    {
        ['A'] = ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
        ['B'] = ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
        ['C'] = ["01111", "10000", "10000", "10000", "10000", "10000", "01111"],
        ['D'] = ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
        ['E'] = ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
        ['F'] = ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
        ['G'] = ["01111", "10000", "10000", "10011", "10001", "10001", "01111"],
        ['H'] = ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
        ['I'] = ["11111", "00100", "00100", "00100", "00100", "00100", "11111"],
        ['J'] = ["00111", "00010", "00010", "00010", "10010", "10010", "01100"],
        ['K'] = ["10001", "10010", "10100", "11000", "10100", "10010", "10001"],
        ['L'] = ["10000", "10000", "10000", "10000", "10000", "10000", "11111"],
        ['M'] = ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
        ['N'] = ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
        ['O'] = ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
        ['P'] = ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
        ['Q'] = ["01110", "10001", "10001", "10001", "10101", "10010", "01101"],
        ['R'] = ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
        ['S'] = ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
        ['T'] = ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
        ['U'] = ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
        ['V'] = ["10001", "10001", "10001", "10001", "10001", "01010", "00100"],
        ['W'] = ["10001", "10001", "10001", "10101", "10101", "10101", "01010"],
        ['X'] = ["10001", "10001", "01010", "00100", "01010", "10001", "10001"],
        ['Y'] = ["10001", "10001", "01010", "00100", "00100", "00100", "00100"],
        ['Z'] = ["11111", "00001", "00010", "00100", "01000", "10000", "11111"],
        ['0'] = ["01110", "10001", "10011", "10101", "11001", "10001", "01110"],
        ['1'] = ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
        ['2'] = ["01110", "10001", "00001", "00010", "00100", "01000", "11111"],
        ['3'] = ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
        ['4'] = ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
        ['5'] = ["11111", "10000", "10000", "11110", "00001", "00001", "11110"],
        ['6'] = ["01110", "10000", "10000", "11110", "10001", "10001", "01110"],
        ['7'] = ["11111", "00001", "00010", "00100", "01000", "01000", "01000"],
        ['8'] = ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
        ['9'] = ["01110", "10001", "10001", "01111", "00001", "00001", "01110"],
        ['+'] = ["00000", "00100", "00100", "11111", "00100", "00100", "00000"],
        ['/'] = ["00001", "00010", "00010", "00100", "01000", "01000", "10000"],
        ['-'] = ["00000", "00000", "00000", "11111", "00000", "00000", "00000"],
        [':'] = ["00000", "00100", "00100", "00000", "00100", "00100", "00000"],
    };
}
