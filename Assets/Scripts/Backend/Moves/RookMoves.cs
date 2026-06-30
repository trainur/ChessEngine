using System.Collections.Generic;

public class RookMoves
{
    // https://www.chessprogramming.org/Magic_Bitboards
    // Magic numbers via: https://github.com/maksimKorzh/chess_programming/blob/master/src/magics/magics.c

    private static readonly ulong[] Masks = new ulong[64];
    private static readonly ulong[] Magics =
    {
        0xa8002c000108020UL,
        0x6c00049b0002001UL,
        0x100200010090040UL,
        0x2480041000800801UL,
        0x280028004000800UL,
        0x900410008040022UL,
        0x280020001001080UL,
        0x2880002041000080UL,
        0xa000800080400034UL,
        0x4808020004000UL,
        0x2290802004801000UL,
        0x411000d00100020UL,
        0x402800800040080UL,
        0xb000401004208UL,
        0x2409000100040200UL,
        0x1002100004082UL,
        0x22878001e24000UL,
        0x1090810021004010UL,
        0x801030040200012UL,
        0x500808008001000UL,
        0xa08018014000880UL,
        0x8000808004000200UL,
        0x201008080010200UL,
        0x801020000441091UL,
        0x800080204005UL,
        0x1040200040100048UL,
        0x120200402082UL,
        0xd14880480100080UL,
        0x12040280080080UL,
        0x100040080020080UL,
        0x9020010080800200UL,
        0x813241200148449UL,
        0x491604001800080UL,
        0x100401000402001UL,
        0x4820010021001040UL,
        0x400402202000812UL,
        0x209009005000802UL,
        0x810800601800400UL,
        0x4301083214000150UL,
        0x204026458e001401UL,
        0x40204000808000UL,
        0x8001008040010020UL,
        0x8410820820420010UL,
        0x1003001000090020UL,
        0x804040008008080UL,
        0x12000810020004UL,
        0x1000100200040208UL,
        0x430000a044020001UL,
        0x280009023410300UL,
        0xe0100040002240UL,
        0x200100401700UL,
        0x2244100408008080UL,
        0x8000400801980UL,
        0x2000810040200UL,
        0x8010100228810400UL,
        0x2000009044210200UL,
        0x4080008040102101UL,
        0x40002080411d01UL,
        0x2005524060000901UL,
        0x502001008400422UL,
        0x489a000810200402UL,
        0x1004400080a13UL,
        0x4000011008020084UL,
        0x26002114058042UL
    };
    private static readonly int[] Shifts = new int[64];
    public static readonly ulong[][] AttackTable = new ulong[64][];

    static RookMoves()
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

        // Generate mask of up, down, left, right moves.
        /* 0 0 1 0 0
         * 0 0 1 0 0
         * 1 1 r 1 1
         * 0 0 1 0 0
         * 0 0 1 0 0
         */

        int r, f;
        for (r = rank + 1; r <= 6; r++) mask |= 1UL << (r * 8 + file);
        for (r = rank - 1; r >= 1; r--) mask |= 1UL << (r * 8 + file);
        for (f = file + 1; f <= 6; f++) mask |= 1UL << (rank * 8 + f);
        for (f = file - 1; f >= 1; f--) mask |= 1UL << (rank * 8 + f);

        return mask;
    }

    private static ulong ComputeAttacks(int sq, ulong blockers)
    {
        ulong attacks = 0;
        int rank = sq / 8, file = sq % 8;

        int r, f;
        for (r = rank + 1; r < 8; r++) { attacks |= 1UL << (r * 8 + file); if ((blockers & (1UL << (r * 8 + file))) != 0) break; }
        for (r = rank - 1; r >= 0; r--) { attacks |= 1UL << (r * 8 + file); if ((blockers & (1UL << (r * 8 + file))) != 0) break; }
        for (f = file + 1; f < 8; f++) { attacks |= 1UL << (rank * 8 + f); if ((blockers & (1UL << (rank * 8 + f))) != 0) break; }
        for (f = file - 1; f >= 0; f--) { attacks |= 1UL << (rank * 8 + f); if ((blockers & (1UL << (rank * 8 + f))) != 0) break; }

        return attacks;
    }

    // useQueens param to make generating queen moves simple
    public static void Generate(BoardState state, List<Move> moves, bool useQueens = false)
    {
        ulong pieces = useQueens
            ? (state.IsWhiteTurn ? state.WhiteQueens : state.BlackQueens)
            : (state.IsWhiteTurn ? state.WhiteRooks : state.BlackRooks);
        ulong friendly = state.IsWhiteTurn ? state.WhitePieces : state.BlackPieces;
        PieceType pieceType = useQueens ? PieceType.Queen : PieceType.Rook;

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
