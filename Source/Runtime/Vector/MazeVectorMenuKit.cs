using UnityEngine;
using UnityEngine.UI;

namespace Maze
{
    public readonly struct MazeVectorIconButtonParts
    {
        public readonly RectTransform root;
        public readonly MazeVectorGroup plate;
        public readonly MazeVectorGroup frame;
        public readonly MazeVectorGroup icon;
        public readonly Text label;

        public MazeVectorIconButtonParts(RectTransform root, MazeVectorGroup plate, MazeVectorGroup frame, MazeVectorGroup icon, Text label)
        {
            this.root = root;
            this.plate = plate;
            this.frame = frame;
            this.icon = icon;
            this.label = label;
        }

        public void SetSelected(bool selected)
        {
            plate?.SetSelected(selected);
            frame?.SetSelected(selected);
            icon?.SetSelected(selected);
        }
    }

    public readonly struct MazeVectorValueRowParts
    {
        public readonly RectTransform root;
        public readonly MazeVectorGroup plate;
        public readonly MazeVectorGroup selector;
        public readonly Text label;
        public readonly Text value;
        public readonly Text applyHint;

        public MazeVectorValueRowParts(RectTransform root, MazeVectorGroup plate, MazeVectorGroup selector, Text label, Text value, Text applyHint)
        {
            this.root = root;
            this.plate = plate;
            this.selector = selector;
            this.label = label;
            this.value = value;
            this.applyHint = applyHint;
        }

        public void SetSelected(bool selected)
        {
            plate?.SetSelected(selected);
            selector?.SetSelected(selected);
        }
    }

    public static class MazeVectorMenuKit
    {
        public static RectTransform CreateRect(string name, RectTransform parent, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(string.IsNullOrWhiteSpace(name) ? "Vector UI" : name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static Text CreateText(string name, RectTransform parent, string text, Vector2 position, Vector2 size, Font font, int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var label = new GameObject(string.IsNullOrWhiteSpace(name) ? "Label" : name, typeof(RectTransform)).AddComponent<Text>();
            label.rectTransform.SetParent(parent, false);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = size;
            label.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.text = text ?? string.Empty;
            label.raycastTarget = false;
            return label;
        }

        public static MazeVectorIconButtonParts CreateIconButton(MazeVectorSurface surface, RectTransform parent, string label, MazeVectorIconDefinition icon, Vector2 position, Vector2 size, Font font = null, MazeVectorFrameVariant frameVariant = MazeVectorFrameVariant.CornerOnly)
        {
            var root = CreateRect(string.IsNullOrWhiteSpace(label) ? "Icon Button" : label + " Button", parent != null ? parent : surface.Root, position, size);
            var plateRecipe = new MazeVectorRecipe("icon_button_plate", size)
                .Add("Plate", MazeVectorShape.Rect(size), size, Vector2.zero, MazeVectorReferenceKit.Button(MazeVectorState.Idle, 1.7f, 0.05f, 0.85f, 0f), MazeVectorLayer.Fill, MazeVectorRole.ButtonPlate);
            var plate = surface.BuildStaticBatch(root, plateRecipe, null, MazeVectorLayerSet.ArtOnly, MazeVectorState.Idle);
            var frame = surface.BuildIcon(root, MazeVectorReferenceKit.Frame(size, frameVariant, 1.8f, Mathf.Min(size.x, size.y) * 0.19f, 7f), null, MazeVectorState.Idle);

            var iconSize = new Vector2(size.x * 0.46f, size.y * 0.46f);
            var iconRoot = CreateRect("Icon", root, new Vector2(0f, size.y * 0.08f), iconSize);
            var iconGroup = surface.BuildIcon(iconRoot, icon, null, MazeVectorState.Idle);
            var text = CreateText("Label", root, label, new Vector2(0f, -size.y * 0.58f), new Vector2(size.x * 1.35f, 34f), font, 22, new Color(0.72f, 0.46f, 0.95f, 0.92f));
            return new MazeVectorIconButtonParts(root, plate, frame, iconGroup, text);
        }

        public static MazeVectorSelector CreateSelector(MazeVectorSurface surface, RectTransform parent, Vector2 position, Vector2 size, MazeVectorSelectorVariant variant = MazeVectorSelectorVariant.SideTicks, float smoothSeconds = 0.045f)
        {
            var root = CreateRect("Vector Selector", parent != null ? parent : surface.Root, position, size);
            var selector = root.gameObject.AddComponent<MazeVectorSelector>();
            selector.SmoothSeconds = smoothSeconds;
            selector.Build(surface, size, variant);
            return selector;
        }

        public static MazeVectorValueRowParts CreateValueRow(MazeVectorSurface surface, RectTransform parent, string label, string value, Vector2 position, Vector2 size, Font font = null, bool showApplyHint = false)
        {
            var root = CreateRect(string.IsNullOrWhiteSpace(label) ? "Value Row" : label + " Row", parent != null ? parent : surface.Root, position, size);
            var recipe = new MazeVectorRecipe("value_row", size)
                .Add("Row Frame", MazeVectorShape.Rect(size), size, Vector2.zero, MazeVectorReferenceKit.Outline(1.7f, 0.82f), MazeVectorLayer.Stroke, MazeVectorRole.PanelFrame)
                .Add("Value Cell", MazeVectorShape.Rect(new Vector2(size.x * 0.26f, size.y - 18f)), new Vector2(size.x * 0.26f, size.y - 18f), new Vector2(size.x * 0.32f, 0f), MazeVectorReferenceKit.Outline(1.35f, 0.78f), MazeVectorLayer.Accent, MazeVectorRole.Accent);
            var plate = surface.BuildStaticBatch(root, recipe, null, MazeVectorLayerSet.ArtOnly, MazeVectorState.Idle);
            var selector = surface.BuildIcon(root, MazeVectorReferenceKit.Selector(size, MazeVectorSelectorVariant.GappedSides, 1.2f, 3.4f, 18f, 34f, 12f), null, MazeVectorState.Idle);
            var labelText = CreateText("Label", root, label, new Vector2(-size.x * 0.34f, 0f), new Vector2(size.x * 0.42f, size.y), font, 24, new Color(0.72f, 0.46f, 0.95f, 0.9f), TextAnchor.MiddleLeft);
            var valueText = CreateText("Value", root, value, new Vector2(size.x * 0.32f, 0f), new Vector2(size.x * 0.22f, size.y), font, 22, new Color(0.84f, 0.68f, 1f, 0.92f));
            var applyText = showApplyHint ? CreateText("Apply Hint", root, "LMB APPLY", new Vector2(size.x * 0.56f, 0f), new Vector2(130f, size.y), font, 18, new Color(0.9f, 0.68f, 1f, 0.92f)) : null;
            return new MazeVectorValueRowParts(root, plate, selector, labelText, valueText, applyText);
        }
    }
}
