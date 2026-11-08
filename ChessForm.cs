using System.Diagnostics;

namespace Chess3D;

/// <summary>The window: it draws the scene, takes the mouse and the keyboard, and lets the computer move.</summary>
internal sealed class ChessForm : Form
{
    private const string Title = "3D Chess - C# WinForms OpenGL Shader Demo";

    private readonly ChessGame _game = new();
    private readonly Camera _camera = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Wgl.OpenGlContext? _context;
    private Renderer? _renderer;

    private (int File, int Rank)? _selected;
    private List<BoardMove> _legalMoves = [];

    private bool _vsComputer = true;
    private PieceSide _humanSide = PieceSide.White;
    private int _level = 2;                     // 1 to 3: the computer looks 2 to 4 half-moves ahead
    private CancellationTokenSource? _thinking; // while the computer looks for its move

    private bool _mouseDown;
    private Point _lastMouse;
    private float _dragDistance; // logical pixels

    public ChessForm()
    {
        // The sizes are in logical pixels; the layout resumes scaled to the screen's DPI.
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1200, 860);
        MinimumSize = new Size(720, 520);
        ResumeLayout(false);

        Text = Title;
        // OpenGL covers the whole client area; while the window is being resized, frames come from WM_PAINT.
        SetStyle(ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    // OpenGL needs a device context of its own, which keeps its pixel format.
    protected override CreateParams CreateParams
    {
        get
        {
            const int CsOwnDc = 0x0020;
            CreateParams parameters = base.CreateParams;
            parameters.ClassStyle |= CsOwnDc;
            return parameters;
        }
    }

    private float UiScale => DeviceDpi / 96f;
    private bool ComputerToMove => _vsComputer && !_game.IsOver && _game.Turn != _humanSide;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        FitToScreen();

        try
        {
            _context = Wgl.CreateContext(Handle);
            Gl.Load();
            _renderer = new Renderer();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "3D Chess", MessageBoxButtons.OK, MessageBoxIcon.Error);
            BeginInvoke(Close);
            return;
        }

        ResetCamera();
        UpdateStatus();
        Application.Idle += OnIdle;
        StartComputerMove();
    }

    // At a high DPI scale, a small screen may not hold the window.
    private void FitToScreen()
    {
        Rectangle area = Screen.GetWorkingArea(this);
        if (Width <= area.Width && Height <= area.Height)
            return;
        Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        CenterToScreen();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopThinking();
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Application.Idle -= OnIdle;
        _renderer?.Dispose();
        _context?.Dispose();
        _renderer = null;
        _context = null;
        base.OnFormClosed(e);
    }

    // Frames are drawn whenever no message waits, at the screen refresh rate: swapping the buffers waits
    // for it. A frame that took less than 2 ms did not wait (no vertical sync, or a window the driver does
    // not show), so a short sleep follows it, and the loop does not spin.
    private void OnIdle(object? sender, EventArgs e)
    {
        while (_context != null && WindowState != FormWindowState.Minimized && Wgl.IsMessageQueueEmpty())
        {
            long start = Stopwatch.GetTimestamp();
            Render();
            if (Stopwatch.GetElapsedTime(start).TotalMilliseconds < 2)
                Thread.Sleep(1);
        }
    }

    protected override void OnPaint(PaintEventArgs e) => Render();

    private void Render()
    {
        if (_renderer == null || _context == null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
            return;

        _renderer.Draw(ClientSize, UiScale, _camera, _game, _selected, _legalMoves, (float)_clock.Elapsed.TotalSeconds);
        _context.SwapBuffers();
    }

    // Input ----------------------------------------------------------------------------------------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _mouseDown = true;
            _lastMouse = e.Location;
            _dragDistance = 0;
        }
        base.OnMouseDown(e);
    }

    // The window holds the mouse while a button is down; if another window takes it (Alt+Tab), the drag ends.
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        _mouseDown = false;
        base.OnMouseCaptureChanged(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_mouseDown)
        {
            float dx = (_lastMouse.X - e.X) / UiScale;
            float dy = (e.Y - _lastMouse.Y) / UiScale;
            _lastMouse = e.Location;
            _dragDistance += Math.Abs(dx) + Math.Abs(dy);
            if (_dragDistance > 3)
                _camera.Rotate(dx, dy);
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _mouseDown)
        {
            _mouseDown = false;
            // A click, not a drag, and not on the overlay
            if (_dragDistance <= 6 && !Renderer.OverlayArea.Contains(e.X / UiScale, e.Y / UiScale))
                HandleClick(e.Location);
        }
        base.OnMouseUp(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        _camera.Zoom(e.Delta);
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
                StopThinking();
                UpdateStatus();
                StartComputerMove();
                break;

            case Keys.S:
                _humanSide = ChessGame.Opposite(_humanSide);
                _vsComputer = true;
                StartNewGame();
                break;

            case Keys.D1 or Keys.NumPad1:
                SetLevel(1);
                break;

            case Keys.D2 or Keys.NumPad2:
                SetLevel(2);
                break;

            case Keys.D3 or Keys.NumPad3:
                SetLevel(3);
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

    // The game -------------------------------------------------------------------------------------

    private void StartNewGame()
    {
        StopThinking();
        _game.NewGame();
        ClearSelection();
        ResetCamera();
        UpdateStatus();
        StartComputerMove();
    }

    // The level applies from the computer's next move.
    private void SetLevel(int level)
    {
        _level = level;
        UpdateStatus();
    }

    private void ResetCamera() => _camera.Reset(_vsComputer ? _humanSide : PieceSide.White);

    private void HandleClick(Point point)
    {
        if (_game.IsOver || _thinking != null || ComputerToMove)
            return;
        if (!_camera.TryPick(point, ClientSize, Renderer.BoardTopY, out var hit) || !Renderer.TrySquareAt(hit, out int file, out int rank))
            return;

        // The first move to the square: promotions come queen first, so a pawn promotes to a queen.
        int moveIndex = _legalMoves.FindIndex(move => move.ToFile == file && move.ToRank == rank);
        if (moveIndex >= 0)
        {
            MakeMove(_legalMoves[moveIndex]);
        }
        else if (_selected != (file, rank) && _game[file, rank]?.Side == _game.Turn)
        {
            _selected = (file, rank);
            _legalMoves = _game.LegalMovesFrom(file, rank);
        }
        else
        {
            ClearSelection(); // a click on the selected piece, or elsewhere
        }
    }

    private void ClearSelection()
    {
        _selected = null;
        _legalMoves = [];
    }

    private void MakeMove(BoardMove move)
    {
        if (!_game.TryMakeMove(move))
            return;

        ClearSelection();
        UpdateStatus();
        StartComputerMove();
    }

    // The search runs on a copy of the game in the background, so the window keeps drawing and answering.
    private async void StartComputerMove()
    {
        if (!ComputerToMove || _thinking != null)
            return;

        var thinking = new CancellationTokenSource();
        _thinking = thinking;
        UpdateStatus();

        ChessGame position = _game.Clone();
        int depth = _level + 1;
        BoardMove? move = null;
        try
        {
            move = await Task.Run(() => position.FindBestMove(depth, thinking.Token), thinking.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_thinking == thinking)
                _thinking = null;
            thinking.Dispose();
        }

        // A new game, or the computer turned off, while it was thinking: the move is no longer wanted.
        if (move is not { } bestMove || IsDisposed || _thinking != null || !ComputerToMove)
            return;

        _game.TryMakeMove(bestMove);
        ClearSelection();
        UpdateStatus();
        StartComputerMove();
    }

    private void StopThinking()
    {
        _thinking?.Cancel();
        _thinking = null;
    }

    private void UpdateStatus()
    {
        string turn = _game.Turn.ToString().ToUpperInvariant();
        string winner = ChessGame.Opposite(_game.Turn).ToString().ToUpperInvariant();
        string draw = _game.Draw switch
        {
            ChessGame.DrawReason.Repetition => "BY REPETITION",
            ChessGame.DrawReason.FiftyMoves => "BY 50 MOVE RULE",
            _ => "NO MATERIAL"
        };

        string status = _game.Status switch
        {
            ChessGame.GameStatus.Checkmate => $"CHECKMATE {winner} WINS",
            ChessGame.GameStatus.Stalemate => "STALEMATE DRAW",
            ChessGame.GameStatus.Draw => $"DRAW {draw}",
            _ when _thinking != null => "COMPUTER THINKING",
            ChessGame.GameStatus.Check => $"{turn} TO MOVE  CHECK",
            _ => $"{turn} TO MOVE"
        };
        _renderer?.SetStatus(status, _vsComputer ? $"VS COMPUTER LEVEL {_level}" : "TWO PLAYERS");

        string title = _game.Status switch
        {
            ChessGame.GameStatus.Check => $"Turn: {_game.Turn}  CHECK",
            ChessGame.GameStatus.Checkmate => $"CHECKMATE - {winner} WINS",
            ChessGame.GameStatus.Stalemate => "STALEMATE - DRAW",
            ChessGame.GameStatus.Draw => $"DRAW {draw}",
            _ => $"Turn: {_game.Turn}"
        };
        Text = $"{Title}   |   {title}";
    }
}
