using System;
public class FenParser
{
    private const string INITFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

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
}
