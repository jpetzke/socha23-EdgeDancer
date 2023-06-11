#define BMI

using System.Runtime.Intrinsics.X86;

namespace EgdeDancer
{
    public static class BitOps
    {
#if !BMI
        private const ulong Magic = 0x37E84A99DAE458F;

        private static readonly int[] MagicTable =
        {
                0, 1, 17, 2, 18, 50, 3, 57,
                47, 19, 22, 51, 29, 4, 33, 58,
                15, 48, 20, 27, 25, 23, 52, 41,
                54, 30, 38, 5, 43, 34, 59, 8,
                63, 16, 49, 56, 46, 21, 28, 32,
                14, 26, 24, 40, 53, 37, 42, 7,
                62, 55, 45, 31, 13, 39, 36, 6,
                61, 44, 12, 35, 60, 11, 10, 9,
        };
#endif

        public static int GetLsb(ulong value)
        {
#if BMI
            return (int)Bmi1.X64.TrailingZeroCount(value);
#else
            return MagicTable[((ulong)((long)value & -(long)value) * Magic) >> 58];
#endif
        }

        public static int GetMsb(ulong value)
        {
#if BMI
            return (int)63 - System.Numerics.BitOperations.LeadingZeroCount(value);
#else
            return (int)(Math.Log(value,2));
#endif
        }

        public static int GetAndClearLsb(ref ulong value)
        {
#if BMI
            int lsb = (int)Bmi1.X64.TrailingZeroCount(value);
#else
            int lsb = MagicTable[((ulong)((long)value & -(long)value) * Magic) >> 58];
#endif
            value ^= Types.BB_SQUARES[lsb];
            return lsb;
        }
        public static int GetAndClearMsb(ref ulong value)
        {
#if BMI
            int msb = (int)63 - System.Numerics.BitOperations.LeadingZeroCount(value);
#else
            int msb = (int)(Math.Log(value,2));
#endif
            value ^= Types.BB_SQUARES[msb];
            return msb;
        }

        public static int PopCount(ulong value)
        {
#if BMI
            return (int)Popcnt.X64.PopCount(value);
#else
            ulong result = value - ((value >> 1) & 0x5555555555555555UL);
            result = (result & 0x3333333333333333UL) + ((result >> 2) & 0x3333333333333333UL);
            return (byte)(unchecked(((result + (result >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
#endif
        }

    }
}

