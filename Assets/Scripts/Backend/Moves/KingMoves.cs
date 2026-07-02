using System.Collections.Generic;
using System;

public class KingMoves
{
    public static readonly ulong[] AttackTable = new ulong[64];
    
    static KingMoves()
    {
        for (int sq = 0; sq < 64; sq++)
        {
            AttackTable[sq] = ComputeAttacks(sq);
        }
    }

    private static ulong ComputeAttacks(int sq)
    {
        // https://www.chessprogramming.org/King_Pattern

        // King location
        ulong bit = 1UL << sq;

        // Calculate
        // 0 1 k 1 0 generally
        ulong l = (bit >> 1) & 0x7f7f7f7f7f7f7f7f;
        ulong r = (bit << 1) & 0xfefefefefefefefe;


        /* return
         * 0 1 1 1 0
         * 0 1 k 1 0
         * 0 1 1 1 0
         * generally
         */ 
        return l | r | ((l | r | bit) >> 8) | ((l | r | bit) << 8);
    }

    public static void Generate(ref BoardState state, List<Move> moves)
    {
        ulong king = state.IsWhiteTurn ? state.WhiteKing : state.BlackKing;
        ulong friendly = state.IsWhiteTurn ? state.WhitePieces : state.BlackPieces;

        if (king == 0) throw new InvalidOperationException($"State is corrupted. King not on board for active colour. Bitboard: {state.AllPieces}");

        int from = BitUtils.PopLsb(ref king);
        ulong attacks = AttackTable[from] & ~friendly;

        while (attacks != 0)
        {
            int to = BitUtils.PopLsb(ref attacks);
            moves.Add(new Move(from, to, PieceType.King, state.GetPieceAt(to)));
        }
    }
}