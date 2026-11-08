using System.Numerics;

namespace Chess3D;

/// <summary>
/// Draws the scene: the board and the pieces over a glowing floor grid that reflects them, the
/// highlights of the selected piece and its moves, and the text overlay. The OpenGL context must be current.
/// </summary>
internal sealed class Renderer : IDisposable
{
    public const float BoardTopY = 0.055f;
    private const float FloorY = -0.34f;
    private const float PieceScale = 0.72f;

    /// <summary>The overlay, in logical (96 DPI) pixels from the top left corner; clicks there do not reach the board.</summary>
    public static readonly RectangleF OverlayArea = new(0, 0, 384, 320);

    // The look of a surface: uMaterial in chess.frag.
    private enum Material
    {
        BoardSquare = 0,
        Piece = 1,
        Floor = 2,
        Overlay = 3,
        Highlight = 4,
        BoardBase = 5
    }

    private static readonly string[] Help =
    [
        "3D CHESS",
        "",
        "LEFT CLICK SELECT MOVE",
        "DRAG ROTATE   WHEEL ZOOM",
        "",
        "N NEW GAME",
        "C TOGGLE COMPUTER",
        "S SWAP SIDES",
        "1 2 3 LEVEL",
        "HOME RESET CAMERA",
        "ESC QUIT"
    ];

    private static readonly Vector3 LightSquare = new(0.78f, 0.66f, 0.46f);
    private static readonly Vector3 DarkSquare = new(0.18f, 0.085f, 0.040f);
    private static readonly Vector3 BoardBaseColor = new(0.028f, 0.025f, 0.022f);
    private static readonly Vector3 WhitePiece = new(0.92f, 0.86f, 0.70f);
    private static readonly Vector3 BlackPiece = new(0.018f, 0.024f, 0.038f);
    private static readonly Vector3 FloorColor = new(0.05f, 0.35f, 0.85f);
    private static readonly Vector3 SelectionColor = new(1.0f, 0.70f, 0.20f);
    private static readonly Vector3 MoveColor = new(0.06f, 0.62f, 1.0f);

    // The pieces mirrored in the floor.
    private static readonly Matrix4x4 Mirror = Matrix4x4.CreateScale(1, -1, 1) * Matrix4x4.CreateTranslation(0, FloorY * 2, 0);

    private readonly uint _program;
    private readonly int _model, _view, _projection, _color, _cameraPosition, _time, _material, _alpha, _reflection;
    private readonly Mesh _cube;
    private readonly Mesh _plane;
    private readonly Mesh[] _pieces; // by PieceKind
    private readonly Mesh _panel;
    private readonly Mesh _help;
    private Mesh? _status;
    private string[] _statusLines = [];

    public Renderer()
    {
        _program = Gl.BuildProgram(File.ReadAllText(Files.ShaderVertex), File.ReadAllText(Files.ShaderFragment));
        _model = Gl.UniformLocation(_program, "uModel");
        _view = Gl.UniformLocation(_program, "uView");
        _projection = Gl.UniformLocation(_program, "uProjection");
        _color = Gl.UniformLocation(_program, "uColor");
        _cameraPosition = Gl.UniformLocation(_program, "uCameraPos");
        _time = Gl.UniformLocation(_program, "uTime");
        _material = Gl.UniformLocation(_program, "uMaterial");
        _alpha = Gl.UniformLocation(_program, "uAlpha");
        _reflection = Gl.UniformLocation(_program, "uReflection");

        _cube = Mesh.CreateCube();
        _plane = Mesh.CreatePlane();
        _pieces = [.. Enum.GetValues<PieceKind>().Select(kind => new Mesh(ObjLoader.Load(Files.Model(kind.ToString().ToLowerInvariant()))))];
        _panel = Mesh.CreateScreenRectangle(14, 14, 360, 296);
        _help = Mesh.CreateText(Help, 26, 28, 2, 4);

        Gl.ClearColor(0.003f, 0.005f, 0.010f, 1);
        Gl.Enable(Gl.DepthTest);
        Gl.DepthFunc(Gl.LessOrEqual);
        Gl.CullFaces(Gl.Back);
        Gl.Enable(Gl.Blend);
        Gl.BlendFunc(Gl.SrcAlpha, Gl.OneMinusSrcAlpha);
        Gl.Enable(Gl.Multisample);
    }

    // From White's side the a-file is on the left: the files run along -X, the ranks along +Z.
    public static Vector3 SquareCenter(int file, int rank) => new(3.5f - file, BoardTopY, rank - 3.5f);

    public static bool TrySquareAt(Vector3 point, out int file, out int rank)
    {
        file = (int)MathF.Floor(4 - point.X);
        rank = (int)MathF.Floor(point.Z + 4);
        return ChessGame.IsInside(file, rank);
    }

    /// <summary>The lines of the status under the help, in a larger font.</summary>
    public void SetStatus(params string[] lines)
    {
        if (_status != null && lines.SequenceEqual(_statusLines))
            return;
        _status?.Dispose();
        _status = Mesh.CreateText(lines, 26, 250, 3, 6);
        _statusLines = lines;
    }

    /// <param name="viewport">The size of the window's client area, in pixels.</param>
    /// <param name="uiScale">Pixels per logical pixel of the overlay: the DPI over 96.</param>
    /// <param name="time">Seconds, for the animated effects.</param>
    public void Draw(Size viewport, float uiScale, Camera camera, ChessGame game, (int File, int Rank)? selected,
                     IReadOnlyList<BoardMove> moves, float time)
    {
        Gl.Viewport(0, 0, viewport.Width, viewport.Height);
        Gl.Clear(Gl.ColorBufferBit | Gl.DepthBufferBit);
        Gl.UseProgram(_program);

        Gl.Uniform(_view, camera.View);
        Gl.Uniform(_projection, Camera.Projection(viewport.Width / Math.Max(1f, viewport.Height)));
        Gl.Uniform(_cameraPosition, camera.Eye);
        Gl.Uniform(_time, time);

        // The reflections first, then the half-transparent floor over them.
        Gl.DepthMask(false);
        DrawPieces(game, Mirror, reflection: true);
        Gl.DepthMask(true);
        DrawFloor();

        DrawBoard();
        DrawHighlights(selected, moves);
        DrawPieces(game, Matrix4x4.Identity, reflection: false);
        DrawOverlay(viewport, uiScale);
    }

    private void DrawPieces(ChessGame game, Matrix4x4 world, bool reflection)
    {
        Gl.Disable(Gl.CullFace);
        Gl.Uniform(_reflection, reflection ? 1 : 0);
        float alpha = reflection ? 0.20f : 1.0f;

        for (int file = 0; file < 8; file++)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                if (game[file, rank] is not { } piece)
                    continue;

                Vector3 center = SquareCenter(file, rank);
                // The knights face the other side.
                float angle = piece.Kind != PieceKind.Knight ? 0 : piece.Side == PieceSide.White ? -MathF.PI / 2 : MathF.PI / 2;
                Matrix4x4 model = Matrix4x4.CreateScale(PieceScale) *
                                  Matrix4x4.CreateRotationY(angle) *
                                  Matrix4x4.CreateTranslation(center.X, BoardTopY + 0.015f, center.Z) *
                                  world;
                DrawMesh(_pieces[(int)piece.Kind], model, piece.Side == PieceSide.White ? WhitePiece : BlackPiece, Material.Piece, alpha);
            }
        }
    }

    private void DrawFloor()
    {
        Gl.Disable(Gl.CullFace);
        Gl.Uniform(_reflection, 0);
        DrawMesh(_plane, Matrix4x4.CreateScale(18, 1, 18) * Matrix4x4.CreateTranslation(0, FloorY, 0), FloorColor, Material.Floor, 0.76f);
    }

    private void DrawBoard()
    {
        Gl.Enable(Gl.CullFace);
        Gl.Uniform(_reflection, 0);
        Matrix4x4 boardBase = Matrix4x4.CreateScale(8.90f, 0.16f, 8.90f) * Matrix4x4.CreateTranslation(0, -0.05f, 0);
        DrawMesh(_cube, boardBase, BoardBaseColor, Material.BoardBase, 1);

        for (int file = 0; file < 8; file++)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                Vector3 center = SquareCenter(file, rank);
                bool light = ((file + rank) & 1) == 1; // a1 is dark
                Matrix4x4 model = Matrix4x4.CreateScale(0.98f, 0.045f, 0.98f) * Matrix4x4.CreateTranslation(center.X, 0.025f, center.Z);
                DrawMesh(_cube, model, light ? LightSquare : DarkSquare, Material.BoardSquare, 1);
            }
        }
    }

    private void DrawHighlights((int File, int Rank)? selected, IReadOnlyList<BoardMove> moves)
    {
        if (selected is not { } square)
            return;

        Gl.Disable(Gl.CullFace);
        Gl.DepthMask(false);
        DrawHighlight(square.File, square.Rank, SelectionColor, 0.55f, 0.96f);
        foreach (BoardMove move in moves)
            DrawHighlight(move.ToFile, move.ToRank, MoveColor, 0.33f, 0.72f);
        Gl.DepthMask(true);
    }

    private void DrawHighlight(int file, int rank, Vector3 color, float alpha, float size)
    {
        Vector3 center = SquareCenter(file, rank);
        Matrix4x4 model = Matrix4x4.CreateScale(size, 1, size) * Matrix4x4.CreateTranslation(center.X, BoardTopY + 0.012f, center.Z);
        DrawMesh(_plane, model, color, Material.Highlight, alpha);
    }

    private void DrawOverlay(Size viewport, float uiScale)
    {
        Gl.Disable(Gl.DepthTest);
        Gl.Disable(Gl.CullFace);

        // Logical pixels from the top left corner
        Gl.Uniform(_view, Matrix4x4.Identity);
        Gl.Uniform(_projection, Matrix4x4.CreateOrthographicOffCenter(0, viewport.Width / uiScale, viewport.Height / uiScale, 0, -1, 1));
        Gl.Uniform(_reflection, 0);

        DrawMesh(_panel, Matrix4x4.Identity, new Vector3(0.006f, 0.011f, 0.020f), Material.Overlay, 0.58f);
        DrawMesh(_help, Matrix4x4.Identity, new Vector3(0.72f, 0.90f, 1.00f), Material.Overlay, 0.94f);
        if (_status != null)
            DrawMesh(_status, Matrix4x4.Identity, new Vector3(1.00f, 0.86f, 0.42f), Material.Overlay, 0.98f);

        Gl.Enable(Gl.DepthTest);
    }

    private void DrawMesh(Mesh mesh, in Matrix4x4 model, Vector3 color, Material material, float alpha)
    {
        Gl.Uniform(_model, model);
        Gl.Uniform(_color, color);
        Gl.Uniform(_material, (int)material);
        Gl.Uniform(_alpha, alpha);
        mesh.Draw();
    }

    public void Dispose()
    {
        _cube.Dispose();
        _plane.Dispose();
        foreach (Mesh piece in _pieces)
            piece.Dispose();
        _panel.Dispose();
        _help.Dispose();
        _status?.Dispose();
        Gl.DeleteProgram(_program);
    }
}
