using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Maze
{
    /* Variant-only sharp procedural control glyphs. Not used by the default control hint bar. */
    public static class MazeVectorSharpControlGlyphs
    {
        public static Vector2 SizeFor(MazeControlGlyph glyph)
        {
            return glyph.kind switch
            {
                MazeControlGlyphKind.KeyboardCluster => new Vector2(86f, 54f),
                MazeControlGlyphKind.KeyboardPairHorizontal => new Vector2(78f, 38f),
                MazeControlGlyphKind.KeyboardPairVertical => new Vector2(38f, 60f),
                MazeControlGlyphKind.MouseLeft => new Vector2(46f, 52f),
                MazeControlGlyphKind.MouseRight => new Vector2(46f, 52f),
                MazeControlGlyphKind.DPad => new Vector2(58f, 58f),
                MazeControlGlyphKind.DPadHorizontal => new Vector2(64f, 38f),
                MazeControlGlyphKind.DPadVertical => new Vector2(34f, 56f),
                MazeControlGlyphKind.GamepadButton => new Vector2(42f, 42f),
                MazeControlGlyphKind.GamepadStart => new Vector2(54f, 30f),
                _ => new Vector2(Mathf.Clamp(24f + SafeText(glyph.text).Length * 8f, 34f, 86f), 34f)
            };
        }

        public static MazeVectorGroup Build(RectTransform parent, MazeControlGlyph glyph, MazeVectorPool pool, MazeVectorRenderMode renderMode, MazeVectorProfile profile, Font font, Color textColor, MazeVectorState state = MazeVectorState.Idle)
        {
            var group = new MazeVectorGroup();
            if (parent == null || pool == null || glyph.kind == MazeControlGlyphKind.None)
            {
                return group;
            }

            var size = SizeFor(glyph);
            var parts = new List<MazeVectorIconPart>(8);
            var plate = PlateStyle(profile, state);
            var line = LineStyle(profile, state);
            var accent = AccentStyle(profile, state);

            switch (glyph.kind)
            {
                case MazeControlGlyphKind.KeyboardCluster:
                    AddKey(parts, "W", new Vector2(0f, 13f), new Vector2(24f, 20f), plate, line);
                    AddKey(parts, "A", new Vector2(-24f, -11f), new Vector2(24f, 20f), plate, line);
                    AddKey(parts, "S", new Vector2(0f, -11f), new Vector2(24f, 20f), plate, line);
                    AddKey(parts, "D", new Vector2(24f, -11f), new Vector2(24f, 20f), plate, line);
                    BuildLetters(parent, new[] { ("W", new Vector2(0f, 13f)), ("A", new Vector2(-24f, -11f)), ("S", new Vector2(0f, -11f)), ("D", new Vector2(24f, -11f)) }, font, textColor, 12, new Vector2(24f, 20f));
                    break;
                case MazeControlGlyphKind.KeyboardPairHorizontal:
                    var pair = SplitGlyphText(glyph.text, "A", "D");
                    AddKey(parts, "Left Key", new Vector2(-18f, 0f), new Vector2(30f, 26f), plate, line);
                    AddKey(parts, "Right Key", new Vector2(18f, 0f), new Vector2(30f, 26f), plate, line);
                    BuildLetters(parent, new[] { (pair.Item1, new Vector2(-18f, 0f)), (pair.Item2, new Vector2(18f, 0f)) }, font, textColor, 13, new Vector2(30f, 26f));
                    break;
                case MazeControlGlyphKind.KeyboardPairVertical:
                    var vertical = SplitGlyphText(glyph.text, "W", "S");
                    AddKey(parts, "Up Key", new Vector2(0f, 13f), new Vector2(30f, 24f), plate, line);
                    AddKey(parts, "Down Key", new Vector2(0f, -13f), new Vector2(30f, 24f), plate, line);
                    BuildLetters(parent, new[] { (vertical.Item1, new Vector2(0f, 13f)), (vertical.Item2, new Vector2(0f, -13f)) }, font, textColor, 12, new Vector2(30f, 24f));
                    break;
                case MazeControlGlyphKind.MouseLeft:
                case MazeControlGlyphKind.MouseRight:
                    AddMouse(parts, glyph.kind == MazeControlGlyphKind.MouseRight, size, plate, line, accent);
                    break;
                case MazeControlGlyphKind.DPad:
                    parts.Add(Part("DPad Fill", new MazeVectorShape { kind = MazeVectorShapeKind.GridCells, size = new Vector2(42f, 42f), gridColumns = 3, gridRows = 3, gridGap = 4f }, new Vector2(48f, 48f), Vector2.zero, plate));
                    parts.Add(Part("DPad Cross", MazeVectorShape.Line(new Vector2(-0.62f, 0f), new Vector2(0.62f, 0f), new Vector2(0f, 0f), new Vector2(0f, -0.62f), new Vector2(0f, 0.62f)), new Vector2(48f, 48f), Vector2.zero, accent));
                    break;
                case MazeControlGlyphKind.DPadHorizontal:
                    parts.Add(Part("DPad Horizontal", MazeVectorShape.Line(new Vector2(-0.76f, 0f), new Vector2(0.76f, 0f)), size, Vector2.zero, line));
                    parts.Add(Part("DPad Left", Triangle(false), new Vector2(16f, 16f), new Vector2(-20f, 0f), accent));
                    parts.Add(Part("DPad Right", Triangle(true), new Vector2(16f, 16f), new Vector2(20f, 0f), accent));
                    break;
                case MazeControlGlyphKind.DPadVertical:
                    parts.Add(Part("DPad Vertical", MazeVectorShape.Line(new Vector2(0f, -0.76f), new Vector2(0f, 0.76f)), size, Vector2.zero, line));
                    break;
                case MazeControlGlyphKind.GamepadButton:
                    parts.Add(Part("Button Ring", MazeVectorShape.Circle(17f, 32), size, Vector2.zero, line));
                    parts.Add(Part("Button Fill", MazeVectorShape.Circle(13f, 32), size, Vector2.zero, plate));
                    BuildLetters(parent, new[] { (SafeText(glyph.text, "A"), Vector2.zero) }, font, textColor, 14, size);
                    break;
                case MazeControlGlyphKind.GamepadStart:
                    parts.Add(Part("Start Plate", MazeVectorShape.Rect(size), size, Vector2.zero, plate));
                    parts.Add(Part("Start Line", MazeVectorShape.Line(new Vector2(-0.45f, 0f), new Vector2(0.45f, 0f)), size, Vector2.zero, accent));
                    break;
                default:
                    parts.Add(Part("Key Plate", MazeVectorShape.Rect(size), size, Vector2.zero, plate));
                    BuildLetters(parent, new[] { (SafeText(glyph.text), Vector2.zero) }, font, textColor, Mathf.Clamp(14 - SafeText(glyph.text).Length / 3, 9, 14), size);
                    break;
            }

            var definition = new MazeVectorIconDefinition { id = "control_" + glyph.kind, size = size, parts = parts.ToArray() };
            var vectors = MazeVectorIconFactory.Build(parent, definition, null, pool, renderMode, state);
            for (var i = 0; i < vectors.Graphics.Count; i++) group.Add(vectors.Graphics[i]);
            var labels = parent.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                labels[i].transform.SetAsLastSibling();
            }
            return group;
        }

        private static void AddKey(List<MazeVectorIconPart> parts, string name, Vector2 position, Vector2 size, MazeVectorStyle plate, MazeVectorStyle line)
        {
            parts.Add(Part(name + " Fill", MazeVectorShape.Rect(size), size, position, plate));
            parts.Add(Part(name + " Edge", MazeVectorShape.Line(new Vector2(-0.48f, -0.42f), new Vector2(0.48f, -0.42f), new Vector2(0.48f, 0.42f), new Vector2(-0.48f, 0.42f), new Vector2(-0.48f, -0.42f)), size, position, line));
        }

        private static void AddMouse(List<MazeVectorIconPart> parts, bool right, Vector2 size, MazeVectorStyle plate, MazeVectorStyle line, MazeVectorStyle accent)
        {
            var bodySize = new Vector2(34f, 44f);
            parts.Add(Part("Mouse Body", MazeVectorShape.Rect(bodySize), bodySize, Vector2.zero, plate));
            parts.Add(Part("Mouse Edge", MazeVectorShape.Line(new Vector2(-0.48f, -0.62f), new Vector2(0.48f, -0.62f), new Vector2(0.48f, 0.62f), new Vector2(-0.48f, 0.62f), new Vector2(-0.48f, -0.62f)), bodySize, Vector2.zero, line));
            parts.Add(Part("Mouse Split", MazeVectorShape.Line(new Vector2(0f, 0.62f), new Vector2(0f, 0.12f)), bodySize, Vector2.zero, line));
            parts.Add(Part(right ? "Right Button" : "Left Button", MazeVectorShape.Rect(new Vector2(11f, 13f)), new Vector2(18f, 18f), new Vector2(right ? 8f : -8f, 12f), accent));
        }

        private static MazeVectorShape MouseBody()
        {
            return new MazeVectorShape { kind = MazeVectorShapeKind.Polygon, points = { new Vector2(0f, 0.82f), new Vector2(0.5f, 0.58f), new Vector2(0.56f, -0.4f), new Vector2(0.3f, -0.78f), new Vector2(-0.3f, -0.78f), new Vector2(-0.56f, -0.4f), new Vector2(-0.5f, 0.58f) }, closed = true };
        }

        private static MazeVectorShape Triangle(bool right)
        {
            return new MazeVectorShape
            {
                kind = MazeVectorShapeKind.Polygon,
                points = right
                    ? new List<Vector2> { new(0.48f, 0f), new(-0.36f, 0.46f), new(-0.36f, -0.46f) }
                    : new List<Vector2> { new(-0.48f, 0f), new(0.36f, 0.46f), new(0.36f, -0.46f) },
                closed = true
            };
        }

        private static MazeVectorIconPart Part(string name, MazeVectorShape shape, Vector2 size, Vector2 position, MazeVectorStyle style)
        {
            return new MazeVectorIconPart { name = name, shape = shape, size = size, position = position, role = MazeVectorRole.ButtonIcon, useStyleOverride = true, styleOverride = style.Clone() };
        }

        private static MazeVectorStyle PlateStyle(MazeVectorProfile profile, MazeVectorState state)
        {
            var style = profile != null ? profile.Resolve(MazeVectorRole.ButtonPlate, state) : MazeVectorStyle.Filled(new Color(0.02f, 0.01f, 0.04f, 0.82f));
            style.fill = true;
            style.stroke = false;
            return style;
        }

        private static MazeVectorStyle LineStyle(MazeVectorProfile profile, MazeVectorState state)
        {
            var style = profile != null ? profile.Resolve(MazeVectorRole.ButtonIcon, state) : MazeVectorStyle.Stroke(new Color(0.75f, 0.25f, 1.2f, 1f), 2.5f);
            style.fill = false;
            style.stroke = true;
            return style;
        }

        private static MazeVectorStyle AccentStyle(MazeVectorProfile profile, MazeVectorState state)
        {
            var style = profile != null ? profile.Resolve(MazeVectorRole.Accent, state) : MazeVectorStyle.Stroke(new Color(1f, 0.65f, 1.4f, 1f), 3f);
            style.fill = true;
            style.stroke = false;
            style.fillColor = style.selectedStrokeColor;
            style.selectedFillColor = style.selectedStrokeColor;
            return style;
        }

        private static Tuple<string, string> SplitGlyphText(string value, string fallbackA, string fallbackB)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                var parts = value.Split(new[] { '/', '+', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    return Tuple.Create(parts[0], parts[1]);
                }
            }
            return Tuple.Create(fallbackA, fallbackB);
        }

        private static void BuildLetters(RectTransform parent, (string text, Vector2 position)[] letters, Font font, Color color, int size, Vector2 boxSize)
        {
            if (parent == null || letters == null)
            {
                return;
            }
            for (var i = 0; i < letters.Length; i++)
            {
                var go = new GameObject("Glyph Label", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(parent, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = boxSize;
                rect.anchoredPosition = letters[i].position;
                var text = go.GetComponent<Text>();
                text.text = letters[i].text;
                text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = size;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = color;
                text.raycastTarget = false;
            }
        }

        private static string SafeText(string text, string fallback = "?")
        {
            return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
        }
    }
}
