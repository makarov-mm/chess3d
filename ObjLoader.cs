using System.Globalization;
using System.Numerics;

namespace Chess3D;

/// <summary>Reads the triangles of a Wavefront OBJ file: positions, normals and texture coordinates.</summary>
internal static class ObjLoader
{
    private readonly record struct Corner(int Position, int TexCoord, int Normal);

    /// <summary>The vertices, in the layout of <see cref="Mesh"/>.</summary>
    public static float[] Load(string path)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var texCoords = new List<Vector2>();
        var vertices = new List<float>();
        int lineNumber = 0;

        foreach (string line in File.ReadLines(path))
        {
            lineNumber++;
            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts[0].StartsWith('#'))
                continue;

            try
            {
                switch (parts[0])
                {
                    case "v" when parts.Length >= 4:
                        positions.Add(new Vector3(Parse(parts[1]), Parse(parts[2]), Parse(parts[3])));
                        break;

                    case "vn" when parts.Length >= 4:
                        normals.Add(Vector3.Normalize(new Vector3(Parse(parts[1]), Parse(parts[2]), Parse(parts[3]))));
                        break;

                    case "vt" when parts.Length >= 3:
                        texCoords.Add(new Vector2(Parse(parts[1]), Parse(parts[2])));
                        break;

                    case "f" when parts.Length >= 4:
                        var corners = new Corner[parts.Length - 1];
                        for (int i = 0; i < corners.Length; i++)
                            corners[i] = ParseCorner(parts[i + 1], positions.Count, texCoords.Count, normals.Count);

                        // A polygon is a fan of triangles around its first corner.
                        for (int i = 1; i < corners.Length - 1; i++)
                            AddTriangle(vertices, corners[0], corners[i], corners[i + 1], positions, texCoords, normals);
                        break;
                }
            }
            catch (Exception exception) when (exception is FormatException or OverflowException)
            {
                throw new InvalidDataException($"{path}, line {lineNumber}: {exception.Message}", exception);
            }
        }

        if (vertices.Count == 0)
            throw new InvalidDataException($"{path} has no triangles.");
        return vertices.ToArray();
    }

    private static void AddTriangle(List<float> vertices, Corner a, Corner b, Corner c,
                                    List<Vector3> positions, List<Vector2> texCoords, List<Vector3> normals)
    {
        // Corners without a normal get the triangle's.
        Vector3 faceNormal = Vector3.UnitY;
        if (a.Normal < 0 || b.Normal < 0 || c.Normal < 0)
        {
            Vector3 cross = Vector3.Cross(positions[b.Position] - positions[a.Position], positions[c.Position] - positions[a.Position]);
            if (cross.LengthSquared() > 1e-12f)
                faceNormal = Vector3.Normalize(cross);
        }

        foreach (Corner corner in (ReadOnlySpan<Corner>)[a, b, c])
        {
            Mesh.AddVertex(
                vertices,
                positions[corner.Position],
                corner.Normal >= 0 ? normals[corner.Normal] : faceNormal,
                corner.TexCoord >= 0 ? texCoords[corner.TexCoord] : Vector2.Zero);
        }
    }

    // "v", "v/vt", "v//vn" or "v/vt/vn", with 1-based indices, or negative ones counting back from the end.
    private static Corner ParseCorner(string token, int positionCount, int texCoordCount, int normalCount)
    {
        string[] parts = token.Split('/');
        int position = ParseIndex(parts[0], positionCount);
        int texCoord = parts.Length > 1 && parts[1].Length > 0 ? ParseIndex(parts[1], texCoordCount) : -1;
        int normal = parts.Length > 2 && parts[2].Length > 0 ? ParseIndex(parts[2], normalCount) : -1;
        return new Corner(position, texCoord, normal);
    }

    private static int ParseIndex(string text, int count)
    {
        int index = int.Parse(text, CultureInfo.InvariantCulture);
        int result = index > 0 ? index - 1 : count + index;
        if (index == 0 || result < 0 || result >= count)
            throw new FormatException($"index {index} is out of range");
        return result;
    }

    private static float Parse(string text) => float.Parse(text, CultureInfo.InvariantCulture);
}
