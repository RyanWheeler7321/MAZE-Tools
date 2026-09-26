using UnityEngine;
using UnityEngine.UI;

namespace Maze
{
    public readonly struct MazeVectorSurfaceContext
    {
        public readonly string id;
        public readonly MazeVectorRenderMode requestedMode;
        public readonly RenderMode actualCanvasMode;
        public readonly Camera camera;
        public readonly RectTransform root;
        public readonly MazeVectorPool pool;
        public readonly Vector2 referenceResolution;

        public MazeVectorSurfaceContext(string id, MazeVectorRenderMode requestedMode, RenderMode actualCanvasMode, Camera camera, RectTransform root, MazeVectorPool pool, Vector2 referenceResolution)
        {
            this.id = id;
            this.requestedMode = requestedMode;
            this.actualCanvasMode = actualCanvasMode;
            this.camera = camera;
            this.root = root;
            this.pool = pool;
            this.referenceResolution = referenceResolution;
        }
    }

    public interface IMazeVectorSurfaceBridge
    {
        string BridgeId { get; }
        bool Supports(MazeVectorSurfaceContext context);
        void OnMazeVectorSurfaceReady(MazeVectorSurface surface, MazeVectorSurfaceContext context);
        void OnMazeVectorSurfaceState(MazeVectorSurface surface, MazeVectorDiagnosticsReport report);
    }

    public interface IMazeVectorRecipeBridge
    {
        string BridgeId { get; }
        bool TryBuildRecipe(MazeVectorSurface surface, RectTransform parent, MazeVectorRecipe recipe, MazeVectorProfile profile, MazeVectorLayerSet layers, MazeVectorState state, out Object bridgeHandle);
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class MazeVectorSurface : MonoBehaviour
    {
        [SerializeField] private string surfaceId = "vector_surface";
        [SerializeField] private MazeVectorRenderMode renderMode = MazeVectorRenderMode.Overlay;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private int sortingOrder = 20;
        [SerializeField, Min(0.01f)] private float planeDistance = 2f;
        [SerializeField] private Vector2 referenceResolution = new(1920f, 1080f);
        [SerializeField] private bool ensureCanvas = true;
        [SerializeField] private bool logStateOnChange = true;
        [SerializeField] private bool richDiagnostics;
        [SerializeField, Min(0.1f)] private float richDiagnosticsMinSeconds = 1f;

        private Canvas canvas;
        private RectTransform root;
        private MazeVectorPool pool;
        private IMazeVectorSurfaceBridge[] surfaceBridges;
        private IMazeVectorRecipeBridge[] recipeBridges;
        private string lastBridgeKey = string.Empty;
        private float lastRichDiagnosticsTime = -999f;

        public string SurfaceId => surfaceId;
        public MazeVectorRenderMode RenderMode => renderMode;
        public Canvas Canvas => canvas;
        public RectTransform Root => root;
        public MazeVectorPool Pool => pool;
        public Camera TargetCamera => targetCamera;
        public Vector2 ReferenceResolution => referenceResolution;

        private void Awake()
        {
            EnsureReady();
        }

        private void OnEnable()
        {
            EnsureReady();
        }

        public MazeVectorSurfaceContext EnsureReady()
        {
            root = transform as RectTransform;
            if (root == null)
            {
                MazeDiagnosticsLog.WarnOnce("maze.vector.surface.no_rect:" + GetEntityId(), "Maze.Vector", "surface_missing_rect", "vector surface needs a RectTransform", MazeDiagnosticsLog.JsonString("object", name));
                return CreateContext();
            }

            if (ensureCanvas)
            {
                canvas = GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
                MazeVectorCanvasUtility.ConfigureCanvas(canvas, renderMode, targetCamera, sortingOrder, planeDistance);
                MazeVectorCanvasUtility.EnsureScreenCanvasBasics(canvas, referenceResolution);
            }
            else
            {
                canvas = GetComponent<Canvas>();
            }

            pool = GetComponent<MazeVectorPool>() ?? gameObject.AddComponent<MazeVectorPool>();
            CacheBridges();
            var context = CreateContext();
            NotifyReady(context);
            return context;
        }

        public RectTransform CreateRect(string name, RectTransform parent, Vector2 position, Vector2 size)
        {
            EnsureReady();
            var go = new GameObject(string.IsNullOrWhiteSpace(name) ? "Vector Rect" : name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent != null ? parent : root, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public MazeVectorGraphic GetGraphic(RectTransform parent, string objectName, MazeVectorShape shape, MazeVectorStyle style)
        {
            EnsureReady();
            return pool.GetGraphic(parent != null ? parent : root, objectName, shape, style, renderMode);
        }

        public MazeVectorGroup BuildIcon(RectTransform parent, MazeVectorIconDefinition definition, MazeVectorProfile profile = null, MazeVectorState state = MazeVectorState.Idle)
        {
            EnsureReady();
            return MazeVectorIconFactory.Build(parent != null ? parent : root, definition, profile, pool, renderMode, state);
        }

        public MazeVectorAssetGraphic BuildAsset(RectTransform parent, MazeVectorAsset asset, MazeVectorProfile profile = null, MazeVectorState state = MazeVectorState.Idle, string objectName = null)
        {
            EnsureReady();
            var buildParent = parent != null ? parent : root;
            return pool.GetAssetGraphic(buildParent, string.IsNullOrWhiteSpace(objectName) ? asset != null ? asset.name : "Vector Asset" : objectName, asset, profile, renderMode, state);
        }

        public MazeVectorGraphic BuildPainted(
            RectTransform parent,
            MazeVectorShape shape,
            MazeVectorStyle style,
            MazeVectorPaint paint,
            string objectName = null)
        {
            EnsureReady();
            var buildParent = parent != null ? parent : root;
            var name = string.IsNullOrWhiteSpace(objectName)
                ? paint != null && paint.source != null ? paint.source.name : "Painted Vector"
                : objectName;
            var graphic = pool.GetGraphic(buildParent, name, shape, style, renderMode);
            graphic.SetPaint(paint);
            return graphic;
        }

        public MazeVectorGroup BuildRecipe(RectTransform parent, MazeVectorRecipe recipe, MazeVectorProfile profile = null, MazeVectorLayerSet layers = null, MazeVectorState state = MazeVectorState.Idle, bool batch = false)
        {
            EnsureReady();
            var group = new MazeVectorGroup();
            if (recipe == null)
            {
                return group;
            }

            MazeVectorDiagnostics.ValidateRecipe(recipe, "surface:" + surfaceId + ":" + recipe.Id);
            if (TryBuildRecipeThroughBridge(parent, recipe, profile, layers, state, out _))
            {
                return group;
            }

            var buildParent = parent != null ? parent : root;
            if (batch)
            {
                var batchGraphic = pool.GetBatch(buildParent, recipe.Id + " Batch", recipe, profile, layers, state, renderMode);
                group.Add(batchGraphic);
                return group;
            }

            var activeLayers = layers ?? new MazeVectorLayerSet();
            for (var i = 0; i < recipe.Elements.Count; i++)
            {
                var element = recipe.Elements[i];
                if (element == null || element.shape == null || !activeLayers.Allows(element.layer))
                {
                    continue;
                }
                var style = element.useStyleOverride && element.styleOverride != null
                    ? element.styleOverride.Clone()
                    : profile != null
                        ? profile.Resolve(element.role, state)
                        : MazeVectorReferenceKit.Outline(2f);
                MazeVectorDiagnostics.ValidateShape(element.shape, "surface:" + surfaceId + ":" + recipe.Id + ":" + i);
                MazeVectorDiagnostics.ValidateStyle(style, "surface:" + surfaceId + ":" + recipe.Id + ":" + i);
                var graphic = pool.GetGraphic(buildParent, string.IsNullOrWhiteSpace(element.name) ? "Recipe Part" : element.name, element.shape, style, renderMode);
                graphic.SetPaint(element.paint);
                var rect = graphic.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = element.size == Vector2.zero ? recipe.Size : element.size;
                rect.anchoredPosition = element.position;
                group.Add(graphic);
            }
            return group;
        }

        public MazeVectorGroup BuildRecipeAsset(RectTransform parent, MazeVectorRecipeAsset asset, MazeVectorProfile profile = null, MazeVectorLayerSet layers = null, MazeVectorState state = MazeVectorState.Idle, bool? batch = null)
        {
            if (asset == null)
            {
                return new MazeVectorGroup();
            }
            return BuildRecipe(parent, asset.CreateRecipe(), profile != null ? profile : asset.DefaultProfile, layers ?? asset.DefaultLayers, state, batch ?? asset.DefaultBatch);
        }

        public MazeVectorGroup BuildStaticBatch(RectTransform parent, MazeVectorRecipe recipe, MazeVectorProfile profile = null, MazeVectorLayerSet layers = null, MazeVectorState state = MazeVectorState.Idle)
        {
            return BuildRecipe(parent, recipe, profile, layers, state, true);
        }

        public MazeVectorGroup BuildDynamicGroup(RectTransform parent, MazeVectorRecipe recipe, MazeVectorProfile profile = null, MazeVectorLayerSet layers = null, MazeVectorState state = MazeVectorState.Idle)
        {
            return BuildRecipe(parent, recipe, profile, layers, state, false);
        }

        public bool TryBuildRecipeThroughBridge(RectTransform parent, MazeVectorRecipe recipe, MazeVectorProfile profile, MazeVectorLayerSet layers, MazeVectorState state, out Object bridgeHandle)
        {
            bridgeHandle = null;
            CacheBridges();
            if (recipeBridges == null)
            {
                return false;
            }
            for (var i = 0; i < recipeBridges.Length; i++)
            {
                if (recipeBridges[i] != null && recipeBridges[i].TryBuildRecipe(this, parent != null ? parent : root, recipe, profile, layers, state, out bridgeHandle))
                {
                    return true;
                }
            }
            return false;
        }

        public MazeVectorDiagnosticsReport LogState(string label = null)
        {
            EnsureReady();
            var report = MazeVectorDiagnostics.Count(root);
            if (logStateOnChange)
            {
                MazeVectorDiagnostics.LogOnChange("maze.vector.surface." + surfaceId, root, label ?? surfaceId);
            }
            if (richDiagnostics)
            {
                LogRichState(label);
            }
            NotifyState(report);
            return report;
        }

        public MazeVectorDiagnosticsReport LogRichState(string label = null, bool force = false)
        {
            EnsureReady();
            if (!force && Time.unscaledTime - lastRichDiagnosticsTime < richDiagnosticsMinSeconds)
            {
                return MazeVectorDiagnostics.Count(root);
            }
            lastRichDiagnosticsTime = Time.unscaledTime;
            var report = MazeVectorDiagnostics.Count(root);
            var data = MazeDiagnosticsLog.JoinData(
                report.ToJsonFields(label ?? surfaceId),
                MazeDiagnosticsLog.JsonString("surfaceId", surfaceId),
                MazeDiagnosticsLog.JsonString("renderMode", renderMode.ToString()),
                MazeDiagnosticsLog.JsonString("canvasMode", canvas != null ? canvas.renderMode.ToString() : "none"),
                MazeDiagnosticsLog.JsonString("camera", targetCamera != null ? targetCamera.name : "none"),
                MazeDiagnosticsLog.JsonNumber("poolActive", pool != null ? pool.ActiveCount : 0),
                MazeDiagnosticsLog.JsonNumber("poolPooled", pool != null ? pool.PooledCount : 0),
                MazeDiagnosticsLog.JsonNumber("poolCreated", pool != null ? pool.CreatedCount : 0),
                MazeDiagnosticsLog.JsonNumber("batchActive", pool != null ? pool.ActiveBatchCount : 0),
                MazeDiagnosticsLog.JsonNumber("batchPooled", pool != null ? pool.PooledBatchCount : 0),
                MazeDiagnosticsLog.JsonNumber("batchCreated", pool != null ? pool.CreatedBatchCount : 0));
            MazeDiagnosticsLog.InfoOnChange("maze.vector.surface.rich." + surfaceId, data, "Maze.Vector", "surface_rich_state", "vector surface rich state", data);
            NotifyState(report);
            return report;
        }

        public void ReleaseAll()
        {
            EnsureReady();
            pool.ReleaseAll();
        }

        public void Configure(
            MazeVectorRenderMode mode,
            Camera camera = null,
            int order = -99999,
            float cameraPlaneDistance = -1f)
        {
            renderMode = mode;
            if (camera != null)
            {
                targetCamera = camera;
            }
            if (order != -99999)
            {
                sortingOrder = order;
            }
            if (cameraPlaneDistance > 0f)
            {
                planeDistance = Mathf.Max(0.01f, cameraPlaneDistance);
            }
            EnsureReady();
        }

        private MazeVectorSurfaceContext CreateContext()
        {
            return new MazeVectorSurfaceContext(surfaceId, renderMode, canvas != null ? canvas.renderMode : UnityEngine.RenderMode.WorldSpace, targetCamera, root, pool, referenceResolution);
        }

        private void CacheBridges()
        {
            surfaceBridges = GetComponents<IMazeVectorSurfaceBridge>();
            recipeBridges = GetComponents<IMazeVectorRecipeBridge>();
        }

        private void NotifyReady(MazeVectorSurfaceContext context)
        {
            if (surfaceBridges == null)
            {
                return;
            }
            var key = context.requestedMode + ":" + context.actualCanvasMode + ":" + (context.camera != null ? context.camera.GetEntityId().ToString() : "none");
            if (lastBridgeKey == key)
            {
                return;
            }
            lastBridgeKey = key;
            for (var i = 0; i < surfaceBridges.Length; i++)
            {
                if (surfaceBridges[i] != null && surfaceBridges[i].Supports(context))
                {
                    surfaceBridges[i].OnMazeVectorSurfaceReady(this, context);
                }
            }
        }

        private void NotifyState(MazeVectorDiagnosticsReport report)
        {
            if (surfaceBridges == null)
            {
                return;
            }
            for (var i = 0; i < surfaceBridges.Length; i++)
            {
                surfaceBridges[i]?.OnMazeVectorSurfaceState(this, report);
            }
        }
    }
}
