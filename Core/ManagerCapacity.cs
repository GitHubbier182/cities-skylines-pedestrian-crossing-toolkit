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
    }
}
