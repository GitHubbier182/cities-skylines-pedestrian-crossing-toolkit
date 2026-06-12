using System;
using System.Collections.Generic;
using ColossalFramework.Math;
using UnityEngine;
using UnityEngine.Rendering;

namespace PedestrianCrossingToolkit
{
    public static partial class CrossingPathBuilder
    {
        private sealed class SubwayEntranceInfoViewVisibility : MonoBehaviour
        {
            private static int _lastCheckedFrame = -1;
            private static bool _showSurfaceEntrances = true;

            private Renderer _renderer;

            private void Awake()
            {
                _renderer = GetComponent<Renderer>();
                UpdateVisibility();
            }

            private void OnEnable()
            {
                UpdateVisibility();
            }

            private void Update()
            {
                UpdateVisibility();
            }

            private void UpdateVisibility()
            {
                if (_renderer == null)
                    _renderer = GetComponent<Renderer>();

                if (_renderer == null)
                    return;

                bool shouldShow = ShouldShowSurfaceEntrances();
                if (_renderer.enabled != shouldShow)
                    _renderer.enabled = shouldShow;
            }

            private static bool ShouldShowSurfaceEntrances()
            {
                if (Time.frameCount == _lastCheckedFrame)
                    return _showSurfaceEntrances;

                _lastCheckedFrame = Time.frameCount;
                InfoManager infoManager = InfoManager.instance;
                _showSurfaceEntrances = infoManager == null || infoManager.CurrentMode == InfoManager.InfoMode.None;
                return _showSurfaceEntrances;
            }
        }

        public struct BuiltConnectorValidationSummary
        {
            public static readonly BuiltConnectorValidationSummary Empty = new BuiltConnectorValidationSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            public readonly int Segments;
            public readonly int CreatedSegments;
            public readonly int PedestrianSegments;
            public readonly int Nodes;
            public readonly int ConnectedNodes;
            public readonly int IsolatedNodes;
            public readonly int IsolatedSurfaceNodes;
            public readonly int IsolatedAccessNodes;
            public readonly int UnattachedTerminalNodes;
            public readonly int UnattachedSurfaceTerminalNodes;
            public readonly int UnattachedAccessTerminalNodes;

            public BuiltConnectorValidationSummary(int segments, int createdSegments, int pedestrianSegments, int nodes, int connectedNodes, int isolatedNodes, int isolatedSurfaceNodes, int isolatedAccessNodes, int unattachedTerminalNodes, int unattachedSurfaceTerminalNodes, int unattachedAccessTerminalNodes)
            {
                Segments = segments;
                CreatedSegments = createdSegments;
                PedestrianSegments = pedestrianSegments;
                Nodes = nodes;
                ConnectedNodes = connectedNodes;
                IsolatedNodes = isolatedNodes;
                IsolatedSurfaceNodes = isolatedSurfaceNodes;
                IsolatedAccessNodes = isolatedAccessNodes;
                UnattachedTerminalNodes = unattachedTerminalNodes;
                UnattachedSurfaceTerminalNodes = unattachedSurfaceTerminalNodes;
                UnattachedAccessTerminalNodes = unattachedAccessTerminalNodes;
            }

            public string ToLogString()
            {
                return "segments=" + Segments
                       + " created=" + CreatedSegments
                       + " pedestrian=" + PedestrianSegments
                       + " nodes=" + Nodes
                       + " connectedNodes=" + ConnectedNodes
                       + " isolatedNodes=" + IsolatedNodes
                       + " isolatedSurfaceNodes=" + IsolatedSurfaceNodes
                       + " isolatedAccessNodes=" + IsolatedAccessNodes
                       + " unattachedTerminalNodes=" + UnattachedTerminalNodes
                       + " unattachedSurfaceTerminalNodes=" + UnattachedSurfaceTerminalNodes
                       + " unattachedAccessTerminalNodes=" + UnattachedAccessTerminalNodes;
            }
        }

        public struct SignalControllerDebugSnapshot
        {
            public readonly bool IsValid;
            public readonly int AssetId;
            public readonly ushort NodeId;
            public readonly Vector3 Center;
            public readonly Vector3 FirstPosition;
            public readonly Vector3 SecondPosition;
            public readonly string Phase;
            public readonly float PhaseTime;
            public readonly float CooldownTime;
            public readonly bool HasPedestriansNear;
            public readonly bool HasPedestriansWaitingAtEntrance;
            public readonly bool HasPedestriansOnCrossing;
            public readonly RoadBaseAI.TrafficLightState VehicleState;
            public readonly RoadBaseAI.TrafficLightState PedestrianState;
            public readonly bool HasLiveState;
            public readonly RoadBaseAI.TrafficLightState LiveVehicleState;
            public readonly RoadBaseAI.TrafficLightState LivePedestrianState;
            public readonly bool LiveVehiclesFlag;
            public readonly bool LivePedestriansFlag;
            public readonly bool NodeIsJunction;
            public readonly bool TrafficManagerTimedActive;

            public SignalControllerDebugSnapshot(
                int assetId,
                ushort nodeId,
                Vector3 center,
                Vector3 firstPosition,
                Vector3 secondPosition,
                string phase,
                float phaseTime,
                float cooldownTime,
                bool hasPedestriansNear,
                bool hasPedestriansWaitingAtEntrance,
                bool hasPedestriansOnCrossing,
                RoadBaseAI.TrafficLightState vehicleState,
                RoadBaseAI.TrafficLightState pedestrianState,
                bool hasLiveState,
                RoadBaseAI.TrafficLightState liveVehicleState,
                RoadBaseAI.TrafficLightState livePedestrianState,
                bool liveVehiclesFlag,
                bool livePedestriansFlag,
                bool nodeIsJunction,
                bool trafficManagerTimedActive)
            {
                IsValid = true;
                AssetId = assetId;
                NodeId = nodeId;
                Center = center;
                FirstPosition = firstPosition;
                SecondPosition = secondPosition;
                Phase = phase;
                PhaseTime = phaseTime;
                CooldownTime = cooldownTime;
                HasPedestriansNear = hasPedestriansNear;
                HasPedestriansWaitingAtEntrance = hasPedestriansWaitingAtEntrance;
                HasPedestriansOnCrossing = hasPedestriansOnCrossing;
                VehicleState = vehicleState;
                PedestrianState = pedestrianState;
                HasLiveState = hasLiveState;
                LiveVehicleState = liveVehicleState;
                LivePedestrianState = livePedestrianState;
                LiveVehiclesFlag = liveVehiclesFlag;
                LivePedestriansFlag = livePedestriansFlag;
                NodeIsJunction = nodeIsJunction;
                TrafficManagerTimedActive = trafficManagerTimedActive;
            }
        }

        private struct SubwayEntranceTerrainFrame
        {
            public readonly CrossingLandingAccessAssetWorkOrder Order;
            public readonly Vector3 Origin;
            public readonly Vector3 Forward;
            public readonly Vector3 Side;
            public readonly Vector3 Normal;
            public readonly Quaternion Rotation;

            public SubwayEntranceTerrainFrame(CrossingLandingAccessAssetWorkOrder order, Vector3 origin, Vector3 forward, Vector3 side, Vector3 normal)
            {
                Order = order;
                Origin = origin;
                Forward = forward;
                Side = side;
                Normal = normal;
                Rotation = Quaternion.LookRotation(forward, normal);
            }

            public Vector3 SurfacePoint(Vector3 position, float lift)
            {
                Vector3 offset = position - Origin;
                offset.y = 0f;
                return Origin
                       + Forward * Vector3.Dot(offset, Forward)
                       + Side * Vector3.Dot(offset, Side)
                       + Normal * lift;
            }
        }

        private struct SurfaceMarkingSpan
        {
            public float Min;
            public float Max;

            public SurfaceMarkingSpan(float min, float max)
            {
                Min = min;
                Max = max;
            }
        }

        private struct SurfaceSidewalkEdges
        {
            public readonly bool HasNegativeEdge;
            public readonly bool HasPositiveEdge;
            public readonly float NegativeEdge;
            public readonly float PositiveEdge;

            public SurfaceSidewalkEdges(bool hasNegativeEdge, float negativeEdge, bool hasPositiveEdge, float positiveEdge)
            {
                HasNegativeEdge = hasNegativeEdge;
                NegativeEdge = negativeEdge;
                HasPositiveEdge = hasPositiveEdge;
                PositiveEdge = positiveEdge;
            }
        }

        private struct SignalPedestrianSpan
        {
            public Vector3 FirstPosition;
            public Vector3 SecondPosition;
            public Vector3 ConflictFirstPosition;
            public Vector3 ConflictSecondPosition;
            public Vector3 RoadDirection;
            public bool ExpandedWaitingZones;

            public SignalPedestrianSpan(Vector3 firstPosition, Vector3 secondPosition, Vector3 conflictFirstPosition, Vector3 conflictSecondPosition, Vector3 roadDirection, bool expandedWaitingZones)
            {
                FirstPosition = firstPosition;
                SecondPosition = secondPosition;
                ConflictFirstPosition = conflictFirstPosition;
                ConflictSecondPosition = conflictSecondPosition;
                RoadDirection = roadDirection;
                ExpandedWaitingZones = expandedWaitingZones;
            }
        }

        private struct SignalPedestrianZoneStatus
        {
            public bool HasPedestriansNear;
            public bool HasPedestriansWaiting;
            public bool HasPedestriansOnCrossing;
            public bool HasDemandCandidateNear;
        }

        private struct SignalControlledVehicleSpan
        {
            public readonly SurfaceMarkingSpan Span;
            public readonly Vector3 TrafficDirection;

            public SignalControlledVehicleSpan(SurfaceMarkingSpan span, Vector3 trafficDirection)
            {
                Span = span;
                TrafficDirection = trafficDirection;
            }
        }

        private struct BuiltSignalController
        {
            public int AssetId;
            public ushort NodeId;
            public ushort RoadSegmentId;
            public HashSet<ushort> RoadSegmentIds;
            public Vector3 Center;
            public Vector3 FirstPosition;
            public Vector3 SecondPosition;
            public Vector3 ConflictFirstPosition;
            public Vector3 ConflictSecondPosition;
            public List<SignalPedestrianSpan> PedestrianSpans;
            public SignalControllerPhase Phase;
            public float PhaseTime;
            public float CooldownTime;
            public float SignalStateRefreshTime;
            public float PedestrianScanTime;
            public float PedestrianFullScanFallbackTime;
            public bool HasAppliedSignalState;
            public bool HasPedestrianRequest;
            public bool HasPedestriansNear;
            public bool HasPedestriansWaitingAtEntrance;
            public bool HasPedestriansOnCrossing;
            public int PedestriansNearCount;
            public int PedestrianDemandCandidateCount;
            public RoadBaseAI.TrafficLightState AppliedVehicleState;
            public RoadBaseAI.TrafficLightState AppliedPedestrianState;

            public BuiltSignalController(int assetId, ushort nodeId, ushort roadSegmentId, Vector3 center, Vector3 firstPosition, Vector3 secondPosition, Vector3 conflictFirstPosition, Vector3 conflictSecondPosition, Vector3 roadDirection, bool expandedWaitingZones)
            {
                AssetId = assetId;
                NodeId = nodeId;
                RoadSegmentId = roadSegmentId;
                RoadSegmentIds = new HashSet<ushort>();
                if (roadSegmentId != 0)
                    RoadSegmentIds.Add(roadSegmentId);
                Center = center;
                FirstPosition = firstPosition;
                SecondPosition = secondPosition;
                ConflictFirstPosition = conflictFirstPosition;
                ConflictSecondPosition = conflictSecondPosition;
                PedestrianSpans = new List<SignalPedestrianSpan>
                {
                    new SignalPedestrianSpan(firstPosition, secondPosition, conflictFirstPosition, conflictSecondPosition, roadDirection, expandedWaitingZones)
                };
                Phase = SignalControllerPhase.Idle;
                PhaseTime = 0f;
                CooldownTime = 0f;
                SignalStateRefreshTime = GetSignalControllerStagger(assetId, nodeId, SignalControllerStateRefreshSeconds);
                PedestrianScanTime = GetSignalControllerStagger(assetId, nodeId, SignalPedestrianScanSeconds);
                PedestrianFullScanFallbackTime = GetSignalControllerStagger(assetId, nodeId, SignalPedestrianFullScanFallbackSeconds);
                HasAppliedSignalState = false;
                HasPedestrianRequest = false;
                HasPedestriansNear = false;
                HasPedestriansWaitingAtEntrance = false;
                HasPedestriansOnCrossing = false;
                PedestriansNearCount = 0;
                PedestrianDemandCandidateCount = 0;
                AppliedVehicleState = RoadBaseAI.TrafficLightState.Red;
                AppliedPedestrianState = RoadBaseAI.TrafficLightState.Red;
            }
        }

        private struct SignalControllerStateRestore
        {
            public readonly int AssetId;
            public readonly ushort NodeId;
            public readonly SignalControllerPhase Phase;
            public readonly float PhaseTime;
            public readonly float CooldownTime;
            public readonly float SignalStateRefreshTime;
            public readonly float PedestrianScanTime;
            public readonly float PedestrianFullScanFallbackTime;
            public readonly bool HasAppliedSignalState;
            public readonly bool HasPedestrianRequest;
            public readonly bool HasPedestriansNear;
            public readonly bool HasPedestriansWaitingAtEntrance;
            public readonly bool HasPedestriansOnCrossing;
            public readonly int PedestriansNearCount;
            public readonly int PedestrianDemandCandidateCount;
            public readonly RoadBaseAI.TrafficLightState AppliedVehicleState;
            public readonly RoadBaseAI.TrafficLightState AppliedPedestrianState;

            public SignalControllerStateRestore(BuiltSignalController controller)
            {
                AssetId = controller.AssetId;
                NodeId = controller.NodeId;
                Phase = controller.Phase;
                PhaseTime = controller.PhaseTime;
                CooldownTime = controller.CooldownTime;
                SignalStateRefreshTime = controller.SignalStateRefreshTime;
                PedestrianScanTime = controller.PedestrianScanTime;
                PedestrianFullScanFallbackTime = controller.PedestrianFullScanFallbackTime;
                HasAppliedSignalState = controller.HasAppliedSignalState;
                HasPedestrianRequest = controller.HasPedestrianRequest;
                HasPedestriansNear = controller.HasPedestriansNear;
                HasPedestriansWaitingAtEntrance = controller.HasPedestriansWaitingAtEntrance;
                HasPedestriansOnCrossing = controller.HasPedestriansOnCrossing;
                PedestriansNearCount = controller.PedestriansNearCount;
                PedestrianDemandCandidateCount = controller.PedestrianDemandCandidateCount;
                AppliedVehicleState = controller.AppliedVehicleState;
                AppliedPedestrianState = controller.AppliedPedestrianState;
            }

            public void ApplyTo(ref BuiltSignalController controller)
            {
                controller.Phase = Phase;
                controller.PhaseTime = PhaseTime;
                controller.CooldownTime = CooldownTime;
                controller.SignalStateRefreshTime = SignalStateRefreshTime;
                controller.PedestrianScanTime = PedestrianScanTime;
                controller.PedestrianFullScanFallbackTime = PedestrianFullScanFallbackTime;
                controller.HasAppliedSignalState = HasAppliedSignalState;
                controller.HasPedestrianRequest = HasPedestrianRequest;
                controller.HasPedestriansNear = HasPedestriansNear;
                controller.HasPedestriansWaitingAtEntrance = HasPedestriansWaitingAtEntrance;
                controller.HasPedestriansOnCrossing = HasPedestriansOnCrossing;
                controller.PedestriansNearCount = PedestriansNearCount;
                controller.PedestrianDemandCandidateCount = PedestrianDemandCandidateCount;
                controller.AppliedVehicleState = AppliedVehicleState;
                controller.AppliedPedestrianState = AppliedPedestrianState;
            }
        }

        private static float GetSignalControllerStagger(int assetId, ushort nodeId, float interval)
        {
            if (interval <= 0.01f)
                return 0f;

            uint seed = (uint)(assetId * 1103515245) ^ ((uint)nodeId * 2654435761u);
            return ((seed % 1000u) / 1000f) * interval;
        }

        private struct SurfaceCrossingFrame
        {
            public readonly Vector3 First;
            public readonly Vector3 Second;
            public readonly Vector3 Across;
            public readonly Vector3 RoadDirection;
            public readonly Vector3 Center;
            public readonly Vector3 PathBuildOffset;

            public SurfaceCrossingFrame(Vector3 first, Vector3 second, Vector3 across, Vector3 roadDirection, Vector3 center, Vector3 pathBuildOffset)
            {
                First = first;
                Second = second;
                Across = across;
                RoadDirection = roadDirection;
                Center = center;
                PathBuildOffset = pathBuildOffset;
            }
        }

        private enum SignalControllerPhase
        {
            Idle,
            Crossing,
            Clearance
        }

        private const float ManagedSegmentMatchTolerance = 4f;
        private const float TerrainSensitiveSegmentMatchHorizontalTolerance = 0.75f;
        private const float TerrainSensitiveSegmentMatchVerticalTolerance = 0.6f;
        private const float BridgeAccessVisualWidth = 1.35f;
        private const float BridgeDeckVisualWidth = BridgeAccessVisualWidth;
        private const float BridgeConcreteThickness = 0.3f;
        private const float BridgeSupportPillarWidth = 0.45f;
        private const float BridgeDeckEndOverhang = 0.75f;
        private const float BridgeGlassHeight = 1.2f;
        private const float BridgeGlassThickness = 0.05f;
        private const float BridgeWallHeight = 2.5f;
        private const float BridgeWallThickness = 0.08f;
        private const float BridgeWindowSillHeight = 0.72f;
        private const float BridgeWindowHeight = 1.28f;
        private const float BridgeWindowSpacing = 2.4f;
        private const float BridgeWallPostWidth = 0.12f;
        private const float BridgeWindowFrameInset = 0.03f;
        private const float BridgeRoofThickness = 0.14f;
        private const float BridgeRoofOverhang = 0f;
        private const float BridgeAccessFootExtension = 0f;
        private const float BridgeAccessFootGroundDrop = 0.55f;
        private const int BridgeAccessStairCount = 30;
        private const float BridgeAccessStepDepth = 0.24f;
        private const float BridgeAccessStepThickness = 0.035f;
        private const float BridgeFunctionalPadExitLead = 0.35f;
        private const float BridgeFunctionalLaneWalkOutDistance = 1.75f;
        private const float MaxBridgeFunctionalLaneSurfaceSnapDistance = 4f;
        private const float BridgeHiddenTunnelDepth = -6.0f;
        private const float BridgeFunctionalPadSurfaceLift = 0.006f;
        private const float BridgeAccessLandingCapLength = 0.58f;
        private const float BridgeAccessLandingCapThickness = 0.08f;
        private const float BridgeAccessSideTrimHeight = 0.08f;
        private const float BridgeAccessSideTrimOutset = 0.05f;
        private static readonly bool EnableBridgePathSegments = true;
        private static readonly bool EnableBridgeHiddenSubwayFallback = true;
        private const float SubwayEntranceVisualLength = 2.95f;
        private const float SubwayEntranceVisualWidth = 1.4f;
        private const float SubwayEntranceWallHeight = 0.55f;
        private const float SubwayEntranceDoorwayWidth = 0.6f;
        private const float SubwayEntranceDoorwayHeight = 4.5f;
        private const float SubwayEntranceWallThickness = 0.2f;
        private const float SubwayEntranceBackWallThickness = 0.2f;
        private const float SubwayEntranceStepPanelLength = 1.85f;
        private const float SubwayEntranceVisualGroundSink = 0.15f;
        private const float SubwayEntranceSlopeSurfaceLift = 0.2f;
        private const float SubwayEntrancePadSurfaceLift = 0.2f;
        private const float SubwayEntranceSnapStubMinLength = 0.4f;
        private const float SubwayEntranceStepNoseWidth = 0.035f;
        private const float SubwayEntranceCopingHeight = 0.075f;
        private const float SubwayEntranceCopingOverhang = 0.055f;
        private const float SubwayEntranceThresholdDepth = 0.18f;
        private const float SurfaceCrossingVisualWidth = 2.0f;
        private const float SurfaceStripeLength = SurfaceCrossingVisualWidth;
        private const float SurfaceStripeDepth = 0.4f;
        private const float SurfaceStripeGapDepth = 0.4f;
        private const float SurfaceMarkingLift = 0.01f;
        private const int CrossingStripeTextureSize = 64;
        private const float SurfaceVehicleLaneEdgeMargin = 0.05f;
        private const float SurfaceVehicleSpanMergeGap = 1.25f;
        private const float SignalVehicleSpanMergeGap = 0.35f;
        private const float VergeCrossingConnectorWidth = SurfaceCrossingVisualWidth;
        private const float VergeCrossingConnectorMinLength = 0.1f;
        private const float VergeCrossingConnectorThickness = 0.035f;
        private const float VergeCrossingConnectorLift = 0.018f;
        private const float PedestrianPillarHeight = 1.05f;
        private const float PedestrianPillarWidth = 0.16f;
        private const float SignalPoleHeight = 2.65f;
        private const float SignalPoleWidth = 0.16f;
        private const float SignalHeadSize = 0.42f;
        private const float SignalPoleGroundSink = 0.25f;
        private const float SignalStopLineOffsetFromCrossing = 0.3f;
        private const float SignalStopLineSideBalanceOffset = 0.3f;
        private const float SignalStopLineWidth = 0.2f;
        private const float SignalStopLineExtraClearanceFromVanillaZebra = 0.8f;
        private const float SignalCrossingMaxVisualDepth = 4.0f;
        private const float SignalCrossingVisualRoadHalfDepth = SurfaceCrossingVisualWidth * 0.5f;
        private const float VanillaSignalCrossingVisualRoadHalfDepth = 2.0f;
        private const float VanillaSignalCrossingFarSideStopLineExtra = 0.8f;
        private const float VanillaSignalCrossingAdjustedStopLineExtra = 0.5f;
        private const float VehicleSignalPoleHeight = 3.25f;
        private const float VehicleSignalPoleWidth = 0.14f;
        private const float VehicleSignalHeadWidth = 0.46f;
        private const float VehicleSignalHeadHeight = 0.78f;
        private const float VehicleSignalHeadDepth = 0.2f;
        private const float VehicleSignalLensYOffset = 0.24f;
        private const float SignalRoadEdgeSetback = 0.45f;
        private const float VehicleSignalRoadEdgeSetback = SignalRoadEdgeSetback;
        private const float VehicleSignalArmHeight = 3.15f;
        private const float VehicleSignalArmThickness = 0.1f;
        private const float VehicleSignalHeadDrop = 0.42f;
        private const float VehicleSignalHangerThickness = 0.05f;
        private const float SignalPedestrianDetectionRadius = 7f;
        private const float SignalPedestrianWaitCooldownSeconds = 30f;
        private const float SignalPedestrianRequestConfirmSeconds = 1f;
        private const float SignalPedestrianGreenSeconds = 5f;
        private const float SignalPedestrianClearanceAssumedWalkSpeed = 2.5f;
        private const float SignalPedestrianClearanceStartupBufferSeconds = 1.5f;
        private const float SignalPedestrianClearanceHardMaxSeconds = 18f;
        private const float SignalPedestrianClearanceMinSeconds = 1f;
        private const float SignalControllerStateRefreshSeconds = 1.0f;
        private const float SignalPedestrianScanSeconds = 1.25f;
        private const float SignalNearbyRoadSegmentSearchRadius = 18f;
        private const float SignalControlledSegmentAlignmentDot = 0.7f;
        private const float SignalPedestrianOnCrossingRadius = 2.4f;
        private const float SignalPedestrianWaitingZebraEdgeRadius = 2.4f;
        private const float SignalPedestrianWaitingBodySpeedSqr = 0.04f;
        private const float SignalPedestrianOnCrossingEndMarginMeters = 0.75f;
        private const float SignalPedestrianSpatialScanPadding = 2f;
        private const float SignalPedestrianExpandedWaitingLongitudinalOffset = 2f;
        private const float SignalPedestrianApproachLongitudinalDistance = 18f;
        private const float SignalPedestrianApproachLateralDistance = 8f;
        private const float SignalPedestrianTargetObservationDistance = 22f;
        private const float SignalExpandedWaitingRoadAlignmentDot = 0.96f;
        private const float SignalExpandedWaitingWideConflictSpan = 12f;
        private const float SignalPedestrianFullScanFallbackSeconds = 15f;
        private const int SignalPedestrianQueuedNearbyDemandThreshold = 8;
        private const int SignalPedestrianGridTraversalLimit = 65536;
        private const float SignalVehicleCrossingMovingRadius = 7.5f;
        private const float SignalVehicleMovingSpeedSqr = 0.25f;
        private const int SubwayEntranceStairCount = 8;
        private static readonly CrossingPathWorkOrder[] BuildBuffer = new CrossingPathWorkOrder[1024];
        private static readonly CrossingLandingConnectorWorkOrder[] ConnectorBuildBuffer = new CrossingLandingConnectorWorkOrder[2048];
        private static readonly CrossingLandingAccessAssetWorkOrder[] AccessBuildBuffer = new CrossingLandingAccessAssetWorkOrder[2048];
        private static readonly CrossingPlacementAsset[] SurfaceControlAssetBuffer = new CrossingPlacementAsset[4096];
        private static readonly List<ushort> BuiltSegments = new List<ushort>();
        private static readonly List<ushort> BuiltNodes = new List<ushort>();
        private static readonly Dictionary<ushort, CrossingPathWorkOrderKind> BuiltSegmentKinds = new Dictionary<ushort, CrossingPathWorkOrderKind>();
        private static readonly Dictionary<ushort, List<int>> BuiltSegmentAssets = new Dictionary<ushort, List<int>>();
        private static readonly Dictionary<ushort, int> BuiltSignalPathSegmentAssets = new Dictionary<ushort, int>();
        private static readonly Dictionary<ushort, CrossingPathWorkOrderKind> BuiltTerminalNodeKinds = new Dictionary<ushort, CrossingPathWorkOrderKind>();
        private static readonly List<BuiltBridgeDeckVisual> BuiltBridgeDecks = new List<BuiltBridgeDeckVisual>();
        private static readonly List<GameObject> BuiltBridgeConcreteObjects = new List<GameObject>();
        private static readonly Dictionary<string, ushort> BuiltBridgeAnchorNodes = new Dictionary<string, ushort>();
        private static readonly Dictionary<string, ushort> BuiltPathAnchorNodes = new Dictionary<string, ushort>();
        private static readonly HashSet<ushort> BuiltBridgeFallbackSegments = new HashSet<ushort>();
        private static readonly HashSet<string> BuiltSubwayEntranceVisualKeys = new HashSet<string>();
        private static readonly Dictionary<string, List<int>> BuiltSubwayEntranceVisualAssets = new Dictionary<string, List<int>>();
        private static readonly Dictionary<string, List<GameObject>> BuiltSubwayEntranceVisualObjects = new Dictionary<string, List<GameObject>>();
        private static readonly HashSet<string> BuiltSurfaceVisualKeys = new HashSet<string>();
        private static readonly List<BuiltSignalController> BuiltSignalControllers = new List<BuiltSignalController>();
        private static readonly List<CrossingPathWorkOrder> PendingSignalControlOrders = new List<CrossingPathWorkOrder>();
        private static readonly Dictionary<ushort, NetNode.Flags> BuiltSignalNodeSnapshots = new Dictionary<ushort, NetNode.Flags>();
        private static readonly Dictionary<ushort, NetSegment.Flags> BuiltSignalSegmentSnapshots = new Dictionary<ushort, NetSegment.Flags>();
        private static readonly List<SignalControllerStateRestore> PendingSignalControllerStateRestores = new List<SignalControllerStateRestore>();
        private static readonly CrossingPlacementAsset[] SignalRoadStateAssetBuffer = new CrossingPlacementAsset[2048];
        private static string _pendingSignalControllerStateRestoreReason = string.Empty;
        private static Material _bridgeConcreteMaterial;
        private static Material _bridgeGlassMaterial;
        private static Material _bridgeMetalMaterial;
        private static Material _bridgeAccessWallMaterial;
        private static Material _bridgeAccessStructureMaterial;
        private static Material _bridgeTrimMaterial;
        private static Material _subwayOpeningMaterial;
        private static Material _subwayEntranceWallMaterial;
        private static Material _subwayStepNoseMaterial;
        private static Material _crossingStripeMaterial;
        private static Texture2D _crossingStripeTexture;
        private static Material _signalPoleMaterial;
        private static Material _signalHeadMaterial;
        private static Material _signalLensMaterial;
        private static Material _signalRedLensMaterial;
        private static Material _signalAmberLensMaterial;
        private static Material _vergeCrossingMaterial;
        private static BuiltConnectorValidationSummary _lastValidationSummary = BuiltConnectorValidationSummary.Empty;
        private static int _activeBuildAssetId;

        public static bool HasBuiltPaths
        {
            get { return BuiltSegments.Count > 0 || BuiltNodes.Count > 0 || BuiltBridgeDecks.Count > 0 || BuiltBridgeConcreteObjects.Count > 0; }
        }

        public static int BuiltSegmentCount
        {
            get { return BuiltSegments.Count; }
        }

        public static BuiltConnectorValidationSummary LastValidationSummary
        {
            get { return _lastValidationSummary; }
        }

        public static BuiltConnectorValidationSummary RefreshBuiltConnectorValidationSummary()
        {
            _lastValidationSummary = ValidateBuiltConnectors();
            return _lastValidationSummary;
        }

        public static bool IsBuiltCrossingSegment(ushort segmentId)
        {
            return segmentId != 0 && BuiltSegments.Contains(segmentId);
        }

        public static int BuiltVisualObjectCount
        {
            get { return BuiltBridgeDecks.Count + BuiltBridgeConcreteObjects.Count; }
        }

        public static int CountBuiltOwnedItemsForAsset(int assetId)
        {
            if (assetId == 0)
                return 0;

            int count = 0;
            foreach (KeyValuePair<ushort, List<int>> entry in BuiltSegmentAssets)
            {
                if (entry.Value != null && entry.Value.Contains(assetId))
                    count++;
            }

            for (int i = 0; i < BuiltSignalControllers.Count; i++)
            {
                if (BuiltSignalControllers[i].AssetId == assetId)
                    count++;
            }

            for (int i = 0; i < BuiltBridgeDecks.Count; i++)
            {
                if (BuiltBridgeDecks[i].AssetId == assetId)
                    count++;
            }

            string suffix = "#" + assetId.ToString();
            for (int i = 0; i < BuiltBridgeConcreteObjects.Count; i++)
            {
                GameObject obj = BuiltBridgeConcreteObjects[i];
                if (obj != null && obj.name != null && obj.name.EndsWith(suffix))
                    count++;
            }

            string surfacePrefix = "surface:" + assetId.ToString() + ":";
            foreach (string key in BuiltSurfaceVisualKeys)
            {
                if (key != null && key.StartsWith(surfacePrefix))
                    count++;
            }

            foreach (KeyValuePair<string, List<int>> entry in BuiltSubwayEntranceVisualAssets)
            {
                if (entry.Value != null && entry.Value.Contains(assetId))
                    count++;
            }

            return count;
        }

        public static int CopySignalControllerDebugSnapshotsTo(SignalControllerDebugSnapshot[] buffer)
        {
            if (buffer == null)
                return 0;

            int count = Mathf.Min(buffer.Length, BuiltSignalControllers.Count);
            for (int i = 0; i < count; i++)
            {
                BuiltSignalController controller = BuiltSignalControllers[i];
                buffer[i] = BuildSignalControllerDebugSnapshot(ref controller);
            }

            return count;
        }

        public static bool TryGetSignalControllerDebugSnapshot(int assetId, out SignalControllerDebugSnapshot snapshot)
        {
            snapshot = default(SignalControllerDebugSnapshot);
            if (assetId == 0)
                return false;

            for (int i = 0; i < BuiltSignalControllers.Count; i++)
            {
                BuiltSignalController controller = BuiltSignalControllers[i];
                if (controller.AssetId != assetId)
                    continue;

                snapshot = BuildSignalControllerDebugSnapshot(ref controller);
                return true;
            }

            return false;
        }

        private static SignalControllerDebugSnapshot BuildSignalControllerDebugSnapshot(ref BuiltSignalController controller)
        {
            RoadBaseAI.TrafficLightState liveVehicleState;
            RoadBaseAI.TrafficLightState livePedestrianState;
            bool liveVehiclesFlag;
            bool livePedestriansFlag;
            bool hasLiveState = TryGetSignalControllerLiveState(
                ref controller,
                out liveVehicleState,
                out livePedestrianState,
                out liveVehiclesFlag,
                out livePedestriansFlag);
            return new SignalControllerDebugSnapshot(
                controller.AssetId,
                controller.NodeId,
                controller.Center,
                controller.FirstPosition,
                controller.SecondPosition,
                controller.Phase.ToString(),
                controller.PhaseTime,
                controller.CooldownTime,
                controller.HasPedestriansNear,
                controller.HasPedestriansWaitingAtEntrance,
                controller.HasPedestriansOnCrossing,
                controller.AppliedVehicleState,
                controller.AppliedPedestrianState,
                hasLiveState,
                liveVehicleState,
                livePedestrianState,
                liveVehiclesFlag,
                livePedestriansFlag,
                IsSignalControllerNodeJunction(ref controller),
                TrafficManagerPedestrianCrossingIntegration.HasActiveTimedSimulation(controller.NodeId));
        }

        public static int CopyBuiltBridgeDecksTo(BuiltBridgeDeckVisual[] buffer)
        {
            int count = Mathf.Min(buffer.Length, BuiltBridgeDecks.Count);
            for (int i = 0; i < count; i++)
                buffer[i] = BuiltBridgeDecks[i];

            return count;
        }

        public static int BuildPaths(out int skipped)
        {
            return BuildPaths(out skipped, 0, true);
        }

        public static int BuildPathsForAsset(int assetId, out int skipped)
        {
            if (assetId <= 0)
                return BuildPaths(out skipped);

            return BuildPaths(out skipped, assetId, false);
        }

        private static int BuildPaths(out int skipped, int assetIdFilter, bool clearExistingVisuals)
        {
            skipped = 0;
            int built = 0;
            _activeBuildAssetId = 0;
            bool filteredBuild = assetIdFilter > 0;
            if (clearExistingVisuals)
                ClearBuiltVisualObjects();

            PendingSignalControlOrders.Clear();
            int accessCount = CrossingLandingConnectorPlanner.CopyAccessAssetsTo(AccessBuildBuffer);
            int connectorCount = CrossingLandingConnectorPlanner.CopyWorkOrdersTo(ConnectorBuildBuffer);
            ApplySuppressedSurfaceCrossingFlags(true);
            int count = CrossingPathWorkOrderPlanner.CopyWorkOrdersTo(BuildBuffer);
            int max = Mathf.Min(count, BuildBuffer.Length);
            if (filteredBuild)
                QueueAllSignalControls(BuildBuffer, max);

            for (int i = 0; i < max; i++)
            {
                CrossingPathWorkOrder order = BuildBuffer[i];
                if (filteredBuild && order.AssetId != assetIdFilter)
                    continue;

                _activeBuildAssetId = order.AssetId;
                if (!ShouldBuild(order))
                {
                    skipped++;
                    continue;
                }

                if (order.Kind == CrossingPathWorkOrderKind.BridgePath)
                {
                    int bridgeBuilt = BuildBridgeDeck(order, accessCount, connectorCount);
                    if (bridgeBuilt == 0 && EnableBridgePathSegments)
                    {
                        skipped++;
                        continue;
                    }

                    built += bridgeBuilt;
                    continue;
                }

                ushort firstNode;
                ushort lastNode;
                ushort segment;
                Vector3 pathStart = order.Kind == CrossingPathWorkOrderKind.BridgePath
                    ? order.FirstBuildPosition
                    : order.FirstPosition;
                Vector3 pathEnd = order.Kind == CrossingPathWorkOrderKind.BridgePath
                    ? order.SecondBuildPosition
                    : order.SecondPosition;
                float startElevation = order.Kind == CrossingPathWorkOrderKind.BridgePath ? 0f : order.VerticalOffset;
                float endElevation = order.Kind == CrossingPathWorkOrderKind.BridgePath ? 0f : order.VerticalOffset;
                if (IsSegmentedSurfacePath(order.Kind))
                {
                    int surfaceBuilt;
                    int surfaceSkipped;
                    if (TryBuildSegmentedSurfacePath(order, pathStart, pathEnd, int.MaxValue, out surfaceBuilt, out surfaceSkipped))
                    {
                        built += surfaceBuilt;
                        skipped += surfaceSkipped;
                        continue;
                    }
                }

                Vector3 pathBuildOffset = GetSurfacePathBuildOffset(order.Prefab, pathStart, pathEnd);
                if (TryFindMatchingManagedSegment(order.Prefab, pathStart + pathBuildOffset + Vector3.up * startElevation, pathEnd + pathBuildOffset + Vector3.up * endElevation, out segment))
                {
                    AddSegment(segment);
                    BuiltSegmentKinds[segment] = order.Kind;
                    if (order.Kind == CrossingPathWorkOrderKind.SignalizedSurfacePath)
                        BuiltSignalPathSegmentAssets[segment] = order.AssetId;
                    RegisterBuiltPathAnchorNodesFromSegment(
                        segment,
                        GetPathAnchorPosition(order, true),
                        GetPathAnchorPosition(order, false),
                        order.FirstBuildPosition,
                        order.SecondBuildPosition);
                    AddSurfaceCrossingVisuals(order);
                    ApplyBuiltSurfaceCrossingControl(order);
                    QueueBuiltSignalControl(order);
                    skipped++;
                    Debug.Log("[PedestrianCrossingToolkit] Connector path already exists: asset="
                              + order.AssetId
                              + " kind="
                              + order.Kind
                              + " segment="
                              + segment
                              + " prefab="
                              + order.PrefabName);
                    continue;
                }

                ushort startNodeId = 0;
                ushort endNodeId = 0;
                if (order.Kind == CrossingPathWorkOrderKind.SubwayPath)
                {
                    TryGetReusablePathAnchorNode(GetPathAnchorPosition(order, true), out startNodeId);
                    TryGetReusablePathAnchorNode(GetPathAnchorPosition(order, false), out endNodeId);
                    if (startNodeId == endNodeId)
                    {
                        startNodeId = 0;
                        endNodeId = 0;
                    }
                }

                ToolBase.ToolErrors errors = order.Kind == CrossingPathWorkOrderKind.SubwayPath
                    ? CreateExactPath(order.Prefab, pathStart + pathBuildOffset, pathEnd + pathBuildOffset, startElevation, endElevation, out firstNode, out lastNode, out segment)
                    : CreatePath(order.Prefab, pathStart + pathBuildOffset, pathEnd + pathBuildOffset, startElevation, endElevation, startNodeId, endNodeId, out firstNode, out lastNode, out segment);
                if (errors != ToolBase.ToolErrors.None || segment == 0)
                {
                    skipped++;
                    Debug.Log("[PedestrianCrossingToolkit] Connector path build skipped: asset="
                              + order.AssetId
                              + " kind="
                              + order.Kind
                              + " errors="
                              + errors
                              + " prefab="
                              + order.PrefabName);
                    continue;
                }

                AddSegment(segment);
                BuiltSegmentKinds[segment] = order.Kind;
                if (order.Kind == CrossingPathWorkOrderKind.SignalizedSurfacePath)
                    BuiltSignalPathSegmentAssets[segment] = order.AssetId;
                AddNode(firstNode);
                AddNode(lastNode);
                RegisterBuiltPathAnchorNode(GetPathAnchorPosition(order, true), firstNode);
                RegisterBuiltPathAnchorNode(GetPathAnchorPosition(order, false), lastNode);
                if (order.Kind == CrossingPathWorkOrderKind.SurfacePath)
                {
                    AddTerminalNode(firstNode, order.Kind);
                    AddTerminalNode(lastNode, order.Kind);
                }
                AddSurfaceCrossingVisuals(order);
                ApplyBuiltSurfaceCrossingControl(order);
                QueueBuiltSignalControl(order);
                built++;
                Debug.Log("[PedestrianCrossingToolkit] Connector path built: asset="
                          + order.AssetId
                          + " kind="
                          + order.Kind
                          + " segment="
                          + segment
                          + " firstNode="
                          + firstNode
                          + " lastNode="
                          + lastNode
                          + " prefab="
                          + order.PrefabName
                          + " from="
                          + order.FirstBuildPosition
                          + " to="
                          + order.SecondBuildPosition);
            }

            int connectorBuilt = 0;
            int connectorMax = Mathf.Min(connectorCount, ConnectorBuildBuffer.Length);
            List<string> connectorKeys = new List<string>();
            for (int i = 0; i < connectorMax; i++)
            {
                CrossingLandingConnectorWorkOrder order = ConnectorBuildBuffer[i];
                if (filteredBuild && order.AssetId != assetIdFilter)
                    continue;

                _activeBuildAssetId = order.AssetId;
                if (IsBridgeLandingConnector(order))
                {
                    skipped++;
                    Debug.Log("[PedestrianCrossingToolkit] Bridge landing connector skipped: asset="
                              + order.AssetId
                              + " endpoint="
                              + order.EndpointName
                              + " access="
                              + order.AccessKind
                              + " reason=hidden-bridge-route-owns-connector");
                    continue;
                }

                if (!ShouldBuild(order))
                {
                    skipped++;
                    continue;
                }

                Vector3 connectorFrom = GetLandingConnectorBuildPosition(order, order.FromPosition);
                Vector3 connectorTo = GetLandingConnectorBuildPosition(order, order.ToPosition);
                string connectorKey = CrossingConnectorKey.Make(connectorFrom, connectorTo);
                if (connectorKeys.Contains(connectorKey))
                {
                    skipped++;
                    Debug.Log("[PedestrianCrossingToolkit] Landing connector duplicate skipped: asset="
                              + order.AssetId
                              + " endpoint="
                              + order.EndpointName
                              + " access="
                              + order.AccessKind
                              + " key="
                              + connectorKey);
                    continue;
                }

                ushort firstNode;
                ushort lastNode;
                ushort segment;
                ReleaseStaleLandingConnector(order, connectorFrom, connectorTo);
                bool foundExisting = IsSubwayLandingConnector(order)
                    ? TryFindMatchingTerrainSensitiveManagedSegment(order.Prefab, connectorFrom, connectorTo, out segment)
                    : TryFindMatchingManagedSegment(order.Prefab, connectorFrom, connectorTo, out segment);
                if (foundExisting)
                {
                    AddSegment(segment);
                    BuiltSegmentKinds[segment] = GetConnectorSegmentKind(order);
                    RegisterBuiltPathAnchorNode(connectorFrom, GetNearestSegmentNode(segment, connectorFrom));
                    RegisterBuiltPathAnchorNode(connectorTo, GetNearestSegmentNode(segment, connectorTo));
                    AddTerminalNode(GetNearestSegmentNode(segment, connectorTo), GetConnectorSegmentKind(order));
                    connectorKeys.Add(connectorKey);
                    skipped++;
                    Debug.Log("[PedestrianCrossingToolkit] Landing connector already exists: asset="
                              + order.AssetId
                              + " endpoint="
                              + order.EndpointName
                              + " access="
                              + order.AccessKind
                              + " segment="
                              + segment
                              + " prefab="
                              + order.PrefabName);
                    continue;
                }

                ToolBase.ToolErrors errors = CreatePath(order.Prefab, connectorFrom, connectorTo, 0f, 0f, out firstNode, out lastNode, out segment);
                if (errors != ToolBase.ToolErrors.None || segment == 0)
                {
                    skipped++;
                    Debug.Log("[PedestrianCrossingToolkit] Landing connector build skipped: asset="
                              + order.AssetId
                              + " endpoint="
                              + order.EndpointName
                              + " access="
                              + order.AccessKind
                              + " errors="
                              + errors
                              + " prefab="
                              + order.PrefabName);
                    continue;
                }

                AddSegment(segment);
                BuiltSegmentKinds[segment] = GetConnectorSegmentKind(order);
                AddNode(firstNode);
                AddNode(lastNode);
                RegisterBuiltPathAnchorNode(connectorFrom, firstNode);
                RegisterBuiltPathAnchorNode(connectorTo, lastNode);
                AddTerminalNode(lastNode, GetConnectorSegmentKind(order));
                connectorKeys.Add(connectorKey);
                built++;
                connectorBuilt++;
                Debug.Log("[PedestrianCrossingToolkit] Landing connector built: asset="
                          + order.AssetId
                          + " endpoint="
                          + order.EndpointName
                          + " access="
                          + order.AccessKind
                          + " segment="
                          + segment
                          + " firstNode="
                          + firstNode
                          + " lastNode="
                          + lastNode
                          + " prefab="
                          + order.PrefabName
                          + " from="
                          + connectorFrom
                          + " to="
                          + connectorTo);
            }

            int accessBuilt = 0;
            int accessMax = Mathf.Min(accessCount, AccessBuildBuffer.Length);
            for (int i = 0; i < accessMax; i++)
            {
                CrossingLandingAccessAssetWorkOrder order = AccessBuildBuffer[i];
                if (filteredBuild && order.AssetId != assetIdFilter)
                    continue;

                _activeBuildAssetId = order.AssetId;
                NetInfo prefab = GetAccessPathPrefab(order);
                float deckElevation = GetAccessDeckElevation(order);
                ushort firstNode;
                ushort lastNode;
                ushort segment;
                ushort deckNodeId = 0;
                ushort groundNodeId = 0;
                Vector3 groundPosition = GetAccessGroundPosition(order);
                TryGetBuiltPathAnchorNode(order.DeckPosition + Vector3.up * deckElevation, out deckNodeId);
                TryGetBuiltPathAnchorNode(groundPosition, out groundNodeId);
                if (groundNodeId == 0 && order.AssetKind == CrossingLandingAccessAssetKind.SubwayEntrance)
                    TryGetBuiltPathAnchorNode(order.Position, out groundNodeId);
                if (order.AssetKind == CrossingLandingAccessAssetKind.SubwayEntrance && groundNodeId == deckNodeId)
                    groundNodeId = 0;
                if (order.AssetKind == CrossingLandingAccessAssetKind.BridgeStairRampLanding)
                {
                    bool nonRoadBridgeAccess = RoadPlacementRules.IsNonRoadGradeSeparatedPlacementTarget(order.SegmentId);
                    if (!ShouldBuildAccess(order, prefab))
                    {
                        skipped++;
                        continue;
                    }

                    if (nonRoadBridgeAccess || !EnableBridgePathSegments)
                    {
                        AddAccessVisual(order);
                        built++;
                        accessBuilt++;
                        Debug.Log("[PedestrianCrossingToolkit] Bridge access path skipped: asset="
                                  + order.AssetId
                                  + " endpoint="
                                  + order.EndpointName
                                  + " access="
                                  + order.AccessKind
                                  + " reason="
                                  + (nonRoadBridgeAccess ? "non-road-visible-access-hidden-subway" : "visual-only-bridge"));
                        continue;
                    }
                }
                else if (order.AssetKind == CrossingLandingAccessAssetKind.SubwayEntrance)
                {
                    if (!ShouldBuildAccess(order, prefab))
                    {
                        skipped++;
                        continue;
                    }
                }
                else if (!ShouldBuildAccess(order, prefab))
                {
                    skipped++;
                    continue;
                }

                bool isSubwayEntranceAccess = order.AssetKind == CrossingLandingAccessAssetKind.SubwayEntrance;
                bool foundExistingAccess = isSubwayEntranceAccess
                    ? TryFindMatchingTerrainSensitiveManagedSegment(prefab, order.DeckPosition + Vector3.up * deckElevation, groundPosition, out segment)
                    : TryFindMatchingManagedSegment(prefab, order.DeckPosition + Vector3.up * deckElevation, groundPosition, out segment);
                if (foundExistingAccess)
                {
                    AddSegment(segment);
                    BuiltSegmentKinds[segment] = GetAccessSegmentKind(order);
                    if (isSubwayEntranceAccess)
                    {
                        ushort accessGroundNode = GetNearestSegmentNode(segment, groundPosition);
                        int snapStubBuilt = EnsureSubwayEntranceSnapStub(order, groundPosition, accessGroundNode);
                        built += snapStubBuilt;
                        accessBuilt += snapStubBuilt;
                    }

                    AddAccessVisual(order);
                    skipped++;
                    Debug.Log("[PedestrianCrossingToolkit] Access connector already exists: asset="
                              + order.AssetId
                              + " endpoint="
                              + order.EndpointName
                              + " access="
                              + order.AccessKind
                              + " segment="
                              + segment
                              + " prefab="
                              + prefab.name);
                    continue;
                }

                bool nonRoadAccess = RoadPlacementRules.IsNonRoadGradeSeparatedPlacementTarget(order.SegmentId);
                ToolBase.ToolErrors errors = isSubwayEntranceAccess || nonRoadAccess
                    ? CreateExactPath(prefab, order.DeckPosition, groundPosition, deckElevation, 0f, order.FacingDirection, deckNodeId, groundNodeId, out firstNode, out lastNode, out segment)
                    : CreatePath(prefab, order.DeckPosition, groundPosition, deckElevation, 0f, deckNodeId, groundNodeId, out firstNode, out lastNode, out segment);
                if (errors != ToolBase.ToolErrors.None || segment == 0)
                {
                    skipped++;
                    Debug.Log("[PedestrianCrossingToolkit] Access connector build skipped: asset="
                              + order.AssetId
                              + " endpoint="
                              + order.EndpointName
                              + " access="
                              + order.AccessKind
                              + " errors="
                              + errors
                              + " prefab="
                              + (prefab == null ? "none" : prefab.name));
                    continue;
                }

                AddSegment(segment);
                BuiltSegmentKinds[segment] = GetAccessSegmentKind(order);
                AddNode(firstNode);
                AddNode(lastNode);
                RegisterBuiltPathAnchorNode(groundPosition, lastNode);
                if (order.AssetKind == CrossingLandingAccessAssetKind.SubwayEntrance)
                    RegisterBuiltPathAnchorNode(order.Position, lastNode);
                if (isSubwayEntranceAccess)
                {
                    int snapStubBuilt = EnsureSubwayEntranceSnapStub(order, groundPosition, lastNode);
                    built += snapStubBuilt;
                    accessBuilt += snapStubBuilt;
                }

                AddAccessVisual(order);
                built++;
                accessBuilt++;
                Debug.Log("[PedestrianCrossingToolkit] Access connector built: asset="
                          + order.AssetId
                          + " endpoint="
                          + order.EndpointName
                          + " access="
                          + order.AccessKind
                          + " segment="
                          + segment
                          + " firstNode="
                          + firstNode
                          + " lastNode="
                          + lastNode
                          + " prefab="
                          + prefab.name
                          + " from="
                          + (order.DeckPosition + Vector3.up * deckElevation)
                          + " to="
                          + groundPosition);
            }

            ApplyPendingSignalControls();
            if (!filteredBuild)
                _lastValidationSummary = ValidateBuiltConnectors();

            Debug.Log("[PedestrianCrossingToolkit] Connector path build complete: built=" + built + " connectors=" + connectorBuilt + " access=" + accessBuilt + " skipped=" + skipped + " validation=" + _lastValidationSummary.ToLogString());
            _activeBuildAssetId = 0;
            return built;
        }

        private static int BuildBridgeDeck(CrossingPathWorkOrder order, int accessCount, int connectorCount)
        {
            CrossingPlacementAsset asset;
            if (!CrossingPlacementRegistry.TryGetAssetById(order.AssetId, out asset) || !asset.Plan.IsValid)
                return 0;

            NetInfo deckPrefab = PedestrianCrossingPrefabCatalog.InvisibleBridgePathPrefab
                                 ?? PedestrianCrossingPrefabCatalog.PedestrianPathPrefab
                                 ?? order.Prefab;
            string deckPrefabName = deckPrefab == null ? order.PrefabName : deckPrefab.name;

            return BuildStraightBridgeDeck(order, deckPrefab, deckPrefabName, accessCount, connectorCount);
        }

        private static int BuildStraightBridgeDeck(CrossingPathWorkOrder order, NetInfo deckPrefab, string deckPrefabName, int accessCount, int connectorCount)
        {
            bool nonRoadTarget = RoadPlacementRules.IsNonRoadGradeSeparatedPlacementTarget(order.SegmentId);
            int hiddenBuilt = EnableBridgeHiddenSubwayFallback
                ? (nonRoadTarget
                    ? BuildNonRoadBridgeHiddenSubway(order, accessCount)
                    : BuildBridgeHiddenTunnel(order, accessCount, connectorCount))
                : 0;

            if (nonRoadTarget || !EnableBridgePathSegments)
            {
                if (AddBuiltBridgeDeckVisual(order.AssetId, order.SegmentId, order.FirstBuildPosition, order.SecondBuildPosition))
                {
                    AddBridgeConcrete(order.AssetId, GetDeckOverhangStart(order.FirstPosition, order.SecondPosition), GetDeckOverhangEnd(order.FirstPosition, order.SecondPosition), BridgeDeckVisualWidth, CrossingVerticalProfile.BridgeDeckHeight, CrossingVerticalProfile.BridgeDeckHeight, BridgeConcreteThickness, "deck");
                    AddBridgeSupportPillar(order, order.FirstPosition);
                    AddBridgeSupportPillar(order, order.SecondPosition);
                }

                Debug.Log("[PedestrianCrossingToolkit] Bridge deck path skipped: asset="
                          + order.AssetId
                          + " segment="
                          + order.SegmentId
                          + " layout=Straight"
                          + " reason="
                          + (nonRoadTarget ? "non-road-visible-deck-hidden-subway" : "visual-only-bridge")
                          + " from="
                          + order.FirstBuildPosition
                          + " to="
                          + order.SecondBuildPosition);
                return 1 + hiddenBuilt;
            }

            ushort firstNode;
            ushort lastNode;
            ushort segment;
            if (TryFindMatchingManagedSegment(deckPrefab, order.FirstBuildPosition, order.SecondBuildPosition, out segment))
            {
                AddSegment(segment);
                BuiltSegmentKinds[segment] = CrossingPathWorkOrderKind.BridgePath;
                    RegisterBuiltPathAnchorNodesFromSegment(
                        segment,
                        GetPathAnchorPosition(order, true),
                        GetPathAnchorPosition(order, false),
                        order.FirstBuildPosition,
                        order.SecondBuildPosition);
                if (AddBuiltBridgeDeckVisual(order.AssetId, order.SegmentId, order.FirstBuildPosition, order.SecondBuildPosition))
                {
                    AddBridgeConcrete(order.AssetId, GetDeckOverhangStart(order.FirstPosition, order.SecondPosition), GetDeckOverhangEnd(order.FirstPosition, order.SecondPosition), BridgeDeckVisualWidth, CrossingVerticalProfile.BridgeDeckHeight, CrossingVerticalProfile.BridgeDeckHeight, BridgeConcreteThickness, "deck");
                    AddBridgeSupportPillar(order, order.FirstPosition);
                    AddBridgeSupportPillar(order, order.SecondPosition);
                }

                Debug.Log("[PedestrianCrossingToolkit] Bridge deck already exists: asset="
                          + order.AssetId
                          + " segment="
                          + order.SegmentId
                          + " builtSegment="
                          + segment
                          + " prefab="
                          + deckPrefabName);
                return hiddenBuilt;
            }

            ToolBase.ToolErrors errors = CreatePath(
                deckPrefab,
                order.FirstPosition,
                order.SecondPosition,
                order.VerticalOffset,
                order.VerticalOffset,
                out firstNode,
                out lastNode,
                out segment);
            if (errors != ToolBase.ToolErrors.None || segment == 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Bridge deck build skipped: asset="
                          + order.AssetId
                          + " segment="
                          + order.SegmentId
                          + " layout=Straight"
                          + " errors="
                          + errors
                          + " prefab="
                          + deckPrefabName);
                return hiddenBuilt;
            }

            AddSegment(segment);
            BuiltSegmentKinds[segment] = CrossingPathWorkOrderKind.BridgePath;
            AddNode(firstNode);
            AddNode(lastNode);
                RegisterBuiltPathAnchorNode(GetPathAnchorPosition(order, true), firstNode);
                RegisterBuiltPathAnchorNode(GetPathAnchorPosition(order, false), lastNode);
            RegisterBuiltBridgeAnchorNode(order.FirstPosition, firstNode);
            RegisterBuiltBridgeAnchorNode(order.SecondPosition, lastNode);
                AddBuiltBridgeDeckVisual(order.AssetId, order.SegmentId, order.FirstBuildPosition, order.SecondBuildPosition);
                AddBridgeConcrete(order.AssetId, GetDeckOverhangStart(order.FirstPosition, order.SecondPosition), GetDeckOverhangEnd(order.FirstPosition, order.SecondPosition), BridgeDeckVisualWidth, CrossingVerticalProfile.BridgeDeckHeight, CrossingVerticalProfile.BridgeDeckHeight, BridgeConcreteThickness, "deck");
                AddBridgeSupportPillar(order, order.FirstPosition);
                AddBridgeSupportPillar(order, order.SecondPosition);
                Debug.Log("[PedestrianCrossingToolkit] Bridge deck built: asset="
                          + order.AssetId
                      + " segment="
                      + order.SegmentId
                      + " layout=Straight"
                      + " builtSegment="
                      + segment
                      + " prefab="
                      + deckPrefabName
                      + " from="
                      + order.FirstBuildPosition
                      + " to="
                      + order.SecondBuildPosition
                      + " width="
                      + BridgeDeckVisualWidth.ToString("0.0"));
            return 1 + hiddenBuilt;
        }

        private struct BridgeFunctionalPadPoint
        {
            public Vector3 EntryPosition;
            public Vector3 HiddenPosition;
            public Vector3 LaneTargetPosition;
            public bool HasLaneTarget;
        }

        private static int BuildBridgeHiddenTunnel(CrossingPathWorkOrder order, int accessCount, int connectorCount)
        {
            BridgeFunctionalPadPoint firstPad;
            BridgeFunctionalPadPoint secondPad;
            if (!TryGetBridgeFunctionalPadPoints(order.AssetId, accessCount, connectorCount, out firstPad, out secondPad))
            {
                Debug.Log("[PedestrianCrossingToolkit] Bridge hidden tunnel skipped: asset="
                          + order.AssetId
                          + " segment="
                          + order.SegmentId
                          + " reason=missing-stair-foot-pads");
                return 0;
            }

            if (!firstPad.HasLaneTarget || !secondPad.HasLaneTarget)
            {
                Debug.Log("[PedestrianCrossingToolkit] Bridge hidden tunnel skipped: asset="
                          + order.AssetId
                          + " segment="
                          + order.SegmentId
                          + " reason=road-edge-landing-without-pedestrian-lane-targets"
                          + " firstHasLane="
                          + firstPad.HasLaneTarget
                          + " secondHasLane="
                          + secondPad.HasLaneTarget);
                return 0;
            }

            ushort firstNode;
            ushort lastNode;
            ushort segment;
            NetInfo prefab;
            int built;
            if (!TryBuildBridgeHiddenRun(order, firstPad, secondPad, out prefab, out firstNode, out lastNode, out segment, out built))
                return 0;

            string prefabName = prefab.name;
            ushort firstGroundNode;
            ushort secondGroundNode;
            built += BuildBridgeHiddenFootConnector(order.AssetId, "A", firstPad, out firstGroundNode);
            built += BuildBridgeHiddenFootConnector(order.AssetId, "B", secondPad, out secondGroundNode);
            built += BuildBridgeHiddenEntrance(order.AssetId, "A", prefab, prefabName, firstPad.HiddenPosition, firstNode, firstGroundNode);
            built += BuildBridgeHiddenEntrance(order.AssetId, "B", prefab, prefabName, secondPad.HiddenPosition, lastNode, secondGroundNode);

            Debug.Log("[PedestrianCrossingToolkit] Bridge hidden tunnel built: asset="
                      + order.AssetId
                      + " segment="
                      + order.SegmentId
                      + " builtSegment="
                      + segment
                      + " prefab="
                      + prefabName
                      + " from="
                      + firstPad.HiddenPosition
                      + " to="
                      + secondPad.HiddenPosition
                      + " firstLane="
                      + firstPad.LaneTargetPosition
                      + " secondLane="
                      + secondPad.LaneTargetPosition
                      + " reason=straight-between-stair-bottoms");
            return built;
        }

        private static int BuildNonRoadBridgeHiddenSubway(CrossingPathWorkOrder order, int accessCount)
        {
            BridgeFunctionalPadPoint firstPad;
            BridgeFunctionalPadPoint secondPad;
            if (!TryGetBridgeFunctionalPadPoints(order.AssetId, accessCount, 0, out firstPad, out secondPad))
            {
                Debug.Log("[PedestrianCrossingToolkit] Non-road bridge hidden subway skipped: asset="
                          + order.AssetId
                          + " segment="
                          + order.SegmentId
                          + " reason=missing-stair-foot-pads");
                return 0;
            }

            ushort firstNode;
            ushort lastNode;
            ushort segment;
            NetInfo prefab;
            int built;
            if (!TryBuildBridgeHiddenRun(order, firstPad, secondPad, out prefab, out firstNode, out lastNode, out segment, out built))
                return 0;

            string prefabName = prefab.name;
            built += BuildNonRoadBridgeHiddenEntrance(order.AssetId, "A", prefab, prefabName, firstPad.HiddenPosition, firstNode);
            built += BuildNonRoadBridgeHiddenEntrance(order.AssetId, "B", prefab, prefabName, secondPad.HiddenPosition, lastNode);

            Debug.Log("[PedestrianCrossingToolkit] Non-road bridge hidden subway built: asset="
                      + order.AssetId
                      + " segment="
                      + order.SegmentId
                      + " builtSegment="
                      + segment
                      + " prefab="
                      + prefabName
                      + " from="
                      + firstPad.HiddenPosition
                      + " to="
                      + secondPad.HiddenPosition
                      + " reason=terrain-safe-subway-style-route");
            return built;
        }

        private static bool TryBuildBridgeHiddenRun(CrossingPathWorkOrder order, BridgeFunctionalPadPoint firstPad, BridgeFunctionalPadPoint secondPad, out NetInfo prefab, out ushort firstNode, out ushort lastNode, out ushort segment, out int built)
        {
            prefab = GetBridgeHiddenRunPrefab(order);
            firstNode = 0;
            lastNode = 0;
            segment = 0;
            built = 0;
            if (prefab == null)
                return false;

            Vector3 firstBuildPosition = firstPad.HiddenPosition + Vector3.up * BridgeHiddenTunnelDepth;
            Vector3 secondBuildPosition = secondPad.HiddenPosition + Vector3.up * BridgeHiddenTunnelDepth;
            if (TryFindMatchingManagedSegment(prefab, firstBuildPosition, secondBuildPosition, out segment))
            {
                AddSegment(segment);
                BuiltSegmentKinds[segment] = CrossingPathWorkOrderKind.BridgePath;
                AddBridgeFallbackSegment(segment);
                firstNode = GetNearestSegmentNode(segment, firstBuildPosition);
                lastNode = GetNearestSegmentNode(segment, secondBuildPosition);
                RegisterBuiltPathAnchorNode(firstBuildPosition, firstNode);
                RegisterBuiltPathAnchorNode(secondBuildPosition, lastNode);
                Debug.Log("[PedestrianCrossingToolkit] Bridge hidden tunnel already exists: asset="
                          + order.AssetId
                          + " segment="
                          + order.SegmentId
                          + " builtSegment="
                          + segment
                          + " prefab="
                          + prefab.name);
                return true;
            }

            ToolBase.ToolErrors errors = CreateExactPath(prefab, firstPad.HiddenPosition, secondPad.HiddenPosition, BridgeHiddenTunnelDepth, BridgeHiddenTunnelDepth, out firstNode, out lastNode, out segment);
            if (errors != ToolBase.ToolErrors.None || segment == 0)
            {
                NetInfo fallbackPrefab = PedestrianCrossingPrefabCatalog.PedestrianTunnelPrefab;
                if (fallbackPrefab != null && fallbackPrefab != prefab)
                {
                    prefab = fallbackPrefab;
                    if (TryFindMatchingManagedSegment(prefab, firstBuildPosition, secondBuildPosition, out segment))
                    {
                        AddSegment(segment);
                        BuiltSegmentKinds[segment] = CrossingPathWorkOrderKind.BridgePath;
                        AddBridgeFallbackSegment(segment);
                        firstNode = GetNearestSegmentNode(segment, firstBuildPosition);
                        lastNode = GetNearestSegmentNode(segment, secondBuildPosition);
                        RegisterBuiltPathAnchorNode(firstBuildPosition, firstNode);
                        RegisterBuiltPathAnchorNode(secondBuildPosition, lastNode);
                        Debug.Log("[PedestrianCrossingToolkit] Bridge hidden tunnel already exists: asset="
                                  + order.AssetId
                                  + " segment="
                                  + order.SegmentId
                                  + " builtSegment="
                                  + segment
                                  + " prefab="
                                  + prefab.name);
                        return true;
                    }

                    errors = CreateExactPath(prefab, firstPad.HiddenPosition, secondPad.HiddenPosition, BridgeHiddenTunnelDepth, BridgeHiddenTunnelDepth, out firstNode, out lastNode, out segment);
                }
            }

            if (errors != ToolBase.ToolErrors.None || segment == 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Bridge hidden tunnel build skipped: asset="
                          + order.AssetId
                          + " segment="
                          + order.SegmentId
                          + " errors="
                          + errors
                          + " prefab="
                          + prefab.name);
                return false;
            }

            AddSegment(segment);
            BuiltSegmentKinds[segment] = CrossingPathWorkOrderKind.BridgePath;
            AddBridgeFallbackSegment(segment);
            AddNode(firstNode);
            AddNode(lastNode);
            RegisterBuiltPathAnchorNode(firstBuildPosition, firstNode);
            RegisterBuiltPathAnchorNode(secondBuildPosition, lastNode);
            built = 1;
            return true;
        }

        private static NetInfo GetBridgeHiddenRunPrefab(CrossingPathWorkOrder order)
        {
            return PedestrianCrossingPrefabCatalog.PedestrianTunnelPrefab
                   ?? PedestrianCrossingPrefabCatalog.InvisibleBridgePathPrefab
                   ?? PedestrianCrossingPrefabCatalog.PedestrianPathPrefab
                   ?? order.Prefab;
        }

        private static int BuildBridgeHiddenFootConnector(int assetId, string endpointName, BridgeFunctionalPadPoint pad, out ushort hiddenGroundNodeId)
        {
            hiddenGroundNodeId = 0;
            if (!pad.HasLaneTarget)
            {
                Debug.Log("[PedestrianCrossingToolkit] Bridge hidden foot connector skipped: asset="
                          + assetId
                          + " endpoint="
                          + endpointName
                          + " reason=missing-lane-target"
                          + " hidden="
                          + pad.HiddenPosition
                          + " entry="
                          + pad.EntryPosition);
                return 0;
            }

            NetInfo prefab = PedestrianCrossingPrefabCatalog.SurfaceCrossingPathPrefab
                             ?? PedestrianCrossingPrefabCatalog.PedestrianPathPrefab;
            if (prefab == null)
                return 0;

            ushort firstNode;
            ushort lastNode;
            ushort segment;
            if (TryFindMatchingManagedSegment(prefab, pad.HiddenPosition, pad.LaneTargetPosition, out segment))
            {
                AddSegment(segment);
                BuiltSegmentKinds[segment] = CrossingPathWorkOrderKind.BridgePath;
                AddBridgeFallbackSegment(segment);
                ushort hiddenNode = GetNearestSegmentNode(segment, pad.HiddenPosition);
                ushort laneNode = GetNearestSegmentNode(segment, pad.LaneTargetPosition);
                hiddenGroundNodeId = hiddenNode;
                RegisterBuiltPathAnchorNode(pad.HiddenPosition, hiddenNode);
                RegisterBuiltPathAnchorNode(pad.LaneTargetPosition, laneNode);
                AddTerminalNode(laneNode, CrossingPathWorkOrderKind.BridgePath);
                Debug.Log("[PedestrianCrossingToolkit] Bridge hidden foot connector already exists: asset="
                          + assetId
                          + " endpoint="
                          + endpointName
                          + " segment="
                          + segment
                          + " prefab="
                          + prefab.name
                          + " hidden="
                          + pad.HiddenPosition
                          + " lane="
                          + pad.LaneTargetPosition);
                return 0;
            }

            ToolBase.ToolErrors errors = CreatePath(prefab, pad.HiddenPosition, pad.LaneTargetPosition, 0f, 0f, out firstNode, out lastNode, out segment);
            if (errors != ToolBase.ToolErrors.None || segment == 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Bridge hidden foot connector build skipped: asset="
                          + assetId
                          + " endpoint="
                          + endpointName
                          + " errors="
                          + errors
                          + " prefab="
                          + prefab.name
                          + " hidden="
                          + pad.HiddenPosition
                          + " lane="
                          + pad.LaneTargetPosition);
                return 0;
            }

            AddSegment(segment);
            BuiltSegmentKinds[segment] = CrossingPathWorkOrderKind.BridgePath;
            AddBridgeFallbackSegment(segment);
            AddNode(firstNode);
            AddNode(lastNode);
            RegisterBuiltPathAnchorNode(pad.HiddenPosition, firstNode);
            RegisterBuiltPathAnchorNode(pad.LaneTargetPosition, lastNode);
            AddTerminalNode(lastNode, CrossingPathWorkOrderKind.BridgePath);
            hiddenGroundNodeId = firstNode;
            Debug.Log("[PedestrianCrossingToolkit] Bridge hidden foot connector built: asset="
                      + assetId
                      + " endpoint="
                      + endpointName
                      + " segment="
                      + segment
                      + " firstNode="
                      + firstNode
                      + " lastNode="
                      + lastNode
                      + " prefab="
                      + prefab.name
                      + " hidden="
                      + pad.HiddenPosition
                      + " lane="
                      + pad.LaneTargetPosition);
            return 1;
        }

        private static int BuildBridgeHiddenEntrance(int assetId, string endpointName, NetInfo prefab, string prefabName, Vector3 position, ushort tunnelNodeId, ushort groundNodeId)
        {
            if (prefab == null || tunnelNodeId == 0 || groundNodeId == 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Bridge hidden entrance skipped: asset="
                          + assetId
                          + " endpoint="
                          + endpointName
                          + " reason=missing-node"
                          + " tunnelNode="
                          + tunnelNodeId
                          + " groundNode="
                          + groundNodeId);
                return 0;
            }

            ushort firstNode;
            ushort lastNode;
            ushort segment;
            Vector3 hiddenBuildPosition = position + Vector3.up * BridgeHiddenTunnelDepth;
            if (TryFindMatchingManagedSegment(prefab, hiddenBuildPosition, position, out segment))
            {
                AddSegment(segment);
                BuiltSegmentKinds[segment] = CrossingPathWorkOrderKind.BridgePath;
                AddBridgeFallbackSegment(segment);
                ushort hiddenNode = GetNearestSegmentNode(segment, hiddenBuildPosition);
                ushort groundNode = GetNearestSegmentNode(segment, position);
                RegisterBuiltPathAnchorNode(hiddenBuildPosition, hiddenNode);
                RegisterBuiltPathAnchorNode(position, groundNode);
                Debug.Log("[PedestrianCrossingToolkit] Bridge hidden entrance already exists: asset="
                          + assetId
                          + " endpoint="
                          + endpointName
                          + " segment="
                          + segment
                          + " prefab="
                          + prefabName
                          + " hidden="
                          + hiddenBuildPosition
                          + " ground="
                          + position);
                return 0;
            }

            ToolBase.ToolErrors errors = CreateExactPath(prefab, position, position, BridgeHiddenTunnelDepth, 0f, Vector3.forward, tunnelNodeId, groundNodeId, out firstNode, out lastNode, out segment);
            if (errors != ToolBase.ToolErrors.None || segment == 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Bridge hidden entrance build skipped: asset="
                          + assetId
                          + " endpoint="
                          + endpointName
                          + " errors="
                          + errors
                          + " prefab="
                          + prefabName
                          + " hidden="
                          + hiddenBuildPosition
                          + " ground="
                          + position);
                return 0;
            }

            AddSegment(segment);
            BuiltSegmentKinds[segment] = CrossingPathWorkOrderKind.BridgePath;
            AddBridgeFallbackSegment(segment);
            AddNode(firstNode);
            AddNode(lastNode);
            RegisterBuiltPathAnchorNode(hiddenBuildPosition, firstNode);
            RegisterBuiltPathAnchorNode(position, lastNode);
            Debug.Log("[PedestrianCrossingToolkit] Bridge hidden entrance built: asset="
                      + assetId
                      + " endpoint="
                      + endpointName
                      + " segment="
                      + segment
                      + " firstNode="
                      + firstNode
                      + " lastNode="
                      + lastNode
                      + " prefab="
                      + prefabName
                      + " hidden="
                      + hiddenBuildPosition
                      + " ground="
                      + position);
            return 1;
        }

        private static int BuildNonRoadBridgeHiddenEntrance(int assetId, string endpointName, NetInfo prefab, string prefabName, Vector3 position, ushort tunnelNodeId)
        {
            if (prefab == null || tunnelNodeId == 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Non-road bridge hidden entrance skipped: asset="
                          + assetId
                          + " endpoint="
                          + endpointName
                          + " reason=missing-node"
                          + " tunnelNode="
                          + tunnelNodeId);
                return 0;
            }

            ushort firstNode;
            ushort lastNode;
            ushort segment;
            Vector3 hiddenBuildPosition = position + Vector3.up * BridgeHiddenTunnelDepth;
            if (TryFindMatchingManagedSegment(prefab, hiddenBuildPosition, position, out segment))
            {
                AddSegment(segment);
                BuiltSegmentKinds[segment] = CrossingPathWorkOrderKind.BridgePath;
                AddBridgeFallbackSegment(segment);
                ushort hiddenNode = GetNearestSegmentNode(segment, hiddenBuildPosition);
                ushort groundNode = GetNearestSegmentNode(segment, position);
                RegisterBuiltPathAnchorNode(hiddenBuildPosition, hiddenNode);
                RegisterBuiltPathAnchorNode(position, groundNode);
                Debug.Log("[PedestrianCrossingToolkit] Non-road bridge hidden entrance already exists: asset="
                          + assetId
                          + " endpoint="
                          + endpointName
                          + " segment="
                          + segment
                          + " prefab="
                          + prefabName
                          + " hidden="
                          + hiddenBuildPosition
                          + " ground="
                          + position);
                return 0;
            }

            ToolBase.ToolErrors errors = CreateExactPath(prefab, position, position, BridgeHiddenTunnelDepth, 0f, Vector3.forward, tunnelNodeId, 0, out firstNode, out lastNode, out segment);
            if (errors != ToolBase.ToolErrors.None || segment == 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Non-road bridge hidden entrance build skipped: asset="
                          + assetId
                          + " endpoint="
                          + endpointName
                          + " errors="
                          + errors
                          + " prefab="
                          + prefabName
                          + " hidden="
                          + hiddenBuildPosition
                          + " ground="
                          + position);
                return 0;
            }

            AddSegment(segment);
            BuiltSegmentKinds[segment] = CrossingPathWorkOrderKind.BridgePath;
            AddBridgeFallbackSegment(segment);
            AddNode(firstNode);
            AddNode(lastNode);
            RegisterBuiltPathAnchorNode(hiddenBuildPosition, firstNode);
            RegisterBuiltPathAnchorNode(position, lastNode);
            Debug.Log("[PedestrianCrossingToolkit] Non-road bridge hidden entrance built: asset="
                      + assetId
                      + " endpoint="
                      + endpointName
                      + " segment="
                      + segment
                      + " firstNode="
                      + firstNode
                      + " lastNode="
                      + lastNode
                      + " prefab="
                      + prefabName
                      + " hidden="
                      + hiddenBuildPosition
                      + " ground="
                      + position);
            return 1;
        }

        private static bool TryGetBridgeFunctionalPadPoints(int assetId, int accessCount, int connectorCount, out BridgeFunctionalPadPoint firstPad, out BridgeFunctionalPadPoint secondPad)
        {
            firstPad = new BridgeFunctionalPadPoint();
            secondPad = new BridgeFunctionalPadPoint();
            bool hasFirst = false;
            bool hasSecond = false;
            int max = Mathf.Min(accessCount, AccessBuildBuffer.Length);
            for (int i = 0; i < max; i++)
            {
                CrossingLandingAccessAssetWorkOrder order = AccessBuildBuffer[i];
                if (order.AssetId != assetId || order.AssetKind != CrossingLandingAccessAssetKind.BridgeStairRampLanding)
                    continue;

                BridgeFunctionalPadPoint point = GetBridgeFunctionalPadPoint(order, connectorCount);
                if (order.EndpointName == "A")
                {
                    firstPad = point;
                    hasFirst = true;
                }
                else if (order.EndpointName == "B")
                {
                    secondPad = point;
                    hasSecond = true;
                }
            }

            return hasFirst && hasSecond;
        }

        private static BridgeFunctionalPadPoint GetBridgeFunctionalPadPoint(CrossingLandingAccessAssetWorkOrder order, int connectorCount)
        {
            BridgeFunctionalPadPoint point = new BridgeFunctionalPadPoint();
            Vector3 direction;
            float runLength;
            bool hasAccessFrame = TryGetBridgeAccessVisualFrame(order, out direction, out runLength);
            Vector3 entryPosition = order.Position;
            if (RoadPlacementRules.IsNonRoadGradeSeparatedPlacementTarget(order.SegmentId) && hasAccessFrame)
                entryPosition = GetBridgeAccessVisualFootPosition(order, direction, runLength);

            point.EntryPosition = GetBridgeFunctionalSurfacePosition(order, entryPosition, entryPosition.y);
            point.HiddenPosition = point.EntryPosition;

            point.HasLaneTarget = hasAccessFrame && TryGetBridgeForwardLaneTarget(order, direction, out point.LaneTargetPosition);
            if (!point.HasLaneTarget)
                point.HasLaneTarget = TryGetBridgeLaneTarget(order.AssetId, order.EndpointName, connectorCount, out point.LaneTargetPosition);

            if (point.HasLaneTarget)
                point.LaneTargetPosition = GetBridgeFunctionalSurfacePosition(order, point.LaneTargetPosition, point.HiddenPosition.y);

            return point;
        }

        private static bool TryGetBridgeForwardLaneTarget(CrossingLandingAccessAssetWorkOrder order, Vector3 direction, out Vector3 target)
        {
            target = Vector3.zero;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return false;

            direction.Normalize();
            Vector3 requested = order.Position + direction * BridgeFunctionalLaneWalkOutDistance;
            if (!RoadPavementAnchorResolver.TryGetPedestrianLaneSurfacePosition(
                    order.SegmentId,
                    requested,
                    MaxBridgeFunctionalLaneSurfaceSnapDistance,
                    out target))
            {
                return false;
            }

            Vector3 targetDirection = target - order.Position;
            targetDirection.y = 0f;
            if (targetDirection.sqrMagnitude <= 0.25f)
                return false;

            targetDirection.Normalize();
            return Vector3.Dot(direction, targetDirection) >= 0.3f;
        }

        private static bool TryGetBridgeLaneTarget(int assetId, string endpointName, int connectorCount, out Vector3 target)
        {
            target = Vector3.zero;
            int max = Mathf.Min(connectorCount, ConnectorBuildBuffer.Length);
            for (int i = 0; i < max; i++)
            {
                CrossingLandingConnectorWorkOrder order = ConnectorBuildBuffer[i];
                if (!IsBridgeLandingConnector(order) || order.AssetId != assetId || order.EndpointName != endpointName)
                    continue;

                target = order.ToPosition;
                return true;
            }

            return false;
        }

        private static Vector3 GetBridgeFunctionalSurfacePosition(CrossingLandingAccessAssetWorkOrder order, Vector3 position, float fallbackHeight)
        {
            position.y = GetBridgeFunctionalPavementHeight(order, position, fallbackHeight) + BridgeFunctionalPadSurfaceLift;
            return position;
        }

        private static float GetBridgeFunctionalPavementHeight(CrossingLandingAccessAssetWorkOrder order, Vector3 position, float fallbackHeight)
        {
            float height = fallbackHeight;
            NetManager netManager = NetManager.instance;
            if (order.ConnectorTargetKind == CrossingLandingConnectorTargetKind.PedestrianLane
                && netManager != null
                && order.SegmentId != 0
                && order.SegmentId < netManager.m_segments.m_size)
            {
                ref NetSegment segment = ref netManager.m_segments.m_buffer[order.SegmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) != 0)
                    height = segment.GetClosestPosition(position).y;
            }

            TerrainManager terrainManager = TerrainManager.instance;
            if (terrainManager != null)
            {
                float terrainHeight = terrainManager.SampleRawHeightSmooth(position);
                height = order.ConnectorTargetKind == CrossingLandingConnectorTargetKind.PedestrianLane
                    ? Mathf.Max(height, terrainHeight)
                    : terrainHeight;
            }

            return height;
        }

        public static int ClearBuiltPaths(string reason)
        {
            NetManager netManager = NetManager.instance;
            int removed = 0;
            try
            {
                for (int i = BuiltSegments.Count - 1; i >= 0; i--)
                {
                    ushort segmentId = BuiltSegments[i];
                    if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                        continue;

                    ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                    if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                        continue;

                    removed += ReleaseSegmentAndUnusedNodes(netManager, segmentId) ? 1 : 0;
                }

                for (int i = BuiltNodes.Count - 1; i >= 0; i--)
                {
                    ushort nodeId = BuiltNodes[i];
                    if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                        continue;

                    ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
                    if ((node.m_flags & NetNode.Flags.Created) == 0)
                        continue;

                    if (ReleaseUnusedManagedNode(netManager, nodeId))
                        removed++;
                }

                RevertBuiltSignalControls();
                // Restore vanilla/TM:PE crossing permissions only after PCT-owned paths are gone.
                GradeSeparatedVanillaCrossingSuppression.Clear(reason);
            }
            finally
            {
                ClearBuiltPathTracking(false);
            }

            Debug.Log("[PedestrianCrossingToolkit] Connector path clear: reason=" + reason + " removed=" + removed);
            return removed;
        }

        public static int ForgetBuiltPathsForLevelUnload(string reason)
        {
            int trackedSegments = BuiltSegments.Count;
            int trackedNodes = BuiltNodes.Count;
            int trackedVisuals = BuiltVisualObjectCount;
            ClearBuiltPathTracking(true);
            int tracked = trackedSegments + trackedNodes;
            if (tracked > 0 || trackedVisuals > 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Connector path unload state forgotten without NetManager release: reason="
                          + reason
                          + " segments="
                          + trackedSegments
                          + " nodes="
                          + trackedNodes
                          + " visuals="
                          + trackedVisuals);
            }

            return tracked;
        }

        public static int RemoveBuiltPathsForAsset(int assetId, string reason)
        {
            if (assetId <= 0)
                return 0;

            NetManager netManager = NetManager.instance;
            if (netManager == null)
                return 0;

            int removed = 0;
            int shared = 0;
            ClearBuiltVisualObjectsForAsset(assetId);
            for (int i = BuiltSegments.Count - 1; i >= 0; i--)
            {
                ushort segmentId = BuiltSegments[i];
                List<int> assetIds;
                if (!BuiltSegmentAssets.TryGetValue(segmentId, out assetIds) || !assetIds.Contains(assetId))
                    continue;

                if (assetIds.Count > 1)
                {
                    assetIds.Remove(assetId);
                    shared++;
                    continue;
                }

                bool released = true;
                if (segmentId != 0 && segmentId < netManager.m_segments.m_size)
                {
                    ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                    if ((segment.m_flags & NetSegment.Flags.Created) != 0)
                    released = ReleaseSegmentAndUnusedNodes(netManager, segmentId);
                }

                if (!released)
                    continue;

                BuiltSegments.RemoveAt(i);
                BuiltSegmentAssets.Remove(segmentId);
                BuiltSegmentKinds.Remove(segmentId);
                BuiltSignalPathSegmentAssets.Remove(segmentId);
                removed++;
            }

            Debug.Log("[PedestrianCrossingToolkit] Removed built paths for asset: reason="
                      + reason
                      + " asset="
                      + assetId
                      + " removed="
                      + removed
                      + " sharedKept="
                      + shared);
            return removed;
        }

        public static int ClearSignalRoadStateForAsset(CrossingPlacementAsset asset, string reason)
        {
            if (asset.Id == 0 || asset.Placement.Mode != PedestrianToolMode.SignalCrossing)
                return 0;

            bool restoredSnapshot = RestoreSignalRoadStateSnapshot(asset.SignalRoadState, asset.Id, reason);
            int segmentEndCount;
            ushort nodeId;
            bool normalizedJoin = ClearSignalRoadStateForAssetJoin(asset, reason, out nodeId, out segmentEndCount);
            if (normalizedJoin || segmentEndCount > 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Normalized signal road join: reason="
                          + reason
                          + " asset="
                          + asset.Id
                          + " node="
                          + nodeId
                          + " segmentEnds="
                          + segmentEndCount
                          + " restoredSnapshot="
                          + restoredSnapshot);
            }

            if (restoredSnapshot || normalizedJoin)
                return 1;

            Debug.Log("[PedestrianCrossingToolkit] Signal road state restore skipped: reason="
                      + reason
                      + " asset="
                      + asset.Id
                      + " reason=no-captured-snapshot");
            return 0;
        }

        private static bool ClearSignalRoadStateForAssetJoin(CrossingPlacementAsset asset, string reason, out ushort nodeId, out int segmentEndCount)
        {
            segmentEndCount = 0;
            if (!TryGetSignalRoadStateCleanupNode(asset, out nodeId))
                return false;

            NetManager netManager = NetManager.instance;
            if (netManager == null)
                return false;

            return ClearSignalRoadStateForNode(netManager, nodeId, ref segmentEndCount);
        }

        private static bool TryGetSignalRoadStateCleanupNode(CrossingPlacementAsset asset, out ushort nodeId)
        {
            if (asset.SignalRoadState.HasSnapshot && asset.SignalRoadState.NodeId != 0)
            {
                nodeId = asset.SignalRoadState.NodeId;
                return true;
            }

            nodeId = asset.Plan.TargetNodeId != 0 ? asset.Plan.TargetNodeId : asset.Placement.TargetNodeId;
            return nodeId != 0;
        }

        private static bool ClearSignalRoadStateForNode(NetManager netManager, ushort nodeId, ref int segmentEndCount)
        {
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0 || node.CountSegments() != 2)
                return false;

            NetNode.Flags beforeNodeFlags = node.m_flags;
            node.m_flags &= ~NetNode.Flags.TrafficLights;
            node.m_flags &= ~NetNode.Flags.CustomTrafficLights;
            node.m_flags &= ~NetNode.Flags.Junction;
            node.m_flags |= NetNode.Flags.Middle;

            int attachedSegmentCount = node.CountSegments();
            uint frame = GetCurrentSimulationFrame();
            for (int i = 0; i < attachedSegmentCount; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || !HasVehicleLane(segment.Info))
                    continue;

                bool startNode;
                NetSegment.Flags crossingFlag;
                NetSegment.Flags trafficFlag;
                if (segment.m_startNode == nodeId)
                {
                    startNode = true;
                    crossingFlag = NetSegment.Flags.CrossingStart;
                    trafficFlag = NetSegment.Flags.TrafficStart;
                }
                else if (segment.m_endNode == nodeId)
                {
                    startNode = false;
                    crossingFlag = NetSegment.Flags.CrossingEnd;
                    trafficFlag = NetSegment.Flags.TrafficEnd;
                }
                else
                    continue;

                segment.m_flags &= ~crossingFlag;
                segment.m_flags &= ~trafficFlag;
                RoadBaseAI.SetTrafficLightState(
                    nodeId,
                    ref segment,
                    frame,
                    RoadBaseAI.TrafficLightState.Green,
                    RoadBaseAI.TrafficLightState.Red,
                    false,
                    false);
                RoadBaseAI.SetTrafficLightState(
                    nodeId,
                    ref segment,
                    frame + 256u,
                    RoadBaseAI.TrafficLightState.Green,
                    RoadBaseAI.TrafficLightState.Red,
                    false,
                    false);
                TrafficManagerPedestrianCrossingIntegration.SetPedestrianCrossingAllowed(segmentId, startNode, false);
                TrafficManagerPedestrianCrossingIntegration.ClearManagedSignalLightState(nodeId, segmentId, startNode);
                netManager.UpdateSegmentFlags(segmentId);
                netManager.UpdateSegmentRenderer(segmentId, true);
                segmentEndCount++;
            }

            if (beforeNodeFlags != node.m_flags)
            {
                netManager.UpdateNodeFlags(nodeId);
                netManager.UpdateNodeRenderer(nodeId, true);
            }

            return true;
        }

        public static int RemoveSignalControllerForAsset(int assetId, string reason)
        {
            if (assetId <= 0)
                return 0;

            int removedControllers = 0;
            int removedOrders = 0;
            int removedRestores = 0;
            for (int i = BuiltSignalControllers.Count - 1; i >= 0; i--)
            {
                if (BuiltSignalControllers[i].AssetId != assetId)
                    continue;

                BuiltSignalControllers.RemoveAt(i);
                removedControllers++;
            }

            for (int i = PendingSignalControlOrders.Count - 1; i >= 0; i--)
            {
                if (PendingSignalControlOrders[i].AssetId != assetId)
                    continue;

                PendingSignalControlOrders.RemoveAt(i);
                removedOrders++;
            }

            for (int i = PendingSignalControllerStateRestores.Count - 1; i >= 0; i--)
            {
                if (PendingSignalControllerStateRestores[i].AssetId != assetId)
                    continue;

                PendingSignalControllerStateRestores.RemoveAt(i);
                removedRestores++;
            }

            if (removedControllers > 0 || removedOrders > 0 || removedRestores > 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Removed signal controller for asset: reason="
                          + reason
                          + " asset="
                          + assetId
                          + " controllers="
                          + removedControllers
                          + " pendingOrders="
                          + removedOrders
                          + " pendingRestores="
                          + removedRestores);
            }

            return removedControllers;
        }

        public static SignalRoadStateSnapshot CaptureSignalRoadState(CrossingPlacementRecord placement, CrossingPlacementPlan plan)
        {
            if (placement.Mode != PedestrianToolMode.SignalCrossing)
                return SignalRoadStateSnapshot.Empty;

            ushort nodeId = plan.TargetNodeId != 0 ? plan.TargetNodeId : placement.TargetNodeId;
            if (nodeId == 0)
                return SignalRoadStateSnapshot.Empty;

            NetManager netManager = NetManager.instance;
            if (netManager == null || nodeId >= netManager.m_nodes.m_size)
                return SignalRoadStateSnapshot.Empty;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return SignalRoadStateSnapshot.Empty;

            List<SignalRoadSegmentState> segments = new List<SignalRoadSegmentState>();
            uint frame = GetCurrentSimulationFrame();
            int segmentCount = node.CountSegments();
            for (int i = 0; i < segmentCount; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || !HasVehicleLane(segment.Info))
                    continue;

                bool startNode;
                if (segment.m_startNode == nodeId)
                    startNode = true;
                else if (segment.m_endNode == nodeId)
                    startNode = false;
                else
                    continue;

                RoadBaseAI.TrafficLightState vehicleState;
                RoadBaseAI.TrafficLightState pedestrianState;
                bool vehicles;
                bool pedestrians;
                RoadBaseAI.GetTrafficLightState(
                    nodeId,
                    ref segment,
                    frame,
                    out vehicleState,
                    out pedestrianState,
                    out vehicles,
                    out pedestrians);

                segments.Add(new SignalRoadSegmentState(segmentId, startNode, segment.m_flags, vehicleState, pedestrianState, vehicles, pedestrians));
            }

            return new SignalRoadStateSnapshot(true, nodeId, node.m_flags, segments.ToArray());
        }

        private static BuiltConnectorValidationSummary ValidateBuiltConnectors()
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null)
                return BuiltConnectorValidationSummary.Empty;

            int created = 0;
            int pedestrian = 0;
            int isolatedSurface = 0;
            int isolatedAccess = 0;
            int unattachedTerminal = 0;
            int unattachedSurfaceTerminal = 0;
            int unattachedAccessTerminal = 0;
            HashSet<ushort> nodeIds = new HashSet<ushort>();
            Dictionary<ushort, CrossingPathWorkOrderKind> nodeKinds = new Dictionary<ushort, CrossingPathWorkOrderKind>();
            for (int i = 0; i < BuiltSegments.Count; i++)
            {
                ushort segmentId = BuiltSegments[i];
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                    continue;

                created++;
                if (HasPedestrianLane(segment.Info))
                    pedestrian++;

                CrossingPathWorkOrderKind kind;
                if (!BuiltSegmentKinds.TryGetValue(segmentId, out kind))
                    kind = CrossingPathWorkOrderKind.SurfacePath;
                AddCreatedNode(nodeIds, netManager, segment.m_startNode);
                AddCreatedNode(nodeIds, netManager, segment.m_endNode);
                AddNodeKind(nodeKinds, segment.m_startNode, kind);
                AddNodeKind(nodeKinds, segment.m_endNode, kind);
            }

            int connected = 0;
            int isolated = 0;
            foreach (ushort nodeId in nodeIds)
            {
                ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
                if (node.CountSegments() > 1)
                    connected++;
                else
                {
                    isolated++;
                    CrossingPathWorkOrderKind kind;
                    if (nodeKinds.TryGetValue(nodeId, out kind)
                        && (kind == CrossingPathWorkOrderKind.SubwayPath || kind == CrossingPathWorkOrderKind.BridgePath))
                    {
                        isolatedAccess++;
                    }
                    else
                    {
                        isolatedSurface++;
                    }

                    CrossingPathWorkOrderKind terminalKind;
                    if (BuiltTerminalNodeKinds.TryGetValue(nodeId, out terminalKind))
                    {
                        unattachedTerminal++;
                        if (terminalKind == CrossingPathWorkOrderKind.SubwayPath || terminalKind == CrossingPathWorkOrderKind.BridgePath)
                            unattachedAccessTerminal++;
                        else
                            unattachedSurfaceTerminal++;
                    }
                }
            }

            return new BuiltConnectorValidationSummary(BuiltSegments.Count, created, pedestrian, nodeIds.Count, connected, isolated, isolatedSurface, isolatedAccess, unattachedTerminal, unattachedSurfaceTerminal, unattachedAccessTerminal);
        }

        private static void AddCreatedNode(HashSet<ushort> nodeIds, NetManager netManager, ushort nodeId)
        {
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return;

            nodeIds.Add(nodeId);
        }

        private static void AddNodeKind(Dictionary<ushort, CrossingPathWorkOrderKind> nodeKinds, ushort nodeId, CrossingPathWorkOrderKind kind)
        {
            if (nodeId == 0)
                return;

            CrossingPathWorkOrderKind existing;
            if (nodeKinds.TryGetValue(nodeId, out existing)
                && (existing == CrossingPathWorkOrderKind.SubwayPath || existing == CrossingPathWorkOrderKind.BridgePath))
            {
                return;
            }

            nodeKinds[nodeId] = kind;
        }

        private static bool HasPedestrianLane(NetInfo info)
        {
            if (info == null || info.m_lanes == null)
                return false;

            for (int i = 0; i < info.m_lanes.Length; i++)
            {
                NetInfo.Lane lane = info.m_lanes[i];
                if (lane != null && (lane.m_laneType & NetInfo.LaneType.Pedestrian) != 0)
                    return true;
            }

            return false;
        }

        private static bool TryFindMatchingManagedSegment(NetInfo prefab, Vector3 expectedStart, Vector3 expectedEnd, out ushort matchingSegmentId)
        {
            matchingSegmentId = 0;
            if (prefab == null)
                return false;

            NetManager netManager = NetManager.instance;
            for (ushort segmentId = 1; segmentId < netManager.m_segments.m_size; segmentId++)
            {
                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info != prefab)
                    continue;

                ushort startNodeId = segment.m_startNode;
                ushort endNodeId = segment.m_endNode;
                if (startNodeId == 0 || endNodeId == 0 || startNodeId >= netManager.m_nodes.m_size || endNodeId >= netManager.m_nodes.m_size)
                    continue;

                Vector3 start = netManager.m_nodes.m_buffer[startNodeId].m_position;
                Vector3 end = netManager.m_nodes.m_buffer[endNodeId].m_position;
                bool sameDirection = IsNearExpectedPoint(start, expectedStart)
                                     && IsNearExpectedPoint(end, expectedEnd);
                bool oppositeDirection = IsNearExpectedPoint(start, expectedEnd)
                                         && IsNearExpectedPoint(end, expectedStart);
                if (!sameDirection && !oppositeDirection)
                    continue;

                matchingSegmentId = segmentId;
                return true;
            }

            return false;
        }

        private static bool TryFindMatchingTerrainSensitiveManagedSegment(NetInfo prefab, Vector3 expectedStart, Vector3 expectedEnd, out ushort matchingSegmentId)
        {
            matchingSegmentId = 0;
            if (prefab == null)
                return false;

            NetManager netManager = NetManager.instance;
            for (ushort segmentId = 1; segmentId < netManager.m_segments.m_size; segmentId++)
            {
                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info != prefab)
                    continue;

                ushort startNodeId = segment.m_startNode;
                ushort endNodeId = segment.m_endNode;
                if (startNodeId == 0 || endNodeId == 0 || startNodeId >= netManager.m_nodes.m_size || endNodeId >= netManager.m_nodes.m_size)
                    continue;

                Vector3 start = netManager.m_nodes.m_buffer[startNodeId].m_position;
                Vector3 end = netManager.m_nodes.m_buffer[endNodeId].m_position;
                bool sameDirection = IsNearTerrainSensitivePoint(start, expectedStart)
                                     && IsNearTerrainSensitivePoint(end, expectedEnd);
                bool oppositeDirection = IsNearTerrainSensitivePoint(start, expectedEnd)
                                         && IsNearTerrainSensitivePoint(end, expectedStart);
                if (!sameDirection && !oppositeDirection)
                    continue;

                matchingSegmentId = segmentId;
                return true;
            }

            return false;
        }

        private static bool IsManagedPathPrefab(NetInfo info)
        {
            if (info == null)
                return false;

            if (info == PedestrianCrossingPrefabCatalog.PedestrianPathPrefab
                || info == PedestrianCrossingPrefabCatalog.PedestrianBridgePrefab
                || info == PedestrianCrossingPrefabCatalog.PedestrianTunnelPrefab)
            {
                return true;
            }

            string aiName = info.m_netAI == null ? string.Empty : info.m_netAI.GetType().Name;
            string prefabName = info.name ?? string.Empty;
            return aiName.IndexOf("Pedestrian", StringComparison.OrdinalIgnoreCase) >= 0
                   && prefabName.IndexOf("Pedestrian", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsNearExpectedPoint(Vector3 actual, Vector3 expected)
        {
            return HorizontalDistance(actual, expected) <= ManagedSegmentMatchTolerance;
        }

        private static bool IsNearTerrainSensitivePoint(Vector3 actual, Vector3 expected)
        {
            return HorizontalDistance(actual, expected) <= TerrainSensitiveSegmentMatchHorizontalTolerance
                   && Mathf.Abs(actual.y - expected.y) <= TerrainSensitiveSegmentMatchVerticalTolerance;
        }

        private static bool IsNearLandingConnectorHorizontalPoint(Vector3 actual, Vector3 expected)
        {
            return HorizontalDistance(actual, expected) <= TerrainSensitiveSegmentMatchHorizontalTolerance;
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            float dx = first.x - second.x;
            float dz = first.z - second.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float FlatDistance(Vector3 first, Vector3 second)
        {
            return Mathf.Sqrt(DistanceSqr2D(first, second));
        }

        private static float DistanceSqr2D(Vector3 first, Vector3 second)
        {
            float dx = first.x - second.x;
            float dz = first.z - second.z;
            return (dx * dx) + (dz * dz);
        }

        private static float DistanceSqrToSegment2D(Vector3 point, Vector3 first, Vector3 second)
        {
            Vector2 p = new Vector2(point.x, point.z);
            Vector2 a = new Vector2(first.x, first.z);
            Vector2 b = new Vector2(second.x, second.z);
            Vector2 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            if (lengthSqr <= 0.001f)
                return (p - a).sqrMagnitude;

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSqr);
            Vector2 nearest = a + ab * t;
            return (p - nearest).sqrMagnitude;
        }

        private static bool ReleaseSegmentAndUnusedNodes(NetManager netManager, ushort segmentId)
        {
            if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                return false;

            if (!IsManagedPathPrefab(segment.Info))
            {
                Debug.LogWarning("[PedestrianCrossingToolkit] Refused to release non-managed segment: segment="
                                 + segmentId
                                 + " prefab="
                                 + (segment.Info == null ? "none" : segment.Info.name));
                return false;
            }

            ushort startNodeId = segment.m_startNode;
            ushort endNodeId = segment.m_endNode;
            string prefabName = segment.Info == null ? "none" : segment.Info.name;
            try
            {
                netManager.ReleaseSegment(segmentId, true);
                SafeReleaseUnusedManagedNode(netManager, startNodeId);
                SafeReleaseUnusedManagedNode(netManager, endNodeId);
                return true;
            }
            catch (Exception e)
            {
                Exception retryError;
                if (TryFinishPartiallyReleasedManagedSegment(netManager, segmentId, startNodeId, endNodeId, out retryError))
                {
                    Debug.LogWarning("[PedestrianCrossingToolkit] Recovered partially released managed segment: segment="
                                     + segmentId
                                     + " prefab="
                                     + prefabName
                                     + " firstError="
                                     + e.GetType().Name);
                    return true;
                }

                Debug.LogError("[PedestrianCrossingToolkit] Failed to release managed segment: segment="
                               + segmentId
                               + " prefab="
                               + prefabName
                               + " error="
                               + e
                               + (retryError == null ? string.Empty : " retryError=" + retryError));
                return false;
            }
        }

        private static bool TryFinishPartiallyReleasedManagedSegment(NetManager netManager, ushort segmentId, ushort startNodeId, ushort endNodeId, out Exception retryError)
        {
            retryError = null;
            if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            try
            {
                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                {
                    SafeReleaseUnusedManagedNode(netManager, startNodeId);
                    SafeReleaseUnusedManagedNode(netManager, endNodeId);
                    return true;
                }

                if ((segment.m_flags & NetSegment.Flags.Deleted) == 0)
                    return false;

                netManager.ReleaseSegment(segmentId, true);
                SafeReleaseUnusedManagedNode(netManager, startNodeId);
                SafeReleaseUnusedManagedNode(netManager, endNodeId);
                return true;
            }
            catch (Exception e)
            {
                retryError = e;
                return false;
            }
        }

        private static bool ReleaseUnusedManagedNode(NetManager netManager, ushort nodeId)
        {
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0 || node.CountSegments() != 0)
                return false;

            if (!IsManagedPathPrefab(node.Info))
            {
                Debug.LogWarning("[PedestrianCrossingToolkit] Refused to release non-managed node: node="
                                 + nodeId
                                 + " prefab="
                                 + (node.Info == null ? "none" : node.Info.name));
                return false;
            }

            netManager.ReleaseNode(nodeId);
            return true;
        }

        private static bool SafeReleaseUnusedManagedNode(NetManager netManager, ushort nodeId)
        {
            try
            {
                return ReleaseUnusedManagedNode(netManager, nodeId);
            }
            catch (Exception e)
            {
                Debug.LogError("[PedestrianCrossingToolkit] Failed to release unused managed node: node="
                               + nodeId
                               + " error="
                               + e);
                return false;
            }
        }

        private static bool ShouldBuild(CrossingPathWorkOrder order)
        {
            return order.HasPrefab
                   && order.Prefab != null
                   && Vector3.Distance(order.FirstBuildPosition, order.SecondBuildPosition) >= CrossingPathExecutionBoundary.MinExecutablePathLength;
        }

        private static bool ShouldBuild(CrossingLandingConnectorWorkOrder order)
        {
            float minDistance = IsSubwayAccessKind(order.AccessKind)
                ? 0.25f
                : 2f;

            return order.HasPrefab
                   && order.Prefab != null
                   && Vector3.Distance(order.FromPosition, order.ToPosition) >= minDistance;
        }

        private static Vector3 GetLandingConnectorBuildPosition(CrossingLandingConnectorWorkOrder order, Vector3 position)
        {
            if (!IsSubwayLandingConnector(order))
            {
                return position;
            }

            Vector3 resolved = position;
            resolved.y = GetSubwayLandingConnectorHeight(order, position, position.y) + SubwayEntrancePadSurfaceLift;
            return resolved;
        }

        private static void ReleaseStaleLandingConnector(CrossingLandingConnectorWorkOrder order, Vector3 connectorFrom, Vector3 connectorTo)
        {
            if (!IsSubwayLandingConnector(order))
            {
                return;
            }

            if (Vector3.Distance(order.FromPosition, connectorFrom) <= 0.05f
                && Vector3.Distance(order.ToPosition, connectorTo) <= 0.05f)
            {
                return;
            }

            NetManager netManager = NetManager.instance;
            for (ushort segmentId = 1; segmentId < netManager.m_segments.m_size; segmentId++)
            {
                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info != order.Prefab)
                    continue;

                ushort startNodeId = segment.m_startNode;
                ushort endNodeId = segment.m_endNode;
                if (startNodeId == 0 || endNodeId == 0 || startNodeId >= netManager.m_nodes.m_size || endNodeId >= netManager.m_nodes.m_size)
                    continue;

                Vector3 start = netManager.m_nodes.m_buffer[startNodeId].m_position;
                Vector3 end = netManager.m_nodes.m_buffer[endNodeId].m_position;
                bool sameHorizontal = IsNearLandingConnectorHorizontalPoint(start, connectorFrom)
                                      && IsNearLandingConnectorHorizontalPoint(end, connectorTo);
                bool oppositeHorizontal = IsNearLandingConnectorHorizontalPoint(start, connectorTo)
                                          && IsNearLandingConnectorHorizontalPoint(end, connectorFrom);
                if (!sameHorizontal && !oppositeHorizontal)
                    continue;

                bool sameCurrent = IsNearTerrainSensitivePoint(start, connectorFrom)
                                   && IsNearTerrainSensitivePoint(end, connectorTo);
                bool oppositeCurrent = IsNearTerrainSensitivePoint(start, connectorTo)
                                       && IsNearTerrainSensitivePoint(end, connectorFrom);
                if (sameCurrent || oppositeCurrent)
                    continue;

                ReleaseSegmentAndUnusedNodes(netManager, segmentId);
                Debug.Log("[PedestrianCrossingToolkit] Released stale subway landing connector: asset="
                          + order.AssetId
                          + " endpoint="
                          + order.EndpointName
                          + " segment="
                          + segmentId
                          + " oldFrom="
                          + start
                          + " oldTo="
                          + end
                          + " newFrom="
                          + connectorFrom
                          + " newTo="
                          + connectorTo);
            }
        }

        private static float GetSubwayLandingConnectorHeight(CrossingLandingConnectorWorkOrder order, Vector3 position, float fallbackHeight)
        {
            float height = fallbackHeight;
            float segmentHeight = height;
            bool hasSegmentHeight = order.TargetKind == CrossingLandingConnectorTargetKind.PedestrianLane
                                    && TryGetPedestrianLaneSurfaceHeight(order.SegmentId, position, out segmentHeight);
            if (hasSegmentHeight)
            {
                return segmentHeight;
            }

            TerrainManager terrainManager = TerrainManager.instance;
            if (terrainManager != null)
            {
                float terrainHeight = terrainManager.SampleRawHeightSmooth(position);
                height = terrainHeight;
            }

            return height;
        }

        private static bool IsSubwayAccessKind(CrossingLandingAccessKind accessKind)
        {
            return accessKind == CrossingLandingAccessKind.SubwayStraightEntrance
                   || accessKind == CrossingLandingAccessKind.SubwayUShapedEntrance
                   || accessKind == CrossingLandingAccessKind.SubwayZShapedEntrance
                   || accessKind == CrossingLandingAccessKind.SubwayXShapedEntrance
                   || accessKind == CrossingLandingAccessKind.SubwayYShapedEntrance;
        }

        private static bool IsSubwayLandingConnector(CrossingLandingConnectorWorkOrder order)
        {
            return IsSubwayAccessKind(order.AccessKind)
                   || order.CrossingKind == CrossingConnectivityLinkKind.SubwaySpan
                   || order.CrossingKind == CrossingConnectivityLinkKind.JunctionSubwayApproach;
        }

        private static bool IsBridgeLandingConnector(CrossingLandingConnectorWorkOrder order)
        {
            return order.CrossingKind == CrossingConnectivityLinkKind.PedestrianBridgeSpan
                   || order.CrossingKind == CrossingConnectivityLinkKind.JunctionBridgeApproach
                   || order.AccessKind == CrossingLandingAccessKind.BridgeStraightStairs
                   || order.AccessKind == CrossingLandingAccessKind.BridgeUShapedStairs
                   || order.AccessKind == CrossingLandingAccessKind.BridgeZShapedStairs
                   || order.AccessKind == CrossingLandingAccessKind.BridgeXShapedStairs
                   || order.AccessKind == CrossingLandingAccessKind.BridgeYShapedStairs;
        }

        private static Vector3 GetPathAnchorPosition(CrossingPathWorkOrder order, bool first)
        {
            if (order.Kind == CrossingPathWorkOrderKind.SubwayPath
                || order.Kind == CrossingPathWorkOrderKind.BridgePath)
                return first ? order.FirstBuildPosition : order.SecondBuildPosition;

            return first ? order.FirstPosition : order.SecondPosition;
        }

        private static bool ShouldBuildAccess(CrossingLandingAccessAssetWorkOrder order, NetInfo prefab)
        {
            return prefab != null
                   && Vector3.Distance(order.DeckPosition + Vector3.up * GetAccessDeckElevation(order), GetAccessGroundPosition(order)) >= GetMinAccessPathLength(order);
        }

        private static float GetMinAccessPathLength(CrossingLandingAccessAssetWorkOrder order)
        {
            return CrossingPathExecutionBoundary.MinExecutableAccessPathLength;
        }

        private static void AddAccessVisual(CrossingLandingAccessAssetWorkOrder order)
        {
            if (order.AssetKind == CrossingLandingAccessAssetKind.BridgeStairRampLanding)
            {
                AddBridgeAccessVisual(order);
                return;
            }

            if (order.AssetKind == CrossingLandingAccessAssetKind.SubwayEntrance)
                AddSubwayEntranceVisual(order);
        }

        private static void AddBridgeFallbackSegment(ushort segmentId)
        {
            if (segmentId != 0)
                BuiltBridgeFallbackSegments.Add(segmentId);
        }

        private static CrossingPathWorkOrderKind GetAccessSegmentKind(CrossingLandingAccessAssetWorkOrder order)
        {
            return order.AssetKind == CrossingLandingAccessAssetKind.BridgeStairRampLanding
                ? CrossingPathWorkOrderKind.BridgePath
                : CrossingPathWorkOrderKind.SubwayPath;
        }

        private static CrossingPathWorkOrderKind GetConnectorSegmentKind(CrossingLandingConnectorWorkOrder order)
        {
            return order.CrossingKind == CrossingConnectivityLinkKind.PedestrianBridgeSpan
                   || order.CrossingKind == CrossingConnectivityLinkKind.JunctionBridgeApproach
                ? CrossingPathWorkOrderKind.BridgePath
                : CrossingPathWorkOrderKind.SubwayPath;
        }

        public static float GetBridgeAccessVisualWidth()
        {
            return BridgeAccessVisualWidth;
        }

        private static NetInfo GetAccessPathPrefab(CrossingLandingAccessAssetWorkOrder order)
        {
            if (order.AssetKind == CrossingLandingAccessAssetKind.BridgeStairRampLanding)
            {
                return PedestrianCrossingPrefabCatalog.InvisibleBridgePathPrefab
                       ?? PedestrianCrossingPrefabCatalog.PedestrianPathPrefab;
            }

            return PedestrianCrossingPrefabCatalog.PedestrianTunnelPrefab
                   ?? PedestrianCrossingPrefabCatalog.PedestrianPathPrefab;
        }

        private static int EnsureSubwayEntranceSnapStub(CrossingLandingAccessAssetWorkOrder order, Vector3 groundPosition, ushort groundNodeId)
        {
            if (order.AssetKind != CrossingLandingAccessAssetKind.SubwayEntrance
                || order.ConnectorTargetKind == CrossingLandingConnectorTargetKind.PedestrianLane
                || groundNodeId == 0)
            {
                return 0;
            }

            NetInfo prefab = PedestrianCrossingPrefabCatalog.InvisibleBridgePathPrefab;
            if (prefab == null)
            {
                Debug.Log("[PedestrianCrossingToolkit] Subway entrance snap stub skipped: asset="
                          + order.AssetId
                          + " endpoint="
                          + order.EndpointName
                          + " reason=missing-invisible-pedestrian-connection-prefab");
                return 0;
            }

            Vector3 snapEnd = GetSubwayEntranceSnapStubEndPosition(order, groundPosition);
            if (HorizontalDistance(groundPosition, snapEnd) < SubwayEntranceSnapStubMinLength)
            {
                Debug.Log("[PedestrianCrossingToolkit] Subway entrance snap stub skipped: asset="
                          + order.AssetId
                          + " endpoint="
                          + order.EndpointName
                          + " reason=too-short"
                          + " from="
                          + groundPosition
                          + " to="
                          + snapEnd);
                return 0;
            }

            ushort segment;
            if (TryFindMatchingTerrainSensitiveManagedSegment(prefab, groundPosition, snapEnd, out segment))
            {
                AddSegment(segment);
                BuiltSegmentKinds[segment] = CrossingPathWorkOrderKind.SubwayPath;
                RegisterBuiltPathAnchorNode(groundPosition, GetNearestSegmentNode(segment, groundPosition));
                RegisterBuiltPathAnchorNode(snapEnd, GetNearestSegmentNode(segment, snapEnd));
                Debug.Log("[PedestrianCrossingToolkit] Subway entrance snap stub already exists: asset="
                          + order.AssetId
                          + " endpoint="
                          + order.EndpointName
                          + " segment="
                          + segment
                          + " prefab="
                          + prefab.name
                          + " from="
                          + groundPosition
                          + " to="
                          + snapEnd);
                return 0;
            }

            ushort firstNode;
            ushort lastNode;
            ToolBase.ToolErrors errors = CreateExactPath(
                prefab,
                groundPosition,
                snapEnd,
                0f,
                0f,
                order.FacingDirection,
                groundNodeId,
                0,
                out firstNode,
                out lastNode,
                out segment);
            if (errors != ToolBase.ToolErrors.None || segment == 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Subway entrance snap stub build skipped: asset="
                          + order.AssetId
                          + " endpoint="
                          + order.EndpointName
                          + " errors="
                          + errors
                          + " prefab="
                          + prefab.name
                          + " from="
                          + groundPosition
                          + " to="
                          + snapEnd);
                return 0;
            }

            AddSegment(segment);
            BuiltSegmentKinds[segment] = CrossingPathWorkOrderKind.SubwayPath;
            AddNode(firstNode);
            AddNode(lastNode);
            RegisterBuiltPathAnchorNode(groundPosition, firstNode);
            RegisterBuiltPathAnchorNode(snapEnd, lastNode);
            Debug.Log("[PedestrianCrossingToolkit] Subway entrance snap stub built: asset="
                      + order.AssetId
                      + " endpoint="
                      + order.EndpointName
                      + " segment="
                      + segment
                      + " firstNode="
                      + firstNode
                      + " lastNode="
                      + lastNode
                      + " prefab="
                      + prefab.name
                      + " from="
                      + groundPosition
                      + " to="
                      + snapEnd);
            return 1;
        }

        private static Vector3 GetSubwayEntranceSnapStubEndPosition(CrossingLandingAccessAssetWorkOrder order, Vector3 groundPosition)
        {
            Vector3 snapEnd = order.Position;
            snapEnd.y = GetSubwayEntrancePavementHeight(order, snapEnd, groundPosition.y) + SubwayEntrancePadSurfaceLift;
            if (HorizontalDistance(groundPosition, snapEnd) >= SubwayEntranceSnapStubMinLength)
                return snapEnd;

            Vector3 direction = order.FacingDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                direction = groundPosition - order.Position;

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                direction = Vector3.forward;
            else
                direction.Normalize();

            snapEnd = groundPosition - direction * CrossingLandingConnectorPlanner.SubwayEntranceFunctionalEntryOffset;
            snapEnd.y = GetSubwayEntrancePavementHeight(order, snapEnd, groundPosition.y) + SubwayEntrancePadSurfaceLift;
            return snapEnd;
        }

        private static float GetAccessDeckElevation(CrossingLandingAccessAssetWorkOrder order)
        {
            if (order.AssetKind == CrossingLandingAccessAssetKind.BridgeStairRampLanding)
                return CrossingVerticalProfile.BridgeDeckHeight;

            return CrossingVerticalProfile.SubwayTunnelDepth;
        }

        private static Vector3 GetDeckOverhangStart(Vector3 start, Vector3 end)
        {
            Vector3 direction = end - start;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return start;

            direction.Normalize();
            return start - direction * BridgeDeckEndOverhang;
        }

        private static Vector3 GetDeckOverhangEnd(Vector3 start, Vector3 end)
        {
            Vector3 direction = end - start;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return end;

            direction.Normalize();
            return end + direction * BridgeDeckEndOverhang;
        }

        private static void AddBridgeAccessVisual(CrossingLandingAccessAssetWorkOrder order)
        {
            Vector3 direction;
            float runLength;
            if (!TryGetBridgeAccessVisualFrame(order, out direction, out runLength))
                return;

            Vector3 deckEdge = order.DeckPosition + direction * (BridgeDeckVisualWidth * 0.5f);
            Vector3 deckBase = deckEdge + Vector3.up * CrossingVerticalProfile.BridgeDeckHeight;
            Vector3 footPath = GetBridgeAccessVisualFootPosition(order, direction, runLength);
            AddBridgeConcretePrism(order.AssetId, deckBase, footPath, BridgeAccessVisualWidth, BridgeConcreteThickness, "access");
            AddBridgeAccessStairs(order.AssetId, deckBase, footPath);
            AddBridgeGlassSides(order.AssetId, deckBase, footPath, BridgeAccessVisualWidth, BridgeConcreteThickness, "access");
            AddBridgeAccessFinishTrim(order.AssetId, deckBase, footPath, direction);
        }

        private static Vector3 GetBridgeAccessVisualFootPosition(CrossingLandingAccessAssetWorkOrder order, Vector3 direction, float runLength)
        {
            Vector3 footPath = order.DeckPosition + direction * Mathf.Max(BridgeDeckVisualWidth * 0.5f, runLength + BridgeAccessFootExtension);
            if (RoadPlacementRules.IsNonRoadGradeSeparatedPlacementTarget(order.SegmentId))
            {
                footPath.y = GetBridgeFunctionalPavementHeight(order, footPath, order.Position.y);
                return footPath;
            }

            footPath.y = order.Position.y - BridgeAccessFootGroundDrop;
            return footPath;
        }

        private static bool TryGetBridgeAccessVisualFrame(CrossingLandingAccessAssetWorkOrder order, out Vector3 direction, out float runLength)
        {
            direction = order.FacingDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
            {
                direction = order.Position - order.DeckPosition;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= 0.01f)
            {
                runLength = 0f;
                return false;
            }

            direction.Normalize();
            Vector3 offset = order.Position - order.DeckPosition;
            offset.y = 0f;
            float projected = Vector3.Dot(offset, direction);
            if (projected < 0f)
            {
                direction = -direction;
                projected = -projected;
            }

            runLength = projected > 0.01f ? projected : HorizontalDistance(order.Position, order.DeckPosition);
            return runLength > 0.01f;
        }

        private static void AddBridgeAccessStairs(int assetId, Vector3 deckBase, Vector3 foot)
        {
            Vector3 horizontal = deckBase - foot;
            horizontal.y = 0f;
            float horizontalLength = horizontal.magnitude;
            if (horizontalLength <= 0.01f)
                return;

            Vector3 upDirection = horizontal / horizontalLength;
            int steps = Mathf.Max(3, BridgeAccessStairCount);
            float depth = Mathf.Min(BridgeAccessStepDepth, horizontalLength / steps);
            Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, upDirection);
            for (int i = 0; i < steps; i++)
            {
                float t = (i + 0.5f) / steps;
                Vector3 center = Vector3.Lerp(foot, deckBase, t);
                center += upDirection * (depth * 0.18f);
                center.y += BridgeConcreteThickness + (BridgeAccessStepThickness * 0.5f);
                AddTrackedCube(
                    "PCT Bridge Stair tread #" + assetId,
                    center,
                    rotation,
                    new Vector3(BridgeAccessVisualWidth, BridgeAccessStepThickness, depth),
                    GetBridgeAccessStructureMaterial(),
                    true);
            }
        }

        private static void AddBridgeAccessFinishTrim(int assetId, Vector3 deckBase, Vector3 foot, Vector3 direction)
        {
            Vector3 flatDirection = direction;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude <= 0.01f)
            {
                flatDirection = deckBase - foot;
                flatDirection.y = 0f;
            }

            if (flatDirection.sqrMagnitude <= 0.01f)
                return;

            flatDirection.Normalize();
            Quaternion flatRotation = Quaternion.FromToRotation(Vector3.forward, flatDirection);
            float capWidth = BridgeAccessVisualWidth + 0.28f;
            AddBridgeShellCube(
                "PCT Bridge Metal access foot threshold #" + assetId,
                foot + flatDirection * (BridgeAccessLandingCapLength * 0.32f) + Vector3.up * (BridgeConcreteThickness + BridgeAccessLandingCapThickness * 0.5f),
                flatRotation,
                new Vector3(capWidth, BridgeAccessLandingCapThickness, BridgeAccessLandingCapLength),
                GetBridgeTrimMaterial());
            AddBridgeShellCube(
                "PCT Bridge Metal access deck threshold #" + assetId,
                deckBase - flatDirection * (BridgeAccessLandingCapLength * 0.32f) + Vector3.up * (BridgeConcreteThickness + BridgeAccessLandingCapThickness * 0.5f),
                flatRotation,
                new Vector3(capWidth, BridgeAccessLandingCapThickness, BridgeAccessLandingCapLength),
                GetBridgeTrimMaterial());

            Vector3 span = deckBase - foot;
            if (span.sqrMagnitude <= 0.01f)
                return;

            float length = span.magnitude;
            Vector3 slopeDirection = span / length;
            Vector3 side = new Vector3(-flatDirection.z, 0f, flatDirection.x);
            AddBridgeShellCube(
                "PCT Bridge Metal access side trim L #" + assetId,
                (deckBase + foot) * 0.5f + side * (BridgeAccessVisualWidth * 0.5f + BridgeAccessSideTrimOutset) + Vector3.up * (BridgeConcreteThickness + BridgeAccessSideTrimHeight),
                Quaternion.LookRotation(slopeDirection, Vector3.up),
                new Vector3(BridgeAccessSideTrimHeight, BridgeAccessSideTrimHeight, length),
                GetBridgeTrimMaterial());
            AddBridgeShellCube(
                "PCT Bridge Metal access side trim R #" + assetId,
                (deckBase + foot) * 0.5f - side * (BridgeAccessVisualWidth * 0.5f + BridgeAccessSideTrimOutset) + Vector3.up * (BridgeConcreteThickness + BridgeAccessSideTrimHeight),
                Quaternion.LookRotation(slopeDirection, Vector3.up),
                new Vector3(BridgeAccessSideTrimHeight, BridgeAccessSideTrimHeight, length),
                GetBridgeTrimMaterial());
        }

        private static void AddSubwayEntranceVisual(CrossingLandingAccessAssetWorkOrder order)
        {
            if (!ShouldRenderSubwayEntrance(order))
                return;

            Vector3 entranceDirection = ResolveSubwayEntranceDirection(order);
            if (entranceDirection.sqrMagnitude <= 0.01f)
                return;

            Vector3 visualPosition = GetSubwayEntranceVisualGroundPosition(order, entranceDirection);
            AddSubwayEntranceVisual(
                order,
                order.AssetId,
                order.EndpointName,
                visualPosition,
                entranceDirection,
                order.FootprintWidth,
                order.FootprintLength);
        }

        private static bool ShouldRenderSubwayEntrance(CrossingLandingAccessAssetWorkOrder order)
        {
            return order.AssetKind == CrossingLandingAccessAssetKind.SubwayEntrance
                   && (order.CrossingKind == CrossingConnectivityLinkKind.SubwaySpan
                       || order.CrossingKind == CrossingConnectivityLinkKind.JunctionSubwayApproach);
        }

        private static void AddSubwayEntranceVisual(CrossingLandingAccessAssetWorkOrder order, int assetId, string endpointName, Vector3 position, Vector3 direction, float footprintWidth, float footprintLength)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return;

            direction.Normalize();
            string key = MakeSubwayEntranceVisualKey(position);
            if (BuiltSubwayEntranceVisualKeys.Contains(key))
            {
                AddSubwayEntranceVisualAsset(key, assetId);
                return;
            }

            float width = footprintWidth > 0f ? footprintWidth : SubwayEntranceVisualWidth;
            float length = SubwayEntranceVisualLength;
            SubwayEntranceTerrainFrame frame;
            if (!TryCreateSubwayEntranceTerrainFrame(order, position, direction, width, length, out frame))
                return;

            BuiltSubwayEntranceVisualKeys.Add(key);
            AddSubwayEntranceVisualAsset(key, assetId);
            int objectStartIndex = BuiltBridgeConcreteObjects.Count;
            Vector3 boxMid = position + frame.Forward * (length * 0.5f);

            AddSubwaySlopePlane(assetId, position, frame, width, length);

            float sideOffset = (width * 0.5f) + (SubwayEntranceWallThickness * 0.5f);
            AddSubwayEntranceBlock(
                assetId,
                frame.SurfacePoint(boxMid + frame.Side * sideOffset, 0f) + frame.Normal * (SubwayEntranceWallHeight * 0.5f),
                frame.Rotation,
                new Vector3(SubwayEntranceWallThickness, SubwayEntranceWallHeight, length),
                "side-left",
                GetSubwayEntranceWallMaterial());
            AddSubwayEntranceBlock(
                assetId,
                frame.SurfacePoint(boxMid - frame.Side * sideOffset, 0f) + frame.Normal * (SubwayEntranceWallHeight * 0.5f),
                frame.Rotation,
                new Vector3(SubwayEntranceWallThickness, SubwayEntranceWallHeight, length),
                "side-right",
                GetSubwayEntranceWallMaterial());

            AddSubwayEntranceDoorWall(assetId, position + frame.Forward * (SubwayEntranceBackWallThickness * 0.5f), frame, width, "front");
            AddSubwayEntranceDoorWall(assetId, position + frame.Forward * (length - SubwayEntranceBackWallThickness * 0.5f), frame, width, "back");
            AddSubwayEntranceTrim(assetId, position, frame, width, length);
            RegisterSubwayEntranceVisualObjects(key, objectStartIndex);
            Debug.Log("[PedestrianCrossingToolkit] Subway entrance visual built: asset="
                      + assetId
                      + " endpoint="
                      + endpointName
                      + " position="
                      + position
                      + " direction="
                      + direction
                      + " slopeNormal="
                      + frame.Normal
                      + " length="
                      + length.ToString("0.0")
                      + " width="
                      + width.ToString("0.0")
                      + " height="
                      + SubwayEntranceWallHeight.ToString("0.0")
                      + " doorway="
                      + SubwayEntranceDoorwayWidth.ToString("0.0")
                      + "x"
                      + SubwayEntranceDoorwayHeight.ToString("0.0"));
        }

        private static string MakeSubwayEntranceVisualKey(Vector3 position)
        {
            return "subway:" + MakeBridgeAnchorKey(position);
        }

        private static void AddSubwayEntranceVisualAsset(string key, int assetId)
        {
            if (assetId <= 0)
                return;

            List<int> assetIds;
            if (!BuiltSubwayEntranceVisualAssets.TryGetValue(key, out assetIds))
            {
                assetIds = new List<int>();
                BuiltSubwayEntranceVisualAssets[key] = assetIds;
            }

            if (!assetIds.Contains(assetId))
                assetIds.Add(assetId);
        }

        private static void RegisterSubwayEntranceVisualObjects(string key, int objectStartIndex)
        {
            List<GameObject> objects;
            if (!BuiltSubwayEntranceVisualObjects.TryGetValue(key, out objects))
            {
                objects = new List<GameObject>();
                BuiltSubwayEntranceVisualObjects[key] = objects;
            }

            for (int i = objectStartIndex; i < BuiltBridgeConcreteObjects.Count; i++)
            {
                GameObject obj = BuiltBridgeConcreteObjects[i];
                if (obj != null && !objects.Contains(obj))
                    objects.Add(obj);
            }
        }

        private static void AddSubwayEntranceDoorWall(int assetId, Vector3 wallBaseCenter, SubwayEntranceTerrainFrame frame, float openingWidth, string role)
        {
            float totalWidth = openingWidth + SubwayEntranceWallThickness * 2f;
            float doorwayWidth = Mathf.Clamp(SubwayEntranceDoorwayWidth, 0.1f, totalWidth - 0.1f);
            float postWidth = Mathf.Max(0.05f, (totalWidth - doorwayWidth) * 0.5f);
            float doorwayHeight = Mathf.Clamp(SubwayEntranceDoorwayHeight, 0.1f, SubwayEntranceWallHeight);
            float lintelHeight = SubwayEntranceWallHeight - doorwayHeight;
            Vector3 fullHeightCenter = frame.SurfacePoint(wallBaseCenter, 0f) + frame.Normal * (SubwayEntranceWallHeight * 0.5f);

            AddSubwayEntranceBlock(
                assetId,
                fullHeightCenter + frame.Side * ((doorwayWidth * 0.5f) + (postWidth * 0.5f)),
                frame.Rotation,
                new Vector3(postWidth, SubwayEntranceWallHeight, SubwayEntranceBackWallThickness),
                role + "-doorpost-left",
                GetSubwayEntranceWallMaterial());
            AddSubwayEntranceBlock(
                assetId,
                fullHeightCenter - frame.Side * ((doorwayWidth * 0.5f) + (postWidth * 0.5f)),
                frame.Rotation,
                new Vector3(postWidth, SubwayEntranceWallHeight, SubwayEntranceBackWallThickness),
                role + "-doorpost-right",
                GetSubwayEntranceWallMaterial());
            if (lintelHeight > 0.01f)
            {
                AddSubwayEntranceBlock(
                    assetId,
                    frame.SurfacePoint(wallBaseCenter, 0f) + frame.Normal * (doorwayHeight + lintelHeight * 0.5f),
                    frame.Rotation,
                    new Vector3(doorwayWidth, lintelHeight, SubwayEntranceBackWallThickness),
                    role + "-door-lintel",
                    GetSubwayEntranceWallMaterial());
            }
        }

        private static void AddSubwayEntranceTrim(int assetId, Vector3 position, SubwayEntranceTerrainFrame frame, float width, float length)
        {
            float totalWidth = width + SubwayEntranceWallThickness * 2f + SubwayEntranceCopingOverhang * 2f;
            float sideOffset = (width * 0.5f) + (SubwayEntranceWallThickness * 0.5f);
            float copingWidth = SubwayEntranceWallThickness + SubwayEntranceCopingOverhang * 2f;
            Vector3 boxMid = position + frame.Forward * (length * 0.5f);
            Vector3 sideScale = new Vector3(copingWidth, SubwayEntranceCopingHeight, length + SubwayEntranceCopingOverhang * 2f);

            AddSubwayEntranceBlock(
                assetId,
                frame.SurfacePoint(boxMid + frame.Side * sideOffset, 0f) + frame.Normal * (SubwayEntranceWallHeight + SubwayEntranceCopingHeight * 0.5f),
                frame.Rotation,
                sideScale,
                "coping-left",
                GetSubwayStepNoseMaterial());
            AddSubwayEntranceBlock(
                assetId,
                frame.SurfacePoint(boxMid - frame.Side * sideOffset, 0f) + frame.Normal * (SubwayEntranceWallHeight + SubwayEntranceCopingHeight * 0.5f),
                frame.Rotation,
                sideScale,
                "coping-right",
                GetSubwayStepNoseMaterial());

            AddSubwayEntranceBlock(
                assetId,
                frame.SurfacePoint(position + frame.Forward * (SubwayEntranceThresholdDepth * 0.5f), 0f) + frame.Normal * (SubwayEntranceCopingHeight * 0.5f),
                frame.Rotation,
                new Vector3(totalWidth, SubwayEntranceCopingHeight, SubwayEntranceThresholdDepth),
                "front-threshold",
                GetSubwayStepNoseMaterial());
            AddSubwayEntranceBlock(
                assetId,
                frame.SurfacePoint(position + frame.Forward * (length - SubwayEntranceThresholdDepth * 0.5f), 0f) + frame.Normal * (SubwayEntranceCopingHeight * 0.5f),
                frame.Rotation,
                new Vector3(totalWidth, SubwayEntranceCopingHeight, SubwayEntranceThresholdDepth),
                "rear-threshold",
                GetSubwayOpeningMaterial());
        }

        private static Vector3 ResolveSubwayEntranceDirection(CrossingLandingAccessAssetWorkOrder order)
        {
            Vector3 direction = order.FacingDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
            {
                direction = order.DeckPosition - order.Position;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= 0.01f)
                return Vector3.zero;

            direction.Normalize();
            return direction;
        }

        private static bool TryCreateSubwayEntranceTerrainFrame(CrossingLandingAccessAssetWorkOrder order, Vector3 position, Vector3 direction, float width, float length, out SubwayEntranceTerrainFrame frame)
        {
            frame = default(SubwayEntranceTerrainFrame);
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return false;

            direction.Normalize();
            Vector3 side = new Vector3(-direction.z, 0f, direction.x);
            if (side.sqrMagnitude <= 0.01f)
                return false;

            side.Normalize();
            float sampleLength = Mathf.Max(0.4f, length);
            Vector3 start = GetSubwayEntranceTerrainSample(order, position);
            Vector3 end = GetSubwayEntranceTerrainSample(order, position + direction * sampleLength);

            Vector3 slopeForward = end - start;
            if (slopeForward.sqrMagnitude <= 0.01f)
                slopeForward = direction;

            Vector3 normal = Vector3.Cross(side, slopeForward);
            if (normal.sqrMagnitude <= 0.01f)
                normal = Vector3.up;

            normal.Normalize();
            if (normal.y < 0f)
                normal = -normal;

            Vector3 tiltedForward = slopeForward - normal * Vector3.Dot(slopeForward, normal);
            if (tiltedForward.sqrMagnitude <= 0.01f)
                tiltedForward = direction;

            tiltedForward.Normalize();
            if (Vector3.Dot(tiltedForward, direction) < 0f)
                tiltedForward = -tiltedForward;

            Vector3 tiltedSide = Vector3.Cross(tiltedForward, normal);
            if (tiltedSide.sqrMagnitude <= 0.01f)
                tiltedSide = side;

            tiltedSide.Normalize();
            if (Vector3.Dot(tiltedSide, side) < 0f)
                tiltedSide = -tiltedSide;

            frame = new SubwayEntranceTerrainFrame(order, start, tiltedForward, tiltedSide, normal);
            return true;
        }

        private static Vector3 GetSubwayEntranceTerrainSample(CrossingLandingAccessAssetWorkOrder order, Vector3 position)
        {
            Vector3 sample = position;
            sample.y = GetSubwayEntrancePavementHeight(order, position, order.Position.y);
            return sample;
        }

        private static Vector3 GetSubwayEntranceVisualGroundPosition(CrossingLandingAccessAssetWorkOrder order, Vector3 direction)
        {
            if (order.AssetKind != CrossingLandingAccessAssetKind.SubwayEntrance)
                return GetAccessGroundPosition(order);

            Vector3 position = order.Position;
            position.y = GetSubwayEntrancePavementHeight(order, position, order.Position.y);
            position.y += SubwayEntrancePadSurfaceLift;
            return position;
        }

        private static Vector3 GetAccessGroundPosition(CrossingLandingAccessAssetWorkOrder order)
        {
            if (order.AssetKind != CrossingLandingAccessAssetKind.SubwayEntrance)
                return order.Position;

            Vector3 position = order.DeckPosition;
            position.y = GetSubwayEntrancePavementHeight(order, position, order.Position.y);
            position.y += SubwayEntrancePadSurfaceLift;
            return position;
        }

        private static float GetSubwayEntrancePavementHeight(CrossingLandingAccessAssetWorkOrder order, Vector3 position, float fallbackHeight)
        {
            float height = fallbackHeight;
            float segmentHeight = height;
            bool hasSegmentHeight = order.ConnectorTargetKind == CrossingLandingConnectorTargetKind.PedestrianLane
                                    && TryGetPedestrianLaneSurfaceHeight(order.SegmentId, position, out segmentHeight);
            if (hasSegmentHeight)
            {
                return segmentHeight;
            }

            TerrainManager terrainManager = TerrainManager.instance;
            if (terrainManager != null)
            {
                float terrainHeight = terrainManager.SampleRawHeightSmooth(position);
                height = terrainHeight;
            }

            return height;
        }

        private static bool TryGetPedestrianLaneSurfaceHeight(ushort segmentId, Vector3 position, out float height)
        {
            height = position.y;
            Vector3 laneSurfacePosition;
            if (!RoadPavementAnchorResolver.TryGetPedestrianLaneSurfacePosition(segmentId, position, out laneSurfacePosition))
                return false;

            height = laneSurfacePosition.y;
            return true;
        }

        private static void AddSubwaySlopePlane(int assetId, Vector3 top, SubwayEntranceTerrainFrame frame, float width, float length)
        {
            float visibleLength = Mathf.Clamp(SubwayEntranceStepPanelLength, 0.6f, length - SubwayEntranceBackWallThickness - 0.2f);
            float backDistance = length - SubwayEntranceBackWallThickness - 0.03f;
            Vector3 startCenter = top;
            Vector3 splitCenter = top + frame.Forward * visibleLength;
            Vector3 backCenter = top + frame.Forward * backDistance;

            AddSubwayConcreteStairRun(assetId, startCenter, splitCenter, frame, width);
            AddSubwaySurfacePanel(assetId, "rear-haze", splitCenter, backCenter, frame, width, GetSubwayOpeningMaterial(), SubwayEntranceSlopeSurfaceLift);
        }

        private static void AddSubwayConcreteStairRun(int assetId, Vector3 startCenter, Vector3 endCenter, SubwayEntranceTerrainFrame frame, float width)
        {
            int steps = Mathf.Max(3, SubwayEntranceStairCount);
            Vector3 innerSide = frame.Side * (width * 0.5f);
            GameObject stairs = new GameObject("PCT Subway Entrance concrete stairs #" + assetId);
            Mesh mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>(steps * 8);
            List<int> triangles = new List<int>(steps * 24);

            for (int i = 0; i < steps; i++)
            {
                float nearT = i / (float)steps;
                float farT = (i + 1) / (float)steps;
                Vector3 nearCenter = Vector3.Lerp(startCenter, endCenter, nearT);
                Vector3 farCenter = Vector3.Lerp(startCenter, endCenter, farT);
                float inset = Mathf.Min(0.02f, Vector3.Distance(nearCenter, farCenter) * 0.2f);
                nearCenter += (farCenter - nearCenter).normalized * inset;
                farCenter -= (farCenter - nearCenter).normalized * inset;

                AddUpwardQuad(
                    vertices,
                    triangles,
                    frame.SurfacePoint(nearCenter - innerSide, SubwayEntranceSlopeSurfaceLift),
                    frame.SurfacePoint(nearCenter + innerSide, SubwayEntranceSlopeSurfaceLift),
                    frame.SurfacePoint(farCenter + innerSide, SubwayEntranceSlopeSurfaceLift),
                    frame.SurfacePoint(farCenter - innerSide, SubwayEntranceSlopeSurfaceLift));
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.normals = CreateUniformNormals(vertices.Count, frame.Normal);
            mesh.RecalculateBounds();

            MeshFilter filter = stairs.AddComponent<MeshFilter>();
            filter.mesh = mesh;
            MeshRenderer renderer = stairs.AddComponent<MeshRenderer>();
            renderer.material = GetSubwayEntranceWallMaterial();
            ConfigureSubwayEntranceSurfaceRenderer(renderer);
            BuiltBridgeConcreteObjects.Add(stairs);
            AddSubwayStepNoseLines(assetId, startCenter, endCenter, frame, width, steps);
        }

        private static void AddSubwayStepNoseLines(int assetId, Vector3 startCenter, Vector3 endCenter, SubwayEntranceTerrainFrame frame, float width, int steps)
        {
            Vector3 run = endCenter - startCenter;
            run.y = 0f;
            if (run.sqrMagnitude <= 0.01f)
                return;

            run.Normalize();
            for (int i = 1; i < steps; i++)
            {
                Vector3 center = Vector3.Lerp(startCenter, endCenter, i / (float)steps);
                AddSubwaySurfacePanel(
                    assetId,
                    "step-nose",
                    center - run * (SubwayEntranceStepNoseWidth * 0.5f),
                    center + run * (SubwayEntranceStepNoseWidth * 0.5f),
                    frame,
                    width,
                    GetSubwayStepNoseMaterial(),
                    SubwayEntranceSlopeSurfaceLift + 0.006f);
            }
        }

        private static void AddSubwaySurfacePanel(int assetId, string role, Vector3 startCenter, Vector3 endCenter, SubwayEntranceTerrainFrame frame, float width, Material material, float lift)
        {
            Vector3 innerSide = frame.Side * (width * 0.5f);
            GameObject section = new GameObject("PCT Subway Entrance " + role + " #" + assetId);
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
            {
                frame.SurfacePoint(startCenter - innerSide, lift),
                frame.SurfacePoint(startCenter + innerSide, lift),
                frame.SurfacePoint(endCenter + innerSide, lift),
                frame.SurfacePoint(endCenter - innerSide, lift)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = CreateUniformNormals(4, frame.Normal);
            mesh.RecalculateBounds();

            MeshFilter filter = section.AddComponent<MeshFilter>();
            filter.mesh = mesh;
            MeshRenderer renderer = section.AddComponent<MeshRenderer>();
            renderer.material = material ?? GetBridgeConcreteMaterial();
            ConfigureSubwayEntranceSurfaceRenderer(renderer);
            BuiltBridgeConcreteObjects.Add(section);
        }

        private static void AddUpwardQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static Vector3[] CreateUniformNormals(int count, Vector3 normal)
        {
            Vector3[] normals = new Vector3[count];
            for (int i = 0; i < count; i++)
                normals[i] = normal;

            return normals;
        }

        public static void MaintainSuppressedSurfaceCrossings()
        {
            ApplySuppressedSurfaceCrossingFlags(false);
        }

        private static void ApplySuppressedSurfaceCrossingFlags(bool log)
        {
            GradeSeparatedVanillaCrossingSuppression.Reconcile(log, "built-structure");
        }

        private static bool SetBuiltNodeSurfaceCrossings(ushort nodeId, bool enabled)
        {
            return SetBuiltNodeSurfaceCrossings(nodeId, enabled, true);
        }

        private static bool SetBuiltNodeSurfaceCrossings(ushort nodeId, bool enabled, bool useTrafficManagerInterop)
        {
            NetManager netManager = NetManager.instance;
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return false;

            bool changed = false;
            int segmentCount = node.CountSegments();
            for (int i = 0; i < segmentCount; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                    continue;

                if (!HasVehicleLane(segment.Info))
                    continue;

                NetSegment.Flags crossingFlag;
                bool startNode;
                if (segment.m_startNode == nodeId)
                {
                    crossingFlag = NetSegment.Flags.CrossingStart;
                    startNode = true;
                }
                else if (segment.m_endNode == nodeId)
                {
                    crossingFlag = NetSegment.Flags.CrossingEnd;
                    startNode = false;
                }
                else
                    continue;

                if (!BuiltSignalSegmentSnapshots.ContainsKey(segmentId))
                    BuiltSignalSegmentSnapshots.Add(segmentId, segment.m_flags);

                bool segmentFlagChanged = enabled
                    ? (segment.m_flags & crossingFlag) == 0
                    : (segment.m_flags & crossingFlag) != 0;

                if (enabled)
                    segment.m_flags |= crossingFlag;
                else
                    segment.m_flags &= ~crossingFlag;

                bool tmpeChanged = useTrafficManagerInterop
                    && TrafficManagerPedestrianCrossingIntegration.SetPedestrianCrossingAllowed(segmentId, startNode, enabled);
                if (segmentFlagChanged || tmpeChanged)
                {
                    netManager.UpdateSegmentFlags(segmentId);
                    netManager.UpdateSegmentRenderer(segmentId, true);
                    changed = true;
                }
            }

            if (changed)
            {
                netManager.UpdateNodeFlags(nodeId);
                netManager.UpdateNodeRenderer(nodeId, true);
            }

            return changed;
        }

        private static void ApplyBuiltSignalControl(CrossingPathWorkOrder order)
        {
            if (order.Kind != CrossingPathWorkOrderKind.SignalizedSurfacePath)
                return;

            CrossingPlacementAsset asset;
            if (!CrossingPlacementRegistry.TryGetAssetById(order.AssetId, out asset) || !asset.Plan.IsValid)
                return;

            ushort nodeId;
            if (!TryGetSignalControlNode(asset, out nodeId))
            {
                Debug.Log("[PedestrianCrossingToolkit] Signal traffic control skipped: asset="
                          + order.AssetId
                          + " reason=signal-crossing-needs-segment-join-node");
                return;
            }

            bool junction = SetBuiltSignalNodeFlag(nodeId, NetNode.Flags.Junction, true);
            bool trafficLights = SetBuiltSignalNodeFlag(nodeId, NetNode.Flags.TrafficLights, true);
            bool crossings = SetBuiltSignalSurfaceCrossings(nodeId, true);
            Debug.Log("[PedestrianCrossingToolkit] Signal traffic control applied: asset="
                      + order.AssetId
                      + " node="
                      + nodeId
                      + " junction="
                      + junction
                      + " trafficLights="
                      + trafficLights
                      + " crossings="
                      + crossings
                      + " crossingSegment="
                      + asset.Placement.SegmentId);
            Vector3 conflictFirstPosition;
            Vector3 conflictSecondPosition;
            if (!TryGetSignalConflictSpan(order, asset, out conflictFirstPosition, out conflictSecondPosition))
            {
                conflictFirstPosition = order.FirstPosition;
                conflictSecondPosition = order.SecondPosition;
            }

            Vector3 signalRoadDirection = GetSignalPedestrianRoadDirection(order, asset, conflictFirstPosition, conflictSecondPosition);
            bool expandedWaitingZones = ShouldUseExpandedSignalWaitingZones(asset, conflictFirstPosition, conflictSecondPosition);
            RegisterBuiltSignalController(
                order.AssetId,
                nodeId,
                asset.Placement.SegmentId,
                GetSignalControllerCenter(order, asset),
                order.FirstPosition,
                order.SecondPosition,
                conflictFirstPosition,
                conflictSecondPosition,
                signalRoadDirection,
                expandedWaitingZones);
        }

        private static void QueueBuiltSignalControl(CrossingPathWorkOrder order)
        {
            if (order.Kind != CrossingPathWorkOrderKind.SignalizedSurfacePath)
                return;

            for (int i = 0; i < PendingSignalControlOrders.Count; i++)
            {
                if (PendingSignalControlOrders[i].AssetId == order.AssetId)
                    return;
            }

            PendingSignalControlOrders.Add(order);
        }

        private static void QueueAllSignalControls(CrossingPathWorkOrder[] orders, int count)
        {
            int max = Mathf.Min(count, orders.Length);
            for (int i = 0; i < max; i++)
            {
                CrossingPathWorkOrder order = orders[i];
                if (order.Kind == CrossingPathWorkOrderKind.SignalizedSurfacePath)
                    QueueBuiltSignalControl(order);
            }
        }

        private static void ApplyPendingSignalControls()
        {
            if (PendingSignalControlOrders.Count == 0)
                return;

            for (int i = 0; i < PendingSignalControlOrders.Count; i++)
                ApplyBuiltSignalControl(PendingSignalControlOrders[i]);

            PendingSignalControlOrders.Clear();
        }

        private static Vector3 GetSignalControllerCenter(CrossingPathWorkOrder order, CrossingPlacementAsset asset)
        {
            Vector3 center = (order.FirstPosition + order.SecondPosition) * 0.5f;
            if (center.sqrMagnitude > 0.01f)
                return center;

            return asset.Plan.Center;
        }

        private static bool TryGetSignalConflictSpan(CrossingPathWorkOrder order, CrossingPlacementAsset asset, out Vector3 conflictFirstPosition, out Vector3 conflictSecondPosition)
        {
            conflictFirstPosition = order.FirstPosition;
            conflictSecondPosition = order.SecondPosition;

            SurfaceCrossingFrame frame;
            if (!TryCreateSurfaceCrossingFrame(order, out frame))
                return false;

            List<SurfaceMarkingSpan> roadSpans = BuildSurfaceVisualPlan(asset, frame.Across).RoadSpans;
            if (roadSpans == null || roadSpans.Count == 0)
                return false;

            float minOffset = float.MaxValue;
            float maxOffset = float.MinValue;
            for (int i = 0; i < roadSpans.Count; i++)
            {
                SurfaceMarkingSpan span = roadSpans[i];
                minOffset = Mathf.Min(minOffset, span.Min);
                maxOffset = Mathf.Max(maxOffset, span.Max);
            }

            float startOffset = Vector3.Dot(frame.First - frame.Center, frame.Across);
            float endOffset = Vector3.Dot(frame.Second - frame.Center, frame.Across);
            float pathMin = Mathf.Min(startOffset, endOffset);
            float pathMax = Mathf.Max(startOffset, endOffset);
            minOffset = Mathf.Clamp(minOffset, pathMin, pathMax);
            maxOffset = Mathf.Clamp(maxOffset, pathMin, pathMax);
            if (maxOffset <= minOffset + 0.1f)
                return false;

            conflictFirstPosition = GetSurfacePathPoint(frame.First, frame.Second, frame.Center, frame.Across, minOffset, startOffset, endOffset);
            conflictSecondPosition = GetSurfacePathPoint(frame.First, frame.Second, frame.Center, frame.Across, maxOffset, startOffset, endOffset);
            return true;
        }

        private static void ApplyBuiltSurfaceCrossingControl(CrossingPathWorkOrder order)
        {
            if (order.Kind != CrossingPathWorkOrderKind.SurfacePath)
                return;

            CrossingPlacementAsset asset;
            if (!CrossingPlacementRegistry.TryGetAssetById(order.AssetId, out asset) || !asset.Plan.IsValid)
                return;

            ushort nodeId;
            if (!TryGetSurfaceCrossingControlNode(asset, out nodeId))
            {
                Debug.Log("[PedestrianCrossingToolkit] Surface crossing control skipped: asset="
                          + order.AssetId
                          + " reason=connector-only-crossing-has-no-road-node-control");
                return;
            }

            bool crossings = SetBuiltNodeSurfaceCrossings(nodeId, true);
            Debug.Log("[PedestrianCrossingToolkit] Surface crossing control applied: asset="
                      + order.AssetId
                      + " node="
                      + nodeId
                      + " crossings="
                      + crossings);
        }

        private static bool TryGetSignalControlNode(CrossingPlacementAsset asset, out ushort nodeId)
        {
            nodeId = asset.Plan.TargetNodeId;
            return ShouldUseVanillaSignalNodeCrossing(asset);
        }

        private static bool ShouldUseVanillaSignalNodeCrossing(CrossingPlacementAsset asset)
        {
            if (!asset.Plan.IsValid || asset.Plan.TargetNodeId == 0)
                return false;

            NetManager netManager = NetManager.instance;
            if (netManager == null
                || asset.Plan.TargetNodeId >= netManager.m_nodes.m_size
                || asset.Placement.SegmentId == 0
                || asset.Placement.SegmentId >= netManager.m_segments.m_size)
            {
                return false;
            }

            ref NetNode node = ref netManager.m_nodes.m_buffer[asset.Plan.TargetNodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0 || node.CountSegments() != 2)
                return false;

            int vehicleSegments = 0;
            bool includesPlacementSegment = false;
            for (int i = 0; i < 2; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    return false;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0
                    || (segment.m_startNode != asset.Plan.TargetNodeId && segment.m_endNode != asset.Plan.TargetNodeId)
                    || !HasVehicleLane(segment.Info))
                {
                    return false;
                }

                vehicleSegments++;
                if (segmentId == asset.Placement.SegmentId)
                    includesPlacementSegment = true;
            }

            return vehicleSegments == 2 && includesPlacementSegment;
        }

        private static bool TryGetSurfaceCrossingControlNode(CrossingPlacementAsset asset, out ushort nodeId)
        {
            nodeId = asset.Plan.TargetNodeId;
            return nodeId != 0;
        }

        public static int RefreshSurfaceCrossingControlForAssetRemoval(CrossingPlacementAsset removed, string reason)
        {
            if (removed.Id == 0
                || !removed.Plan.IsValid
                || removed.Plan.ApplicationKind != CrossingApplicationKind.SurfaceCrossing)
            {
                return 0;
            }

            ushort nodeId;
            if (!TryGetSurfaceCrossingControlNode(removed, out nodeId))
                return 0;

            bool keepEnabled = HasRemainingSurfaceCrossingControlAtNode(nodeId, removed.Id);
            bool changed = SetBuiltNodeSurfaceCrossings(nodeId, keepEnabled);
            Debug.Log("[PedestrianCrossingToolkit] Surface crossing control refreshed after removal: reason="
                      + reason
                      + " removedAsset="
                      + removed.Id
                      + " node="
                      + nodeId
                      + " enabled="
                      + keepEnabled
                      + " changed="
                      + changed);
            return changed ? 1 : 0;
        }

        private static bool HasRemainingSurfaceCrossingControlAtNode(ushort nodeId, int removedAssetId)
        {
            int count = CrossingPlacementRegistry.CopyTo(SurfaceControlAssetBuffer);
            for (int i = 0; i < count; i++)
            {
                CrossingPlacementAsset asset = SurfaceControlAssetBuffer[i];
                SurfaceControlAssetBuffer[i] = CrossingPlacementAsset.None;
                if (asset.Id == 0
                    || asset.Id == removedAssetId
                    || !asset.Plan.IsValid
                    || asset.Plan.ApplicationKind != CrossingApplicationKind.SurfaceCrossing)
                {
                    continue;
                }

                ushort assetNodeId;
                if (TryGetSurfaceCrossingControlNode(asset, out assetNodeId) && assetNodeId == nodeId)
                    return true;
            }

            return false;
        }

        private static void RegisterBuiltSignalController(int assetId, ushort nodeId, ushort roadSegmentId, Vector3 center, Vector3 firstPosition, Vector3 secondPosition, Vector3 conflictFirstPosition, Vector3 conflictSecondPosition, Vector3 roadDirection, bool expandedWaitingZones)
        {
            if (nodeId == 0)
                return;

            for (int i = 0; i < BuiltSignalControllers.Count; i++)
            {
                BuiltSignalController controller = BuiltSignalControllers[i];
                if (controller.AssetId == assetId && controller.NodeId == nodeId)
                {
                    AddSignalPedestrianSpan(ref controller, firstPosition, secondPosition, conflictFirstPosition, conflictSecondPosition, roadDirection, expandedWaitingZones);
                    AddAlignedSignalRoadSegments(ref controller);
                    TryRestorePreparedSignalControllerState(ref controller);
                    RefreshSignalPedestrianCache(ref controller, true);
                    ReassertSignalControllerState(ref controller);
                    BuiltSignalControllers[i] = controller;
                    return;
                }
            }

            BuiltSignalController newController = new BuiltSignalController(assetId, nodeId, roadSegmentId, center, firstPosition, secondPosition, conflictFirstPosition, conflictSecondPosition, roadDirection, expandedWaitingZones);
            AddAlignedSignalRoadSegments(ref newController);
            TryRestorePreparedSignalControllerState(ref newController);
            RefreshSignalPedestrianCache(ref newController, true);
            ReassertSignalControllerState(ref newController);
            BuiltSignalControllers.Add(newController);
            Debug.Log("[PedestrianCrossingToolkit] Signal phase timing: asset="
                      + assetId
                      + " node="
                      + nodeId
                      + " span="
                      + GetSignalCrossingSpanLength(newController).ToString("0.0")
                      + "m requestConfirm="
                      + SignalPedestrianRequestConfirmSeconds.ToString("0.0")
                      + "s waitCooldown="
                      + SignalPedestrianWaitCooldownSeconds.ToString("0.0")
                      + "s pedestrianGreen="
                      + SignalPedestrianGreenSeconds.ToString("0.0")
                      + "s crossingClamp="
                      + GetSignalPedestrianProtectionSeconds(ref newController).ToString("0.0")
                      + "s clearance="
                      + GetSignalPedestrianClearanceSeconds(ref newController).ToString("0.0")
                      + "s spans="
                      + GetSignalPedestrianSpanCount(ref newController)
                      + " expandedWaiting="
                      + HasExpandedSignalWaitingZones(ref newController));
        }

        private static void AddSignalPedestrianSpan(ref BuiltSignalController controller, Vector3 firstPosition, Vector3 secondPosition, Vector3 conflictFirstPosition, Vector3 conflictSecondPosition, Vector3 roadDirection, bool expandedWaitingZones)
        {
            if (controller.PedestrianSpans == null)
                controller.PedestrianSpans = new List<SignalPedestrianSpan>();

            SignalPedestrianSpan span = new SignalPedestrianSpan(
                firstPosition,
                secondPosition,
                conflictFirstPosition,
                conflictSecondPosition,
                NormalizeSignalPedestrianRoadDirection(roadDirection, firstPosition, secondPosition),
                expandedWaitingZones);

            for (int i = 0; i < controller.PedestrianSpans.Count; i++)
            {
                SignalPedestrianSpan existing = controller.PedestrianSpans[i];
                if (!IsSameSignalPedestrianSpan(existing, span))
                    continue;

                if (span.ExpandedWaitingZones && !existing.ExpandedWaitingZones)
                {
                    existing.ExpandedWaitingZones = true;
                    controller.PedestrianSpans[i] = existing;
                }

                return;
            }

            controller.PedestrianSpans.Add(span);
        }

        private static bool IsSameSignalPedestrianSpan(SignalPedestrianSpan first, SignalPedestrianSpan second)
        {
            const float toleranceSqr = 0.25f;
            float direct = DistanceSqr2D(first.ConflictFirstPosition, second.ConflictFirstPosition)
                           + DistanceSqr2D(first.ConflictSecondPosition, second.ConflictSecondPosition);
            if (direct <= toleranceSqr)
                return true;

            float swapped = DistanceSqr2D(first.ConflictFirstPosition, second.ConflictSecondPosition)
                            + DistanceSqr2D(first.ConflictSecondPosition, second.ConflictFirstPosition);
            return swapped <= toleranceSqr;
        }

        private static int GetSignalPedestrianSpanCount(ref BuiltSignalController controller)
        {
            return controller.PedestrianSpans == null || controller.PedestrianSpans.Count == 0
                ? 1
                : controller.PedestrianSpans.Count;
        }

        private static bool HasExpandedSignalWaitingZones(ref BuiltSignalController controller)
        {
            if (controller.PedestrianSpans == null)
                return false;

            for (int i = 0; i < controller.PedestrianSpans.Count; i++)
            {
                if (controller.PedestrianSpans[i].ExpandedWaitingZones)
                    return true;
            }

            return false;
        }

        private static Vector3 GetSignalPedestrianRoadDirection(CrossingPathWorkOrder order, CrossingPlacementAsset asset, Vector3 conflictFirstPosition, Vector3 conflictSecondPosition)
        {
            Vector3 roadDirection = asset.Plan.RoadDirection;
            roadDirection.y = 0f;
            if (roadDirection.sqrMagnitude > 0.01f)
                return NormalizeSignalPedestrianRoadDirection(roadDirection, order.FirstPosition, order.SecondPosition);

            Vector3 crossingDirection = conflictSecondPosition - conflictFirstPosition;
            crossingDirection.y = 0f;
            if (crossingDirection.sqrMagnitude <= 0.01f)
                crossingDirection = order.SecondPosition - order.FirstPosition;

            crossingDirection.y = 0f;
            if (crossingDirection.sqrMagnitude <= 0.01f)
                return Vector3.forward;

            crossingDirection.Normalize();
            return new Vector3(-crossingDirection.z, 0f, crossingDirection.x);
        }

        private static Vector3 NormalizeSignalPedestrianRoadDirection(Vector3 roadDirection, Vector3 firstPosition, Vector3 secondPosition)
        {
            roadDirection.y = 0f;
            if (roadDirection.sqrMagnitude <= 0.01f)
            {
                Vector3 across = secondPosition - firstPosition;
                across.y = 0f;
                if (across.sqrMagnitude <= 0.01f)
                    return Vector3.forward;

                across.Normalize();
                roadDirection = new Vector3(-across.z, 0f, across.x);
            }

            roadDirection.Normalize();
            return roadDirection;
        }

        private static bool ShouldUseExpandedSignalWaitingZones(CrossingPlacementAsset asset, Vector3 conflictFirstPosition, Vector3 conflictSecondPosition)
        {
            if (FlatDistance(conflictFirstPosition, conflictSecondPosition) >= SignalExpandedWaitingWideConflictSpan)
                return true;

            NetManager netManager = NetManager.instance;
            if (netManager == null || asset.Plan.TargetNodeId == 0 || asset.Plan.TargetNodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[asset.Plan.TargetNodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0 || node.CountSegments() != 2)
                return false;

            Vector3 firstDirection = Vector3.zero;
            Vector3 secondDirection = Vector3.zero;
            int directions = 0;
            for (int i = 0; i < 2; i++)
            {
                ushort segmentId = node.GetSegment(i);
                Vector3 direction;
                if (!TryGetSegmentDirectionFromNode(segmentId, asset.Plan.TargetNodeId, out direction))
                    continue;

                if (directions == 0)
                    firstDirection = direction;
                else
                    secondDirection = direction;

                directions++;
            }

            return directions == 2 && Mathf.Abs(Vector3.Dot(firstDirection, secondDirection)) < SignalExpandedWaitingRoadAlignmentDot;
        }

        public static void PrepareSignalControllerStateRestoreExcept(int excludedAssetId, string reason)
        {
            PendingSignalControllerStateRestores.Clear();
            _pendingSignalControllerStateRestoreReason = reason ?? string.Empty;
            for (int i = 0; i < BuiltSignalControllers.Count; i++)
            {
                BuiltSignalController controller = BuiltSignalControllers[i];
                if (controller.AssetId == 0 || controller.AssetId == excludedAssetId)
                    continue;

                PendingSignalControllerStateRestores.Add(new SignalControllerStateRestore(controller));
            }

            if (PendingSignalControllerStateRestores.Count > 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Prepared signal controller state restore: reason="
                          + _pendingSignalControllerStateRestoreReason
                          + " excludedAsset="
                          + excludedAssetId
                          + " controllers="
                          + PendingSignalControllerStateRestores.Count);
            }
        }

        public static void ClearPreparedSignalControllerStateRestores()
        {
            PendingSignalControllerStateRestores.Clear();
            _pendingSignalControllerStateRestoreReason = string.Empty;
        }

        private static bool TryRestorePreparedSignalControllerState(ref BuiltSignalController controller)
        {
            for (int i = PendingSignalControllerStateRestores.Count - 1; i >= 0; i--)
            {
                SignalControllerStateRestore restore = PendingSignalControllerStateRestores[i];
                if (restore.AssetId != controller.AssetId || restore.NodeId != controller.NodeId)
                    continue;

                restore.ApplyTo(ref controller);
                PendingSignalControllerStateRestores.RemoveAt(i);
                Debug.Log("[PedestrianCrossingToolkit] Restored signal controller state after rebuild: reason="
                          + _pendingSignalControllerStateRestoreReason
                          + " asset="
                          + controller.AssetId
                          + " node="
                          + controller.NodeId
                          + " phase="
                          + controller.Phase
                          + " phaseTime="
                          + controller.PhaseTime.ToString("0.00")
                          + " vehicle="
                          + controller.AppliedVehicleState
                          + " pedestrian="
                          + controller.AppliedPedestrianState);
                return true;
            }

            return false;
        }

        private static void ReassertSignalControllerState(ref BuiltSignalController controller)
        {
            RoadBaseAI.TrafficLightState vehicleState;
            RoadBaseAI.TrafficLightState pedestrianState;
            if (controller.HasAppliedSignalState)
            {
                vehicleState = controller.AppliedVehicleState;
                pedestrianState = controller.AppliedPedestrianState;
            }
            else
            {
                GetSignalControllerPhaseStates(controller.Phase, out vehicleState, out pedestrianState);
            }

            SetSignalControllerStates(ref controller, vehicleState, pedestrianState, true);
        }

        private static void GetSignalControllerPhaseStates(SignalControllerPhase phase, out RoadBaseAI.TrafficLightState vehicleState, out RoadBaseAI.TrafficLightState pedestrianState)
        {
            switch (phase)
            {
                case SignalControllerPhase.Crossing:
                    vehicleState = RoadBaseAI.TrafficLightState.Red;
                    pedestrianState = RoadBaseAI.TrafficLightState.Green;
                    break;
                case SignalControllerPhase.Clearance:
                    vehicleState = RoadBaseAI.TrafficLightState.Red;
                    pedestrianState = RoadBaseAI.TrafficLightState.Red;
                    break;
                default:
                    vehicleState = RoadBaseAI.TrafficLightState.Green;
                    pedestrianState = RoadBaseAI.TrafficLightState.Red;
                    break;
            }
        }

        public static void UpdateSignalControllers(float simulationTimeDelta)
        {
            if (BuiltSignalControllers.Count == 0 || simulationTimeDelta <= 0f)
                return;

            for (int i = 0; i < BuiltSignalControllers.Count; i++)
            {
                BuiltSignalController controller = BuiltSignalControllers[i];
                UpdateSignalController(ref controller, simulationTimeDelta);
                BuiltSignalControllers[i] = controller;
            }
        }

        public static void ReapplySignalControllerStates()
        {
            if (BuiltSignalControllers.Count == 0)
                return;

            for (int i = 0; i < BuiltSignalControllers.Count; i++)
            {
                BuiltSignalController controller = BuiltSignalControllers[i];
                if (controller.HasAppliedSignalState)
                    SetSignalControllerStates(ref controller, controller.AppliedVehicleState, controller.AppliedPedestrianState, true);
                BuiltSignalControllers[i] = controller;
            }
        }

        private static void UpdateSignalController(ref BuiltSignalController controller, float delta)
        {
            controller.SignalStateRefreshTime += delta;
            controller.PedestrianScanTime += delta;
            controller.PedestrianFullScanFallbackTime += delta;
            controller.PhaseTime += delta;

            bool pedestriansOnCrossing = HasPedestriansOnSignalCrossing(ref controller);
            switch (controller.Phase)
            {
                case SignalControllerPhase.Idle:
                    SetSignalControllerLights(ref controller, true);
                    if (!HasPedestriansWaitingAtSignalEntrance(ref controller))
                    {
                        controller.HasPedestrianRequest = false;
                        controller.PhaseTime = 0f;
                        controller.CooldownTime = 0f;
                        break;
                    }

                    controller.HasPedestrianRequest = true;
                    controller.CooldownTime = Mathf.Min(SignalPedestrianWaitCooldownSeconds, controller.CooldownTime + delta);
                    if (controller.CooldownTime < SignalPedestrianWaitCooldownSeconds)
                    {
                        controller.PhaseTime = 0f;
                        break;
                    }

                    if (controller.PhaseTime < SignalPedestrianRequestConfirmSeconds)
                        break;

                    if (!HasFreshPedestriansWaitingAtSignalEntrance(ref controller))
                    {
                        controller.HasPedestrianRequest = false;
                        controller.PhaseTime = 0f;
                        controller.CooldownTime = 0f;
                        break;
                    }

                    controller.Phase = SignalControllerPhase.Crossing;
                    controller.PhaseTime = 0f;
                    controller.CooldownTime = 0f;
                    controller.HasPedestrianRequest = false;
                    SetSignalControllerLights(ref controller, false);
                    break;
                case SignalControllerPhase.Crossing:
                    SetSignalControllerLights(ref controller, false);
                    controller.CooldownTime = 0f;
                    if (controller.PhaseTime >= SignalPedestrianGreenSeconds)
                    {
                        controller.Phase = SignalControllerPhase.Clearance;
                        controller.PhaseTime = 0f;
                        controller.HasPedestrianRequest = false;
                        SetSignalControllerAllRed(ref controller);
                    }
                    break;
                case SignalControllerPhase.Clearance:
                    SetSignalControllerAllRed(ref controller);
                    controller.CooldownTime = 0f;
                    bool minimumClearanceMet = controller.PhaseTime >= GetSignalPedestrianClearanceSeconds(ref controller);
                    bool hardClearanceLimitMet = controller.PhaseTime >= SignalPedestrianClearanceHardMaxSeconds;
                    if (minimumClearanceMet && (!pedestriansOnCrossing || hardClearanceLimitMet))
                    {
                        controller.Phase = SignalControllerPhase.Idle;
                        controller.PhaseTime = 0f;
                        controller.HasPedestrianRequest = false;
                        SetSignalControllerLights(ref controller, true);
                    }
                    break;
            }
        }

        public static bool ShouldStopPedestriansAtSignalSegment(ushort segmentId)
        {
            int assetId;
            return TryGetSignalPathAssetId(segmentId, out assetId) && ShouldStopPedestriansForSignalAsset(assetId);
        }

        public static bool ShouldStopPedestriansAtSignalSegment(ushort previousSegmentId, ushort nextSegmentId, Vector3 pedestrianPosition)
        {
            int nextAssetId;
            if (!TryGetSignalPathAssetId(nextSegmentId, out nextAssetId))
                return false;

            int previousAssetId;
            if (TryGetSignalPathAssetId(previousSegmentId, out previousAssetId)
                && previousAssetId == nextAssetId
                && IsPedestrianOnSignalCrossingBody(nextAssetId, pedestrianPosition))
            {
                return false;
            }

            return ShouldStopPedestriansForSignalAsset(nextAssetId);
        }

        private static bool TryGetSignalPathAssetId(ushort segmentId, out int assetId)
        {
            assetId = 0;
            if (segmentId == 0)
                return false;

            CrossingPathWorkOrderKind kind;
            if (!BuiltSegmentKinds.TryGetValue(segmentId, out kind) || kind != CrossingPathWorkOrderKind.SignalizedSurfacePath)
                return false;

            return BuiltSignalPathSegmentAssets.TryGetValue(segmentId, out assetId);
        }

        private static bool ShouldStopPedestriansForSignalAsset(int assetId)
        {
            if (assetId == 0)
                return false;

            for (int i = 0; i < BuiltSignalControllers.Count; i++)
            {
                BuiltSignalController controller = BuiltSignalControllers[i];
                if (controller.AssetId == assetId)
                    return controller.Phase != SignalControllerPhase.Crossing;
            }

            return false;
        }

        private static void AddAlignedSignalRoadSegments(ref BuiltSignalController controller)
        {
            if (controller.RoadSegmentIds == null)
                controller.RoadSegmentIds = new HashSet<ushort>();

            if (controller.RoadSegmentId != 0)
                controller.RoadSegmentIds.Add(controller.RoadSegmentId);

            NetManager netManager = NetManager.instance;
            if (netManager == null
                || controller.NodeId == 0
                || controller.NodeId >= netManager.m_nodes.m_size
                || controller.RoadSegmentId == 0
                || controller.RoadSegmentId >= netManager.m_segments.m_size)
            {
                return;
            }

            Vector3 primaryDirection;
            if (!TryGetSegmentDirectionFromNode(controller.RoadSegmentId, controller.NodeId, out primaryDirection))
                return;

            AddNearbyAlignedSignalRoadSegments(ref controller, primaryDirection);

            ref NetNode node = ref netManager.m_nodes.m_buffer[controller.NodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return;

            int segmentCount = node.CountSegments();
            for (int i = 0; i < segmentCount; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || !HasVehicleLane(segment.Info))
                    continue;

                Vector3 direction;
                if (!TryGetSegmentDirectionFromNode(segmentId, controller.NodeId, out direction))
                    continue;

                if (Mathf.Abs(Vector3.Dot(primaryDirection, direction)) < SignalControlledSegmentAlignmentDot)
                    continue;

                controller.RoadSegmentIds.Add(segmentId);
            }
        }

        private static void AddNearbyAlignedSignalRoadSegments(ref BuiltSignalController controller, Vector3 primaryDirection)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null || netManager.m_segments == null || netManager.m_segments.m_buffer == null)
                return;

            float maxDistanceSqr = SignalNearbyRoadSegmentSearchRadius * SignalNearbyRoadSegmentSearchRadius;
            for (ushort segmentId = 1; segmentId < netManager.m_segments.m_size; segmentId++)
            {
                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || !HasVehicleLane(segment.Info))
                    continue;

                Vector3 direction;
                if (!TryGetSegmentDirection(segmentId, out direction))
                    continue;

                if (Mathf.Abs(Vector3.Dot(primaryDirection, direction)) < SignalControlledSegmentAlignmentDot)
                    continue;

                Vector3 closest = segment.GetClosestPosition(controller.Center);
                closest.y = controller.Center.y;
                if ((closest - controller.Center).sqrMagnitude > maxDistanceSqr)
                    continue;

                controller.RoadSegmentIds.Add(segmentId);
            }
        }

        private static bool TryGetSegmentDirection(ushort segmentId, out Vector3 direction)
        {
            direction = Vector3.zero;
            NetManager netManager = NetManager.instance;
            if (netManager == null
                || segmentId == 0
                || segmentId >= netManager.m_segments.m_size)
            {
                return false;
            }

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0
                || segment.m_startNode == 0
                || segment.m_endNode == 0
                || segment.m_startNode >= netManager.m_nodes.m_size
                || segment.m_endNode >= netManager.m_nodes.m_size)
            {
                return false;
            }

            direction = netManager.m_nodes.m_buffer[segment.m_endNode].m_position
                        - netManager.m_nodes.m_buffer[segment.m_startNode].m_position;
            direction.y = 0f;
            float sqrMagnitude = direction.sqrMagnitude;
            if (sqrMagnitude < 0.01f)
                return false;

            direction /= Mathf.Sqrt(sqrMagnitude);
            return true;
        }

        private static bool TryGetSegmentDirectionFromNode(ushort segmentId, ushort nodeId, out Vector3 direction)
        {
            direction = Vector3.zero;
            NetManager netManager = NetManager.instance;
            if (netManager == null
                || segmentId == 0
                || nodeId == 0
                || segmentId >= netManager.m_segments.m_size
                || nodeId >= netManager.m_nodes.m_size)
            {
                return false;
            }

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0
                || (segment.m_startNode != nodeId && segment.m_endNode != nodeId))
            {
                return false;
            }

            ushort otherNodeId = segment.m_startNode == nodeId ? segment.m_endNode : segment.m_startNode;
            if (otherNodeId == 0 || otherNodeId >= netManager.m_nodes.m_size)
                return false;

            Vector3 nodePosition = netManager.m_nodes.m_buffer[nodeId].m_position;
            Vector3 otherPosition = netManager.m_nodes.m_buffer[otherNodeId].m_position;
            direction = otherPosition - nodePosition;
            direction.y = 0f;
            float sqrMagnitude = direction.sqrMagnitude;
            if (sqrMagnitude < 0.01f)
                return false;

            direction /= Mathf.Sqrt(sqrMagnitude);
            return true;
        }

        private static bool HasVehicleLane(NetInfo info)
        {
            if (info == null || info.m_lanes == null)
                return false;

            for (int i = 0; i < info.m_lanes.Length; i++)
            {
                NetInfo.Lane lane = info.m_lanes[i];
                if (lane != null && (lane.m_laneType & NetInfo.LaneType.Vehicle) != 0)
                    return true;
            }

            return false;
        }

        private static bool HasPedestriansNearSignal(ref BuiltSignalController controller)
        {
            RefreshSignalPedestrianCache(ref controller);
            return controller.HasPedestriansNear;
        }

        private static bool HasPedestriansWaitingAtSignalEntrance(ref BuiltSignalController controller)
        {
            RefreshSignalPedestrianCache(ref controller);
            return controller.HasPedestriansWaitingAtEntrance;
        }

        private static bool HasFreshPedestriansWaitingAtSignalEntrance(ref BuiltSignalController controller)
        {
            RefreshSignalPedestrianCache(ref controller, true);
            return controller.HasPedestriansWaitingAtEntrance;
        }

        private static bool HasPedestriansOnSignalCrossing(ref BuiltSignalController controller)
        {
            RefreshSignalPedestrianCache(ref controller);
            return controller.HasPedestriansOnCrossing;
        }

        private static void RefreshSignalPedestrianOccupancyCache(ref BuiltSignalController controller)
        {
            RefreshSignalPedestrianOccupancyCache(ref controller, false);
        }

        private static void RefreshSignalPedestrianOccupancyCache(ref BuiltSignalController controller, bool force)
        {
            if (!force && controller.PedestrianScanTime < SignalPedestrianScanSeconds)
                return;

            controller.PedestrianScanTime = 0f;
            controller.HasPedestriansOnCrossing = false;

            CitizenManager citizenManager = CitizenManager.instance;
            if (citizenManager == null || citizenManager.m_instances == null || citizenManager.m_instances.m_buffer == null)
                return;

            float crossingRadiusSqr = SignalPedestrianOnCrossingRadius * SignalPedestrianOnCrossingRadius;
            CitizenInstance[] buffer = citizenManager.m_instances.m_buffer;
            if (ScanSignalPedestrianOccupancyInGrid(ref controller, citizenManager, buffer, crossingRadiusSqr))
                return;

            ScanSignalPedestrianOccupancyInBuffer(ref controller, citizenManager, buffer, crossingRadiusSqr);
        }

        private static bool ScanSignalPedestrianOccupancyInGrid(
            ref BuiltSignalController controller,
            CitizenManager citizenManager,
            CitizenInstance[] buffer,
            float crossingRadiusSqr)
        {
            if (citizenManager.m_citizenGrid == null || buffer == null)
                return false;

            int resolution = CitizenManager.CITIZENGRID_RESOLUTION;
            float cellSize = CitizenManager.CITIZENGRID_CELL_SIZE;
            if (resolution <= 0 || cellSize <= 0f || citizenManager.m_citizenGrid.Length < resolution * resolution)
                return false;

            Vector3 min = controller.Center;
            Vector3 max = controller.Center;
            IncludeSignalPedestrianOccupancyBounds(ref min, ref max, ref controller);

            float padding = SignalPedestrianOnCrossingRadius + SignalPedestrianSpatialScanPadding;
            int minX = GetSignalPedestrianGridCoord(min.x - padding, cellSize, resolution);
            int maxX = GetSignalPedestrianGridCoord(max.x + padding, cellSize, resolution);
            int minZ = GetSignalPedestrianGridCoord(min.z - padding, cellSize, resolution);
            int maxZ = GetSignalPedestrianGridCoord(max.z + padding, cellSize, resolution);
            int traversalLimit = Math.Min(buffer.Length, SignalPedestrianGridTraversalLimit);

            for (int gridZ = minZ; gridZ <= maxZ; gridZ++)
            {
                int rowOffset = gridZ * resolution;
                for (int gridX = minX; gridX <= maxX; gridX++)
                {
                    ushort instanceId = citizenManager.m_citizenGrid[rowOffset + gridX];
                    int traversed = 0;
                    while (instanceId != 0 && traversed++ < traversalLimit)
                    {
                        if (instanceId >= buffer.Length)
                            break;

                        CitizenInstance instance = buffer[instanceId];
                        ushort nextInstanceId = instance.m_nextGridInstance;
                        if (IsSignalPedestrianOnCrossing(instance, controller, crossingRadiusSqr))
                        {
                            controller.HasPedestriansOnCrossing = true;
                            return true;
                        }

                        instanceId = nextInstanceId;
                    }
                }
            }

            return true;
        }

        private static void ScanSignalPedestrianOccupancyInBuffer(
            ref BuiltSignalController controller,
            CitizenManager citizenManager,
            CitizenInstance[] buffer,
            float crossingRadiusSqr)
        {
            uint max = Math.Min(citizenManager.m_instances.m_size, (uint)buffer.Length);
            for (uint i = 1; i < max; i++)
            {
                if (IsSignalPedestrianOnCrossing(buffer[i], controller, crossingRadiusSqr))
                {
                    controller.HasPedestriansOnCrossing = true;
                    return;
                }
            }
        }

        private static bool IsSignalPedestrianOnCrossing(CitizenInstance instance, BuiltSignalController controller, float crossingRadiusSqr)
        {
            if (!IsSignalPedestrianCrossingCandidate(instance))
                return false;

            return DistanceSqrToSignalCrossingBody(instance.GetLastFramePosition(), controller) <= crossingRadiusSqr;
        }

        private static void RefreshSignalPedestrianCache(ref BuiltSignalController controller)
        {
            RefreshSignalPedestrianCache(ref controller, false);
        }

        private static void RefreshSignalPedestrianCache(ref BuiltSignalController controller, bool force)
        {
            if (!force && controller.PedestrianScanTime < SignalPedestrianScanSeconds)
                return;

            controller.PedestrianScanTime = 0f;
            controller.HasPedestriansNear = false;
            controller.HasPedestriansWaitingAtEntrance = false;
            controller.HasPedestriansOnCrossing = false;
            controller.PedestriansNearCount = 0;
            controller.PedestrianDemandCandidateCount = 0;

            CitizenManager citizenManager = CitizenManager.instance;
            if (citizenManager == null || citizenManager.m_instances == null || citizenManager.m_instances.m_buffer == null)
                return;

            float nearRadiusSqr = SignalPedestrianDetectionRadius * SignalPedestrianDetectionRadius;
            float waitingRadiusSqr = SignalPedestrianWaitingZebraEdgeRadius * SignalPedestrianWaitingZebraEdgeRadius;
            float crossingRadiusSqr = SignalPedestrianOnCrossingRadius * SignalPedestrianOnCrossingRadius;
            CitizenInstance[] buffer = citizenManager.m_instances.m_buffer;
            bool hasGridCitizens;
            if (!force
                && ScanSignalPedestriansInGrid(ref controller, citizenManager, buffer, nearRadiusSqr, waitingRadiusSqr, crossingRadiusSqr, out hasGridCitizens)
                && (hasGridCitizens || (!force && controller.PedestrianFullScanFallbackTime < SignalPedestrianFullScanFallbackSeconds)))
            {
                ApplySignalNearbyDemandFallback(ref controller);
                return;
            }

            controller.PedestrianFullScanFallbackTime = 0f;
            ScanSignalPedestriansInBuffer(ref controller, citizenManager, buffer, nearRadiusSqr, waitingRadiusSqr, crossingRadiusSqr);
            ApplySignalNearbyDemandFallback(ref controller);
        }

        private static bool ScanSignalPedestriansInGrid(
            ref BuiltSignalController controller,
            CitizenManager citizenManager,
            CitizenInstance[] buffer,
            float nearRadiusSqr,
            float waitingRadiusSqr,
            float crossingRadiusSqr,
            out bool hasGridCitizens)
        {
            hasGridCitizens = false;
            if (citizenManager.m_citizenGrid == null || buffer == null)
                return false;

            int resolution = CitizenManager.CITIZENGRID_RESOLUTION;
            float cellSize = CitizenManager.CITIZENGRID_CELL_SIZE;
            if (resolution <= 0 || cellSize <= 0f || citizenManager.m_citizenGrid.Length < resolution * resolution)
                return false;

            Vector3 min = controller.Center;
            Vector3 max = controller.Center;
            IncludeSignalPedestrianScanBounds(ref min, ref max, ref controller);

            float padding = Mathf.Max(
                Mathf.Max(SignalPedestrianDetectionRadius, SignalPedestrianTargetObservationDistance),
                Mathf.Max(SignalPedestrianApproachLongitudinalDistance, Mathf.Max(SignalPedestrianWaitingZebraEdgeRadius, SignalPedestrianOnCrossingRadius)))
                + SignalPedestrianSpatialScanPadding;
            int minX = GetSignalPedestrianGridCoord(min.x - padding, cellSize, resolution);
            int maxX = GetSignalPedestrianGridCoord(max.x + padding, cellSize, resolution);
            int minZ = GetSignalPedestrianGridCoord(min.z - padding, cellSize, resolution);
            int maxZ = GetSignalPedestrianGridCoord(max.z + padding, cellSize, resolution);
            int traversalLimit = Math.Min(buffer.Length, SignalPedestrianGridTraversalLimit);

            for (int gridZ = minZ; gridZ <= maxZ; gridZ++)
            {
                int rowOffset = gridZ * resolution;
                for (int gridX = minX; gridX <= maxX; gridX++)
                {
                    ushort instanceId = citizenManager.m_citizenGrid[rowOffset + gridX];
                    int traversed = 0;
                    while (instanceId != 0 && traversed++ < traversalLimit)
                    {
                        if (instanceId >= buffer.Length)
                            break;

                        CitizenInstance instance = buffer[instanceId];
                        ushort nextInstanceId = instance.m_nextGridInstance;
                        if ((instance.m_flags & CitizenInstance.Flags.Created) != 0)
                            hasGridCitizens = true;

                        if (UpdateSignalPedestrianCacheFromInstance(ref controller, instance, nearRadiusSqr, waitingRadiusSqr, crossingRadiusSqr))
                            return true;

                        instanceId = nextInstanceId;
                    }
                }
            }

            return true;
        }

        private static void ScanSignalPedestriansInBuffer(
            ref BuiltSignalController controller,
            CitizenManager citizenManager,
            CitizenInstance[] buffer,
            float nearRadiusSqr,
            float waitingRadiusSqr,
            float crossingRadiusSqr)
        {
            uint max = Math.Min(citizenManager.m_instances.m_size, (uint)buffer.Length);
            for (uint i = 1; i < max; i++)
            {
                CitizenInstance instance = buffer[i];
                if (UpdateSignalPedestrianCacheFromInstance(ref controller, instance, nearRadiusSqr, waitingRadiusSqr, crossingRadiusSqr))
                    return;
            }
        }

        private static bool UpdateSignalPedestrianCacheFromInstance(
            ref BuiltSignalController controller,
            CitizenInstance instance,
            float nearRadiusSqr,
            float waitingRadiusSqr,
            float crossingRadiusSqr)
        {
            if ((instance.m_flags & CitizenInstance.Flags.Created) == 0)
                return false;

            SignalPedestrianZoneStatus status = GetSignalPedestrianZoneStatus(instance, controller, nearRadiusSqr, waitingRadiusSqr, crossingRadiusSqr);
            if (status.HasPedestriansNear || status.HasPedestriansWaiting || status.HasPedestriansOnCrossing)
            {
                controller.HasPedestriansNear = true;
                controller.PedestriansNearCount++;
            }

            if (status.HasPedestriansWaiting)
                controller.HasPedestriansWaitingAtEntrance = true;

            if (status.HasPedestriansOnCrossing)
                controller.HasPedestriansOnCrossing = true;

            if (status.HasDemandCandidateNear)
                controller.PedestrianDemandCandidateCount++;

            return controller.HasPedestriansNear
                   && controller.HasPedestriansWaitingAtEntrance
                   && controller.HasPedestriansOnCrossing;
        }

        private static void ApplySignalNearbyDemandFallback(ref BuiltSignalController controller)
        {
            if (controller.HasPedestriansWaitingAtEntrance || controller.HasPedestriansOnCrossing)
                return;

            if (controller.PedestrianDemandCandidateCount >= SignalPedestrianQueuedNearbyDemandThreshold)
                controller.HasPedestriansWaitingAtEntrance = true;
        }

        public static bool TryGetSignalPedestrianZoneStatus(int assetId, CitizenInstance instance, out bool hasPedestriansNear, out bool hasPedestriansWaiting, out bool hasPedestriansOnCrossing)
        {
            hasPedestriansNear = false;
            hasPedestriansWaiting = false;
            hasPedestriansOnCrossing = false;
            if (assetId == 0 || (instance.m_flags & CitizenInstance.Flags.Created) == 0)
                return false;

            float nearRadiusSqr = SignalPedestrianDetectionRadius * SignalPedestrianDetectionRadius;
            float waitingRadiusSqr = SignalPedestrianWaitingZebraEdgeRadius * SignalPedestrianWaitingZebraEdgeRadius;
            float crossingRadiusSqr = SignalPedestrianOnCrossingRadius * SignalPedestrianOnCrossingRadius;
            bool foundController = false;
            for (int i = 0; i < BuiltSignalControllers.Count; i++)
            {
                BuiltSignalController controller = BuiltSignalControllers[i];
                if (controller.AssetId != assetId)
                    continue;

                foundController = true;
                SignalPedestrianZoneStatus status = GetSignalPedestrianZoneStatus(instance, controller, nearRadiusSqr, waitingRadiusSqr, crossingRadiusSqr);
                hasPedestriansNear |= status.HasPedestriansNear;
                hasPedestriansWaiting |= status.HasPedestriansWaiting;
                hasPedestriansOnCrossing |= status.HasPedestriansOnCrossing;
            }

            return foundController;
        }

        private static SignalPedestrianZoneStatus GetSignalPedestrianZoneStatus(CitizenInstance instance, BuiltSignalController controller, float nearRadiusSqr, float waitingRadiusSqr, float crossingRadiusSqr)
        {
            Vector3 position = instance.GetLastFramePosition();
            bool waitingCandidate = IsSignalPedestrianWaitingCandidate(instance);
            bool observedByController = IsSignalPedestrianObservedByController(position, controller);
            SignalPedestrianZoneStatus status = new SignalPedestrianZoneStatus();
            Vector3 targetPosition = Vector3.zero;
            bool hasTargetPosition = observedByController && TryGetSignalPedestrianTargetPosition(instance, out targetPosition);

            if (controller.PedestrianSpans != null && controller.PedestrianSpans.Count > 0)
            {
                for (int i = 0; i < controller.PedestrianSpans.Count; i++)
                {
                    SignalPedestrianSpan span = controller.PedestrianSpans[i];
                    AddSignalPedestrianSpanZoneStatus(position, waitingCandidate, span, nearRadiusSqr, waitingRadiusSqr, crossingRadiusSqr, ref status);
                    if (hasTargetPosition && IsInsideSignalTargetObservationZone(position, span))
                        AddSignalPedestrianSpanTargetZoneStatus(targetPosition, waitingCandidate, span, nearRadiusSqr, waitingRadiusSqr, ref status);
                }
            }
            else
            {
                SignalPedestrianSpan span = new SignalPedestrianSpan(
                    controller.FirstPosition,
                    controller.SecondPosition,
                    controller.ConflictFirstPosition,
                    controller.ConflictSecondPosition,
                    NormalizeSignalPedestrianRoadDirection(Vector3.zero, controller.FirstPosition, controller.SecondPosition),
                    false);
                AddSignalPedestrianSpanZoneStatus(position, waitingCandidate, span, nearRadiusSqr, waitingRadiusSqr, crossingRadiusSqr, ref status);
                if (hasTargetPosition && IsInsideSignalTargetObservationZone(position, span))
                    AddSignalPedestrianSpanTargetZoneStatus(targetPosition, waitingCandidate, span, nearRadiusSqr, waitingRadiusSqr, ref status);
            }

            return status;
        }

        private static void AddSignalPedestrianSpanZoneStatus(Vector3 position, bool waitingCandidate, SignalPedestrianSpan span, float nearRadiusSqr, float waitingRadiusSqr, float crossingRadiusSqr, ref SignalPedestrianZoneStatus status)
        {
            bool insideApproachZone = IsInsideSignalApproachZone(position, span);
            if (!status.HasPedestriansNear
                && (insideApproachZone || DistanceSqrToSignalCrossing(position, span) <= nearRadiusSqr))
            {
                status.HasPedestriansNear = true;
            }

            if (!status.HasDemandCandidateNear
                && insideApproachZone
                && waitingCandidate)
            {
                status.HasDemandCandidateNear = true;
            }

            if (!status.HasPedestriansWaiting
                && waitingCandidate
                && (insideApproachZone || IsInsideSignalWaitingZone(position, span, waitingRadiusSqr)))
            {
                status.HasPedestriansWaiting = true;
            }

            if (!status.HasPedestriansOnCrossing
                && IsInsideSignalCrossingUseZone(position, waitingCandidate, span, crossingRadiusSqr))
            {
                status.HasPedestriansOnCrossing = true;
            }
        }

        private static void AddSignalPedestrianSpanTargetZoneStatus(Vector3 targetPosition, bool waitingCandidate, SignalPedestrianSpan span, float nearRadiusSqr, float waitingRadiusSqr, ref SignalPedestrianZoneStatus status)
        {
            bool insideApproachZone = IsInsideSignalApproachZone(targetPosition, span);
            if (!status.HasPedestriansNear
                && (insideApproachZone || DistanceSqrToSignalCrossing(targetPosition, span) <= nearRadiusSqr))
            {
                status.HasPedestriansNear = true;
            }

            if (!status.HasDemandCandidateNear
                && insideApproachZone
                && waitingCandidate)
            {
                status.HasDemandCandidateNear = true;
            }

            if (status.HasPedestriansWaiting)
                return;

            if (waitingCandidate && (insideApproachZone || IsInsideSignalWaitingZone(targetPosition, span, waitingRadiusSqr)))
                status.HasPedestriansWaiting = true;
        }

        private static bool IsInsideSignalWaitingZone(Vector3 position, SignalPedestrianSpan span, float waitingRadiusSqr)
        {
            Vector3 flatFirst = span.ConflictFirstPosition;
            Vector3 flatSecond = span.ConflictSecondPosition;
            Vector3 roadDirection = NormalizeSignalPedestrianRoadDirection(span.RoadDirection, span.FirstPosition, span.SecondPosition);
            flatFirst.y = 0f;
            flatSecond.y = 0f;

            if (DistanceSqr2D(position, flatFirst) <= waitingRadiusSqr
                || DistanceSqr2D(position, flatSecond) <= waitingRadiusSqr)
            {
                return true;
            }

            if (!span.ExpandedWaitingZones)
                return false;

            Vector3 offset = roadDirection * SignalPedestrianExpandedWaitingLongitudinalOffset;
            return DistanceSqr2D(position, flatFirst + offset) <= waitingRadiusSqr
                   || DistanceSqr2D(position, flatFirst - offset) <= waitingRadiusSqr
                   || DistanceSqr2D(position, flatSecond + offset) <= waitingRadiusSqr
                   || DistanceSqr2D(position, flatSecond - offset) <= waitingRadiusSqr
                   || DistanceSqrToSegment2D(position, flatFirst - offset, flatFirst + offset) <= waitingRadiusSqr
                   || DistanceSqrToSegment2D(position, flatSecond - offset, flatSecond + offset) <= waitingRadiusSqr;
        }

        private static bool IsInsideSignalApproachZone(Vector3 position, SignalPedestrianSpan span)
        {
            Vector3 roadDirection = NormalizeSignalPedestrianRoadDirection(span.RoadDirection, span.FirstPosition, span.SecondPosition);
            float lateralRadiusSqr = SignalPedestrianApproachLateralDistance * SignalPedestrianApproachLateralDistance;
            if (IsInsideSignalApproachPointZone(position, span.ConflictFirstPosition, roadDirection, lateralRadiusSqr)
                || IsInsideSignalApproachPointZone(position, span.ConflictSecondPosition, roadDirection, lateralRadiusSqr))
            {
                return true;
            }

            Vector3 flatPosition = position;
            Vector3 flatFirst = span.ConflictFirstPosition;
            Vector3 flatSecond = span.ConflictSecondPosition;
            flatPosition.y = 0f;
            flatFirst.y = 0f;
            flatSecond.y = 0f;
            return DistanceSqrToSegment2D(flatPosition, flatFirst, flatSecond) <= lateralRadiusSqr;
        }

        private static bool IsInsideSignalApproachPointZone(Vector3 position, Vector3 point, Vector3 roadDirection, float lateralRadiusSqr)
        {
            Vector3 flatPosition = position;
            Vector3 flatPoint = point;
            flatPosition.y = 0f;
            flatPoint.y = 0f;
            Vector3 delta = flatPosition - flatPoint;
            float longitudinal = Vector3.Dot(delta, roadDirection);
            if (Mathf.Abs(longitudinal) > SignalPedestrianApproachLongitudinalDistance)
                return false;

            Vector3 lateral = delta - (roadDirection * longitudinal);
            return lateral.sqrMagnitude <= lateralRadiusSqr;
        }

        private static bool IsInsideSignalCrossingUseZone(Vector3 position, bool waitingCandidate, SignalPedestrianSpan span, float crossingRadiusSqr)
        {
            if (DistanceSqrToSignalCrossingBody(position, span) <= crossingRadiusSqr)
                return true;

            if (waitingCandidate)
                return false;

            return DistanceSqrToSignalCrossing(position, span) <= crossingRadiusSqr;
        }

        private static bool IsInsideSignalTargetObservationZone(Vector3 position, SignalPedestrianSpan span)
        {
            float observationRadiusSqr = SignalPedestrianTargetObservationDistance * SignalPedestrianTargetObservationDistance;
            return IsInsideSignalApproachZone(position, span)
                   || DistanceSqrToSignalCrossing(position, span) <= observationRadiusSqr;
        }

        private static bool IsSignalPedestrianObservedByController(Vector3 position, BuiltSignalController controller)
        {
            if (controller.PedestrianSpans != null && controller.PedestrianSpans.Count > 0)
            {
                for (int i = 0; i < controller.PedestrianSpans.Count; i++)
                {
                    if (IsInsideSignalTargetObservationZone(position, controller.PedestrianSpans[i]))
                        return true;
                }

                return false;
            }

            return IsInsideSignalTargetObservationZone(
                position,
                new SignalPedestrianSpan(
                    controller.FirstPosition,
                    controller.SecondPosition,
                    controller.ConflictFirstPosition,
                    controller.ConflictSecondPosition,
                    NormalizeSignalPedestrianRoadDirection(Vector3.zero, controller.FirstPosition, controller.SecondPosition),
                    false));
        }

        private static bool TryGetSignalPedestrianTargetPosition(CitizenInstance instance, out Vector3 targetPosition)
        {
            Vector4 target = instance.m_targetPos;
            targetPosition = new Vector3(target.x, target.y, target.z);
            if (float.IsNaN(targetPosition.x)
                || float.IsNaN(targetPosition.y)
                || float.IsNaN(targetPosition.z)
                || float.IsInfinity(targetPosition.x)
                || float.IsInfinity(targetPosition.y)
                || float.IsInfinity(targetPosition.z))
            {
                targetPosition = Vector3.zero;
                return false;
            }

            return targetPosition.sqrMagnitude > 0.01f || Mathf.Abs(target.w) > 0.01f;
        }

        private static void IncludeSignalPedestrianScanBounds(ref Vector3 min, ref Vector3 max, ref BuiltSignalController controller)
        {
            if (controller.PedestrianSpans != null && controller.PedestrianSpans.Count > 0)
            {
                for (int i = 0; i < controller.PedestrianSpans.Count; i++)
                    IncludeSignalPedestrianSpanScanBounds(ref min, ref max, controller.PedestrianSpans[i]);

                return;
            }

            IncludeSignalPedestrianSpanScanBounds(
                ref min,
                ref max,
                new SignalPedestrianSpan(
                    controller.FirstPosition,
                    controller.SecondPosition,
                    controller.ConflictFirstPosition,
                    controller.ConflictSecondPosition,
                    NormalizeSignalPedestrianRoadDirection(Vector3.zero, controller.FirstPosition, controller.SecondPosition),
                    false));
        }

        private static void IncludeSignalPedestrianSpanScanBounds(ref Vector3 min, ref Vector3 max, SignalPedestrianSpan span)
        {
            IncludeSignalPedestrianScanPoint(ref min, ref max, span.FirstPosition);
            IncludeSignalPedestrianScanPoint(ref min, ref max, span.SecondPosition);
            IncludeSignalPedestrianScanPoint(ref min, ref max, span.ConflictFirstPosition);
            IncludeSignalPedestrianScanPoint(ref min, ref max, span.ConflictSecondPosition);

            if (!span.ExpandedWaitingZones)
                return;

            Vector3 roadDirection = NormalizeSignalPedestrianRoadDirection(span.RoadDirection, span.FirstPosition, span.SecondPosition);
            Vector3 offset = roadDirection * SignalPedestrianExpandedWaitingLongitudinalOffset;
            IncludeSignalPedestrianScanPoint(ref min, ref max, span.ConflictFirstPosition + offset);
            IncludeSignalPedestrianScanPoint(ref min, ref max, span.ConflictFirstPosition - offset);
            IncludeSignalPedestrianScanPoint(ref min, ref max, span.ConflictSecondPosition + offset);
            IncludeSignalPedestrianScanPoint(ref min, ref max, span.ConflictSecondPosition - offset);
        }

        private static void IncludeSignalPedestrianOccupancyBounds(ref Vector3 min, ref Vector3 max, ref BuiltSignalController controller)
        {
            if (controller.PedestrianSpans != null && controller.PedestrianSpans.Count > 0)
            {
                for (int i = 0; i < controller.PedestrianSpans.Count; i++)
                    IncludeSignalPedestrianOccupancySpanBounds(ref min, ref max, controller.PedestrianSpans[i]);
                return;
            }

            IncludeSignalPedestrianOccupancySpanBounds(
                ref min,
                ref max,
                new SignalPedestrianSpan(
                    controller.FirstPosition,
                    controller.SecondPosition,
                    controller.ConflictFirstPosition,
                    controller.ConflictSecondPosition,
                    NormalizeSignalPedestrianRoadDirection(Vector3.zero, controller.FirstPosition, controller.SecondPosition),
                    false));
        }

        private static void IncludeSignalPedestrianOccupancySpanBounds(ref Vector3 min, ref Vector3 max, SignalPedestrianSpan span)
        {
            IncludeSignalPedestrianScanPoint(ref min, ref max, span.ConflictFirstPosition);
            IncludeSignalPedestrianScanPoint(ref min, ref max, span.ConflictSecondPosition);
        }

        private static void IncludeSignalPedestrianScanPoint(ref Vector3 min, ref Vector3 max, Vector3 point)
        {
            min.x = Mathf.Min(min.x, point.x);
            min.z = Mathf.Min(min.z, point.z);
            max.x = Mathf.Max(max.x, point.x);
            max.z = Mathf.Max(max.z, point.z);
        }

        private static int GetSignalPedestrianGridCoord(float value, float cellSize, int resolution)
        {
            return Mathf.Clamp((int)((value / cellSize) + (resolution * 0.5f)), 0, resolution - 1);
        }

        private static bool IsSignalPedestrianCrossingCandidate(CitizenInstance instance)
        {
            CitizenInstance.Flags flags = instance.m_flags;
            return (flags & CitizenInstance.Flags.Created) != 0
                   && (flags & (CitizenInstance.Flags.Deleted
                                | CitizenInstance.Flags.InsideBuilding
                                | CitizenInstance.Flags.WaitingTransport
                                | CitizenInstance.Flags.WaitingTaxi
                                | CitizenInstance.Flags.EnteringVehicle
                                | CitizenInstance.Flags.SittingDown)) == 0;
        }

        private static bool IsSignalPedestrianWaitingCandidate(CitizenInstance instance)
        {
            CitizenInstance.Flags flags = instance.m_flags;
            if ((flags & (CitizenInstance.Flags.Deleted
                          | CitizenInstance.Flags.InsideBuilding
                          | CitizenInstance.Flags.WaitingTransport
                          | CitizenInstance.Flags.WaitingTaxi
                          | CitizenInstance.Flags.EnteringVehicle
                          | CitizenInstance.Flags.SittingDown)) != 0)
            {
                return false;
            }

            if ((flags & (CitizenInstance.Flags.WaitingPath | CitizenInstance.Flags.BoredOfWaiting)) != 0)
                return true;

            return instance.m_waitCounter > 0 || IsStationarySignalPedestrian(instance);
        }

        private static bool IsStationarySignalPedestrian(CitizenInstance instance)
        {
            Vector3 velocity = instance.GetLastFrameData().m_velocity;
            velocity.y = 0f;
            return velocity.sqrMagnitude <= SignalPedestrianWaitingBodySpeedSqr;
        }

        private static bool HasVehiclesInSignalConflictZone(ref BuiltSignalController controller)
        {
            VehicleManager vehicleManager = VehicleManager.instance;
            if (vehicleManager == null || vehicleManager.m_vehicles == null || vehicleManager.m_vehicles.m_buffer == null)
                return false;

            Vehicle[] buffer = vehicleManager.m_vehicles.m_buffer;
            uint max = Math.Min(vehicleManager.m_vehicles.m_size, (uint)buffer.Length);
            for (uint i = 1; i < max && i <= ushort.MaxValue; i++)
            {
                Vehicle vehicle = buffer[(int)i];
                if ((vehicle.m_flags & Vehicle.Flags.Created) == 0
                    || (vehicle.m_flags & Vehicle.Flags.Deleted) != 0)
                {
                    continue;
                }

                if (IsVehicleInSignalConflictZone(vehicle, controller))
                    return true;
            }

            return false;
        }

        private static bool IsVehicleInSignalConflictZone(Vehicle vehicle, BuiltSignalController controller)
        {
            if ((vehicle.m_flags & Vehicle.Flags.Stopped) != 0)
                return false;

            Vector3 velocity = vehicle.GetLastFrameVelocity();
            if (velocity.sqrMagnitude < SignalVehicleMovingSpeedSqr)
                return false;

            Vector3 position = vehicle.GetLastFramePosition();
            return DistanceSqrToSignalCrossingSpan(position, controller) <= SignalVehicleCrossingMovingRadius * SignalVehicleCrossingMovingRadius;
        }

        private static bool IsPedestrianOnSignalCrossingBody(int assetId, Vector3 position)
        {
            float radiusSqr = SignalPedestrianOnCrossingRadius * SignalPedestrianOnCrossingRadius;
            for (int i = 0; i < BuiltSignalControllers.Count; i++)
            {
                BuiltSignalController controller = BuiltSignalControllers[i];
                if (controller.AssetId == assetId)
                    return DistanceSqrToSignalCrossingBody(position, controller) <= radiusSqr;
            }

            return false;
        }

        private static float GetSignalPedestrianClearanceSeconds(ref BuiltSignalController controller)
        {
            return Mathf.Max(
                SignalPedestrianClearanceMinSeconds,
                GetSignalPedestrianProtectionSeconds(ref controller) - SignalPedestrianGreenSeconds);
        }

        private static float GetSignalPedestrianProtectionSeconds(ref BuiltSignalController controller)
        {
            float spanLength = GetSignalCrossingSpanLength(controller);
            if (spanLength <= 0.01f)
                return SignalPedestrianGreenSeconds;

            float walkSeconds = (spanLength / SignalPedestrianClearanceAssumedWalkSpeed) + SignalPedestrianClearanceStartupBufferSeconds;
            return Mathf.Clamp(walkSeconds, SignalPedestrianGreenSeconds, SignalPedestrianClearanceHardMaxSeconds);
        }

        private static float GetSignalCrossingSpanLength(BuiltSignalController controller)
        {
            if (controller.PedestrianSpans != null && controller.PedestrianSpans.Count > 0)
            {
                float maxLength = 0f;
                for (int i = 0; i < controller.PedestrianSpans.Count; i++)
                {
                    SignalPedestrianSpan span = controller.PedestrianSpans[i];
                    maxLength = Mathf.Max(maxLength, FlatDistance(span.FirstPosition, span.SecondPosition));
                }

                return maxLength;
            }

            Vector3 flatFirst = controller.FirstPosition;
            Vector3 flatSecond = controller.SecondPosition;
            flatFirst.y = 0f;
            flatSecond.y = 0f;
            return Vector3.Distance(flatFirst, flatSecond);
        }

        private static float DistanceSqrToSignalCrossing(Vector3 position, BuiltSignalController controller)
        {
            if (controller.PedestrianSpans != null && controller.PedestrianSpans.Count > 0)
            {
                float minDistance = float.MaxValue;
                for (int i = 0; i < controller.PedestrianSpans.Count; i++)
                    minDistance = Mathf.Min(minDistance, DistanceSqrToSignalCrossing(position, controller.PedestrianSpans[i]));

                return minDistance;
            }

            return DistanceSqrToSignalCrossing(
                position,
                new SignalPedestrianSpan(
                    controller.FirstPosition,
                    controller.SecondPosition,
                    controller.ConflictFirstPosition,
                    controller.ConflictSecondPosition,
                    NormalizeSignalPedestrianRoadDirection(Vector3.zero, controller.FirstPosition, controller.SecondPosition),
                    false));
        }

        private static float DistanceSqrToSignalCrossing(Vector3 position, SignalPedestrianSpan span)
        {
            Vector3 flatPosition = position;
            Vector3 flatCenter = (span.FirstPosition + span.SecondPosition) * 0.5f;
            flatPosition.y = 0f;
            flatCenter.y = 0f;

            float centerDistance = (flatPosition - flatCenter).sqrMagnitude;
            Vector3 flatFirst = span.FirstPosition;
            Vector3 flatSecond = span.SecondPosition;
            flatFirst.y = 0f;
            flatSecond.y = 0f;
            Vector3 crossingLine = flatSecond - flatFirst;
            float spanLengthSqr = crossingLine.sqrMagnitude;
            if (spanLengthSqr <= 0.01f)
                return centerDistance;

            float t = Mathf.Clamp01(Vector3.Dot(flatPosition - flatFirst, crossingLine) / spanLengthSqr);
            Vector3 nearest = flatFirst + crossingLine * t;
            return Mathf.Min(centerDistance, (flatPosition - nearest).sqrMagnitude);
        }

        private static float DistanceSqrToSignalCrossingBody(Vector3 position, BuiltSignalController controller)
        {
            if (controller.PedestrianSpans != null && controller.PedestrianSpans.Count > 0)
            {
                float minDistance = float.MaxValue;
                for (int i = 0; i < controller.PedestrianSpans.Count; i++)
                    minDistance = Mathf.Min(minDistance, DistanceSqrToSignalCrossingBody(position, controller.PedestrianSpans[i]));

                return minDistance;
            }

            return DistanceSqrToSignalCrossingBody(
                position,
                new SignalPedestrianSpan(
                    controller.FirstPosition,
                    controller.SecondPosition,
                    controller.ConflictFirstPosition,
                    controller.ConflictSecondPosition,
                    NormalizeSignalPedestrianRoadDirection(Vector3.zero, controller.FirstPosition, controller.SecondPosition),
                    false));
        }

        private static float DistanceSqrToSignalCrossingBody(Vector3 position, SignalPedestrianSpan signalSpan)
        {
            Vector3 flatPosition = position;
            Vector3 flatFirst = signalSpan.ConflictFirstPosition;
            Vector3 flatSecond = signalSpan.ConflictSecondPosition;
            flatPosition.y = 0f;
            flatFirst.y = 0f;
            flatSecond.y = 0f;

            Vector3 crossingSpan = flatSecond - flatFirst;
            float spanSqr = crossingSpan.sqrMagnitude;
            if (spanSqr <= 0.01f)
                return (flatPosition - flatFirst).sqrMagnitude;

            float t = Vector3.Dot(flatPosition - flatFirst, crossingSpan) / spanSqr;
            float endMarginT = Mathf.Min(0.25f, SignalPedestrianOnCrossingEndMarginMeters / Mathf.Sqrt(spanSqr));
            if (t < endMarginT || t > 1f - endMarginT)
                return float.MaxValue;

            Vector3 nearest = flatFirst + crossingSpan * t;
            return (flatPosition - nearest).sqrMagnitude;
        }

        private static float DistanceSqrToSignalCrossingSpan(Vector3 position, BuiltSignalController controller)
        {
            if (controller.PedestrianSpans != null && controller.PedestrianSpans.Count > 0)
            {
                float minDistance = float.MaxValue;
                for (int i = 0; i < controller.PedestrianSpans.Count; i++)
                    minDistance = Mathf.Min(minDistance, DistanceSqrToSignalCrossingSpan(position, controller.PedestrianSpans[i]));

                return minDistance;
            }

            return DistanceSqrToSignalCrossingSpan(
                position,
                new SignalPedestrianSpan(
                    controller.FirstPosition,
                    controller.SecondPosition,
                    controller.ConflictFirstPosition,
                    controller.ConflictSecondPosition,
                    NormalizeSignalPedestrianRoadDirection(Vector3.zero, controller.FirstPosition, controller.SecondPosition),
                    false));
        }

        private static float DistanceSqrToSignalCrossingSpan(Vector3 position, SignalPedestrianSpan signalSpan)
        {
            Vector3 flatPosition = position;
            Vector3 flatFirst = signalSpan.ConflictFirstPosition;
            Vector3 flatSecond = signalSpan.ConflictSecondPosition;
            flatPosition.y = 0f;
            flatFirst.y = 0f;
            flatSecond.y = 0f;

            Vector3 span = flatSecond - flatFirst;
            float spanSqr = span.sqrMagnitude;
            if (spanSqr <= 0.01f)
                return (flatPosition - flatFirst).sqrMagnitude;

            float t = Mathf.Clamp01(Vector3.Dot(flatPosition - flatFirst, span) / spanSqr);
            Vector3 nearest = flatFirst + span * t;
            return (flatPosition - nearest).sqrMagnitude;
        }

        private static void SetSignalControllerLights(ref BuiltSignalController controller, bool vehiclesGreen)
        {
            SetSignalControllerStates(
                ref controller,
                vehiclesGreen ? RoadBaseAI.TrafficLightState.Green : RoadBaseAI.TrafficLightState.Red,
                vehiclesGreen ? RoadBaseAI.TrafficLightState.Red : RoadBaseAI.TrafficLightState.Green);
        }

        private static void SetSignalControllerAllRed(ref BuiltSignalController controller)
        {
            SetSignalControllerStates(ref controller, RoadBaseAI.TrafficLightState.Red, RoadBaseAI.TrafficLightState.Red);
        }

        private static void SetSignalControllerStates(ref BuiltSignalController controller, RoadBaseAI.TrafficLightState vehicleState, RoadBaseAI.TrafficLightState pedestrianState)
        {
            SetSignalControllerStates(ref controller, vehicleState, pedestrianState, false);
        }

        private static void SetSignalControllerStates(ref BuiltSignalController controller, RoadBaseAI.TrafficLightState vehicleState, RoadBaseAI.TrafficLightState pedestrianState, bool force)
        {
            if (!force
                && controller.HasAppliedSignalState
                && controller.AppliedVehicleState == vehicleState
                && controller.AppliedPedestrianState == pedestrianState
                && controller.SignalStateRefreshTime < SignalControllerStateRefreshSeconds)
            {
                return;
            }

            NetManager netManager = NetManager.instance;
            if (netManager == null || controller.NodeId == 0 || controller.NodeId >= netManager.m_nodes.m_size)
                return;

            ref NetNode node = ref netManager.m_nodes.m_buffer[controller.NodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return;

            node.m_flags |= NetNode.Flags.Junction;
            node.m_flags |= NetNode.Flags.TrafficLights;

            bool hasRegisteredRoadSegments = HasRegisteredSignalRoadSegmentAtNode(ref controller, netManager);
            int segmentCount = node.CountSegments();
            uint frame = GetCurrentSimulationFrame();
            for (int i = 0; i < segmentCount; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                if (hasRegisteredRoadSegments && !controller.RoadSegmentIds.Contains(segmentId))
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                    continue;

                if (!HasVehicleLane(segment.Info))
                    continue;

                RoadBaseAI.SetTrafficLightState(controller.NodeId, ref segment, frame, vehicleState, pedestrianState, true, true);
                RoadBaseAI.SetTrafficLightState(controller.NodeId, ref segment, frame + 256u, vehicleState, pedestrianState, true, true);
                bool startNode = segment.m_startNode == controller.NodeId;
                TrafficManagerPedestrianCrossingIntegration.SetSignalLightState(controller.NodeId, segmentId, startNode, vehicleState, pedestrianState);
                SetSignalGateFlags(ref segment, startNode, true, true);
            }

            controller.SignalStateRefreshTime = -GetSignalControllerStagger(controller.AssetId, controller.NodeId, SignalControllerStateRefreshSeconds);
            controller.HasAppliedSignalState = true;
            controller.AppliedVehicleState = vehicleState;
            controller.AppliedPedestrianState = pedestrianState;
        }

        private static void SetSignalGateFlags(ref NetSegment segment, bool startNode, bool vehicles, bool pedestrians)
        {
            NetSegment.Flags vehicleFlag = startNode ? NetSegment.Flags.TrafficStart : NetSegment.Flags.TrafficEnd;
            NetSegment.Flags pedestrianFlag = startNode ? NetSegment.Flags.CrossingStart : NetSegment.Flags.CrossingEnd;
            if (vehicles)
                segment.m_flags |= vehicleFlag;
            else
                segment.m_flags &= ~vehicleFlag;

            if (pedestrians)
                segment.m_flags |= pedestrianFlag;
            else
                segment.m_flags &= ~pedestrianFlag;
        }

        private static bool TryGetSignalControllerLiveState(
            ref BuiltSignalController controller,
            out RoadBaseAI.TrafficLightState vehicleState,
            out RoadBaseAI.TrafficLightState pedestrianState,
            out bool vehicles,
            out bool pedestrians)
        {
            vehicleState = RoadBaseAI.TrafficLightState.Red;
            pedestrianState = RoadBaseAI.TrafficLightState.Red;
            vehicles = false;
            pedestrians = false;

            NetManager netManager = NetManager.instance;
            if (netManager == null || controller.NodeId == 0 || controller.NodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[controller.NodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return false;

            bool hasRegisteredRoadSegments = HasRegisteredSignalRoadSegmentAtNode(ref controller, netManager);
            uint frame = GetCurrentSimulationFrame();
            int segmentCount = node.CountSegments();
            for (int i = 0; i < segmentCount; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                if (hasRegisteredRoadSegments && !controller.RoadSegmentIds.Contains(segmentId))
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || !HasVehicleLane(segment.Info))
                    continue;

                RoadBaseAI.GetTrafficLightState(
                    controller.NodeId,
                    ref segment,
                    frame,
                    out vehicleState,
                    out pedestrianState,
                    out vehicles,
                    out pedestrians);
                return true;
            }

            return false;
        }

        private static bool IsSignalControllerNodeJunction(ref BuiltSignalController controller)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null || controller.NodeId == 0 || controller.NodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[controller.NodeId];
            return (node.m_flags & NetNode.Flags.Junction) != 0;
        }

        private static uint GetCurrentSimulationFrame()
        {
            SimulationManager simulationManager = SimulationManager.instance;
            return simulationManager == null ? 0u : simulationManager.m_currentFrameIndex;
        }

        private static bool HasRegisteredSignalRoadSegmentAtNode(ref BuiltSignalController controller, NetManager netManager)
        {
            if (controller.RoadSegmentIds == null || controller.RoadSegmentIds.Count == 0)
                return false;

            if (netManager == null || controller.NodeId == 0 || controller.NodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[controller.NodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return false;

            int segmentCount = node.CountSegments();
            for (int i = 0; i < segmentCount; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0 && controller.RoadSegmentIds.Contains(segmentId))
                    return true;
            }

            return false;
        }

        private static bool SetBuiltSignalNodeFlag(ushort nodeId, NetNode.Flags flag, bool enabled)
        {
            return SetBuiltSignalNodeFlag(nodeId, flag, enabled, true);
        }

        private static bool SetBuiltSignalNodeFlag(ushort nodeId, NetNode.Flags flag, bool enabled, bool updateRenderer)
        {
            NetManager netManager = NetManager.instance;
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return false;

            if (!BuiltSignalNodeSnapshots.ContainsKey(nodeId))
                BuiltSignalNodeSnapshots.Add(nodeId, node.m_flags);

            NetNode.Flags beforeFlags = node.m_flags;
            if (enabled)
                node.m_flags |= flag;
            else
                node.m_flags &= ~flag;

            if (beforeFlags == node.m_flags)
                return true;

            netManager.UpdateNodeFlags(nodeId);
            if (updateRenderer)
                netManager.UpdateNodeRenderer(nodeId, true);
            return true;
        }

        private static bool SetBuiltSignalSurfaceCrossings(ushort nodeId, bool enabled)
        {
            return SetBuiltSignalSurfaceCrossings(nodeId, enabled, true);
        }

        private static bool SetBuiltSignalSurfaceCrossings(ushort nodeId, bool enabled, bool updateRenderer)
        {
            NetManager netManager = NetManager.instance;
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return false;

            bool changed = false;
            int segmentCount = node.CountSegments();
            for (int i = 0; i < segmentCount; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                    continue;

                if (!HasVehicleLane(segment.Info))
                    continue;

                NetSegment.Flags crossingFlag;
                NetSegment.Flags trafficFlag;
                bool startNode;
                if (segment.m_startNode == nodeId)
                {
                    crossingFlag = NetSegment.Flags.CrossingStart;
                    trafficFlag = NetSegment.Flags.TrafficStart;
                    startNode = true;
                }
                else if (segment.m_endNode == nodeId)
                {
                    crossingFlag = NetSegment.Flags.CrossingEnd;
                    trafficFlag = NetSegment.Flags.TrafficEnd;
                    startNode = false;
                }
                else
                    continue;

                if (!BuiltSignalSegmentSnapshots.ContainsKey(segmentId))
                    BuiltSignalSegmentSnapshots.Add(segmentId, segment.m_flags);

                NetSegment.Flags beforeFlags = segment.m_flags;
                if (enabled)
                {
                    segment.m_flags |= crossingFlag;
                    segment.m_flags |= trafficFlag;
                }
                else
                {
                    segment.m_flags &= ~crossingFlag;
                    segment.m_flags &= ~trafficFlag;
                }

                bool tmpeChanged = TrafficManagerPedestrianCrossingIntegration.SetPedestrianCrossingAllowed(segmentId, startNode, enabled);
                if (beforeFlags != segment.m_flags || tmpeChanged)
                {
                    netManager.UpdateSegmentFlags(segmentId);
                    if (updateRenderer)
                        netManager.UpdateSegmentRenderer(segmentId, true);
                    changed = true;
                }
            }

            if (changed)
            {
                netManager.UpdateNodeFlags(nodeId);
                if (updateRenderer)
                    netManager.UpdateNodeRenderer(nodeId, true);
            }

            return changed;
        }

        private static void AddSubwayEntranceBlock(int assetId, Vector3 center, Quaternion rotation, Vector3 scale, string role, Material material)
        {
            GameObject block = AddTrackedCube(
                "PCT Subway Entrance " + role + " #" + assetId,
                center,
                rotation,
                scale,
                material ?? GetBridgeConcreteMaterial(),
                ShouldSubwayEntranceBlockCims(role));
            ConfigureSubwayEntranceSurfaceRenderer(block.GetComponent<Renderer>());
        }

        private static bool ShouldSubwayEntranceBlockCims(string role)
        {
            return role == "side-left"
                   || role == "side-right"
                   || role.IndexOf("doorpost", StringComparison.OrdinalIgnoreCase) >= 0
                   || role.IndexOf("door-lintel", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddBridgeConcrete(int assetId, Vector3 start, Vector3 end, float width, float startElevation, float endElevation, float thickness, string role)
        {
            Vector3 raisedStart = start + Vector3.up * startElevation;
            Vector3 raisedEnd = end + Vector3.up * endElevation;
            Vector3 span = raisedEnd - raisedStart;
            if (span.sqrMagnitude < 0.01f)
                return;

            if (role == "access")
            {
                AddBridgeConcretePrism(assetId, raisedStart, raisedEnd, width, thickness, role);
                AddBridgeGlassSides(assetId, raisedStart, raisedEnd, width, thickness, role);
                return;
            }

            float length = span.magnitude;
            Vector3 direction = span / length;
            Vector3 center = (raisedStart + raisedEnd) * 0.5f + Vector3.up * (thickness * 0.5f);

            GameObject concrete = GameObject.CreatePrimitive(PrimitiveType.Cube);
            concrete.name = "PCT Bridge Concrete " + role + " #" + assetId;
            concrete.transform.position = center;
            concrete.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            concrete.transform.localScale = new Vector3(width, thickness, length);
            Renderer renderer = concrete.GetComponent<Renderer>();
            renderer.material = GetBridgeDeckMaterial();
            ConfigureBridgeCosmeticRenderer(renderer);
            BuiltBridgeConcreteObjects.Add(concrete);

            AddBridgeGlassSides(assetId, raisedStart, raisedEnd, width, thickness, role);
        }

        private static void AddBridgeConcretePrism(int assetId, Vector3 raisedStart, Vector3 raisedEnd, float width, float thickness, string role)
        {
            Vector3 span = raisedEnd - raisedStart;
            span.y = 0f;
            if (span.sqrMagnitude < 0.01f)
                return;

            span.Normalize();
            Vector3 normal = new Vector3(-span.z, 0f, span.x) * (width * 0.5f);
            Vector3 bottomStartLeft = raisedStart - normal;
            Vector3 bottomStartRight = raisedStart + normal;
            Vector3 bottomEndLeft = raisedEnd - normal;
            Vector3 bottomEndRight = raisedEnd + normal;
            Vector3 topStartLeft = bottomStartLeft + Vector3.up * thickness;
            Vector3 topStartRight = bottomStartRight + Vector3.up * thickness;
            Vector3 topEndLeft = bottomEndLeft;
            Vector3 topEndRight = bottomEndRight;

            GameObject concrete = new GameObject("PCT Bridge Concrete " + role + " #" + assetId);
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
            {
                topStartLeft, topStartRight, topEndRight, topEndLeft,
                bottomStartLeft, bottomStartRight, bottomEndRight, bottomEndLeft
            };
            mesh.triangles = new[]
            {
                0, 1, 2, 0, 2, 3,
                4, 6, 5, 4, 7, 6,
                0, 4, 5, 0, 5, 1,
                3, 2, 6, 3, 6, 7,
                1, 5, 6, 1, 6, 2,
                0, 3, 7, 0, 7, 4
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter filter = concrete.AddComponent<MeshFilter>();
            filter.mesh = mesh;
            MeshRenderer renderer = concrete.AddComponent<MeshRenderer>();
            renderer.material = GetBridgeConcretePrismMaterial(role);
            ConfigureBridgeCosmeticRenderer(renderer);
            BuiltBridgeConcreteObjects.Add(concrete);
        }

        private static void AddBridgeSupportPillar(CrossingPathWorkOrder order, Vector3 basePosition)
        {
            Vector3 pavementPosition;
            bool pavementAnchored = TryResolveBridgeSupportPavementPosition(order.SegmentId, basePosition, out pavementPosition);
            AddBridgeSupportPillar(order.AssetId, pavementAnchored ? pavementPosition : basePosition, pavementAnchored);
        }

        private static bool TryResolveBridgeSupportPavementPosition(ushort segmentId, Vector3 basePosition, out Vector3 resolvedPosition)
        {
            resolvedPosition = Vector3.zero;
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
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

            Vector3 start = netManager.m_nodes.m_buffer[segment.m_startNode].m_position;
            Vector3 end = netManager.m_nodes.m_buffer[segment.m_endNode].m_position;
            Vector3 roadVector = end - start;
            roadVector.y = 0f;
            float roadLength = roadVector.magnitude;
            if (roadLength <= 0.01f)
                return false;

            Vector3 roadDirection = roadVector / roadLength;
            Vector3 roadRight = new Vector3(-roadDirection.z, 0f, roadDirection.x);
            float along = Mathf.Clamp(Vector3.Dot(basePosition - start, roadDirection), 0f, roadLength);
            Vector3 roadCenter = start + roadDirection * along;
            float lateral = Vector3.Dot(basePosition - roadCenter, roadRight);

            float lanePosition;
            if (!RoadPavementAnchorResolver.TryGetNearestPedestrianLanePosition(segment.Info, lateral, out lanePosition))
                return false;

            resolvedPosition = roadCenter + roadRight * lanePosition;
            resolvedPosition.y = Mathf.Lerp(start.y, end.y, along / roadLength);

            if (HorizontalDistance(resolvedPosition, basePosition) > 0.25f)
            {
                Debug.Log("[PedestrianCrossingToolkit] Bridge support anchored to pedestrian lane: segment="
                          + segmentId
                          + " lanePosition="
                          + lanePosition.ToString("0.00")
                          + " requested="
                          + basePosition
                          + " resolved="
                          + resolvedPosition);
            }

            return true;
        }

        private static void AddBridgeSupportPillar(int assetId, Vector3 basePosition, bool pavementAnchored)
        {
            if (!pavementAnchored && RoadSurfacePlacementGuard.IsOnRoadSurface(basePosition))
            {
                Debug.Log("[PedestrianCrossingToolkit] Bridge support pillar skipped on road surface: asset="
                          + assetId
                          + " position="
                          + basePosition);
                return;
            }

            float height = Mathf.Max(0.5f, CrossingVerticalProfile.BridgeDeckHeight);
            basePosition.y -= BridgeAccessFootGroundDrop;
            height += BridgeAccessFootGroundDrop;
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "PCT Bridge Concrete pillar #" + assetId;
            pillar.transform.position = basePosition + Vector3.up * (height * 0.5f);
            pillar.transform.localScale = new Vector3(BridgeSupportPillarWidth, height, BridgeSupportPillarWidth);
            Renderer renderer = pillar.GetComponent<Renderer>();
            renderer.material = GetBridgeSupportMaterial();
            ConfigureBridgeCosmeticRenderer(renderer);
            BuiltBridgeConcreteObjects.Add(pillar);
        }

        private static void AddBridgeGlassSides(int assetId, Vector3 raisedStart, Vector3 raisedEnd, float width, float deckThickness, string role)
        {
            Vector3 span = raisedEnd - raisedStart;
            if (span.sqrMagnitude < 0.01f)
                return;

            float length = span.magnitude;
            Vector3 direction = span / length;
            Vector3 normal = new Vector3(-direction.z, 0f, direction.x);
            if (normal.sqrMagnitude <= 0.01f)
                return;

            normal.Normalize();
            if (role == "access")
            {
                float accessSideOffset = width * 0.5f;
                AddBridgeAccessCoveredSideRun(assetId, raisedStart, raisedEnd, normal * accessSideOffset, deckThickness, role);
                AddBridgeAccessCoveredSideRun(assetId, raisedStart, raisedEnd, -normal * accessSideOffset, deckThickness, role);
                AddBridgeAccessRoofRun(assetId, raisedStart, raisedEnd, width, deckThickness, role);
                return;
            }

            float deckSideOffset = Mathf.Max(0.01f, width * 0.5f - BridgeWallThickness * 0.5f);
            AddBridgeCoveredSideRun(assetId, raisedStart, raisedEnd, normal * deckSideOffset, direction, deckThickness, role);
            AddBridgeCoveredSideRun(assetId, raisedStart, raisedEnd, -normal * deckSideOffset, direction, deckThickness, role);
            if (role == "deck")
            {
                AddBridgeDeckEndWall(assetId, raisedStart, normal, width, deckThickness, role + " start");
                AddBridgeDeckEndWall(assetId, raisedEnd, normal, width, deckThickness, role + " end");
            }

            AddBridgeRoofRun(assetId, raisedStart, raisedEnd, width, deckThickness, role);
        }

        private static void AddBridgeAccessCoveredSideRun(int assetId, Vector3 start, Vector3 end, Vector3 sideOffset, float deckThickness, string role)
        {
            if (Vector3.Distance(start, end) < 0.1f)
                return;

            Vector3 baseStart = start + sideOffset + Vector3.up * deckThickness;
            Vector3 baseEnd = end + sideOffset + Vector3.up * deckThickness;
            AddBridgeTrapezoidWallBand(assetId, baseStart, baseEnd, BridgeWindowSillHeight * 0.5f, BridgeWindowSillHeight, role + " lower wall");

            float upperBandHeight = Mathf.Max(0.1f, BridgeWallHeight - BridgeWindowSillHeight - BridgeWindowHeight);
            AddBridgeTrapezoidWallBand(
                assetId,
                baseStart,
                baseEnd,
                BridgeWindowSillHeight + BridgeWindowHeight + (upperBandHeight * 0.5f),
                upperBandHeight,
                role + " upper wall");

            AddBridgeTrapezoidWindowRun(assetId, baseStart, baseEnd, role);
        }

        private static void AddBridgeCoveredSideRun(int assetId, Vector3 start, Vector3 end, Vector3 sideOffset, Vector3 direction, float deckThickness, string role)
        {
            if (Vector3.Distance(start, end) < 0.1f)
                return;

            Vector3 baseStart = start + sideOffset + Vector3.up * deckThickness;
            Vector3 baseEnd = end + sideOffset + Vector3.up * deckThickness;
            AddBridgeWallBand(assetId, baseStart, baseEnd, BridgeWindowSillHeight * 0.5f, BridgeWindowSillHeight, role + " lower wall");

            float upperBandHeight = Mathf.Max(0.1f, BridgeWallHeight - BridgeWindowSillHeight - BridgeWindowHeight);
            AddBridgeWallBand(
                assetId,
                baseStart,
                baseEnd,
                BridgeWindowSillHeight + BridgeWindowHeight + (upperBandHeight * 0.5f),
                upperBandHeight,
                role + " upper wall");

            AddBridgeWindowRun(assetId, baseStart, baseEnd, role);
        }

        private static void AddBridgeWallBand(int assetId, Vector3 baseStart, Vector3 baseEnd, float centerHeight, float height, string role)
        {
            Vector3 span = baseEnd - baseStart;
            if (span.sqrMagnitude < 0.0001f || height <= 0.01f)
                return;

            float length = span.magnitude;
            Vector3 direction = span / length;
            AddBridgeShellCube(
                "PCT Bridge Metal " + role + " #" + assetId,
                (baseStart + baseEnd) * 0.5f + Vector3.up * centerHeight,
                Quaternion.FromToRotation(Vector3.forward, direction),
                new Vector3(BridgeWallThickness, height, length),
                GetBridgeWallMaterial());
        }

        private static void AddBridgeTrapezoidWallBand(int assetId, Vector3 baseStart, Vector3 baseEnd, float centerHeight, float height, string role)
        {
            Vector3 span = baseEnd - baseStart;
            if (span.sqrMagnitude < 0.0001f || height <= 0.01f)
                return;

            Vector3 bottomStart = baseStart + Vector3.up * (centerHeight - height * 0.5f);
            Vector3 bottomEnd = baseEnd + Vector3.up * (centerHeight - height * 0.5f);
            AddBridgeTrapezoidPanel(
                "PCT Bridge Metal " + role + " #" + assetId,
                bottomStart,
                bottomEnd,
                bottomEnd + Vector3.up * height,
                bottomStart + Vector3.up * height,
                role.StartsWith("access ") ? GetBridgeAccessWallMaterial() : GetBridgeWallMaterial());
        }

        private static void AddBridgeWindowRun(int assetId, Vector3 baseStart, Vector3 baseEnd, string role)
        {
            Vector3 span = baseEnd - baseStart;
            if (span.sqrMagnitude < 0.0001f)
                return;

            float length = span.magnitude;
            Vector3 direction = span / length;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, direction);
            int windows = Mathf.Max(1, Mathf.FloorToInt(length / BridgeWindowSpacing));
            float bayLength = length / windows;
            float windowWidth = GetBridgeWindowWidth(bayLength);
            float windowHeightCenter = BridgeWindowSillHeight + (BridgeWindowHeight * 0.5f);

            for (int i = 0; i <= windows; i++)
            {
                Vector3 postCenter = baseStart + direction * Mathf.Min(length, i * bayLength);
                AddBridgeShellCube(
                    "PCT Bridge Metal " + role + " mullion #" + assetId,
                    postCenter + Vector3.up * (BridgeWallHeight * 0.5f),
                    rotation,
                    new Vector3(BridgeWallThickness * 1.15f, BridgeWallHeight, BridgeWallPostWidth),
                    GetBridgeTrimMaterial());
            }

            for (int i = 0; i < windows; i++)
            {
                Vector3 windowCenter = baseStart + direction * ((i + 0.5f) * bayLength);
                AddBridgeShellCube(
                    "PCT Bridge Window " + role + " #" + assetId,
                    windowCenter + Vector3.up * windowHeightCenter,
                    rotation,
                    new Vector3(BridgeGlassThickness, BridgeWindowHeight, windowWidth),
                    GetBridgeGlassMaterial());
            }
        }

        private static void AddBridgeTrapezoidWindowRun(int assetId, Vector3 baseStart, Vector3 baseEnd, string role)
        {
            Vector3 span = baseEnd - baseStart;
            if (span.sqrMagnitude < 0.0001f)
                return;

            float length = span.magnitude;
            Vector3 direction = span / length;
            int windows = Mathf.Max(1, Mathf.FloorToInt(length / BridgeWindowSpacing));
            float bayLength = length / windows;
            float windowWidth = GetBridgeWindowWidth(bayLength);

            for (int i = 0; i <= windows; i++)
            {
                Vector3 postCenter = baseStart + direction * Mathf.Min(length, i * bayLength);
                Vector3 bottomStart = postCenter - direction * (BridgeWallPostWidth * 0.5f);
                Vector3 bottomEnd = postCenter + direction * (BridgeWallPostWidth * 0.5f);
                AddBridgeTrapezoidPanel(
                    "PCT Bridge Metal " + role + " mullion #" + assetId,
                    bottomStart,
                    bottomEnd,
                    bottomEnd + Vector3.up * BridgeWallHeight,
                    bottomStart + Vector3.up * BridgeWallHeight,
                    GetBridgeTrimMaterial());
            }

            for (int i = 0; i < windows; i++)
            {
                Vector3 windowCenter = baseStart + direction * ((i + 0.5f) * bayLength);
                Vector3 bottomStart = windowCenter - direction * (windowWidth * 0.5f) + Vector3.up * BridgeWindowSillHeight;
                Vector3 bottomEnd = windowCenter + direction * (windowWidth * 0.5f) + Vector3.up * BridgeWindowSillHeight;
                AddBridgeTrapezoidPanel(
                    "PCT Bridge Window " + role + " #" + assetId,
                    bottomStart,
                    bottomEnd,
                    bottomEnd + Vector3.up * BridgeWindowHeight,
                    bottomStart + Vector3.up * BridgeWindowHeight,
                    GetBridgeGlassMaterial());
            }
        }

        private static void AddBridgeDeckEndWall(int assetId, Vector3 center, Vector3 normal, float width, float deckThickness, string role)
        {
            if (normal.sqrMagnitude <= 0.0001f)
                return;

            normal.Normalize();
            Vector3 baseStart = center - normal * (width * 0.5f) + Vector3.up * deckThickness;
            Vector3 baseEnd = center + normal * (width * 0.5f) + Vector3.up * deckThickness;
            AddBridgeWallBand(assetId, baseStart, baseEnd, BridgeWindowSillHeight * 0.5f, BridgeWindowSillHeight, role + " lower wall");

            float upperBandHeight = Mathf.Max(0.1f, BridgeWallHeight - BridgeWindowSillHeight - BridgeWindowHeight);
            AddBridgeWallBand(
                assetId,
                baseStart,
                baseEnd,
                BridgeWindowSillHeight + BridgeWindowHeight + (upperBandHeight * 0.5f),
                upperBandHeight,
                role + " upper wall");

            AddBridgeWindowRun(assetId, baseStart, baseEnd, role);
        }

        private static float GetBridgeWindowWidth(float bayLength)
        {
            return Mathf.Max(0.45f, bayLength - BridgeWallPostWidth - BridgeWindowFrameInset * 2f);
        }

        private static void ConfigureBridgeCosmeticRenderer(Renderer renderer)
        {
            if (renderer == null)
                return;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void ConfigureSubwayEntranceSurfaceRenderer(Renderer renderer)
        {
            ConfigureBridgeCosmeticRenderer(renderer);
            if (renderer == null)
                return;

            if (renderer.gameObject.GetComponent<SubwayEntranceInfoViewVisibility>() == null)
                renderer.gameObject.AddComponent<SubwayEntranceInfoViewVisibility>();
        }

        private static void ConfigureBridgeShellRenderer(Renderer renderer)
        {
            ConfigureBridgeCosmeticRenderer(renderer);
        }

        private static GameObject AddBridgeShellCube(string name, Vector3 center, Quaternion rotation, Vector3 scale, Material material)
        {
            GameObject obj = AddTrackedCube(name, center, rotation, scale, material ?? GetBridgeMetalMaterial());
            ConfigureBridgeShellRenderer(obj.GetComponent<Renderer>());
            return obj;
        }

        private static void AddBridgeTrapezoidPanel(string name, Vector3 bottomStart, Vector3 bottomEnd, Vector3 topEnd, Vector3 topStart, Material material)
        {
            GameObject panel = new GameObject(name);
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
            {
                bottomStart,
                bottomEnd,
                topEnd,
                topStart,
                bottomStart,
                bottomEnd,
                topEnd,
                topStart
            };
            mesh.triangles = new[]
            {
                0, 1, 2, 0, 2, 3,
                4, 6, 5, 4, 7, 6
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter filter = panel.AddComponent<MeshFilter>();
            filter.mesh = mesh;
            MeshRenderer renderer = panel.AddComponent<MeshRenderer>();
            renderer.material = material ?? GetBridgeMetalMaterial();
            ConfigureBridgeShellRenderer(renderer);
            BuiltBridgeConcreteObjects.Add(panel);
        }

        private static void AddBridgeRoofRun(int assetId, Vector3 raisedStart, Vector3 raisedEnd, float width, float deckThickness, string role)
        {
            Vector3 start = raisedStart;
            Vector3 end = raisedEnd;
            Vector3 span = end - start;
            if (span.sqrMagnitude < 0.0001f)
                return;

            Vector3 roofDirection = end - start;
            roofDirection.y = 0f;

            if (roofDirection.sqrMagnitude <= 0.0001f)
                return;

            roofDirection.Normalize();
            Vector3 flatSpan = end - start;
            flatSpan.y = 0f;
            float length = flatSpan.magnitude;
            if (length <= 0.01f)
                return;

            AddBridgeShellCube(
                "PCT Bridge Metal roof " + role + " #" + assetId,
                (start + end) * 0.5f + Vector3.up * (deckThickness + BridgeWallHeight + BridgeRoofThickness * 0.5f),
                Quaternion.FromToRotation(Vector3.forward, roofDirection),
                new Vector3(width + BridgeRoofOverhang * 2f, BridgeRoofThickness, length),
                GetBridgeRoofMaterial());
        }

        private static void AddBridgeAccessRoofRun(int assetId, Vector3 raisedStart, Vector3 raisedEnd, float width, float deckThickness, string role)
        {
            Vector3 span = raisedEnd - raisedStart;
            if (span.sqrMagnitude < 0.0001f)
                return;

            Vector3 roofDirection = span.normalized;
            float length = span.magnitude;
            AddBridgeShellCube(
                "PCT Bridge Metal roof " + role + " #" + assetId,
                (raisedStart + raisedEnd) * 0.5f + Vector3.up * (deckThickness + BridgeWallHeight + BridgeRoofThickness * 0.5f),
                Quaternion.LookRotation(roofDirection, Vector3.up),
                new Vector3(width + BridgeRoofOverhang * 2f, BridgeRoofThickness, length),
                GetBridgeRoofMaterial());
        }

        private static GameObject AddTrackedCube(string name, Vector3 center, Quaternion rotation, Vector3 scale, Material material, bool keepCollider = false)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.position = center;
            obj.transform.rotation = rotation;
            obj.transform.localScale = scale;
            Renderer renderer = obj.GetComponent<Renderer>();
            renderer.material = material ?? GetBridgeConcreteMaterial();
            if (name.StartsWith("PCT Bridge "))
                ConfigureBridgeCosmeticRenderer(renderer);

            Collider collider = obj.GetComponent<Collider>();
            if (collider != null && !keepCollider)
                UnityEngine.Object.Destroy(collider);

            BuiltBridgeConcreteObjects.Add(obj);
            return obj;
        }

        private static GameObject AddTrackedQuad(string name, Vector3 center, Quaternion rotation, float length, float width, Material material)
        {
            GameObject obj = new GameObject(name);
            obj.transform.position = center;
            obj.transform.rotation = rotation;

            float halfLength = Mathf.Max(0.01f, length * 0.5f);
            float halfWidth = Mathf.Max(0.01f, width * 0.5f);
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-halfLength, 0f, -halfWidth),
                new Vector3(halfLength, 0f, -halfWidth),
                new Vector3(halfLength, 0f, halfWidth),
                new Vector3(-halfLength, 0f, halfWidth)
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

        private static Material GetCrossingStripeMaterial()
        {
            if (_crossingStripeMaterial != null)
                return _crossingStripeMaterial;

            Shader shader = Shader.Find("Transparent/Diffuse") ?? Shader.Find("Diffuse");
            if (shader == null)
                return GetBridgeConcreteMaterial();

            _crossingStripeMaterial = new Material(shader);
            ConfigureCrossingStripeMaterial(_crossingStripeMaterial);
            return _crossingStripeMaterial;
        }

        private static void ConfigureCrossingStripeMaterial(Material material)
        {
            if (material == null)
                return;

            Color color = new Color(0.94f, 0.93f, 0.86f, 0.5f);
            material.color = color;
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            Texture2D texture = GetCrossingStripeTexture();
            if (texture != null)
            {
                material.mainTexture = texture;
                if (material.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex", texture);
            }
        }

        private static Texture2D GetCrossingStripeTexture()
        {
            if (_crossingStripeTexture != null)
                return _crossingStripeTexture;

            Texture2D texture = new Texture2D(CrossingStripeTextureSize, CrossingStripeTextureSize, TextureFormat.ARGB32, false);
            texture.name = "PCT Crossing Stripe Road Marking";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;

            for (int y = 0; y < CrossingStripeTextureSize; y++)
            {
                for (int x = 0; x < CrossingStripeTextureSize; x++)
                {
                    float u = (x + 0.5f) / CrossingStripeTextureSize;
                    float v = (y + 0.5f) / CrossingStripeTextureSize;
                    float edgeDistance = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    float edgeFade = Mathf.Clamp01(edgeDistance / 0.13f);
                    float coarse = Mathf.PerlinNoise(x * 0.105f + 3.7f, y * 0.095f + 41.2f);
                    float fine = Mathf.PerlinNoise(x * 0.43f + 19.4f, y * 0.39f + 8.6f);
                    float fleck = Hash01((x + 17) * 73856093 ^ (y + 23) * 19349663);
                    float wear = Mathf.Lerp(0.78f, 1f, coarse) * Mathf.Lerp(0.88f, 1.04f, fine);
                    if (fleck < 0.10f)
                        wear *= Mathf.Lerp(0.62f, 0.84f, fleck / 0.10f);

                    float alpha = Mathf.Lerp(0.42f, 0.86f, edgeFade) * Mathf.Lerp(0.84f, 1f, coarse);
                    if (fleck < 0.07f)
                        alpha *= Mathf.Lerp(0.55f, 0.85f, fleck / 0.07f);

                    float value = Mathf.Clamp01(wear);
                    texture.SetPixel(x, y, new Color(value, value * 0.985f, value * 0.92f, Mathf.Clamp01(alpha)));
                }
            }

            texture.Apply(false, true);
            _crossingStripeTexture = texture;
            return _crossingStripeTexture;
        }

        private static float Hash01(int seed)
        {
            unchecked
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                return (seed & 0x7fffffff) / 2147483647f;
            }
        }

        private static Material GetSignalPoleMaterial()
        {
            if (_signalPoleMaterial != null)
                return _signalPoleMaterial;

            Shader shader = Shader.Find("Diffuse");
            if (shader == null)
                return GetBridgeConcreteMaterial();

            _signalPoleMaterial = new Material(shader);
            _signalPoleMaterial.color = new Color(0.14f, 0.14f, 0.13f, 1f);
            return _signalPoleMaterial;
        }

        private static Material GetSignalHeadMaterial()
        {
            if (_signalHeadMaterial != null)
                return _signalHeadMaterial;

            Shader shader = Shader.Find("Diffuse");
            if (shader == null)
                return GetSignalPoleMaterial();

            _signalHeadMaterial = new Material(shader);
            _signalHeadMaterial.color = new Color(0.05f, 0.05f, 0.04f, 1f);
            return _signalHeadMaterial;
        }

        private static Material GetSignalLensMaterial()
        {
            if (_signalLensMaterial != null)
                return _signalLensMaterial;

            Shader shader = Shader.Find("Diffuse");
            if (shader == null)
                return GetSignalHeadMaterial();

            _signalLensMaterial = new Material(shader);
            _signalLensMaterial.color = new Color(0.15f, 0.85f, 0.28f, 1f);
            return _signalLensMaterial;
        }

        private static Material GetSignalRedLensMaterial()
        {
            if (_signalRedLensMaterial != null)
                return _signalRedLensMaterial;

            Shader shader = Shader.Find("Diffuse");
            if (shader == null)
                return GetSignalHeadMaterial();

            _signalRedLensMaterial = new Material(shader);
            _signalRedLensMaterial.color = new Color(0.9f, 0.08f, 0.04f, 1f);
            return _signalRedLensMaterial;
        }

        private static Material GetSignalAmberLensMaterial()
        {
            if (_signalAmberLensMaterial != null)
                return _signalAmberLensMaterial;

            Shader shader = Shader.Find("Diffuse");
            if (shader == null)
                return GetSignalHeadMaterial();

            _signalAmberLensMaterial = new Material(shader);
            _signalAmberLensMaterial.color = new Color(1f, 0.62f, 0.06f, 1f);
            return _signalAmberLensMaterial;
        }

        private static Material GetVergeCrossingMaterial()
        {
            if (_vergeCrossingMaterial != null)
                return _vergeCrossingMaterial;

            Shader shader = GetBridgeCosmeticShader();
            if (shader == null)
                return GetCrossingStripeMaterial();

            _vergeCrossingMaterial = new Material(shader);
            _vergeCrossingMaterial.color = new Color(0.62f, 0.59f, 0.45f, 1f);
            return _vergeCrossingMaterial;
        }

        private static Material GetBridgeConcreteMaterial()
        {
            return GetBridgeDeckMaterial();
        }

        private static Material GetBridgeDeckMaterial()
        {
            return GetBridgeCosmeticMaterial(ref _bridgeConcreteMaterial, new Color(0.24f, 0.25f, 0.23f, 1f));
        }

        private static Material GetBridgeSupportMaterial()
        {
            return GetBridgeAccessStructureMaterial();
        }

        private static Material GetBridgeGlassMaterial()
        {
            Material material = GetBridgeCosmeticMaterial(ref _bridgeGlassMaterial, new Color(0.27f, 0.42f, 0.47f, 1f));
            ConfigureBridgeOpaqueWindowMaterial(material);
            return material;
        }

        private static Material GetBridgeMetalMaterial()
        {
            return GetBridgeTrimMaterial();
        }

        private static Material GetBridgeRoofMaterial()
        {
            return GetBridgeTrimMaterial();
        }

        private static Material GetBridgeWallMaterial()
        {
            return GetBridgeCosmeticMaterial(ref _bridgeMetalMaterial, new Color(0.46f, 0.48f, 0.44f, 1f));
        }

        private static Material GetBridgeAccessWallMaterial()
        {
            return GetBridgeCosmeticMaterial(ref _bridgeAccessWallMaterial, new Color(0.50f, 0.49f, 0.42f, 1f));
        }

        private static Material GetBridgeAccessStructureMaterial()
        {
            return GetBridgeCosmeticMaterial(ref _bridgeAccessStructureMaterial, new Color(0.40f, 0.41f, 0.36f, 1f));
        }

        private static Material GetBridgeConcretePrismMaterial(string role)
        {
            return role == "access" ? GetBridgeAccessStructureMaterial() : GetBridgeDeckMaterial();
        }

        private static Material GetBridgeTrimMaterial()
        {
            return GetBridgeCosmeticMaterial(ref _bridgeTrimMaterial, new Color(0.14f, 0.17f, 0.18f, 1f));
        }

        private static Shader GetBridgeCosmeticShader()
        {
            return Shader.Find("Unlit/Color")
                   ?? Shader.Find("Self-Illumin/Diffuse")
                   ?? Shader.Find("Diffuse");
        }

        private static Material GetBridgeCosmeticMaterial(ref Material material, Color color)
        {
            if (material != null)
                return material;

            Shader shader = GetBridgeCosmeticShader();
            material = new Material(shader);
            ConfigureBridgeCosmeticMaterial(material, color);
            return material;
        }

        private static void ConfigureBridgeCosmeticMaterial(Material material, Color color)
        {
            if (material == null)
                return;

            material.color = color;
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_TintColor"))
                material.SetColor("_TintColor", color);
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", color * 0.35f);
            material.EnableKeyword("_EMISSION");
        }

        private static Material GetSubwayEntranceWallMaterial()
        {
            if (_subwayEntranceWallMaterial != null)
                return _subwayEntranceWallMaterial;

            Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Diffuse");
            if (shader == null)
                return GetBridgeConcreteMaterial();

            _subwayEntranceWallMaterial = new Material(shader);
            _subwayEntranceWallMaterial.color = new Color(0.66f, 0.64f, 0.58f, 1f);
            return _subwayEntranceWallMaterial;
        }

        private static Material GetSubwayOpeningMaterial()
        {
            if (_subwayOpeningMaterial != null)
                return _subwayOpeningMaterial;

            Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Diffuse");
            if (shader == null)
                return GetBridgeConcreteMaterial();

            _subwayOpeningMaterial = new Material(shader);
            _subwayOpeningMaterial.color = new Color(0.35f, 0.36f, 0.34f, 1f);
            return _subwayOpeningMaterial;
        }

        private static Material GetSubwayStepNoseMaterial()
        {
            if (_subwayStepNoseMaterial != null)
                return _subwayStepNoseMaterial;

            Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Diffuse");
            if (shader == null)
                return GetSubwayEntranceWallMaterial();

            _subwayStepNoseMaterial = new Material(shader);
            _subwayStepNoseMaterial.color = new Color(0.78f, 0.68f, 0.36f, 1f);
            return _subwayStepNoseMaterial;
        }

        private static void ConfigureBridgeOpaqueWindowMaterial(Material material)
        {
            if (material == null)
                return;

            if (material.HasProperty("_Mode"))
                material.SetFloat("_Mode", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = -1;
        }

        private static void ClearBridgeConcreteObjects()
        {
            for (int i = BuiltBridgeConcreteObjects.Count - 1; i >= 0; i--)
            {
                GameObject obj = BuiltBridgeConcreteObjects[i];
                if (obj != null)
                    UnityEngine.Object.Destroy(obj);
            }

            BuiltBridgeConcreteObjects.Clear();
        }

        private static void ClearBuiltPathTracking(bool clearSignalSnapshots)
        {
            ClearBridgeConcreteObjects();
            BuiltSegments.Clear();
            BuiltSegmentKinds.Clear();
            BuiltSegmentAssets.Clear();
            BuiltSignalPathSegmentAssets.Clear();
            BuiltTerminalNodeKinds.Clear();
            BuiltNodes.Clear();
            BuiltBridgeDecks.Clear();
            BuiltBridgeAnchorNodes.Clear();
            BuiltPathAnchorNodes.Clear();
            BuiltBridgeFallbackSegments.Clear();
            BuiltSubwayEntranceVisualKeys.Clear();
            BuiltSubwayEntranceVisualAssets.Clear();
            BuiltSubwayEntranceVisualObjects.Clear();
            BuiltSurfaceVisualKeys.Clear();
            PendingSignalControlOrders.Clear();
            BuiltSignalControllers.Clear();
            PendingSignalControllerStateRestores.Clear();
            _pendingSignalControllerStateRestoreReason = string.Empty;
            _lastValidationSummary = BuiltConnectorValidationSummary.Empty;
            _activeBuildAssetId = 0;
            if (clearSignalSnapshots)
            {
                BuiltSignalNodeSnapshots.Clear();
                BuiltSignalSegmentSnapshots.Clear();
            }
        }

        private static void ClearBuiltVisualObjects()
        {
            ClearBridgeConcreteObjects();
            BuiltBridgeDecks.Clear();
            BuiltSubwayEntranceVisualKeys.Clear();
            BuiltSubwayEntranceVisualAssets.Clear();
            BuiltSubwayEntranceVisualObjects.Clear();
            BuiltSurfaceVisualKeys.Clear();
        }

        private static void ClearBuiltVisualObjectsForAsset(int assetId)
        {
            string suffix = "#" + assetId.ToString();
            ClearSubwayEntranceVisualObjectsForAsset(assetId);
            for (int i = BuiltBridgeConcreteObjects.Count - 1; i >= 0; i--)
            {
                GameObject obj = BuiltBridgeConcreteObjects[i];
                if (obj == null)
                {
                    BuiltBridgeConcreteObjects.RemoveAt(i);
                    continue;
                }

                if (obj.name.StartsWith("PCT Subway Entrance ", StringComparison.Ordinal))
                    continue;

                if (!obj.name.EndsWith(suffix, StringComparison.Ordinal))
                    continue;

                UnityEngine.Object.Destroy(obj);
                BuiltBridgeConcreteObjects.RemoveAt(i);
            }

            for (int i = BuiltBridgeDecks.Count - 1; i >= 0; i--)
            {
                if (BuiltBridgeDecks[i].AssetId == assetId)
                    BuiltBridgeDecks.RemoveAt(i);
            }

            RemoveVisualKeysForAsset(BuiltSurfaceVisualKeys, "surface:" + assetId.ToString() + ":");
        }

        private static void ClearSubwayEntranceVisualObjectsForAsset(int assetId)
        {
            if (BuiltSubwayEntranceVisualAssets.Count == 0)
                return;

            List<string> removeKeys = new List<string>();
            foreach (KeyValuePair<string, List<int>> visualAsset in BuiltSubwayEntranceVisualAssets)
            {
                List<int> assetIds = visualAsset.Value;
                if (assetIds == null || !assetIds.Contains(assetId))
                    continue;

                assetIds.Remove(assetId);
                if (assetIds.Count == 0)
                    removeKeys.Add(visualAsset.Key);
            }

            for (int i = 0; i < removeKeys.Count; i++)
            {
                string key = removeKeys[i];
                List<GameObject> objects;
                if (BuiltSubwayEntranceVisualObjects.TryGetValue(key, out objects))
                {
                    for (int j = objects.Count - 1; j >= 0; j--)
                    {
                        GameObject obj = objects[j];
                        if (obj == null)
                            continue;

                        BuiltBridgeConcreteObjects.Remove(obj);
                        UnityEngine.Object.Destroy(obj);
                    }
                }

                BuiltSubwayEntranceVisualObjects.Remove(key);
                BuiltSubwayEntranceVisualAssets.Remove(key);
                BuiltSubwayEntranceVisualKeys.Remove(key);
            }
        }

        private static void RemoveVisualKeysForAsset(HashSet<string> keys, string prefix)
        {
            if (keys.Count == 0)
                return;

            List<string> removeKeys = new List<string>();
            foreach (string key in keys)
            {
                if (key != null && key.StartsWith(prefix, StringComparison.Ordinal))
                    removeKeys.Add(key);
            }

            for (int i = 0; i < removeKeys.Count; i++)
                keys.Remove(removeKeys[i]);
        }

        private static void RevertBuiltSignalControls()
        {
            NetManager netManager = NetManager.instance;
            int nodeCount = 0;
            foreach (KeyValuePair<ushort, NetNode.Flags> snapshot in BuiltSignalNodeSnapshots)
            {
                ushort nodeId = snapshot.Key;
                if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                    continue;

                ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
                if ((node.m_flags & NetNode.Flags.Created) == 0)
                    continue;

                node.m_flags = snapshot.Value;
                netManager.UpdateNodeFlags(nodeId);
                netManager.UpdateNodeRenderer(nodeId, true);
                nodeCount++;
            }

            int segmentCount = 0;
            foreach (KeyValuePair<ushort, NetSegment.Flags> snapshot in BuiltSignalSegmentSnapshots)
            {
                ushort segmentId = snapshot.Key;
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                    continue;

                segment.m_flags = snapshot.Value;
                RestoreSegmentPedestrianCrossingMarkers(segmentId, snapshot.Value);
                netManager.UpdateSegmentFlags(segmentId);
                netManager.UpdateSegmentRenderer(segmentId, true);
                segmentCount++;
            }

            if (nodeCount > 0 || segmentCount > 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Reverted built signal traffic controls: nodes="
                          + nodeCount
                          + " segments="
                          + segmentCount);
            }

            BuiltSignalNodeSnapshots.Clear();
            BuiltSignalSegmentSnapshots.Clear();
        }

        private static bool RestoreSignalRoadStateSnapshot(SignalRoadStateSnapshot snapshot, int assetId, string reason)
        {
            if (!snapshot.HasSnapshot || snapshot.NodeId == 0)
                return false;

            NetManager netManager = NetManager.instance;
            if (netManager == null || snapshot.NodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[snapshot.NodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return false;

            node.m_flags = snapshot.NodeFlags;
            netManager.UpdateNodeFlags(snapshot.NodeId);
            netManager.UpdateNodeRenderer(snapshot.NodeId, true);

            uint frame = GetCurrentSimulationFrame();
            int restoredSegments = 0;
            int segmentCount = snapshot.SegmentCount;
            for (int i = 0; i < segmentCount; i++)
            {
                SignalRoadSegmentState state = snapshot.Segments[i];
                if (state.SegmentId == 0 || state.SegmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[state.SegmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                    continue;

                bool stillAttached = state.StartNode
                    ? segment.m_startNode == snapshot.NodeId
                    : segment.m_endNode == snapshot.NodeId;
                if (!stillAttached)
                    continue;

                segment.m_flags = RestoreSignalSegmentEndFlags(segment.m_flags, state.SegmentFlags, state.StartNode);
                RoadBaseAI.SetTrafficLightState(
                    snapshot.NodeId,
                    ref segment,
                    frame,
                    state.VehicleState,
                    state.PedestrianState,
                    state.Vehicles,
                    state.Pedestrians);
                RoadBaseAI.SetTrafficLightState(
                    snapshot.NodeId,
                    ref segment,
                    frame + 256u,
                    state.VehicleState,
                    state.PedestrianState,
                    state.Vehicles,
                    state.Pedestrians);

                bool crossingAllowed = state.StartNode
                    ? (state.SegmentFlags & NetSegment.Flags.CrossingStart) != 0
                    : (state.SegmentFlags & NetSegment.Flags.CrossingEnd) != 0;
                TrafficManagerPedestrianCrossingIntegration.SetPedestrianCrossingAllowed(state.SegmentId, state.StartNode, crossingAllowed);
                TrafficManagerPedestrianCrossingIntegration.ClearManagedSignalLightState(snapshot.NodeId, state.SegmentId, state.StartNode);
                netManager.UpdateSegmentFlags(state.SegmentId);
                netManager.UpdateSegmentRenderer(state.SegmentId, true);
                restoredSegments++;
            }

            ReapplySignalControllerStates();

            Debug.Log("[PedestrianCrossingToolkit] Restored signal road state snapshot: reason="
                      + reason
                      + " asset="
                      + assetId
                      + " node="
                      + snapshot.NodeId
                      + " segments="
                      + restoredSegments);
            return true;
        }

        private static NetSegment.Flags RestoreSignalSegmentEndFlags(NetSegment.Flags currentFlags, NetSegment.Flags snapshotFlags, bool startNode)
        {
            NetSegment.Flags ownedEndMask = startNode
                ? NetSegment.Flags.TrafficStart | NetSegment.Flags.CrossingStart
                : NetSegment.Flags.TrafficEnd | NetSegment.Flags.CrossingEnd;

            return (currentFlags & ~ownedEndMask) | (snapshotFlags & ownedEndMask);
        }

        private static void RestoreSegmentPedestrianCrossingMarkers(ushort segmentId, NetSegment.Flags flags)
        {
            bool startAllowed = (flags & NetSegment.Flags.CrossingStart) != 0;
            bool endAllowed = (flags & NetSegment.Flags.CrossingEnd) != 0;
            TrafficManagerPedestrianCrossingIntegration.SetPedestrianCrossingAllowed(segmentId, true, startAllowed);
            TrafficManagerPedestrianCrossingIntegration.SetPedestrianCrossingAllowed(segmentId, false, endAllowed);

            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            TrafficManagerPedestrianCrossingIntegration.RestoreSignalLightState(segment.m_startNode, segmentId, true);
            TrafficManagerPedestrianCrossingIntegration.RestoreSignalLightState(segment.m_endNode, segmentId, false);
        }

        private static ToolBase.ToolErrors CreatePath(NetInfo prefab, Vector3 start, Vector3 end, float startElevation, float endElevation, out ushort firstNode, out ushort lastNode, out ushort segment)
        {
            return CreatePath(prefab, start, end, startElevation, endElevation, 0, 0, out firstNode, out lastNode, out segment);
        }

        private static ToolBase.ToolErrors CreatePath(NetInfo prefab, Vector3 start, Vector3 end, float startElevation, float endElevation, ushort startNodeId, ushort endNodeId, out ushort firstNode, out ushort lastNode, out ushort segment)
        {
            Vector3 direction = end - start;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                direction = Vector3.forward;
            else
                direction.Normalize();

            NetTool.ControlPoint startPoint = CreateControlPoint(start, direction, startElevation, startNodeId);
            NetTool.ControlPoint middlePoint = CreateControlPoint((start + end) * 0.5f, direction, (startElevation + endElevation) * 0.5f);
            NetTool.ControlPoint endPoint = CreateControlPoint(end, -direction, endElevation, endNodeId);
            int cost;
            int productionRate;

            return NetTool.CreateNode(
                prefab,
                startPoint,
                middlePoint,
                endPoint,
                new FastList<NetTool.NodePosition>(),
                2,
                false,
                false,
                false,
                true,
                false,
                false,
                false,
                0,
                out firstNode,
                out lastNode,
                out segment,
                out cost,
                out productionRate);
        }

        private static ToolBase.ToolErrors CreateExactPath(NetInfo prefab, Vector3 start, Vector3 end, float startElevation, float endElevation, out ushort firstNode, out ushort lastNode, out ushort segment)
        {
            return CreateExactPath(prefab, start, end, startElevation, endElevation, Vector3.zero, 0, 0, out firstNode, out lastNode, out segment);
        }

        private static ToolBase.ToolErrors CreateExactPath(NetInfo prefab, Vector3 start, Vector3 end, float startElevation, float endElevation, Vector3 fallbackDirection, ushort startNodeId, ushort endNodeId, out ushort firstNode, out ushort lastNode, out ushort segment)
        {
            firstNode = 0;
            lastNode = 0;
            segment = 0;
            if (prefab == null)
                return ToolBase.ToolErrors.CannotConnect;

            NetManager netManager = NetManager.instance;
            SimulationManager simulationManager = SimulationManager.instance;
            if (netManager == null || simulationManager == null)
                return ToolBase.ToolErrors.CannotConnect;

            Vector3 direction = end - start;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = fallbackDirection;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.01f)
                    direction = Vector3.forward;
                else
                    direction.Normalize();
            }
            else
            {
                direction.Normalize();
            }

            Vector3 startPosition = start + Vector3.up * startElevation;
            Vector3 endPosition = end + Vector3.up * endElevation;
            uint buildIndex = simulationManager.m_currentBuildIndex;
            Randomizer randomizer = new Randomizer((uint)(DateTime.UtcNow.Ticks ^ buildIndex));
            bool createdFirstNode = false;
            bool createdLastNode = false;
            firstNode = startNodeId;
            lastNode = endNodeId;

            try
            {
                if (firstNode == 0)
                {
                    if (!netManager.CreateNode(out firstNode, ref randomizer, prefab, startPosition, buildIndex))
                        return ToolBase.ToolErrors.CannotConnect;

                    createdFirstNode = true;
                }

                if (lastNode == 0)
                {
                    if (!netManager.CreateNode(out lastNode, ref randomizer, prefab, endPosition, buildIndex))
                    {
                        if (createdFirstNode)
                            ReleaseUnusedManagedNode(netManager, firstNode);

                        firstNode = 0;
                        return ToolBase.ToolErrors.CannotConnect;
                    }

                    createdLastNode = true;
                }

                if (firstNode == lastNode)
                    return ToolBase.ToolErrors.TooShort;

                if (!netManager.CreateSegment(out segment, ref randomizer, prefab, firstNode, lastNode, direction, -direction, buildIndex, buildIndex, false))
                {
                    if (createdFirstNode)
                        ReleaseUnusedManagedNode(netManager, firstNode);
                    if (createdLastNode)
                        ReleaseUnusedManagedNode(netManager, lastNode);

                    firstNode = 0;
                    lastNode = 0;
                    return ToolBase.ToolErrors.CannotConnect;
                }

                return ToolBase.ToolErrors.None;
            }
            catch (Exception e)
            {
                if (segment != 0)
                    ReleaseSegmentAndUnusedNodes(netManager, segment);
                else
                {
                    if (createdFirstNode)
                        ReleaseUnusedManagedNode(netManager, firstNode);
                    if (createdLastNode)
                        ReleaseUnusedManagedNode(netManager, lastNode);
                }

                firstNode = 0;
                lastNode = 0;
                segment = 0;
                Debug.LogError("[PedestrianCrossingToolkit] Exact path build failed: prefab="
                               + prefab.name
                               + " from="
                               + startPosition
                               + " to="
                               + endPosition
                               + " error="
                               + e);
                return ToolBase.ToolErrors.CannotConnect;
            }
        }

        private static NetTool.ControlPoint CreateControlPoint(Vector3 position, Vector3 direction, float elevation)
        {
            return CreateControlPoint(position, direction, elevation, 0);
        }

        private static NetTool.ControlPoint CreateControlPoint(Vector3 position, Vector3 direction, float elevation, ushort nodeId)
        {
            NetTool.ControlPoint point = new NetTool.ControlPoint();
            point.m_node = nodeId;
            point.m_segment = 0;
            point.m_position = position;
            point.m_direction = direction;
            point.m_elevation = elevation;
            return point;
        }

        private static void RegisterBuiltBridgeAnchorNode(Vector3 position, ushort nodeId)
        {
            if (nodeId == 0)
                return;

            BuiltBridgeAnchorNodes[MakeBridgeAnchorKey(position)] = nodeId;
        }

        private static void RegisterBuiltPathAnchorNode(Vector3 position, ushort nodeId)
        {
            if (nodeId == 0)
                return;

            BuiltPathAnchorNodes[MakeBridgeAnchorKey(position)] = nodeId;
        }

        private static void RegisterBuiltPathAnchorNodesFromSegment(ushort segmentId, Vector3 firstPosition, Vector3 secondPosition, Vector3 firstBuildPosition, Vector3 secondBuildPosition)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                return;

            ushort startNode = segment.m_startNode;
            ushort endNode = segment.m_endNode;
            Vector3 startPosition = GetNodePosition(netManager, startNode);
            Vector3 endPosition = GetNodePosition(netManager, endNode);
            if ((startPosition - firstBuildPosition).sqrMagnitude <= (endPosition - firstBuildPosition).sqrMagnitude)
            {
                RegisterBuiltPathAnchorNode(firstPosition, startNode);
                RegisterBuiltPathAnchorNode(secondPosition, endNode);
            }
            else
            {
                RegisterBuiltPathAnchorNode(firstPosition, endNode);
                RegisterBuiltPathAnchorNode(secondPosition, startNode);
            }
        }

        private static Vector3 GetNodePosition(NetManager netManager, ushort nodeId)
        {
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return Vector3.zero;

            return netManager.m_nodes.m_buffer[nodeId].m_position;
        }

        private static void MarkManagedPathSegmentTerrainSafe(ushort segmentId)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || !IsManagedPathPrefab(segment.Info))
                return;

            segment.m_flags |= NetSegment.Flags.Untouchable;
            MarkManagedPathNodeTerrainSafe(netManager, segment.m_startNode);
            MarkManagedPathNodeTerrainSafe(netManager, segment.m_endNode);
        }

        private static void MarkManagedPathNodeTerrainSafe(NetManager netManager, ushort nodeId)
        {
            if (netManager == null || nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0 || !IsManagedPathPrefab(node.Info))
                return;

            node.m_flags |= NetNode.Flags.Untouchable;
        }

        private static ushort GetNearestSegmentNode(ushort segmentId, Vector3 position)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return 0;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                return 0;

            ushort startNode = segment.m_startNode;
            ushort endNode = segment.m_endNode;
            if (startNode == 0 || endNode == 0 || startNode >= netManager.m_nodes.m_size || endNode >= netManager.m_nodes.m_size)
                return 0;

            Vector3 start = netManager.m_nodes.m_buffer[startNode].m_position;
            Vector3 end = netManager.m_nodes.m_buffer[endNode].m_position;
            return (start - position).sqrMagnitude <= (end - position).sqrMagnitude ? startNode : endNode;
        }

        private static bool TryGetBuiltBridgeAnchorNode(Vector3 position, out ushort nodeId)
        {
            return BuiltBridgeAnchorNodes.TryGetValue(MakeBridgeAnchorKey(position), out nodeId);
        }

        private static bool TryGetBuiltPathAnchorNode(Vector3 position, out ushort nodeId)
        {
            return BuiltPathAnchorNodes.TryGetValue(MakeBridgeAnchorKey(position), out nodeId);
        }

        private static bool TryGetReusablePathAnchorNode(Vector3 position, out ushort nodeId)
        {
            nodeId = 0;
            ushort candidate;
            if (!TryGetBuiltPathAnchorNode(position, out candidate))
                return false;

            NetManager netManager = NetManager.instance;
            if (netManager == null || candidate == 0 || candidate >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[candidate];
            if ((node.m_flags & NetNode.Flags.Created) == 0 || !IsManagedPathPrefab(node.Info))
                return false;

            nodeId = candidate;
            return true;
        }

        private static string MakeBridgeAnchorKey(Vector3 position)
        {
            return Mathf.RoundToInt(position.x * 10f).ToString()
                   + ":"
                   + Mathf.RoundToInt(position.y * 10f).ToString()
                   + ":"
                   + Mathf.RoundToInt(position.z * 10f).ToString();
        }

        private static void AddNode(ushort nodeId)
        {
            if (nodeId == 0 || BuiltNodes.Contains(nodeId))
                return;

            BuiltNodes.Add(nodeId);
        }

        private static void AddTerminalNode(ushort nodeId, CrossingPathWorkOrderKind kind)
        {
            if (nodeId == 0)
                return;

            BuiltTerminalNodeKinds[nodeId] = kind;
        }

        private static bool IsSegmentedSurfacePath(CrossingPathWorkOrderKind kind)
        {
            return kind == CrossingPathWorkOrderKind.SurfacePath
                   || kind == CrossingPathWorkOrderKind.SignalizedSurfacePath;
        }

        private static int ClearSegmentedSurfacePath(CrossingPathWorkOrder order)
        {
            if (!IsSegmentedSurfacePath(order.Kind))
                return 0;

            CrossingPlacementAsset asset;
            if (!CrossingPlacementRegistry.TryGetAssetById(order.AssetId, out asset) || !asset.Plan.IsValid)
                return 0;

            SurfaceCrossingFrame frame;
            if (!TryCreateSurfaceCrossingFrame(order, out frame))
                return 0;

            List<Vector3> points = BuildSurfacePathPoints(asset, frame);
            int removed = 0;
            for (int i = 0; i + 1 < points.Count; i++)
            {
                Vector3 from = points[i];
                Vector3 to = points[i + 1];
                if ((to - from).sqrMagnitude < 0.25f)
                    continue;

                ushort segment;
                if (!TryFindMatchingManagedSegment(order.Prefab, from + frame.PathBuildOffset + Vector3.up * order.VerticalOffset, to + frame.PathBuildOffset + Vector3.up * order.VerticalOffset, out segment))
                    continue;

                ReleaseSegmentAndUnusedNodes(NetManager.instance, segment);
                removed++;
            }

            return removed;
        }

        private static bool TryBuildSegmentedSurfacePath(CrossingPathWorkOrder order, Vector3 pathStart, Vector3 pathEnd, int remainingBudget, out int built, out int skipped)
        {
            built = 0;
            skipped = 0;

            if (!IsSegmentedSurfacePath(order.Kind) || remainingBudget <= 0)
                return false;

            CrossingPlacementAsset asset;
            if (!CrossingPlacementRegistry.TryGetAssetById(order.AssetId, out asset) || !asset.Plan.IsValid)
                return false;

            if (order.Kind == CrossingPathWorkOrderKind.SignalizedSurfacePath
                && ShouldUseVanillaSignalNodeCrossing(asset))
            {
                int removedConnectorSegments = ClearSegmentedSurfacePath(order);
                AddSurfaceCrossingVisuals(order);
                QueueBuiltSignalControl(order);
                Debug.Log("[PedestrianCrossingToolkit] Signal crossing using vanilla node crossing: asset="
                          + order.AssetId
                          + " node="
                          + asset.Plan.TargetNodeId
                          + " orderSegment="
                          + order.SegmentId
                          + " placementSegment="
                          + asset.Placement.SegmentId
                          + " removedConnectorSegments="
                          + removedConnectorSegments
                          + " reason=vanilla-pedestrian-signal-routing");
                return true;
            }

            SurfaceCrossingFrame frame;
            if (!TryCreateSurfaceCrossingFrame(order, out frame))
                return false;

            List<Vector3> points = BuildSurfacePathPoints(asset, frame);
            if (points.Count < 3)
                return false;
            if (points.Count - 1 > remainingBudget)
                return false;

            ushort firstPathNode = 0;
            ushort previousNode = 0;
            ushort lastPathNode = 0;
            bool anySegment = false;

            for (int i = 0; i + 1 < points.Count && i < remainingBudget; i++)
            {
                Vector3 from = points[i];
                Vector3 to = points[i + 1];
                if ((to - from).sqrMagnitude < 0.25f)
                    continue;

                ushort segment;
                if (TryFindMatchingManagedSegment(order.Prefab, from + frame.PathBuildOffset + Vector3.up * order.VerticalOffset, to + frame.PathBuildOffset + Vector3.up * order.VerticalOffset, out segment))
                {
                    AddSegment(segment);
                    BuiltSegmentKinds[segment] = order.Kind;
                    if (order.Kind == CrossingPathWorkOrderKind.SignalizedSurfacePath)
                        BuiltSignalPathSegmentAssets[segment] = order.AssetId;
                    ushort existingFirstNode = GetNearestSegmentNode(segment, from + frame.PathBuildOffset + Vector3.up * order.VerticalOffset);
                    ushort existingLastNode = GetNearestSegmentNode(segment, to + frame.PathBuildOffset + Vector3.up * order.VerticalOffset);
                    AddNode(existingFirstNode);
                    AddNode(existingLastNode);
                    if (firstPathNode == 0)
                        firstPathNode = existingFirstNode;
                    previousNode = existingLastNode;
                    lastPathNode = existingLastNode;
                    RegisterBuiltPathAnchorNode(from, existingFirstNode);
                    RegisterBuiltPathAnchorNode(to, existingLastNode);
                    built++;
                    anySegment = true;
                    continue;
                }

                ushort firstNode;
                ushort lastNode;
                ToolBase.ToolErrors errors = CreatePath(order.Prefab, from + frame.PathBuildOffset, to + frame.PathBuildOffset, order.VerticalOffset, order.VerticalOffset, previousNode, 0, out firstNode, out lastNode, out segment);
                if (errors != ToolBase.ToolErrors.None || segment == 0)
                {
                    skipped++;
                    Debug.Log("[PedestrianCrossingToolkit] Segmented surface path section skipped: asset="
                              + order.AssetId
                              + " kind="
                              + order.Kind
                              + " errors="
                              + errors
                              + " prefab="
                              + order.PrefabName
                              + " from="
                              + from
                              + " to="
                              + to);
                    continue;
                }

                AddSegment(segment);
                BuiltSegmentKinds[segment] = order.Kind;
                if (order.Kind == CrossingPathWorkOrderKind.SignalizedSurfacePath)
                    BuiltSignalPathSegmentAssets[segment] = order.AssetId;
                AddNode(firstNode);
                AddNode(lastNode);
                if (firstPathNode == 0)
                    firstPathNode = firstNode;
                previousNode = lastNode;
                lastPathNode = lastNode;
                RegisterBuiltPathAnchorNode(from, firstNode);
                RegisterBuiltPathAnchorNode(to, lastNode);
                built++;
                anySegment = true;
            }

            if (!anySegment)
                return false;

            if (order.Kind == CrossingPathWorkOrderKind.SurfacePath)
            {
                AddTerminalNode(firstPathNode, order.Kind);
                AddTerminalNode(lastPathNode, order.Kind);
            }

            AddSurfaceCrossingVisuals(order);
            ApplyBuiltSurfaceCrossingControl(order);
            QueueBuiltSignalControl(order);
            Debug.Log("[PedestrianCrossingToolkit] Segmented surface connector path built: asset="
                      + order.AssetId
                      + " kind="
                      + order.Kind
                      + " sections="
                      + (points.Count - 1)
                      + " built="
                      + built
                      + " skipped="
                      + skipped
                      + " prefab="
                      + order.PrefabName);
            return true;
        }

        private static List<Vector3> BuildSurfacePathPoints(CrossingPlacementAsset asset, SurfaceCrossingFrame frame)
        {
            List<Vector3> points = new List<Vector3>();
            Vector3 midpoint = frame.Center;
            float startOffset = Vector3.Dot(frame.First - midpoint, frame.Across);
            float endOffset = Vector3.Dot(frame.Second - midpoint, frame.Across);
            bool increasing = endOffset >= startOffset;
            float minOffset = Mathf.Min(startOffset, endOffset);
            float maxOffset = Mathf.Max(startOffset, endOffset);

            List<float> cuts = new List<float>();
            List<SurfaceMarkingSpan> roadSpans = BuildSurfaceVisualPlan(asset, frame.Across).RoadSpans;
            if (roadSpans.Count == 0)
                roadSpans.Add(new SurfaceMarkingSpan(-Mathf.Max(2f, asset.Plan.Width * 0.5f), Mathf.Max(2f, asset.Plan.Width * 0.5f)));

            for (int i = 0; i < roadSpans.Count; i++)
            {
                AddSurfacePathCut(cuts, roadSpans[i].Min, minOffset, maxOffset);
                AddSurfacePathCut(cuts, roadSpans[i].Max, minOffset, maxOffset);
            }

            cuts.Sort();
            if (!increasing)
                cuts.Reverse();

            points.Add(frame.First);
            for (int i = 0; i < cuts.Count; i++)
                points.Add(GetSurfacePathPoint(frame.First, frame.Second, midpoint, frame.Across, cuts[i], startOffset, endOffset));
            points.Add(frame.Second);
            return points;
        }

        private static void AddSurfacePathCut(List<float> cuts, float offset, float minOffset, float maxOffset)
        {
            if (offset <= minOffset + VergeCrossingConnectorMinLength || offset >= maxOffset - VergeCrossingConnectorMinLength)
                return;

            for (int i = 0; i < cuts.Count; i++)
            {
                if (Mathf.Abs(cuts[i] - offset) < VergeCrossingConnectorMinLength)
                    return;
            }

            cuts.Add(offset);
        }

        private static Vector3 GetSurfacePathPoint(Vector3 pathStart, Vector3 pathEnd, Vector3 midpoint, Vector3 across, float offset, float startOffset, float endOffset)
        {
            Vector3 point = midpoint + across * offset;
            float denominator = endOffset - startOffset;
            float t = Mathf.Abs(denominator) <= 0.01f ? 0.5f : Mathf.Clamp01((offset - startOffset) / denominator);
            point.y = Mathf.Lerp(pathStart.y, pathEnd.y, t);
            return point;
        }

        private static bool TryCreateSurfaceCrossingFrame(CrossingPathWorkOrder order, out SurfaceCrossingFrame frame)
        {
            frame = new SurfaceCrossingFrame();
            if (!IsSegmentedSurfacePath(order.Kind))
                return false;

            Vector3 across = order.SecondPosition - order.FirstPosition;
            across.y = 0f;
            if (across.sqrMagnitude <= 0.01f)
                return false;

            across.Normalize();
            Vector3 roadDirection = new Vector3(-across.z, 0f, across.x);
            Vector3 center = (order.FirstPosition + order.SecondPosition) * 0.5f;
            Vector3 pathBuildOffset = GetSurfacePathLaneOffset(order.Prefab, across, roadDirection);
            frame = new SurfaceCrossingFrame(order.FirstPosition, order.SecondPosition, across, roadDirection, center, pathBuildOffset);
            return true;
        }

        private static Vector3 GetSurfacePathLaneOffset(NetInfo prefab, Vector3 across, Vector3 roadDirection)
        {
            float laneOffset = GetPrefabPedestrianLaneCenterOffset(prefab);
            if (Mathf.Abs(laneOffset) <= 0.01f)
                return Vector3.zero;

            Vector3 side = new Vector3(-roadDirection.z, 0f, roadDirection.x);
            if (Vector3.Dot(side, across) < 0f)
                side = -side;

            return -side.normalized * laneOffset;
        }

        private static Vector3 GetSurfacePathBuildOffset(NetInfo prefab, Vector3 first, Vector3 second)
        {
            Vector3 across = second - first;
            across.y = 0f;
            if (across.sqrMagnitude <= 0.01f)
                return Vector3.zero;

            across.Normalize();
            Vector3 roadDirection = new Vector3(-across.z, 0f, across.x);
            return GetSurfacePathLaneOffset(prefab, across, roadDirection);
        }

        private static float GetPrefabPedestrianLaneCenterOffset(NetInfo prefab)
        {
            if (prefab == null || prefab.m_lanes == null)
                return 0f;

            float total = 0f;
            int count = 0;
            for (int i = 0; i < prefab.m_lanes.Length; i++)
            {
                NetInfo.Lane lane = prefab.m_lanes[i];
                if (lane == null || (lane.m_laneType & NetInfo.LaneType.Pedestrian) == 0)
                    continue;

                total += lane.m_position;
                count++;
            }

            return count == 0 ? 0f : total / count;
        }

        private static void AddSegment(ushort segmentId)
        {
            if (segmentId == 0)
                return;

            MarkManagedPathSegmentTerrainSafe(segmentId);

            if (_activeBuildAssetId > 0)
                AddSegmentAsset(segmentId, _activeBuildAssetId);

            if (!BuiltSegments.Contains(segmentId))
                BuiltSegments.Add(segmentId);
        }

        private static void AddSegmentAsset(ushort segmentId, int assetId)
        {
            List<int> assetIds;
            if (!BuiltSegmentAssets.TryGetValue(segmentId, out assetIds))
            {
                assetIds = new List<int>();
                BuiltSegmentAssets[segmentId] = assetIds;
            }

            if (!assetIds.Contains(assetId))
                assetIds.Add(assetId);
        }

        private static bool AddBuiltBridgeDeckVisual(int assetId, ushort segmentId, Vector3 start, Vector3 end)
        {
            for (int i = 0; i < BuiltBridgeDecks.Count; i++)
            {
                BuiltBridgeDeckVisual deck = BuiltBridgeDecks[i];
                if (deck.AssetId == assetId
                    && HorizontalDistance(deck.Start, start) <= ManagedSegmentMatchTolerance
                    && HorizontalDistance(deck.End, end) <= ManagedSegmentMatchTolerance)
                {
                    return false;
                }
            }

            BuiltBridgeDecks.Add(new BuiltBridgeDeckVisual(assetId, segmentId, start, end, BridgeDeckVisualWidth));
            return true;
        }
    }
}
