using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public static class TrafficManagerPedestrianCrossingIntegration
    {
        private static bool _resolved;
        private static bool _available;
        private static object _junctionRestrictionsManager;
        private static object _trafficLightSimulationManager;
        private static object _customSegmentLightsManager;
        private static MethodInfo _setPedestrianCrossingAllowed;
        private static MethodInfo _isPedestrianCrossingAllowed;
        private static MethodInfo _hasActiveTimedSimulation;
        private static MethodInfo _getSegmentLightsByNode;
        private static MethodInfo _getSegmentLightsByEnd;
        private static MethodInfo _setSegmentLights;
        private static MethodInfo _removeSegmentLight;
        private static MethodInfo _cloneSegmentLights;
        private static MethodInfo _setLights;
        private static MethodInfo _updateVisuals;
        private static PropertyInfo _manualPedestrianMode;
        private static PropertyInfo _pedestrianLightState;
        private static bool _trafficLightInteropAvailable;
        private static readonly Dictionary<long, SignalLightSnapshot> SignalLightSnapshots = new Dictionary<long, SignalLightSnapshot>();

        private struct SignalLightSnapshot
        {
            public readonly ushort NodeId;
            public readonly ushort SegmentId;
            public readonly bool StartNode;
            public readonly object Lights;

            public SignalLightSnapshot(ushort nodeId, ushort segmentId, bool startNode, object lights)
            {
                NodeId = nodeId;
                SegmentId = segmentId;
                StartNode = startNode;
                Lights = lights;
            }
        }

        public static bool IsAvailable
        {
            get
            {
                EnsureResolved();
                return _available;
            }
        }

        public static bool SetPedestrianCrossingAllowed(ushort segmentId, bool startNode, bool allowed)
        {
            if (!PedestrianCrossingToolkitState.TrafficManagerInteropAllowed)
                return false;

            EnsureResolved();
            if (!_available)
                return false;

            try
            {
                object result = _setPedestrianCrossingAllowed.Invoke(
                    _junctionRestrictionsManager,
                    new object[] { segmentId, startNode, allowed });

                return result is bool && (bool)result;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PedestrianCrossingToolkit] TM:PE pedestrian crossing API call failed: segment="
                                 + segmentId
                                 + " startNode="
                                 + startNode
                                 + " allowed="
                                 + allowed
                                 + " error="
                                 + e.GetType().Name
                                 + ": "
                                 + e.Message);
                _available = false;
                return false;
            }
        }

        public static bool TryGetPedestrianCrossingAllowed(ushort segmentId, bool startNode, out bool allowed)
        {
            allowed = false;
            if (!PedestrianCrossingToolkitState.TrafficManagerInteropAllowed)
                return false;

            EnsureResolved();
            if (!_available || _isPedestrianCrossingAllowed == null)
                return false;

            try
            {
                object result = _isPedestrianCrossingAllowed.Invoke(
                    _junctionRestrictionsManager,
                    new object[] { segmentId, startNode });
                if (result is bool)
                {
                    allowed = (bool)result;
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PedestrianCrossingToolkit] TM:PE pedestrian crossing query failed: segment="
                                 + segmentId
                                 + " startNode="
                                 + startNode
                                 + " error="
                                 + e.GetType().Name
                                 + ": "
                                 + e.Message);
            }

            return false;
        }

        public static bool HasActiveTimedSimulation(ushort nodeId)
        {
            EnsureResolved();
            if (!_trafficLightInteropAvailable || _trafficLightSimulationManager == null || _hasActiveTimedSimulation == null)
                return false;

            try
            {
                object result = _hasActiveTimedSimulation.Invoke(_trafficLightSimulationManager, new object[] { nodeId });
                return result is bool && (bool)result;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PedestrianCrossingToolkit] TM:PE timed light query failed: node="
                                 + nodeId
                                 + " error="
                                 + e.GetType().Name
                                 + ": "
                                 + e.Message);
                return false;
            }
        }

        public static bool SetSignalLightState(
            ushort nodeId,
            ushort segmentId,
            bool startNode,
            RoadBaseAI.TrafficLightState vehicleState,
            RoadBaseAI.TrafficLightState pedestrianState)
        {
            if (!PedestrianCrossingToolkitState.TrafficManagerInteropAllowed)
                return false;

            EnsureResolved();
            if (!_trafficLightInteropAvailable || _customSegmentLightsManager == null || _getSegmentLightsByEnd == null || _setLights == null)
                return false;

            try
            {
                long key = MakeSignalLightKey(segmentId, startNode);
                if (!SignalLightSnapshots.ContainsKey(key))
                {
                    object existing = _getSegmentLightsByNode == null
                        ? null
                        : _getSegmentLightsByNode.Invoke(_customSegmentLightsManager, new object[] { nodeId, segmentId });
                    object snapshot = existing != null && _cloneSegmentLights != null
                        ? _cloneSegmentLights.Invoke(existing, new object[] { _customSegmentLightsManager, false })
                        : null;
                    SignalLightSnapshots.Add(key, new SignalLightSnapshot(nodeId, segmentId, startNode, snapshot));
                }

                object lights = _getSegmentLightsByEnd.Invoke(
                    _customSegmentLightsManager,
                    new object[] { segmentId, startNode, true, vehicleState });
                if (lights == null)
                    return false;

                _setLights.Invoke(lights, new object[] { vehicleState });
                if (_manualPedestrianMode != null)
                    _manualPedestrianMode.SetValue(lights, true, null);
                if (_pedestrianLightState != null)
                    _pedestrianLightState.SetValue(lights, pedestrianState, null);
                if (_updateVisuals != null)
                    _updateVisuals.Invoke(lights, null);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PedestrianCrossingToolkit] TM:PE signal light state failed: node="
                                 + nodeId
                                 + " segment="
                                 + segmentId
                                 + " startNode="
                                 + startNode
                                 + " vehicle="
                                 + vehicleState
                                 + " pedestrian="
                                 + pedestrianState
                                 + " error="
                                 + e.GetType().Name
                                 + ": "
                                 + e.Message);
                return false;
            }
        }

        public static bool RestoreSignalLightState(ushort nodeId, ushort segmentId, bool startNode)
        {
            EnsureResolved();
            if (!_trafficLightInteropAvailable || _customSegmentLightsManager == null)
                return false;

            long key = MakeSignalLightKey(segmentId, startNode);
            SignalLightSnapshot snapshot;
            if (!SignalLightSnapshots.TryGetValue(key, out snapshot))
                return false;

            try
            {
                if (snapshot.Lights != null && _setSegmentLights != null)
                {
                    ushort restoreNodeId = snapshot.NodeId != 0 ? snapshot.NodeId : nodeId;
                    _setSegmentLights.Invoke(_customSegmentLightsManager, new object[] { restoreNodeId, snapshot.SegmentId, snapshot.Lights });
                    if (_updateVisuals != null)
                        _updateVisuals.Invoke(snapshot.Lights, null);
                }
                else if (_removeSegmentLight != null)
                {
                    _removeSegmentLight.Invoke(_customSegmentLightsManager, new object[] { snapshot.SegmentId, snapshot.StartNode });
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PedestrianCrossingToolkit] TM:PE signal light restore failed: node="
                                 + nodeId
                                 + " segment="
                                 + segmentId
                                 + " startNode="
                                 + startNode
                                 + " error="
                                 + e.GetType().Name
                                 + ": "
                                 + e.Message);
                return false;
            }
            finally
            {
                SignalLightSnapshots.Remove(key);
            }
        }

        public static bool ClearManagedSignalLightState(ushort nodeId, ushort segmentId, bool startNode)
        {
            if (RestoreSignalLightState(nodeId, segmentId, startNode))
                return true;

            EnsureResolved();
            if (!_trafficLightInteropAvailable || _customSegmentLightsManager == null || _removeSegmentLight == null)
                return false;

            try
            {
                _removeSegmentLight.Invoke(_customSegmentLightsManager, new object[] { segmentId, startNode });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PedestrianCrossingToolkit] TM:PE signal light clear failed: node="
                                 + nodeId
                                 + " segment="
                                 + segmentId
                                 + " startNode="
                                 + startNode
                                 + " error="
                                 + e.GetType().Name
                                 + ": "
                                 + e.Message);
                return false;
            }
        }

        private static void EnsureResolved()
        {
            if (_resolved)
                return;

            _resolved = true;
            try
            {
                Type implementationsType = FindType("TrafficManager.API.Implementations", "TMPE.API");
                if (implementationsType == null)
                    return;

                PropertyInfo managerFactoryProperty = implementationsType.GetProperty("ManagerFactory", BindingFlags.Public | BindingFlags.Static);
                if (managerFactoryProperty == null)
                    return;

                object managerFactory = managerFactoryProperty.GetValue(null, null);
                if (managerFactory == null)
                    return;

                PropertyInfo junctionRestrictionsProperty = managerFactory.GetType().GetProperty("JunctionRestrictionsManager", BindingFlags.Public | BindingFlags.Instance);
                if (junctionRestrictionsProperty == null)
                    return;

                object junctionRestrictionsManager = junctionRestrictionsProperty.GetValue(managerFactory, null);
                if (junctionRestrictionsManager == null)
                    return;

                MethodInfo setPedestrianCrossingAllowed = junctionRestrictionsManager.GetType().GetMethod(
                    "SetPedestrianCrossingAllowed",
                    new Type[] { typeof(ushort), typeof(bool), typeof(bool) });
                if (setPedestrianCrossingAllowed == null)
                    setPedestrianCrossingAllowed = FindInterfaceMethod(
                        junctionRestrictionsManager.GetType(),
                        "TrafficManager.API.Manager.IJunctionRestrictionsManager",
                        "SetPedestrianCrossingAllowed",
                        new Type[] { typeof(ushort), typeof(bool), typeof(bool) });
                if (setPedestrianCrossingAllowed == null)
                    return;

                MethodInfo isPedestrianCrossingAllowed = junctionRestrictionsManager.GetType().GetMethod(
                    "IsPedestrianCrossingAllowed",
                    new Type[] { typeof(ushort), typeof(bool) });
                if (isPedestrianCrossingAllowed == null)
                    isPedestrianCrossingAllowed = FindInterfaceMethod(
                        junctionRestrictionsManager.GetType(),
                        "TrafficManager.API.Manager.IJunctionRestrictionsManager",
                        "IsPedestrianCrossingAllowed",
                        new Type[] { typeof(ushort), typeof(bool) });

                _junctionRestrictionsManager = junctionRestrictionsManager;
                _setPedestrianCrossingAllowed = setPedestrianCrossingAllowed;
                _isPedestrianCrossingAllowed = isPedestrianCrossingAllowed;
                _available = true;
                Debug.Log("[PedestrianCrossingToolkit] TM:PE pedestrian crossing API detected; grade-separated suppression will use TM:PE crossing bans when available.");

                ResolveTrafficLightInterop(managerFactory);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PedestrianCrossingToolkit] TM:PE pedestrian crossing API detection failed: "
                                 + e.GetType().Name
                                 + ": "
                                 + e.Message);
            }
        }

        private static void ResolveTrafficLightInterop(object managerFactory)
        {
            try
            {
                PropertyInfo trafficLightSimulationProperty = managerFactory.GetType().GetProperty("TrafficLightSimulationManager", BindingFlags.Public | BindingFlags.Instance);
                if (trafficLightSimulationProperty != null)
                {
                    _trafficLightSimulationManager = trafficLightSimulationProperty.GetValue(managerFactory, null);
                    if (_trafficLightSimulationManager != null)
                    {
                        _hasActiveTimedSimulation = _trafficLightSimulationManager.GetType().GetMethod(
                            "HasActiveTimedSimulation",
                            new Type[] { typeof(ushort) });
                    }
                }

                PropertyInfo customSegmentLightsProperty = managerFactory.GetType().GetProperty("CustomSegmentLightsManager", BindingFlags.Public | BindingFlags.Instance);
                if (customSegmentLightsProperty == null)
                    return;

                _customSegmentLightsManager = customSegmentLightsProperty.GetValue(managerFactory, null);
                if (_customSegmentLightsManager == null)
                    return;

                Type managerType = _customSegmentLightsManager.GetType();
                _getSegmentLightsByNode = managerType.GetMethod("GetSegmentLights", new Type[] { typeof(ushort), typeof(ushort) });
                _getSegmentLightsByEnd = managerType.GetMethod(
                    "GetSegmentLights",
                    new Type[] { typeof(ushort), typeof(bool), typeof(bool), typeof(RoadBaseAI.TrafficLightState) });
                _setSegmentLights = managerType.GetMethod("SetSegmentLights", new Type[] { typeof(ushort), typeof(ushort), FindType("TrafficManager.TrafficLight.Impl.CustomSegmentLights", "TrafficManager") });
                _removeSegmentLight = managerType.GetMethod("RemoveSegmentLight", new Type[] { typeof(ushort), typeof(bool) });

                Type segmentLightsType = FindType("TrafficManager.TrafficLight.Impl.CustomSegmentLights", "TrafficManager");
                if (segmentLightsType != null)
                {
                    _cloneSegmentLights = segmentLightsType.GetMethod("Clone", new Type[] { FindType("TrafficManager.TrafficLight.Impl.ITrafficLightContainer", "TrafficManager"), typeof(bool) });
                    _setLights = segmentLightsType.GetMethod("SetLights", new Type[] { typeof(RoadBaseAI.TrafficLightState) });
                    _updateVisuals = segmentLightsType.GetMethod("UpdateVisuals", Type.EmptyTypes);
                    _manualPedestrianMode = segmentLightsType.GetProperty("ManualPedestrianMode", BindingFlags.Public | BindingFlags.Instance);
                    _pedestrianLightState = segmentLightsType.GetProperty("PedestrianLightState", BindingFlags.Public | BindingFlags.Instance);
                }

                _trafficLightInteropAvailable = _getSegmentLightsByEnd != null && _setLights != null;
                if (_trafficLightInteropAvailable)
                    Debug.Log("[PedestrianCrossingToolkit] TM:PE signal light API detected; signal crossings will mirror phase state into TM:PE lights.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PedestrianCrossingToolkit] TM:PE signal light API detection failed: "
                                 + e.GetType().Name
                                 + ": "
                                 + e.Message);
            }
        }

        private static long MakeSignalLightKey(ushort segmentId, bool startNode)
        {
            return ((long)segmentId << 1) | (startNode ? 1L : 0L);
        }

        private static Type FindType(string typeName, string assemblyName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null)
                    continue;

                AssemblyName name = assembly.GetName();
                if (name == null || !string.Equals(name.Name, assemblyName, StringComparison.Ordinal))
                    continue;

                Type type = assembly.GetType(typeName, false);
                if (type != null)
                    return type;
            }

            return Type.GetType(typeName + ", " + assemblyName, false);
        }

        private static MethodInfo FindInterfaceMethod(
            Type implementationType,
            string interfaceName,
            string methodName,
            Type[] parameterTypes)
        {
            Type[] interfaces = implementationType.GetInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                Type interfaceType = interfaces[i];
                if (interfaceType == null || interfaceType.FullName != interfaceName)
                    continue;

                return interfaceType.GetMethod(methodName, parameterTypes);
            }

            return null;
        }
    }
}
