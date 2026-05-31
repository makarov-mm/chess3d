using System.Numerics;

namespace Chess3D;

internal sealed class Mesh
{
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly int _vertexCount;

    public Mesh(float[] data)
    {
        _vertexCount = data.Length / 8;

        Gl.GenVertexArrays(1, out _vao);
        Gl.GenBuffers(1, out _vbo);

        Gl.BindVertexArray(_vao);
        Gl.BindBuffer(Gl.GL_ARRAY_BUFFER, _vbo);
        Gl.BufferData(Gl.GL_ARRAY_BUFFER, data, Gl.GL_STATIC_DRAW);

        int stride = 8 * sizeof(float);
        Gl.EnableVertexAttribArray(0);
        Gl.VertexAttribPointer(0, 3, Gl.GL_FLOAT, false, stride, IntPtr.Zero);

        Gl.EnableVertexAttribArray(1);
        Gl.VertexAttribPointer(1, 3, Gl.GL_FLOAT, false, stride, new IntPtr(3 * sizeof(float)));

        Gl.EnableVertexAttribArray(2);
        Gl.VertexAttribPointer(2, 2, Gl.GL_FLOAT, false, stride, new IntPtr(6 * sizeof(float)));

        Gl.BindVertexArray(0);
    }

    public void Draw()
    {
        Gl.BindVertexArray(_vao);
        Gl.DrawArrays(Gl.GL_TRIANGLES, 0, _vertexCount);
        Gl.BindVertexArray(0);
    }

    public void Delete()
    {
        Gl.BindVertexArray(0);
        uint vbo = _vbo;
        uint vao = _vao;
        if (vbo != 0) Gl.DeleteBuffers(1, ref vbo);
        if (vao != 0) Gl.DeleteVertexArrays(1, ref vao);
    }

    public static Mesh CreateScreenQuad(float x, float y, float width, float height)
    {
        var v = new List<float>();
        AddVertex(v, new Vector3(x, y, 0.0f), Vector3.UnitZ, new Vector2(0, 0));
        AddVertex(v, new Vector3(x + width, y, 0.0f), Vector3.UnitZ, new Vector2(1, 0));
        AddVertex(v, new Vector3(x + width, y + height, 0.0f), Vector3.UnitZ, new Vector2(1, 1));
        AddVertex(v, new Vector3(x, y, 0.0f), Vector3.UnitZ, new Vector2(0, 0));
        AddVertex(v, new Vector3(x + width, y + height, 0.0f), Vector3.UnitZ, new Vector2(1, 1));
        AddVertex(v, new Vector3(x, y + height, 0.0f), Vector3.UnitZ, new Vector2(0, 1));
        return new Mesh(v.ToArray());
    }

    public static Mesh CreateBitmapText(string[] lines, float x, float y, float pixelSize, float lineGap)
    {
        var v = new List<float>();
        float cursorY = y;

        foreach (string line in lines)
        {
            float cursorX = x;

            foreach (char raw in line)
            {
                char ch = char.ToUpperInvariant(raw);
                if (ch == ' ')
                {
                    cursorX += 4.0f * pixelSize;
                    continue;
                }

                if (TryGetGlyph(ch, out string[] glyph))
                {
                    for (int gy = 0; gy < glyph.Length; gy++)
                    {
                        string row = glyph[gy];

                        for (int gx = 0; gx < row.Length; gx++)
                        {
                            if (row[gx] != '1')
                                continue;

                            float px = cursorX + gx * pixelSize;
                            float py = cursorY + gy * pixelSize;
                            AddScreenRect(v, px, py, pixelSize, pixelSize);
                        }
                    }

                    cursorX += 6.0f * pixelSize;
                }
                else
                {
                    cursorX += 4.0f * pixelSize;
                }
            }

            cursorY += 7.0f * pixelSize + lineGap;
        }

        return new Mesh(v.ToArray());
    }

    private static void AddScreenRect(List<float> v, float x, float y, float w, float h)
    {
        AddVertex(v, new Vector3(x, y, 0.0f), Vector3.UnitZ, new Vector2(0, 0));
        AddVertex(v, new Vector3(x + w, y, 0.0f), Vector3.UnitZ, new Vector2(1, 0));
        AddVertex(v, new Vector3(x + w, y + h, 0.0f), Vector3.UnitZ, new Vector2(1, 1));
        AddVertex(v, new Vector3(x, y, 0.0f), Vector3.UnitZ, new Vector2(0, 0));
        AddVertex(v, new Vector3(x + w, y + h, 0.0f), Vector3.UnitZ, new Vector2(1, 1));
        AddVertex(v, new Vector3(x, y + h, 0.0f), Vector3.UnitZ, new Vector2(0, 1));
    }

    private static bool TryGetGlyph(char ch, out string[] glyph)
    {
        glyph = ch switch
        {
            'A' => new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" },
            'B' => new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" },
            'C' => new[] { "01111", "10000", "10000", "10000", "10000", "10000", "01111" },
            'D' => new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" },
            'E' => new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" },
            'F' => new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" },
            'G' => new[] { "01111", "10000", "10000", "10011", "10001", "10001", "01111" },
            'H' => new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" },
            'I' => new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" },
            'J' => new[] { "00111", "00010", "00010", "00010", "10010", "10010", "01100" },
            'K' => new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" },
            'L' => new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" },
            'M' => new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" },
            'N' => new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" },
            'O' => new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" },
            'P' => new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" },
            'Q' => new[] { "01110", "10001", "10001", "10001", "10101", "10010", "01101" },
            'R' => new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" },
            'S' => new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" },
            'T' => new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" },
            'U' => new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" },
            'V' => new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" },
            'W' => new[] { "10001", "10001", "10001", "10101", "10101", "10101", "01010" },
            'X' => new[] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" },
            'Y' => new[] { "10001", "10001", "01010", "00100", "00100", "00100", "00100" },
            'Z' => new[] { "11111", "00001", "00010", "00100", "01000", "10000", "11111" },
            '0' => new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" },
            '1' => new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" },
            '2' => new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" },
            '3' => new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" },
            '4' => new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" },
            '5' => new[] { "11111", "10000", "10000", "11110", "00001", "00001", "11110" },
            '6' => new[] { "01110", "10000", "10000", "11110", "10001", "10001", "01110" },
            '7' => new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
            '8' => new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
            '9' => new[] { "01110", "10001", "10001", "01111", "00001", "00001", "01110" },
            '+' => new[] { "00000", "00100", "00100", "11111", "00100", "00100", "00000" },
            '/' => new[] { "00001", "00010", "00010", "00100", "01000", "01000", "10000" },
            '-' => new[] { "00000", "00000", "00000", "11111", "00000", "00000", "00000" },
            ':' => new[] { "00000", "00100", "00100", "00000", "00100", "00100", "00000" },
            _ => Array.Empty<string>()
        };

        return glyph.Length > 0;
    }

    public static Mesh CreateStickerPlane()
    {
        var v = new List<float>();

        AddQuad(v,
            new Vector3(-0.5f, -0.5f, 0.0f),
            new Vector3(0.5f, -0.5f, 0.0f),
            new Vector3(0.5f, 0.5f, 0.0f),
            new Vector3(-0.5f, 0.5f, 0.0f),
            Vector3.UnitZ);

        return new Mesh(v.ToArray());
    }

    public static Mesh CreateFloorPlane()
    {
        var v = new List<float>();
        AddVertex(v, new Vector3(-0.5f, 0.0f, -0.5f), Vector3.UnitY, new Vector2(0, 0));
        AddVertex(v, new Vector3(0.5f, 0.0f, -0.5f), Vector3.UnitY, new Vector2(1, 0));
        AddVertex(v, new Vector3(0.5f, 0.0f, 0.5f), Vector3.UnitY, new Vector2(1, 1));
        AddVertex(v, new Vector3(-0.5f, 0.0f, -0.5f), Vector3.UnitY, new Vector2(0, 0));
        AddVertex(v, new Vector3(0.5f, 0.0f, 0.5f), Vector3.UnitY, new Vector2(1, 1));
        AddVertex(v, new Vector3(-0.5f, 0.0f, 0.5f), Vector3.UnitY, new Vector2(0, 1));
        return new Mesh(v.ToArray());
    }

    public static Mesh CreateCube()
    {
        var v = new List<float>();

        AddQuad(v,
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
            Vector3.UnitZ);

        AddQuad(v,
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            -Vector3.UnitZ);

        AddQuad(v,
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            Vector3.UnitX);

        AddQuad(v,
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            -Vector3.UnitX);

        AddQuad(v,
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            Vector3.UnitY);

        AddQuad(v,
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            -Vector3.UnitY);

        return new Mesh(v.ToArray());
    }

    private static void AddQuad(List<float> v, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
    {
        AddVertex(v, a, n, new Vector2(0, 0));
        AddVertex(v, b, n, new Vector2(1, 0));
        AddVertex(v, c, n, new Vector2(1, 1));
        AddVertex(v, a, n, new Vector2(0, 0));
        AddVertex(v, c, n, new Vector2(1, 1));
        AddVertex(v, d, n, new Vector2(0, 1));
    }

    private static void AddVertex(List<float> v, Vector3 p, Vector3 n, System.Numerics.Vector2 uv)
    {
        v.Add(p.X); v.Add(p.Y); v.Add(p.Z);
        v.Add(n.X); v.Add(n.Y); v.Add(n.Z);
        v.Add(uv.X); v.Add(uv.Y);
    }
}
