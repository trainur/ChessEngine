using System.Collections.Generic;

public class PawnMoves
{
    public static readonly ulong[] WhiteAttackTable = new ulong[64];
    public static readonly ulong[] BlackAttackTable = new ulong[64];

    static PawnMoves()
    {
        for (int sq = 0; sq < 64; sq++)
        {
            WhiteAttackTable[sq] = ComputeWhiteAttacks(sq);
            BlackAttackTable[sq] = ComputeBlackAttacks(sq);
        }
    }

    // https://www.chessprogramming.org/Pawn_Attacks_(Bitboards)#Attack_Maps
    private static ulong ComputeWhiteAttacks(int sq)
    {
        // Pawn location
        ulong bit = 1UL << sq;

        ulong left = (bit << 9) & 0xfefefefefefefefeUL;
        ulong right = (bit << 7) & 0x7f7f7f7f7f7f7f7fUL;

        /* return
        * .. 1 0 1 ..
        * .. 0 p 0 ..
        * generally
        */ 
        return left | right;
    }

    private static ulong ComputeBlackAttacks(int sq)
    {
        // Pawn location
        ulong bit = 1UL << sq;

        ulong left = (bit >> 7) & 0xfefefefefefefefeUL;
        ulong right = (bit >> 9) & 0x7f7f7f7f7f7f7f7fUL;

        /* return
        * .. 0 p 0 ..
        * .. 1 0 1 ..
        * generally
        */
        return left | right;
    }

    public static void Generate(ref BoardState state, List<Move> moves)
    {
        ulong pawns = state.IsWhiteTurn ? state.WhitePawns : state.BlackPawns;
        ulong opponent = state.IsWhiteTurn ? state.BlackPieces : state.WhitePieces;

        int dir = state.IsWhiteTurn ? 8 : -8;
        ulong[] attackTable = state.IsWhiteTurn ? WhiteAttackTable : BlackAttackTable;
        ulong doublePushMask = state.IsWhiteTurn ? 0x0000000000ff0000UL : 0x0000ff0000000000UL;

        while (pawns != 0)
        {
            int from = BitUtils.PopLsb(ref pawns);
            ulong bit = 1UL << from;

            // Single push
            ulong push = dir > 0 ? (bit << 8) & ~state.AllPieces : (bit >> 8) & ~state.AllPieces;
            if (push != 0)
            {
                int to = from + dir;

                AddMove(from, to, moves, in state);

                // Double push
                ulong doublePush = dir > 0
                    ? ((push & doublePushMask) << 8) & ~state.AllPieces
                    : ((push & doublePushMask) >> 8) & ~state.AllPieces;
                if (doublePush != 0) moves.Add(new Move(from, to + dir, PieceType.Pawn, state.GetPieceAt(to + dir), MoveFlag.DoublePawnPush));
            }

            // Captures
            ulong attacks = attackTable[from] & opponent;
            while (attacks != 0)
            {
                int to = BitUtils.PopLsb(ref attacks);
                AddMove(from, to, moves, in state);
            }

            // En passant
            if (state.EnPassantSquare != -1 && (attackTable[from] & (1UL << state.EnPassantSquare)) != 0)
                moves.Add(new Move(from, state.EnPassantSquare, PieceType.Pawn, PieceType.Pawn, MoveFlag.EnPassant));
        }
    }

    // Helper function for handling promotion moves
    private static void AddMove(int from, int to, List<Move> moves, in BoardState state)
    {
        int toRank = to / 8;
        if (toRank == 0 || toRank == 7)
        {
            moves.Add(new Move(from, to, PieceType.Pawn, state.GetPieceAt(to), MoveFlag.PromoteQueen));
            moves.Add(new Move(from, to, PieceType.Pawn, state.GetPieceAt(to), MoveFlag.PromoteRook));
            moves.Add(new Move(from, to, PieceType.Pawn, state.GetPieceAt(to), MoveFlag.PromoteBishop));
            moves.Add(new Move(from, to, PieceType.Pawn, state.GetPieceAt(to), MoveFlag.PromoteKnight));
        }
        else
        {
            moves.Add(new Move(from, to, PieceType.Pawn, state.GetPieceAt(to)));
        }
    }
}
