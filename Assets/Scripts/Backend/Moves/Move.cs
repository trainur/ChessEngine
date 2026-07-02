public struct Move
{
    public int From { get; }
    public int To { get; }
    public PieceType Piece { get; }
    public PieceType? CapturePiece { get; }
    public MoveFlag? Flag { get; }

    public Move(int from, int to, PieceType piece, PieceType? capturePiece = null, MoveFlag? flag = null)
    {
        From = from;
        To = to;
        Piece = piece;
        CapturePiece = capturePiece;
        Flag = flag;
    }

    public bool IsPromotion()
    {
        return Flag.HasValue &&
            (Flag == MoveFlag.PromoteKnight
            || Flag == MoveFlag.PromoteBishop
            || Flag == MoveFlag.PromoteRook
            || Flag == MoveFlag.PromoteQueen);
    }

    public bool IsCastle()
    {
        return Flag.HasValue &&
            (Flag == MoveFlag.CastleKingside
            || Flag == MoveFlag.CastleQueenside);
    }

    public override string ToString()
    {
        string SquareToAlgebraic(int sq)
        {
            char file = (char)('a' + (sq % 8));
            int rank = (sq / 8) + 1;
            return $"{file}{rank}";
        }

        return $"{Piece} {SquareToAlgebraic(From)} -> {SquareToAlgebraic(To)} ({Flag})";
    }
}

public enum MoveFlag
{
    DoublePawnPush,
    EnPassant,
    CastleKingside,
    CastleQueenside,
    PromoteQueen,
    PromoteRook,
    PromoteBishop,
    PromoteKnight,
    Draw
}

public enum PieceType
{
    Pawn = 0,
    Knight = 1,
    Bishop = 2,
    Rook = 3,
    Queen = 4,
    King = 5
}