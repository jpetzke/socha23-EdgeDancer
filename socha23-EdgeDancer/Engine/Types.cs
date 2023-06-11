namespace EgdeDancer
{
    public enum EScores
    {
        NO_SCORE = -100000,
        DRAW = 0,
        INFINITY = 32767,
    }
    public enum ESquare
    {
        A1, B1, C1, D1, E1, F1, G1, H1,
        A2, B2, C2, D2, E2, F2, G2, H2,
        A3, B3, C3, D3, E3, F3, G3, H3,
        A4, B4, C4, D4, E4, F4, G4, H4,
        A5, B5, C5, D5, E5, F5, G5, H5,
        A6, B6, C6, D6, E6, F6, G6, H6,
        A7, B7, C7, D7, E7, F7, G7, H7,
        A8, B8, C8, D8, E8, F8, G8, H8,
        SQ_CNT, ER
    }

    public enum EPiece
    {
        R_PENGUIN, B_PENGUIN, PIECE_CNT, EMPTY, FISH_1, FISH_2, FISH_3, FISH_4
    }

    public enum EColor { RED, BLUE, BOTH_SIDES, NO_COLOR }
    public enum ENullMove { NULL_MOVE_NO, NULL_MOVE_OK }
    public enum EMoveList {  HASH_MOVE_LIST, REMAINING_MOVES, ALL_MOVES }
    static class Types
    {
        public const string NAME = "ED";
        public const string NAME_FULL = "EgdeDancer";
        public const int VERSION = 2;
        public const int SUBVERSION = 27;
        public const bool DEBUG = false;

        public const int MATE_SCORE = (int)EScores.INFINITY;

        // It is possible that one side is trapped early and makes null moves which will lead to a longer game
        public const int MAX_PLY = 128; 
        public const int VALUE_MATE_IN_MAX_PLY = MATE_SCORE - 2 * MAX_PLY;
        public const int VALUE_MATED_IN_MAX_PLY = -MATE_SCORE + 2 * MAX_PLY;

        public const int PENGUIN_COUNT = 4;

        public static readonly string[] PIECE_SYMBOLS =
        {
            "r", "b", "E", "0", "1", "2", "3", "4"
        };

        public static readonly string[] COLOR_NAMES = { "Red", "Blue", "No Color" };  

        public static readonly string[] SQUARE_NAMES =
        {    
            "a1", "b1", "c1", "d1", "e1", "f1", "g1", "h1",
            "a2", "b2", "c2", "d2", "e2", "f2", "g2", "h2",
            "a3", "b3", "c3", "d3", "e3", "f3", "g3", "h3",
            "a4", "b4", "c4", "d4", "e4", "f4", "g4", "h4",
            "a5", "b5", "c5", "d5", "e5", "f5", "g5", "h5",
            "a6", "b6", "c6", "d6", "e6", "f6", "g6", "h6",
            "a7", "b7", "c7", "d7", "e7", "f7", "g7", "h7",
            "a8", "b8", "c8", "d8", "e8", "f8", "g8", "h8", "er"
        };


        // BITBOARD INT64 SQUARE IDENTIFIER 
        public static readonly ulong[] BB_SQUARES = 
        {
            0x0000000000000001, 0x0000000000000002, 0x0000000000000004, 0x0000000000000008, 0x0000000000000010, 0x0000000000000020, 0x0000000000000040, 0x0000000000000080, 
            0x0000000000000100, 0x0000000000000200, 0x0000000000000400, 0x0000000000000800, 0x0000000000001000, 0x0000000000002000, 0x0000000000004000, 0x0000000000008000, 
            0x0000000000010000, 0x0000000000020000, 0x0000000000040000, 0x0000000000080000, 0x0000000000100000, 0x0000000000200000, 0x0000000000400000, 0x0000000000800000, 
            0x0000000001000000, 0x0000000002000000, 0x0000000004000000, 0x0000000008000000, 0x0000000010000000, 0x0000000020000000, 0x0000000040000000, 0x0000000080000000, 
            0x0000000100000000, 0x0000000200000000, 0x0000000400000000, 0x0000000800000000, 0x0000001000000000, 0x0000002000000000, 0x0000004000000000, 0x0000008000000000, 
            0x0000010000000000, 0x0000020000000000, 0x0000040000000000, 0x0000080000000000, 0x0000100000000000, 0x0000200000000000, 0x0000400000000000, 0x0000800000000000, 
            0x0001000000000000, 0x0002000000000000, 0x0004000000000000, 0x0008000000000000, 0x0010000000000000, 0x0020000000000000, 0x0040000000000000, 0x0080000000000000, 
            0x0100000000000000, 0x0200000000000000, 0x0400000000000000, 0x0800000000000000, 0x1000000000000000, 0x2000000000000000, 0x4000000000000000, 0x8000000000000000,
            0x0000000000000000
        };

        // File and Rank Numbers per Square
        public static readonly int[] FILE_NO = 
        { 
            0, 1, 2, 3, 4, 5, 6, 7,
            0, 1, 2, 3, 4, 5, 6, 7,
            0, 1, 2, 3, 4, 5, 6, 7,
            0, 1, 2, 3, 4, 5, 6, 7,
            0, 1, 2, 3, 4, 5, 6, 7,
            0, 1, 2, 3, 4, 5, 6, 7,
            0, 1, 2, 3, 4, 5, 6, 7,
            0, 1, 2, 3, 4, 5, 6, 7 
        };

        public static readonly int[] RANK_NO = 
        { 
            0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 3,
            4, 4, 4, 4, 4, 4, 4, 4,
            5, 5, 5, 5, 5, 5, 5, 5,
            6, 6, 6, 6, 6, 6, 6, 6,
            7, 7, 7, 7, 7, 7, 7, 7 
        };

        // Color of the pieces Red for Red Penguin, Blue for Blue Penguin
        public static readonly EColor[] COLOR_OF = { EColor.RED, EColor.BLUE, EColor.NO_COLOR };

        // Fishes per FISH PIECE TYPE
        public static readonly int[] FISH_COUNT = { 0, 0, 0, 0, 1, 2, 3, 4 };

        // Piece per FISH Cnt
        public static readonly EPiece[] PIECE_OF_FISH = { EPiece.EMPTY, EPiece.FISH_1, EPiece.FISH_2, EPiece.FISH_3, EPiece.FISH_4 };



        // Calculate the Square A1..H8 based on file and rank
        // Note: we have ranks with 7 and ranks with 8 files, so we add a square for every other rank
        public static ESquare GetSquare(int file, int rank)
        {
            return (ESquare) (8 * rank + file);
        }

        public static readonly bool[] SQUARE_TO_SE =
        {
            false, false, false, false, false, false, false, false,
            true,  true,  true,  true,  true,  true,  true,  true,
            true,  true,  true,  true,  true,  true,  true,  false,
            true,  true,  true,  true,  true,  true,  true,  true,
            true,  true,  true,  true,  true,  true,  true,  false,
            true,  true,  true,  true,  true,  true,  true,  true,
            true,  true,  true,  true,  true,  true,  true,  false,
            true,  true,  true,  true,  true,  true,  true,  true,
        };

        public static readonly bool[] SQUARE_TO_SW =
        {
            false, false, false, false, false, false, false, false,
            false, true,  true,  true,  true,  true,  true,  true,
            true,  true,  true,  true,  true,  true,  true,  true,
            false, true,  true,  true,  true,  true,  true,  true,
            true,  true,  true,  true,  true,  true,  true,  true,
            false, true,  true,  true,  true,  true,  true,  true,
            true,  true,  true,  true,  true,  true,  true,  true,
            false,  true,  true,  true,  true,  true,  true,  true,
        };

        public static readonly bool[] SQUARE_TO_NW =
        {
            true,  true,  true,  true,  true,  true,  true,  true,
            false, true,  true,  true,  true,  true,  true,  true,
            true,  true,  true,  true,  true,  true,  true,  true,
            false, true,  true,  true,  true,  true,  true,  true,
            true,  true,  true,  true,  true,  true,  true,  true,
            false, true,  true,  true,  true,  true,  true,  true,
            true,  true,  true,  true,  true,  true,  true,  true,
            false, false, false, false, false, false, false, false,
        };

        public static readonly bool[] SQUARE_TO_NE =
        {
            true,  true,  true,  true,  true,  true,  true,  false,
            true,  true,  true,  true,  true,  true,  true,  true,
            true,  true,  true,  true,  true,  true,  true,  false,
            true,  true,  true,  true,  true,  true,  true,  true,
            true,  true,  true,  true,  true,  true,  true,  false,
            true,  true,  true,  true,  true,  true,  true,  true,
            true,  true,  true,  true,  true,  true,  true,  false,
            false, false, false, false, false, false, false, false,

        };

        public static readonly int[] SQUARE_BONUS =
        {
            1, 2, 2, 2, 2, 2, 2, 0,
            1, 4, 4, 4, 4, 4, 4, 3,
            3, 4, 5, 5, 5, 5, 4, 1,
            1, 4, 5, 6, 6, 5, 4, 3,
            3, 4, 5, 6, 6, 5, 4, 1,
            1, 4, 5, 5, 5, 5, 4, 3,
            3, 4, 4, 4, 4, 4, 4, 1,
            0, 2, 2, 2, 2, 2, 2, 1
        };



    }
}

