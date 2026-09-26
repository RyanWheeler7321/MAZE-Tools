using UnityEngine;

namespace Maze
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class MazeVectorSelector : MonoBehaviour
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private Vector2 targetPosition;
        [SerializeField] private bool useExplicitPosition;
        [SerializeField, Min(0f)] private float smoothSeconds = 0.045f;
        [SerializeField] private MazeVectorSelectorVariant variant = MazeVectorSelectorVariant.SideTicks;
        private MazeVectorGroup selectorGroup = new();

        private RectTransform rect;
        private Vector2 velocity;
        private bool snapped;

        public RectTransform Rect => rect != null ? rect : rect = (RectTransform)transform;
        public MazeVectorGroup Group => selectorGroup;
        public RectTransform Target => target;
        public float SmoothSeconds { get => smoothSeconds; set => smoothSeconds = Mathf.Max(0f, value); }

        private void Awake()
        {
            rect = (RectTransform)transform;
        }

        private void LateUpdate()
        {
            var desired = ResolveTargetPosition();
            if (!snapped || smoothSeconds <= 0f)
            {
                Rect.anchoredPosition = desired;
                velocity = Vector2.zero;
                snapped = true;
                return;
            }

            Rect.anchoredPosition = Vector2.SmoothDamp(Rect.anchoredPosition, desired, ref velocity, smoothSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        public void Build(MazeVectorSurface surface, Vector2 size, MazeVectorSelectorVariant nextVariant = MazeVectorSelectorVariant.SideTicks, float thickness = 1.6f, float accentThickness = 4.2f, float cornerLength = 28f, float sideGap = 42f, float hookLength = 14f)
        {
            if (surface == null)
            {
                return;
            }
            variant = nextVariant;
            Rect.sizeDelta = size;
            selectorGroup?.ReleaseToPool(surface.Pool);
            selectorGroup = surface.BuildIcon(Rect, MazeVectorReferenceKit.Selector(size, variant, thickness, accentThickness, cornerLength, sideGap, hookLength), null, MazeVectorState.Selected);
            selectorGroup.SetSelected(true);
            Snap();
        }

        public void SetTarget(RectTransform nextTarget, bool snap = false)
        {
            target = nextTarget;
            useExplicitPosition = false;
            snapped = snapped && !snap;
            if (snap)
            {
                Snap();
            }
        }

        public void SetTargetPosition(Vector2 position, bool snap = false)
        {
            targetPosition = position;
            useExplicitPosition = true;
            snapped = snapped && !snap;
            if (snap)
            {
                Snap();
            }
        }

        public void Snap()
        {
            Rect.anchoredPosition = ResolveTargetPosition();
            velocity = Vector2.zero;
            snapped = true;
        }

        private Vector2 ResolveTargetPosition()
        {
            if (useExplicitPosition || target == null)
            {
                return targetPosition;
            }

            var parent = Rect.parent as RectTransform;
            if (parent == null)
            {
                return target.anchoredPosition;
            }
            if (target.parent == parent)
            {
                return target.anchoredPosition;
            }

            var world = target.TransformPoint(target.rect.center);
            var local = parent.InverseTransformPoint(world);
            return new Vector2(local.x, local.y);
        }
    }
}
