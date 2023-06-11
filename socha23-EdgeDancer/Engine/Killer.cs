using System.Diagnostics;

namespace EgdeDancer
{
    internal class TKiller
    {
        uint[,] slots = new uint[64, 3];

        public TKiller()
        {
            ResetSlots();
        }
        public void ResetSlots()
        {
            for (int i = 0; i < slots.GetLength(0); i++)
                for (int j = 0; j < slots.GetLength(1); j++) slots[i, j] = 0;
        }
        public void StoreNewKiller(int ply, uint move)
        {
            Debug.Assert(ply >= 0 && ply <= 60);
            Debug.Assert(move != Move.INVALID_MOVE);
            Debug.Assert(move == Move.GetID(move));

            slots[ply, 2] = move;  // we store the move in our threat slot

            // check whether the move is already in, then do nothing
            if (move == slots[ply, 0]) return;

            slots[ply, 1] = slots[ply, 0];
            slots[ply, 0] = move;
        }
        public void GetKillers(int ply, ref uint move1, ref uint move2)
        {
            if (ply < 0 || ply > 60)
            {
                move1 = Move.INVALID_MOVE;
                move2 = Move.INVALID_MOVE;
                return;
            }

            move1 = slots[ply, 0];
            move2 = slots[ply, 1];
        }
        public void StoreThreatMove(int ply, uint move)
        {
            Debug.Assert(ply >= 0 && ply <= 60);

            slots[ply, 2] = move;
        }
        public uint GetThreatMove(int ply)
        {
            Debug.Assert(ply >= 0 && ply <= 60);

            return slots[ply, 2];
        }
    }

    class TCounterMoves
    {
        private uint[,,,] slots = new uint[(int)EPiece.PIECE_CNT, (int)ESquare.SQ_CNT, (int)EPiece.PIECE_CNT, (int)ESquare.SQ_CNT];

        public TCounterMoves()
        {
            ResetSlots();
        }
        public void ResetSlots()
        {
            for (EPiece piece1 = EPiece.R_PENGUIN; piece1 < EPiece.PIECE_CNT; piece1++)
                for (ESquare sq1 = ESquare.A1; sq1 < ESquare.SQ_CNT; sq1++)
                    for (EPiece piece2 = EPiece.R_PENGUIN; piece2 < EPiece.PIECE_CNT; piece2++)
                        for (ESquare sq2 = ESquare.A1; sq2 < ESquare.SQ_CNT; sq2++)
                            slots[(int)piece1, (int)sq1, (int)piece2, (int)sq2] = Move.INVALID_MOVE;
        }
        public void StoreNewCounter(uint mv1, uint mv2, uint counter)
        {
            slots[(int)Move.GetMovedPiece(mv1), (int)Move.GetToSquare(mv1), (int)Move.GetMovedPiece(mv2), (int)Move.GetToSquare(mv2)] = counter;
        }
        public uint GetCounter(uint mv1, uint mv2)
        {
            return slots[(int)Move.GetMovedPiece(mv1), (int)Move.GetToSquare(mv1), (int)Move.GetMovedPiece(mv2), (int)Move.GetToSquare(mv2)];
        }
    };
}
