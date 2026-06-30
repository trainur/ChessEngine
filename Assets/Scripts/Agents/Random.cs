using System;

public class RandomAgent : ChessAgent
{
    private readonly Random rng = new Random();

    protected override SearchResult ChooseMove(BoardState state)
    {
        var moves = MoveGenerator.GenerateMoves(ref state);
        return new SearchResult(
            moves[rng.Next(moves.Count)],
            null,
            0);
    }
}