using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Maze
{
    public sealed class MazeVectorGroup
    {
        private readonly List<IMazeVectorRenderer> graphics = new(8);

        public IReadOnlyList<IMazeVectorRenderer> Graphics => graphics;
        public int Count => graphics.Count;

        public void Clear()
        {
            graphics.Clear();
        }

        public void Add(IMazeVectorRenderer graphic)
        {
            if (graphic != null && !graphics.Contains(graphic))
            {
                graphics.Add(graphic);
            }
        }

        public void SetSelected(bool selected)
        {
            for (var i = 0; i < graphics.Count; i++)
            {
                if (graphics[i] != null)
                {
                    graphics[i].SetSelected(selected);
                }
            }
        }

        public void SetState(MazeVectorState state)
        {
            for (var i = 0; i < graphics.Count; i++)
            {
                graphics[i]?.SetState(state);
            }
        }

        public void SetProgress(float progress)
        {
            for (var i = 0; i < graphics.Count; i++)
            {
                graphics[i]?.SetProgress(progress);
            }
        }

        public void SetVisible(bool visible)
        {
            for (var i = 0; i < graphics.Count; i++)
            {
                graphics[i]?.SetVisible(visible);
            }
        }

        public void SetAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            for (var i = 0; i < graphics.Count; i++)
            {
                if (graphics[i] is Graphic uiGraphic)
                {
                    uiGraphic.canvasRenderer.SetAlpha(alpha);
                }
            }
        }

        public void MarkGeometryDirty()
        {
            for (var i = 0; i < graphics.Count; i++)
            {
                graphics[i]?.MarkGeometryDirty();
            }
        }

        public void ReleaseToPool(MazeVectorPool pool)
        {
            if (pool == null)
            {
                SetVisible(false);
                Clear();
                return;
            }

            for (var i = graphics.Count - 1; i >= 0; i--)
            {
                pool.Release(graphics[i]);
            }
            Clear();
        }
    }

    public static class MazeVectorIconFactory
    {
        public static MazeVectorGroup Build(RectTransform parent, MazeVectorIconDefinition definition, MazeVectorProfile profile, MazeVectorPool pool, MazeVectorRenderMode renderMode, MazeVectorState state = MazeVectorState.Idle)
        {
            var group = new MazeVectorGroup();
            if (parent == null || definition == null || definition.parts == null || pool == null)
            {
                return group;
            }

            for (var i = 0; i < definition.parts.Length; i++)
            {
                var part = definition.parts[i];
                if (part == null || part.shape == null)
                {
                    continue;
                }
                var style = part.useStyleOverride && part.styleOverride != null
                    ? part.styleOverride.Clone()
                    : profile != null
                        ? profile.Resolve(part.role, state)
                        : MazeVectorStyle.Stroke(Color.white, 4f);
                var graphic = pool.GetGraphic(parent, string.IsNullOrWhiteSpace(part.name) ? "Icon Part" : part.name, part.shape, style, renderMode);
                graphic.SetPaint(part.paint);
                var rect = graphic.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = part.size == Vector2.zero ? definition.size : part.size;
                rect.anchoredPosition = part.position;
                group.Add(graphic);
            }
            return group;
        }
    }
}
