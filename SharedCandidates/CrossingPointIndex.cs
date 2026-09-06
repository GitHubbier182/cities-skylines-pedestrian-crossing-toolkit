using System.Collections.Generic;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    // A broad phase only: callers retain their exact distance and eligibility checks.
    internal sealed class CrossingPointIndex
    {
        private readonly float _cellSize;
        private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
        internal CrossingPointIndex(float cellSize) { _cellSize = cellSize; }
        internal void Add(Vector3 point, int index)
        {
            long key = Key(Mathf.FloorToInt(point.x / _cellSize), Mathf.FloorToInt(point.z / _cellSize));
            List<int> entries;
            if (!_cells.TryGetValue(key, out entries))
            {
                entries = new List<int>();
                _cells.Add(key, entries);
            }
            entries.Add(index);
        }
        internal void AppendNearby(Vector3 point, float radius, List<int> result)
        {
            int minX = Mathf.FloorToInt((point.x - radius) / _cellSize);
            int maxX = Mathf.FloorToInt((point.x + radius) / _cellSize);
            int minZ = Mathf.FloorToInt((point.z - radius) / _cellSize);
            int maxZ = Mathf.FloorToInt((point.z + radius) / _cellSize);
            for (long z = minZ; z <= maxZ; z++)
                for (long x = minX; x <= maxX; x++)
                {
                    List<int> entries;
                    if (_cells.TryGetValue(Key((int)x, (int)z), out entries))
                        result.AddRange(entries);
                }
        }
        private static long Key(int x, int z) { return ((long)x << 32) | (uint)z; }
    }
}
