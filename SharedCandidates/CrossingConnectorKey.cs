using UnityEngine;

namespace PedestrianCrossingToolkit
{
    // Candidate for a shared dependency mod; kept local while the connector crossing model stabilizes.
    public static class CrossingConnectorKey
    {
        public static string Make(Vector3 first, Vector3 second)
        {
            string firstKey = MakePointKey(first);
            string secondKey = MakePointKey(second);
            return string.CompareOrdinal(firstKey, secondKey) <= 0
                ? firstKey + "|" + secondKey
                : secondKey + "|" + firstKey;
        }

        private static string MakePointKey(Vector3 position)
        {
            return Mathf.RoundToInt(position.x * 2f)
                   + ","
                   + Mathf.RoundToInt(position.y * 2f)
                   + ","
                   + Mathf.RoundToInt(position.z * 2f);
        }
    }
}
