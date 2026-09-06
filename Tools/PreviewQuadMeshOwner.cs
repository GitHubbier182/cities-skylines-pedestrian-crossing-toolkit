using System.Collections.Generic;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    // All preview primitives are complete four-vertex, six-index quads.
    // Keep every primitive within Unity's 16-bit mesh boundary and own disposal.
    internal sealed class PreviewQuadMeshOwner : MonoBehaviour
    {
        private const int VerticesPerMesh = 60000;
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<int> _triangles = new List<int>();

        internal void Initialize(Mesh first)
        {
            _meshes.Add(first);
            _objects.Add(gameObject);
        }

        internal void Upload(IList<Vector3> vertices, IList<Vector2> uvs, IList<Color> colors, IList<int> triangles)
        {
            int used = 0;
            for (int start = 0; start < vertices.Count; start += VerticesPerMesh)
            {
                int count = System.Math.Min(VerticesPerMesh, vertices.Count - start);
                if (used == _meshes.Count)
                {
                    GameObject child = new GameObject(name + " continuation");
                    child.transform.SetParent(transform, false);
                    child.layer = gameObject.layer;
                    child.hideFlags = gameObject.hideFlags;
                    Mesh mesh = new Mesh();
                    mesh.name = name + " mesh continuation";
                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer source = GetComponent<MeshRenderer>();
                    MeshRenderer renderer = child.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = source.sharedMaterial;
                    renderer.shadowCastingMode = source.shadowCastingMode;
                    renderer.receiveShadows = source.receiveShadows;
                    renderer.lightProbeUsage = source.lightProbeUsage;
                    _objects.Add(child);
                    _meshes.Add(mesh);
                }
                _vertices.Clear();
                _uvs.Clear();
                _colors.Clear();
                _triangles.Clear();
                for (int i = start; i < start + count; i++)
                {
                    _vertices.Add(vertices[i]);
                    if (uvs != null) _uvs.Add(uvs[i]);
                    if (colors != null) _colors.Add(colors[i]);
                }
                int firstTriangle = start / 4 * 6;
                int endTriangle = firstTriangle + count / 4 * 6;
                for (int i = firstTriangle; i < endTriangle; i++)
                    _triangles.Add(triangles[i] - start);
                Mesh target = _meshes[used];
                target.Clear();
                target.SetVertices(_vertices);
                if (uvs != null) target.SetUVs(0, _uvs);
                if (colors != null) target.SetColors(_colors);
                target.SetTriangles(_triangles, 0);
                target.RecalculateBounds();
                if (used > 0) _objects[used].SetActive(true);
                used++;
            }
            for (int i = used; i < _objects.Count; i++)
            {
                if (i > 0) _objects[i].SetActive(false);
                else _meshes[i].Clear();
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _meshes.Count; i++)
                if (_meshes[i] != null) Object.Destroy(_meshes[i]);
            _meshes.Clear();
            _objects.Clear();
        }
    }
}
