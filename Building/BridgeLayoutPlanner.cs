using System;
using System.Collections.Generic;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public struct BuiltBridgeDeckVisual
    {
        public readonly int AssetId;
        public readonly ushort SegmentId;
        public readonly Vector3 Start;
        public readonly Vector3 End;
        public readonly float Width;

        public BuiltBridgeDeckVisual(int assetId, ushort segmentId, Vector3 start, Vector3 end, float width)
        {
            AssetId = assetId;
            SegmentId = segmentId;
            Start = start;
            End = end;
            Width = width;
        }
    }

    public enum BridgeLayoutType
    {
        Straight,
        Triangle,
        CrossHub,
        Ring
    }

    public static class BridgeLayoutPlanner
    {
        private const float TriangleAnchorFactor = 0.42f;
        private const float CrossHubAnchorFactor = 0.48f;
        private const float RingRadiusFactor = 0.58f;
        private const float LargeJunctionRingRadius = 24f;
        private const float CrossHubRampDistance = 10f;
        private const float CrossHubDeckCornerSetback = 1f;
        private const float CrossHubRoadCornerForwardOffset = 2f;

        private struct ApproachSidewalkPair
        {
            public Vector3 First;
            public Vector3 Second;
            public Vector3 Midpoint;
        }

        public static BridgeLayoutType DetermineLayout(CrossingPlacementPlan plan, Vector3[] sidewalkAnchors)
        {
            if (plan.TargetNodeId == 0 || sidewalkAnchors == null || sidewalkAnchors.Length < 2)
                return BridgeLayoutType.Straight;

            if (sidewalkAnchors.Length == 3)
                return BridgeLayoutType.Triangle;

            if (sidewalkAnchors.Length >= 5)
                return BridgeLayoutType.Ring;

            if (sidewalkAnchors.Length == 4 && GetAverageRadius(plan.Center, sidewalkAnchors) >= LargeJunctionRingRadius)
                return BridgeLayoutType.Ring;

            return BridgeLayoutType.CrossHub;
        }

        public static bool TryBuildJunctionLayout(CrossingPlacementPlan plan, out BridgeLayoutType layout, out Vector3[] deckAnchors, out Vector3[] sidewalkAnchors)
        {
            sidewalkAnchors = GetOrderedApproachMidpoints(plan);
            layout = DetermineLayout(plan, sidewalkAnchors);
            if (sidewalkAnchors == null || sidewalkAnchors.Length == 0)
            {
                deckAnchors = new Vector3[0];
                return false;
            }

            deckAnchors = new Vector3[sidewalkAnchors.Length];
            switch (layout)
            {
                case BridgeLayoutType.Triangle:
                    for (int i = 0; i < sidewalkAnchors.Length; i++)
                        deckAnchors[i] = Vector3.Lerp(plan.Center, sidewalkAnchors[i], TriangleAnchorFactor);
                    return true;
                case BridgeLayoutType.CrossHub:
                    Vector3[] cornerDeckAnchors;
                    Vector3[] cornerSidewalkAnchorsA;
                    Vector3[] cornerSidewalkAnchorsB;
                    if (!TryBuildCrossHubCornerLayout(plan, out cornerDeckAnchors, out cornerSidewalkAnchorsA, out cornerSidewalkAnchorsB))
                        goto default;

                    deckAnchors = cornerDeckAnchors;
                    sidewalkAnchors = new Vector3[cornerDeckAnchors.Length];
                    for (int i = 0; i < cornerDeckAnchors.Length; i++)
                        sidewalkAnchors[i] = (cornerSidewalkAnchorsA[i] + cornerSidewalkAnchorsB[i]) * 0.5f;
                    return true;
                case BridgeLayoutType.Ring:
                    float ringRadius = Mathf.Max(8f, GetAverageRadius(plan.Center, sidewalkAnchors) * RingRadiusFactor);
                    for (int i = 0; i < sidewalkAnchors.Length; i++)
                    {
                        Vector3 radial = sidewalkAnchors[i] - plan.Center;
                        radial.y = 0f;
                        if (radial.sqrMagnitude <= 0.01f)
                            radial = Vector3.forward;
                        else
                            radial.Normalize();

                        deckAnchors[i] = plan.Center + radial * ringRadius;
                    }
                    return true;
                default:
                    for (int i = 0; i < sidewalkAnchors.Length; i++)
                        deckAnchors[i] = Vector3.Lerp(plan.Center, sidewalkAnchors[i], CrossHubAnchorFactor);
                    return true;
            }
        }

        public static int AddDeckVisuals(CrossingPlacementAsset asset, float width, List<BuiltBridgeDeckVisual> target)
        {
            BridgeLayoutType layout;
            Vector3[] deckAnchors;
            Vector3[] sidewalkAnchors;
            if (!TryBuildJunctionLayout(asset.Plan, out layout, out deckAnchors, out sidewalkAnchors))
                return 0;

            int added = 0;
            switch (layout)
            {
                case BridgeLayoutType.Triangle:
                case BridgeLayoutType.Ring:
                    for (int i = 0; i < deckAnchors.Length; i++)
                    {
                        int next = (i + 1) % deckAnchors.Length;
                        target.Add(new BuiltBridgeDeckVisual(asset.Id, asset.Plan.SegmentId, deckAnchors[i], deckAnchors[next], width));
                        added++;
                    }
                    break;
                case BridgeLayoutType.CrossHub:
                    for (int i = 0; i < deckAnchors.Length; i++)
                    {
                        target.Add(new BuiltBridgeDeckVisual(asset.Id, asset.Plan.SegmentId, deckAnchors[i], asset.Plan.Center, width));
                        added++;
                    }
                    break;
            }

            return added;
        }

        public static bool TryBuildCrossHubCornerLayout(CrossingPlacementPlan plan, out Vector3[] deckCorners, out Vector3[] sidewalkCornersA, out Vector3[] sidewalkCornersB)
        {
            deckCorners = new Vector3[0];
            sidewalkCornersA = new Vector3[0];
            sidewalkCornersB = new Vector3[0];

            ApproachSidewalkPair[] approachPairs = GetOrderedApproachPairs(plan);
            if (approachPairs == null || approachPairs.Length != 4)
                return false;

            int cornerCount = approachPairs.Length;
            deckCorners = new Vector3[cornerCount];
            sidewalkCornersA = new Vector3[cornerCount];
            sidewalkCornersB = new Vector3[cornerCount];

            for (int i = 0; i < cornerCount; i++)
            {
                int next = (i + 1) % cornerCount;
                ApproachSidewalkPair current = approachPairs[i];
                ApproachSidewalkPair adjacent = approachPairs[next];
                Vector3 first = GetPointClosestToTarget(current.First, current.Second, adjacent.Midpoint);
                Vector3 second = GetPointClosestToTarget(adjacent.First, adjacent.Second, current.Midpoint);
                Vector3 firstRoadCorner = GetCrossHubRoadCornerSidewalkAnchor(first, current.Midpoint, plan.Center);
                Vector3 secondRoadCorner = GetCrossHubRoadCornerSidewalkAnchor(second, adjacent.Midpoint, plan.Center);
                Vector3 roadCorner = (firstRoadCorner + secondRoadCorner) * 0.5f;
                Vector3 firstPad = GetCrossHubSidewalkRampTarget(firstRoadCorner, current.Midpoint, plan.Center);
                Vector3 secondPad = GetCrossHubSidewalkRampTarget(secondRoadCorner, adjacent.Midpoint, plan.Center);
                Vector3 deckCorner = GetCrossHubDeckCorner(plan.Center, roadCorner);

                deckCorners[i] = deckCorner;
                sidewalkCornersA[i] = firstPad;
                sidewalkCornersB[i] = secondPad;
            }

            return true;
        }

        private static Vector3 GetCrossHubDeckCorner(Vector3 center, Vector3 sidewalkCorner)
        {
            Vector3 radial = sidewalkCorner - center;
            radial.y = 0f;
            float cornerDistance = radial.magnitude;
            if (cornerDistance <= 0.01f)
                return center;

            radial /= cornerDistance;
            float deckArmLength = Mathf.Max(0f, cornerDistance - CrossHubDeckCornerSetback);
            return center + radial * deckArmLength;
        }

        private static Vector3 GetCrossHubRoadCornerSidewalkAnchor(Vector3 sidewalkAnchor, Vector3 approachMidpoint, Vector3 center)
        {
            Vector3 approachDirection = approachMidpoint - center;
            approachDirection.y = 0f;
            if (approachDirection.sqrMagnitude <= 0.01f)
                return sidewalkAnchor;

            approachDirection.Normalize();
            Vector3 fromCenter = sidewalkAnchor - center;
            fromCenter.y = 0f;
            float forwardDistance = Vector3.Dot(fromCenter, approachDirection);
            float pullBack = Mathf.Max(0f, forwardDistance - CrossHubRoadCornerForwardOffset);
            return sidewalkAnchor - approachDirection * pullBack;
        }

        private static Vector3 GetCrossHubSidewalkRampTarget(Vector3 sidewalkAnchor, Vector3 approachMidpoint, Vector3 center)
        {
            Vector3 sidewalkDirection = approachMidpoint - center;
            sidewalkDirection.y = 0f;
            if (sidewalkDirection.sqrMagnitude <= 0.01f)
                return sidewalkAnchor;

            sidewalkDirection.Normalize();
            return sidewalkAnchor + sidewalkDirection * CrossHubRampDistance;
        }

        private static Vector3[] GetOrderedApproachMidpoints(CrossingPlacementPlan plan)
        {
            int pairCount = plan.JunctionExitCount / 2;
            if (pairCount <= 0)
                return new Vector3[0];

            Vector3[] midpoints = new Vector3[pairCount];
            for (int i = 0; i < pairCount; i++)
            {
                Vector3 first = plan.JunctionExitPoints[i * 2];
                Vector3 second = plan.JunctionExitPoints[(i * 2) + 1];
                midpoints[i] = (first + second) * 0.5f;
            }

            Array.Sort(midpoints, delegate (Vector3 a, Vector3 b)
            {
                float angleA = Mathf.Atan2(a.z - plan.Center.z, a.x - plan.Center.x);
                float angleB = Mathf.Atan2(b.z - plan.Center.z, b.x - plan.Center.x);
                return angleA.CompareTo(angleB);
            });

            return midpoints;
        }

        private static ApproachSidewalkPair[] GetOrderedApproachPairs(CrossingPlacementPlan plan)
        {
            int pairCount = plan.JunctionExitCount / 2;
            if (pairCount <= 0)
                return new ApproachSidewalkPair[0];

            ApproachSidewalkPair[] pairs = new ApproachSidewalkPair[pairCount];
            for (int i = 0; i < pairCount; i++)
            {
                Vector3 first = plan.JunctionExitPoints[i * 2];
                Vector3 second = plan.JunctionExitPoints[(i * 2) + 1];
                pairs[i] = new ApproachSidewalkPair
                {
                    First = first,
                    Second = second,
                    Midpoint = (first + second) * 0.5f
                };
            }

            Array.Sort(pairs, delegate (ApproachSidewalkPair a, ApproachSidewalkPair b)
            {
                float angleA = Mathf.Atan2(a.Midpoint.z - plan.Center.z, a.Midpoint.x - plan.Center.x);
                float angleB = Mathf.Atan2(b.Midpoint.z - plan.Center.z, b.Midpoint.x - plan.Center.x);
                return angleA.CompareTo(angleB);
            });

            return pairs;
        }

        private static Vector3 GetPointClosestToTarget(Vector3 first, Vector3 second, Vector3 target)
        {
            return HorizontalDistance(first, target) <= HorizontalDistance(second, target)
                ? first
                : second;
        }

        private static float GetAverageRadius(Vector3 center, Vector3[] points)
        {
            if (points == null || points.Length == 0)
                return 0f;

            float total = 0f;
            for (int i = 0; i < points.Length; i++)
                total += HorizontalDistance(center, points[i]);

            return total / points.Length;
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            float dx = first.x - second.x;
            float dz = first.z - second.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
