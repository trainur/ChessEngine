public class BitUtils
{
    // De Bruijn sequence trick
    // idfk

    private static readonly int[] DeBruijnIndex64 =
    {
        0, 1, 48, 2, 57, 49, 28, 3,
        61, 58, 50, 42, 38, 29, 17, 4,
        62, 55, 59, 36, 53, 51, 43, 22,
        45, 39, 33, 30, 24, 18, 12, 5,
        63, 47, 56, 27, 60, 41, 37, 16,
        54, 35, 52, 21, 44, 32, 23, 11,
        46, 26, 40, 15, 34, 20, 31, 10,
        25, 14, 19, 9, 13, 8, 7, 6
    };

    private const ulong DeBruijn64 = 0x03f79d71b4cb0a89UL;

    public static int BitScan(ulong bb)
    {
        if (bb == 0) throw new System.ArgumentException("Cannot BitScan an empty bitboard.");

        // Isolate least sig. set bit
        ulong lsb = bb & (~bb + 1);

        return DeBruijnIndex64[(lsb * DeBruijn64) >> 58];
    }

    public static int PopLsb(ref ulong bb)
    {
        int square = BitScan(bb);

        if ((bb & (1UL << square)) == 0)
            throw new System.Exception($"BitScan returned non-set square {square} for bitboard {bb:X16}");

        bb &= bb - 1;
        return square;
    }

    // Returns number of set bits in a ulong 
    public static int PopCount(ulong x)
    {
        int count = 0;
        while (x != 0) { x &= x - 1; count++; }
        return count;
    }
}
