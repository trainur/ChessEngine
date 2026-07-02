using System.Collections.Generic;
using UnityEngine;

// Implements a piece-square table to promote good piece placement
// Also implements priority for sooner mates

public class PST_7 : ChessAgent
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

    private const float KING_PAWN_SHIELD_BONUS = 0.2f;

    // Adapted from: https://adamberent.com/piece-square-table/
    private static readonly float[] PawnPST = new float[64]
    {
        0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f,
        0.50f, 0.50f, 0.50f, 0.50f, 0.50f, 0.50f, 0.50f, 0.50f,
        0.10f, 0.10f, 0.20f, 0.30f, 0.30f, 0.20f, 0.10f, 0.10f,
        0.05f, 0.05f, 0.10f, 0.25f, 0.25f, 0.10f, 0.05f, 0.05f,
        0.00f, 0.00f, 0.00f, 0.20f, 0.20f, 0.00f, 0.00f, 0.00f,
        0.05f,-0.05f,-0.10f, 0.00f, 0.00f,-0.10f,-0.05f, 0.05f,
        0.05f, 0.10f, 0.10f,-0.20f,-0.20f, 0.10f, 0.10f, 0.05f,
        0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f,
    };
    private static readonly float[] KnightPST = new float[64]
    {
        -0.50f,-0.40f,-0.30f,-0.30f,-0.30f,-0.30f,-0.40f,-0.50f,
        -0.40f,-0.20f, 0.00f, 0.00f, 0.00f, 0.00f,-0.20f,-0.40f,
        -0.30f, 0.00f, 0.10f, 0.15f, 0.15f, 0.10f, 0.00f,-0.30f,
        -0.30f, 0.05f, 0.15f, 0.20f, 0.20f, 0.15f, 0.05f,-0.30f,
        -0.30f, 0.00f, 0.15f, 0.20f, 0.20f, 0.15f, 0.00f,-0.30f,
        -0.30f, 0.05f, 0.10f, 0.15f, 0.15f, 0.10f, 0.05f,-0.30f,
        -0.40f,-0.20f, 0.00f, 0.05f, 0.05f, 0.00f,-0.20f,-0.40f,
        -0.50f,-0.40f,-0.20f,-0.30f,-0.30f,-0.20f,-0.40f,-0.50f,
    };
    private static readonly float[] BishopPST = new float[64]
    {
        -0.20f,-0.10f,-0.10f,-0.10f,-0.10f,-0.10f,-0.10f,-0.20f,
        -0.10f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f,-0.10f,
        -0.10f, 0.00f, 0.05f, 0.10f, 0.10f, 0.05f, 0.00f,-0.10f,
        -0.10f, 0.05f, 0.05f, 0.10f, 0.10f, 0.05f, 0.05f,-0.10f,
        -0.10f, 0.00f, 0.10f, 0.10f, 0.10f, 0.10f, 0.00f,-0.10f,
        -0.10f, 0.10f, 0.10f, 0.10f, 0.10f, 0.10f, 0.10f,-0.10f,
        -0.10f, 0.05f, 0.00f, 0.00f, 0.00f, 0.00f, 0.05f,-0.10f,
        -0.20f,-0.10f,-0.40f,-0.10f,-0.10f,-0.40f,-0.10f,-0.20f,
    };

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

        float alpha = float.NegativeInfinity;
        float beta = float.PositiveInfinity;

        foreach (Move move in moves)
        {
            UndoInfo undoInfo = state.MakeMove(move);

            RecordPosition(state.ZobristKey);
            float score = Minimax(state, searchDepth - 1, alpha, beta);
            positionHistory[state.ZobristKey]--;

            state.UnmakeMove(move, undoInfo);

            if (bestMove == null || (IsWhite && score > bestScore) || (!IsWhite &&  score < bestScore))
            {
                bestScore = score;
                bestMove = move;

                if (IsWhite) alpha = Mathf.Max(alpha, score);
                else beta = Mathf.Min(beta, score);
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
        if (GetPositionCount(state.ZobristKey) >= 3) return 0f;

        float originalAlpha = alpha;
        float originalBeta = beta;

        bool isRepeatedInLine = GetPositionCount(state.ZobristKey) > 1;

        if (!isRepeatedInLine &&
            transpositionTable.TryGetValue(state.ZobristKey, out TTEntry entry) &&
            entry.Depth >= depth)
        {
            if (entry.Type == NodeType.Exact) return entry.Score;
            if (entry.Type == NodeType.LowerBound) alpha = Mathf.Max(alpha, entry.Score);
            if (entry.Type == NodeType.UpperBound) beta = Mathf.Min(beta, entry.Score);
            
            if (alpha >= beta) return entry.Score; // still a cutoff
        }

        if (state.IsFifty() || state.IsInsufficientMaterial())
        {
            return 0f;
        }

        if (depth == 0) return EvaluateState(in state);

        List<Move> moves = MoveGenerator.GenerateMoves(ref state);
        OrderMoves(moves);

        if (moves.Count == 0)
        {
            if (state.IsCheck())
            {
                // Side to move is mated
                return state.IsWhiteTurn
                    ? -MATE_SCORE - depth // black mated white, prioritise sooner mates
                    : MATE_SCORE + depth; // white mated black, prioritise sooner mates
            }
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

        // Determine node type before storing
        NodeType nodeType;
        if (best <= originalAlpha) nodeType = NodeType.UpperBound;
        else if (best >= originalBeta) nodeType = NodeType.LowerBound;
        else nodeType = NodeType.Exact;

        if (GetPositionCount(state.ZobristKey) <= 1)
            transpositionTable[state.ZobristKey] = new TTEntry { Score = best, Depth = depth, Type = nodeType };

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

        // King safety
        float whiteShieldBonus = KING_PAWN_SHIELD_BONUS * KingPawnShield(state.WhiteKing, state.WhitePawns, true);
        float blackShieldBonus = KING_PAWN_SHIELD_BONUS * KingPawnShield(state.BlackKing, state.BlackPawns, false);

        // PST
        float whitePST = GetPSTBonus(PawnPST, state.WhitePawns, true)
               + GetPSTBonus(KnightPST, state.WhiteKnights, true)
               + GetPSTBonus(BishopPST, state.WhiteBishops, true);

        float blackPST = GetPSTBonus(PawnPST, state.BlackPawns, false)
                       + GetPSTBonus(KnightPST, state.BlackKnights, false)
                       + GetPSTBonus(BishopPST, state.BlackBishops, false);

        return
            whiteMaterialScore
            - blackMaterialScore
            - whiteStackedPawnPenalty
            + blackStackedPawnPenalty
            + whiteShieldBonus
            - blackShieldBonus
            + whitePST
            - blackPST;
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

    private static float GetPSTBonus(float[] table, ulong pieces, bool isWhite)
    {
        float bonus = 0f;
        ulong bb = pieces;

        while (bb != 0)
        {
            int sq = BitUtils.BitScan(bb);
            bb &= bb - 1;

            int index = isWhite ? sq : (sq + 56) - (sq / 8) * 16; // vertical mirror only
            bonus += table[index];
        }

        return bonus;
    }

    private struct TTEntry
    {
        public float Score;
        public int Depth;
        public NodeType Type;
    }

    private enum NodeType
    {
        Exact, // score is exact
        LowerBound, // score caused beta cutoff
        UpperBound // score never exceeded alpha
    }
}
