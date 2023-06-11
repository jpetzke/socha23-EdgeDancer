namespace EgdeDancer
{
    /***************************************************
	 * history heuristics table
	 ***************************************************/
    internal class THistoryTable
    {
        public const int HISTORY_MAX_VALUE = 2048;
        private int[,] tbl = new int[(int)EPiece.PIECE_CNT, (int)ESquare.SQ_CNT];
        public THistoryTable() { Reset(); }
        public void Reset()
        {
            for (EPiece piece = EPiece.R_PENGUIN; piece < EPiece.PIECE_CNT; piece++)
                for (ESquare sq = ESquare.A1; sq < ESquare.SQ_CNT; sq++)
                    tbl[(int)piece, (int)sq] = 0;
        }
        public void Update(uint move, int bonus)
        {
            EPiece piece = Move.GetMovedPiece(move);
            ESquare sq = Move.GetToSquare(move);

            tbl[(int)piece, (int)sq] += Math.Min(bonus, HISTORY_MAX_VALUE / 2);

            if (tbl[(int)piece, (int)sq] >= HISTORY_MAX_VALUE || tbl[(int)piece, (int)sq] <= -HISTORY_MAX_VALUE) // over or underflow 
            {
                for (piece = EPiece.R_PENGUIN; piece < EPiece.PIECE_CNT; piece++)
                    for (sq = ESquare.A1; sq < ESquare.SQ_CNT; sq++)
                        tbl[(int)piece, (int)sq] /= 2;
            }
        }
        public int getScore(uint move) { return tbl[(int)Move.GetMovedPiece(move), (int)Move.GetToSquare(move)]; }
    }

}
