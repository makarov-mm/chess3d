using System.Globalization;
using System.Numerics;

namespace Chess3D;

internal static class ObjLoader
{
    public static Mesh LoadMesh(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("OBJ file not found.", path);
        }

        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var texCoords = new List<Vector2>();
        var data = new List<float>();

        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();

            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                continue;

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
                    var face = new FaceVertex[parts.Length - 1];
                    for (int i = 1; i < parts.Length; i++)
                        face[i - 1] = ParseFaceVertex(parts[i], positions.Count, texCoords.Count, normals.Count);

                    for (int i = 1; i < face.Length - 1; i++)
                        EmitTriangle(data, face[0], face[i], face[i + 1], positions, texCoords, normals);
                    break;
            }
        }

        if (data.Count == 0)
            throw new InvalidOperationException($"OBJ file has no renderable triangles: {path}");

        return new Mesh(data.ToArray());
    }

    private static void EmitTriangle(
        List<float> data,
        FaceVertex a,
        FaceVertex b,
        FaceVertex c,
        List<Vector3> positions,
        List<Vector2> texCoords,
        List<Vector3> normals)
    {
        Vector3 pa = positions[a.PositionIndex];
        Vector3 pb = positions[b.PositionIndex];
        Vector3 pc = positions[c.PositionIndex];
        Vector3 faceNormal = Vector3.Normalize(Vector3.Cross(pb - pa, pc - pa));

        if (float.IsNaN(faceNormal.X) || faceNormal.LengthSquared() < 0.000001f)
            faceNormal = Vector3.UnitY;

        EmitVertex(data, a, positions, texCoords, normals, faceNormal);
        EmitVertex(data, b, positions, texCoords, normals, faceNormal);
        EmitVertex(data, c, positions, texCoords, normals, faceNormal);
    }

    private static void EmitVertex(
        List<float> data,
        FaceVertex v,
        List<Vector3> positions,
        List<Vector2> texCoords,
        List<Vector3> normals,
        Vector3 fallbackNormal)
    {
        Vector3 p = positions[v.PositionIndex];

        Vector3 n = v.NormalIndex >= 0 
            ? normals[v.NormalIndex] 
            : fallbackNormal;

        Vector2 uv = v.TexCoordIndex >= 0 
            ? texCoords[v.TexCoordIndex] 
            : Vector2.Zero;

        data.Add(p.X);
        data.Add(p.Y);
        data.Add(p.Z);

        data.Add(n.X);
        data.Add(n.Y);
        data.Add(n.Z);

        data.Add(uv.X);
        data.Add(uv.Y);
    }

    private static FaceVertex ParseFaceVertex(string token, int positionCount, int texCoordCount, int normalCount)
    {
        string[] parts = token.Split('/');
        int p = ParseIndex(parts[0], positionCount);
        int t = -1;
        int n = -1;

        if (parts.Length > 1 && parts[1].Length > 0)
            t = ParseIndex(parts[1], texCoordCount);
        if (parts.Length > 2 && parts[2].Length > 0)
            n = ParseIndex(parts[2], normalCount);

        return new FaceVertex(p, t, n);
    }

    private static int ParseIndex(string s, int count)
    {
        int index = int.Parse(s, CultureInfo.InvariantCulture);
        if (index > 0) return index - 1;
        if (index < 0) return count + index;
        throw new FormatException("OBJ index 0 is invalid.");
    }

    private static float Parse(string s) => float.Parse(s, CultureInfo.InvariantCulture);
    private readonly record struct FaceVertex(int PositionIndex, int TexCoordIndex, int NormalIndex);
}
