using System;

namespace HockeyGame.Core
{
    /// <summary>Stable fan order for the optional draw-call grouping experiment.</summary>
    public static class CrowdSliceOrder
    {
        public static void Validate(int sectors)
        {
            if (sectors != 4 && sectors != 8 && sectors != 16 && sectors != 32)
                throw new ArgumentOutOfRangeException(nameof(sectors), "Use 4, 8, 16, or 32 sectors.");
        }
        public static int Key(int group, int tier, double x, double centredZ, int sectors)
        {
            double angle = Math.Atan2(centredZ, x) + Math.PI;
            int sector = Math.Min(sectors - 1, (int)(angle / (2 * Math.PI) * sectors));
            return (group * 2 + tier) * sectors + sector;
        }
        public static int[] Order(int[] sliceKeys, int[] originalIds)
        {
            var order = new int[sliceKeys.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            var keys = new long[sliceKeys.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = (long)sliceKeys[i] * keys.Length + originalIds[i];
            Array.Sort(keys, order);
            return order;
        }
        public static T[] Copy<T>(T[] source, int[] order)
        {
            var result = new T[order.Length];
            for (int i = 0; i < order.Length; i++) result[i] = source[order[i]];
            return result;
        }
    }
}
