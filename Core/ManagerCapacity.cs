using System;

namespace PedestrianCrossingToolkit
{
    internal static class ManagerCapacity
    {
        public const int UShortIdDomainExclusive = ushort.MaxValue + 1;

        public static int GetExclusiveUpperBound(long reportedSize, int bufferLength)
        {
            if (reportedSize <= 1 || bufferLength <= 1)
                return (int)Math.Max(0L, Math.Min(reportedSize, bufferLength));

            return (int)Math.Min(UShortIdDomainExclusive, Math.Min(reportedSize, bufferLength));
        }

        public static void EnsureArrayCapacity<T>(ref T[] buffer, int required)
        {
            if (required <= 0 || (buffer != null && buffer.Length >= required))
                return;

            int capacity = buffer == null || buffer.Length == 0 ? 4 : buffer.Length;
            while (capacity < required)
            {
                int next = capacity <= int.MaxValue / 2 ? capacity * 2 : int.MaxValue;
                if (next <= capacity)
                {
                    capacity = required;
                    break;
                }

                capacity = next;
            }

            Array.Resize(ref buffer, capacity);
        }
    }
}
