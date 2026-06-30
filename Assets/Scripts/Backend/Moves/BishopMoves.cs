using System.Collections.Generic;
using UnityEngine;

public class BishopMoves
{
    // https://www.chessprogramming.org/Magic_Bitboards
    // Magic numbers via: https://github.com/maksimKorzh/chess_programming/blob/master/src/magics/magics.c

    private static readonly ulong[] Masks = new ulong[64];
    private static readonly ulong[] Magics =
    {
        0x89a1121896040240UL,
        0x2004844802002010UL,
        0x2068080051921000UL,
        0x62880a0220200808UL,
        0x4042004000000UL,
        0x100822020200011UL,
        0xc00444222012000aUL,
        0x28808801216001UL,
        0x400492088408100UL,
        0x201c401040c0084UL,
        0x840800910a0010UL,
        0x82080240060UL,
        0x2000840504006000UL,
        0x30010c4108405004UL,
        0x1008005410080802UL,
        0x8144042209100900UL,
        0x208081020014400UL,
        0x4800201208ca00UL,
        0xf18140408012008UL,
        0x1004002802102001UL,
        0x841000820080811UL,
        0x40200200a42008UL,
        0x800054042000UL,
        0x88010400410c9000UL,
        0x520040470104290UL,
        0x1004040051500081UL,
        0x2002081833080021UL,
        0x400c00c010142UL,
        0x941408200c002000UL,
        0x658810000806011UL,
        0x188071040440a00UL,
        0x4800404002011c00UL,
        0x104442040404200UL,
        0x511080202091021UL,
        0x4022401120400UL,
        0x80c0040400080120UL,
        0x8040010040820802UL,
        0x480810700020090UL,
        0x102008e00040242UL,
        0x809005202050100UL,
        0x8002024220104080UL,
        0x431008804142000UL,
        0x19001802081400UL,
        0x200014208040080UL,
        0x3308082008200100UL,
        0x41010500040c020UL,
        0x4012020c04210308UL,
        0x208220a202004080UL,
        0x111040120082000UL,
        0x6803040141280a00UL,
        0x2101004202410000UL,
        0x8200000041108022UL,
        0x21082088000UL,
        0x2410204010040UL,
        0x40100400809000UL,
        0x822088220820214UL,
        0x40808090012004UL,
        0x910224040218c9UL,
        0x402814422015008UL,
        0x90014004842410UL,
        0x1000042304105UL,
        0x10008830412a00UL,
        0x2520081090008908UL,
        0x40102000a0a60140UL
    };
    private static readonly int[] Shifts = new int[64];
    public static readonly ulong[][] AttackTable = new ulong[64][];

    static BishopMoves()
    {
        for (int sq = 0; sq < 64; sq++)
        {
            Masks[sq] = ComputeMask(sq);
            int bits = BitUtils.PopCount(Masks[sq]);
            Shifts[sq] = 64 - bits;
            int size = 1 << bits;
            AttackTable[sq] = new ulong[size];

            // Enumerate all subsets of the mask
            ulong mask = Masks[sq];
            ulong subset = 0;
            do
            {
                int index = (int)((subset * Magics[sq]) >> Shifts[sq]);
                AttackTable[sq][index] = ComputeAttacks(sq, subset);
                subset = (subset - mask) & mask;
            } while (subset != 0);
        }
    }

    private static ulong ComputeMask(int sq)
    {
        ulong mask = 0;
        int rank = sq / 8, file = sq % 8;

        // Generate mask of ne, nw, se, sw blockers. excludes edges
        /* 1 0 0 0 1
         * 0 1 0 1 0
         * 0 0 b 0 0
         * 0 1 0 1 0
         * 1 0 0 0 1 */

        int r, f;
        for (r = rank + 1, f = file + 1; r <= 6 && f <= 6; r++, f++) mask |= 1UL << (r * 8 + f);
        for (r = rank + 1, f = file - 1; r <= 6 && f >= 1; r++, f--) mask |= 1UL << (r * 8 + f);
        for (r = rank - 1, f = file + 1; r >= 1 && f <= 6; r--, f++) mask |= 1UL << (r * 8 + f);
        for (r = rank - 1, f = file - 1; r >= 1 && f >= 1; r--, f--) mask |= 1UL << (r * 8 + f);

        return mask;
    }

    private static ulong ComputeAttacks(int sq, ulong blockers)
    {
        ulong attacks = 0;
        int rank = sq / 8, file = sq % 8;

        int r, f;
        for (r = rank + 1, f = file + 1; r < 8 && f < 8; r++, f++) { attacks |= 1UL << (r * 8 + f); if ((blockers & (1UL << (r * 8 + f))) != 0) break; }
        for (r = rank + 1, f = file - 1; r < 8 && f >= 0; r++, f--) { attacks |= 1UL << (r * 8 + f); if ((blockers & (1UL << (r * 8 + f))) != 0) break; }
        for (r = rank - 1, f = file + 1; r >= 0 && f < 8; r--, f++) { attacks |= 1UL << (r * 8 + f); if ((blockers & (1UL << (r * 8 + f))) != 0) break; }
        for (r = rank - 1, f = file - 1; r >= 0 && f >= 0; r--, f--) { attacks |= 1UL << (r * 8 + f); if ((blockers & (1UL << (r * 8 + f))) != 0) break; }

        return attacks;
    }

    // useQueens param to make generating queen moves simple
    public static void Generate(BoardState state, List<Move> moves, bool useQueens = false)
    {
        ulong pieces = useQueens
            ? (state.IsWhiteTurn ? state.WhiteQueens : state.BlackQueens)
            : (state.IsWhiteTurn ? state.WhiteBishops : state.BlackBishops);
        ulong friendly = state.IsWhiteTurn ? state.WhitePieces : state.BlackPieces;
        PieceType pieceType = useQueens ? PieceType.Queen : PieceType.Bishop;

        while (pieces != 0)
        {
            int from = BitUtils.PopLsb(ref pieces);
            ulong occupancy = state.AllPieces & Masks[from];
            int index = (int)((occupancy * Magics[from]) >> Shifts[from]);
            ulong attacks = AttackTable[from][index] & ~friendly;

            while (attacks != 0)
            {
                int to = BitUtils.PopLsb(ref attacks);
                moves.Add(new Move(from, to, pieceType, state.GetPieceAt(to)));
            }
        }
    }

    public static ulong GetAttacks(ulong occupied, int sq)
    {
        ulong occ = occupied & Masks[sq];
        int index = (int)((occ * Magics[sq]) >> Shifts[sq]);
        return AttackTable[sq][index];
    }
}
