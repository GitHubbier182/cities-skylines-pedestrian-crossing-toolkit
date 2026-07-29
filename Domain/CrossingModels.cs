using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public enum PedestrianToolMode
    {
        None,
        MidBlockCrossing,
        SignalCrossing,
        SubwayLink,
        SubwayPointToPoint,
        PedestrianBridge,
        AutoScanReject,
        RemoveCrossing
    }

    public enum CrossingApplicationKind
    {
        None,
        SurfaceCrossing,
        SignalizedSurfaceCrossing,
        SubwayLink,
        SubwayPointToPoint,
        SubwayJunctionSuppressSurface,
        PedestrianBridge
    }

    public enum CrossingNetworkOperationKind
    {
        None,
        EnableNodeSurfaceCrossing,
        EnablePedestrianSignal,
        CreateMidBlockSignalCrossing,
        CreateSubwayLink,
        CreatePointToPointSubwayLink,
        CreateJunctionSubwayLink,
        CreatePedestrianBridge,
        CreateJunctionPedestrianBridge,
        SuppressNodeSurfaceCrossings
    }

    public enum CrossingNetworkOperationReadiness
    {
        Ready,
        Blocked,
        Unsupported
    }

    public struct CrossingPlacementRecord
    {
        public static readonly CrossingPlacementRecord None = new CrossingPlacementRecord(PedestrianToolMode.None, 0, 0f, Vector3.zero, false, "No preview.");

        public readonly PedestrianToolMode Mode;
        public readonly ushort SegmentId;
        public readonly float SegmentPosition;
        public readonly Vector3 WorldPosition;
        public readonly bool IsValid;
        public readonly string Message;
        public readonly bool NearNode;
        public readonly int SlotNumber;
        public readonly bool IsEndSegmentSlot;
        public readonly ushort TargetNodeId;
        public readonly bool HasSecondaryPoint;
        public readonly ushort SecondarySegmentId;
        public readonly float SecondarySegmentPosition;
        public readonly Vector3 SecondaryWorldPosition;
        public readonly bool SecondaryNearNode;
        public readonly int SecondarySlotNumber;
        public readonly bool SecondaryIsEndSegmentSlot;
        public readonly ushort SecondaryTargetNodeId;

        public CrossingPlacementRecord(PedestrianToolMode mode, ushort segmentId, float segmentPosition, Vector3 worldPosition, bool isValid, string message)
            : this(mode, segmentId, segmentPosition, worldPosition, isValid, message, false)
        {
        }

        public CrossingPlacementRecord(PedestrianToolMode mode, ushort segmentId, float segmentPosition, Vector3 worldPosition, bool isValid, string message, bool nearNode)
            : this(mode, segmentId, segmentPosition, worldPosition, isValid, message, nearNode, 0, false)
        {
        }

        public CrossingPlacementRecord(PedestrianToolMode mode, ushort segmentId, float segmentPosition, Vector3 worldPosition, bool isValid, string message, bool nearNode, int slotNumber, bool isEndSegmentSlot)
            : this(mode, segmentId, segmentPosition, worldPosition, isValid, message, nearNode, slotNumber, isEndSegmentSlot, 0)
        {
        }

        public CrossingPlacementRecord(PedestrianToolMode mode, ushort segmentId, float segmentPosition, Vector3 worldPosition, bool isValid, string message, bool nearNode, int slotNumber, bool isEndSegmentSlot, ushort targetNodeId)
            : this(mode, segmentId, segmentPosition, worldPosition, isValid, message, nearNode, slotNumber, isEndSegmentSlot, targetNodeId, false, 0, 0f, Vector3.zero, false, 0, false, 0)
        {
        }

        public CrossingPlacementRecord(
            PedestrianToolMode mode,
            ushort segmentId,
            float segmentPosition,
            Vector3 worldPosition,
            bool isValid,
            string message,
            bool nearNode,
            int slotNumber,
            bool isEndSegmentSlot,
            ushort targetNodeId,
            bool hasSecondaryPoint,
            ushort secondarySegmentId,
            float secondarySegmentPosition,
            Vector3 secondaryWorldPosition,
            bool secondaryNearNode,
            int secondarySlotNumber,
            bool secondaryIsEndSegmentSlot,
            ushort secondaryTargetNodeId)
        {
            Mode = mode;
            SegmentId = segmentId;
            SegmentPosition = segmentPosition;
            WorldPosition = worldPosition;
            IsValid = isValid;
            Message = message;
            NearNode = nearNode;
            SlotNumber = slotNumber;
            IsEndSegmentSlot = isEndSegmentSlot;
            TargetNodeId = targetNodeId;
            HasSecondaryPoint = hasSecondaryPoint;
            SecondarySegmentId = secondarySegmentId;
            SecondarySegmentPosition = secondarySegmentPosition;
            SecondaryWorldPosition = secondaryWorldPosition;
            SecondaryNearNode = secondaryNearNode;
            SecondarySlotNumber = secondarySlotNumber;
            SecondaryIsEndSegmentSlot = secondaryIsEndSegmentSlot;
            SecondaryTargetNodeId = secondaryTargetNodeId;
        }
    }

    public struct CrossingPlacementPlan
    {
        public static readonly CrossingPlacementPlan Invalid = new CrossingPlacementPlan(false, PedestrianToolMode.None, 0, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, 0f, 0, 0, new Vector3[0], false, CrossingApplicationKind.None, false);

        public readonly bool IsValid;
        public readonly PedestrianToolMode Mode;
        public readonly ushort SegmentId;
        public readonly ushort TargetNodeId;
        public readonly Vector3 Center;
        public readonly Vector3 RoadDirection;
        public readonly Vector3 CrossingDirection;
        public readonly Vector3 LeftEdge;
        public readonly Vector3 RightEdge;
        public readonly float Width;
        public readonly int ConnectedSegmentCount;
        public readonly Vector3[] JunctionExitPoints;
        public readonly bool SuppressSurfaceCrossing;
        public readonly CrossingApplicationKind ApplicationKind;
        public readonly bool FlipBridgeAccess;

        public int JunctionExitCount
        {
            get { return JunctionExitPoints == null ? 0 : JunctionExitPoints.Length; }
        }

        public CrossingPlacementPlan(bool isValid, PedestrianToolMode mode, ushort segmentId, Vector3 center, Vector3 roadDirection, Vector3 crossingDirection, Vector3 leftEdge, Vector3 rightEdge, float width, ushort targetNodeId, int connectedSegmentCount, Vector3[] junctionExitPoints, bool suppressSurfaceCrossing, CrossingApplicationKind applicationKind)
            : this(isValid, mode, segmentId, center, roadDirection, crossingDirection, leftEdge, rightEdge, width, targetNodeId, connectedSegmentCount, junctionExitPoints, suppressSurfaceCrossing, applicationKind, false)
        {
        }

        public CrossingPlacementPlan(bool isValid, PedestrianToolMode mode, ushort segmentId, Vector3 center, Vector3 roadDirection, Vector3 crossingDirection, Vector3 leftEdge, Vector3 rightEdge, float width, ushort targetNodeId, int connectedSegmentCount, Vector3[] junctionExitPoints, bool suppressSurfaceCrossing, CrossingApplicationKind applicationKind, bool flipBridgeAccess)
        {
            IsValid = isValid;
            Mode = mode;
            SegmentId = segmentId;
            TargetNodeId = targetNodeId;
            Center = center;
            RoadDirection = roadDirection;
            CrossingDirection = crossingDirection;
            LeftEdge = leftEdge;
            RightEdge = rightEdge;
            Width = width;
            ConnectedSegmentCount = connectedSegmentCount;
            JunctionExitPoints = junctionExitPoints ?? new Vector3[0];
            SuppressSurfaceCrossing = suppressSurfaceCrossing;
            ApplicationKind = applicationKind;
            FlipBridgeAccess = flipBridgeAccess;
        }

        public CrossingPlacementPlan WithBridgeAccessFlipped()
        {
            return new CrossingPlacementPlan(IsValid, Mode, SegmentId, Center, RoadDirection, CrossingDirection, LeftEdge, RightEdge, Width, TargetNodeId, ConnectedSegmentCount, JunctionExitPoints, SuppressSurfaceCrossing, ApplicationKind, !FlipBridgeAccess);
        }
    }

    public struct CrossingPlacementAsset
    {
        public static readonly CrossingPlacementAsset None = new CrossingPlacementAsset(0, CrossingPlacementRecord.None, CrossingPlacementPlan.Invalid);

        public readonly int Id;
        public readonly CrossingPlacementRecord Placement;
        public readonly CrossingPlacementPlan Plan;
        public readonly SignalRoadStateSnapshot SignalRoadState;

        public CrossingPlacementAsset(int id, CrossingPlacementRecord placement, CrossingPlacementPlan plan)
            : this(id, placement, plan, SignalRoadStateSnapshot.Empty)
        {
        }

        public CrossingPlacementAsset(int id, CrossingPlacementRecord placement, CrossingPlacementPlan plan, SignalRoadStateSnapshot signalRoadState)
        {
            Id = id;
            Placement = placement;
            Plan = plan;
            SignalRoadState = signalRoadState;
        }
    }

    public struct SignalRoadStateSnapshot
    {
        public static readonly SignalRoadStateSnapshot Empty = new SignalRoadStateSnapshot(false, 0, 0, new SignalRoadSegmentState[0]);

        public readonly bool HasSnapshot;
        public readonly ushort NodeId;
        public readonly NetNode.Flags NodeFlags;
        public readonly SignalRoadSegmentState[] Segments;

        public SignalRoadStateSnapshot(bool hasSnapshot, ushort nodeId, NetNode.Flags nodeFlags, SignalRoadSegmentState[] segments)
        {
            HasSnapshot = hasSnapshot;
            NodeId = nodeId;
            NodeFlags = nodeFlags;
            Segments = segments ?? new SignalRoadSegmentState[0];
        }

        public int SegmentCount
        {
            get { return Segments == null ? 0 : Segments.Length; }
        }
    }

    public struct SignalRoadSegmentState
    {
        public readonly ushort SegmentId;
        public readonly bool StartNode;
        public readonly NetSegment.Flags SegmentFlags;
        public readonly RoadBaseAI.TrafficLightState VehicleState;
        public readonly RoadBaseAI.TrafficLightState PedestrianState;
        public readonly bool Vehicles;
        public readonly bool Pedestrians;

        public SignalRoadSegmentState(
            ushort segmentId,
            bool startNode,
            NetSegment.Flags segmentFlags,
            RoadBaseAI.TrafficLightState vehicleState,
            RoadBaseAI.TrafficLightState pedestrianState,
            bool vehicles,
            bool pedestrians)
        {
            SegmentId = segmentId;
            StartNode = startNode;
            SegmentFlags = segmentFlags;
            VehicleState = vehicleState;
            PedestrianState = pedestrianState;
            Vehicles = vehicles;
            Pedestrians = pedestrians;
        }
    }

    public struct CrossingNetworkOperation
    {
        public readonly CrossingNetworkOperationKind Kind;
        public readonly int AssetId;
        public readonly ushort SegmentId;
        public readonly ushort NodeId;
        public readonly Vector3 Position;

        public CrossingNetworkOperation(CrossingNetworkOperationKind kind, int assetId, ushort segmentId, ushort nodeId, Vector3 position)
        {
            Kind = kind;
            AssetId = assetId;
            SegmentId = segmentId;
            NodeId = nodeId;
            Position = position;
        }

        public string ToLogString()
        {
            return "asset=" + AssetId
                   + " op=" + Kind
                   + " segment=" + SegmentId
                   + " node=" + NodeId
                   + " position=" + Position;
        }
    }

    public struct CrossingNetworkOperationValidation
    {
        public readonly CrossingNetworkOperationReadiness Readiness;
        public readonly string Reason;

        public CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness readiness, string reason)
        {
            Readiness = readiness;
            Reason = reason;
        }
    }

    public struct CrossingNetworkValidationSummary
    {
        public static readonly CrossingNetworkValidationSummary Empty = new CrossingNetworkValidationSummary(0, 0, 0);

        public readonly int Ready;
        public readonly int Blocked;
        public readonly int Unsupported;

        public CrossingNetworkValidationSummary(int ready, int blocked, int unsupported)
        {
            Ready = ready;
            Blocked = blocked;
            Unsupported = unsupported;
        }

        public string ToLogString()
        {
            return "ready=" + Ready
                   + " blocked=" + Blocked
                   + " unsupported=" + Unsupported;
        }
    }

    public struct CrossingConnectivitySummary
    {
        public static readonly CrossingConnectivitySummary Empty = new CrossingConnectivitySummary(0, 0, 0, 0, 0, 0, 0, 0);

        public readonly int Assets;
        public readonly int AssetsWithPedestrianLanes;
        public readonly int AssetsMissingPedestrianLanes;
        public readonly int CandidateLanes;
        public readonly int CandidateLinks;
        public readonly int LandingLinks;
        public readonly int AssetsWithoutLinks;
        public readonly int JunctionSegmentsScanned;

        public CrossingConnectivitySummary(int assets, int assetsWithPedestrianLanes, int assetsMissingPedestrianLanes, int candidateLanes, int candidateLinks, int landingLinks, int assetsWithoutLinks, int junctionSegmentsScanned)
        {
            Assets = assets;
            AssetsWithPedestrianLanes = assetsWithPedestrianLanes;
            AssetsMissingPedestrianLanes = assetsMissingPedestrianLanes;
            CandidateLanes = candidateLanes;
            CandidateLinks = candidateLinks;
            LandingLinks = landingLinks;
            AssetsWithoutLinks = assetsWithoutLinks;
            JunctionSegmentsScanned = junctionSegmentsScanned;
        }

        public string ToLogString()
        {
            return "assets=" + Assets
                   + " withPedestrianLanes=" + AssetsWithPedestrianLanes
                   + " missingPedestrianLanes=" + AssetsMissingPedestrianLanes
                   + " candidateLanes=" + CandidateLanes
                   + " candidateLinks=" + CandidateLinks
                   + " landingLinks=" + LandingLinks
                   + " assetsWithoutLinks=" + AssetsWithoutLinks
                   + " junctionSegmentsScanned=" + JunctionSegmentsScanned;
        }
    }

    public enum CrossingConnectivityLinkKind
    {
        SurfaceSpan,
        SignalizedSurfaceSpan,
        SubwaySpan,
        PedestrianBridgeSpan,
        JunctionSubwayApproach,
        JunctionBridgeApproach
    }

    public struct CrossingConnectivityCandidate
    {
        public readonly int AssetId;
        public readonly ushort SegmentId;
        public readonly ushort NodeId;
        public readonly uint LaneId;
        public readonly int LaneIndex;
        public readonly float LanePosition;
        public readonly Vector3 WorldPosition;

        public CrossingConnectivityCandidate(int assetId, ushort segmentId, ushort nodeId, uint laneId, int laneIndex, float lanePosition, Vector3 worldPosition)
        {
            AssetId = assetId;
            SegmentId = segmentId;
            NodeId = nodeId;
            LaneId = laneId;
            LaneIndex = laneIndex;
            LanePosition = lanePosition;
            WorldPosition = worldPosition;
        }

        public string ToLogString()
        {
            return "asset=" + AssetId
                   + " segment=" + SegmentId
                   + " node=" + NodeId
                   + " lane=" + LaneId
                   + " laneIndex=" + LaneIndex
                   + " lanePosition=" + LanePosition.ToString("0.00")
                   + " position=" + WorldPosition;
        }
    }

    public struct CrossingConnectivityLink
    {
        public readonly int AssetId;
        public readonly ushort SegmentId;
        public readonly CrossingConnectivityLinkKind Kind;
        public readonly uint FirstLaneId;
        public readonly uint SecondLaneId;
        public readonly Vector3 FirstPosition;
        public readonly Vector3 SecondPosition;
        public readonly float Span;
        public readonly bool UsesLaneTargets;
        public readonly bool FlipBridgeAccess;

        public CrossingConnectivityLink(int assetId, ushort segmentId, CrossingConnectivityLinkKind kind, uint firstLaneId, uint secondLaneId, Vector3 firstPosition, Vector3 secondPosition)
            : this(assetId, segmentId, kind, firstLaneId, secondLaneId, firstPosition, secondPosition, true)
        {
        }

        public CrossingConnectivityLink(int assetId, ushort segmentId, CrossingConnectivityLinkKind kind, uint firstLaneId, uint secondLaneId, Vector3 firstPosition, Vector3 secondPosition, bool usesLaneTargets)
            : this(assetId, segmentId, kind, firstLaneId, secondLaneId, firstPosition, secondPosition, usesLaneTargets, false)
        {
        }

        public CrossingConnectivityLink(int assetId, ushort segmentId, CrossingConnectivityLinkKind kind, uint firstLaneId, uint secondLaneId, Vector3 firstPosition, Vector3 secondPosition, bool usesLaneTargets, bool flipBridgeAccess)
        {
            AssetId = assetId;
            SegmentId = segmentId;
            Kind = kind;
            FirstLaneId = firstLaneId;
            SecondLaneId = secondLaneId;
            FirstPosition = firstPosition;
            SecondPosition = secondPosition;
            Span = Vector3.Distance(firstPosition, secondPosition);
            UsesLaneTargets = usesLaneTargets;
            FlipBridgeAccess = flipBridgeAccess;
        }

        public string ToLogString()
        {
            return "asset=" + AssetId
                   + " segment=" + SegmentId
                   + " kind=" + Kind
                   + " laneA=" + FirstLaneId
                   + " laneB=" + SecondLaneId
                   + " source=" + (UsesLaneTargets ? "pedestrian-lanes" : "road-edge-landings")
                   + " flipBridgeAccess=" + FlipBridgeAccess
                   + " span=" + Span.ToString("0.0")
                   + " from=" + FirstPosition
                   + " to=" + SecondPosition;
        }
    }
}
