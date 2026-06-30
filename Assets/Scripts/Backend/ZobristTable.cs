using System;

public static class ZobristTable
{
    // [piece 0-5][square 0-63][colour 0-1]
    public static readonly ulong[,,] Pieces = new ulong[6, 64, 2];
    public static readonly ulong[] EnpPassant = new ulong[64];
    public static readonly ulong[] CastlingRights = new ulong[16]; // 4 bits -> 2^4 = 16 combinations
    public static readonly ulong SideToMove;

    private static readonly int SEED = 8008135;

    static ZobristTable()
    {
        var rng = new Random(SEED);

        for (int p = 0; p< 6; p++)
            for (int sq = 0; sq < 64; sq++)
                for (int c = 0; c < 2; c++)
                    Pieces[p, sq, c] = NextUlong(rng);

        for (int sq = 0; sq < 64; sq++)
            EnpPassant[sq] = NextUlong(rng);

        for (int i = 0; i < 16; i++)
            CastlingRights[i] = NextUlong(rng);

        SideToMove = NextUlong(rng);
    }

    private static ulong NextUlong(Random rng)
    {
        byte[] buf = new byte[8];
        rng.NextBytes(buf);
        return BitConverter.ToUInt64(buf, 0);
    }

    public static int PieceIndex(PieceType p) => (int)p;

    public static int CastlingIndex(bool wK, bool wQ, bool bK, bool bQ) => (wK ? 1 : 0) | (wQ ? 2 : 0) | (bK ? 4 : 0) | (bQ ? 8 : 0);
}
