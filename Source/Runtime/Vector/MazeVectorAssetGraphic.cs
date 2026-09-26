using UnityEngine;
using UnityEngine.UI;

namespace Maze
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MazeVectorAssetGraphic : MaskableGraphic, IMazeVectorRenderer, IMazeVectorPaintRenderer, IMazeVectorOutputRenderer, IMazeVectorImmediateRefresh
    {
        [SerializeField] private MazeVectorAsset asset;
        [SerializeField] private MazeVectorProfile profile;
        [SerializeField] private MazeVectorRenderMode renderMode = MazeVectorRenderMode.Overlay;
        [SerializeField] private MazeVectorAssetFitMode fitMode = MazeVectorAssetFitMode.Contain;
        [SerializeField] private MazeVectorPaint paint;
        [SerializeField] private MazeVectorState state = MazeVectorState.Idle;
        [SerializeField, Range(0f, 1f)] private float progress = 1f;
        [SerializeField, Range(MazeVectorUiOutput.NeutralIntensity, MazeVectorUiOutput.MaximumIntensity)] private float outputIntensity = MazeVectorUiOutput.NeutralIntensity;
        [SerializeField] private bool selected;

        private readonly MazeVectorBuilder.MeshData meshData = new();
        private readonly MazeVectorPaintMesh.MeshData paintMeshData = new();

        public MazeVectorShape Shape => null;
        public MazeVectorStyle Style => null;
        public MazeVectorPaint Paint => paint;
        public float Progress => progress;
        public float OutputIntensity => outputIntensity;
        public bool Selected => selected;
        public MazeVectorAsset Asset => asset;
        public MazeVectorRenderMode RenderMode => renderMode;
        public MazeVectorAssetFitMode FitMode => fitMode;
        public int LastVertexCount { get; private set; }
        public int LastTriangleCount { get; private set; }
        public int LastBuildFrame { get; private set; } = -1;
        public int DirtyVersion { get; private set; }
        public string LastGeometryHash { get; private set; } = string.Empty;
        public MazeVectorDirtyFlags DirtyFlags { get; private set; } = MazeVectorDirtyFlags.All;
        public override Texture mainTexture => paint != null && paint.HasSource ? paint.source : s_WhiteTexture;

        protected override void OnEnable()
        {
            base.OnEnable();
            ApplyOutputMaterial();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            MazeVectorUiOutput.EnsureCanvasChannels(this, paint, outputIntensity);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            outputIntensity = MazeVectorUiOutput.ClampIntensity(outputIntensity);
            ApplyOutputMaterial();
            MarkDirty(MazeVectorDirtyFlags.All);
        }
#endif

        public void SetAsset(MazeVectorAsset nextAsset, MazeVectorProfile nextProfile = null, MazeVectorState nextState = MazeVectorState.Idle)
        {
            asset = nextAsset;
            profile = nextProfile;
            state = nextState;
            selected = nextState == MazeVectorState.Selected || nextState == MazeVectorState.Pressed;
            MarkDirty(MazeVectorDirtyFlags.Geometry | MazeVectorDirtyFlags.Style | MazeVectorDirtyFlags.State);
        }

        public void SetProfile(MazeVectorProfile nextProfile)
        {
            if (profile == nextProfile)
            {
                return;
            }
            profile = nextProfile;
            MarkDirty(MazeVectorDirtyFlags.Style);
        }

        public void SetRenderMode(MazeVectorRenderMode nextMode)
        {
            if (renderMode == nextMode)
            {
                return;
            }
            renderMode = nextMode;
            MarkDirty(MazeVectorDirtyFlags.Render);
        }

        public void SetFitMode(MazeVectorAssetFitMode nextMode)
        {
            if (fitMode == nextMode)
            {
                return;
            }
            fitMode = nextMode;
            MarkDirty(MazeVectorDirtyFlags.Geometry | MazeVectorDirtyFlags.Transform);
        }

        public void SetPaint(MazeVectorPaint nextPaint)
        {
            paint = nextPaint != null ? nextPaint.Clone() : null;
            ApplyOutputMaterial();
            MarkDirty(MazeVectorDirtyFlags.Geometry | MazeVectorDirtyFlags.Style | MazeVectorDirtyFlags.Render);
            SetMaterialDirty();
        }

        public void SetOutputIntensity(float intensity)
        {
            intensity = MazeVectorUiOutput.ClampIntensity(intensity);
            if (Mathf.Approximately(outputIntensity, intensity))
            {
                return;
            }

            outputIntensity = intensity;
            ApplyOutputMaterial();
            MarkDirty(MazeVectorDirtyFlags.Render);
            SetMaterialDirty();
        }

        public void SetState(MazeVectorState nextState)
        {
            if (state == nextState)
            {
                return;
            }
            state = nextState;
            selected = nextState == MazeVectorState.Selected || nextState == MazeVectorState.Pressed;
            MarkDirty(MazeVectorDirtyFlags.State | MazeVectorDirtyFlags.Style);
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
            state = selected ? MazeVectorState.Selected : MazeVectorState.Idle;
            MarkDirty(MazeVectorDirtyFlags.State);
        }

        public void MarkGeometryDirty()
        {
            MarkDirty(MazeVectorDirtyFlags.Geometry);
        }

        public void RefreshVectorNow()
        {
            ApplyOutputMaterial();
            MarkDirty(MazeVectorDirtyFlags.All);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            LastVertexCount = 0;
            LastTriangleCount = 0;
            LastBuildFrame = Time.frameCount;
            var rect = rectTransform.rect;
            var geometry = MazeVectorCompiler.CompileAsset(rect, asset, fitMode, profile, state, paint, progress);
            geometry.WriteTo(vh, outputIntensity);
            LastVertexCount = geometry.VertexCount;
            LastTriangleCount = geometry.TriangleCount;
            LastGeometryHash = geometry.StableHash;

            DirtyFlags &= ~(MazeVectorDirtyFlags.Geometry | MazeVectorDirtyFlags.Style | MazeVectorDirtyFlags.Progress | MazeVectorDirtyFlags.State);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            MarkDirty(MazeVectorDirtyFlags.Transform);
        }

        public override void Cull(Rect clipRect, bool validRect)
        {
            if (asset == null || !asset.HasGeometryBounds)
            {
                base.Cull(clipRect, validRect);
                return;
            }

            var localRect = MapNormalizedRect(asset.GeometryBounds, rectTransform.rect, asset.ViewBox, fitMode);
            var padding = paint != null ? paint.GeometryPadding : 0f;
            localRect.xMin -= padding;
            localRect.yMin -= padding;
            localRect.xMax += padding;
            localRect.yMax += padding;
            var rootCanvas = canvas != null ? canvas.rootCanvas : null;
            var matrix = rootCanvas != null
                ? rootCanvas.transform.worldToLocalMatrix * rectTransform.localToWorldMatrix
                : rectTransform.localToWorldMatrix;
            var first = matrix.MultiplyPoint3x4(new Vector3(localRect.xMin, localRect.yMin));
            var min = (Vector2)first;
            var max = (Vector2)first;
            Encapsulate(matrix.MultiplyPoint3x4(new Vector3(localRect.xMax, localRect.yMin)), ref min, ref max);
            Encapsulate(matrix.MultiplyPoint3x4(new Vector3(localRect.xMax, localRect.yMax)), ref min, ref max);
            Encapsulate(matrix.MultiplyPoint3x4(new Vector3(localRect.xMin, localRect.yMax)), ref min, ref max);

            var cull = !validRect || !clipRect.Overlaps(Rect.MinMaxRect(min.x, min.y, max.x, max.y), true);
            if (canvasRenderer.cull == cull)
            {
                return;
            }
            canvasRenderer.cull = cull;
            onCullStateChanged.Invoke(cull);
            OnCullingChanged();
        }

        public static Vector2 ComputeLayoutScale(Rect target, Vector4 viewBox, MazeVectorAssetFitMode mode)
        {
            var longest = Mathf.Max(Mathf.Abs(viewBox.z), Mathf.Abs(viewBox.w), 0.0001f);
            var layoutWidth = Mathf.Max(0.0001f, Mathf.Abs(viewBox.z) / longest);
            var layoutHeight = Mathf.Max(0.0001f, Mathf.Abs(viewBox.w) / longest);
            var x = target.width / layoutWidth;
            var y = target.height / layoutHeight;
            return mode switch
            {
                MazeVectorAssetFitMode.Cover => Vector2.one * Mathf.Max(x, y),
                MazeVectorAssetFitMode.Stretch => new Vector2(x, y),
                _ => Vector2.one * Mathf.Min(x, y)
            };
        }

        public static Rect MapNormalizedRect(Rect normalizedRect, Rect target, Vector4 viewBox, MazeVectorAssetFitMode mode)
        {
            var scale = ComputeLayoutScale(target, viewBox, mode);
            var center = target.center;
            return Rect.MinMaxRect(
                center.x + normalizedRect.xMin * scale.x,
                center.y + normalizedRect.yMin * scale.y,
                center.x + normalizedRect.xMax * scale.x,
                center.y + normalizedRect.yMax * scale.y);
        }

        private static void Encapsulate(Vector3 point, ref Vector2 min, ref Vector2 max)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        private void ApplyOutputMaterial()
        {
            outputIntensity = MazeVectorUiOutput.ClampIntensity(outputIntensity);
            material = MazeVectorUiOutput.ResolveMaterial(paint, outputIntensity);
            MazeVectorUiOutput.EnsureCanvasChannels(this, paint, outputIntensity);
        }

        private void MarkDirty(MazeVectorDirtyFlags flags)
        {
            DirtyFlags |= flags;
            DirtyVersion++;
            SetVerticesDirty();
        }
    }
}
