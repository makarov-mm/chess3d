using System.Runtime.InteropServices;

namespace Chess3D;

/// <summary>
/// The rules of chess, with castling, en passant, promotion and the draws by repetition, by the
/// fifty-move rule and for lack of material, and the computer player: a fixed-depth alpha-beta search.
/// </summary>
internal sealed class ChessGame
{
    public enum GameStatus
    {
        Playing,
        Check,
        Checkmate,
        Stalemate,
        Draw
    }

    public enum DrawReason
    {
        None,
        Repetition,
        FiftyMoves,
        InsufficientMaterial
    }

    [Flags]
    private enum Castling
    {
        None = 0,
        WhiteKingSide = 1,
        WhiteQueenSide = 2,
        BlackKingSide = 4,
        BlackQueenSide = 8,
        All = 15
    }

    // What taking a move back needs.
    private readonly record struct Undo(
        BoardMove Move,
        ChessPiece Moved,
        ChessPiece? Captured,
        int CapturedSquare,
        Castling Castling,
        int EnPassant,
        int QuietPlies);

    private const int Infinity = 1_000_000_000;
    private const int MateScore = 1_000_000;

    private static readonly PieceKind[] BackRank =
    [
        PieceKind.Rook, PieceKind.Knight, PieceKind.Bishop, PieceKind.Queen,
        PieceKind.King, PieceKind.Bishop, PieceKind.Knight, PieceKind.Rook
    ];
    private static readonly PieceKind[] Promotions = [PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight];
    private static readonly (int Df, int Dr)[] KnightSteps = [(1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2)];
    private static readonly (int Df, int Dr)[] KingSteps = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];
    private static readonly (int Df, int Dr)[] DiagonalSteps = [(1, 1), (1, -1), (-1, 1), (-1, -1)];
    private static readonly (int Df, int Dr)[] StraightSteps = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    private readonly ChessPiece?[] _board = new ChessPiece?[64]; // by square: rank * 8 + file
    private readonly int[] _kings = new int[2];                  // the squares of the kings, by side
    private Castling _castling;
    private int _enPassant;  // the square a pawn has just passed over, or -1
    private int _quietPlies; // since the last capture or pawn move
    private readonly List<string> _positions = []; // since the last capture or pawn move
    private readonly Random _random;
    private readonly List<List<BoardMove>> _moveLists = []; // the search's, one per ply
    private long _nodes;

    public ChessGame(int? seed = null)
    {
        _random = seed is { } value ? new Random(value) : new Random();
        NewGame();
    }

    private ChessGame(ChessGame other)
    {
        Array.Copy(other._board, _board, _board.Length);
        Array.Copy(other._kings, _kings, _kings.Length);
        _castling = other._castling;
        _enPassant = other._enPassant;
        _quietPlies = other._quietPlies;
        _positions.AddRange(other._positions);
        _random = new Random(other._random.Next());
        Turn = other.Turn;
        Status = other.Status;
        Draw = other.Draw;
    }

    public ChessPiece? this[int file, int rank] => _board[Square(file, rank)];
    public PieceSide Turn { get; private set; }
    public GameStatus Status { get; private set; }
    public DrawReason Draw { get; private set; }
    public bool IsOver => Status is GameStatus.Checkmate or GameStatus.Stalemate or GameStatus.Draw;

    public static bool IsInside(int file, int rank) => file is >= 0 and < 8 && rank is >= 0 and < 8;
    public static PieceSide Opposite(PieceSide side) => side == PieceSide.White ? PieceSide.Black : PieceSide.White;

    /// <summary>A copy, for searching on another thread.</summary>
    public ChessGame Clone() => new(this);

    public void NewGame()
    {
        Array.Clear(_board);
        for (int file = 0; file < 8; file++)
        {
            _board[Square(file, 0)] = new ChessPiece(BackRank[file], PieceSide.White);
            _board[Square(file, 1)] = new ChessPiece(PieceKind.Pawn, PieceSide.White);
            _board[Square(file, 6)] = new ChessPiece(PieceKind.Pawn, PieceSide.Black);
            _board[Square(file, 7)] = new ChessPiece(BackRank[file], PieceSide.Black);
        }

        _kings[(int)PieceSide.White] = Square(4, 0);
        _kings[(int)PieceSide.Black] = Square(4, 7);
        _castling = Castling.All;
        _enPassant = -1;
        _quietPlies = 0;
        Turn = PieceSide.White;
        _positions.Clear();
        _positions.Add(PositionKey());
        UpdateStatus();
    }

    /// <summary>The legal moves of the piece on a square, if it is that side's turn.</summary>
    public List<BoardMove> LegalMovesFrom(int file, int rank)
    {
        var moves = new List<BoardMove>();
        int square = Square(file, rank);
        if (_board[square] is { } piece && piece.Side == Turn)
        {
            AddPseudoMoves(square, piece, moves);
            moves.RemoveAll(move => !IsLegal(move));
        }
        return moves;
    }

    /// <summary>Plays the move if it is legal.</summary>
    public bool TryMakeMove(BoardMove move)
    {
        if (IsOver || !IsInside(move.FromFile, move.FromRank) || !LegalMovesFrom(move.FromFile, move.FromRank).Contains(move))
            return false;

        Make(move);
        if (_quietPlies == 0)
            _positions.Clear(); // the earlier positions cannot come back
        _positions.Add(PositionKey());
        UpdateStatus();
        return true;
    }

    // Moves and their legality ------------------------------------------------------------------

    private static int Square(int file, int rank) => rank * 8 + file;
    private static int FileOf(int square) => square & 7;
    private static int RankOf(int square) => square >> 3;

    private bool IsEmpty(int file, int rank) => _board[Square(file, rank)] == null;

    // Whether the mover's king is safe after the move.
    private bool IsLegal(BoardMove move)
    {
        PieceSide side = Turn;
        Undo undo = Make(move);
        bool legal = !IsAttacked(_kings[(int)side], Opposite(side));
        Unmake(undo);
        return legal;
    }

    private bool InCheck(PieceSide side) => IsAttacked(_kings[(int)side], Opposite(side));

    // The moves of the side to move by the movement rules; some may leave its king in check.
    private void GenerateMoves(List<BoardMove> moves)
    {
        for (int square = 0; square < 64; square++)
        {
            if (_board[square] is { } piece && piece.Side == Turn)
                AddPseudoMoves(square, piece, moves);
        }
    }

    private void AddPseudoMoves(int square, ChessPiece piece, List<BoardMove> moves)
    {
        int file = FileOf(square);
        int rank = RankOf(square);

        switch (piece.Kind)
        {
            case PieceKind.Pawn:
                AddPawnMoves(file, rank, piece.Side, moves);
                break;

            case PieceKind.Knight:
                foreach ((int df, int dr) in KnightSteps)
                    AddStep(file, rank, file + df, rank + dr, piece.Side, moves);
                break;

            case PieceKind.Bishop:
                AddSlides(file, rank, piece.Side, DiagonalSteps, moves);
                break;

            case PieceKind.Rook:
                AddSlides(file, rank, piece.Side, StraightSteps, moves);
                break;

            case PieceKind.Queen:
                AddSlides(file, rank, piece.Side, StraightSteps, moves);
                AddSlides(file, rank, piece.Side, DiagonalSteps, moves);
                break;

            case PieceKind.King:
                foreach ((int df, int dr) in KingSteps)
                    AddStep(file, rank, file + df, rank + dr, piece.Side, moves);
                AddCastling(piece.Side, moves);
                break;
        }
    }

    private void AddPawnMoves(int file, int rank, PieceSide side, List<BoardMove> moves)
    {
        int forward = side == PieceSide.White ? 1 : -1;
        int next = rank + forward;
        if (!IsInside(file, next))
            return;

        if (IsEmpty(file, next))
        {
            AddPawnMove(file, rank, file, next, moves);
            int start = side == PieceSide.White ? 1 : 6;
            if (rank == start && IsEmpty(file, next + forward))
                moves.Add(new BoardMove(file, rank, file, next + forward));
        }

        for (int toFile = file - 1; toFile <= file + 1; toFile += 2)
        {
            if (!IsInside(toFile, next))
                continue;

            int target = Square(toFile, next);
            if (_board[target] is { } victim && victim.Side != side)
                AddPawnMove(file, rank, toFile, next, moves);
            else if (target == _enPassant)
                moves.Add(new BoardMove(file, rank, toFile, next, EnPassant: true));
        }
    }

    // A pawn reaching the last rank promotes; the queen comes first, so the player's pawns promote to queens.
    private static void AddPawnMove(int fromFile, int fromRank, int toFile, int toRank, List<BoardMove> moves)
    {
        if (toRank is 0 or 7)
        {
            foreach (PieceKind promotion in Promotions)
                moves.Add(new BoardMove(fromFile, fromRank, toFile, toRank, Promotion: promotion));
        }
        else
        {
            moves.Add(new BoardMove(fromFile, fromRank, toFile, toRank));
        }
    }

    private void AddStep(int file, int rank, int toFile, int toRank, PieceSide side, List<BoardMove> moves)
    {
        if (IsInside(toFile, toRank) && _board[Square(toFile, toRank)]?.Side != side)
            moves.Add(new BoardMove(file, rank, toFile, toRank));
    }

    private void AddSlides(int file, int rank, PieceSide side, (int Df, int Dr)[] steps, List<BoardMove> moves)
    {
        foreach ((int df, int dr) in steps)
        {
            for (int f = file + df, r = rank + dr; IsInside(f, r); f += df, r += dr)
            {
                ChessPiece? target = _board[Square(f, r)];
                if (target?.Side != side)
                    moves.Add(new BoardMove(file, rank, f, r));
                if (target != null)
                    break;
            }
        }
    }

    // The king may not castle out of, through or into check. The rights are lost when the king or the
    // rook moves, or the rook is taken, so with a right the king and the rook stand on their squares.
    private void AddCastling(PieceSide side, List<BoardMove> moves)
    {
        int rank = side == PieceSide.White ? 0 : 7;
        (Castling kingSide, Castling queenSide) = side == PieceSide.White
            ? (Castling.WhiteKingSide, Castling.WhiteQueenSide)
            : (Castling.BlackKingSide, Castling.BlackQueenSide);
        PieceSide enemy = Opposite(side);

        if ((_castling & (kingSide | queenSide)) == 0 || IsAttacked(Square(4, rank), enemy))
            return;

        if ((_castling & kingSide) != 0 && IsEmpty(5, rank) && IsEmpty(6, rank) &&
            !IsAttacked(Square(5, rank), enemy) && !IsAttacked(Square(6, rank), enemy))
        {
            moves.Add(new BoardMove(4, rank, 6, rank, CastleKingSide: true));
        }

        if ((_castling & queenSide) != 0 && IsEmpty(1, rank) && IsEmpty(2, rank) && IsEmpty(3, rank) &&
            !IsAttacked(Square(3, rank), enemy) && !IsAttacked(Square(2, rank), enemy))
        {
            moves.Add(new BoardMove(4, rank, 2, rank, CastleQueenSide: true));
        }
    }

    // Whether a piece of the side `by` attacks the square.
    private bool IsAttacked(int square, PieceSide by)
    {
        int file = FileOf(square);
        int rank = RankOf(square);

        int pawnRank = rank - (by == PieceSide.White ? 1 : -1);
        if (Holds(file - 1, pawnRank, PieceKind.Pawn, by) || Holds(file + 1, pawnRank, PieceKind.Pawn, by))
            return true;

        foreach ((int df, int dr) in KnightSteps)
        {
            if (Holds(file + df, rank + dr, PieceKind.Knight, by))
                return true;
        }

        foreach ((int df, int dr) in KingSteps)
        {
            if (Holds(file + df, rank + dr, PieceKind.King, by))
                return true;
        }

        return IsAttackedAlong(file, rank, by, DiagonalSteps, PieceKind.Bishop) ||
               IsAttackedAlong(file, rank, by, StraightSteps, PieceKind.Rook);
    }

    private bool Holds(int file, int rank, PieceKind kind, PieceSide side) =>
        IsInside(file, rank) && _board[Square(file, rank)] == new ChessPiece(kind, side);

    // Whether the first piece in one of the directions is a queen or the given slider of the side `by`.
    private bool IsAttackedAlong(int file, int rank, PieceSide by, (int Df, int Dr)[] steps, PieceKind slider)
    {
        foreach ((int df, int dr) in steps)
        {
            for (int f = file + df, r = rank + dr; IsInside(f, r); f += df, r += dr)
            {
                if (_board[Square(f, r)] is not { } piece)
                    continue;
                if (piece.Side == by && (piece.Kind == slider || piece.Kind == PieceKind.Queen))
                    return true;
                break;
            }
        }
        return false;
    }

    private Undo Make(BoardMove move)
    {
        int from = Square(move.FromFile, move.FromRank);
        int to = Square(move.ToFile, move.ToRank);
        ChessPiece moved = _board[from]!.Value;
        int capturedSquare = move.EnPassant ? Square(move.ToFile, move.FromRank) : to;
        ChessPiece? captured = _board[capturedSquare];
        var undo = new Undo(move, moved, captured, capturedSquare, _castling, _enPassant, _quietPlies);

        _board[capturedSquare] = null;
        _board[from] = null;
        _board[to] = move.Promotion is { } promotion ? moved with { Kind = promotion } : moved;
        if (moved.Kind == PieceKind.King)
            _kings[(int)moved.Side] = to;

        if (move.CastleKingSide || move.CastleQueenSide)
        {
            (int rookFrom, int rookTo) = RookCastling(move);
            _board[rookTo] = _board[rookFrom];
            _board[rookFrom] = null;
        }

        _castling &= ~(CastlingLostAt(from) | CastlingLostAt(to));
        _enPassant = moved.Kind == PieceKind.Pawn && Math.Abs(move.ToRank - move.FromRank) == 2
            ? Square(move.FromFile, (move.FromRank + move.ToRank) / 2)
            : -1;
        _quietPlies = moved.Kind == PieceKind.Pawn || captured != null ? 0 : _quietPlies + 1;
        Turn = Opposite(Turn);
        return undo;
    }

    private void Unmake(in Undo undo)
    {
        BoardMove move = undo.Move;
        int from = Square(move.FromFile, move.FromRank);
        int to = Square(move.ToFile, move.ToRank);

        _board[to] = null;
        _board[undo.CapturedSquare] = undo.Captured;
        _board[from] = undo.Moved;
        if (undo.Moved.Kind == PieceKind.King)
            _kings[(int)undo.Moved.Side] = from;

        if (move.CastleKingSide || move.CastleQueenSide)
        {
            (int rookFrom, int rookTo) = RookCastling(move);
            _board[rookFrom] = _board[rookTo];
            _board[rookTo] = null;
        }

        _castling = undo.Castling;
        _enPassant = undo.EnPassant;
        _quietPlies = undo.QuietPlies;
        Turn = Opposite(Turn);
    }

    private static (int From, int To) RookCastling(BoardMove move) => move.CastleKingSide
        ? (Square(7, move.FromRank), Square(5, move.FromRank))
        : (Square(0, move.FromRank), Square(3, move.FromRank));

    // A move from or to the home square of a king or a rook ends the castling it is part of.
    private static Castling CastlingLostAt(int square) => square switch
    {
        4 => Castling.WhiteKingSide | Castling.WhiteQueenSide,
        7 => Castling.WhiteKingSide,
        0 => Castling.WhiteQueenSide,
        60 => Castling.BlackKingSide | Castling.BlackQueenSide,
        63 => Castling.BlackKingSide,
        56 => Castling.BlackQueenSide,
        _ => Castling.None
    };

    // The end of the game -------------------------------------------------------------------------

    private void UpdateStatus()
    {
        bool inCheck = InCheck(Turn);
        Draw = DrawReason.None;

        if (!HasLegalMove())
            Status = inCheck ? GameStatus.Checkmate : GameStatus.Stalemate;
        else if ((Draw = FindDraw()) != DrawReason.None)
            Status = GameStatus.Draw;
        else
            Status = inCheck ? GameStatus.Check : GameStatus.Playing;
    }

    private bool HasLegalMove()
    {
        var moves = new List<BoardMove>();
        GenerateMoves(moves);
        return moves.Exists(IsLegal);
    }

    private DrawReason FindDraw()
    {
        if (!HasMatingMaterial())
            return DrawReason.InsufficientMaterial;

        string position = _positions[^1];
        if (_positions.Count(key => key == position) >= 3)
            return DrawReason.Repetition;

        return _quietPlies >= 100 ? DrawReason.FiftyMoves : DrawReason.None;
    }

    // No mate is possible with the kings and a single minor piece, or with bishops all on one color.
    private bool HasMatingMaterial()
    {
        int knights = 0;
        int bishops = 0;
        int bishopColors = 0;

        for (int square = 0; square < 64; square++)
        {
            switch (_board[square]?.Kind)
            {
                case PieceKind.Pawn or PieceKind.Rook or PieceKind.Queen:
                    return true;
                case PieceKind.Knight:
                    knights++;
                    break;
                case PieceKind.Bishop:
                    bishops++;
                    bishopColors |= 1 << ((FileOf(square) + RankOf(square)) & 1);
                    break;
            }
        }

        return knights + bishops > 1 && (knights > 0 || bishopColors == 3);
    }

    // The same positions have the same pieces on the same squares, the same side to move, and the same
    // castling and en passant captures possible.
    private string PositionKey()
    {
        Span<char> key = stackalloc char[67];
        for (int square = 0; square < 64; square++)
            key[square] = _board[square] is { } piece ? "PNBRQKpnbrqk"[(int)piece.Kind + 6 * (int)piece.Side] : '.';

        key[64] = Turn == PieceSide.White ? 'w' : 'b';
        key[65] = (char)('A' + (int)_castling);
        key[66] = CanCaptureEnPassant() ? (char)('a' + FileOf(_enPassant)) : '-';
        return new string(key);
    }

    private bool CanCaptureEnPassant()
    {
        if (_enPassant < 0)
            return false;

        int file = FileOf(_enPassant);
        int pawnRank = RankOf(_enPassant) - (Turn == PieceSide.White ? 1 : -1);
        return Holds(file - 1, pawnRank, PieceKind.Pawn, Turn) || Holds(file + 1, pawnRank, PieceKind.Pawn, Turn);
    }

    // The computer player -------------------------------------------------------------------------

    /// <summary>
    /// The best move for the side to move by a search <paramref name="depth"/> half-moves deep; a random one
    /// of the best when there are several. Null when there is no legal move.
    /// </summary>
    public BoardMove? FindBestMove(int depth, CancellationToken cancel = default)
    {
        List<BoardMove> moves = MoveList(0);
        GenerateMoves(moves);
        moves.RemoveAll(move => !IsLegal(move));
        if (moves.Count == 0)
            return null;

        OrderMoves(moves);
        var best = new List<BoardMove>();
        int bestValue = -Infinity;

        foreach (BoardMove move in moves)
        {
            Undo undo = Make(move);
            // The window starts just below the best value: a move as good gets its exact value,
            // a worse one is cut off below it and cannot pass for a tie.
            int value = -Negamax(depth - 1, -Infinity, -(bestValue - 1), 1, cancel);
            Unmake(undo);

            if (value > bestValue)
            {
                bestValue = value;
                best.Clear();
            }
            if (value == bestValue)
                best.Add(move);
        }

        return best[_random.Next(best.Count)];
    }

    private int Negamax(int depth, int alpha, int beta, int ply, CancellationToken cancel)
    {
        if (depth == 0)
            return Evaluate();
        if ((++_nodes & 4095) == 0)
            cancel.ThrowIfCancellationRequested();

        List<BoardMove> moves = MoveList(ply);
        GenerateMoves(moves);
        OrderMoves(moves);

        PieceSide side = Turn;
        int best = -Infinity;
        bool anyLegal = false;

        foreach (BoardMove move in moves)
        {
            Undo undo = Make(move);
            if (InCheck(side))
            {
                Unmake(undo);
                continue;
            }

            anyLegal = true;
            int value = -Negamax(depth - 1, -beta, -alpha, ply + 1, cancel);
            Unmake(undo);

            best = Math.Max(best, value);
            alpha = Math.Max(alpha, best);
            if (alpha >= beta)
                break;
        }

        if (!anyLegal)
            return InCheck(side) ? -(MateScore - ply) : 0; // a quicker mate is a better one
        return best;
    }

    private List<BoardMove> MoveList(int ply)
    {
        while (_moveLists.Count <= ply)
            _moveLists.Add([]);
        List<BoardMove> moves = _moveLists[ply];
        moves.Clear();
        return moves;
    }

    // Captures of valuable pieces by cheap ones, and promotions, first: the cutoffs come sooner.
    private void OrderMoves(List<BoardMove> moves)
    {
        Span<BoardMove> span = CollectionsMarshal.AsSpan(moves);
        Span<int> keys = span.Length <= 256 ? stackalloc int[span.Length] : new int[span.Length];
        for (int i = 0; i < span.Length; i++)
            keys[i] = -ScoreMove(span[i]);
        keys.Sort(span);
    }

    private int ScoreMove(BoardMove move)
    {
        int score = -PieceValue(_board[Square(move.FromFile, move.FromRank)]!.Value.Kind);
        if (_board[Square(move.ToFile, move.ToRank)] is { } victim)
            score += 10 * PieceValue(victim.Kind);
        else if (move.EnPassant)
            score += 10 * PieceValue(PieceKind.Pawn);
        if (move.Promotion is { } promotion)
            score += PieceValue(promotion);
        return score;
    }

    // Material and a little positional sense, for the side to move.
    private int Evaluate()
    {
        int white = 0;
        for (int square = 0; square < 64; square++)
        {
            if (_board[square] is not { } piece)
                continue;
            int value = PieceValue(piece.Kind) + SquareBonus(piece, FileOf(square), RankOf(square));
            white += piece.Side == PieceSide.White ? value : -value;
        }
        return Turn == PieceSide.White ? white : -white;
    }

    private static int PieceValue(PieceKind kind) => kind switch
    {
        PieceKind.Pawn => 100,
        PieceKind.Knight => 320,
        PieceKind.Bishop => 330,
        PieceKind.Rook => 500,
        PieceKind.Queen => 900,
        _ => 20000
    };

    // Knights and bishops like the centre, pawns like advancing, the king likes shelter.
    private static int SquareBonus(ChessPiece piece, int file, int rank)
    {
        float dx = file - 3.5f;
        float dy = rank - 3.5f;
        int centre = (int)(14 - (Math.Abs(dx) + Math.Abs(dy)) * 2);

        return piece.Kind switch
        {
            PieceKind.Pawn => centre / 2 + (piece.Side == PieceSide.White ? rank : 7 - rank) * 4,
            PieceKind.Knight => centre,
            PieceKind.Bishop => centre / 2,
            PieceKind.King => -centre / 2,
            _ => 0
        };
    }
}
