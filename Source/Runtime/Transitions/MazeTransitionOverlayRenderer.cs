using UnityEngine;
using UnityEngine.UI;

namespace Maze
{
    internal sealed class MazeTransitionOverlayRenderer : IMazeTransitionRenderer
    {
        private GameObject root;
        private RectTransform content;
        private Canvas canvas;
        private MazeTransitionBatchGraphic batchGraphic;
        private Vector2 canvasSize;

        public void Build(MazeSquareTransitionProfile profile, Rect screenRect, Transform parent, Camera camera)
        {
            EnsureRoot(parent);
            SetSortingOrder(profile.SortingOrder);

            canvasSize = new Vector2(screenRect.width, screenRect.height);
            content.sizeDelta = canvasSize;
            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            batchGraphic.SetCanvasSize(canvasSize);
            batchGraphic.SetFrameCapacity(profile.TileCount);
            SetVisible(true);
        }

        public void SetSortingOrder(int sortingOrder)
        {
            if (canvas != null)
            {
                canvas.sortingOrder = sortingOrder;
            }
        }

        public void SetDrawOrder(int[] tileIndicesBottomToTop)
        {
            batchGraphic?.SetDrawOrder(tileIndicesBottomToTop);
        }

        public void SetTile(int index, MazeTransitionTileFrame frame)
        {
            if (batchGraphic == null)
            {
                return;
            }

            batchGraphic.SetFrame(index, frame);
        }

        public void Upload()
        {
            batchGraphic?.Upload();
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
            batchGraphic?.ClearFrames();
            SetVisible(false);
        }

        private void EnsureRoot(Transform parent)
        {
            if (root != null)
            {
                return;
            }

            root = new GameObject("MAZE Transition Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(parent, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.pixelPerfect = false;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;

            var contentObject = new GameObject("Tiles", typeof(RectTransform), typeof(MazeTransitionBatchGraphic));
            contentObject.transform.SetParent(root.transform, false);
            content = (RectTransform)contentObject.transform;
            batchGraphic = contentObject.GetComponent<MazeTransitionBatchGraphic>();
            batchGraphic.raycastTarget = false;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class MazeTransitionBatchGraphic : MaskableGraphic
    {
        private MazeTransitionTileFrame[] frames = new MazeTransitionTileFrame[0];
        private int[] drawOrder;
        private Vector2 canvasSize;
        private bool dirty;

        public int FrameCount => frames.Length;

        public void SetCanvasSize(Vector2 size)
        {
            if (canvasSize == size)
            {
                return;
            }

            canvasSize = size;
            dirty = true;
            SetVerticesDirty();
        }

        public void SetFrameCapacity(int count)
        {
            count = Mathf.Max(0, count);
            if (frames.Length == count)
            {
                return;
            }

            frames = new MazeTransitionTileFrame[count];
            drawOrder = null;
            dirty = true;
            SetVerticesDirty();
        }

        public void SetDrawOrder(int[] tileIndicesBottomToTop)
        {
            if (tileIndicesBottomToTop == null || tileIndicesBottomToTop.Length != frames.Length)
            {
                drawOrder = null;
            }
            else
            {
                drawOrder = new int[tileIndicesBottomToTop.Length];
                System.Array.Copy(tileIndicesBottomToTop, drawOrder, tileIndicesBottomToTop.Length);
            }

            dirty = true;
            SetVerticesDirty();
        }

        public void SetFrame(int index, MazeTransitionTileFrame frame)
        {
            if (index < 0 || index >= frames.Length)
            {
                return;
            }

            frames[index] = frame;
            dirty = true;
        }

        public void Upload()
        {
            if (!dirty)
            {
                return;
            }

            dirty = false;
            SetVerticesDirty();
        }

        public void ClearFrames()
        {
            for (var i = 0; i < frames.Length; i++)
            {
                frames[i] = default;
            }

            dirty = true;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var count = frames.Length;
            for (var order = 0; order < count; order++)
            {
                var index = drawOrder != null ? drawOrder[order] : order;
                if (index < 0 || index >= count)
                {
                    continue;
                }

                var frame = frames[index];
                if (!frame.Visible || frame.Color.a <= 0.001f)
                {
                    continue;
                }

                AddRect(vh, frame.Rect, frame.Color);
            }
        }

        private void AddRect(VertexHelper vh, Rect pixelRect, Color color)
        {
            var min = PixelToLocal(pixelRect.min);
            var max = PixelToLocal(pixelRect.max);
            var vertexStart = vh.currentVertCount;
            vh.AddVert(new Vector3(min.x, min.y, 0f), color, new Vector2(0f, 0f));
            vh.AddVert(new Vector3(min.x, max.y, 0f), color, new Vector2(0f, 1f));
            vh.AddVert(new Vector3(max.x, max.y, 0f), color, new Vector2(1f, 1f));
            vh.AddVert(new Vector3(max.x, min.y, 0f), color, new Vector2(1f, 0f));
            vh.AddTriangle(vertexStart + 0, vertexStart + 1, vertexStart + 2);
            vh.AddTriangle(vertexStart + 0, vertexStart + 2, vertexStart + 3);
        }

        private Vector2 PixelToLocal(Vector2 pixel)
        {
            return new Vector2(pixel.x - canvasSize.x * 0.5f, pixel.y - canvasSize.y * 0.5f);
        }
    }
}
