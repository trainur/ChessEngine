using System.Collections.Generic;

public class QueenMoves
{
    public static void Generate(BoardState state, List<Move> moves)
    {
        // Heavy lifting done by bishop and rook. YEAH!!!!
        RookMoves.Generate(state, moves, useQueens: true);
        BishopMoves.Generate(state, moves, useQueens: true);
    }
}
