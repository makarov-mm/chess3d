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

internal readonly record struct ChessPiece(PieceKind Kind, PieceSide Side);

/// <summary>A move; files and ranks count from 0 (a1 is file 0, rank 0).</summary>
internal readonly record struct BoardMove(
    int FromFile,
    int FromRank,
    int ToFile,
    int ToRank,
    bool CastleKingSide = false,
    bool CastleQueenSide = false,
    bool EnPassant = false,
    PieceKind? Promotion = null);
