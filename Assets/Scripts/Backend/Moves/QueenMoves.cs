using System.Collections.Generic;

public class QueenMoves
{
    public static void Generate(ref BoardState state, List<Move> moves)
    {
        // Heavy lifting done by bishop and rook. YEAH!!!!
        RookMoves.Generate(ref state, moves, useQueens: true);
        BishopMoves.Generate(ref state, moves, useQueens: true);
    }
}
