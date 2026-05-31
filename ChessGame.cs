namespace Chess3D;

internal sealed class ChessGame
{
    public enum GameStatus
    {
        Playing,
        Check,
        Checkmate,
        Stalemate
    }

    private readonly ChessPiece?[,] _board = new ChessPiece?[8, 8];
    private readonly Random _rng;

    public ChessGame(int? seed = null)
    {
        _rng = seed is { } s ? new Random(s) : new Random();
        NewGame();
    }

    public ChessPiece? this[int file, int rank] => _board[file, rank];
    public PieceSide Turn { get; private set; }
    public (int File, int Rank)? EnPassantTarget { get; private set; }
    public GameStatus Status { get; private set; }
    public bool IsOver => Status is GameStatus.Checkmate or GameStatus.Stalemate;

    public void NewGame()
    {
        for (int f = 0; f < 8; f++)
            for (int r = 0; r < 8; r++)
                _board[f, r] = null;

        Turn = PieceSide.White;
        EnPassantTarget = null;

        PieceKind[] backRank =
        {
            PieceKind.Rook, PieceKind.Knight, PieceKind.Bishop, PieceKind.Queen,
            PieceKind.King, PieceKind.Bishop, PieceKind.Knight, PieceKind.Rook
        };

        for (int file = 0; file < 8; file++)
        {
            _board[file, 0] = new ChessPiece(backRank[file], PieceSide.White, file, 0);
            _board[file, 1] = new ChessPiece(PieceKind.Pawn, PieceSide.White, file, 1);
            _board[file, 6] = new ChessPiece(PieceKind.Pawn, PieceSide.Black, file, 6);
            _board[file, 7] = new ChessPiece(backRank[file], PieceSide.Black, file, 7);
        }

        RecomputeStatus();
    }

    public IEnumerable<ChessPiece> Pieces()
    {
        for (int file = 0; file < 8; file++)
            for (int rank = 0; rank < 8; rank++)
                if (_board[file, rank] is { } piece)
                    yield return piece;
    }

    public List<BoardMove> LegalMoves(ChessPiece piece)
    {
        var result = new List<BoardMove>();
        foreach (BoardMove move in PseudoMoves(piece, includeCastling: true))
        {
            if (!WouldLeaveKingInCheck(piece, move))
                result.Add(move);
        }
        return result;
    }

    public List<BoardMove> AllLegalMoves(PieceSide side)
    {
        var result = new List<BoardMove>();
        foreach (ChessPiece piece in Pieces())
            if (piece.Side == side)
                result.AddRange(LegalMoves(piece));
        return result;
    }

    public bool TryMakeMove(BoardMove move)
    {
        if (_board[move.FromFile, move.FromRank] == null)
            return false;

        Apply(move);
        RecomputeStatus();
        return true;
    }

    private readonly struct MoveUndo(
        ChessPiece piece,
        int fromFile,
        int fromRank,
        int toFile,
        int toRank,
        bool pieceOldHasMoved,
        bool promoted,
        ChessPiece? captured,
        int capturedFile,
        int capturedRank,
        ChessPiece? rook,
        int rookFromFile,
        int rookFromRank,
        int rookToFile,
        int rookToRank,
        bool rookOldHasMoved,
        (int File, int Rank)? oldEnPassant,
        PieceSide oldTurn)
    {
        public readonly ChessPiece Piece = piece;
        public readonly int FromFile = fromFile;
        public readonly int FromRank = fromRank;
        public readonly int ToFile = toFile;
        public readonly int ToRank = toRank;
        public readonly bool PieceOldHasMoved = pieceOldHasMoved;
        public readonly bool Promoted = promoted;
        public readonly ChessPiece? Captured = captured;
        public readonly int CapturedFile = capturedFile;
        public readonly int CapturedRank = capturedRank;
        public readonly ChessPiece? Rook = rook;
        public readonly int RookFromFile = rookFromFile;
        public readonly int RookFromRank = rookFromRank;
        public readonly int RookToFile = rookToFile;
        public readonly int RookToRank = rookToRank;
        public readonly bool RookOldHasMoved = rookOldHasMoved;
        public readonly (int File, int Rank)? OldEnPassant = oldEnPassant;
        public readonly PieceSide OldTurn = oldTurn;
    }

    private MoveUndo Apply(BoardMove move)
    {
        ChessPiece piece = _board[move.FromFile, move.FromRank]!;
        (int File, int Rank)? oldEnPassant = EnPassantTarget;
        PieceSide oldTurn = Turn;
        bool oldHasMoved = piece.HasMoved;

        int capturedFile;
        int capturedRank;
        if (move.EnPassant)
        {
            capturedFile = move.ToFile;
            capturedRank = move.FromRank;
        }
        else
        {
            capturedFile = move.ToFile;
            capturedRank = move.ToRank;
        }

        ChessPiece? captured = _board[capturedFile, capturedRank];
        if (captured != null)
            _board[capturedFile, capturedRank] = null;

        _board[move.FromFile, move.FromRank] = null;
        _board[move.ToFile, move.ToRank] = piece;
        piece.File = move.ToFile;
        piece.Rank = move.ToRank;
        piece.HasMoved = true;

        bool promoted = false;
        if (move.Promotion is { } promo && piece.Kind == PieceKind.Pawn)
        {
            piece.Kind = promo;
            promoted = true;
        }

        ChessPiece? rook = null;
        int rookFromFile = -1, rookFromRank = -1, rookToFile = -1, rookToRank = -1;
        bool rookOldHasMoved = false;
        if (move.CastleKingSide || move.CastleQueenSide)
        {
            rookFromFile = move.CastleKingSide ? 7 : 0;
            rookFromRank = move.FromRank;
            rookToFile = move.CastleKingSide ? 5 : 3;
            rookToRank = move.FromRank;
            rook = _board[rookFromFile, rookFromRank];

            if (rook != null)
            {
                rookOldHasMoved = rook.HasMoved;
                _board[rookFromFile, rookFromRank] = null;
                _board[rookToFile, rookToRank] = rook;
                rook.File = rookToFile;
                rook.Rank = rookToRank;
                rook.HasMoved = true;
            }
        }

        if (piece.Kind == PieceKind.Pawn && Math.Abs(move.ToRank - move.FromRank) == 2)
            EnPassantTarget = (move.FromFile, (move.FromRank + move.ToRank) / 2);
        else
            EnPassantTarget = null;

        Turn = Opposite(Turn);

        return new MoveUndo(
            piece, move.FromFile, move.FromRank, move.ToFile, move.ToRank,
            oldHasMoved, promoted, captured, capturedFile, capturedRank,
            rook, rookFromFile, rookFromRank, rookToFile, rookToRank, rookOldHasMoved,
            oldEnPassant, oldTurn);
    }

    private void Undo(in MoveUndo u)
    {
        ChessPiece piece = u.Piece;

        _board[u.ToFile, u.ToRank] = null;
        _board[u.FromFile, u.FromRank] = piece;
        piece.File = u.FromFile;
        piece.Rank = u.FromRank;
        piece.HasMoved = u.PieceOldHasMoved;
        if (u.Promoted)
            piece.Kind = PieceKind.Pawn;

        if (u.Captured != null)
            _board[u.CapturedFile, u.CapturedRank] = u.Captured;

        if (u.Rook != null)
        {
            _board[u.RookToFile, u.RookToRank] = null;
            _board[u.RookFromFile, u.RookFromRank] = u.Rook;
            u.Rook.File = u.RookFromFile;
            u.Rook.Rank = u.RookFromRank;
            u.Rook.HasMoved = u.RookOldHasMoved;
        }

        EnPassantTarget = u.OldEnPassant;
        Turn = u.OldTurn;
    }

    private IEnumerable<BoardMove> PseudoMoves(ChessPiece piece, bool includeCastling)
    {
        int f = piece.File;
        int r = piece.Rank;

        switch (piece.Kind)
        {
            case PieceKind.Pawn:
            {
                int dir = piece.Side == PieceSide.White ? 1 : -1;
                int startRank = piece.Side == PieceSide.White ? 1 : 6;
                int one = r + dir;

                if (IsInside(f, one) && _board[f, one] == null)
                {
                    foreach (BoardMove m in PawnAdvance(f, r, f, one))
                        yield return m;

                    int two = r + dir * 2;
                    if (r == startRank && IsInside(f, two) && _board[f, two] == null)
                        yield return new BoardMove(f, r, f, two);
                }

                foreach (int df in PawnCaptureFiles)
                {
                    int tf = f + df;
                    int tr = r + dir;
                    if (!IsInside(tf, tr))
                        continue;

                    if (_board[tf, tr] is { } target && target.Side != piece.Side)
                    {
                        foreach (BoardMove m in PawnAdvance(f, r, tf, tr))
                            yield return m;
                    }
                    else if (EnPassantTarget is { } ep && ep.File == tf && ep.Rank == tr)
                    {
                        yield return new BoardMove(f, r, tf, tr, EnPassant: true);
                    }
                }
                break;
            }

            case PieceKind.Knight:
                foreach ((int df, int dr) in KnightOffsets)
                    if (CanLandOn(piece, f + df, r + dr))
                        yield return new BoardMove(f, r, f + df, r + dr);
                break;

            case PieceKind.Bishop:
                foreach (BoardMove m in RayMoves(piece, DiagonalDirs))
                    yield return m;
                break;

            case PieceKind.Rook:
                foreach (BoardMove m in RayMoves(piece, OrthogonalDirs))
                    yield return m;
                break;

            case PieceKind.Queen:
                foreach (BoardMove m in RayMoves(piece, AllDirs))
                    yield return m;
                break;

            case PieceKind.King:
                for (int df = -1; df <= 1; df++)
                    for (int dr = -1; dr <= 1; dr++)
                        if ((df != 0 || dr != 0) && CanLandOn(piece, f + df, r + dr))
                            yield return new BoardMove(f, r, f + df, r + dr);

                if (includeCastling)
                    foreach (BoardMove castle in CastlingMoves(piece))
                        yield return castle;
                break;
        }
    }

    private static IEnumerable<BoardMove> PawnAdvance(int fromFile, int fromRank, int toFile, int toRank)
    {
        if (toRank is 0 or 7)
        {
            yield return new BoardMove(fromFile, fromRank, toFile, toRank, Promotion: PieceKind.Queen);
            yield return new BoardMove(fromFile, fromRank, toFile, toRank, Promotion: PieceKind.Rook);
            yield return new BoardMove(fromFile, fromRank, toFile, toRank, Promotion: PieceKind.Bishop);
            yield return new BoardMove(fromFile, fromRank, toFile, toRank, Promotion: PieceKind.Knight);
        }
        else
        {
            yield return new BoardMove(fromFile, fromRank, toFile, toRank);
        }
    }

    private IEnumerable<BoardMove> RayMoves(ChessPiece piece, (int Df, int Dr)[] dirs)
    {
        foreach ((int df, int dr) in dirs)
        {
            int f = piece.File + df;
            int r = piece.Rank + dr;
            while (IsInside(f, r))
            {
                ChessPiece? target = _board[f, r];
                if (target == null)
                {
                    yield return new BoardMove(piece.File, piece.Rank, f, r);
                }
                else
                {
                    if (target.Side != piece.Side)
                        yield return new BoardMove(piece.File, piece.Rank, f, r);
                    break;
                }

                f += df;
                r += dr;
            }
        }
    }

    private IEnumerable<BoardMove> CastlingMoves(ChessPiece king)
    {
        if (king.HasMoved)
            yield break;

        int rank = king.Side == PieceSide.White ? 0 : 7;
        if (king.File != 4 || king.Rank != rank || IsKingInCheck(king.Side))
            yield break;

        PieceSide enemy = Opposite(king.Side);

        ChessPiece? rookK = _board[7, rank];
        if (rookK is { Kind: PieceKind.Rook, HasMoved: false } && rookK.Side == king.Side &&
            _board[5, rank] == null && _board[6, rank] == null &&
            !IsSquareAttacked(5, rank, enemy) && !IsSquareAttacked(6, rank, enemy))
        {
            yield return new BoardMove(4, rank, 6, rank, CastleKingSide: true);
        }

        ChessPiece? rookQ = _board[0, rank];
        if (rookQ is { Kind: PieceKind.Rook, HasMoved: false } && rookQ.Side == king.Side &&
            _board[1, rank] == null && _board[2, rank] == null && _board[3, rank] == null &&
            !IsSquareAttacked(3, rank, enemy) && !IsSquareAttacked(2, rank, enemy))
        {
            yield return new BoardMove(4, rank, 2, rank, CastleQueenSide: true);
        }
    }

    private bool CanLandOn(ChessPiece piece, int file, int rank)
    {
        if (!IsInside(file, rank))
            return false;
        ChessPiece? target = _board[file, rank];
        return target == null || target.Side != piece.Side;
    }

    private bool WouldLeaveKingInCheck(ChessPiece piece, BoardMove move)
    {
        MoveUndo undo = Apply(move);
        bool inCheck = IsKingInCheck(piece.Side);
        Undo(undo);
        return inCheck;
    }

    public bool IsKingInCheck(PieceSide side)
    {
        foreach (ChessPiece p in Pieces())
            if (p.Side == side && p.Kind == PieceKind.King)
                return IsSquareAttacked(p.File, p.Rank, Opposite(side));
        return false;
    }

    private bool IsSquareAttacked(int file, int rank, PieceSide bySide)
    {
        foreach (ChessPiece piece in Pieces())
        {
            if (piece.Side != bySide)
                continue;

            int df = file - piece.File;
            int dr = rank - piece.Rank;

            switch (piece.Kind)
            {
                case PieceKind.Pawn:
                {
                    int dir = piece.Side == PieceSide.White ? 1 : -1;
                    if (dr == dir && Math.Abs(df) == 1)
                        return true;
                    break;
                }

                case PieceKind.Knight:
                    if ((Math.Abs(df) == 1 && Math.Abs(dr) == 2) || (Math.Abs(df) == 2 && Math.Abs(dr) == 1))
                        return true;
                    break;

                case PieceKind.King:
                    if (Math.Abs(df) <= 1 && Math.Abs(dr) <= 1)
                        return true;
                    break;

                case PieceKind.Bishop:
                    if (Math.Abs(df) == Math.Abs(dr) && df != 0 && IsPathClear(piece.File, piece.Rank, file, rank))
                        return true;
                    break;

                case PieceKind.Rook:
                    if ((df == 0) != (dr == 0) && IsPathClear(piece.File, piece.Rank, file, rank))
                        return true;
                    break;

                case PieceKind.Queen:
                    if ((df == 0 || dr == 0 || Math.Abs(df) == Math.Abs(dr)) && (df != 0 || dr != 0) &&
                        IsPathClear(piece.File, piece.Rank, file, rank))
                        return true;
                    break;
            }
        }

        return false;
    }

    private bool IsPathClear(int fromFile, int fromRank, int toFile, int toRank)
    {
        int df = Math.Sign(toFile - fromFile);
        int dr = Math.Sign(toRank - fromRank);
        int f = fromFile + df;
        int r = fromRank + dr;

        while (f != toFile || r != toRank)
        {
            if (_board[f, r] != null)
                return false;
            f += df;
            r += dr;
        }

        return true;
    }

    private void RecomputeStatus()
    {
        bool hasMove = AllLegalMoves(Turn).Count > 0;
        bool inCheck = IsKingInCheck(Turn);

        Status = (hasMove, inCheck) switch
        {
            (false, true) => GameStatus.Checkmate,
            (false, false) => GameStatus.Stalemate,
            (true, true) => GameStatus.Check,
            _ => GameStatus.Playing
        };
    }

    private const int Infinity = 1_000_000_000;
    private const int MateScore = 1_000_000;

    public BoardMove? FindBestMove(PieceSide side, int depth)
    {
        List<BoardMove> moves = AllLegalMoves(side);
        if (moves.Count == 0)
            return null;

        OrderMoves(moves);

        int bestValue = -Infinity;
        var best = new List<BoardMove>();
        int alpha = -Infinity;

        foreach (BoardMove move in moves)
        {
            MoveUndo undo = Apply(move);
            int value = -Negamax(depth - 1, -Infinity, -alpha, 1);
            Undo(undo);

            if (value > bestValue)
            {
                bestValue = value;
                best.Clear();
                best.Add(move);
            }
            else if (value == bestValue)
            {
                best.Add(move);
            }

            if (bestValue > alpha)
                alpha = bestValue;
        }

        return best[_rng.Next(best.Count)];
    }

    private int Negamax(int depth, int alpha, int beta, int ply)
    {
        if (depth == 0)
            return EvaluateForSideToMove();

        List<BoardMove> moves = AllLegalMoves(Turn);

        if (moves.Count == 0)
        {
            if (IsKingInCheck(Turn))
                return -(MateScore - ply);
            return 0;
        }

        OrderMoves(moves);

        int best = -Infinity;
        foreach (BoardMove move in moves)
        {
            MoveUndo undo = Apply(move);
            int value = -Negamax(depth - 1, -beta, -alpha, ply + 1);
            Undo(undo);

            if (value > best)
                best = value;
            if (best > alpha)
                alpha = best;
            if (alpha >= beta)
                break;
        }

        return best;
    }

    private int EvaluateForSideToMove()
    {
        int whiteScore = 0;
        foreach (ChessPiece p in Pieces())
        {
            int value = PieceValue(p.Kind) + SquareBonus(p);
            whiteScore += p.Side == PieceSide.White ? value : -value;
        }

        return Turn == PieceSide.White ? whiteScore : -whiteScore;
    }

    private void OrderMoves(List<BoardMove> moves)
        => moves.Sort((a, b) => ScoreMove(b).CompareTo(ScoreMove(a)));

    private int ScoreMove(BoardMove m)
    {
        int score = 0;

        ChessPiece? victim = _board[m.ToFile, m.ToRank];
        if (victim != null)
            score += 10 * PieceValue(victim.Kind);

        ChessPiece? mover = _board[m.FromFile, m.FromRank];
        if (mover != null)
            score -= PieceValue(mover.Kind);

        if (m.Promotion is { } promo)
            score += PieceValue(promo);

        return score;
    }

    private static int PieceValue(PieceKind kind) => kind switch
    {
        PieceKind.Pawn => 100,
        PieceKind.Knight => 320,
        PieceKind.Bishop => 330,
        PieceKind.Rook => 500,
        PieceKind.Queen => 900,
        PieceKind.King => 20000,
        _ => 0
    };

    private static int SquareBonus(ChessPiece p)
    {
        float dx = p.File - 3.5f;
        float dy = p.Rank - 3.5f;
        int centre = (int)(14 - (Math.Abs(dx) + Math.Abs(dy)) * 2);

        return p.Kind switch
        {
            PieceKind.Pawn => centre / 2 + (p.Side == PieceSide.White ? p.Rank : 7 - p.Rank) * 4,
            PieceKind.Knight => centre,
            PieceKind.Bishop => centre / 2,
            PieceKind.King => -centre / 2,
            _ => 0
        };
    }

    private static readonly int[] PawnCaptureFiles = [-1, 1];
    private static readonly (int Df, int Dr)[] KnightOffsets = [(1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2)];
    private static readonly (int Df, int Dr)[] DiagonalDirs = [(1, 1), (1, -1), (-1, 1), (-1, -1)];
    private static readonly (int Df, int Dr)[] OrthogonalDirs = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    private static readonly (int Df, int Dr)[] AllDirs = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];
    public static bool IsInside(int file, int rank) => file is >= 0 and < 8 && rank is >= 0 and < 8;
    public static PieceSide Opposite(PieceSide side) => side == PieceSide.White ? PieceSide.Black : PieceSide.White;
}
