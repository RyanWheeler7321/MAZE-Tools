using UnityEngine;
using UnityEngine.UI;

namespace Maze
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MazeVectorGraphic : MaskableGraphic, IMazeVectorRenderer, IMazeVectorShapeRenderer, IMazeVectorStyleRenderer, IMazeVectorPaintRenderer, IMazeVectorOutputRenderer, IMazeVectorImmediateRefresh
    {
        [SerializeField] private MazeVectorRenderMode renderMode = MazeVectorRenderMode.Overlay;
        [SerializeField] private MazeVectorShape shape = MazeVectorShape.RoundedRect(new Vector2(120f, 80f), 18f);
        [SerializeField] private MazeVectorStyle style = new()
        {
            fill = true,
            stroke = true,
            fillColor = new Color(0.08f, 0.025f, 0.13f, 0.72f),
            strokeColor = new Color(0.78f, 0.28f, 1.25f, 1f),
            strokeThickness = 3f
        };
        [SerializeField] private MazeVectorPaint paint;
        [SerializeField, Range(0f, 1f)] private float progress = 1f;
        [SerializeField, Range(MazeVectorUiOutput.NeutralIntensity, MazeVectorUiOutput.MaximumIntensity)] private float outputIntensity = MazeVectorUiOutput.NeutralIntensity;
        [SerializeField] private bool selected;

        private readonly MazeVectorBuilder.MeshData meshData = new();
        private readonly MazeVectorPaintMesh.MeshData paintMeshData = new();

        public MazeVectorRenderMode RenderMode => renderMode;
        public MazeVectorShape Shape => shape;
        public MazeVectorStyle Style => style;
        public MazeVectorPaint Paint => paint;
        public float Progress => progress;
        public float OutputIntensity => outputIntensity;
        public bool Selected => selected;
        public float GlowIntent => style != null ? style.glow : 0f;
        public int LastVertexCount { get; private set; }
        public int LastTriangleCount { get; private set; }
        public Vector2 LastRectSize { get; private set; }
        public MazeVectorShapeKind LastShapeKind { get; private set; }
        public int LastBuildFrame { get; private set; } = -1;
        public int DirtyVersion { get; private set; }
        public string LastGeometryHash { get; private set; } = string.Empty;
        public MazeVectorDirtyFlags DirtyFlags { get; private set; } = MazeVectorDirtyFlags.All;
        public bool HasBuiltVisibleGeometry => LastVertexCount > 0 && LastTriangleCount > 0 && isActiveAndEnabled && gameObject.activeInHierarchy;
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

        public void ApplyPreset(MazeVectorPreset preset)
        {
            if (preset == null)
            {
                return;
            }

            renderMode = preset.RenderMode;
            shape = preset.Shape != null ? preset.Shape.Clone() : shape;
            style = preset.Style != null ? preset.Style.Clone() : style;
            MarkDirty(MazeVectorDirtyFlags.Geometry | MazeVectorDirtyFlags.Style | MazeVectorDirtyFlags.Render);
        }

        public void SetShape(MazeVectorShape nextShape)
        {
            shape = nextShape != null ? nextShape.Clone() : null;
            MarkDirty(MazeVectorDirtyFlags.Geometry);
        }

        public void SetRectSize(Vector2 size)
        {
            shape ??= MazeVectorShape.Rect(size);
            if (shape.kind != MazeVectorShapeKind.Rect)
            {
                shape = MazeVectorShape.Rect(size);
                MarkDirty(MazeVectorDirtyFlags.Geometry);
                return;
            }

            if (shape.size == size)
            {
                return;
            }

            shape.size = size;
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

        public void SetState(MazeVectorState state)
        {
            SetSelected(state == MazeVectorState.Selected || state == MazeVectorState.Pressed);
        }

        public void SetSolidFill(Color fillColor)
        {
            style ??= new MazeVectorStyle();

            if (style.fill
                && !style.stroke
                && style.fillColor == fillColor
                && Mathf.Approximately(style.glow, 0f))
            {
                return;
            }

            style.fill = true;
            style.stroke = false;
            style.fillColor = fillColor;
            style.glow = 0f;
            MarkDirty(MazeVectorDirtyFlags.Style);
        }

        public void SetTransitionVisual(
            MazeVectorShapeKind shapeKind,
            Vector2 size,
            Color fillColor,
            Color strokeColor,
            float strokeThickness,
            float cornerLength,
            float dashLength,
            float dashGap,
            float nextProgress)
        {
            shape ??= MazeVectorShape.Rect(size);
            style ??= new MazeVectorStyle();

            shape.kind = shapeKind;
            shape.size = size;
            shape.cornerLength = Mathf.Max(0f, cornerLength);
            shape.dashLength = Mathf.Max(1f, dashLength);
            shape.dashGap = Mathf.Max(0f, dashGap);
            shape.closed = true;

            style.fill = fillColor.a > 0f && shapeKind == MazeVectorShapeKind.Rect;
            style.stroke = strokeColor.a > 0f;
            style.fillColor = fillColor;
            style.strokeColor = strokeColor;
            style.strokeThickness = Mathf.Max(0.5f, strokeThickness);
            style.glow = 0f;
            progress = Mathf.Clamp01(nextProgress);
            MarkDirty(MazeVectorDirtyFlags.Geometry | MazeVectorDirtyFlags.Style | MazeVectorDirtyFlags.Progress);
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
            ApplyOutputMaterial();
            MarkDirty(MazeVectorDirtyFlags.All);
        }

        public void SetRenderMode(MazeVectorRenderMode nextMode)
        {
            if (renderMode == nextMode)
            {
                return;
            }
            renderMode = nextMode;
            MarkDirty(MazeVectorDirtyFlags.Render, false);
        }

        public void MarkTransformDirty()
        {
            DirtyFlags |= MazeVectorDirtyFlags.Transform;
            DirtyVersion++;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            var rect = rectTransform.rect;
            var geometry = MazeVectorCompiler.CompileShape(rect, shape, style, paint, progress, selected);
            geometry.WriteTo(vh, outputIntensity);
            LastVertexCount = geometry.VertexCount;
            LastTriangleCount = geometry.TriangleCount;
            LastGeometryHash = geometry.StableHash;
            LastRectSize = rect.size;
            LastShapeKind = shape != null ? shape.kind : default;
            LastBuildFrame = Time.frameCount;
            DirtyFlags &= ~(MazeVectorDirtyFlags.Geometry | MazeVectorDirtyFlags.Style | MazeVectorDirtyFlags.Progress | MazeVectorDirtyFlags.State);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            MarkDirty(MazeVectorDirtyFlags.Transform);
        }

        public override void Cull(Rect clipRect, bool validRect)
        {
            var padding = paint != null ? paint.GeometryPadding : 0f;
            if (padding <= 0f)
            {
                base.Cull(clipRect, validRect);
                return;
            }

            var localRect = rectTransform.rect;
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
            if (canvasRenderer.cull != cull)
            {
                canvasRenderer.cull = cull;
                onCullStateChanged.Invoke(cull);
                OnCullingChanged();
            }
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

        private void MarkDirty(MazeVectorDirtyFlags flags, bool verticesDirty = true)
        {
            DirtyFlags |= flags;
            DirtyVersion++;
            if (verticesDirty)
            {
                SetVerticesDirty();
            }
        }
    }
}
