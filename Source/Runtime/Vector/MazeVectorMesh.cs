using UnityEngine;

namespace Maze
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class MazeVectorMesh : MonoBehaviour, IMazeVectorRenderer, IMazeVectorShapeRenderer, IMazeVectorStyleRenderer, IMazeVectorPaintRenderer, IMazeVectorImmediateRefresh
    {
        [SerializeField] private MazeVectorShape shape = MazeVectorShape.RoundedRect(new Vector2(1.2f, 0.8f), 0.18f);
        [SerializeField] private MazeVectorStyle style = new()
        {
            fill = true,
            stroke = true,
            fillColor = new Color(0.08f, 0.025f, 0.13f, 0.72f),
            strokeColor = new Color(0.78f, 0.28f, 1.25f, 1f),
            strokeThickness = 0.035f
        };
        [SerializeField] private MazeVectorPaint paint;
        [SerializeField, Range(0f, 1f)] private float progress = 1f;
        [SerializeField] private bool selected;

        private readonly MazeVectorBuilder.MeshData meshData = new();
        private readonly MazeVectorPaintMesh.MeshData paintMeshData = new();
        private Mesh mesh;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Material solidMaterial;
        private MaterialPropertyBlock propertyBlock;
        private bool geometryDirty = true;

        public MazeVectorShape Shape => shape;
        public MazeVectorStyle Style => style;
        public MazeVectorPaint Paint => paint;
        public float Progress => progress;
        public bool Selected => selected;
        public int LastVertexCount { get; private set; }
        public int LastTriangleCount { get; private set; }
        public int LastBuildFrame { get; private set; } = -1;
        public int DirtyVersion { get; private set; }
        public string LastGeometryHash { get; private set; } = string.Empty;
        public MazeVectorDirtyFlags DirtyFlags { get; private set; } = MazeVectorDirtyFlags.All;

        private void OnEnable()
        {
            EnsureMesh();
            MarkGeometryDirty();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            MarkDirty(MazeVectorDirtyFlags.All);
        }
#endif

        private void LateUpdate()
        {
            if (geometryDirty)
            {
                RebuildMesh();
            }
        }

        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(mesh);
                mesh = null;
            }
        }

        public void SetShape(MazeVectorShape nextShape)
        {
            shape = nextShape != null ? nextShape.Clone() : null;
            MarkDirty(MazeVectorDirtyFlags.Geometry);
        }

        public void SetStyle(MazeVectorStyle nextStyle)
        {
            style = nextStyle != null ? nextStyle.Clone() : null;
            MarkDirty(MazeVectorDirtyFlags.Style);
        }

        public void SetPaint(MazeVectorPaint nextPaint)
        {
            paint = nextPaint != null ? nextPaint.Clone() : null;
            ApplyPaintMaterial();
            MarkDirty(MazeVectorDirtyFlags.Geometry | MazeVectorDirtyFlags.Style | MazeVectorDirtyFlags.Render);
        }

        public void SetState(MazeVectorState state)
        {
            SetSelected(state == MazeVectorState.Selected || state == MazeVectorState.Pressed);
        }

        public void SetProgress(float nextProgress)
        {
            nextProgress = Mathf.Clamp01(nextProgress);
            if (Mathf.Approximately(progress, nextProgress))
            {
                return;
            }
            progress = nextProgress;
            MarkDirty(MazeVectorDirtyFlags.Progress);
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
                DirtyFlags |= MazeVectorDirtyFlags.Visibility;
                DirtyVersion++;
            }
        }

        public void SetSelected(bool nextSelected)
        {
            if (selected == nextSelected)
            {
                return;
            }
            selected = nextSelected;
            MarkDirty(MazeVectorDirtyFlags.State);
        }

        public void MarkGeometryDirty()
        {
            MarkDirty(MazeVectorDirtyFlags.Geometry);
        }

        public void RefreshVectorNow()
        {
            MarkDirty(MazeVectorDirtyFlags.All);
            RebuildMesh();
        }

        private void EnsureMesh()
        {
            meshFilter = meshFilter != null ? meshFilter : GetComponent<MeshFilter>();
            meshRenderer = meshRenderer != null ? meshRenderer : GetComponent<MeshRenderer>();
            if (mesh == null)
            {
                mesh = new Mesh { name = "Maze Vector Mesh" };
                mesh.MarkDynamic();
            }
            meshFilter.sharedMesh = mesh;
            EnsureMaterial();
        }

        private void EnsureMaterial()
        {
            if (meshRenderer == null)
            {
                return;
            }
            if (solidMaterial == null && meshRenderer.sharedMaterial != null)
            {
                solidMaterial = meshRenderer.sharedMaterial;
            }
            if (solidMaterial == null)
            {
                solidMaterial = MazeVectorRuntimeResources.GetWorldSolidMaterial();
                if (solidMaterial == null)
                {
                    MazeDiagnosticsLog.WarnOnce("maze.vector.world.material_missing:" + GetEntityId(), "Maze.Vector", "world_material_missing", "world vector mesh has no material and no fallback shader was found", MazeDiagnosticsLog.JsonString("object", name));
                    return;
                }
            }
            ApplyPaintMaterial();
        }

        private void RebuildMesh()
        {
            EnsureMesh();
            var boundsSize = shape != null ? shape.size : Vector2.one;
            var rect = new Rect(-boundsSize.x * 0.5f, -boundsSize.y * 0.5f, boundsSize.x, boundsSize.y);
            var geometry = MazeVectorCompiler.CompileShape(rect, shape, style, paint, progress, selected);
            mesh.Clear();
            mesh.SetVertices(geometry.Positions);
            mesh.SetTriangles(geometry.Indices, 0);
            mesh.SetColors(geometry.Colors);
            mesh.SetUVs(0, geometry.Uv0);
            mesh.SetUVs(1, geometry.Uv1);
            mesh.bounds = geometry.Bounds;
            if (paint != null && paint.GeometryPadding > 0f)
            {
                var bounds = mesh.bounds;
                var padding = paint.GeometryPadding;
                bounds.Expand(new Vector3(padding * 2f, padding * 2f, 0f));
                mesh.bounds = bounds;
            }
            ApplyPaintMaterial();
            LastVertexCount = geometry.VertexCount;
            LastTriangleCount = geometry.TriangleCount;
            LastGeometryHash = geometry.StableHash;
            LastBuildFrame = Time.frameCount;
            if (LastVertexCount == 0 && isActiveAndEnabled)
            {
                MazeDiagnosticsLog.WarnOnChange("maze.vector.world.zero_geometry:" + GetEntityId(), shape != null ? shape.kind.ToString() + ":" + progress : "null", "Maze.Vector", "world_zero_geometry", "world vector mesh produced no geometry", MazeDiagnosticsLog.JoinData(
                    MazeDiagnosticsLog.JsonString("object", name),
                    MazeDiagnosticsLog.JsonString("shape", shape != null ? shape.kind.ToString() : string.Empty),
                    MazeDiagnosticsLog.JsonNumber("progress", progress)));
            }
            geometryDirty = false;
            DirtyFlags &= ~(MazeVectorDirtyFlags.Geometry | MazeVectorDirtyFlags.Style | MazeVectorDirtyFlags.Progress | MazeVectorDirtyFlags.State);
        }

        private void ApplyPaintMaterial()
        {
            if (meshRenderer == null)
            {
                return;
            }
            if (paint != null && paint.RequiresShader)
            {
                var paintMaterial = MazeVectorPaintMaterials.GetWorld();
                if (paintMaterial != null)
                {
                    meshRenderer.sharedMaterial = paintMaterial;
                }
                propertyBlock ??= new MaterialPropertyBlock();
                propertyBlock.Clear();
                propertyBlock.SetTexture("_MainTex", paint.HasSource ? paint.source : Texture2D.whiteTexture);
                propertyBlock.SetTexture("_ControlMap", paint.controlMap != null ? paint.controlMap : MazeVectorPaintMaterials.NeutralControlMap);
                propertyBlock.SetVector("_ControlScroll", paint.controlScroll);
                meshRenderer.SetPropertyBlock(propertyBlock);
                return;
            }

            if (solidMaterial != null)
            {
                meshRenderer.sharedMaterial = solidMaterial;
            }
            meshRenderer.SetPropertyBlock(null);
        }

        private void MarkDirty(MazeVectorDirtyFlags flags)
        {
            DirtyFlags |= flags;
            DirtyVersion++;
            geometryDirty = true;
        }
    }
}
