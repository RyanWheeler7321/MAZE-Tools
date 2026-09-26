using System.Collections.Generic;
using UnityEngine;

namespace Maze
{
    [DisallowMultipleComponent]
    public sealed class MazeVectorPool : MonoBehaviour
    {
        private readonly Stack<MazeVectorGraphic> pooledGraphics = new(32);
        private readonly List<MazeVectorGraphic> activeGraphics = new(32);
        private readonly Stack<MazeVectorBatchGraphic> pooledBatches = new(8);
        private readonly List<MazeVectorBatchGraphic> activeBatches = new(8);
        private readonly Stack<MazeVectorAssetGraphic> pooledAssetGraphics = new(16);
        private readonly List<MazeVectorAssetGraphic> activeAssetGraphics = new(16);

        public int ActiveCount => activeGraphics.Count;
        public int PooledCount => pooledGraphics.Count;
        public int CreatedCount { get; private set; }
        public int ActiveBatchCount => activeBatches.Count;
        public int PooledBatchCount => pooledBatches.Count;
        public int CreatedBatchCount { get; private set; }
        public int ActiveAssetCount => activeAssetGraphics.Count;
        public int PooledAssetCount => pooledAssetGraphics.Count;
        public int CreatedAssetCount { get; private set; }
        public int TotalActiveCount => activeGraphics.Count + activeBatches.Count + activeAssetGraphics.Count;
        public int TotalPooledCount => pooledGraphics.Count + pooledBatches.Count + pooledAssetGraphics.Count;
        public int TotalCreatedCount => CreatedCount + CreatedBatchCount + CreatedAssetCount;

        public MazeVectorGraphic GetGraphic(RectTransform parent, string objectName, MazeVectorShape shape, MazeVectorStyle style, MazeVectorRenderMode renderMode = MazeVectorRenderMode.Overlay)
        {
            var graphic = pooledGraphics.Count > 0 ? pooledGraphics.Pop() : CreateGraphic();
            var rect = (RectTransform)graphic.transform;
            ResetRect(rect, parent, objectName, Vector2.zero);
            graphic.gameObject.SetActive(true);
            graphic.enabled = true;
            graphic.raycastTarget = false;
            graphic.canvasRenderer.SetAlpha(1f);
            graphic.SetRenderMode(renderMode);
            graphic.SetShape(shape);
            graphic.SetStyle(style);
            graphic.SetPaint(null);
            graphic.SetProgress(1f);
            graphic.SetOutputIntensity(MazeVectorUiOutput.NeutralIntensity);
            graphic.SetSelected(false);
            graphic.MarkGeometryDirty();
            if (!activeGraphics.Contains(graphic))
            {
                activeGraphics.Add(graphic);
            }
            return graphic;
        }

        public MazeVectorBatchGraphic GetBatch(RectTransform parent, string objectName, MazeVectorRecipe recipe, MazeVectorProfile profile, MazeVectorLayerSet layers, MazeVectorState state, MazeVectorRenderMode renderMode = MazeVectorRenderMode.Overlay)
        {
            var batch = pooledBatches.Count > 0 ? pooledBatches.Pop() : CreateBatch();
            var rect = (RectTransform)batch.transform;
            ResetRect(rect, parent, objectName, recipe != null ? recipe.Size : Vector2.zero);
            batch.gameObject.SetActive(true);
            batch.enabled = true;
            batch.raycastTarget = false;
            batch.canvasRenderer.SetAlpha(1f);
            batch.SetPaint(null);
            batch.SetRecipe(recipe, profile, layers, state);
            batch.SetProgress(1f);
            batch.SetOutputIntensity(MazeVectorUiOutput.NeutralIntensity);
            if (!activeBatches.Contains(batch))
            {
                activeBatches.Add(batch);
            }
            return batch;
        }

        public MazeVectorAssetGraphic GetAssetGraphic(RectTransform parent, string objectName, MazeVectorAsset asset, MazeVectorProfile profile, MazeVectorRenderMode renderMode = MazeVectorRenderMode.Overlay, MazeVectorState state = MazeVectorState.Idle)
        {
            var graphic = pooledAssetGraphics.Count > 0 ? pooledAssetGraphics.Pop() : CreateAssetGraphic();
            var rect = (RectTransform)graphic.transform;
            ResetRect(rect, parent, objectName, asset != null ? asset.SourceSize : Vector2.zero);
            graphic.gameObject.SetActive(true);
            graphic.enabled = true;
            graphic.raycastTarget = false;
            graphic.canvasRenderer.SetAlpha(1f);
            graphic.SetRenderMode(renderMode);
            graphic.SetAsset(asset, profile, state);
            graphic.SetPaint(null);
            graphic.SetProgress(1f);
            graphic.SetOutputIntensity(MazeVectorUiOutput.NeutralIntensity);
            graphic.MarkGeometryDirty();
            if (!activeAssetGraphics.Contains(graphic))
            {
                activeAssetGraphics.Add(graphic);
            }
            return graphic;
        }

        public void ClearLists()
        {
            activeGraphics.Clear();
            pooledGraphics.Clear();
            activeBatches.Clear();
            pooledBatches.Clear();
            activeAssetGraphics.Clear();
            pooledAssetGraphics.Clear();
        }

        public void ReleaseAll()
        {
            for (var i = activeGraphics.Count - 1; i >= 0; i--)
            {
                Release(activeGraphics[i]);
            }
            activeGraphics.Clear();

            for (var i = activeBatches.Count - 1; i >= 0; i--)
            {
                Release(activeBatches[i]);
            }
            activeBatches.Clear();

            for (var i = activeAssetGraphics.Count - 1; i >= 0; i--)
            {
                Release(activeAssetGraphics[i]);
            }
            activeAssetGraphics.Clear();

        }

        public void Release(IMazeVectorRenderer renderer)
        {
            if (renderer is MazeVectorGraphic graphic)
            {
                Release(graphic);
            }
            else if (renderer is MazeVectorBatchGraphic batch)
            {
                Release(batch);
            }
            else if (renderer is MazeVectorAssetGraphic assetGraphic)
            {
                Release(assetGraphic);
            }
        }

        public void Release(MazeVectorGraphic graphic)
        {
            if (graphic == null)
            {
                return;
            }
            if (!activeGraphics.Remove(graphic))
            {
                return;
            }
            if (pooledGraphics.Contains(graphic))
            {
                return;
            }
            graphic.gameObject.SetActive(false);
            graphic.transform.SetParent(transform, false);
            pooledGraphics.Push(graphic);
        }

        public void Release(MazeVectorBatchGraphic batch)
        {
            if (batch == null)
            {
                return;
            }
            if (!activeBatches.Remove(batch))
            {
                return;
            }
            if (pooledBatches.Contains(batch))
            {
                return;
            }
            batch.gameObject.SetActive(false);
            batch.transform.SetParent(transform, false);
            pooledBatches.Push(batch);
        }

        public void Release(MazeVectorAssetGraphic graphic)
        {
            if (graphic == null)
            {
                return;
            }
            if (!activeAssetGraphics.Remove(graphic))
            {
                return;
            }
            if (pooledAssetGraphics.Contains(graphic))
            {
                return;
            }
            graphic.gameObject.SetActive(false);
            graphic.transform.SetParent(transform, false);
            pooledAssetGraphics.Push(graphic);
        }

        private MazeVectorGraphic CreateGraphic()
        {
            var go = new GameObject("Maze Vector", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(transform, false);
            CreatedCount++;
            return go.AddComponent<MazeVectorGraphic>();
        }

        private MazeVectorBatchGraphic CreateBatch()
        {
            var go = new GameObject("Maze Vector Batch", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(transform, false);
            CreatedBatchCount++;
            return go.AddComponent<MazeVectorBatchGraphic>();
        }

        private MazeVectorAssetGraphic CreateAssetGraphic()
        {
            var go = new GameObject("Maze Vector Asset", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(transform, false);
            CreatedAssetCount++;
            return go.AddComponent<MazeVectorAssetGraphic>();
        }

        private static void ResetRect(RectTransform rect, RectTransform parent, string objectName, Vector2 size)
        {
            rect.SetParent(parent, false);
            rect.name = string.IsNullOrWhiteSpace(objectName) ? "Maze Vector" : objectName;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition3D = Vector3.zero;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            if (size != Vector2.zero)
            {
                rect.sizeDelta = size;
            }
        }
    }
}
