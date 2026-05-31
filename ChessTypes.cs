namespace Chess3D;

internal enum PieceKind
{
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King
}

internal enum PieceSide
{
    White,
    Black
}

internal sealed class ChessPiece(PieceKind kind, PieceSide side, int file, int rank)
{
    public PieceKind Kind { get; set; } = kind;
    public PieceSide Side { get; } = side;
    public int File { get; set; } = file;
    public int Rank { get; set; } = rank;
    public bool HasMoved { get; set; }
}

internal readonly record struct BoardMove(
    int FromFile,
    int FromRank,
    int ToFile,
    int ToRank,
    bool CastleKingSide = false,
    bool CastleQueenSide = false,
    bool EnPassant = false,
    PieceKind? Promotion = null);
