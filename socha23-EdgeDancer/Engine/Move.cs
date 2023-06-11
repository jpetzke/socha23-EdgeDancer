using System.Diagnostics;

namespace EgdeDancer
{
    public struct TRootMove
    {
        public uint move;
        public int value;
        public int previousValue;
        public int data;
        public uint[] pv;
    }

    internal class Move
    {
        public const uint INVALID_MOVE = 0;
        
        // Move Integer
        // 0 -> Color
        // 1-6 -> From Square
        // 7-12 -> To Square
        // 13-15 -> Score Gain (Fishes captured)
        // 16-31 -> Some Value

        const int SHIFT_MOVED_PIECE = 0;
        const int SHIFT_FROM_SQUARE = 1;
        const int SHIFT_TO_SQUARE = 7;
        const int SHIFT_FISH = 13;
        const int SHIFT_VALUE = 16;

        const uint MASK_MOVED_PIECE = 0x01;
        const uint MASK_FROM_SQUARE = 0x3F;
        const uint MASK_TO_SQUARE = 0x3F;
        const uint MASK_FISH = 0x07;
        const uint MASK_MOVEID = 0xFFFF;
        const uint MASK_VALUE = 0xFFFF;

        public const uint NULL_MOVE = (uint) EPiece.EMPTY << SHIFT_FISH;

        public static uint Create(EPiece movedPiece, ESquare fromSquare, ESquare toSquare, EPiece fish)
        {
            return (uint)fish << SHIFT_FISH | (uint)toSquare << SHIFT_TO_SQUARE | (uint)fromSquare << SHIFT_FROM_SQUARE | (uint)movedPiece << SHIFT_MOVED_PIECE;
        }

        public static uint Create(EPiece movedPiece, ESquare toSquare)   // a set move collects always <1> fish and to and from square are equal
        {
            return (uint)EPiece.FISH_1 << SHIFT_FISH | (uint)toSquare << SHIFT_TO_SQUARE | (uint)toSquare << SHIFT_FROM_SQUARE | (uint)movedPiece << SHIFT_MOVED_PIECE;
        }

        public static ESquare GetFromSquare(uint move)
        {
            return (ESquare)((move >> SHIFT_FROM_SQUARE) & MASK_FROM_SQUARE);
        }

        public static ESquare GetToSquare(uint move)
        {
            return (ESquare)((move >> SHIFT_TO_SQUARE) & MASK_TO_SQUARE);
        }

        public static EPiece GetMovedPiece(uint move)
        {
            return (EPiece)((move >> SHIFT_MOVED_PIECE) & MASK_MOVED_PIECE);
        }

        public static EPiece GetFish(uint move)
        {
            return (EPiece)((move >> SHIFT_FISH) & MASK_FISH);
        }
        public static int GetFishCount(uint move)
        {
            return Types.FISH_COUNT[(int) GetFish(move)];
        }
        public static uint GetValue(uint move)
        {
            return (uint)((move >> SHIFT_VALUE) & MASK_VALUE);
        }
        public static uint SetValue(uint move, uint value)
        {
            move |= (value << SHIFT_VALUE);
            return move;
        }
        public static uint GetID(uint move)
        {
            return move & MASK_MOVEID;
        }

        public static string GetInfo(uint move)
        {
            if (GetID(move) == NULL_MOVE) return "0000";
            return Move.GetMovedPiece(move) + " " + Types.SQUARE_NAMES[(int)Move.GetFromSquare(move)] + Types.SQUARE_NAMES[(int)Move.GetToSquare(move)] + " [" + Move.GetFish(move) + "]";
        }
        public static string SAN(uint move)
        {
            if (GetID(move) == NULL_MOVE) return "0000";
            return Types.SQUARE_NAMES[(int)Move.GetFromSquare(move)] + Types.SQUARE_NAMES[(int)Move.GetToSquare(move)];
        }
        public static bool IsNullMove(uint move)
        {
            return move == NULL_MOVE;
        }
        public static bool IsSetMove(uint move)
        {
            // A set move has an equal to and from square
            return GetToSquare(move) == GetFromSquare(move);
        }
        public static int FromX(uint move)
        {
            // Calculates the X value (file) in the coordinate system of the SW challenge
            if (IsNullMove(move)) return 0;
            ESquare from = GetFromSquare(move);

            int x = Types.FILE_NO[(int)from];
            return x;
        }
        public static int FromY(uint move)
        {
            // Calculates the Y value (rank) in the reverse coordinate system of the SW challenge
            if (IsNullMove(move)) return 0;
            ESquare from = GetFromSquare(move);

            int y = Types.RANK_NO[(int)from];
            return 7 - y;
        }
        public static int ToX(uint move)
        {
            // Calculates the X value (file) in the coordinate system of the SW challenge
            if (IsNullMove(move)) return 0;
            ESquare to = GetToSquare(move);

            int x = Types.FILE_NO[(int)to];
            return x;
        }
        public static int ToY(uint move)
        {
            // Calculates the Y value (rank) in the coordinate system of the SW challenge
            if (IsNullMove(move)) return 0;
            ESquare to = GetToSquare(move);

            int y = Types.RANK_NO[(int)to];
            return 7 - y;
        }

    }
    public class TMoveList
    {
        public int count;
        public uint[] moves = new uint[256];    // In the initial placement phase a high number of moves is possible ca. 4*60
 
        public TMoveList() { count = 0; }
        public void AddMove(uint move) { Debug.Assert(count < 256); moves[count++] = move; }
        public uint GetMoveAt(int index) { return moves[index]; }
        void DeleteMoveAt(int idx)
        {
            if (idx < 0 || idx >= count) return;

            moves[idx] = moves[count - 1];
            count--;
        }
        public void DeleteMove(uint move)
        {
            Debug.Assert(move == Move.GetID(move));

            for (int i = 0; i < count; i++)
                if (moves[i] == move)
                {
                    DeleteMoveAt(i);
                    break;
                }
        }
        public void Clear() { count = 0; }
        public void Sort()
        {
            // we can sort the move int directly as the ordervalue is in the most significant bits
            for (int i = 1; i < count; ++i)
            {
                uint key = moves[i];
                int j = i - 1;

                // Move elements of arr[0..i-1], that are greater than key,
                while (j >= 0 && moves[j] < key)
                {
                    moves[j + 1] = moves[j];
                    j = j - 1;
                }
                moves[j + 1] = key;
            }
        }
        public void Print()
        {
            for (int i=0; i < count; i++)
            {
                Console.Write("{0}[{1}] ", Move.SAN(moves[i]), Move.GetValue(moves[i]));
                if ((i > 0) && (i % 25 == 0 || i == count - 1)) Console.WriteLine("");
            }
        }

    }

    public class TDivideList : TMoveList
    {
        public ulong[] perfts = new ulong[256];
        public void AddMove(uint move, ulong perft) { Debug.Assert(count < 256); perfts[count] = perft;  moves[count++] = move; }
    }

    public class TRootMoveList
    {
        public int count;
        public TRootMove[] moves = new TRootMove[256];    // maximum possible moves on a board

        public TRootMoveList() { count = 0; for (int i = 0; i < moves.Length; i++) moves[i].pv = new uint[64]; }
        public TRootMoveList(ref TMoveList movelist)
        {
            count = 0;
            for (int i = 0; i < moves.Length; i++) moves[i].pv = new uint[60];
            for (int i = 0; i < movelist.count; i++) Add(movelist.moves[i]);
        }
        public void Add(uint move)
        {
            moves[count].move = move;
            moves[count].value = (int)EScores.NO_SCORE;
            moves[count].data = 0;
            moves[count].pv[0] = Move.INVALID_MOVE;
            count++;
        }
        public TRootMove GetMoveAt(int index) { return moves[index]; }
        public void Clear() { count = 0; }

        /******************************************************************
        * Save all scores to previousValue field and set them to NOVALUE
        ******************************************************************/
        public void ResetScores()
        {
            for (int i = 0; i < count; i++)
            {
                moves[i].previousValue = moves[i].value;
                moves[i].value = (int)EScores.NO_SCORE;
            }
        }

        /******************************************************************
         * return the value of the nth move in the sorted !!! list
         * can be used to inititalize alpha in multipv mode
         ******************************************************************/
        public int GetMinAlpha(int multipv)
        {
            int idx = Math.Min(multipv - 1, count - 1);
            return moves[idx].value;
        }

        public void Sort()
        {
            for (int i = 1; i < count; ++i)
            {
                TRootMove key = moves[i];
                int j = i - 1;

                // Move elements of arr[0..i-1], that are greater than key,
                // to one position ahead of their current position
                while (j >= 0 && moves[j].value < key.value)
                {
                    moves[j + 1] = moves[j];
                    j = j - 1;
                }
                moves[j + 1] = key;
            }
        }

        /******************************************************************
        * set the data for the move at position idx
        ******************************************************************/
        public void SetDataAt(int idx, int value, int data)
        {
            if ((idx < 0) || (idx >= count)) return;

            moves[idx].value = value;
            moves[idx].data = data;
        }

        /******************************************************************
         * set the pv line for the given move
         ******************************************************************/
        public void SetPVforMove(uint move, ref uint[] pv)
        {
            int i = 0;
            while (i < count && Move.GetID(moves[i].move) != Move.GetID(move)) i++;   // locate the move index

            if (i < count)
            {
                moves[i].pv[0] = move;
                for (int j = 0; j < 60; j++)
                {
                    if (Move.INVALID_MOVE == (moves[i].pv[j + 1] = pv[j])) break;    // add the moves from the subtree
                }
            }
        }
        public void MoveToTop(uint move)
        {
            // move the root move entry that contains the given move to the 
            // top of the list, shift all other entries one slot to the right
            TRootMove key;

            // if we have no elements we quit
            if (count == 0) return;

            // search for the move in the list
            int i = 0;
            while ((i < count) && (Move.GetID(moves[i].move) != Move.GetID(move))) i++;

            if (i >= count) return;   // move was not found in the list
            key = moves[i];           // move was found at i, save the complete root move information

            // shift all root moves 1 slot to the right and set the first element to the requested
            for (int j = i; j > 0; j--) moves[j] = moves[j - 1];
            moves[0] = key;
        }

    }
}
