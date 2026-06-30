using System.Collections.Generic;
using UnityEngine;

public class MaterialDiffPruning : ChessAgent
{
    [SerializeField, Range(1,15)] private int searchDepth = 5;
    public override int? SearchDepth => searchDepth;

    private const float PAWN_SCORE = 100;
    private const float KNIGHT_SCORE = 100;
    private const float BISHOP_SCORE = 100;
    private const float ROOK_SCORE = 100;
    private const float QUEEN_SCORE = 100;
    private const float KING_SCORE = float.PositiveInfinity;

    protected override SearchResult ChooseMove(BoardState state)
    {
        Move? bestMove = default;
        float bestScore = IsWhite ? float.NegativeInfinity : float.PositiveInfinity;

        foreach (Move move in MoveGenerator.GenerateMoves(ref state))
        {
            BoardState child = state;
            child.ApplyMove(move);

            float score = Minimax(child, searchDepth - 1, float.MinValue, float.MaxValue);

            if ((IsWhite && score > bestScore) || (!IsWhite &&  score < bestScore))
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

    private float Minimax(BoardState state, int depth, float alpha, float beta)
    {
        if (depth == 0 || state.IsTerminalState()) return EvaluateState(state);

        float best = state.IsWhiteTurn ? float.NegativeInfinity : float.PositiveInfinity;

        foreach (Move move in MoveGenerator.GenerateMoves(ref state))
        {
            BoardState child = state;
            child.ApplyMove(move);

            float score = Minimax(child, depth - 1, alpha, beta);
            best = state.IsWhiteTurn ? Mathf.Max(best, score) : Mathf.Min(best, score);

            if (state.IsWhiteTurn)
            {
                alpha = Mathf.Max(alpha, best);
                if (beta <= alpha) break;
            }
            else
            {
                beta = Mathf.Min(beta, best);
                if (beta <= alpha) break;
            }
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
            (PAWN_SCORE * BitUtils.PopCount(state.WhitePawns)) +
            (KNIGHT_SCORE * BitUtils.PopCount(state.WhiteKnights)) +
            (BISHOP_SCORE * BitUtils.PopCount(state.WhiteBishops)) +
            (ROOK_SCORE * BitUtils.PopCount(state.WhiteRooks)) +
            (QUEEN_SCORE * BitUtils.PopCount(state.WhiteQueens));
        float blackMaterialScore = 
            (PAWN_SCORE * BitUtils.PopCount(state.BlackPawns)) +
            (KNIGHT_SCORE * BitUtils.PopCount(state.BlackKnights)) +
            (BISHOP_SCORE * BitUtils.PopCount(state.BlackBishops)) +
            (ROOK_SCORE * BitUtils.PopCount(state.BlackRooks)) +
            (QUEEN_SCORE * BitUtils.PopCount(state.BlackQueens));

        return whiteMaterialScore - blackMaterialScore;
    }
}