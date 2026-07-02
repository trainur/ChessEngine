using System.Collections.Generic;
using UnityEngine;

// Implements quiscence search to avoid the horizon effect
// Edited to work in centipawns
// Further implements king PST and phases

public class Quiescence_8 : ChessAgent
{
    [SerializeField, Range(1,15)] private int searchDepth = 5;
    public override int? SearchDepth => searchDepth;

    private const int PAWN_SCORE = 100;
    private const int KNIGHT_SCORE = 300;
    private const int BISHOP_SCORE = 350;
    private const int ROOK_SCORE = 500;
    private const int QUEEN_SCORE = 900;

    private const ulong AFile = 0x0101010101010101UL;
    private const int STACKED_PAWN_PENALTY = 20;

    private const int KING_PAWN_SHIELD_BONUS = 20;

    // https://adamberent.com/piece-square-table/
    private static readonly short[] PawnPST = new short[64]
    {
        0,  0,  0,  0,  0,  0,  0,  0,
        50, 50, 50, 50, 50, 50, 50, 50,
        10, 10, 20, 30, 30, 20, 10, 10,
         5,  5, 10, 27, 27, 10,  5,  5,
         0,  0,  0, 25, 25,  0,  0,  0,
         5, -5,-10,  0,  0,-10, -5,  5,
         5, 10, 10,-25,-25, 10, 10,  5,
         0,  0,  0,  0,  0,  0,  0,  0
    };
    private static readonly short[] KnightPST = new short[64]
    {
        -50,-40,-30,-30,-30,-30,-40,-50,
        -40,-20,  0,  0,  0,  0,-20,-40,
        -30,  0, 10, 15, 15, 10,  0,-30,
        -30,  5, 15, 20, 20, 15,  5,-30,
        -30,  0, 15, 20, 20, 15,  0,-30,
        -30,  5, 10, 15, 15, 10,  5,-30,
        -40,-20,  0,  5,  5,  0,-20,-40,
        -50,-40,-20,-30,-30,-20,-40,-50,
    };
    private static readonly short[] BishopPST = new short[64]
    {
        -20,-10,-10,-10,-10,-10,-10,-20,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -10,  0,  5, 10, 10,  5,  0,-10,
        -10,  5,  5, 10, 10,  5,  5,-10,
        -10,  0, 10, 10, 10, 10,  0,-10,
        -10, 10, 10, 10, 10, 10, 10,-10,
        -10,  5,  0,  0,  0,  0,  5,-10,
        -20,-10,-40,-10,-10,-40,-10,-20,
    };
    private static readonly short[] KingPST = new short[64]
    {
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -20, -30, -30, -40, -40, -30, -30, -20,
        -10, -20, -20, -20, -20, -20, -20, -10,
         20,  20,   0,   0,   0,   0,  20,  20,
         20,  30,  10,   0,   0,  10,  30,  20
    };
    private static readonly short[] KingPSTEndgame = new short[64]
    {
        -50,-40,-30,-20,-20,-30,-40,-50,
        -30,-20,-10,  0,  0,-10,-20,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-30,  0,  0,  0,  0,-30,-30,
        -50,-30,-30,-30,-30,-30,-30,-50
    };

    private const int PHASE_KNIGHT = 1;
    private const int PHASE_BISHOP = 1;
    private const int PHASE_ROOK = 2;
    private const int PHASE_QUEEN = 4;
    private const int TOTAL_PHASE = 24; // 4*1 + 4*1 + 4*2 + 2*4

    private readonly Dictionary<ulong, int> positionHistory = new Dictionary<ulong, int>();
    private Dictionary<ulong, TTEntry> transpositionTable = new Dictionary<ulong, TTEntry>();

    private int GetPhase(in BoardState state)
    {
        int phase = TOTAL_PHASE;

        phase -= BitUtils.PopCount(state.WhiteKnights) * PHASE_KNIGHT;
        phase -= BitUtils.PopCount(state.BlackKnights) * PHASE_KNIGHT;
        phase -= BitUtils.PopCount(state.WhiteBishops) * PHASE_BISHOP;
        phase -= BitUtils.PopCount(state.BlackBishops) * PHASE_BISHOP;
        phase -= BitUtils.PopCount(state.WhiteRooks) * PHASE_ROOK;
        phase -= BitUtils.PopCount(state.BlackRooks) * PHASE_ROOK;
        phase -= BitUtils.PopCount(state.WhiteQueens) * PHASE_QUEEN;
        phase -= BitUtils.PopCount(state.BlackQueens) * PHASE_QUEEN;

        return phase; // 0 = opening, 24 = endgame
    }

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
        int bestScore = IsWhite ? int.MinValue : int.MaxValue;

        if (transpositionTable.Count > 500000) transpositionTable.Clear(); 

        List<Move> moves = MoveGenerator.GenerateMoves(ref state);
        OrderMoves(moves);

        int alpha = int.MinValue;
        int beta = int.MaxValue;

        foreach (Move move in moves)
        {
            UndoInfo undoInfo = state.MakeMove(move);

            RecordPosition(state.ZobristKey);
            int score = Minimax(state, searchDepth - 1, alpha, beta);
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
            bestScore,
            evaluatedStates);
    }

    private int Minimax(BoardState state, int depth, int alpha, int beta)
    {
        if (GetPositionCount(state.ZobristKey) >= 3) return 0;

        int originalAlpha = alpha;
        int originalBeta = beta;

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
            return 0;
        }

        if (depth == 0) return Quiescence(state, alpha, beta);

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
        int best = whiteToMove ? int.MinValue : int.MaxValue;

        foreach (Move move in moves)
        {
            UndoInfo undo = state.MakeMove(move);

            RecordPosition(state.ZobristKey);
            int score = Minimax(state, depth - 1, alpha, beta);
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

    private int Quiescence(BoardState state, int alpha, int beta)
    {
        // https://www.chessprogramming.org/Quiescence_Search
        // https://www.chessprogramming.org/Delta_Pruning

        evaluatedStates++;

        // Stand Pat
        int bestValue = EvaluateState(in state);

        if (state.IsWhiteTurn)
        {
            if (bestValue >= beta) return bestValue;
            if (bestValue > alpha) alpha = bestValue;
        }
        else
        {
            if (bestValue <= alpha) return bestValue;
            if (bestValue < beta) beta = bestValue;
        }

        List<Move> moves = MoveGenerator.GenerateMoves(ref state);
        moves.RemoveAll(m => !m.CapturePiece.HasValue && !m.IsPromotion());
        OrderMoves(moves);

        foreach (Move move in moves)
        {
            // Delta prune - skip if the best case can't improve alpha
            int bigDelta = QUEEN_SCORE;
            if (move.IsPromotion()) bigDelta += QUEEN_SCORE - PAWN_SCORE;

            if (state.IsWhiteTurn && bestValue + bigDelta < alpha) continue;
            if (!state.IsWhiteTurn && bestValue - bigDelta > beta) continue;

            UndoInfo undo = state.MakeMove(move);
            int score = Quiescence(state, alpha, beta);
            state.UnmakeMove(move, undo);

            if (state.IsWhiteTurn)
            {
                if (score >= beta) return score;
                if (score > bestValue) bestValue = score;
                if (score > alpha) alpha = score;
            }
            else
            {
                if (score <= alpha) return score;
                if (score < bestValue) bestValue = score;
                if (score < beta) beta = score;
            }
        }

        return bestValue;
    }

    private int EvaluateState(in BoardState state)
    {
        evaluatedStates++;

        if (state.IsTerminalState())
        {
            if (state.IsMate())
            {
                // The side to move is checkmated
                return state.IsWhiteTurn ? int.MinValue : int.MaxValue;
            }
            else
            {
                // Draw
                return 0;
            }
        }

        int whiteMaterialScore =
            (PAWN_SCORE * BitUtils.PopCount(state.WhitePawns)) +
            (KNIGHT_SCORE * BitUtils.PopCount(state.WhiteKnights)) +
            (BISHOP_SCORE * BitUtils.PopCount(state.WhiteBishops)) +
            (ROOK_SCORE * BitUtils.PopCount(state.WhiteRooks)) +
            (QUEEN_SCORE * BitUtils.PopCount(state.WhiteQueens));
        int blackMaterialScore = 
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

        int whiteStackedPawnPenalty = STACKED_PAWN_PENALTY * CountStackedPawns(state.WhitePawns);
        int blackStackedPawnPenalty = STACKED_PAWN_PENALTY * CountStackedPawns(state.BlackPawns);

        // King safety
        int whiteShieldBonus = KING_PAWN_SHIELD_BONUS * KingPawnShield(state.WhiteKing, state.WhitePawns, true);
        int blackShieldBonus = KING_PAWN_SHIELD_BONUS * KingPawnShield(state.BlackKing, state.BlackPawns, false);

        // PST
        int phase = GetPhase(in state);

        int whiteKing = GetPSTBonus(KingPST, state.WhiteKing, true);
        int whiteKingEG = GetPSTBonus(KingPSTEndgame, state.WhiteKing, true);
        int whiteKingPSTBonus = (whiteKing * (TOTAL_PHASE - phase) + whiteKingEG * phase) / TOTAL_PHASE;

        int blackKing = GetPSTBonus(KingPST, state.BlackKing, false);
        int blackKingEG = GetPSTBonus(KingPSTEndgame, state.BlackKing, false);
        int blackKingPSTBonus = (blackKing * (TOTAL_PHASE - phase) + blackKingEG * phase) / TOTAL_PHASE;

        int whitePST = GetPSTBonus(PawnPST, state.WhitePawns, true)
               + GetPSTBonus(KnightPST, state.WhiteKnights, true)
               + GetPSTBonus(BishopPST, state.WhiteBishops, true)
               + whiteKingPSTBonus;

        int blackPST = GetPSTBonus(PawnPST, state.BlackPawns, false)
                       + GetPSTBonus(KnightPST, state.BlackKnights, false)
                       + GetPSTBonus(BishopPST, state.BlackBishops, false)
                       + blackKingPSTBonus;

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

    private static int GetMoveOrderScore(Move move)
    {
        int score = 0;

        // Captures first: Most Val Victim - Least Val Attacker
        if (move.CapturePiece.HasValue)
        {
            score += 10_000;
            score += GetPieceValue(move.CapturePiece.Value) * 10;
            score -= GetPieceValue(move.Piece);
        }

        // Promotions
        if (move.IsPromotion())
        {
            score += 9_000;
        }

        // Castling
        if (move.IsCastle())
        {
            score += 50;
        }

        return score;
    }

    private static int GetPieceValue(PieceType type)
    {
        return type switch
        {
            PieceType.Pawn => PAWN_SCORE,
            PieceType.Knight => KNIGHT_SCORE,
            PieceType.Bishop => BISHOP_SCORE,
            PieceType.Rook => ROOK_SCORE,
            PieceType.Queen => QUEEN_SCORE,
            PieceType.King => 20_000,
            _ => 0
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

    private static int GetPSTBonus(short[] table, ulong pieces, bool isWhite)
    {
        int bonus = 0;
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
        public int Score;
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
