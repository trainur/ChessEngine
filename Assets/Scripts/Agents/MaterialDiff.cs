using System.Collections.Generic;
using UnityEngine;

public class MaterialDiff : ChessAgent
{
    [SerializeField, Range(1, 15)] private int searchDepth = 5;
    public override int? SearchDepth => searchDepth;

    private readonly Dictionary<PieceType, int> PieceValues = new()
    {
        { PieceType.Pawn, 100 },
        { PieceType.Knight, 300 },
        { PieceType.Bishop, 350 },
        { PieceType.Rook, 500 },
        { PieceType.Queen, 900 },
        { PieceType.King, int.MaxValue / 2 } // Divide by two to avoid over/underflow complications
    };

    protected override SearchResult ChooseMove(BoardState state)
    {
        Move? bestMove = null;
        float bestScore = IsWhite ? float.NegativeInfinity : float.PositiveInfinity;

        foreach (Move move in MoveGenerator.GenerateMoves(ref state))
        {
            BoardState child = state;
            child.ApplyMove(move);

            float score = Minimax(child, searchDepth - 1);

            if ((IsWhite && score > bestScore) || (!IsWhite && score < bestScore))
            {
                bestScore = score;
                bestMove = move;
            }
        }

        if (bestMove == null) throw new System.InvalidOperationException("No move was found!");
        return new SearchResult(
            bestMove.Value,
            (int)bestScore,
            evaluatedStates);
    }

    private float Minimax(BoardState state, int depth)
    {
        if (depth == 0 || state.IsTerminalState()) return EvaluateState(state);

        float best = state.IsWhiteTurn ? float.NegativeInfinity : float.PositiveInfinity;

        foreach (Move move in MoveGenerator.GenerateMoves(ref state))
        {
            BoardState child = state;
            child.ApplyMove(move);

            float score = Minimax(child, depth - 1);
            best = state.IsWhiteTurn ? Mathf.Max(best, score) : Mathf.Min(best, score);
        }
        return best;
    }

    private float EvaluateState(BoardState state)
    {
        evaluatedStates++;

        if (state.IsTerminalState())
        {
            if (state.IsMate())
            {
                // The side to move is checkmated
                return state.IsWhiteTurn ? float.MinValue : float.MaxValue;
            }
            else
            {
                // Draw
                return 0f;
            }
        }

        float whiteMaterialScore =
            (PieceValues[PieceType.Pawn] * BitUtils.PopCount(state.WhitePawns)) +
            (PieceValues[PieceType.Knight] * BitUtils.PopCount(state.WhiteKnights)) +
            (PieceValues[PieceType.Bishop] * BitUtils.PopCount(state.WhiteBishops)) +
            (PieceValues[PieceType.Rook] * BitUtils.PopCount(state.WhiteRooks)) +
            (PieceValues[PieceType.Queen] * BitUtils.PopCount(state.WhiteQueens));
        float blackMaterialScore =
            ((PieceValues[PieceType.Pawn] * BitUtils.PopCount(state.BlackPawns)) +
            (PieceValues[PieceType.Knight] * BitUtils.PopCount(state.BlackKnights)) +
            (PieceValues[PieceType.Bishop] * BitUtils.PopCount(state.BlackBishops)) +
            (PieceValues[PieceType.Rook] * BitUtils.PopCount(state.BlackRooks)) +
            (PieceValues[PieceType.Queen] * BitUtils.PopCount(state.BlackQueens)));

        return whiteMaterialScore - blackMaterialScore;
    }
}