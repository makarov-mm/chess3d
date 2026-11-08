using System.Numerics;

namespace Chess3D;

/// <summary>A camera circling the board: dragging turns it, the mouse wheel moves it closer or away.</summary>
internal sealed class Camera
{
    /// <summary>The vertical field of view, in radians.</summary>
    public const float FieldOfView = 0.72f;

    private const float Near = 0.05f, Far = 90f;
    private const float StartPitch = 0.68f, StartDistance = 10.8f;
    private const float RadiansPerPixel = 0.008f;
    private static readonly Vector3 Target = new(0, 0.45f, 0);

    private float _yaw = MathF.PI;
    private float _pitch = StartPitch;
    private float _distance = StartDistance;

    /// <summary>Looks at the board from the side of the given player.</summary>
    public void Reset(PieceSide side)
    {
        _yaw = side == PieceSide.White ? MathF.PI : 0;
        _pitch = StartPitch;
        _distance = StartDistance;
    }

    public void Rotate(float dx, float dy)
    {
        _yaw += dx * RadiansPerPixel;
        _pitch = Math.Clamp(_pitch + dy * RadiansPerPixel, 0.18f, 1.32f);
    }

    public void Zoom(int wheelDelta) => _distance = Math.Clamp(_distance * (wheelDelta > 0 ? 0.9f : 1.1f), 6.2f, 20f);

    public Vector3 Eye
    {
        get
        {
            float horizontal = MathF.Cos(_pitch);
            var direction = new Vector3(MathF.Sin(_yaw) * horizontal, MathF.Sin(_pitch), MathF.Cos(_yaw) * horizontal);
            return Target + direction * _distance;
        }
    }

    public Matrix4x4 View => Matrix4x4.CreateLookAt(Eye, Target, Vector3.UnitY);

    /// <summary>The perspective projection, for the OpenGL depth range of -1 to 1.</summary>
    public static Matrix4x4 Projection(float aspect)
    {
        float f = 1 / MathF.Tan(FieldOfView / 2);
        return new Matrix4x4(
            f / aspect, 0, 0, 0,
            0, f, 0, 0,
            0, 0, (Far + Near) / (Near - Far), -1,
            0, 0, 2 * Far * Near / (Near - Far), 0);
    }

    /// <summary>Where the ray through a point of the window meets the horizontal plane at the height <paramref name="planeY"/>.</summary>
    public bool TryPick(Point point, Size window, float planeY, out Vector3 hit)
    {
        hit = default;
        Vector3 eye = Eye;
        Vector3 forward = Vector3.Normalize(Target - eye);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        Vector3 up = Vector3.Cross(right, forward);

        float tan = MathF.Tan(FieldOfView / 2);
        float aspect = window.Width / Math.Max(1f, window.Height);
        float x = (2f * point.X / Math.Max(1, window.Width) - 1) * aspect * tan;
        float y = (1 - 2f * point.Y / Math.Max(1, window.Height)) * tan;
        Vector3 ray = Vector3.Normalize(forward + right * x + up * y);

        if (MathF.Abs(ray.Y) < 1e-4f)
            return false;
        float distance = (planeY - eye.Y) / ray.Y;
        if (distance <= 0)
            return false;

        hit = eye + ray * distance;
        return true;
    }
}
