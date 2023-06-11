using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EgdeDancer
{
    public class TCache
    {
        const ulong BITMASK_VALUE = 0x1FFFF;  //   mask for the score + 65536

        private static uint SLOT_COUNT = 1 << 21;  // 2^21 => ~ 2M slots
        private ulong[] slots = new ulong[SLOT_COUNT];

        public ulong probeCount;
        public ulong hitCount;

        public TCache()
        {
            Reset();
        }
        public void Reset()
        {
            for (int i = 0; i < SLOT_COUNT; i++) slots[i] = 0;
            probeCount = 0;
            hitCount = 0;
        }
        int SlotNumber(ulong key)
        {
            return (int) (key & (SLOT_COUNT - 1));
        }
        public bool ProbeCache(ulong key, ref int value)
        {
            probeCount++;
            int slotIndex = SlotNumber(key);
            if (slots[slotIndex] == 0) return false;
            if ((slots[slotIndex] & ~BITMASK_VALUE) != (key & ~BITMASK_VALUE)) return false;

            value = (int) (slots[slotIndex] & BITMASK_VALUE) - 65536;
            hitCount++;
            return true;
        }
        public void Store2Cache(ulong key, int value)
        {
            int slotIndex = SlotNumber(key);
            slots[slotIndex] = (key & ~BITMASK_VALUE) | (uint)(value + 65536);
        }


    }
}
