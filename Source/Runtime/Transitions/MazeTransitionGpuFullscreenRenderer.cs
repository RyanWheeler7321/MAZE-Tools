using UnityEngine;
using UnityEngine.UI;

namespace Maze
{
    internal sealed class MazeTransitionGpuFullscreenRenderer : IMazeTransitionRenderer
    {
        private static readonly int MovingColorId = Shader.PropertyToID("_MovingColor");
        private static readonly int SettledColorId = Shader.PropertyToID("_SettledColor");
        private static readonly int OutColorId = Shader.PropertyToID("_OutColor");
        private static readonly int ScreenSizeId = Shader.PropertyToID("_ScreenSize");
        private static readonly int GridId = Shader.PropertyToID("_Grid");
        private static readonly int TimingId = Shader.PropertyToID("_Timing");
        private static readonly int RuntimeId = Shader.PropertyToID("_Runtime");
        private static readonly int OriginId = Shader.PropertyToID("_Origin");
        private static readonly int MotionId = Shader.PropertyToID("_Motion");
        private static readonly int OrderId = Shader.PropertyToID("_Order");
        private static readonly int DelayId = Shader.PropertyToID("_Delay");

        private GameObject root;
        private RectTransform content;
        private Canvas canvas;
        private MazeTransitionGpuFullscreenGraphic graphic;
        private Material material;
        private Vector2 canvasSize;
        private MazeSquareTransitionProfile activeProfile;
        private MazeTransitionContext activeContext;

        public static bool Supports(MazeSquareTransitionProfile profile, MazeTransitionContext context, out string reason)
        {
            if (profile == null)
            {
                reason = "missing_profile";
                return false;
            }

            if (Shader.Find("MAZE/Transition/GpuGrid") == null)
            {
                reason = "shader_missing";
                return false;
            }

            if (context.Flicker)
            {
                reason = "flicker_uses_cpu";
                return false;
            }

            if (profile.MotionPattern != MazeSquareTransitionPatternKind.RandomTessellate
                && profile.MotionPattern != MazeSquareTransitionPatternKind.TessellateWave)
            {
                reason = "pattern_uses_cpu";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public void Build(MazeSquareTransitionProfile profile, Rect screenRect, Transform parent, Camera camera)
        {
            activeProfile = profile;
            EnsureRoot(parent);
            SetSortingOrder(profile.SortingOrder);
            activeContext = default;

            canvasSize = new Vector2(screenRect.width, screenRect.height);
            content.sizeDelta = canvasSize;
            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            graphic.SetCanvasSize(canvasSize);
            SetVisible(true);
        }

        public void SetSortingOrder(int sortingOrder)
        {
            if (canvas != null)
            {
                canvas.sortingOrder = sortingOrder;
            }
        }

        public void SetTile(int index, MazeTransitionTileFrame frame)
        {
        }

        public void Upload()
        {
            graphic?.SetVerticesDirty();
        }

        public void Render(MazeSquareTransitionProfile profile, MazeTransitionContext context, float time, MazeTransitionDirection direction)
        {
            if (material == null || graphic == null || profile == null)
            {
                return;
            }

            activeProfile = profile;
            activeContext = context;
            material.SetColor(MovingColorId, profile.MovingColor);
            material.SetColor(SettledColorId, profile.SettledColor);
            material.SetColor(OutColorId, profile.OutColor);
            material.SetVector(ScreenSizeId, new Vector4(Mathf.Max(1f, canvasSize.x), Mathf.Max(1f, canvasSize.y), 0f, 0f));
            material.SetVector(GridId, new Vector4(profile.Columns, profile.Rows, profile.OverscanPixels, profile.GapPixels));
            material.SetVector(TimingId, new Vector4(profile.MoveSeconds, profile.FadeSeconds, profile.SettleSeconds, profile.BaseDelaySpanSeconds));
            material.SetVector(RuntimeId, new Vector4(time, direction == MazeTransitionDirection.In ? 0f : 1f, profile.MotionPattern == MazeSquareTransitionPatternKind.RandomTessellate ? 1f : 0f, ResolveSeed(profile, context)));
            material.SetVector(OriginId, ResolveOrigin(profile, context, direction));
            material.SetVector(MotionId, new Vector4(profile.RandomTessellateStartScale, profile.RandomTessellateShimmer, (float)profile.MoveEase, (float)profile.FadeEase));
            material.SetVector(OrderId, new Vector4((float)profile.Order, (float)profile.SpreadDirection, 0f, 0f));
            material.SetVector(DelayId, new Vector4(profile.StaggerSeconds, profile.RandomDelayJitter, profile.DirectionalSpreadSeconds, (float)profile.SpreadEase));
        }

        public void SetVisible(bool visible)
        {
            if (root != null && root.activeSelf != visible)
            {
                root.SetActive(visible);
            }
        }

        public void Release()
        {
            SetVisible(false);
        }

        private void EnsureRoot(Transform parent)
        {
            if (root != null)
            {
                return;
            }

            root = new GameObject("MAZE Transition GPU Fullscreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(parent, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.pixelPerfect = false;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;

            var contentObject = new GameObject("Fullscreen", typeof(RectTransform), typeof(MazeTransitionGpuFullscreenGraphic));
            contentObject.transform.SetParent(root.transform, false);
            content = (RectTransform)contentObject.transform;
            graphic = contentObject.GetComponent<MazeTransitionGpuFullscreenGraphic>();
            graphic.raycastTarget = false;

            var shader = Shader.Find("MAZE/Transition/GpuGrid");
            if (shader == null)
            {
                MazeDiagnosticsLog.WarnOnce("maze_transition_gpu_shader_missing", "Interface.Transition", "gpu_shader_missing", "GPU transition shader missing; renderer will be transparent");
                return;
            }

            material = new Material(shader)
            {
                name = "MAZE Transition GPU Grid Material"
            };
            graphic.material = material;
        }

        private static float ResolveSeed(MazeSquareTransitionProfile profile, MazeTransitionContext context)
        {
            return context.HasRandomSeed ? context.RandomSeed : profile.Seed;
        }

        private static Vector4 ResolveOrigin(MazeSquareTransitionProfile profile, MazeTransitionContext context, MazeTransitionDirection direction)
        {
            if (context.HasScreenPoint)
            {
                return new Vector4(context.ScreenPoint.x, context.ScreenPoint.y, 1f, 0f);
            }

            var origin = direction == MazeTransitionDirection.In ? profile.InOrigin : profile.OutOrigin;
            var point = origin switch
            {
                MazeTransitionOrigin.Left => new Vector2(0f, 0.5f),
                MazeTransitionOrigin.Right => new Vector2(1f, 0.5f),
                MazeTransitionOrigin.Top => new Vector2(0.5f, 1f),
                MazeTransitionOrigin.Bottom => new Vector2(0.5f, 0f),
                MazeTransitionOrigin.TopLeft => new Vector2(0f, 1f),
                MazeTransitionOrigin.TopRight => new Vector2(1f, 1f),
                MazeTransitionOrigin.BottomLeft => new Vector2(0f, 0f),
                MazeTransitionOrigin.BottomRight => new Vector2(1f, 0f),
                MazeTransitionOrigin.Point => profile.CustomPointNormalized,
                _ => new Vector2(0.5f, 0.5f),
            };
            return new Vector4(point.x, point.y, 0f, 0f);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class MazeTransitionGpuFullscreenGraphic : MaskableGraphic
    {
        private Vector2 canvasSize;

        public void SetCanvasSize(Vector2 size)
        {
            if (canvasSize == size)
            {
                return;
            }

            canvasSize = size;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var half = canvasSize * 0.5f;
            vh.AddVert(new Vector3(-half.x, -half.y, 0f), color, new Vector2(0f, 0f));
            vh.AddVert(new Vector3(-half.x, half.y, 0f), color, new Vector2(0f, 1f));
            vh.AddVert(new Vector3(half.x, half.y, 0f), color, new Vector2(1f, 1f));
            vh.AddVert(new Vector3(half.x, -half.y, 0f), color, new Vector2(1f, 0f));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(0, 2, 3);
        }
    }
}
