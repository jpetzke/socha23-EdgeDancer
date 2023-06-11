using System.Diagnostics;

namespace EgdeDancer
{
    public enum ETTResponse { TT_NOTFOUND, TT_EXACT, TT_ALPHA, TT_BETA };

    /* THashEntry ->
    stores the hash data in a condensed 16 byte format */
    struct THashEntry
    {
        public ulong key;
        public uint data_md;
        public uint data_mpta;
    };

    /* THashResult ->
        a data structure that contains those hash elements that are necessary
        for further processing w/o age and ply which are used internally only */
    public struct THashResult
    {
        public ETTResponse flag;
        public int merit;
        public int depth;
        public uint move;
    };

    public class Transposition
    {
        // BITMASKs to apply
        const int BITMASK_AGE = 0x7;
        const int BITMASK_DEPTH = 0x7F;
        const int BITMASK_MOVE = (1 << 23) - 1;
        const int BITMASK_TYPE = 0x7;
        const int BITMASK_PLY = 0x1FF;
        const int BITMASK_MERIT = 0x1FFFF;

        // After masking the bits shift them into the right position 
        const int BITSHIFT_AGE = 29;
        const int BITSHIFT_DEPTH = 25;
        const int BITSHIFT_MOVE = 0;
        const int BITSHIFT_TYPE = 0;
        const int BITSHIFT_PLY = 3;
        const int BITSHIFT_MERIT = 12;

        private static uint SLOT_COUNT = (1024 * 1024 * 512) / 16;  // size of TT = 512 MB, sizeof THashEntry = 16
        private static ulong HASH_MASK = SLOT_COUNT - 1;
        private THashEntry[] entries = new THashEntry[SLOT_COUNT];
        
        /***************************************************************************
         *   THashEntry contains the following attributes
         *
         *  key  : the zobrist key of the position
         *  data_md : a 32 bit integer with the following structure
         *
         *     off    bits   name     description
         *      0     23     move     the best move, only 23 bits are used the order value 
         *							   from the move (highest 9 bits) is discarded
         *     23      2     unused   currently unused
         *     25      7     depth    the depth of the entry was searched to
         *
         *  data_mpta : a 32 bit integer with the following structure
         *
         *      0      3     type     type of score (invalid, exact, alpha ...)
         *      3      9     ply      the ply of the game the entry was written
         *     12     17     merit    the score of the entry + 65536
         *     29      3     age	  the reverse age of the hash entry,	
         *                            entries with age 0 are replacement candid.				
         *****************************************************************************/
        public Transposition()
        {
            //SetSizeInMB(512);
        }
        public uint SetSizeInMB(uint mbSize)
        {
            // make sure the hash size is a power of 2 and check the bounds 1 - 2048
            mbSize = (uint)(Math.Pow(2, Math.Floor(Math.Log(Math.Max(1, Math.Min(2048, mbSize))) / Math.Log(2.0))));

            SLOT_COUNT = (1024 * 1024 * mbSize) / 16;
            if ((uint)SLOT_COUNT == HASH_MASK + 1) return mbSize;

            HASH_MASK = SLOT_COUNT - 1;

            entries = new THashEntry[SLOT_COUNT];
            for (int i = 0; i < SLOT_COUNT; i++) entries[i] = new THashEntry();
            if (entries == null) return 0;

            return mbSize;
        }
        public void ResetHash()
        {
            // delete all keys so the entries are considered free
            for (int i = 0; i < SLOT_COUNT; i++) entries[i].key = 0;
        }
        public void AgeHash()
        {
        }
        public ETTResponse ProbeHash(ulong key, uint ply, ref THashResult hashResult)
        {
            /*******************************************************************************
            *  probe the transposition table
            *  if entry is found return it in hashEntry
            *  Result: TT_NOTFOUND                  -> entry was not found
            *  Result: TT_EXACT, TT_ALPHA, TT_BETA  -> entry was found
            ********************************************************************************/
            int slotNo = HashFct(key);
            hashResult.flag = ETTResponse.TT_NOTFOUND;

            // not found
            if (entries[slotNo].key != key) return ETTResponse.TT_NOTFOUND;

            // we have an entry here, so we return it
            hashResult.flag = (ETTResponse)THashEntry_GET_TYPE(ref entries[slotNo]);
            hashResult.depth = (int)THashEntry_GET_DEPTH(ref entries[slotNo]);
            hashResult.merit = Value_from_TT(THashEntry_GET_MERIT(ref entries[slotNo]), (int)ply);
            hashResult.move = THashEntry_GET_MOVE(ref entries[slotNo]);

            return hashResult.flag;
        }
        public void Store2Hash(ulong key, int aFromPly, int aDepth, int aFlag, int aMerit, uint move)
        {
            int slotNo = HashFct(key);

            entries[slotNo].key = key;
            THashEntry_SET_ALL(ref entries[slotNo], 0, aFlag, move, aDepth, Value_to_TT(aMerit, aFromPly));
            return;
        }

        // private access functions
        private static void THashEntry_SET_AGE(ref THashEntry hashEntry, uint aAge)
        {
            hashEntry.data_mpta = hashEntry.data_mpta & ~(BITMASK_AGE << BITSHIFT_AGE) | (aAge << BITSHIFT_AGE);
        }
        private static uint THashEntry_GET_AGE(ref THashEntry hashEntry)
        {
            return (hashEntry.data_mpta >> BITSHIFT_AGE) & BITMASK_AGE;
        }
        private static void THashEntry_SET_TYPE(ref THashEntry hashEntry, uint aType)
        {
            hashEntry.data_mpta = (uint)(hashEntry.data_mpta & ~(BITMASK_TYPE << BITSHIFT_TYPE) | (aType << BITSHIFT_TYPE));
        }
        private static uint THashEntry_GET_TYPE(ref THashEntry hashEntry)
        {
            return (hashEntry.data_mpta >> BITSHIFT_TYPE) & BITMASK_TYPE;
        }
        private static void THashEntry_SET_MOVE(ref THashEntry hashEntry, uint move)
        {
            hashEntry.data_md = (uint)(hashEntry.data_md & ~(BITMASK_MOVE << BITSHIFT_MOVE) | (move << BITSHIFT_MOVE));
        }
        private static uint THashEntry_GET_MOVE(ref THashEntry hashEntry)
        {
            return (hashEntry.data_md >> BITSHIFT_MOVE) & BITMASK_MOVE;
        }
        private static void THashEntry_SET_DEPTH(ref THashEntry hashEntry, int aDepth)
        {
            hashEntry.data_md = (uint)(hashEntry.data_md & ~(BITMASK_DEPTH << BITSHIFT_DEPTH) | (uint)(aDepth << BITSHIFT_DEPTH));
        }
        private static uint THashEntry_GET_DEPTH(ref THashEntry hashEntry)
        {
            return ((hashEntry.data_md >> BITSHIFT_DEPTH) & BITMASK_DEPTH);
        }
        private static void THashEntry_SET_PLY(ref THashEntry hashEntry, int aPly)
        {
            hashEntry.data_mpta = (uint)(hashEntry.data_mpta & ~(BITMASK_PLY << BITSHIFT_PLY) | (uint)(aPly << BITSHIFT_PLY));
        }
        private static uint THashEntry_GET_PLY(ref THashEntry hashEntry)
        {
            return (hashEntry.data_mpta >> BITSHIFT_PLY) & BITMASK_PLY;
        }
        private static void THashEntry_SET_MERIT(ref THashEntry hashEntry, int aMerit)
        {
            hashEntry.data_mpta = (uint)(hashEntry.data_mpta & ~(BITMASK_MERIT << BITSHIFT_MERIT) | (uint)((aMerit + 65536) << BITSHIFT_MERIT));
        }
        private static int THashEntry_GET_MERIT(ref THashEntry hashEntry)
        {
            return (int)(((hashEntry.data_mpta >> BITSHIFT_MERIT) & BITMASK_MERIT) - 65536);
        }
        private static void THashEntry_SET_ALL(ref THashEntry hashEntry, int aAge, int aType, uint aMove, int aDepth, int aMerit)
        {
            hashEntry.data_md = (uint)((aMove & BITMASK_MOVE) | (uint)((aDepth) << BITSHIFT_DEPTH));
            hashEntry.data_mpta = (uint)(aType | ((aMerit + 65536) << BITSHIFT_MERIT) | (aAge << BITSHIFT_AGE));
        }

        // function to hash the zobist key into a table entry
        private int HashFct(ulong key)
        {
            return (int)(key & HASH_MASK);
        }

        // function to get a bucketOffset using some in between bits of the key
        private static int GetBucketOffset(ulong key)
        {
            return (int)((key >> 32) & 3);
        }
        private int Value_to_TT(int value, int ply)
        {
            return value >= Types.VALUE_MATE_IN_MAX_PLY ? value + ply : value <= Types.VALUE_MATED_IN_MAX_PLY ? value - ply : value;
        }
        private int Value_from_TT(int value, int ply)
        {
            return value == (int)EScores.NO_SCORE ? (int)EScores.NO_SCORE : value >= Types.VALUE_MATE_IN_MAX_PLY ? value - ply : value <= Types.VALUE_MATED_IN_MAX_PLY ? value + ply : value;
        }

    }
}
