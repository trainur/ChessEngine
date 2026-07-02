using System.Collections.Generic;
using UnityEngine;

// Penalises inactive pieces, and promotes king safety
public class InactivePlusSafety : ChessAgent
{
    [SerializeField, Range(1,15)] private int searchDepth = 5;
    public override int? SearchDepth => searchDepth;

    private const float PAWN_SCORE = 1f;
    private const float KNIGHT_SCORE = 3f;
    private const float BISHOP_SCORE = 3.5f;
    private const float ROOK_SCORE = 5f;
    private const float QUEEN_SCORE = 9f;

    private const ulong AFile = 0x0101010101010101UL;
    private const float STACKED_PAWN_PENALTY = 0.2f;

    public override int MateScore => 1_000_000;

    private const ulong WhiteStartingKnights = 0x42UL;
    private const ulong WhiteStartingBishops = 0x24UL;
    private const ulong BlackStartingKnights = 0x4200000000000000UL;
    private const ulong BlackStartingBishops = 0x2400000000000000UL;
    private const float UNDEVELOPED_PIECE_PENALTY = 0.3f;

    private const float KING_PAWN_SHIELD_BONUS = 0.2f;

    private readonly Dictionary<ulong, int> positionHistory = new Dictionary<ulong, int>();
    private Dictionary<ulong, TTEntry> transpositionTable = new Dictionary<ulong, TTEntry>();

    private void RecordPosition(ulong zobristKey)
    {
        int count;

        if (!positionHistory.TryGetValue(zobristKey, out count))
            count = 0;

        positionHistory[zobristKey] = ++count;
    }

    private int GetPositionCount(ulong zobristKey)
    {
        int count;

        if (!positionHistory.TryGetValue(zobristKey, out count))
            return 0;

        return count;
    }

    protected override SearchResult ChooseMove(BoardState state)
    {
        positionHistory.Clear();
        //foreach (ulong key in PositionHistory)
        //    RecordPosition(key);

        Move? bestMove = null;
        float bestScore = IsWhite ? float.NegativeInfinity : float.PositiveInfinity;

        if (transpositionTable.Count > 500000) transpositionTable.Clear(); 

        List<Move> moves = MoveGenerator.GenerateMoves(ref state);
        OrderMoves(moves);

        foreach (Move move in moves)
        {
            UndoInfo undoInfo = state.MakeMove(move);

            RecordPosition(state.ZobristKey);
            float score = Minimax(state, searchDepth - 1, float.MinValue, float.MaxValue);
            positionHistory[state.ZobristKey]--;

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
        // Repetition detection — treat as draw
        if (GetPositionCount(state.ZobristKey) >= 2)
            return 0f;

        if (GetPositionCount(state.ZobristKey) == 0)
            if (transpositionTable.TryGetValue(state.ZobristKey, out TTEntry entry) && entry.Depth >= depth)
                return entry.Score;

        if (state.IsFifty() || state.IsInsufficientMaterial())
        {
            return 0f;
        }

        List<Move> moves = MoveGenerator.GenerateMoves(ref state);
        OrderMoves(moves);

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

            RecordPosition(state.ZobristKey);
            float score = Minimax(state, depth - 1, alpha, beta);
            positionHistory[state.ZobristKey]--;

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

        if (GetPositionCount(state.ZobristKey) == 0)
            transpositionTable[state.ZobristKey] = new TTEntry { Score = best, Depth = depth };

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

        // Calculate pawn stack penalty

        int CountStackedPawns(ulong pawns)
        {
            int stackedPawns = 0;

            for (int file = 0; file < 8; file++)
            {
                ulong fileMask = AFile << file;
                ulong pawnsOnFile = pawns & fileMask;

                int count = BitUtils.PopCount(pawnsOnFile);

                if (count > 1)
                {
                    // Penalize every extra pawn beyond the first.
                    stackedPawns += count - 1;
                }
            }

            return stackedPawns;
        }

        float whiteStackedPawnPenalty = STACKED_PAWN_PENALTY * CountStackedPawns(state.WhitePawns);
        float blackStackedPawnPenalty = STACKED_PAWN_PENALTY * CountStackedPawns(state.BlackPawns);

        // Inactivity penalty
        int whiteUndeveloped = BitUtils.PopCount(state.WhiteKnights & WhiteStartingKnights)
                             + BitUtils.PopCount(state.WhiteBishops & WhiteStartingBishops);
        int blackUndeveloped = BitUtils.PopCount(state.BlackKnights & BlackStartingKnights)
                             + BitUtils.PopCount(state.BlackBishops & BlackStartingBishops);

        float whiteUndevelopedPenalty = UNDEVELOPED_PIECE_PENALTY * whiteUndeveloped;
        float blackUndevelopedPenalty = UNDEVELOPED_PIECE_PENALTY * blackUndeveloped;

        // King safety
        float whiteShieldBonus = KING_PAWN_SHIELD_BONUS * KingPawnShield(state.WhiteKing, state.WhitePawns, true);
        float blackShieldBonus = KING_PAWN_SHIELD_BONUS * KingPawnShield(state.BlackKing, state.BlackPawns, false);

        return
            whiteMaterialScore
            - blackMaterialScore
            - whiteStackedPawnPenalty
            + blackStackedPawnPenalty
            - whiteUndevelopedPenalty
            + blackUndevelopedPenalty
            + whiteShieldBonus
            - blackShieldBonus;
    }

    private static void OrderMoves(List<Move> moves)
    {
        moves.Sort((a, b) => GetMoveOrderScore(b).CompareTo(GetMoveOrderScore(a)));
    }

    private static float GetMoveOrderScore(Move move)
    {
        float score = 0f;

        // Captures first: Most Val Victim - Least Val Attacker
        if (move.CapturePiece.HasValue)
        {
            score += 10_000f;
            score += GetPieceValue(move.CapturePiece.Value) * 10f;
            score -= GetPieceValue(move.Piece);
        }

        // Promotions
        if (move.IsPromotion())
        {
            score += 9_000f;
        }

        // Castling
        if (move.IsCastle())
        {
            score += 50f;
        }

        return score;
    }

    private static float GetPieceValue(PieceType type)
    {
        return type switch
        {
            PieceType.Pawn => PAWN_SCORE,
            PieceType.Knight => KNIGHT_SCORE,
            PieceType.Bishop => BISHOP_SCORE,
            PieceType.Rook => ROOK_SCORE,
            PieceType.Queen => QUEEN_SCORE,
            PieceType.King => 1000f,
            _ => 0f
        };
    }

    int KingPawnShield(ulong king, ulong pawns, bool isWhite)
    {
        int kingSquare = BitUtils.BitScan(king);
        int rank = kingSquare / 8;
        int file = kingSquare % 8;

        // Only apply shield bonus if king is castled (not on central files)
        if (file >= 2 && file <= 5) return 0;

        ulong shieldMask = 0UL;
        int shieldRank = isWhite ? rank + 1 : rank - 1;

        if (shieldRank < 0 || shieldRank > 7) return 0;

        // Three squares directly in front of king, clamped to board
        for (int f = Mathf.Max(0, file - 1); f <= Mathf.Min(7, file + 1); f++)
            shieldMask |= 1UL << (shieldRank * 8 + f);

        return BitUtils.PopCount(pawns & shieldMask);
    }

    private struct TTEntry
    {
        public float Score;
        public int Depth;
    }
}
