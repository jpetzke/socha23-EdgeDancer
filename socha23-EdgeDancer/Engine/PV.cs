namespace EgdeDancer
{
    public class TPV
    {
        public uint[] pv = new uint[64]; // pv buffer for pv collected during search
        private ulong[] hashes = new ulong[64];
        private uint[] moves = new uint[64];
        int moveCnt;

        public TPV()
        {
            Reset();
        }
        public void Reset()
        {
            moveCnt = 0;
            for (int i = 0; i < 64; i++)
            {
                pv[i] = moves[i] = Move.INVALID_MOVE;
                hashes[i] = 0;
            }
        }
        public bool AddPVMove(int ply, uint move, ulong hash)
        {
            if (ply > 60) return false;

            // check whether we have this position already in the pv, then we don't add the move and return false
            for (int i = 0; i < ply; i++)
            {
                if (hashes[i] == hash) return false;
            }

            moves[ply] = move;
            moves[ply + 1] = Move.INVALID_MOVE; // move sentinel
            hashes[ply] = hash;
            moveCnt = ply + 1;

            return true;
        }

        /***************************************************************************************
		* Switch function that selects from the available data which pv data is used
		* 1. option: pv data from search
		* 2. option: pv collected from hash table
		***************************************************************************************/
        public uint[] GetPV()
        {
            if (pv[0] != Move.INVALID_MOVE) return pv;

            moves[moveCnt] = Move.INVALID_MOVE;  // add sentinel
            return moves;
        }

        /**************************************************************************************
		* Return the stored PV as string list of moves
		* If we have an available pv from search it is preferred - the pv[] entries are used
		* Otherwise we use the entries we collected from the hash table - moves[] entries
		**************************************************************************************/
        public string GetPVAsStr()
        {
            string result = "";
            int i;
            uint[] pvMoves = GetPV();

            for (i = 0; i < 60; i++)
            {
                if (pvMoves[i] == Move.INVALID_MOVE) break;
                if (i > 0) result += " ";
                result += Move.SAN(pvMoves[i]);
            }

            return result;
        }

        // this returns the 2nd move in the PV, this is the best move for the opponent and the
        // one we like to ponder on
        public string GetPonderMove()
        {
            string result = "";
            if (moveCnt > 1) result = Move.SAN(moves[1]);
            if (pv[0] != Move.INVALID_MOVE && pv[1] != Move.INVALID_MOVE) result = Move.SAN(pv[1]);
            return result;
        }

        // return the hash at position idx
        public ulong GetHashAt(int idx)
        {
            return hashes[idx];
        }

        // return the move at position idx
        public uint GetMoveAt(int idx)
        {
            return moves[idx];
        }
    }
}
