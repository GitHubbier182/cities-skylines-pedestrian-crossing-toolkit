using System;
using System.Collections.Generic;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public static partial class CrossingPathBuilder
    {
        // Only dynamically created source meshes use this owner. Primitive and
        // prefab meshes remain shared and must never be disposed here.
        private sealed class SourceVisualMeshOwner : MonoBehaviour
        {
            internal Mesh OwnedMesh;
            private void OnDestroy()
            {
                if (OwnedMesh != null) UnityEngine.Object.Destroy(OwnedMesh);
            }
        }

        private sealed class CombinedVisualMeshOwner : MonoBehaviour
        {
            internal Mesh OwnedMesh;
            private void OnDestroy()
            {
                if (OwnedMesh != null) UnityEngine.Object.Destroy(OwnedMesh);
            }
        }

        private sealed class VisualBatch
        {
            internal readonly List<MeshRenderer> Sources = new List<MeshRenderer>();
            internal int Vertices;
            internal int AssetId;
        }

        // Called only during construction, never from an ordinary rendered update.
        // Subway calls operate inside the exact shared entrance's ownership group.
        private static void ConsolidateVisualRange(int startIndex, bool subwayEntrance)
        {
            if (!_unityVisualLifecycleEnabled) return;
            int endIndex = BuiltBridgeConcreteObjects.Count;
            Dictionary<string, VisualBatch> current = new Dictionary<string, VisualBatch>();
            List<VisualBatch> batches = new List<VisualBatch>();
            try
            {
                for (int i = startIndex; i < endIndex; i++)
                {
                    GameObject obj = BuiltBridgeConcreteObjects[i];
                    if (obj == null || !obj.activeInHierarchy || obj.GetComponent<CombinedVisualMeshOwner>() != null)
                        continue;
                    bool subway = obj.name.StartsWith("PCT Subway Entrance ", StringComparison.Ordinal);
                    if (subway != subwayEntrance) continue;
                    MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
                    MeshFilter filter = obj.GetComponent<MeshFilter>();
                    if (renderer == null || filter == null || filter.sharedMesh == null
                        || obj.GetComponent<CrossingWeatherSurface>() != null) continue;
                    Mesh mesh = filter.sharedMesh;
                    Material material = renderer.sharedMaterial;
                    // Keep transparent sorting, roof weather and lightmapped pieces independent.
                    if (material == null || material.shader == null || material.renderQueue > 2500
                        || material.shader.name.IndexOf("Transparent", StringComparison.OrdinalIgnoreCase) >= 0
                        || renderer.sharedMaterials.Length != 1 || mesh.subMeshCount != 1
                        || !mesh.isReadable || mesh.vertexCount <= 0 || mesh.vertexCount > 60000
                        || renderer.lightmapIndex >= 0 && renderer.lightmapIndex < 65534
                        || obj.transform.localToWorldMatrix.determinant <= 0f) continue;
                    int marker = obj.name.LastIndexOf('#');
                    int assetId;
                    if (marker < 0 || !int.TryParse(obj.name.Substring(marker + 1), out assetId) || assetId <= 0)
                        continue;
                    Vector3 center = renderer.bounds.center;
                    bool infoVisibility = obj.GetComponent<SubwayEntranceInfoViewVisibility>() != null;
                    string key = assetId + ":" + material.GetInstanceID() + ":"
                        + Mathf.FloorToInt(center.x / 32f) + ":" + Mathf.FloorToInt(center.y / 32f) + ":" + Mathf.FloorToInt(center.z / 32f)
                        + ":" + obj.layer + ":" + renderer.enabled + ":" + infoVisibility
                        + ":" + (int)renderer.shadowCastingMode + ":" + renderer.receiveShadows
                        + ":" + (int)renderer.lightProbeUsage + ":" + (int)renderer.reflectionProbeUsage
                        + ":" + renderer.sortingLayerID + ":" + renderer.sortingOrder
                        + ":" + (renderer.probeAnchor == null ? 0 : renderer.probeAnchor.GetInstanceID());
                    VisualBatch batch;
                    if (!current.TryGetValue(key, out batch) || batch.Vertices + mesh.vertexCount > 60000
                        || batch.Sources.Count >= 512)
                    {
                        batch = new VisualBatch { AssetId = assetId };
                        current[key] = batch;
                        batches.Add(batch);
                    }
                    batch.Sources.Add(renderer);
                    batch.Vertices += mesh.vertexCount;
                }
                int removed = 0, created = 0;
                for (int i = 0; i < batches.Count; i++)
                {
                    VisualBatch batch = batches[i];
                    if (batch.Sources.Count < 2) continue;
                    if (TryCommitVisualBatch(batch, subwayEntrance))
                    {
                        removed += batch.Sources.Count;
                        created++;
                    }
                }
                if (created > 0 && PedestrianCrossingLog.AdvancedDiagnostics)
                    PedestrianCrossingLog.Advanced("Visual renderer consolidation: sources=" + removed
                        + " batches=" + created + " sharedEntrance=" + subwayEntrance + ".");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PedestrianCrossingToolkit] Visual consolidation stopped; remaining original visuals retained. " + exception.GetType().Name);
            }
        }

        private static bool TryCommitVisualBatch(VisualBatch batch, bool subwayEntrance)
        {
            GameObject combined = null;
            Mesh mesh = null;
            try
            {
                MeshRenderer first = batch.Sources[0];
                Vector3 origin = first.bounds.center;
                CombineInstance[] pieces = new CombineInstance[batch.Sources.Count];
                Matrix4x4 toLocal = Matrix4x4.TRS(-origin, Quaternion.identity, Vector3.one);
                for (int i = 0; i < pieces.Length; i++)
                {
                    MeshRenderer source = batch.Sources[i];
                    pieces[i] = new CombineInstance
                    {
                        mesh = source.GetComponent<MeshFilter>().sharedMesh,
                        subMeshIndex = 0,
                        transform = toLocal * source.transform.localToWorldMatrix
                    };
                }
                mesh = new Mesh();
                mesh.name = "PCT Combined Visual Mesh";
                mesh.CombineMeshes(pieces, true, true);
                if (mesh.vertexCount != batch.Vertices || mesh.subMeshCount != 1)
                    throw new InvalidOperationException("Combined mesh did not retain complete source geometry.");
                mesh.RecalculateBounds();
                combined = new GameObject((subwayEntrance ? "PCT Subway Entrance " : "PCT Crossing ")
                    + "opaque batch #" + batch.AssetId);
                combined.SetActive(false);
                combined.layer = first.gameObject.layer;
                combined.transform.position = origin;
                combined.AddComponent<MeshFilter>().sharedMesh = mesh;
                combined.AddComponent<CombinedVisualMeshOwner>().OwnedMesh = mesh;
                MeshRenderer renderer = combined.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = first.sharedMaterial;
                renderer.enabled = first.enabled;
                renderer.shadowCastingMode = first.shadowCastingMode;
                renderer.receiveShadows = first.receiveShadows;
                renderer.lightProbeUsage = first.lightProbeUsage;
                renderer.reflectionProbeUsage = first.reflectionProbeUsage;
                renderer.probeAnchor = first.probeAnchor;
                renderer.sortingLayerID = first.sortingLayerID;
                renderer.sortingOrder = first.sortingOrder;
                if (first.GetComponent<SubwayEntranceInfoViewVisibility>() != null)
                    combined.AddComponent<SubwayEntranceInfoViewVisibility>();
                // The replacement is complete before any original renderer is retired.
                BuiltBridgeConcreteObjects.Add(combined);
                combined.SetActive(true);
            }
            catch (Exception exception)
            {
                if (combined != null)
                {
                    BuiltBridgeConcreteObjects.Remove(combined);
                    combined.SetActive(false);
                    // Let its owner dispose the mesh if one was attached.
                    bool owned = combined.GetComponent<CombinedVisualMeshOwner>() != null;
                    UnityEngine.Object.Destroy(combined);
                    if (!owned && mesh != null) UnityEngine.Object.Destroy(mesh);
                }
                else if (mesh != null) UnityEngine.Object.Destroy(mesh);
                Debug.LogWarning("[PedestrianCrossingToolkit] Visual batch unavailable; original geometry retained. " + exception.GetType().Name);
                return false;
            }
            for (int i = 0; i < batch.Sources.Count; i++)
            {
                MeshRenderer source = batch.Sources[i];
                source.enabled = false;
                SubwayEntranceInfoViewVisibility visibility = source.GetComponent<SubwayEntranceInfoViewVisibility>();
                if (visibility != null) UnityEngine.Object.Destroy(visibility);
                // Retain source transforms, meshes and any collider under their existing
                // removal owner; only redundant render components leave the render scene.
                UnityEngine.Object.Destroy(source);
            }
            return true;
        }
    }
}
