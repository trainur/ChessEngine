using System.Collections.Generic;

public class KnightMoves
{
    public static readonly ulong[] AttackTable = new ulong[64];

    static KnightMoves()
    {
        for (int sq = 0; sq < 64; sq++)
        {
            AttackTable[sq] = ComputeAttacks(sq);
        }
    }

    private static ulong ComputeAttacks(int sq)
    {
        // https://www.chessprogramming.org/Knight_Pattern

        // Knight location
        ulong bit = 1UL << sq;

        // Calculate two bits right and left, with mask to check if move takes us off the board (..011k110..) generally
        ulong l1 = (bit >> 1) & 0x7f7f7f7f7f7f7f7f;
        ulong l2 = (bit >> 2) & 0x3f3f3f3f3f3f3f3f;
        ulong r1 = (bit << 1) & 0xfefefefefefefefe;
        ulong r2 = (bit << 2) & 0xfcfcfcfcfcfcfcfc;

        // (..01k01..) generally
        ulong h1 = l1 | r1;

        // (..10k01..) generally
        ulong h2 = l2 | r2;

        // Bitshift for final move pattern. Off board moves over/underflow.
        return (h1 << 16) | (h1 >> 16) | (h2 << 8) | (h2 >> 8);
    }

    public static void Generate(ref BoardState state, List<Move> moves)
    {
        ulong knights = state.IsWhiteTurn ? state.WhiteKnights : state.BlackKnights;
        ulong friendly = state.IsWhiteTurn ? state.WhitePieces : state.BlackPieces;

        while (knights != 0)
        {
            int from = BitUtils.PopLsb(ref knights);
            ulong attacks = AttackTable[from] & ~friendly;

            while (attacks != 0)
            {
                int to = BitUtils.PopLsb(ref attacks);
                moves.Add(new Move(from, to, PieceType.Knight, state.GetPieceAt(to)));
            }
        }
    }
}
