using System.Text;

namespace FenRecordParser;

class Printer
{
    public static void PrintBitboard(ulong bitboard)
    {
        StringBuilder sb = new("0x" + bitboard.ToString("X") + "\n");
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                int index = ((7 - i) << 3) + j;
                ulong mask = 1UL << index;
                sb.Append(" " + ((bitboard & mask) != 0 ? '1' : '.'));
            }
            sb.Append('\n');
        }
        Console.WriteLine(sb.ToString());
    }

    public static void PrintBlitboard(Blitboard blitboard)
    {
        StringBuilder sb = new();
        sb.Append('.', 64);

        foreach ((char, ulong) t in new[] {
            ('P', blitboard.white & blitboard.pawns),
            ('N', blitboard.white & blitboard.knights),
            ('B', blitboard.white & blitboard.bishops),
            ('R', blitboard.white & blitboard.rooks),
            ('Q', blitboard.white & blitboard.queens),
            ('K', blitboard.white & blitboard.kings),
            ('p', blitboard.black & blitboard.pawns),
            ('n', blitboard.black & blitboard.knights),
            ('b', blitboard.black & blitboard.bishops),
            ('r', blitboard.black & blitboard.rooks),
            ('q', blitboard.black & blitboard.queens),
            ('k', blitboard.black & blitboard.kings),
        })
        {
            ulong bitboard = t.Item2;
            while (bitboard > 0)
            {
                int bbIndex = DeBruijn.BitScanForward(bitboard);
                int sbIndex = ((7 - (bbIndex >> 3)) << 3) + (bbIndex & 7);
                sb[sbIndex] = t.Item1;
                bitboard &= bitboard - 1;
            }
        }

        for (int i = 56; i > 0; i -= 8)
            sb.Insert(i, '\n');

        for (int i = sb.Length; i >= 0; i--)
            sb.Insert(i, ' ');

        Console.WriteLine(sb.ToString());
    }
}
