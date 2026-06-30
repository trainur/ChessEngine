using System.Collections.Generic;
using UnityEngine;

// IMPLEMENTS BETTER BITBOARD ENGINE FUNCTIONS E.G. UNMAKEMOVE

public class MaterialDiffPruningV2 : ChessAgent
{
    [SerializeField, Range(1,15)] private int searchDepth = 5;
    public override int? SearchDepth => searchDepth;

    private const float PAWN_SCORE = 100;
    private const float KNIGHT_SCORE = 300;
    private const float BISHOP_SCORE = 350;
    private const float ROOK_SCORE = 500;
    private const float QUEEN_SCORE = 900;

    protected override SearchResult ChooseMove(BoardState state)
    {
        Move? bestMove = null;
        float bestScore = IsWhite ? float.NegativeInfinity : float.PositiveInfinity;

        foreach (Move move in MoveGenerator.GenerateMoves(ref state))
        {
            UndoInfo undoInfo = state.MakeMove(move);

            float score = Minimax(state, searchDepth - 1, float.MinValue, float.MaxValue);

            state.UnmakeMove(move, undoInfo);

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
        if (state.IsFifty() || state.IsInsufficientMaterial())
        {
            return 0f;
        }

        List<Move> moves = MoveGenerator.GenerateMoves(ref state);

        if (moves.Count == 0)
        {
            return state.IsCheck()
                ? state.IsWhiteTurn ? float.MinValue : float.MaxValue
                : 0f;
        }

        if (depth == 0)
        {
            return EvaluateState(in state);
        }

        bool whiteToMove = state.IsWhiteTurn;
        float best = whiteToMove ? float.NegativeInfinity : float.PositiveInfinity;

        foreach (Move move in moves)
        {
            UndoInfo undo = state.MakeMove(move);

            float score = Minimax(state, depth - 1, alpha, beta);

            state.UnmakeMove(move, undo);

            if (whiteToMove)
            {
                best = Mathf.Max(best, score);
                alpha = Mathf.Max(alpha, best);
            }
            else
            {
                best = Mathf.Min(best, score);
                beta = Mathf.Min(beta, best);
            }

            if (beta <= alpha)
            {
                break;
            }
        }

        return best;
    }

    private float EvaluateState(in BoardState state)
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