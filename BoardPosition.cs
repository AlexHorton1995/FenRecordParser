using System.Text;

namespace FenRecordParser;

class BoardPosition
{
    private const uint halfmoveClockMox = 100;
    private const uint fullmoveNumberMax = 500;

    private static BoardPosition buffer = new();

    // TODO: find more appropriate name for dictionary
    private static Dictionary<char, Action<ulong>> bitwiseOrTable = new() {
        { 'P', x => buffer.blitboard.pawns   |= x },
        { 'N', x => buffer.blitboard.knights |= x },
        { 'B', x => buffer.blitboard.bishops |= x },
        { 'R', x => buffer.blitboard.rooks   |= x },
        { 'Q', x => buffer.blitboard.queens  |= x },
        { 'K', x => buffer.blitboard.kings   |= x }
    };

    private static readonly Func<string, uint>[] parseStringMethod =
    {
        ParsePiecePlacement,
        ParseSideToMove,
        ParseCastlingRights,
        ParseEpTargetSquare,
        ParseHalfmoveClock,
        ParseFullmoveNumber
    };

    private string fenRecord;
    private Blitboard blitboard;

    private string errorMessage;
    private readonly uint errorCode;

    /* * * * * * * * * *
     *  CONSTRUCTORS   *
     * * * * * * * * * */

    private BoardPosition()
    {
        fenRecord = String.Empty;
        blitboard = new Blitboard();
        errorCode = 1;
        errorMessage = String.Empty;
    }

    public BoardPosition(string fr)
    {
        blitboard = new Blitboard()
        {
            epTargetSquare = 64,
            fullmoveNumber = 1
        };

        buffer.blitboard = new Blitboard()
        {
            epTargetSquare = 64,
            fullmoveNumber = 1
        };

        if (string.IsNullOrEmpty(fr))
        {
            fenRecord = string.Empty;
            errorCode = 1;
            errorMessage = "A null string was passed to constructor.";
            return;
        }

        string[] fields = fr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        fenRecord = string.Join(' ', fields);
        buffer.fenRecord = fenRecord;

        if (fields.Length > parseStringMethod.Length)
        {
            errorCode = 1;
            errorMessage = "The maximum number of fields was exceeded.";
            return;
        }

        for (int i = 0; i < fields.Length; i++)
        {
            errorCode = parseStringMethod[i](fields[i]);
            if (errorCode == 0) continue;
            errorMessage = buffer.errorMessage;
            return;
        }

        errorMessage = "The operation completed successfully.";
        CopyBlitboardFromBuffer();
    }

    public BoardPosition(Blitboard bb)
    {
        StringBuilder sb = new();
        fenRecord = string.Empty;
        blitboard = bb;

        ParsePiecePlacement(bb, ref sb);
        ParseSideToMove(bb, ref sb);
        ParseCastlingRights(bb, ref sb);
        ParseEpTargetSquare(bb, ref sb);
        ParseHalfmoveClock(bb, ref sb);
        ParseFullmoveNumber(bb, ref sb);

        errorCode = 0;
        errorMessage = "The operation completed successfully.";
        fenRecord = sb.ToString();
    }

    /* * * * * * * * * * *
     *  STRING PARSING   *
     * * * * * * * * * * */

    private static uint ParsePiecePlacement(string piecePlacement)
    {
        int squareCount = 0;
        int rankSquareCount = 0;

        for (int i = 0; i < piecePlacement.Length; i++)
        {
            if (squareCount > 63)
            {
                BuildErrorMessage("The board size was exceeded", 0, i);
                return 1;
            }

            char curr = piecePlacement[i];

            int rank = 7 - (squareCount >> 3);
            int file = squareCount & 7;
            int arrayIndex = (rank << 3) | file;

            if (curr >= '1' && curr <= '8')
            {
                int emptyStride = (int)(curr - '0');

                if (emptyStride + squareCount > 64)
                {
                    BuildErrorMessage($"The board size was exceeded, {emptyStride} + {squareCount}", 0, i);
                    return 1;
                }

                if (emptyStride > 8 - rankSquareCount)
                {
                    BuildErrorMessage("The rank width was exceeded", 0, i);
                    return 1;
                }

                rankSquareCount += emptyStride;
                squareCount += emptyStride;
                continue;
            }

            char upperChar = char.ToUpper(curr);

            if (bitwiseOrTable.ContainsKey(upperChar))
            {
                if (rankSquareCount > 7)
                {
                    BuildErrorMessage("The rank width was exceeded", 0, i);
                    return 1;
                }

                ulong square = 1UL << arrayIndex;
                bitwiseOrTable[upperChar](square);

                if (char.IsUpper(curr))
                    buffer.blitboard.white |= square;
                else buffer.blitboard.black |= square;

                rankSquareCount++;
                squareCount++;
                continue;
            }

            if (!curr.Equals('/'))
            {
                BuildErrorMessage($"Invalid character '{curr}' at index {i}", 0, i);
                return 1;
            }

            if (rankSquareCount < 8)
            {
                BuildErrorMessage("The rank is incomplete", 0, i);
                return 1;
            }

            if (rankSquareCount > 8)
            {
                BuildErrorMessage("The rank width was exceeded", 0, i);
                return 1;
            }

            rankSquareCount = 0;
            continue;
        }

        if (!squareCount.Equals(64))
        {
            BuildErrorMessage("The board is incomplete", 0, piecePlacement.Length - 1);
            return 1;
        }

        return 0;
    }

    private static uint ParseSideToMove(string sideToMove)
    {
        char parsed;

        try { parsed = char.Parse(sideToMove); }
        catch (Exception)
        {
            BuildErrorMessage("The length of the argument is not 1", 1, 0);
            return 1;
        }

        switch (parsed)
        {
            case 'w': buffer.blitboard.sideToMove = 0; return 0;
            case 'b': buffer.blitboard.sideToMove = 1; return 0;
        }

        BuildErrorMessage($"Invalid character '{parsed}'", 1, 0);
        return 1;
    }

    private static uint ParseCastlingRights(string castlingRights)
    {
        if (castlingRights.Equals("-")) return 0;

        string trimmed = castlingRights.TrimStart(new char[] { 'K', 'k', 'Q', 'q'});
        if (!string.IsNullOrEmpty(trimmed))
        {
            int index = castlingRights.Length - trimmed.Length;
            BuildErrorMessage($"Invalid character '{trimmed[0]}' at index {index}", 2, index);
            return 1;
        }

        ulong whiteKings = buffer.blitboard.kings & buffer.blitboard.white;
        if ((whiteKings & (1UL << 4)) != 0)
        {
            ulong whiteRooks = buffer.blitboard.rooks & buffer.blitboard.white;
            if (castlingRights.Contains('K') && ((whiteRooks & (1UL << 7)) != 0))
                buffer.blitboard.castlingRights |= 1 << 0;
            if (castlingRights.Contains('Q') && ((whiteRooks & (1UL << 0)) != 0))
                buffer.blitboard.castlingRights |= 1 << 1;
        }

        ulong blackKings = buffer.blitboard.kings & buffer.blitboard.black;
        if ((blackKings & (1UL << 60)) != 0)
        {
            ulong blackRooks = buffer.blitboard.rooks & buffer.blitboard.black;
            if (castlingRights.Contains('k') && ((blackRooks & (1UL << 63)) != 0))
                buffer.blitboard.castlingRights |= 1 << 2;
            if (castlingRights.Contains('q') && ((blackRooks & (1UL << 56)) != 0))
                buffer.blitboard.castlingRights |= 1 << 3;
        }

        return 0;
    }

    private static uint ParseEpTargetSquare(string epTargetSquare)
    {
        if (epTargetSquare.Length > 2)
        {
            BuildErrorMessage("Invalid length", 3, 2);
            return 1;
        }

        if (epTargetSquare.Equals("-"))
            return 0;

        if (epTargetSquare[0] < 'a' || epTargetSquare[0] > 'h')
        {
            BuildErrorMessage($"Invalid character '{epTargetSquare[0]}' at index 0", 3, 0);
            return 1;
        }

        if (epTargetSquare[1] < '1' || epTargetSquare[1] > '8')
        {
            BuildErrorMessage($"Invalid character '{epTargetSquare[1]}' at index 1", 3, 1);
            return 1;
        }

        switch (epTargetSquare[1])
        {
            case '3': buffer.blitboard.epTargetSquare = (uint)(epTargetSquare[0] - 'a') + 16; return 0;
            case '6': buffer.blitboard.epTargetSquare = (uint)(epTargetSquare[0] - 'a') + 40; return 0;
        }

        buffer.blitboard.epTargetSquare = 64;
        return 0;
    }

    private static uint ParseHalfmoveClock(string halfmoveClock)
    {
        try { buffer.blitboard.halfmoveClock = uint.Parse(halfmoveClock); }
        catch (Exception ex) when (ex is ArgumentException || ex is FormatException)
        {
            BuildErrorMessage("The argument is not a number", 4, 0);
            return 1;
        }
        catch (OverflowException)
        {
            BuildErrorMessage("The argument is out of range", 4, 0);
            return 1;
        }

        if (buffer.blitboard.halfmoveClock > 9999)
        {
            BuildErrorMessage("The argument is out of range", 4, 0);
            return 1;
        }

        Math.Clamp(buffer.blitboard.halfmoveClock, 0, halfmoveClockMox);
        return 0;
    }

    private static uint ParseFullmoveNumber(string fullmoveNumber)
    {
        try { buffer.blitboard.fullmoveNumber = uint.Parse(fullmoveNumber); }
        catch (Exception ex) when (ex is ArgumentException || ex is FormatException)
        {
            BuildErrorMessage("The argument is not a number", 5, 0);
            return 1;
        }
        catch (OverflowException)
        {
            BuildErrorMessage("The argument is out of range", 5, 0);
            return 1;
        }

        if (buffer.blitboard.halfmoveClock > 9999)
        {
            BuildErrorMessage("The argument is out of range", 5, 0);
            return 1;
        }

        Math.Clamp(buffer.blitboard.halfmoveClock, 1, fullmoveNumberMax);
        return 0;
    }

    /* * * * * * * * * * * *
     *  BLITBOARD PARSING  *
     * * * * * * * * * * * */

    private static uint ParsePiecePlacement(Blitboard bb, ref StringBuilder sb)
    {
        sb.Append(new string('1', 64));
        
        foreach ((char, ulong) t in new[] {
            ('P', bb.white & bb.pawns),
            ('N', bb.white & bb.knights),
            ('B', bb.white & bb.bishops),
            ('R', bb.white & bb.rooks),
            ('Q', bb.white & bb.queens),
            ('K', bb.white & bb.kings),
            ('p', bb.black & bb.pawns),
            ('n', bb.black & bb.knights),
            ('b', bb.black & bb.bishops),
            ('r', bb.black & bb.rooks),
            ('q', bb.black & bb.queens),
            ('k', bb.black & bb.kings),
        }) {
            ulong bitboard = t.Item2;
            while (bitboard > 0)
            {
                int bbIndex = DeBruijn.BitScanForward(bitboard);
                int rank = bbIndex >> 3;
                int file = bbIndex & 7;
                int sbIndex = ((7 - rank) << 3) + file;
                sb[sbIndex] = t.Item1;
                bitboard &= bitboard - 1;
            }
        }

        for (int i = 56; i > 0; i -=8)
            sb.Insert(i, '/');

        for (int i = 8; i > 1; i--)
            sb.Replace(new string('1', i), new string(new char[] { (char)('0' + i) }));

        sb.Append(' ');
        return 0;
    }

    private static uint ParseSideToMove(Blitboard bb, ref StringBuilder sb)
    {
        switch (bb.sideToMove & 1)
        {
            case 0: sb.Append("w "); break;
            case 1: sb.Append("b "); break;
        }

        return 0;
    }

    private static uint ParseCastlingRights(Blitboard bb, ref StringBuilder sb)
    {
        uint castlingRights = bb.castlingRights & 15;

        if (castlingRights.Equals(0))
        {
            sb.Append("- ");
            return 0;
        }

        ulong whiteKings = bb.kings & bb.white;
        if ((whiteKings & (1UL << 4)) != 0)
        {
            ulong whiteRooks = bb.rooks & bb.white;
            if ((castlingRights & (1 << 0)) != 0 && ((whiteRooks & (1UL << 7)) != 0))
                sb.Append('K');
            if ((castlingRights & (1 << 1)) != 0 && ((whiteRooks & (1UL << 0)) != 0))
                sb.Append('Q');
        }

        ulong blackKings = bb.kings & bb.black;
        if ((blackKings & (1UL << 60)) != 0)
        {
            ulong blackRooks = bb.rooks & bb.black;
            if ((castlingRights & (1 << 2)) != 0 && ((blackRooks & (1UL << 63)) != 0))
                sb.Append('k');
            if ((castlingRights & (1 << 3)) != 0 && ((blackRooks & (1UL << 56)) != 0))
                sb.Append('q');
        }

        sb.Append(' ');

        return 0;
    }

    private static uint ParseEpTargetSquare(Blitboard bb, ref StringBuilder sb)
    {
        uint epTargetSquare = Math.Clamp(bb.epTargetSquare, 0, 64);

        uint rank = epTargetSquare >> 3;
        uint file = epTargetSquare & 7;

        if (epTargetSquare.Equals(64) || !(rank.Equals(2) || rank.Equals(5)))
        {
            sb.Append("- ");
            return 0;
        }

        sb.Append((char)('a' + file));
        sb.Append((char)('1' + rank));
        sb.Append(' ');

        return 0;
    }

    private static uint ParseHalfmoveClock(Blitboard bb, ref StringBuilder sb)
    {
        uint halfmoveClock = Math.Clamp(bb.halfmoveClock, 0, halfmoveClockMox);
        sb.Append(halfmoveClock);
        sb.Append(' ');
        return 0;
    }

    private static uint ParseFullmoveNumber(Blitboard bb, ref StringBuilder sb)
    {
        uint fullmoveNumber = Math.Clamp(bb.fullmoveNumber, 1, fullmoveNumberMax);
        sb.Append(fullmoveNumber);
        return 0;
    }

    /* * * * * * * * * * *
     *  ERROR CHECKING   *
     * * * * * * * * * * */

    private static void BuildErrorMessage(string message, int fieldId, int index)
    {
        StringBuilder sb = new();
        sb.AppendLine(message);
        sb.AppendLine(buffer.fenRecord);

        int leftIndex = 0;
        for (int i = 0; i < fieldId; i++)
            leftIndex = buffer.fenRecord.IndexOf(' ', leftIndex) + 1;

        int rightIndex = buffer.fenRecord.IndexOf(' ', leftIndex);
        if (rightIndex.Equals(-1))
            rightIndex = buffer.fenRecord.Length;

        int diffIndex = rightIndex - leftIndex;

        sb.Append(' ', leftIndex);
        sb.Append('~', index);
        sb.Append('^');
        sb.Append('~', diffIndex - index - 1);

        switch (fieldId)
        {
            case 0: sb.Append("\nPiece placement data could not be parsed."); break;
            case 1: sb.Append("\nSide to move could not be parsed."); break;
            case 2: sb.Append("\nCastling rights could not be parsed."); break;
            case 3: sb.Append("\nEn passant target square could not be parsed."); break;
            case 4: sb.Append("\nHalfmove clock could not be parsed."); break;
            case 5: sb.Append("\nFullmove number could not be parsed."); break;
            default: break;
        }

        buffer.errorMessage = sb.ToString();
    }

    public bool Succeeded() { return errorCode.Equals(0); }

    public uint GetErrorCode() { return errorCode; }

    public string GetErrorMessage() { return errorMessage; }

    /* * * * * * * 
     *  GETTERS  *
     * * * * * * */

    public string GetFenRecord() { return fenRecord; }

    public Blitboard GetBlitboard() { return blitboard; }

    /* * * * * * * * * * * *
     *  COPY FROM BUFFER   *
     * * * * * * * * * * * */

    private void CopyBlitboardFromBuffer()
    {
        blitboard.pawns = buffer.blitboard.pawns;
        blitboard.knights = buffer.blitboard.knights;
        blitboard.bishops = buffer.blitboard.bishops;
        blitboard.rooks = buffer.blitboard.rooks;
        blitboard.queens = buffer.blitboard.queens;
        blitboard.kings = buffer.blitboard.kings;
        blitboard.white = buffer.blitboard.white;
        blitboard.black = buffer.blitboard.black;
        blitboard.sideToMove = buffer.blitboard.sideToMove;
        blitboard.castlingRights = buffer.blitboard.castlingRights;
        blitboard.epTargetSquare = buffer.blitboard.epTargetSquare;
        blitboard.halfmoveClock = buffer.blitboard.halfmoveClock;
        blitboard.fullmoveNumber = buffer.blitboard.fullmoveNumber;
    }
}