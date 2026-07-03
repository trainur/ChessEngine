using System;
public class FenParser
{
    public const string INITFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    public static BoardState? Parse(string fen)
    {
        if (fen == "") fen = INITFEN;

        BoardState state = new BoardState();

        try
        {
            string[] parts = fen.Trim().Split(' ');

            ParsePiecePlacement(parts[0], ref state);

            if (parts.Length > 1) state.IsWhiteTurn = parts[1] == "w";
            if (parts.Length > 2) ParseCastling(parts[2], ref state);
            if (parts.Length > 3) ParseEnPassant(parts[3], ref state);
            if (parts.Length > 4) state.HalfMoveClock = int.Parse(parts[4]);
            if (parts.Length > 5) state.FullMoveNumber = int.Parse(parts[5]);
        }
        catch (Exception e)
        {
            throw new ArgumentException($"Invalid override fen has been parsed: {fen}", e);
        }

        return state;
    }

    private static void ParsePiecePlacement(string placement, ref BoardState state)
    {
        string[] ranks = placement.Split('/');

        for (int rankIndex = 0; rankIndex < 8; rankIndex++)
        {
            int rank = 7 - rankIndex;
            int file = 0;

            foreach (char c in ranks[rankIndex])
            {
                if (char.IsDigit(c)) { file += c - '0'; continue; }

                int square = rank * 8 + file;
                ulong bit = 1UL << square;

                switch (c)
                {
                    case 'P': state.WhitePawns |= bit; break;
                    case 'N': state.WhiteKnights |= bit; break;
                    case 'B': state.WhiteBishops |= bit; break;
                    case 'R': state.WhiteRooks |= bit; break;
                    case 'Q': state.WhiteQueens |= bit; break;
                    case 'K': state.WhiteKing |= bit; break;
                    case 'p': state.BlackPawns |= bit; break;
                    case 'n': state.BlackKnights |= bit; break;
                    case 'b': state.BlackBishops |= bit; break;
                    case 'r': state.BlackRooks |= bit; break;
                    case 'q': state.BlackQueens |= bit; break;
                    case 'k': state.BlackKing |= bit; break;
                }

                file++;
            }
        }
    }

    private static void ParseCastling(string token, ref BoardState state)
    {
        state.WhiteKingsideCastle = token.Contains('K');
        state.WhiteQueensideCastle = token.Contains('Q');
        state.BlackKingsideCastle = token.Contains('k');
        state.BlackQueensideCastle = token.Contains('q');
    }

    private static void ParseEnPassant(string token, ref BoardState state)
    {
        if (token == "-") { state.EnPassantSquare = -1; return; }
        int file = token[0] - 'a';
        int rank = token[1] - '1';
        state.EnPassantSquare = rank * 8 + file;
    }

    public static string ToFen(in BoardState state)
    {
        string board = BoardToFen(in state);
        string turn = state.IsWhiteTurn ? "w" : "b";
        string castling = CastlingToFen(in state);
        string enPassant = state.EnPassantSquare == -1
            ? "-"
            : SquareToAlgebraic(state.EnPassantSquare);

        return $"{board} {turn} {castling} {enPassant} {state.HalfMoveClock} {state.FullMoveNumber}";
    }

    private static string BoardToFen(in BoardState state)
    {
        System.Text.StringBuilder fen = new();

        for (int rank = 7; rank >= 0; rank--)
        {
            int empty = 0;
            for (int file = 0; file < 8; file++)
            {
                int sq = rank * 8 + file;
                char piece = GetFenPieceAt(in state, sq);

                if (piece == '\0') empty++;
                else
                {
                    if (empty > 0)
                    {
                        fen.Append(empty);
                        empty = 0;
                    }

                    fen.Append(piece);
                }
            }

            if (empty > 0) fen.Append(empty);

            if (rank > 0) fen.Append('/');
        }

        return fen.ToString();
    }

    private static char GetFenPieceAt(in BoardState state, int sq)
    {
        ulong mask = 1UL << sq;

        if ((state.WhitePawns & mask) != 0) return 'P';
        if ((state.WhiteKnights & mask) != 0) return 'N';
        if ((state.WhiteBishops & mask) != 0) return 'B';
        if ((state.WhiteRooks & mask) != 0) return 'R';
        if ((state.WhiteQueens & mask) != 0) return 'Q';
        if ((state.WhiteKing & mask) != 0) return 'K';

        if ((state.BlackPawns & mask) != 0) return 'p';
        if ((state.BlackKnights & mask) != 0) return 'n';
        if ((state.BlackBishops & mask) != 0) return 'b';
        if ((state.BlackRooks & mask) != 0) return 'r';
        if ((state.BlackQueens & mask) != 0) return 'q';
        if ((state.BlackKing & mask) != 0) return 'k';

        return '\0';
    }

    private static string CastlingToFen(in BoardState state)
    {
        string castling = "";

        if (state.WhiteKingsideCastle) castling += "K";
        if (state.WhiteQueensideCastle) castling += "Q";
        if (state.BlackKingsideCastle) castling += "k";
        if (state.BlackQueensideCastle) castling += "q";

        return castling.Length == 0 ? "-" : castling;
    }

    private static string SquareToAlgebraic(int square)
    {
        int file = square % 8;
        int rank = square / 8;

        char fileChar = (char)('a' + file);
        char rankChar = (char)('1' + rank);

        return $"{fileChar}{rankChar}";
    }
}
