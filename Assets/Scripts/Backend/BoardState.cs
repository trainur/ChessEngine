using System;

public struct BoardState
{
    public ulong WhitePawns;
    public ulong WhiteKnights;
    public ulong WhiteBishops;
    public ulong WhiteRooks;
    public ulong WhiteQueens;
    public ulong WhiteKing;
    public ulong WhitePieces => WhitePawns | WhiteKnights | WhiteBishops | WhiteRooks | WhiteQueens | WhiteKing;

    public ulong BlackPawns;
    public ulong BlackKnights;
    public ulong BlackBishops;
    public ulong BlackRooks;
    public ulong BlackQueens;
    public ulong BlackKing;
    public ulong BlackPieces => BlackPawns | BlackKnights | BlackBishops | BlackRooks | BlackQueens | BlackKing;

    public ulong AllPieces => WhitePieces | BlackPieces;

    public bool WhiteKingsideCastle;
    public bool WhiteQueensideCastle;
    public bool BlackKingsideCastle;
    public bool BlackQueensideCastle;

    public int EnPassantSquare;

    public int HalfMoveClock;
    public int FullMoveNumber;

    public bool IsWhiteTurn;

    public ulong ZobristKey;

    public BoardState ApplyMove(Move move)
    {
        BoardState next = this;
        next.MakeMove(move);
        return next;
    }

    public UndoInfo MakeMove(Move move)
    {
        int capturedSquare =
            move.Flag == MoveFlag.EnPassant ? IsWhiteTurn ? move.To - 8 : move.To + 8 : move.To;

        // Capture the current state and move details
        UndoInfo undoInfo = new UndoInfo
        {
            CapturePiece = move.CapturePiece,
            CapturedSquare = capturedSquare,

            WhiteKingsideCastle = WhiteKingsideCastle,
            WhiteQueensideCastle = WhiteQueensideCastle,
            BlackKingsideCastle = BlackKingsideCastle,
            BlackQueensideCastle = BlackQueensideCastle,

            EnPassantSquare = EnPassantSquare,
            HalfMoveClock = HalfMoveClock,
            FullMoveNumber = FullMoveNumber,

            IsWhiteTurn = IsWhiteTurn,
            ZobristKey = ZobristKey
        };

        // XOR out old rights and info
        // Castling
        ZobristKey ^= ZobristTable.CastlingRights[ZobristTable.CastlingIndex(WhiteKingsideCastle, WhiteQueensideCastle, BlackKingsideCastle, BlackQueensideCastle)];

        // En passant
        if (EnPassantSquare != -1) ZobristKey ^= ZobristTable.EnpPassant[EnPassantSquare];

        // XOR move piece
        // Move piece
        XorPiece(move.Piece, move.From, IsWhiteTurn); // XOR out
        XorPiece(move.Piece, move.To, IsWhiteTurn); // XOR in

        ulong fromMask = 1UL << move.From;
        ulong toMask = 1UL << move.To;

        ref ulong bitboard = ref GetBitboard(ref this, move.Piece, IsWhiteTurn);
        bitboard &= ~fromMask;
        bitboard |= toMask;

        // Reset en passant
        EnPassantSquare = -1;

        // Cond. block handles capturing pieces, halfmove clocks, promotion, en passant
        if (move.CapturePiece.HasValue)
        {
            // XOR out captured piece
            XorPiece(move.CapturePiece.Value, capturedSquare, !IsWhiteTurn);

            HalfMoveClock = 0;

            ulong capturedMask = 1UL << capturedSquare;

            ref ulong capturedBitboard = ref GetBitboard(ref this, move.CapturePiece.Value, !IsWhiteTurn);
            capturedBitboard &= ~capturedMask;
        }
        else if (move.Piece == PieceType.Pawn)
        {
            HalfMoveClock = 0;        }
        else
        {
            HalfMoveClock += 1;
        }

        // Promotion, Castle, En passant
        if (move.Flag.HasValue) switch (move.Flag.Value)
            {
                case MoveFlag.PromoteKnight:
                    {
                        XorPiece(PieceType.Pawn, move.To, IsWhiteTurn); // Undo pawn arrival
                        XorPiece(PieceType.Knight, move.To, IsWhiteTurn); // Replace with knight

                        bitboard &= ~toMask;
                        ref ulong promoBitboard = ref GetBitboard(ref this, PieceType.Knight, IsWhiteTurn);
                        promoBitboard |= toMask;
                        break;
                    }
                case MoveFlag.PromoteBishop:
                    {
                        XorPiece(PieceType.Pawn, move.To, IsWhiteTurn); // Undo pawn arrival
                        XorPiece(PieceType.Bishop, move.To, IsWhiteTurn); // Replace with bishop

                        bitboard &= ~toMask;
                        ref ulong promoBitboard = ref GetBitboard(ref this, PieceType.Bishop, IsWhiteTurn);
                        promoBitboard |= toMask;
                        break;
                    }
                case MoveFlag.PromoteRook:
                    {
                        XorPiece(PieceType.Pawn, move.To, IsWhiteTurn); // Undo pawn arrival
                        XorPiece(PieceType.Rook, move.To, IsWhiteTurn); // Replace with rook

                        bitboard &= ~toMask;
                        ref ulong promoBitboard = ref GetBitboard(ref this, PieceType.Rook, IsWhiteTurn);
                        promoBitboard |= toMask;
                        break;
                    }
                case MoveFlag.PromoteQueen:
                    {
                        XorPiece(PieceType.Pawn, move.To, IsWhiteTurn); // Undo pawn arrival
                        XorPiece(PieceType.Queen, move.To, IsWhiteTurn); // Replace with queen

                        bitboard &= ~toMask;
                        ref ulong promoBitboard = ref GetBitboard(ref this, PieceType.Queen, IsWhiteTurn);
                        promoBitboard |= toMask;
                        break;
                    }
                case MoveFlag.DoublePawnPush:
                    {
                        // En passant square lives behind pawn to location
                        EnPassantSquare = IsWhiteTurn ? move.To - 8 : move.To + 8;

                        ZobristKey ^= ZobristTable.EnpPassant[EnPassantSquare];
                        break;
                    }
                case MoveFlag.CastleQueenside:
                    {
                        int rookFrom, rookTo;

                        rookFrom = IsWhiteTurn ? 0 : 56;  // a1 or a8
                        rookTo = IsWhiteTurn ? 3 : 59;  // d1 or d8

                        XorPiece(PieceType.Rook, rookFrom, IsWhiteTurn); // XOR out before rook
                        XorPiece(PieceType.Rook, rookTo, IsWhiteTurn); // Place rook

                        ref ulong rooks = ref GetBitboard(ref this, PieceType.Rook, IsWhiteTurn);
                        rooks &= ~(1UL << rookFrom);
                        rooks |= (1UL << rookTo);
                        break;
                    }
                case MoveFlag.CastleKingside:
                    {
                        int rookFrom, rookTo;

                        rookFrom = IsWhiteTurn ? 7 : 63;  // h1 or h8
                        rookTo = IsWhiteTurn ? 5 : 61;  // f1 or f8

                        XorPiece(PieceType.Rook, rookFrom, IsWhiteTurn); // XOR out before rook
                        XorPiece(PieceType.Rook, rookTo, IsWhiteTurn); // Place rook

                        ref ulong rooks = ref GetBitboard(ref this, PieceType.Rook, IsWhiteTurn);
                        rooks &= ~(1UL << rookFrom);
                        rooks |= (1UL << rookTo);
                        break;
                    }
                case MoveFlag.EnPassant:
                    {
                        // Logic already handled
                        break;
                    }
            }


        if (!IsWhiteTurn) FullMoveNumber += 1;

        // Castling Privileges
        ref bool kingside = ref (IsWhiteTurn ? ref WhiteKingsideCastle : ref BlackKingsideCastle);
        ref bool queenside = ref (IsWhiteTurn ? ref WhiteQueensideCastle : ref BlackQueensideCastle);

        if (move.Piece == PieceType.King)
        {
            kingside = false;
            queenside = false;
        }
        else if (move.Piece == PieceType.Rook)
        {
            // Queenside
            ulong rookQueenMask = IsWhiteTurn ? BoardConstants.WhiteQueensideRook : BoardConstants.BlackQueensideRook;
            if ((fromMask & rookQueenMask) != 0) queenside = false;

            // Kingside
            ulong rookKingMask = IsWhiteTurn ? BoardConstants.WhiteKingsideRook : BoardConstants.BlackKingsideRook;
            if ((fromMask & rookKingMask) != 0) kingside = false;
        }

        // If opponent's rook was captured, revoke their castling rights
        if (move.CapturePiece == PieceType.Rook)
        {
            ref bool oppKingside = ref (IsWhiteTurn ? ref BlackKingsideCastle : ref WhiteKingsideCastle);
            ref bool oppQueenside = ref (IsWhiteTurn ? ref BlackQueensideCastle : ref WhiteQueensideCastle);

            ulong oppKingsideRook = IsWhiteTurn ? BoardConstants.BlackKingsideRook : BoardConstants.WhiteKingsideRook;
            ulong oppQueensideRook = IsWhiteTurn ? BoardConstants.BlackQueensideRook : BoardConstants.WhiteQueensideRook;

            if ((toMask & oppKingsideRook) != 0) oppKingside = false;
            if ((toMask & oppQueensideRook) != 0) oppQueenside = false;
        }

        // Update Zobrist castling rights
        ZobristKey ^= ZobristTable.CastlingRights[ZobristTable.CastlingIndex(WhiteKingsideCastle, WhiteQueensideCastle, BlackKingsideCastle, BlackQueensideCastle)];

        // Flip side to move
        ZobristKey ^= ZobristTable.SideToMove;

        IsWhiteTurn = !IsWhiteTurn;

        return undoInfo;
    }

    public void UnmakeMove(Move move, UndoInfo undoInfo)
    {
        IsWhiteTurn = undoInfo.IsWhiteTurn;

        ulong fromMask = 1UL << move.From;
        ulong toMask = 1UL << move.To;

        // Undo promotion or normal piece movement
        if (move.Flag.HasValue && IsPromotion(move.Flag.Value))
        {
            PieceType promotedPiece = GetPromotionPiece(move.Flag.Value);

            ref ulong promoBitboard = ref GetBitboard(ref this, promotedPiece, IsWhiteTurn);
            promoBitboard &= ~toMask;

            ref ulong pawnBitboard = ref GetBitboard(ref this, PieceType.Pawn, IsWhiteTurn);
            pawnBitboard |= fromMask;
        }
        else
        {
            ref ulong bitboard = ref GetBitboard(ref this, move.Piece, IsWhiteTurn);
            bitboard &= ~toMask;
            bitboard |= fromMask;
        }

        //Undo castling rook movement
        if (move.Flag.HasValue)
        {
            switch (move.Flag.Value)
            {
                case MoveFlag.CastleQueenside:
                    {
                        int rookFrom = IsWhiteTurn ? 0 : 56;
                        int rookTo = IsWhiteTurn ? 3 : 59;

                        ref ulong rooks = ref GetBitboard(ref this, PieceType.Rook, IsWhiteTurn);
                        rooks &= ~(1UL << rookTo);
                        rooks |= 1UL << rookFrom;
                        break;
                    }
                case MoveFlag.CastleKingside:
                    {
                        int rookFrom = IsWhiteTurn ? 7 : 63;
                        int rookTo = IsWhiteTurn ? 5 : 61;

                        ref ulong rooks = ref GetBitboard(ref this, PieceType.Rook, IsWhiteTurn);
                        rooks &= ~(1UL << rookTo);
                        rooks |= 1UL << rookFrom;
                        break;
                    }
            }
        }

        // Restore captured piece
        if (undoInfo.CapturePiece.HasValue)
        {
            ref ulong capturedBitboard = ref GetBitboard(ref this, undoInfo.CapturePiece.Value, !IsWhiteTurn);
            capturedBitboard |= 1UL << undoInfo.CapturedSquare;
        }

        // Restore reversible state
        WhiteKingsideCastle = undoInfo.WhiteKingsideCastle;
        WhiteQueensideCastle = undoInfo.WhiteQueensideCastle;
        BlackKingsideCastle = undoInfo.BlackKingsideCastle;
        BlackQueensideCastle = undoInfo.BlackQueensideCastle;

        EnPassantSquare = undoInfo.EnPassantSquare;
        HalfMoveClock = undoInfo.HalfMoveClock;
        FullMoveNumber = undoInfo.FullMoveNumber;

        ZobristKey = undoInfo.ZobristKey;
    }

    private static ref ulong GetBitboard(ref BoardState state, PieceType piece, bool isWhite)
    {
        switch (piece)
        {
            case PieceType.Pawn: return ref (isWhite ? ref state.WhitePawns : ref state.BlackPawns);
            case PieceType.Knight: return ref (isWhite ? ref state.WhiteKnights : ref state.BlackKnights);
            case PieceType.Bishop: return ref (isWhite ? ref state.WhiteBishops : ref state.BlackBishops);
            case PieceType.Rook: return ref (isWhite ? ref state.WhiteRooks : ref state.BlackRooks);
            case PieceType.Queen: return ref (isWhite ? ref state.WhiteQueens : ref state.BlackQueens);
            case PieceType.King: return ref (isWhite ? ref state.WhiteKing : ref state.BlackKing);
            default: throw new ArgumentException("Unknown piece type");
        }
    }

    public PieceType? GetPieceAt(int sq)
    {
        ulong mask = 1UL << sq;

        if ((WhitePawns & mask) != 0 || (BlackPawns & mask) != 0) return PieceType.Pawn;
        if ((WhiteKnights & mask) != 0 || (BlackKnights & mask) != 0) return PieceType.Knight;
        if ((WhiteBishops & mask) != 0 || (BlackBishops & mask) != 0) return PieceType.Bishop;
        if ((WhiteRooks & mask) != 0 || (BlackRooks & mask) != 0) return PieceType.Rook;
        if ((WhiteQueens & mask) != 0 || (BlackQueens & mask) != 0) return PieceType.Queen;
        if ((WhiteKing & mask) != 0 || (BlackKing & mask) != 0) return PieceType.King;

        return null;
    }

    public bool IsPieceActive(int sq)
    {
        ulong mask = 1UL << sq;
        ulong pieceBoard = IsWhiteTurn ? WhitePieces : BlackPieces;

        return (mask & pieceBoard) != 0;
    }

    public bool IsTerminalState() => IsMate() || IsStalemate() || IsFifty() || IsInsufficientMaterial();

    public bool IsMate() => MoveGenerator.GenerateMoves(ref this).Count == 0 && IsCheck();

    public bool IsCheck()
    {
        ulong kingBoard = IsWhiteTurn ? WhiteKing : BlackKing;
        int kingSquare = BitUtils.BitScan(kingBoard);

        return MoveGenerator.IsSquareAttacked(ref this, kingSquare, byWhite: !IsWhiteTurn);
    }

    public bool IsStalemate() => MoveGenerator.GenerateMoves(ref this).Count == 0 && !IsCheck();

    public bool IsFifty() => HalfMoveClock >= 100;

    public bool IsInsufficientMaterial()
    {
        // King vs King
        if (BitUtils.PopCount(AllPieces) == 2) return true;

        bool InsufficientMinor(ulong pieces, ulong bishops, ulong knights) => BitUtils.PopCount(pieces) == 2 && (BitUtils.PopCount(bishops) == 1 || BitUtils.PopCount(knights) == 1);

        // King + Bishop/Knight vs King
        if (BitUtils.PopCount(WhiteKing) == 1 && InsufficientMinor(BlackPieces, BlackBishops, BlackKnights)) return true;
        if (BitUtils.PopCount(BlackKing) == 1 && InsufficientMinor(WhitePieces, WhiteBishops, WhiteKnights)) return true;

        // King + Bishop vs King + Bishop, bishops on same coloured squares
        // Two pieces per, one bishop each
        if (BitUtils.PopCount(WhitePieces) == 2 && BitUtils.PopCount(BlackPieces) == 2 &&
            BitUtils.PopCount(WhiteBishops) == 1 && BitUtils.PopCount(BlackBishops) == 1)
        {
            int whiteBSq = BitUtils.BitScan(WhiteBishops);
            int blackBSq = BitUtils.BitScan(BlackBishops);

            int wFile = whiteBSq % 8;
            int wRank = whiteBSq / 8;

            int bFile = blackBSq % 8;
            int bRank = blackBSq / 8;

            return (wFile + bFile) % 2 == (wRank + bRank) % 2;
        }

        return false;
    }

    private static bool IsPromotion(MoveFlag flag)
    {
        return flag == MoveFlag.PromoteKnight ||
               flag == MoveFlag.PromoteBishop ||
               flag == MoveFlag.PromoteRook ||
               flag == MoveFlag.PromoteQueen;
    }

    private static PieceType GetPromotionPiece(MoveFlag flag)
    {
        switch (flag)
        {
            case MoveFlag.PromoteKnight:
                return PieceType.Knight;

            case MoveFlag.PromoteBishop:
                return PieceType.Bishop;

            case MoveFlag.PromoteRook:
                return PieceType.Rook;

            case MoveFlag.PromoteQueen:
                return PieceType.Queen;

            default:
                throw new ArgumentException("Move flag is not a promotion flag");
        }
    }

    private void XorPiece(PieceType type, int square, bool isWhite)
    {
        ZobristKey ^= ZobristTable.Pieces[ZobristTable.PieceIndex(type), square, isWhite ? 0 : 1];
    }
}

public struct UndoInfo
{
    public PieceType? CapturePiece;
    public int CapturedSquare;

    public bool WhiteKingsideCastle;
    public bool WhiteQueensideCastle;
    public bool BlackKingsideCastle;
    public bool BlackQueensideCastle;

    public int EnPassantSquare;
    public int HalfMoveClock;
    public int FullMoveNumber;

    public bool IsWhiteTurn;

    public ulong ZobristKey;
}