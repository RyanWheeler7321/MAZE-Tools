using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Maze
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MazeVectorBatchGraphic : MaskableGraphic, IMazeVectorRenderer, IMazeVectorShapeRenderer, IMazeVectorStyleRenderer, IMazeVectorPaintRenderer, IMazeVectorOutputRenderer, IMazeVectorImmediateRefresh
    {
        [SerializeField] private MazeVectorRecipe recipe = new("batch", new Vector2(100f, 100f));
        [SerializeField] private MazeVectorProfile profile;
        [SerializeField] private MazeVectorState state = MazeVectorState.Idle;
        [SerializeField] private MazeVectorLayerSet layers = new();
        [SerializeField] private MazeVectorPaint paintOverride;
        [SerializeField, Range(0f, 1f)] private float progress = 1f;
        [SerializeField, Range(MazeVectorUiOutput.NeutralIntensity, MazeVectorUiOutput.MaximumIntensity)] private float outputIntensity = MazeVectorUiOutput.NeutralIntensity;
        [SerializeField] private bool selected;

        private readonly MazeVectorBuilder.MeshData scratch = new();
        private readonly MazeVectorPaintMesh.MeshData paintedScratch = new();
        private readonly MazeVectorPaintMesh.MeshData output = new();
        private MazeVectorPaint batchPaint;

        public MazeVectorShape Shape => recipe != null && recipe.Count > 0 ? recipe.Elements[0].shape : null;
        public MazeVectorStyle Style => recipe != null && recipe.Count > 0 ? ResolveStyle(recipe.Elements[0]) : null;
        public MazeVectorPaint Paint => paintOverride != null ? paintOverride : batchPaint;
        public float Progress => progress;
        public float OutputIntensity => outputIntensity;
        public bool Selected => selected;
        public int LastVertexCount { get; private set; }
        public int LastTriangleCount { get; private set; }
        public int LastBuildFrame { get; private set; } = -1;
        public int DirtyVersion { get; private set; }
        public string LastGeometryHash { get; private set; } = string.Empty;
        public MazeVectorDirtyFlags DirtyFlags { get; private set; } = MazeVectorDirtyFlags.All;
        public bool HasBuiltVisibleGeometry => LastVertexCount > 0 && LastTriangleCount > 0 && isActiveAndEnabled && gameObject.activeInHierarchy;
        public override Texture mainTexture => batchPaint != null && batchPaint.HasSource ? batchPaint.source : s_WhiteTexture;

        protected override void OnEnable()
        {
            base.OnEnable();
            ApplyOutputMaterial();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            MazeVectorUiOutput.EnsureCanvasChannels(this, batchPaint, outputIntensity);
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

        public void SetRecipe(MazeVectorRecipe nextRecipe, MazeVectorProfile nextProfile = null, MazeVectorLayerSet nextLayers = null, MazeVectorState nextState = MazeVectorState.Idle)
        {
            recipe = nextRecipe != null ? nextRecipe.Clone() : null;
            profile = nextProfile;
            layers = nextLayers ?? new MazeVectorLayerSet();
            state = nextState;
            selected = nextState == MazeVectorState.Selected || nextState == MazeVectorState.Pressed;
            ApplyOutputMaterial();
            MarkDirty(MazeVectorDirtyFlags.Geometry | MazeVectorDirtyFlags.Style | MazeVectorDirtyFlags.State);
        }

        public void SetState(MazeVectorState nextState)
        {
            if (state == nextState)
            {
                return;
            }
            state = nextState;
            selected = nextState == MazeVectorState.Selected || nextState == MazeVectorState.Pressed;
            MarkDirty(MazeVectorDirtyFlags.State);
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

        public void SetShape(MazeVectorShape shape)
        {
            recipe = new MazeVectorRecipe("single", rectTransform.rect.size).Add("Shape", shape, rectTransform.rect.size, Vector2.zero, Style);
            MarkDirty(MazeVectorDirtyFlags.Geometry);
        }

        public void SetStyle(MazeVectorStyle style)
        {
            if (recipe == null)
            {
                recipe = new MazeVectorRecipe("single", rectTransform.rect.size);
            }
            if (recipe.Count == 0)
            {
                recipe.Add("Shape", MazeVectorShape.Rect(rectTransform.rect.size), rectTransform.rect.size, Vector2.zero, style);
            }
            else if (recipe.Elements[0] != null)
            {
                recipe.Elements[0].useStyleOverride = true;
                recipe.Elements[0].styleOverride = style != null ? style.Clone() : null;
            }
            MarkDirty(MazeVectorDirtyFlags.Style);
        }

        public void SetPaint(MazeVectorPaint nextPaint)
        {
            paintOverride = nextPaint != null ? nextPaint.Clone() : null;
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
            if (!TryResolveBatchPaint(out batchPaint))
            {
                LastVertexCount = 0;
                LastTriangleCount = 0;
                LastBuildFrame = Time.frameCount;
                MazeDiagnosticsLog.WarnOnChange(
                    "maze.vector.batch.paint_conflict:" + GetEntityId(),
                    recipe != null ? recipe.Id : "null",
                    "Maze.Vector",
                    "batch_paint_conflict",
                    "MazeVector batch has incompatible source textures, control maps, or control scroll values",
                    MazeDiagnosticsLog.JsonString("object", name));
                return;
            }
            var rect = rectTransform.rect;
            var geometry = MazeVectorCompiler.CompileRecipe(rect, recipe, profile, layers, state, paintOverride, progress);
            geometry.WriteTo(vh, outputIntensity);
            LastVertexCount = geometry.VertexCount;
            LastTriangleCount = geometry.TriangleCount;
            LastGeometryHash = geometry.StableHash;
            LastBuildFrame = Time.frameCount;
            DirtyFlags &= ~(MazeVectorDirtyFlags.Geometry | MazeVectorDirtyFlags.Style | MazeVectorDirtyFlags.Progress | MazeVectorDirtyFlags.State);
        }

        private MazeVectorStyle ResolveStyle(MazeVectorElement element)
        {
            if (element != null && element.useStyleOverride && element.styleOverride != null)
            {
                return element.styleOverride.Clone();
            }
            if (profile != null && element != null)
            {
                return profile.Resolve(element.role, state);
            }
            return MazeVectorReferenceKit.Outline(2f);
        }

        private static void Append(MazeVectorPaintMesh.MeshData from, MazeVectorPaintMesh.MeshData to)
        {
            var offset = to.vertices.Count;
            for (var i = 0; i < from.vertices.Count; i++)
            {
                to.vertices.Add(from.vertices[i]);
                to.colors.Add(from.colors[i]);
                to.uv0.Add(from.uv0[i]);
                to.uv1.Add(from.uv1[i]);
            }
            for (var i = 0; i < from.triangles.Count; i++)
            {
                to.triangles.Add(from.triangles[i] + offset);
            }
        }

        private bool TryResolveBatchPaint(out MazeVectorPaint resolved)
        {
            resolved = null;
            if (paintOverride != null && paintOverride.RequiresShader)
            {
                resolved = paintOverride.Clone();
                return true;
            }

            var count = recipe?.Elements != null ? recipe.Elements.Count : 0;
            for (var i = 0; i < count; i++)
            {
                var candidate = recipe.Elements[i]?.paint;
                if (candidate == null || !candidate.RequiresShader)
                {
                    continue;
                }
                if (resolved == null)
                {
                    resolved = candidate.Clone();
                    continue;
                }
                if (!MazeVectorPaint.MaterialCompatible(resolved, candidate))
                {
                    return false;
                }
                if (!resolved.HasSource && candidate.HasSource)
                {
                    resolved.source = candidate.source;
                    resolved.sourceMode = candidate.sourceMode;
                }
                if (resolved.controlMap == null && candidate.controlMap != null)
                {
                    resolved.controlMap = candidate.controlMap;
                    resolved.controlScroll = candidate.controlScroll;
                }
            }
            return true;
        }

        private void ApplyOutputMaterial()
        {
            if (!TryResolveBatchPaint(out batchPaint))
            {
                batchPaint = null;
                material = null;
                return;
            }
            outputIntensity = MazeVectorUiOutput.ClampIntensity(outputIntensity);
            material = MazeVectorUiOutput.ResolveMaterial(batchPaint, outputIntensity);
            MazeVectorUiOutput.EnsureCanvasChannels(this, batchPaint, outputIntensity);
        }

        private void MarkDirty(MazeVectorDirtyFlags flags)
        {
            DirtyFlags |= flags;
            DirtyVersion++;
            SetVerticesDirty();
        }
    }

    [DisallowMultipleComponent]
    public sealed class MazeVectorBatchGroup : MonoBehaviour
    {
        [SerializeField] private MazeVectorBatchGraphic batchGraphic;

        public MazeVectorBatchGraphic Graphic => batchGraphic;

        public MazeVectorBatchGraphic Ensure(RectTransform parent, string objectName, MazeVectorRecipe recipe, MazeVectorProfile profile, MazeVectorLayerSet layers, MazeVectorState state)
        {
            var rect = transform as RectTransform;
            if (rect == null)
            {
                return null;
            }
            if (parent != null && rect.parent != parent)
            {
                rect.SetParent(parent, false);
            }
            name = string.IsNullOrWhiteSpace(objectName) ? "Maze Vector Batch" : objectName;
            batchGraphic = batchGraphic != null ? batchGraphic : gameObject.GetComponent<MazeVectorBatchGraphic>() ?? gameObject.AddComponent<MazeVectorBatchGraphic>();
            batchGraphic.raycastTarget = false;
            batchGraphic.SetRecipe(recipe, profile, layers, state);
            return batchGraphic;
        }
    }
}
