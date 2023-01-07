using System;

namespace pwiz.Skyline.Util
{
    public static class ValueChecking
    {
        public static ushort CheckUShort(int value, bool allowNegativeOne = false)
        {
            return (ushort)CheckValue(value, ushort.MinValue, ushort.MaxValue, allowNegativeOne);
        }

        public static byte CheckByte(int value, int maxValue = byte.MaxValue)
        {
            return (byte)CheckValue(value, byte.MinValue, maxValue);
        }

        private static int CheckValue(int value, int min, int max, bool allowNegativeOne = false)
        {
            if (min > value || value > max)
            {
                if (!allowNegativeOne || value != -1)
                    throw new ArgumentOutOfRangeException(string.Format(@"The value {0} must be between {1} and {2}.", value, min, max)); // CONSIDER: localize?  Does user see this?
            }
            return value;
        }
    }
}
