using System.Collections.Generic;
using UnityEngine;
using System.Threading;

// Adds iterative deepening and principal variation
// More conditions to extensions
// https://www.chessprogramming.org/Principal_Variation

public class IterativeDeepening_11 : ChessAgent
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
    private const int BLOCKED_PAWN_PENALTY = 10;

    // https://adamberent.com/piece-square-table/
    // PST's from white perspective
    // Promote pushing center file pawns, and reaching promotion squares
    private static readonly short[] PawnPST = new short[64]
    {
         0,  0,  0,  0,  0,  0,  0,  0,
         5, 10, 10,-25,-25, 10, 10,  5,
         5, -5,-10,  0,  0,-10, -5,  5,
         0,  0,  0, 25, 25,  0,  0,  0,
         5,  5, 10, 27, 27, 10,  5,  5,
        10, 10, 20, 30, 30, 20, 10, 10,
        50, 50, 50, 50, 50, 50, 50, 50,
         0,  0,  0,  0,  0,  0,  0,  0
    };
    // Promote centralisation
    private static readonly short[] KnightPST = new short[64]
    {
        -50,-40,-20,-30,-30,-20,-40,-50,
        -40,-20,  0,  5,  5,  0,-20,-40,
        -30,  5, 10, 15, 15, 10,  5,-30,
        -30,  0, 15, 20, 20, 15,  0,-30,
        -30,  5, 15, 20, 20, 15,  5,-30,
        -30,  0, 10, 15, 15, 10,  0,-30,
        -40,-20,  0,  0,  0,  0,-20,-40,
        -50,-40,-30,-30,-30,-30,-40,-50
    };
    // Promote centralisation with a nudge to friendly side ranks
    private static readonly short[] BishopPST = new short[64]
    {
        -20,-10,-40,-10,-10,-40,-10,-20,
        -10,  5,  0,  0,  0,  0,  5,-10,
        -10, 10, 10, 10, 10, 10, 10,-10,
        -10,  0, 10, 10, 10, 10,  0,-10,
        -10,  5,  5, 10, 10,  5,  5,-10,
        -10,  0,  5, 10, 10,  5,  0,-10,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -20,-10,-10,-10,-10,-10,-10,-20
    };
    // Promote king safety and castling
    private static readonly short[] KingPSTOpening = new short[64]
    {
         20, 30, 10,  0,  0, 10, 30, 20,
         20, 20,  0,  0,  0,  0, 20, 20,
        -10,-20,-20,-20,-20,-20,-20,-10,
        -20,-30,-30,-40,-40,-30,-30,-20,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30
    };
    // Promote mobility of king and centralising position
    private static readonly short[] KingPSTEndgame = new short[64]
    {
        -50,-30,-30,-30,-30,-30,-30,-50,
        -30,-30,  0,  0,  0,  0,-30,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-20,-10,  0,  0,-10,-20,-30,
        -50,-40,-30,-20,-20,-30,-40,-50
    };

    private const int PHASE_KNIGHT = 1;
    private const int PHASE_BISHOP = 1;
    private const int PHASE_ROOK = 2;
    private const int PHASE_QUEEN = 4;
    private const int TOTAL_PHASE = 24; // (4 Knights * 1) + (4 Bishops * 1) + (4 Rooks * 2) + (2 Queens * 4) 

    private const int MAX_EXTENSION_PLIES = 4;
    private const int MAX_SEARCH_PLY = 128; // Emergency recursion limit

    private Dictionary<ulong, TTEntry> transpositionTable = new Dictionary<ulong, TTEntry>();

    private struct TTEntry
    {
        public int Score;
        public int Depth;
        public NodeType Type;
    }

    private const int MAX_PV_LENGTH = 64;

    private struct Line
    {
        public int CMove;
        public Move[] Moves;

        public Line(int maxMoves)
        {
            CMove = 0;
            Moves = new Move[maxMoves];
        }

        public void Set(Move firstMove, Line childLine)
        {
            Moves[0] = firstMove;

            int copyCount = Mathf.Min(childLine.CMove, Moves.Length - 1);

            for (int i = 0; i < copyCount; i++)
                Moves[i + 1] = childLine.Moves[i];

            CMove = copyCount + 1;
        }
    }

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

        // phase is in [0, 24]. Blends opening and endgame evaluation
        // 0 = opening, 24 = endgame
        return Mathf.Clamp(phase, 0, TOTAL_PHASE);
    }

    protected override SearchResult ChooseMove(BoardState state, CancellationToken ct)
    {
        Line previousPv = new Line(MAX_PV_LENGTH);
        Line currentPv = new Line(MAX_PV_LENGTH);

        int bestScore = -int.MaxValue;

        for (int depth = 1; depth <= searchDepth; depth++)
        {
            currentPv = new Line(MAX_PV_LENGTH);

            bestScore = SearchRoot(
                ref state,
                depth,
                previousPv,
                ref currentPv
            );

            previousPv = currentPv;
        }

        if (currentPv.CMove == 0)
            throw new System.InvalidOperationException("No move was found!");

        return new SearchResult(
            currentPv.Moves[0],
            bestScore,
            evaluatedStates
        );
    }

    private int SearchRoot(ref BoardState state, int depth, Line previousPv, ref Line pv)
    {
        int bestScore = -int.MaxValue;

        List<Move> moves = MoveGenerator.GenerateMoves(ref state);

        Move? pvMove = previousPv.CMove > 0
            ? previousPv.Moves[0]
            : null;

        OrderMoves(moves, pvMove);

        int alpha = -int.MaxValue;
        int beta = int.MaxValue;

        foreach (Move move in moves)
        {
            Line childPv = new Line(MAX_PV_LENGTH);

            UndoInfo undo = MakeSearchMove(ref state, move);

            int ext = GetExtension(ref state, move, depth, legalMoveCount: moves.Count, in pv);
            ext = Mathf.Min(ext, MAX_EXTENSION_PLIES);

            int score = -Negamax(
                ref state,
                depth - 1 + ext,
                -beta,
                -alpha,
                ref childPv,
                previousPv,
                1,
                MAX_EXTENSION_PLIES - ext
            );

            UnmakeSearchMove(ref state, move, undo);

            if (score > bestScore)
            {
                bestScore = score;
                alpha = Mathf.Max(alpha, score);

                pv.Set(move, childPv);
            }
        }

        return bestScore;
    }

    private bool TryFetchTTValue(ulong key, int depth, ref int alpha, ref int beta, out int score)
    {
        score = 0;

        if (!transpositionTable.TryGetValue(key, out TTEntry entry)) return false;

        if (entry.Depth < depth) return false;

        if (entry.Type == NodeType.Exact)
        {
            score = entry.Score;
            return true;
        }

        if (entry.Type == NodeType.Lower) alpha = Mathf.Max(alpha, entry.Score);
        else if (entry.Type == NodeType.Upper) beta = Mathf.Min(beta, entry.Score);

        if (alpha >= beta)
        {
            score = entry.Score;
            return true;
        }

        return false;
    }

    private int Negamax(ref BoardState state, int depth, int alpha, int beta, ref Line pv, Line previousPv, int ply, int extensionsRemaining)
    {
        // Draw state
        if (state.IsFifty() || state.IsInsufficientMaterial() || IsThreefold(ref state))
        {
            pv.CMove = 0;
            return 0;
        }

        int originalAlpha = alpha;
        int originalBeta = beta;

        // Repetition is path-dependent, so skip TT if this position has already occurred in this search line.
        bool hasRepeatedPositionInLine = GetPositionOccurrenceCount(state.ZobristKey) >= 2;

        if (!hasRepeatedPositionInLine && TryFetchTTValue(state.ZobristKey, depth, ref alpha, ref beta, out int ttScore))
        {
            pv.CMove = 0;
            return ttScore;
        }

        List<Move> moves = MoveGenerator.GenerateMoves(ref state);

        Move? pvMove = previousPv.CMove > ply
            ? previousPv.Moves[ply]
            : null;

        OrderMoves(moves, pvMove);

        if (moves.Count == 0)
        {
            if (state.IsCheck())
                // Depth is added/subtracted so the bot prioritises doing mates which occur sooner
                return -MATE_SCORE - depth;

            return 0; // Stalemate
        }

        if (ply >= MAX_SEARCH_PLY)
        {
            pv.CMove = 0;
            return EvaluateState(in state);
        }

        if (depth == 0)
        {
            pv.CMove = 0;
            return Quiescence(ref state, alpha, beta);
        }

        int bestScore = -int.MaxValue;

        foreach (Move move in moves)
        {
            Line childPv = new Line(MAX_PV_LENGTH);

            UndoInfo undo = MakeSearchMove(ref state, move);

            int ext = 0;

            if (extensionsRemaining > 0)
            {
                ext = GetExtension(ref state, move, depth, legalMoveCount: moves.Count, in pv);
                ext = Mathf.Min(ext, extensionsRemaining);
            }

            int score = -Negamax(ref state, depth - 1 + ext, -beta, -alpha, ref childPv, previousPv, ply + 1, extensionsRemaining - ext);


            UnmakeSearchMove(ref state, move, undo);

            if (score > bestScore)
            {
                bestScore = score;
                
                alpha = Mathf.Max(alpha, score);

                pv.Set(move, childPv);
            }

            if (beta <= alpha) break;
        }

        NodeType nodeType;
        if (bestScore <= originalAlpha) nodeType = NodeType.Upper;
        else if (bestScore >= originalBeta) nodeType = NodeType.Lower;
        else nodeType = NodeType.Exact;

        // Only store the TT entry if this position has not already appeared earlier in the current search line
        // for reason as before when fetching
        if (!hasRepeatedPositionInLine
            && (!transpositionTable.TryGetValue(state.ZobristKey, out TTEntry old)
            || depth >= old.Depth))
        {
            transpositionTable[state.ZobristKey] = new TTEntry
            {
                Score = bestScore,
                Depth = depth,
                Type = nodeType
            };
        }

        return bestScore;
    }

    private int Quiescence(ref BoardState state, int alpha, int beta)
    {
        // https://www.chessprogramming.org/Quiescence_Search
        // https://www.chessprogramming.org/Delta_Pruning

        if (state.IsFifty() || state.IsInsufficientMaterial() || IsThreefold(ref state)) return 0;

        bool hasRepeatedPositionInLine = GetPositionOccurrenceCount(state.ZobristKey) >= 2;

        if (!hasRepeatedPositionInLine && TryFetchTTValue(state.ZobristKey, 0, ref alpha, ref beta, out int ttScore)) return ttScore;

        // Stand pat is not allowed when in check
        if (state.IsCheck())
        {
            List<Move> evasions = MoveGenerator.GenerateMoves(ref state);
            OrderMoves(evasions);

            if (evasions.Count == 0)
                return -MATE_SCORE;

            int bestScore = -int.MaxValue;

            foreach (Move move in evasions)
            {
                UndoInfo undo = MakeSearchMove(ref state, move);

                int score = -Quiescence(ref state, -beta, -alpha);

                UnmakeSearchMove(ref state, move, undo);

                if (score > bestScore)
                {
                    bestScore = score;

                    alpha = Mathf.Max(alpha, score);
                }

                if (alpha >= beta) break;
            }

            return bestScore;
        }

        // Stand pat - eval state without making a move
        int bestValue = EvaluateState(in state);

        if (bestValue >= beta) return bestValue;
        if (bestValue > alpha) alpha = bestValue;

        List<Move> moves = MoveGenerator.GenerateMoves(ref state);
        moves.RemoveAll(m => !m.CapturePiece.HasValue && !m.IsPromotion()); // Remove quiet moves from our search
        OrderMoves(moves);

        foreach (Move move in moves)
        {
            // Delta prune - skip if the best case can't improve alpha
            int bigDelta = QUEEN_SCORE;
            if (move.IsPromotion()) bigDelta += QUEEN_SCORE - PAWN_SCORE;

            if (bestValue + bigDelta < alpha) continue;

            UndoInfo undo = MakeSearchMove(ref state, move);
            int score = -Quiescence(ref state, -beta, -alpha);
            UnmakeSearchMove(ref state, move, undo);

            if (score >= beta) return score;
            if (score > bestValue) bestValue = score;
            if (score > alpha) alpha = score;
        }

        return bestValue;
    }

    // Evaluates max move for both sides
    private int EvaluateState(in BoardState state)
    {
        evaluatedStates++;

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

        // PST
        static int GetPSTBonus(short[] table, ulong pieces, bool isWhite)
        {
            int bonus = 0;
            ulong bb = pieces;

            while (bb != 0)
            {
                int sq = BitUtils.PopLsb(ref bb);

                int index = isWhite ? sq : (sq + 56) - (sq / 8) * 16;
                bonus += table[index];
            }

            return bonus;
        }

        int phase = GetPhase(in state);

        int whiteKing = GetPSTBonus(KingPSTOpening, state.WhiteKing, true);
        int whiteKingEG = GetPSTBonus(KingPSTEndgame, state.WhiteKing, true);
        int whiteKingPSTBonus = (whiteKing * (TOTAL_PHASE - phase) + whiteKingEG * phase) / TOTAL_PHASE;

        int blackKing = GetPSTBonus(KingPSTOpening, state.BlackKing, false);
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

        // Blocked pawns
        ulong whiteBlockedPawnMask = state.WhitePawns & (state.AllPieces >> 8);
        int whiteBlockedPawns = BitUtils.PopCount(whiteBlockedPawnMask);
        int whiteBlockedPawnPenalty = whiteBlockedPawns * BLOCKED_PAWN_PENALTY;

        ulong blackBlockedPawnMask = state.BlackPawns & (state.AllPieces << 8);
        int blackBlockedPawns = BitUtils.PopCount(blackBlockedPawnMask);
        int blackBlockedPawnPenalty = blackBlockedPawns * BLOCKED_PAWN_PENALTY;

        int eval = whiteMaterialScore
                    - blackMaterialScore
                    - whiteStackedPawnPenalty
                    + blackStackedPawnPenalty
                    + whitePST
                    - blackPST
                    - whiteBlockedPawnPenalty
                    + blackBlockedPawnPenalty;

        return state.IsWhiteTurn ? eval : -eval;
            
    }

    private static void OrderMoves(List<Move> moves, Move? priorityMove = null)
    {
        static int GetMoveOrderScore(Move move, Move? priorityMove)
        {
            int score = 0;

            if (priorityMove.HasValue && move.Equals(priorityMove.Value))
                score += 1_000_000;

            // Captures first: MVV : LVA
            if (move.CapturePiece.HasValue)
            {
                score += 10_000;
                score += GetPieceValue(move.CapturePiece.Value) * 10;
                score -= GetPieceValue(move.Piece);
            }

            // Promotions
            if (move.IsPromotion()) score += 9_000;

            // Castling
            if (move.IsCastle()) score += 50;

            return score;
        }

        moves.Sort((a, b) => GetMoveOrderScore(b, priorityMove).CompareTo(GetMoveOrderScore(a, priorityMove)));
    }
    private int GetExtension(ref BoardState state, Move move, int currentDepth, int legalMoveCount, in Line pv)
    {
        if (currentDepth <= 1) return 0;

        // Aids in forced lines
        if (legalMoveCount == 1) return 1;

        // Forcing moves are useful
        if (state.IsCheck()) return 1;

        // Extend if move is part of PV line
        if (pv.CMove > 0 && move.Equals(pv.Moves[0])) return 1;

        return 0;
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

    private enum NodeType
    {
        Exact, // Score is exact
        Lower, // Score caused beta cutoff
        Upper // Score never exceeded alpha
    }
}
