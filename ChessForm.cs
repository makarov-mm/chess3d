using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Timer = System.Windows.Forms.Timer;

namespace Chess3D;

internal sealed class ChessForm : Form
{
    private const float BoardTopY = 0.055f;
    private const float FloorY = -0.34f;
    private const float PieceScale = 0.72f;
    private const float FovY = 0.72f;

    private IntPtr _hdc;
    private IntPtr _hrc;

    private uint _program;
    private int _uModel;
    private int _uView;
    private int _uProjection;
    private int _uColor;
    private int _uCameraPos;
    private int _uTime;
    private int _uMaterial;
    private int _uAlpha;
    private int _uReflection;

    private Mesh _cubeMesh = null!;
    private Mesh _floorMesh = null!;
    private Mesh _highlightMesh = null!;
    private Mesh _overlayPanelMesh = null!;
    private Mesh _overlayTextMesh = null!;
    private Mesh? _statusTextMesh;
    private string _statusString = "";
    private readonly Dictionary<PieceKind, Mesh> _pieceMeshes = new();

    private readonly ChessGame _game = new();
    private (int File, int Rank)? _selected;
    private readonly List<BoardMove> _legalMoves = new();

    // Computer opponent state.
    private bool _vsComputer = true;
    private PieceSide _humanSide = PieceSide.White;
    private int _aiDepth = 3;
    private bool _aiThinking;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Timer _timer = new() { Interval = 16 };

    private float _yaw = 3.14f;
    private float _pitch = 0.68f;
    private float _distance = 10.8f;
    private bool _mouseDown;
    private Point _mouseStart;
    private Point _lastMouse;
    private int _dragPixels;

    public ChessForm()
    {
        Text = "3D Chess - C# WinForms OpenGL Shader Demo";
        ClientSize = new Size(1200, 860);
        MinimumSize = new Size(720, 520);
        KeyPreview = true;
        DoubleBuffered = false;
        SetStyle(ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

        _timer.Tick += (_, _) => Invalidate(false);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int CS_OWNDC = 0x0020;
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_OWNDC;
            return cp;
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        InitOpenGL();
        InitScene();
        UpdateWindowTitle();
        UpdateStatusText();
        _timer.Start();
        MaybeTriggerAi();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();

        if (_hrc != IntPtr.Zero)
        {
            Wgl.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            Wgl.wglDeleteContext(_hrc);
            _hrc = IntPtr.Zero;
        }

        if (_hdc != IntPtr.Zero)
        {
            Wgl.ReleaseDC(Handle, _hdc);
            _hdc = IntPtr.Zero;
        }

        base.OnFormClosed(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // OpenGL owns the whole client area.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_program == 0 || ClientSize.Width <= 0 || ClientSize.Height <= 0)
            return;

        Render();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_hdc != IntPtr.Zero && ClientSize.Width > 0 && ClientSize.Height > 0)
            Gl.Viewport(0, 0, ClientSize.Width, ClientSize.Height);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _mouseDown = true;
            _mouseStart = e.Location;
            _lastMouse = e.Location;
            _dragPixels = 0;
            Capture = true;
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_mouseDown)
        {
            int dx = _lastMouse.X - e.X;
            int dy = e.Y - _lastMouse.Y;
            _lastMouse = e.Location;
            _dragPixels += Math.Abs(dx) + Math.Abs(dy);

            if (_dragPixels > 3)
            {
                _yaw += dx * 0.008f;
                _pitch += dy * 0.008f;
                _pitch = Math.Clamp(_pitch, 0.18f, 1.32f);
            }
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _mouseDown = false;
            Capture = false;

            if (_dragPixels <= 6 && !InOverlay(e.Location))
                HandleClick(e.Location);
        }

        base.OnMouseUp(e);
    }

    private static bool InOverlay(Point p) => p.X < 384 && p.Y < 320;

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        _distance *= e.Delta > 0 ? 0.90f : 1.10f;
        _distance = Math.Clamp(_distance, 6.2f, 20.0f);
        base.OnMouseWheel(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.N:
                StartNewGame();
                break;
            case Keys.C:
                _vsComputer = !_vsComputer;
                UpdateStatusText();
                MaybeTriggerAi();
                break;
            case Keys.S:
                _humanSide = ChessGame.Opposite(_humanSide);
                _vsComputer = true;
                StartNewGame();
                break;
            case Keys.D1:
            case Keys.NumPad1:
                _aiDepth = 2;
                UpdateStatusText();
                break;
            case Keys.D2:
            case Keys.NumPad2:
                _aiDepth = 3;
                UpdateStatusText();
                break;
            case Keys.D3:
            case Keys.NumPad3:
                _aiDepth = 4;
                UpdateStatusText();
                break;
            case Keys.Home:
                ResetCamera();
                break;
            case Keys.Escape:
                Close();
                break;
        }

        base.OnKeyDown(e);
    }

    private void ResetCamera()
    {
        _yaw = 3.14f;
        _pitch = 0.68f;
        _distance = 10.8f;
    }

    private void StartNewGame()
    {
        _game.NewGame();
        ClearSelection();
        _aiThinking = false;
        ResetCamera();
        UpdateWindowTitle();
        UpdateStatusText();
        Invalidate(false);
        MaybeTriggerAi();
    }

    private void InitOpenGL()
    {
        _hdc = Wgl.GetDC(Handle);
        if (_hdc == IntPtr.Zero)
            throw new InvalidOperationException("GetDC failed.");

        var pfd = new Wgl.PIXELFORMATDESCRIPTOR
        {
            nSize = (ushort)Marshal.SizeOf<Wgl.PIXELFORMATDESCRIPTOR>(),
            nVersion = 1,
            dwFlags = Wgl.PFD_DRAW_TO_WINDOW | Wgl.PFD_SUPPORT_OPENGL | Wgl.PFD_DOUBLEBUFFER,
            iPixelType = Wgl.PFD_TYPE_RGBA,
            cColorBits = 32,
            cDepthBits = 24,
            cStencilBits = 8,
            iLayerType = Wgl.PFD_MAIN_PLANE
        };

        int pixelFormat = Wgl.ChooseMultisamplePixelFormat(_hdc, 8);

        if (pixelFormat == 0)
            pixelFormat = Wgl.ChooseMultisamplePixelFormat(_hdc, 4);

        if (pixelFormat == 0)
            pixelFormat = Wgl.ChoosePixelFormat(_hdc, ref pfd);

        if (pixelFormat == 0)
            throw new InvalidOperationException("ChoosePixelFormat failed.");

        var actualPfd = new Wgl.PIXELFORMATDESCRIPTOR
        {
            nSize = (ushort)Marshal.SizeOf<Wgl.PIXELFORMATDESCRIPTOR>(),
            nVersion = 1
        };

        Wgl.DescribePixelFormat(
            _hdc,
            pixelFormat,
            (uint)Marshal.SizeOf<Wgl.PIXELFORMATDESCRIPTOR>(),
            ref actualPfd);

        if (!Wgl.SetPixelFormat(_hdc, pixelFormat, ref actualPfd))
            throw new InvalidOperationException("SetPixelFormat failed.");

        _hrc = Wgl.wglCreateContext(_hdc);
        if (_hrc == IntPtr.Zero)
            throw new InvalidOperationException("wglCreateContext failed.");

        if (!Wgl.wglMakeCurrent(_hdc, _hrc))
            throw new InvalidOperationException("wglMakeCurrent failed.");

        Gl.LoadFunctions();

        string version = Gl.GetString(Gl.GL_VERSION);
        Text = $"3D Chess - C# WinForms OpenGL Shader Demo   |   OpenGL {version}";

        Gl.Viewport(0, 0, ClientSize.Width, ClientSize.Height);
        Gl.ClearColor(0.003f, 0.005f, 0.010f, 1.0f);
        Gl.Enable(Gl.GL_DEPTH_TEST);
        Gl.DepthFunc(Gl.GL_LEQUAL);
        Gl.Enable(Gl.GL_CULL_FACE);
        Gl.CullFace(Gl.GL_BACK);
        Gl.Enable(Gl.GL_BLEND);
        Gl.BlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE_MINUS_SRC_ALPHA);
        Gl.Enable(Gl.GL_MULTISAMPLE);
    }

    private void InitScene()
    {
        _program = CreateProgram(File.ReadAllText(Files.ShaderVertex), File.ReadAllText(Files.ShaderFragment));
        Gl.UseProgram(_program);

        _uModel = Gl.GetUniformLocation(_program, "uModel");
        _uView = Gl.GetUniformLocation(_program, "uView");
        _uProjection = Gl.GetUniformLocation(_program, "uProjection");
        _uColor = Gl.GetUniformLocation(_program, "uColor");
        _uCameraPos = Gl.GetUniformLocation(_program, "uCameraPos");
        _uTime = Gl.GetUniformLocation(_program, "uTime");
        _uMaterial = Gl.GetUniformLocation(_program, "uMaterial");
        _uAlpha = Gl.GetUniformLocation(_program, "uAlpha");
        _uReflection = Gl.GetUniformLocation(_program, "uReflection");

        _cubeMesh = Mesh.CreateCube();
        _floorMesh = Mesh.CreateFloorPlane();
        _highlightMesh = Mesh.CreateFloorPlane();

        _pieceMeshes[PieceKind.Pawn] = ObjLoader.LoadMesh(Files.Model("pawn"));
        _pieceMeshes[PieceKind.Knight] = ObjLoader.LoadMesh(Files.Model("knight"));
        _pieceMeshes[PieceKind.Bishop] = ObjLoader.LoadMesh(Files.Model("bishop"));
        _pieceMeshes[PieceKind.Rook] = ObjLoader.LoadMesh(Files.Model("rook"));
        _pieceMeshes[PieceKind.Queen] = ObjLoader.LoadMesh(Files.Model("queen"));
        _pieceMeshes[PieceKind.King] = ObjLoader.LoadMesh(Files.Model("king"));

        _overlayPanelMesh = Mesh.CreateScreenQuad(14.0f, 14.0f, 360.0f, 296.0f);
        _overlayTextMesh = Mesh.CreateBitmapText(new[]
        {
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
        }, 26.0f, 28.0f, 2.0f, 4.0f);
    }

    private void Render()
    {
        Gl.Viewport(0, 0, ClientSize.Width, ClientSize.Height);
        Gl.Clear(Gl.GL_COLOR_BUFFER_BIT | Gl.GL_DEPTH_BUFFER_BIT);
        Gl.UseProgram(_program);

        float aspect = ClientSize.Width / Math.Max(1.0f, (float)ClientSize.Height);
        Vector3 eye = GetCameraPosition();
        Vector3 target = CameraTarget;
        Matrix4 view = Matrix4.LookAt(eye, target, Vector3.UnitY);
        Matrix4 projection = Matrix4.Perspective(FovY, aspect, 0.05f, 90.0f);

        Gl.UniformMatrix4fv(_uView, view);
        Gl.UniformMatrix4fv(_uProjection, projection);
        Gl.Uniform3f(_uCameraPos, eye.X, eye.Y, eye.Z);
        Gl.Uniform1f(_uTime, (float)_clock.Elapsed.TotalSeconds);

        Matrix4 mirror = Matrix4.Multiply(Matrix4.Translation(0.0f, FloorY * 2.0f, 0.0f), Matrix4.Scale(1.0f, -1.0f, 1.0f));

        Gl.Disable(Gl.GL_CULL_FACE);
        Gl.DepthMask(false);
        DrawPieces(mirror, reflection: true);
        Gl.DepthMask(true);

        DrawFloor();

        Gl.Enable(Gl.GL_CULL_FACE);
        Gl.CullFace(Gl.GL_BACK);
        DrawBoard();
        DrawHighlights();
        DrawPieces(Matrix4.Identity(), reflection: false);
        DrawOverlay();

        Wgl.SwapBuffers(_hdc);
    }

    private Vector3 CameraTarget => new(0.0f, 0.45f, 0.0f);

    private Vector3 GetCameraPosition()
    {
        float cp = MathF.Cos(_pitch);
        Vector3 dir = new(MathF.Sin(_yaw) * cp, MathF.Sin(_pitch), MathF.Cos(_yaw) * cp);
        return CameraTarget + dir * _distance;
    }

    private void DrawFloor()
    {
        Gl.Uniform1i(_uMaterial, 2);
        Gl.Uniform1i(_uReflection, 0);
        Gl.Uniform1f(_uAlpha, 0.76f);
        Gl.Uniform3f(_uColor, 0.05f, 0.35f, 0.85f);
        Matrix4 floorModel = Matrix4.Chain(Matrix4.Translation(0.0f, FloorY, 0.0f), Matrix4.Scale(18.0f, 1.0f, 18.0f));
        Gl.UniformMatrix4fv(_uModel, floorModel);
        _floorMesh.Draw();
    }

    private void DrawBoard()
    {
        Gl.Uniform1i(_uReflection, 0);
        Gl.Uniform1f(_uAlpha, 1.0f);

        DrawMesh(_cubeMesh,
            Matrix4.Chain(Matrix4.Translation(0.0f, -0.05f, 0.0f), Matrix4.Scale(8.90f, 0.16f, 8.90f)),
            new Vector3(0.028f, 0.025f, 0.022f), 5, 1.0f);

        for (int file = 0; file < 8; file++)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                bool light = ((file + rank) & 1) == 0;
                Vector3 color = light ? new Vector3(0.78f, 0.66f, 0.46f) : new Vector3(0.18f, 0.085f, 0.040f);
                Vector3 p = SquareCenter(file, rank);
                Matrix4 model = Matrix4.Chain(Matrix4.Translation(p.X, 0.025f, p.Z), Matrix4.Scale(0.98f, 0.045f, 0.98f));
                DrawMesh(_cubeMesh, model, color, 0, 1.0f);
            }
        }
    }

    private void DrawHighlights()
    {
        Gl.Disable(Gl.GL_CULL_FACE);
        Gl.DepthMask(false);
        Gl.Enable(Gl.GL_BLEND);
        Gl.BlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE_MINUS_SRC_ALPHA);

        if (_selected is { } s)
        {
            DrawHighlightSquare(s.File, s.Rank, new Vector3(1.0f, 0.70f, 0.20f), 0.55f, 0.96f);
            foreach (BoardMove move in _legalMoves)
                DrawHighlightSquare(move.ToFile, move.ToRank, new Vector3(0.06f, 0.62f, 1.0f), 0.33f, 0.72f);
        }

        Gl.DepthMask(true);
        Gl.Enable(Gl.GL_CULL_FACE);
    }

    private void DrawHighlightSquare(int file, int rank, Vector3 color, float alpha, float size)
    {
        Vector3 p = SquareCenter(file, rank);
        Matrix4 model = Matrix4.Chain(Matrix4.Translation(p.X, BoardTopY + 0.012f, p.Z), Matrix4.Scale(size, 1.0f, size));
        DrawMesh(_highlightMesh, model, color, 4, alpha);
    }

    private void DrawPieces(Matrix4 worldExtra, bool reflection)
    {
        Gl.Disable(Gl.GL_CULL_FACE);
        Gl.Uniform1i(_uReflection, reflection ? 1 : 0);
        Gl.Uniform1f(_uAlpha, reflection ? 0.20f : 1.0f);

        foreach (ChessPiece piece in _game.Pieces())
        {
            Vector3 center = SquareCenter(piece.File, piece.Rank);
            float y = BoardTopY + 0.015f;
            Vector3 color = piece.Side == PieceSide.White
                ? new Vector3(0.92f, 0.86f, 0.70f)
                : new Vector3(0.018f, 0.024f, 0.038f);

            float angle = piece.Kind == PieceKind.Knight
                ? (piece.Side == PieceSide.White ? -MathF.PI * 0.5f : MathF.PI * 0.5f)
                : 0.0f;

            Matrix4 model = Matrix4.Chain(
                worldExtra,
                Matrix4.Translation(center.X, y, center.Z),
                Matrix4.RotationY(angle),
                Matrix4.Scale(PieceScale, PieceScale, PieceScale));

            DrawMesh(_pieceMeshes[piece.Kind], model, color, 1, reflection ? 0.20f : 1.0f);
        }
    }

    private void DrawOverlay()
    {
        Gl.Disable(Gl.GL_DEPTH_TEST);
        Gl.Disable(Gl.GL_CULL_FACE);
        Gl.Enable(Gl.GL_BLEND);
        Gl.BlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE_MINUS_SRC_ALPHA);

        Gl.UniformMatrix4fv(_uView, Matrix4.Identity());
        Gl.UniformMatrix4fv(_uProjection, Matrix4.Ortho(0.0f, ClientSize.Width, ClientSize.Height, 0.0f, -1.0f, 1.0f));
        Gl.UniformMatrix4fv(_uModel, Matrix4.Identity());
        Gl.Uniform1i(_uMaterial, 3);
        Gl.Uniform1i(_uReflection, 0);

        Gl.Uniform1f(_uAlpha, 0.58f);
        Gl.Uniform3f(_uColor, 0.006f, 0.011f, 0.020f);
        _overlayPanelMesh.Draw();

        Gl.Uniform1f(_uAlpha, 0.94f);
        Gl.Uniform3f(_uColor, 0.72f, 0.90f, 1.00f);
        _overlayTextMesh.Draw();

        if (_statusTextMesh is { } status)
        {
            Gl.Uniform1f(_uAlpha, 0.98f);
            Gl.Uniform3f(_uColor, 1.00f, 0.86f, 0.42f);
            status.Draw();
        }

        Gl.Enable(Gl.GL_DEPTH_TEST);
        Gl.Enable(Gl.GL_CULL_FACE);
    }

    private void DrawMesh(Mesh mesh, Matrix4 model, Vector3 color, int material, float alpha)
    {
        Gl.Uniform1i(_uMaterial, material);
        Gl.Uniform1f(_uAlpha, alpha);
        Gl.Uniform3f(_uColor, color.X, color.Y, color.Z);
        Gl.UniformMatrix4fv(_uModel, model);
        mesh.Draw();
    }

    private static Vector3 SquareCenter(int file, int rank) => new(file - 3.5f, BoardTopY, rank - 3.5f);

    private void HandleClick(Point screenPoint)
    {
        if (_game.IsOver || _aiThinking || (_vsComputer && _game.Turn != _humanSide))
            return;

        if (!TryPickSquare(screenPoint, out int file, out int rank))
            return;

        ChessPiece? clicked = _game[file, rank];

        if (_selected == null)
        {
            if (clicked != null && clicked.Side == _game.Turn)
                SelectPiece(file, rank);
            return;
        }

        // First matching legal move. Promotion variants are emitted queen-first,
        // so a human pawn reaching the back rank auto-queens.
        int moveIndex = _legalMoves.FindIndex(m => m.ToFile == file && m.ToRank == rank);
        if (moveIndex >= 0)
        {
            MakeMove(_legalMoves[moveIndex]);
            return;
        }

        if (clicked != null && clicked.Side == _game.Turn)
            SelectPiece(file, rank);
        else
            ClearSelection();
    }

    private void SelectPiece(int file, int rank)
    {
        ChessPiece? piece = _game[file, rank];
        if (piece == null || piece.Side != _game.Turn)
            return;

        _selected = (file, rank);
        _legalMoves.Clear();
        _legalMoves.AddRange(_game.LegalMoves(piece));
    }

    private void ClearSelection()
    {
        _selected = null;
        _legalMoves.Clear();
    }

    private void MakeMove(BoardMove move)
    {
        if (!_game.TryMakeMove(move))
            return;

        ClearSelection();
        UpdateWindowTitle();
        UpdateStatusText();
        Invalidate(false);
        MaybeTriggerAi();
    }

    private void MaybeTriggerAi()
    {
        if (!_vsComputer || _game.IsOver || _aiThinking || _game.Turn == _humanSide)
            return;

        _aiThinking = true;
        UpdateStatusText();
        // Defer to the message loop so the human's move is painted first.
        BeginInvoke(new Action(RunAi));
    }

    private void RunAi()
    {
        try
        {
            BoardMove? best = _game.FindBestMove(_game.Turn, _aiDepth);
            if (best is { } move)
                _game.TryMakeMove(move);
        }
        finally
        {
            _aiThinking = false;
        }

        ClearSelection();
        UpdateWindowTitle();
        UpdateStatusText();
        Invalidate(false);
        MaybeTriggerAi();
    }

    private bool TryPickSquare(Point p, out int file, out int rank)
    {
        file = -1;
        rank = -1;

        Vector3 eye = GetCameraPosition();
        Vector3 forward = Vector3.Normalize(CameraTarget - eye);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));

        float aspect = ClientSize.Width / Math.Max(1.0f, (float)ClientSize.Height);
        float ndcX = 2.0f * p.X / Math.Max(1, ClientSize.Width) - 1.0f;
        float ndcY = 1.0f - 2.0f * p.Y / Math.Max(1, ClientSize.Height);
        float tanY = MathF.Tan(FovY * 0.5f);

        Vector3 ray = Vector3.Normalize(forward + right * (ndcX * aspect * tanY) + up * (ndcY * tanY));
        if (MathF.Abs(ray.Y) < 0.0001f)
            return false;

        float t = (BoardTopY - eye.Y) / ray.Y;
        if (t <= 0.0f)
            return false;

        Vector3 hit = eye + ray * t;
        if (hit.X < -4.0f || hit.X >= 4.0f || hit.Z < -4.0f || hit.Z >= 4.0f)
            return false;

        file = (int)MathF.Floor(hit.X + 4.0f);
        rank = (int)MathF.Floor(hit.Z + 4.0f);
        return ChessGame.IsInside(file, rank);
    }

    private void UpdateWindowTitle()
    {
        string status = _game.Status switch
        {
            ChessGame.GameStatus.Check => $"Turn: {_game.Turn}  CHECK",
            ChessGame.GameStatus.Checkmate => $"CHECKMATE - {ChessGame.Opposite(_game.Turn)} WINS",
            ChessGame.GameStatus.Stalemate => "STALEMATE - DRAW",
            _ => $"Turn: {_game.Turn}"
        };
        Text = $"3D Chess - C# WinForms OpenGL Shader Demo   |   {status}";
    }

    private void UpdateStatusText()
    {
        string line1 = _game.Status switch
        {
            ChessGame.GameStatus.Checkmate => $"CHECKMATE {ChessGame.Opposite(_game.Turn).ToString().ToUpperInvariant()} WINS",
            ChessGame.GameStatus.Stalemate => "STALEMATE DRAW",
            _ when _aiThinking => "COMPUTER THINKING",
            ChessGame.GameStatus.Check => $"{_game.Turn.ToString().ToUpperInvariant()} TO MOVE  CHECK",
            _ => $"{_game.Turn.ToString().ToUpperInvariant()} TO MOVE"
        };

        string line2 = _vsComputer
            ? $"VS COMPUTER LEVEL {_aiDepth}"
            : "TWO PLAYERS";

        string combined = line1 + "\n" + line2;
        if (combined == _statusString && _statusTextMesh != null)
            return;

        _statusString = combined;
        _statusTextMesh?.Delete();
        _statusTextMesh = Mesh.CreateBitmapText(new[] { line1, line2 }, 26.0f, 250.0f, 3.0f, 6.0f);
    }

    private static uint CreateProgram(string vertexSource, string fragmentSource)
    {
        uint vs = CompileShader(Gl.GL_VERTEX_SHADER, vertexSource);
        uint fs = CompileShader(Gl.GL_FRAGMENT_SHADER, fragmentSource);
        uint program = Gl.CreateProgram();
        Gl.AttachShader(program, vs);
        Gl.AttachShader(program, fs);
        Gl.LinkProgram(program);

        Gl.GetProgramiv(program, Gl.GL_LINK_STATUS, out int ok);
        if (ok == 0)
        {
            string log = GetProgramLog(program);
            throw new Exception("Program link failed:\n" + log);
        }

        Gl.DeleteShader(vs);
        Gl.DeleteShader(fs);
        return program;
    }

    private static uint CompileShader(uint type, string source)
    {
        uint shader = Gl.CreateShader(type);
        Gl.ShaderSource(shader, source);
        Gl.CompileShader(shader);

        Gl.GetShaderiv(shader, Gl.GL_COMPILE_STATUS, out int ok);
        if (ok == 0)
        {
            string log = GetShaderLog(shader);
            throw new Exception("Shader compilation failed:\n" + log);
        }

        return shader;
    }

    private static string GetShaderLog(uint shader)
    {
        Gl.GetShaderiv(shader, Gl.GL_INFO_LOG_LENGTH, out int len);
        if (len <= 1) return string.Empty;

        IntPtr buffer = Marshal.AllocHGlobal(len);
        try
        {
            Gl.GetShaderInfoLog(shader, len, out _, buffer);
            return Marshal.PtrToStringAnsi(buffer) ?? string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string GetProgramLog(uint program)
    {
        Gl.GetProgramiv(program, Gl.GL_INFO_LOG_LENGTH, out int len);
        if (len <= 1) return string.Empty;

        IntPtr buffer = Marshal.AllocHGlobal(len);
        try
        {
            Gl.GetProgramInfoLog(program, len, out _, buffer);
            return Marshal.PtrToStringAnsi(buffer) ?? string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
