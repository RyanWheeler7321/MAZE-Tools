using UnityEngine;
using UnityEngine.UI;

namespace Maze
{
    internal sealed class MazeVectorTransitionOverlayRenderer
    {
        private GameObject root;
        private RectTransform content;
        private Canvas canvas;
        private MazeVectorPool pool;
        private MazeVectorGraphic[] elements = new MazeVectorGraphic[0];
        private Vector2 canvasSize;

        public void Build(MazeTransitionProfile profile, Rect screenRect, Transform parent, int elementCount)
        {
            EnsureRoot(parent);
            SetSortingOrder(profile.SortingOrder);

            canvasSize = new Vector2(screenRect.width, screenRect.height);
            content.sizeDelta = canvasSize;
            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.anchoredPosition = Vector2.zero;

            if (elements.Length != elementCount)
            {
                pool.ReleaseAll();
                elements = new MazeVectorGraphic[elementCount];
                var style = MazeVectorStyle.Filled(Color.clear);
                for (var i = 0; i < elements.Length; i++)
                {
                    elements[i] = pool.GetGraphic(content, $"Vector {i:000}", MazeVectorShape.Rect(Vector2.one), style);
                    elements[i].SetRenderMode(MazeVectorRenderMode.Overlay);
                }
            }

            SetVisible(true);
        }

        public void SetSortingOrder(int sortingOrder)
        {
            if (canvas != null)
            {
                canvas.sortingOrder = sortingOrder;
            }
        }

        public void SetElement(int index, MazeTransitionVectorFrame frame)
        {
            if (index < 0 || index >= elements.Length || elements[index] == null)
            {
                return;
            }

            var graphic = elements[index];
            graphic.SetVisible(frame.Visible);
            if (!frame.Visible)
            {
                return;
            }

            var rectTransform = (RectTransform)graphic.transform;
            rectTransform.sizeDelta = frame.Rect.size;
            rectTransform.anchoredPosition = PixelToAnchored(frame.Rect.center);
            graphic.MarkTransformDirty();
            graphic.SetTransitionVisual(
                ToShapeKind(frame.Kind),
                frame.Rect.size,
                frame.FillColor,
                frame.StrokeColor,
                frame.StrokeThickness,
                frame.CornerLength,
                frame.DashLength,
                frame.DashGap,
                frame.Progress);
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
            if (pool != null)
            {
                pool.ReleaseAll();
            }

            elements = new MazeVectorGraphic[0];
            SetVisible(false);
        }

        private void EnsureRoot(Transform parent)
        {
            if (root != null)
            {
                return;
            }

            root = new GameObject("MAZE Vector Transition Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(parent, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.pixelPerfect = false;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;

            var contentObject = new GameObject("Vector Elements", typeof(RectTransform));
            contentObject.transform.SetParent(root.transform, false);
            content = (RectTransform)contentObject.transform;
            pool = contentObject.AddComponent<MazeVectorPool>();
        }

        private Vector2 PixelToAnchored(Vector2 pixel)
        {
            return new Vector2(pixel.x - canvasSize.x * 0.5f, pixel.y - canvasSize.y * 0.5f);
        }

        private static MazeVectorShapeKind ToShapeKind(MazeVectorTransitionElementKind kind)
        {
            return kind switch
            {
                MazeVectorTransitionElementKind.StrokeRect => MazeVectorShapeKind.OutlineBand,
                MazeVectorTransitionElementKind.CornerAccents => MazeVectorShapeKind.CornerAccents,
                MazeVectorTransitionElementKind.DashedOutline => MazeVectorShapeKind.DashedOutline,
                _ => MazeVectorShapeKind.Rect,
            };
        }
    }
}
