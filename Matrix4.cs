using System.Numerics;

namespace Chess3D;

internal struct Matrix4
{
    public float[] M;

    public Matrix4(bool identity)
    {
        M = new float[16];
        if (identity)
        {
            M[0] = 1.0f;
            M[5] = 1.0f;
            M[10] = 1.0f;
            M[15] = 1.0f;
        }
    }

    public static Matrix4 Identity() => new(true);

    public static Matrix4 Ortho(float left, float right, float bottom, float top, float zNear, float zFar)
    {
        var r = Identity();

        r.M[0] = 2.0f / (right - left);
        r.M[5] = 2.0f / (top - bottom);
        r.M[10] = -2.0f / (zFar - zNear);
        r.M[12] = -(right + left) / (right - left);
        r.M[13] = -(top + bottom) / (top - bottom);
        r.M[14] = -(zFar + zNear) / (zFar - zNear);

        return r;
    }

    public static Matrix4 Perspective(float fovYRadians, float aspect, float zNear, float zFar)
    {
        var r = new Matrix4(false);

        float f = 1.0f / MathF.Tan(fovYRadians * 0.5f);
        r.M[0] = f / aspect;
        r.M[5] = f;
        r.M[10] = (zFar + zNear) / (zNear - zFar);
        r.M[11] = -1.0f;
        r.M[14] = (2.0f * zFar * zNear) / (zNear - zFar);

        return r;
    }

    public static Matrix4 LookAt(Vector3 eye, Vector3 target, Vector3 up)
    {
        Vector3 f = Vector3.Normalize(target - eye);
        Vector3 s = Vector3.Normalize(Vector3.Cross(f, up));
        Vector3 u = Vector3.Cross(s, f);

        var r = Identity();
        r.M[0] = s.X;
        r.M[4] = s.Y;
        r.M[8]  = s.Z;
        r.M[1] = u.X;
        r.M[5] = u.Y;
        r.M[9]  = u.Z;
        r.M[2] = -f.X;
        r.M[6] = -f.Y;
        r.M[10] = -f.Z;

        r.M[12] = -Vector3.Dot(s, eye);
        r.M[13] = -Vector3.Dot(u, eye);
        r.M[14] = Vector3.Dot(f, eye);

        return r;
    }

    public static Matrix4 Translation(float x, float y, float z)
    {
        var r = Identity();

        r.M[12] = x;
        r.M[13] = y;
        r.M[14] = z;

        return r;
    }

    public static Matrix4 Scale(float x, float y, float z)
    {
        var r = Identity();

        r.M[0] = x;
        r.M[5] = y;
        r.M[10] = z;

        return r;
    }

    public static Matrix4 RotationX(float a)
    {
        var r = Identity();
        float c = MathF.Cos(a);
        float s = MathF.Sin(a);

        r.M[5] = c;
        r.M[6] = s;
        r.M[9] = -s;
        r.M[10] = c;

        return r;
    }

    public static Matrix4 RotationY(float a)
    {
        var r = Identity();
        float c = MathF.Cos(a);
        float s = MathF.Sin(a);

        r.M[0] = c;
        r.M[2] = -s;
        r.M[8] = s;
        r.M[10] = c;

        return r;
    }

    public static Matrix4 RotationZ(float a)
    {
        var r = Identity();
        float c = MathF.Cos(a);
        float s = MathF.Sin(a);

        r.M[0] = c;
        r.M[1] = s;
        r.M[4] = -s;
        r.M[5] = c;

        return r;
    }

    public static Matrix4 Multiply(Matrix4 a, Matrix4 b)
    {
        var r = new Matrix4(false);

        for (int col = 0; col < 4; ++col)
        {
            for (int row = 0; row < 4; ++row)
            {
                r.M[col * 4 + row] =
                    a.M[0 * 4 + row] * b.M[col * 4 + 0] +
                    a.M[1 * 4 + row] * b.M[col * 4 + 1] +
                    a.M[2 * 4 + row] * b.M[col * 4 + 2] +
                    a.M[3 * 4 + row] * b.M[col * 4 + 3];
            }
        }

        return r;
    }

    public static Matrix4 Chain(params Matrix4[] matrices)
    {
        if (matrices.Length == 0)
            return Identity();

        Matrix4 result = matrices[0];

        for (int i = 1; i < matrices.Length; i++)
            result = Multiply(result, matrices[i]);

        return result;
    }
}
