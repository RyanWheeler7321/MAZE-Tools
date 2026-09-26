using UnityEngine;

namespace Maze
{
    internal sealed class MazeTransitionMeshRenderer : IMazeTransitionRenderer
    {
        private GameObject root;
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private Camera camera;
        private Rect screenRect;
        private Vector3[] vertices = new Vector3[0];
        private Color[] colors = new Color[0];
        private int[] triangles = new int[0];
        private int tileCount;

        public void Build(MazeSquareTransitionProfile profile, Rect nextScreenRect, Transform parent, Camera nextCamera)
        {
            camera = nextCamera != null ? nextCamera : Camera.main;
            screenRect = nextScreenRect;
            EnsureRoot(parent);
            SetSortingOrder(profile.SortingOrder);
            EnsureBuffers(profile.TileCount);
            SetVisible(true);
        }

        public void SetSortingOrder(int sortingOrder)
        {
            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = sortingOrder;
            }
        }

        public void SetTile(int index, MazeTransitionTileFrame frame)
        {
            if (index < 0 || index >= tileCount)
            {
                return;
            }

            var vertexIndex = index * 4;
            var triangleIndex = index * 6;
            var c = frame.Visible ? frame.Color : Color.clear;
            var min = root.transform.InverseTransformPoint(ScreenToWorld(frame.Rect.min));
            var max = root.transform.InverseTransformPoint(ScreenToWorld(frame.Rect.max));

            vertices[vertexIndex + 0] = new Vector3(min.x, min.y, min.z);
            vertices[vertexIndex + 1] = new Vector3(min.x, max.y, min.z);
            vertices[vertexIndex + 2] = new Vector3(max.x, max.y, min.z);
            vertices[vertexIndex + 3] = new Vector3(max.x, min.y, min.z);
            colors[vertexIndex + 0] = c;
            colors[vertexIndex + 1] = c;
            colors[vertexIndex + 2] = c;
            colors[vertexIndex + 3] = c;

            triangles[triangleIndex + 0] = vertexIndex + 0;
            triangles[triangleIndex + 1] = vertexIndex + 1;
            triangles[triangleIndex + 2] = vertexIndex + 2;
            triangles[triangleIndex + 3] = vertexIndex + 0;
            triangles[triangleIndex + 4] = vertexIndex + 2;
            triangles[triangleIndex + 5] = vertexIndex + 3;
        }

        public void SetVisible(bool visible)
        {
            if (root != null && root.activeSelf != visible)
            {
                root.SetActive(visible);
            }
        }

        public void Upload()
        {
            if (mesh == null)
            {
                return;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        public void Release()
        {
            SetVisible(false);
            if (mesh != null)
            {
                mesh.Clear();
            }
        }

        private void EnsureRoot(Transform parent)
        {
            if (root != null)
            {
                return;
            }

            root = new GameObject("MAZE Transition Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(parent, false);
            mesh = new Mesh { name = "MAZE Transition Mesh" };
            mesh.MarkDynamic();
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = root.GetComponent<MeshRenderer>();
            var shader = Shader.Find("MAZE/Transition/VertexColorUnlit") ?? Shader.Find("Sprites/Default");
            meshRenderer.sharedMaterial = new Material(shader)
            {
                name = "MAZE Transition Vertex Color Material"
            };
        }

        private void EnsureBuffers(int nextTileCount)
        {
            tileCount = nextTileCount;
            var vertexCount = nextTileCount * 4;
            var triangleCount = nextTileCount * 6;
            if (vertices.Length != vertexCount)
            {
                vertices = new Vector3[vertexCount];
                colors = new Color[vertexCount];
                triangles = new int[triangleCount];
            }
        }

        private Vector3 ScreenToWorld(Vector2 pixel)
        {
            if (camera == null)
            {
                return new Vector3(pixel.x / Mathf.Max(1f, screenRect.width), pixel.y / Mathf.Max(1f, screenRect.height), 0f);
            }

            var z = Mathf.Max(camera.nearClipPlane + 0.05f, 0.1f);
            return camera.ScreenToWorldPoint(new Vector3(pixel.x, pixel.y, z));
        }
    }
}
