using System.Collections.Generic;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public static partial class CrossingPathBuilder
    {
        private enum SurfaceVisualSpanKind
        {
            RoadOrBus,
            Concrete
        }

        private struct SurfaceVisualSpan
        {
            public readonly SurfaceMarkingSpan Span;
            public readonly SurfaceVisualSpanKind Kind;

            public SurfaceVisualSpan(SurfaceMarkingSpan span, SurfaceVisualSpanKind kind)
            {
                Span = span;
                Kind = kind;
            }
        }

        private struct SurfaceVisualPlan
        {
            public readonly SurfaceSidewalkEdges SidewalkEdges;
            public readonly SurfaceMarkingSpan Bounds;
            public readonly List<SurfaceVisualSpan> Spans;
            public readonly List<SurfaceMarkingSpan> RoadSpans;
            public readonly List<SurfaceMarkingSpan> ConcreteSpans;

            public SurfaceVisualPlan(SurfaceSidewalkEdges sidewalkEdges, SurfaceMarkingSpan bounds, List<SurfaceVisualSpan> spans, List<SurfaceMarkingSpan> roadSpans, List<SurfaceMarkingSpan> concreteSpans)
            {
                SidewalkEdges = sidewalkEdges;
                Bounds = bounds;
                Spans = spans;
                RoadSpans = roadSpans;
                ConcreteSpans = concreteSpans;
            }
        }

        private static void AddSurfaceCrossingVisuals(CrossingPathWorkOrder order)
        {
            if (order.Kind != CrossingPathWorkOrderKind.SurfacePath
                && order.Kind != CrossingPathWorkOrderKind.SignalizedSurfacePath)
            {
                return;
            }

            string key = "surface:" + order.AssetId + ":" + order.Kind;
            if (BuiltSurfaceVisualKeys.Contains(key))
                return;

            CrossingPlacementAsset asset;
            if (!CrossingPlacementRegistry.TryGetAssetById(order.AssetId, out asset) || !asset.Plan.IsValid)
                return;

            SurfaceCrossingFrame frame;
            if (!TryCreateSurfaceCrossingFrame(order, out frame))
                return;

            Vector3 across;
            Vector3 roadDirection;
            ResolveSurfaceVisualAxes(asset, frame, out across, out roadDirection);
            Vector3 midpoint = frame.Center;
            midpoint.y = asset.Plan.Center.y + SurfaceMarkingLift;
            SurfaceVisualPlan visualPlan = BuildSurfaceVisualPlan(asset, across);
            bool isSignalCrossing = order.Kind == CrossingPathWorkOrderKind.SignalizedSurfacePath;

            for (int i = 0; i < visualPlan.Spans.Count; i++)
            {
                if (isSignalCrossing)
                    continue;

                SurfaceVisualSpan span = visualPlan.Spans[i];
                if (span.Kind == SurfaceVisualSpanKind.RoadOrBus)
                {
                    AddSurfaceStripeRun(order.AssetId, asset.Placement.SegmentId, midpoint, roadDirection, across, Vector3.zero, span.Span.Min, span.Span.Max);
                }
                else
                {
                    AddVergeCrossingConnector(order.AssetId, asset.Placement.SegmentId, midpoint, roadDirection, across, Vector3.zero, span.Span.Min, span.Span.Max);
                }
            }

            if (!isSignalCrossing)
                AddPedestrianPillarPairs(order.AssetId, midpoint, roadDirection, across, visualPlan.Bounds.Min, visualPlan.Bounds.Max);

            if (isSignalCrossing)
            {
                AddSignalVisuals(order.AssetId, asset, midpoint, roadDirection, across, visualPlan.RoadSpans);
            }

            if (ShouldLogSurfaceVisualDiagnostics(asset, order.Kind))
            {
                LogSurfaceVisualDiagnostics(
                    order,
                    asset,
                    frame,
                    across,
                    roadDirection,
                    midpoint,
                    visualPlan);
            }

            BuiltSurfaceVisualKeys.Add(key);
        }

        private static bool ShouldLogSurfaceVisualDiagnostics(CrossingPlacementAsset asset, CrossingPathWorkOrderKind kind)
        {
            if (kind != CrossingPathWorkOrderKind.SurfacePath
                && kind != CrossingPathWorkOrderKind.SignalizedSurfacePath)
            {
                return false;
            }

            return PedestrianCrossingLog.VerboseDiagnostics;
        }

        private static void LogSurfaceVisualDiagnostics(
            CrossingPathWorkOrder order,
            CrossingPlacementAsset asset,
            SurfaceCrossingFrame frame,
            Vector3 across,
            Vector3 roadDirection,
            Vector3 midpoint,
            SurfaceVisualPlan visualPlan)
        {
            string prefix = "[PedestrianCrossingToolkit][SurfaceVisualDiag] asset="
                            + order.AssetId
                            + " segment="
                            + asset.Placement.SegmentId
                            + " kind="
                            + order.Kind
                            + " ";

            PedestrianCrossingLog.Info(prefix
                      + "placement pos="
                      + asset.Placement.SegmentPosition.ToString("0.000")
                      + " nearNode="
                      + asset.Placement.NearNode
                      + " targetNode="
                      + asset.Plan.TargetNodeId
                      + " slot="
                      + asset.Placement.SlotNumber
                      + " endSlot="
                      + asset.Placement.IsEndSegmentSlot
                      + " planWidth="
                      + asset.Plan.Width.ToString("0.000")
                      + " midpoint="
                      + FormatVector(midpoint));

            PedestrianCrossingLog.Info(prefix
                      + "axes planRoad="
                      + FormatVector(asset.Plan.RoadDirection)
                      + " planCross="
                      + FormatVector(asset.Plan.CrossingDirection)
                      + " frameAcross="
                      + FormatVector(frame.Across)
                      + " frameRoad="
                      + FormatVector(frame.RoadDirection)
                      + " resolvedAcross="
                      + FormatVector(across)
                      + " resolvedRoad="
                      + FormatVector(roadDirection));

            PedestrianCrossingLog.Info(prefix
                      + "points first="
                      + FormatVector(frame.First)
                      + " second="
                      + FormatVector(frame.Second)
                      + " center="
                      + FormatVector(frame.Center)
                      + " pathOffset="
                      + FormatVector(frame.PathBuildOffset));

            PedestrianCrossingLog.Info(prefix
                      + "bounds sidewalkNeg="
                      + FormatSidewalkEdge(visualPlan.SidewalkEdges.HasNegativeEdge, visualPlan.SidewalkEdges.NegativeEdge)
                      + " sidewalkPos="
                      + FormatSidewalkEdge(visualPlan.SidewalkEdges.HasPositiveEdge, visualPlan.SidewalkEdges.PositiveEdge)
                      + " visualBounds="
                      + FormatSpan(visualPlan.Bounds)
                      + " vanillaSignalNodeCrossing="
                      + (order.Kind == CrossingPathWorkOrderKind.SignalizedSurfacePath && ShouldUseVanillaSignalNodeCrossing(asset))
                      + " roadSpans="
                      + FormatSpanList(visualPlan.RoadSpans)
                      + " concreteSpans="
                      + FormatSpanList(visualPlan.ConcreteSpans)
                      + " visualSpans="
                      + FormatVisualSpanList(visualPlan.Spans));

            PedestrianCrossingLog.Info(prefix
                      + "rules surfaceWidth="
                      + SurfaceCrossingVisualWidth.ToString("0.000")
                      + " zebraWhiteDepth="
                      + SurfaceStripeDepth.ToString("0.000")
                      + " zebraBlankDepth="
                      + SurfaceStripeGapDepth.ToString("0.000")
                      + " concreteWidth="
                      + VergeCrossingConnectorWidth.ToString("0.000")
                      + " concreteThickness="
                      + VergeCrossingConnectorThickness.ToString("0.000")
                      + " concreteLift="
                      + VergeCrossingConnectorLift.ToString("0.000")
                      + " stopGap="
                      + SignalStopLineOffsetFromCrossing.ToString("0.000")
                      + " stopLineWidth="
                      + SignalStopLineWidth.ToString("0.000")
                      + " signalUsesVanillaCrossingVisual="
                      + (order.Kind == CrossingPathWorkOrderKind.SignalizedSurfacePath && ShouldUseVanillaSignalNodeCrossing(asset)));

            LogSurfaceVisualSegmentDiagnostics(prefix, asset, across, visualPlan);
            if (order.Kind == CrossingPathWorkOrderKind.SignalizedSurfacePath)
                LogSignalVisualSpanDiagnostics(prefix, asset, across, roadDirection, visualPlan.RoadSpans, midpoint);
        }

        private static void LogSurfaceVisualSegmentDiagnostics(string prefix, CrossingPlacementAsset asset, Vector3 across, SurfaceVisualPlan visualPlan)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null
                || asset.Placement.SegmentId == 0
                || asset.Placement.SegmentId >= netManager.m_segments.m_size)
            {
                PedestrianCrossingLog.Info(prefix + "segment unavailable");
                return;
            }

            ref NetSegment segment = ref netManager.m_segments.m_buffer[asset.Placement.SegmentId];
            NetInfo info = segment.Info;
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || info == null)
            {
                PedestrianCrossingLog.Info(prefix + "segment not created or missing info flags=" + segment.m_flags);
                return;
            }

            bool inverted = (segment.m_flags & NetSegment.Flags.Invert) != 0;
            int laneCount = info.m_lanes == null ? 0 : info.m_lanes.Length;
            PedestrianCrossingLog.Info(prefix
                      + "segment flags="
                      + segment.m_flags
                      + " invert="
                      + inverted
                      + " startNode="
                      + segment.m_startNode
                      + " endNode="
                      + segment.m_endNode
                      + " prefab="
                      + info.name
                      + " halfWidth="
                      + info.m_halfWidth.ToString("0.000")
                      + " pavementWidth="
                      + info.m_pavementWidth.ToString("0.000")
                      + " lanes="
                      + laneCount);

            if (info.m_lanes == null)
                return;

            float sideSign = Vector3.Dot(asset.Plan.CrossingDirection, across) >= 0f ? 1f : -1f;
            uint laneId = segment.m_lanes;
            for (int i = 0; i < info.m_lanes.Length; i++)
            {
                NetInfo.Lane lane = info.m_lanes[i];
                uint currentLaneId = laneId;
                if (currentLaneId != 0 && currentLaneId < netManager.m_lanes.m_size)
                    laneId = netManager.m_lanes.m_buffer[currentLaneId].m_nextLane;
                else
                    laneId = 0;

                if (lane == null)
                {
                    PedestrianCrossingLog.Info(prefix + "lane[" + i + "] null laneId=" + currentLaneId);
                    continue;
                }

                NetInfo.Direction effectiveDirection = lane.m_finalDirection;
                if (inverted)
                    effectiveDirection = NetInfo.InvertDirection(effectiveDirection);

                Vector3 dirAtStart;
                Vector3 dirAtEnd;
                TryGetDiagnosticLaneDirection(netManager, currentLaneId, 0.05f, out dirAtStart);
                TryGetDiagnosticLaneDirection(netManager, currentLaneId, 0.95f, out dirAtEnd);

                float lanePosition = lane.m_position * sideSign;
                float laneHalfWidth = Mathf.Max(0.25f, lane.m_width * 0.5f) + SurfaceVehicleLaneEdgeMargin;
                SurfaceMarkingSpan laneSpan = new SurfaceMarkingSpan(lanePosition - laneHalfWidth, lanePosition + laneHalfWidth);
                bool roadOrBus = IsRoadOrBusSurfaceLane(lane);
                bool concrete = IsConcreteSurfaceLane(lane);
                bool inBounds = laneSpan.Max > visualPlan.Bounds.Min && laneSpan.Min < visualPlan.Bounds.Max;

                PedestrianCrossingLog.Info(prefix
                          + "lane["
                          + i
                          + "] id="
                          + currentLaneId
                          + " type="
                          + lane.m_laneType
                          + " vehicle="
                          + IsVehicleSurfaceLane(lane)
                          + " roadOrBus="
                          + roadOrBus
                          + " concrete="
                          + concrete
                          + " pedestrian="
                          + ((lane.m_laneType & NetInfo.LaneType.Pedestrian) != 0)
                          + " pos="
                          + lane.m_position.ToString("0.000")
                          + " posAcross="
                          + lanePosition.ToString("0.000")
                          + " width="
                          + lane.m_width.ToString("0.000")
                          + " visualSpan="
                          + FormatSpan(laneSpan)
                          + " inVisualBounds="
                          + inBounds
                          + " dirRaw="
                          + lane.m_direction
                          + " dirFinal="
                          + lane.m_finalDirection
                          + " dirEffective="
                          + effectiveDirection
                          + " stopOffset="
                          + lane.m_stopOffset.ToString("0.000")
                          + " bezierDir05="
                          + FormatVector(dirAtStart)
                          + " bezierDir95="
                          + FormatVector(dirAtEnd));
            }
        }

        private static void LogSignalVisualSpanDiagnostics(string prefix, CrossingPlacementAsset asset, Vector3 across, Vector3 roadDirection, List<SurfaceMarkingSpan> roadSpans, Vector3 midpoint)
        {
            bool useVanillaCrossingVisual = ShouldUseVanillaSignalNodeCrossing(asset);
            SurfaceMarkingSpan fullRoadSpan;
            bool hasSpan = TryGetSignalStopLineRoadSpan(roadSpans, out fullRoadSpan);
            PedestrianCrossingLog.Info(prefix
                      + "signalStopLines ok="
                      + hasSpan
                      + " mode=symmetric-full-road"
                      + " stopDistance="
                      + GetSignalStopLineRoadOffsetDistance(useVanillaCrossingVisual).ToString("0.000")
                      + " fullRoadSpan="
                      + (hasSpan ? FormatSpan(fullRoadSpan) : "none")
                      + " roadSpans="
                      + FormatSpanList(roadSpans)
                      + " stopLineSide=both-sides-of-zebra");
        }

        private static bool TryGetDiagnosticLaneDirection(NetManager netManager, uint laneId, float offset, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (netManager == null || laneId == 0 || laneId >= netManager.m_lanes.m_size)
                return false;

            direction = netManager.m_lanes.m_buffer[laneId].CalculateDirection(offset);
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return false;

            direction.Normalize();
            return true;
        }

        private static string FormatSidewalkEdge(bool hasEdge, float edge)
        {
            return hasEdge ? edge.ToString("0.000") : "none";
        }

        private static string FormatSpan(SurfaceMarkingSpan span)
        {
            return "["
                   + span.Min.ToString("0.000")
                   + ","
                   + span.Max.ToString("0.000")
                   + " w="
                   + (span.Max - span.Min).ToString("0.000")
                   + "]";
        }

        private static string FormatSpanList(List<SurfaceMarkingSpan> spans)
        {
            if (spans == null || spans.Count == 0)
                return "[]";

            string result = "[";
            for (int i = 0; i < spans.Count; i++)
            {
                if (i > 0)
                    result += ",";

                result += FormatSpan(spans[i]);
            }

            return result + "]";
        }

        private static string FormatVisualSpanList(List<SurfaceVisualSpan> spans)
        {
            if (spans == null || spans.Count == 0)
                return "[]";

            string result = "[";
            for (int i = 0; i < spans.Count; i++)
            {
                if (i > 0)
                    result += ",";

                result += spans[i].Kind + ":" + FormatSpan(spans[i].Span);
            }

            return result + "]";
        }

        private static string FormatVector(Vector3 vector)
        {
            return "("
                   + vector.x.ToString("0.000")
                   + ","
                   + vector.y.ToString("0.000")
                   + ","
                   + vector.z.ToString("0.000")
                   + ")";
        }

        private static void ResolveSurfaceVisualAxes(CrossingPlacementAsset asset, SurfaceCrossingFrame frame, out Vector3 across, out Vector3 roadDirection)
        {
            across = asset.Plan.CrossingDirection;
            roadDirection = asset.Plan.RoadDirection;
            across.y = 0f;
            roadDirection.y = 0f;

            if (across.sqrMagnitude <= 0.01f)
                across = frame.Across;
            else
                across.Normalize();

            if (roadDirection.sqrMagnitude <= 0.01f)
                roadDirection = frame.RoadDirection;
            else
                roadDirection.Normalize();

            if (Vector3.Dot(across, frame.Across) < 0f)
                across = -across;

            if (Mathf.Abs(Vector3.Dot(roadDirection, across)) > 0.05f)
            {
                Vector3 correctedRoadDirection = new Vector3(-across.z, 0f, across.x);
                if (Vector3.Dot(correctedRoadDirection, roadDirection) < 0f)
                    correctedRoadDirection = -correctedRoadDirection;

                roadDirection = correctedRoadDirection;
            }

            roadDirection.Normalize();
        }

        private static SurfaceVisualPlan BuildSurfaceVisualPlan(CrossingPlacementAsset asset, Vector3 across)
        {
            SurfaceSidewalkEdges sidewalkEdges = GetSurfaceSidewalkEdges(asset, across);
            SurfaceMarkingSpan bounds = GetSurfaceVisualBounds(asset, sidewalkEdges);
            List<SurfaceMarkingSpan> roadSpans = GetRoadSurfaceSpans(asset, across, bounds);
            List<SurfaceMarkingSpan> concreteSpans = GetConcreteSurfaceSpans(asset, across, bounds, roadSpans);
            List<SurfaceVisualSpan> spans = BuildSurfaceVisualSpans(bounds, roadSpans, concreteSpans);

            return new SurfaceVisualPlan(sidewalkEdges, bounds, spans, roadSpans, concreteSpans);
        }

        private static List<SurfaceVisualSpan> BuildSurfaceVisualSpans(SurfaceMarkingSpan bounds, List<SurfaceMarkingSpan> roadSpans, List<SurfaceMarkingSpan> concreteSpans)
        {
            List<SurfaceVisualSpan> spans = new List<SurfaceVisualSpan>();
            for (int i = 0; i < roadSpans.Count; i++)
                spans.Add(new SurfaceVisualSpan(roadSpans[i], SurfaceVisualSpanKind.RoadOrBus));

            for (int i = 0; i < concreteSpans.Count; i++)
                spans.Add(new SurfaceVisualSpan(concreteSpans[i], SurfaceVisualSpanKind.Concrete));

            if (spans.Count == 0 && bounds.Max > bounds.Min + VergeCrossingConnectorMinLength)
                spans.Add(new SurfaceVisualSpan(bounds, SurfaceVisualSpanKind.Concrete));

            SortSurfaceVisualSpans(spans);
            return spans;
        }

        private static List<SurfaceMarkingSpan> GetRoadSurfaceSpans(CrossingPlacementAsset asset, Vector3 across, SurfaceMarkingSpan bounds)
        {
            List<SurfaceMarkingSpan> spans = new List<SurfaceMarkingSpan>();
            NetManager netManager = NetManager.instance;
            if (asset.Placement.SegmentId == 0 || asset.Placement.SegmentId >= netManager.m_segments.m_size)
                return spans;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[asset.Placement.SegmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null || segment.Info.m_lanes == null)
                return spans;

            float sideSign = Vector3.Dot(asset.Plan.CrossingDirection, across) >= 0f ? 1f : -1f;
            for (int i = 0; i < segment.Info.m_lanes.Length; i++)
            {
                NetInfo.Lane lane = segment.Info.m_lanes[i];
                if (lane == null || !IsRoadOrBusSurfaceLane(lane))
                    continue;

                float lanePosition = lane.m_position * sideSign;
                float halfWidth = Mathf.Max(0.25f, lane.m_width * 0.5f) + SurfaceVehicleLaneEdgeMargin;
                AddMergedSurfaceSpan(spans, lanePosition - halfWidth, lanePosition + halfWidth);
            }

            ClampSurfaceSpansToBounds(spans, bounds);
            SortAndMergeSurfaceSpans(spans, SurfaceVehicleSpanMergeGap);
            return spans;
        }

        private static SurfaceMarkingSpan GetSurfaceVisualBounds(CrossingPlacementAsset asset, SurfaceSidewalkEdges sidewalkEdges)
        {
            bool hasBothSidewalks = sidewalkEdges.HasNegativeEdge
                                    && sidewalkEdges.HasPositiveEdge
                                    && sidewalkEdges.PositiveEdge > sidewalkEdges.NegativeEdge + 0.5f;
            if (hasBothSidewalks)
                return new SurfaceMarkingSpan(sidewalkEdges.NegativeEdge, sidewalkEdges.PositiveEdge);

            float halfWidth = Mathf.Clamp(Mathf.Max(2f, asset.Plan.Width * 0.5f), 2f, 40f);
            float min = -halfWidth;
            float max = halfWidth;
            if (sidewalkEdges.HasNegativeEdge)
                min = Mathf.Max(min, sidewalkEdges.NegativeEdge);

            if (sidewalkEdges.HasPositiveEdge)
                max = Mathf.Min(max, sidewalkEdges.PositiveEdge);

            if (max <= min + 0.1f)
                return new SurfaceMarkingSpan(-halfWidth, halfWidth);

            return new SurfaceMarkingSpan(min, max);
        }

        private static void ClampSurfaceSpansToBounds(List<SurfaceMarkingSpan> spans, SurfaceMarkingSpan bounds)
        {
            for (int i = spans.Count - 1; i >= 0; i--)
            {
                SurfaceMarkingSpan span = spans[i];
                span.Min = Mathf.Clamp(span.Min, bounds.Min, bounds.Max);
                span.Max = Mathf.Clamp(span.Max, bounds.Min, bounds.Max);
                if (span.Max <= span.Min + 0.1f)
                {
                    spans.RemoveAt(i);
                    continue;
                }

                spans[i] = span;
            }
        }

        private static void AddSurfaceStripeRun(int assetId, ushort segmentId, Vector3 midpoint, Vector3 roadDirection, Vector3 across, Vector3 roadOffset, float min, float max)
        {
            float markingSpan = max - min;
            if (markingSpan <= 0.01f)
                return;

            float pitch = SurfaceStripeDepth + SurfaceStripeGapDepth;
            int stripeCount = Mathf.FloorToInt((markingSpan + SurfaceStripeGapDepth) / pitch);
            if (stripeCount <= 0)
                return;

            float usedSpan = (stripeCount * SurfaceStripeDepth) + ((stripeCount - 1) * SurfaceStripeGapDepth);
            float cursor = (min + max) * 0.5f - (usedSpan * 0.5f);
            for (int i = 0; i < stripeCount; i++)
            {
                float stripeStart = cursor;
                float stripeEnd = stripeStart + SurfaceStripeDepth;
                AddSurfaceQuadOnContour(
                    "PCT Surface Crossing stripe #" + assetId,
                    segmentId,
                    midpoint + roadOffset,
                    roadDirection,
                    across,
                    stripeStart,
                    stripeEnd,
                    SurfaceStripeLength,
                    GetCrossingStripeMaterial());
                cursor += pitch;
            }
        }

        private static void AddPedestrianPillarPairs(int assetId, Vector3 midpoint, Vector3 roadDirection, Vector3 across, float visualMin, float visualMax)
        {
            AddPedestrianPillarPair(assetId, midpoint, roadDirection, across, visualMin);
            AddPedestrianPillarPair(assetId, midpoint, roadDirection, across, visualMax);
        }

        private static void AddPedestrianPillarPair(int assetId, Vector3 midpoint, Vector3 roadDirection, Vector3 across, float acrossPosition)
        {
            Vector3 direction = roadDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return;

            direction.Normalize();
            float halfWidth = SurfaceCrossingVisualWidth * 0.5f;
            Vector3 edgeCenter = midpoint + across * acrossPosition;
            AddPlainPedestrianPillar(assetId, edgeCenter + direction * halfWidth);
            AddPlainPedestrianPillar(assetId, edgeCenter - direction * halfWidth);
        }

        private static void AddPlainPedestrianPillar(int assetId, Vector3 basePosition)
        {
            AddTrackedCube(
                "PCT Pedestrian crossing pillar #" + assetId,
                basePosition + Vector3.up * (PedestrianPillarHeight * 0.5f),
                Quaternion.identity,
                new Vector3(PedestrianPillarWidth, PedestrianPillarHeight, PedestrianPillarWidth),
                GetSignalPoleMaterial());
        }

        private static void AddVergeCrossingConnector(int assetId, ushort segmentId, Vector3 midpoint, Vector3 roadDirection, Vector3 across, Vector3 roadOffset, float firstEdge, float secondEdge)
        {
            float connectorLength = Mathf.Abs(secondEdge - firstEdge);
            if (connectorLength < VergeCrossingConnectorMinLength)
                return;

            AddSurfacePrismOnContour(
                "PCT Verge Crossing connector #" + assetId,
                segmentId,
                midpoint + roadOffset,
                roadDirection,
                across,
                firstEdge,
                secondEdge,
                VergeCrossingConnectorWidth,
                VergeCrossingConnectorThickness,
                GetVergeCrossingMaterial());
        }

        private static void AddSurfaceQuadOnContour(string name, ushort segmentId, Vector3 origin, Vector3 roadDirection, Vector3 across, float firstEdge, float secondEdge, float width, Material material)
        {
            Vector3 road = NormalizeOrFallback(roadDirection, new Vector3(-across.z, 0f, across.x));
            Vector3 cross = NormalizeOrFallback(across, new Vector3(-road.z, 0f, road.x));
            float halfWidth = Mathf.Max(0.01f, width * 0.5f);
            float min = Mathf.Min(firstEdge, secondEdge);
            float max = Mathf.Max(firstEdge, secondEdge);

            Vector3 firstLeft = origin + cross * min - road * halfWidth;
            Vector3 firstRight = origin + cross * min + road * halfWidth;
            Vector3 secondRight = origin + cross * max + road * halfWidth;
            Vector3 secondLeft = origin + cross * max - road * halfWidth;

            firstLeft.y = SampleSurfaceVisualHeight(segmentId, firstLeft, origin.y - SurfaceMarkingLift);
            firstRight.y = SampleSurfaceVisualHeight(segmentId, firstRight, origin.y - SurfaceMarkingLift);
            secondRight.y = SampleSurfaceVisualHeight(segmentId, secondRight, origin.y - SurfaceMarkingLift);
            secondLeft.y = SampleSurfaceVisualHeight(segmentId, secondLeft, origin.y - SurfaceMarkingLift);

            AddTrackedWorldQuad(name, firstLeft, firstRight, secondRight, secondLeft, material);
        }

        private static void AddSurfacePrismOnContour(string name, ushort segmentId, Vector3 origin, Vector3 roadDirection, Vector3 across, float firstEdge, float secondEdge, float width, float thickness, Material material)
        {
            Vector3 road = NormalizeOrFallback(roadDirection, new Vector3(-across.z, 0f, across.x));
            Vector3 cross = NormalizeOrFallback(across, new Vector3(-road.z, 0f, road.x));
            float halfWidth = Mathf.Max(0.01f, width * 0.5f);
            float min = Mathf.Min(firstEdge, secondEdge);
            float max = Mathf.Max(firstEdge, secondEdge);

            Vector3 firstLeftTop = origin + cross * min - road * halfWidth;
            Vector3 firstRightTop = origin + cross * min + road * halfWidth;
            Vector3 secondRightTop = origin + cross * max + road * halfWidth;
            Vector3 secondLeftTop = origin + cross * max - road * halfWidth;

            firstLeftTop.y = SampleSurfaceVisualHeight(segmentId, firstLeftTop, origin.y - SurfaceMarkingLift);
            firstRightTop.y = SampleSurfaceVisualHeight(segmentId, firstRightTop, origin.y - SurfaceMarkingLift);
            secondRightTop.y = SampleSurfaceVisualHeight(segmentId, secondRightTop, origin.y - SurfaceMarkingLift);
            secondLeftTop.y = SampleSurfaceVisualHeight(segmentId, secondLeftTop, origin.y - SurfaceMarkingLift);

            float slabThickness = Mathf.Max(0.005f, thickness);
            Vector3 firstLeftBottom = firstLeftTop - Vector3.up * slabThickness;
            Vector3 firstRightBottom = firstRightTop - Vector3.up * slabThickness;
            Vector3 secondRightBottom = secondRightTop - Vector3.up * slabThickness;
            Vector3 secondLeftBottom = secondLeftTop - Vector3.up * slabThickness;

            AddTrackedWorldPrism(
                name,
                firstLeftTop,
                firstRightTop,
                secondRightTop,
                secondLeftTop,
                firstLeftBottom,
                firstRightBottom,
                secondRightBottom,
                secondLeftBottom,
                material);
        }

        private static float SampleSurfaceVisualHeight(ushort segmentId, Vector3 position, float fallbackHeight)
        {
            NetManager netManager = NetManager.instance;
            if (netManager != null && segmentId != 0 && segmentId < netManager.m_segments.m_size)
            {
                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) != 0)
                    return segment.GetClosestPosition(position).y + SurfaceMarkingLift;
            }

            TerrainManager terrainManager = TerrainManager.instance;
            if (terrainManager != null)
                return terrainManager.SampleRawHeightSmooth(position) + SurfaceMarkingLift;

            return fallbackHeight + SurfaceMarkingLift;
        }

        private static Vector3 NormalizeOrFallback(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                direction = fallback;

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return Vector3.forward;

            return direction.normalized;
        }

        private static GameObject AddTrackedWorldQuad(string name, Vector3 firstLeft, Vector3 firstRight, Vector3 secondRight, Vector3 secondLeft, Material material)
        {
            Vector3 center = (firstLeft + firstRight + secondRight + secondLeft) * 0.25f;
            GameObject obj = new GameObject(name);
            obj.transform.position = center;

            Mesh mesh = new Mesh();
            mesh.vertices = new[]
            {
                firstLeft - center,
                firstRight - center,
                secondRight - center,
                secondLeft - center
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.mesh = mesh;
            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            renderer.material = material ?? GetBridgeConcreteMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            BuiltBridgeConcreteObjects.Add(obj);
            return obj;
        }

        private static GameObject AddTrackedWorldPrism(
            string name,
            Vector3 firstLeftTop,
            Vector3 firstRightTop,
            Vector3 secondRightTop,
            Vector3 secondLeftTop,
            Vector3 firstLeftBottom,
            Vector3 firstRightBottom,
            Vector3 secondRightBottom,
            Vector3 secondLeftBottom,
            Material material)
        {
            Vector3 center = (firstLeftTop + firstRightTop + secondRightTop + secondLeftTop + firstLeftBottom + firstRightBottom + secondRightBottom + secondLeftBottom) * 0.125f;
            GameObject obj = new GameObject(name);
            obj.transform.position = center;

            Mesh mesh = new Mesh();
            mesh.vertices = new[]
            {
                firstLeftTop - center,
                firstRightTop - center,
                secondRightTop - center,
                secondLeftTop - center,
                firstLeftBottom - center,
                firstRightBottom - center,
                secondRightBottom - center,
                secondLeftBottom - center
            };
            mesh.triangles = new[]
            {
                0, 3, 2, 0, 2, 1,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                1, 2, 6, 1, 6, 5,
                2, 3, 7, 2, 7, 6,
                3, 0, 4, 3, 4, 7
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.mesh = mesh;
            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            renderer.material = material ?? GetBridgeConcreteMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            BuiltBridgeConcreteObjects.Add(obj);
            return obj;
        }

        private static List<SurfaceMarkingSpan> GetConcreteSurfaceSpans(CrossingPlacementAsset asset, Vector3 across, SurfaceMarkingSpan bounds, List<SurfaceMarkingSpan> roadSpans)
        {
            List<SurfaceMarkingSpan> spans = new List<SurfaceMarkingSpan>();
            NetManager netManager = NetManager.instance;
            if (asset.Placement.SegmentId == 0 || asset.Placement.SegmentId >= netManager.m_segments.m_size)
            {
                AddUnclassifiedConcreteSpans(spans, roadSpans, bounds);
                return spans;
            }

            ref NetSegment segment = ref netManager.m_segments.m_buffer[asset.Placement.SegmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null || segment.Info.m_lanes == null)
            {
                AddUnclassifiedConcreteSpans(spans, roadSpans, bounds);
                return spans;
            }

            float sideSign = Vector3.Dot(asset.Plan.CrossingDirection, across) >= 0f ? 1f : -1f;
            for (int i = 0; i < segment.Info.m_lanes.Length; i++)
            {
                NetInfo.Lane lane = segment.Info.m_lanes[i];
                if (lane == null || !IsConcreteSurfaceLane(lane))
                    continue;

                float lanePosition = lane.m_position * sideSign;
                float halfWidth = Mathf.Max(0.25f, lane.m_width * 0.5f) + SurfaceVehicleLaneEdgeMargin;
                AddMergedSurfaceSpan(spans, lanePosition - halfWidth, lanePosition + halfWidth);
            }

            AddUnclassifiedConcreteSpans(spans, roadSpans, bounds);
            ClampSurfaceSpansToBounds(spans, bounds);
            SortAndMergeSurfaceSpans(spans, SurfaceVehicleSpanMergeGap);
            return spans;
        }

        private static void AddUnclassifiedConcreteSpans(List<SurfaceMarkingSpan> spans, List<SurfaceMarkingSpan> roadSpans, SurfaceMarkingSpan bounds)
        {
            float cursor = bounds.Min;
            if (roadSpans != null)
            {
                for (int i = 0; i < roadSpans.Count; i++)
                {
                    SurfaceMarkingSpan roadSpan = roadSpans[i];
                    if (roadSpan.Min > cursor + VergeCrossingConnectorMinLength)
                        AddMergedSurfaceSpan(spans, cursor, roadSpan.Min);

                    cursor = Mathf.Max(cursor, roadSpan.Max);
                }
            }

            if (bounds.Max > cursor + VergeCrossingConnectorMinLength)
                AddMergedSurfaceSpan(spans, cursor, bounds.Max);
        }

        private static SurfaceSidewalkEdges GetSurfaceSidewalkEdges(CrossingPlacementAsset asset, Vector3 across)
        {
            NetManager netManager = NetManager.instance;
            if (asset.Placement.SegmentId == 0 || asset.Placement.SegmentId >= netManager.m_segments.m_size)
                return new SurfaceSidewalkEdges(false, 0f, false, 0f);

            ref NetSegment segment = ref netManager.m_segments.m_buffer[asset.Placement.SegmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null || segment.Info.m_lanes == null)
                return new SurfaceSidewalkEdges(false, 0f, false, 0f);

            bool hasNegativeEdge = false;
            bool hasPositiveEdge = false;
            float negativeEdge = 0f;
            float positiveEdge = 0f;
            float sideSign = Vector3.Dot(asset.Plan.CrossingDirection, across) >= 0f ? 1f : -1f;
            for (int i = 0; i < segment.Info.m_lanes.Length; i++)
            {
                NetInfo.Lane lane = segment.Info.m_lanes[i];
                if (lane == null || (lane.m_laneType & NetInfo.LaneType.Pedestrian) == 0)
                    continue;

                float lanePosition = lane.m_position * sideSign;
                float halfWidth = Mathf.Max(0.25f, lane.m_width * 0.5f);
                if (lanePosition < 0f)
                {
                    float innerEdge = lanePosition + halfWidth;
                    if (!hasNegativeEdge || innerEdge > negativeEdge)
                    {
                        negativeEdge = innerEdge;
                        hasNegativeEdge = true;
                    }
                }
                else
                {
                    float innerEdge = lanePosition - halfWidth;
                    if (!hasPositiveEdge || innerEdge < positiveEdge)
                    {
                        positiveEdge = innerEdge;
                        hasPositiveEdge = true;
                    }
                }
            }

            return new SurfaceSidewalkEdges(hasNegativeEdge, negativeEdge, hasPositiveEdge, positiveEdge);
        }

        private static bool IsVehicleSurfaceLane(NetInfo.Lane lane)
        {
            NetInfo.LaneType vehicleTypes = NetInfo.LaneType.Vehicle
                                            | NetInfo.LaneType.TransportVehicle
                                            | NetInfo.LaneType.CargoVehicle
                                            | NetInfo.LaneType.PublicTransport;
            return (lane.m_laneType & vehicleTypes) != 0;
        }

        private static bool IsRoadOrBusSurfaceLane(NetInfo.Lane lane)
        {
            return IsVehicleSurfaceLane(lane)
                   || (lane.m_laneType & NetInfo.LaneType.Parking) != 0;
        }

        private static bool IsConcreteSurfaceLane(NetInfo.Lane lane)
        {
            if ((lane.m_laneType & NetInfo.LaneType.Pedestrian) != 0)
                return false;

            return !IsRoadOrBusSurfaceLane(lane)
                   && lane.m_width > VergeCrossingConnectorMinLength;
        }

        private static void SortSurfaceVisualSpans(List<SurfaceVisualSpan> spans)
        {
            spans.Sort(delegate (SurfaceVisualSpan a, SurfaceVisualSpan b)
            {
                return a.Span.Min.CompareTo(b.Span.Min);
            });
        }

        private static void AddMergedSurfaceSpan(List<SurfaceMarkingSpan> spans, float min, float max)
        {
            if (min > max)
            {
                float temp = min;
                min = max;
                max = temp;
            }

            for (int i = 0; i < spans.Count; i++)
            {
                SurfaceMarkingSpan span = spans[i];
                if (max < span.Min || min > span.Max)
                    continue;

                spans[i] = new SurfaceMarkingSpan(Mathf.Min(span.Min, min), Mathf.Max(span.Max, max));
                return;
            }

            spans.Add(new SurfaceMarkingSpan(min, max));
        }

        private static void SortAndMergeSurfaceSpans(List<SurfaceMarkingSpan> spans)
        {
            SortAndMergeSurfaceSpans(spans, 0f);
        }

        private static void SortAndMergeSurfaceSpans(List<SurfaceMarkingSpan> spans, float mergeGap)
        {
            spans.Sort(delegate (SurfaceMarkingSpan a, SurfaceMarkingSpan b)
            {
                return a.Min.CompareTo(b.Min);
            });

            for (int i = spans.Count - 1; i > 0; i--)
            {
                SurfaceMarkingSpan previous = spans[i - 1];
                SurfaceMarkingSpan current = spans[i];
                if (previous.Max + mergeGap < current.Min)
                    continue;

                spans[i - 1] = new SurfaceMarkingSpan(previous.Min, Mathf.Max(previous.Max, current.Max));
                spans.RemoveAt(i);
            }
        }

        private static void AddSignalVisuals(int assetId, CrossingPlacementAsset asset, Vector3 midpoint, Vector3 roadDirection, Vector3 across, List<SurfaceMarkingSpan> roadSpans)
        {
            Vector3 roadDirectionNormal = roadDirection.normalized;
            bool useVanillaCrossingVisual = ShouldUseVanillaSignalNodeCrossing(asset);
            AddSignalStopLines(assetId, midpoint, roadDirectionNormal, across, roadSpans, useVanillaCrossingVisual);
        }

        private static void AddSignalStopLines(int assetId, Vector3 midpoint, Vector3 roadDirection, Vector3 across, List<SurfaceMarkingSpan> roadSpans, bool useVanillaCrossingVisual)
        {
            if (roadDirection.sqrMagnitude <= 0.01f)
            {
                Debug.Log("[PedestrianCrossingToolkit] Signal stop lines skipped: asset="
                          + assetId
                          + " reason=missing-road-direction");
                return;
            }

            SurfaceMarkingSpan fullRoadSpan;
            if (!TryGetSignalStopLineRoadSpan(roadSpans, out fullRoadSpan))
            {
                Debug.Log("[PedestrianCrossingToolkit] Signal stop lines skipped: asset="
                          + assetId
                          + " reason=no-road-span");
                return;
            }

            float length = fullRoadSpan.Max - fullRoadSpan.Min;
            if (length <= 0.01f)
                return;

            roadDirection.Normalize();
            float acrossCenter = (fullRoadSpan.Min + fullRoadSpan.Max) * 0.5f;
            float stopLineDistance = GetSignalStopLineRoadOffsetDistance(useVanillaCrossingVisual);
            float maxStopLineDistance = SignalCrossingMaxVisualDepth - (SignalStopLineWidth * 0.5f);
            float positiveStopLineDistance = Mathf.Min(maxStopLineDistance, stopLineDistance + SignalStopLineSideBalanceOffset);
            float negativeStopLineDistance = Mathf.Max(0f, stopLineDistance - SignalStopLineSideBalanceOffset);
            AddSignalStopLine(assetId, midpoint, roadDirection, across, acrossCenter, length, roadDirection * positiveStopLineDistance);
            AddSignalStopLine(assetId, midpoint, roadDirection, across, acrossCenter, length, -roadDirection * negativeStopLineDistance);
        }

        private static bool TryGetSignalStopLineRoadSpan(List<SurfaceMarkingSpan> roadSpans, out SurfaceMarkingSpan span)
        {
            span = new SurfaceMarkingSpan();
            if (roadSpans == null || roadSpans.Count == 0)
                return false;

            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < roadSpans.Count; i++)
            {
                SurfaceMarkingSpan roadSpan = roadSpans[i];
                min = Mathf.Min(min, roadSpan.Min);
                max = Mathf.Max(max, roadSpan.Max);
            }

            if (max <= min + 0.01f)
                return false;

            span = new SurfaceMarkingSpan(min, max);
            return true;
        }

        private static Vector3 GetSignalStopLineOffset(Vector3 trafficDirection, bool useVanillaCrossingVisual, bool useFarSideExtra, bool useAdjustedSideExtra)
        {
            return trafficDirection.normalized * GetSignalStopLineRoadOffsetDistance(useVanillaCrossingVisual, useFarSideExtra, useAdjustedSideExtra);
        }

        private static Vector3 GetVehicleSignalHeadOffset(Vector3 trafficDirection, bool useVanillaCrossingVisual, bool useFarSideExtra, bool useAdjustedSideExtra)
        {
            return -trafficDirection.normalized * GetSignalStopLineRoadOffsetDistance(useVanillaCrossingVisual, useFarSideExtra, useAdjustedSideExtra);
        }

        private static float GetSignalStopLineRoadOffsetDistance(bool useVanillaCrossingVisual)
        {
            float crossingHalfDepth = useVanillaCrossingVisual
                ? VanillaSignalCrossingVisualRoadHalfDepth
                : SignalCrossingVisualRoadHalfDepth;

            float requestedDistance = crossingHalfDepth
                                      + SignalStopLineOffsetFromCrossing
                                      + (SignalStopLineWidth * 0.5f);
            if (useVanillaCrossingVisual)
                requestedDistance += SignalStopLineExtraClearanceFromVanillaZebra;

            return Mathf.Min(requestedDistance, SignalCrossingMaxVisualDepth - (SignalStopLineWidth * 0.5f));
        }

        private static float GetSignalStopLineRoadOffsetDistance(bool useVanillaCrossingVisual, bool useFarSideExtra, bool useAdjustedSideExtra)
        {
            return GetSignalStopLineRoadOffsetDistance(useVanillaCrossingVisual);
        }

        private static bool IsVanillaSignalAdjustedStopLineSide(CrossingPlacementAsset asset, Vector3 roadDirection, Vector3 trafficDirection)
        {
            if (!ShouldUseVanillaSignalNodeCrossing(asset))
                return false;

            roadDirection.y = 0f;
            trafficDirection.y = 0f;
            if (roadDirection.sqrMagnitude <= 0.01f || trafficDirection.sqrMagnitude <= 0.01f)
                return false;

            roadDirection.Normalize();
            trafficDirection.Normalize();
            float alignment = Vector3.Dot(trafficDirection, roadDirection);
            return asset.Placement.IsEndSegmentSlot ? alignment > 0f : alignment < 0f;
        }

        private static bool TryGetSignalVehicleTrafficSpans(CrossingPlacementAsset asset, Vector3 across, Vector3 roadDirection, List<SurfaceMarkingSpan> roadSpans, List<SignalControlledVehicleSpan> spans)
        {
            List<SignalControlledVehicleSpan> laneSpans = new List<SignalControlledVehicleSpan>();
            NetManager netManager = NetManager.instance;
            if (netManager == null
                || asset.Placement.SegmentId == 0
                || asset.Placement.SegmentId >= netManager.m_segments.m_size)
            {
                return false;
            }

            ref NetSegment segment = ref netManager.m_segments.m_buffer[asset.Placement.SegmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0
                || segment.Info == null
                || segment.Info.m_lanes == null
                || segment.m_startNode == 0
                || segment.m_endNode == 0
                || segment.m_startNode >= netManager.m_nodes.m_size
                || segment.m_endNode >= netManager.m_nodes.m_size)
            {
                return false;
            }

            bool inverted = (segment.m_flags & NetSegment.Flags.Invert) != 0;
            float sideSign = Vector3.Dot(asset.Plan.CrossingDirection, across) >= 0f ? 1f : -1f;
            uint laneId = segment.m_lanes;

            for (int i = 0; i < segment.Info.m_lanes.Length; i++)
            {
                NetInfo.Lane lane = segment.Info.m_lanes[i];
                uint currentLaneId = laneId;
                if (currentLaneId != 0 && currentLaneId < netManager.m_lanes.m_size)
                    laneId = netManager.m_lanes.m_buffer[currentLaneId].m_nextLane;
                else
                    laneId = 0;

                if (lane == null || !IsVehicleSurfaceLane(lane))
                    continue;

                if (currentLaneId == 0 || currentLaneId >= netManager.m_lanes.m_size)
                    continue;

                NetInfo.Direction effectiveDirection = lane.m_finalDirection;
                if (inverted)
                    effectiveDirection = NetInfo.InvertDirection(effectiveDirection);

                Vector3 trafficDirection;
                if (!TryGetSignalLaneTrafficDirection(netManager, currentLaneId, effectiveDirection, out trafficDirection))
                    continue;

                float lanePosition = lane.m_position * sideSign;
                float halfWidth = Mathf.Max(1f, lane.m_width * 0.5f) + SurfaceVehicleLaneEdgeMargin;
                AddMergedTrafficSurfaceSpan(laneSpans, new SurfaceMarkingSpan(lanePosition - halfWidth, lanePosition + halfWidth), trafficDirection);
            }

            BuildSignalControlledRoadSpans(laneSpans, roadSpans, spans);
            return spans.Count > 0;
        }

        private static bool TryGetSignalLaneTrafficDirection(NetManager netManager, uint laneId, NetInfo.Direction effectiveDirection, out Vector3 trafficDirection)
        {
            trafficDirection = Vector3.zero;
            if (netManager == null || laneId == 0 || laneId >= netManager.m_lanes.m_size)
                return false;

            bool useForward;
            bool useBackward;
            if (!TryGetSignalDirectionFlags(effectiveDirection, out useForward, out useBackward) || useForward == useBackward)
                return false;

            Vector3 laneDirection = netManager.m_lanes.m_buffer[laneId].CalculateDirection(0.5f);
            laneDirection.y = 0f;
            if (laneDirection.sqrMagnitude <= 0.01f)
                return false;

            laneDirection.Normalize();
            trafficDirection = useForward ? laneDirection : -laneDirection;
            return true;
        }

        private static void BuildSignalControlledRoadSpans(List<SignalControlledVehicleSpan> laneSpans, List<SurfaceMarkingSpan> roadSpans, List<SignalControlledVehicleSpan> output)
        {
            if (laneSpans.Count == 0 || roadSpans == null || roadSpans.Count == 0)
                return;

            SortTrafficSurfaceSpans(laneSpans);
            int directionCount = CountDistinctTrafficDirections(laneSpans);
            if (directionCount <= 1)
            {
                for (int i = 0; i < roadSpans.Count; i++)
                    AddMergedTrafficSurfaceSpan(output, roadSpans[i], laneSpans[0].TrafficDirection);
                return;
            }

            Vector3 negativeDirection;
            Vector3 positiveDirection;
            bool hasNegative = TryGetDominantTrafficDirectionForSide(laneSpans, true, out negativeDirection);
            bool hasPositive = TryGetDominantTrafficDirectionForSide(laneSpans, false, out positiveDirection);

            if (!hasNegative || !hasPositive)
            {
                AddSignalLaneSpansWithinRoadSpans(laneSpans, roadSpans, output);
                return;
            }

            for (int i = 0; i < roadSpans.Count; i++)
            {
                SurfaceMarkingSpan roadSpan = roadSpans[i];
                float split = Mathf.Clamp(0f, roadSpan.Min, roadSpan.Max);
                if (split > roadSpan.Min + 0.1f)
                    AddMergedTrafficSurfaceSpan(output, new SurfaceMarkingSpan(roadSpan.Min, split), negativeDirection);

                if (roadSpan.Max > split + 0.1f)
                    AddMergedTrafficSurfaceSpan(output, new SurfaceMarkingSpan(split, roadSpan.Max), positiveDirection);
            }
        }

        private static void AddSignalLaneSpansWithinRoadSpans(List<SignalControlledVehicleSpan> laneSpans, List<SurfaceMarkingSpan> roadSpans, List<SignalControlledVehicleSpan> output)
        {
            for (int i = 0; i < laneSpans.Count; i++)
            {
                SignalControlledVehicleSpan laneSpan = laneSpans[i];
                for (int j = 0; j < roadSpans.Count; j++)
                {
                    SurfaceMarkingSpan roadSpan = roadSpans[j];
                    float min = Mathf.Max(laneSpan.Span.Min, roadSpan.Min);
                    float max = Mathf.Min(laneSpan.Span.Max, roadSpan.Max);
                    if (max <= min + 0.1f)
                        continue;

                    AddMergedTrafficSurfaceSpan(output, new SurfaceMarkingSpan(min, max), laneSpan.TrafficDirection);
                }
            }
        }

        private static int CountDistinctTrafficDirections(List<SignalControlledVehicleSpan> spans)
        {
            int count = 0;
            Vector3 first = Vector3.zero;
            for (int i = 0; i < spans.Count; i++)
            {
                Vector3 direction = spans[i].TrafficDirection;
                direction.y = 0f;
                if (direction.sqrMagnitude <= 0.01f)
                    continue;

                direction.Normalize();
                if (count == 0)
                {
                    first = direction;
                    count = 1;
                    continue;
                }

                if (Vector3.Dot(first, direction) < 0.95f)
                    return 2;
            }

            return count;
        }

        private static bool TryGetDominantTrafficDirectionForSide(List<SignalControlledVehicleSpan> spans, bool negativeSide, out Vector3 direction)
        {
            direction = Vector3.zero;
            float bestWidth = 0f;
            for (int i = 0; i < spans.Count; i++)
            {
                SurfaceMarkingSpan span = spans[i].Span;
                float center = (span.Min + span.Max) * 0.5f;
                if (negativeSide ? center > 0f : center < 0f)
                    continue;

                float width = span.Max - span.Min;
                if (width <= bestWidth)
                    continue;

                direction = spans[i].TrafficDirection;
                bestWidth = width;
            }

            return bestWidth > 0.01f && direction.sqrMagnitude > 0.01f;
        }

        private static bool TryGetSignalDirectionFlags(NetInfo.Direction effectiveDirection, out bool useForward, out bool useBackward)
        {
            useForward = false;
            useBackward = false;

            if (effectiveDirection == NetInfo.Direction.AvoidBackward)
            {
                useForward = true;
                return true;
            }

            if (effectiveDirection == NetInfo.Direction.AvoidForward)
            {
                useBackward = true;
                return true;
            }

            bool hasForward = (effectiveDirection & NetInfo.Direction.Forward) != 0;
            bool hasBackward = (effectiveDirection & NetInfo.Direction.Backward) != 0;
            if (!hasForward && !hasBackward)
                return false;

            useForward = hasForward;
            useBackward = hasBackward;
            return true;
        }

        private static void SortTrafficSurfaceSpans(List<SignalControlledVehicleSpan> spans)
        {
            spans.Sort(delegate (SignalControlledVehicleSpan a, SignalControlledVehicleSpan b)
            {
                return a.Span.Min.CompareTo(b.Span.Min);
            });
        }

        private static void AddMergedTrafficSurfaceSpan(List<SignalControlledVehicleSpan> spans, SurfaceMarkingSpan span, Vector3 trafficDirection)
        {
            Vector3 direction = trafficDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return;

            direction.Normalize();
            if (span.Min > span.Max)
            {
                float temp = span.Min;
                span.Min = span.Max;
                span.Max = temp;
            }

            for (int i = 0; i < spans.Count; i++)
            {
                SignalControlledVehicleSpan existing = spans[i];
                if (Vector3.Dot(existing.TrafficDirection, direction) < 0.95f)
                    continue;

                if (span.Max + SignalVehicleSpanMergeGap < existing.Span.Min
                    || span.Min > existing.Span.Max + SignalVehicleSpanMergeGap)
                {
                    continue;
                }

                spans[i] = new SignalControlledVehicleSpan(
                    new SurfaceMarkingSpan(Mathf.Min(existing.Span.Min, span.Min), Mathf.Max(existing.Span.Max, span.Max)),
                    existing.TrafficDirection);
                return;
            }

            spans.Add(new SignalControlledVehicleSpan(span, direction));
        }

        private static void AddSignalStopLine(int assetId, Vector3 midpoint, Vector3 roadDirection, Vector3 across, float acrossCenter, float length, Vector3 roadOffset)
        {
            AddTrackedQuad(
                "PCT Signal Crossing stop line #" + assetId,
                midpoint + across * acrossCenter + roadOffset,
                Quaternion.LookRotation(roadDirection, Vector3.up),
                length,
                SignalStopLineWidth,
                GetCrossingStripeMaterial());
        }

    }
}
