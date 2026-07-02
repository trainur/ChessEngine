using System.Collections.Generic;
using System.Diagnostics;

public class MoveGenerator
{
    public static List<Move> GenerateMoves(ref BoardState state)
    {
        var moves = new List<Move>(128);
        int start;

        // Generate pseduo-legal moves
        start = moves.Count;
        PawnMoves.Generate(ref state, moves);
        ValidateGeneratedMoves(state, moves, start, "PawnMoves");

        start = moves.Count;
        KnightMoves.Generate(ref state, moves);
        ValidateGeneratedMoves(state, moves, start, "KnightMoves");

        start = moves.Count;
        BishopMoves.Generate(ref state, moves);
        ValidateGeneratedMoves(state, moves, start, "BishopMoves");

        start = moves.Count;
        RookMoves.Generate(ref state, moves);
        ValidateGeneratedMoves(state, moves, start, "RookMoves");

        start = moves.Count;
        QueenMoves.Generate(ref state, moves);
        ValidateGeneratedMoves(state, moves, start, "QueenMoves");

        start = moves.Count;
        KingMoves.Generate(ref state, moves);
        ValidateGeneratedMoves(state, moves, start, "KingMoves");

        start = moves.Count;
        GenerateCastling(ref state, moves);
        ValidateGeneratedMoves(state, moves, start, "Castling");

        // Filter for legal
        // Works by sorting legal moves down to the bottom of the list, overwriting moves deemed illegal (by pointer pos)
        int write = 0;

        for (int read = 0; read < moves.Count; read++)
        {
            Move move = moves[read];

            if (!LeavesKingInCheck(ref state, move))
            {
                moves[write++] = move;
            }
        }

        if (write < moves.Count)
        {
            moves.RemoveRange(write, moves.Count - write);
        }

        return moves;
    }

    [Conditional("MOVEGEN_VALIDATION")]
    private static void ValidateGeneratedMoves(BoardState state, List<Move> moves, int start, string source)
    {
        for (int i = start; i < moves.Count; i++)
        {
            Move move = moves[i];

            PieceType? actual = state.GetPieceAt(move.From);

            if (actual == null)
            {
                UnityEngine.Debug.LogError(
                    $"{source} generated move from empty square: {move} " +
                    $"fromIndex={move.From}, toIndex={move.To}"
                );
            }
            else if (actual != move.Piece)
            {
                UnityEngine.Debug.LogError(
                    $"{source} generated wrong piece move: {move} " +
                    $"board has {actual} on {move.From}, " +
                    $"move says {move.Piece}"
                );
            }
        }
    }

    public static bool IsSquareAttacked(ref BoardState state, int sq, bool byWhite)
    {
        // https://www.chessprogramming.org/Square_Attacked_By

        ulong occupied = state.AllPieces;

        ulong pawns = byWhite ? state.WhitePawns : state.BlackPawns;
        ulong knights = byWhite ? state.WhiteKnights : state.BlackKnights;
        ulong king = byWhite ? state.WhiteKing : state.BlackKing;

        ulong rooksQueens =
            (byWhite ? state.WhiteRooks : state.BlackRooks) |
            (byWhite ? state.WhiteQueens : state.BlackQueens);

        ulong bishopsQueens =
            (byWhite ? state.WhiteBishops : state.BlackBishops) |
            (byWhite ? state.WhiteQueens : state.BlackQueens);


        ulong pawnAttackers = byWhite ? PawnMoves.BlackAttackTable[sq] : PawnMoves.WhiteAttackTable[sq];

        return (pawnAttackers & pawns) != 0
            || (KnightMoves.AttackTable[sq] & knights) != 0
            || (KingMoves.AttackTable[sq] & king) != 0
            || (BishopMoves.GetAttacks(occupied, sq) & bishopsQueens) != 0
            || (RookMoves.GetAttacks(occupied, sq) & rooksQueens) != 0;
    }

    private static void GenerateCastling(ref BoardState state, List<Move> moves)
    {
        int kingSquare = state.IsWhiteTurn ? 4 : 60;
        bool kingSide = state.IsWhiteTurn ? state.WhiteKingsideCastle : state.BlackKingsideCastle;
        bool queenSide = state.IsWhiteTurn ? state.WhiteQueensideCastle : state.BlackQueensideCastle;
        ulong kingSideMask = state.IsWhiteTurn ? 0x60UL : 0x6000000000000000UL;
        ulong queenSideMask = state.IsWhiteTurn ? 0xeUL : 0xe00000000000000UL;

        bool attackedByWhite = !state.IsWhiteTurn;

        if (kingSide &&
            (state.AllPieces & kingSideMask) == 0 &&
            !IsSquareAttacked(ref state, kingSquare, attackedByWhite) &&
            !IsSquareAttacked(ref state, kingSquare + 1, attackedByWhite) &&
            !IsSquareAttacked(ref state, kingSquare + 2, attackedByWhite))
            moves.Add(new Move(kingSquare, kingSquare + 2, PieceType.King, null, MoveFlag.CastleKingside));

        if (queenSide &&
            (state.AllPieces & queenSideMask) == 0 &&
            !IsSquareAttacked(ref state, kingSquare, attackedByWhite) &&
            !IsSquareAttacked(ref state, kingSquare - 1, attackedByWhite) &&
            !IsSquareAttacked(ref state, kingSquare - 2, attackedByWhite))
            moves.Add(new Move(kingSquare, kingSquare - 2, PieceType.King, null, MoveFlag.CastleQueenside));
    }

    private static bool LeavesKingInCheck(ref BoardState state, Move move)
    {
        bool movingWhite = state.IsWhiteTurn;

        UndoInfo undoInfo = state.MakeMove(move);

        int kingSquare = movingWhite ? BitUtils.BitScan(state.WhiteKing) : BitUtils.BitScan(state.BlackKing);

        bool illegal = IsSquareAttacked(ref state, kingSquare, !movingWhite);

        state.UnmakeMove(move, undoInfo);

        return illegal;
    }
}