using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Maze
{
    public enum MazeControlGlyphKind
    {
        None,
        KeyboardKey,
        KeyboardCluster,
        KeyboardPairHorizontal,
        KeyboardPairVertical,
        MouseLeft,
        MouseRight,
        DPad,
        DPadHorizontal,
        DPadVertical,
        GamepadButton,
        GamepadStart
    }

    [Serializable]
    public struct MazeControlGlyph
    {
        public MazeControlGlyphKind kind;
        public string text;

        public MazeControlGlyph(MazeControlGlyphKind kind, string text = null)
        {
            this.kind = kind;
            this.text = text;
        }

        public bool HasGlyph => kind != MazeControlGlyphKind.None;
    }

    public static class MazeControlGlyphResolver
    {
        public static MazeControlGlyph Resolve(string display, bool gamepad)
        {
            if (string.IsNullOrWhiteSpace(display))
            {
                return new MazeControlGlyph(gamepad ? MazeControlGlyphKind.GamepadButton : MazeControlGlyphKind.KeyboardKey, "?");
            }

            var normalized = Normalize(display);
            if (gamepad)
            {
                if (normalized.Contains("dpad") || normalized.Contains("d-pad")) return new MazeControlGlyph(MazeControlGlyphKind.DPad);
                if (normalized.Contains("leftstick") || normalized.Contains("stick")) return new MazeControlGlyph(MazeControlGlyphKind.DPad);
                if (normalized.Contains("start") || normalized.Contains("menu")) return new MazeControlGlyph(MazeControlGlyphKind.GamepadStart);
                if (normalized.Contains("south") || normalized == "a") return new MazeControlGlyph(MazeControlGlyphKind.GamepadButton, "A");
                if (normalized.Contains("east") || normalized == "b") return new MazeControlGlyph(MazeControlGlyphKind.GamepadButton, "B");
                if (normalized.Contains("west") || normalized == "x") return new MazeControlGlyph(MazeControlGlyphKind.GamepadButton, "X");
                if (normalized.Contains("north") || normalized == "y") return new MazeControlGlyph(MazeControlGlyphKind.GamepadButton, "Y");
                return new MazeControlGlyph(MazeControlGlyphKind.GamepadButton, Shorten(display, 3));
            }

            if (normalized.Contains("leftbutton") || normalized.Contains("leftclick") || normalized == "lmb") return new MazeControlGlyph(MazeControlGlyphKind.MouseLeft);
            if (normalized.Contains("rightbutton") || normalized.Contains("rightclick") || normalized == "rmb") return new MazeControlGlyph(MazeControlGlyphKind.MouseRight);
            if (HasAll(normalized, "w", "a", "s", "d")) return new MazeControlGlyph(MazeControlGlyphKind.KeyboardCluster, "WASD");
            if (HasAll(normalized, "a", "d") && !HasAll(normalized, "w", "s")) return new MazeControlGlyph(MazeControlGlyphKind.KeyboardPairHorizontal, "A/D");
            if (HasAll(normalized, "w", "s") && !HasAll(normalized, "a", "d")) return new MazeControlGlyph(MazeControlGlyphKind.KeyboardPairVertical, "W/S");
            return new MazeControlGlyph(MazeControlGlyphKind.KeyboardKey, Shorten(display, 7));
        }

        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace(" ", string.Empty)
                    .Replace("_", string.Empty)
                    .Replace("/", string.Empty)
                    .Replace("|", string.Empty)
                    .Replace(":", string.Empty)
                    .Replace("+", string.Empty)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
        }

        private static bool HasAll(string value, params string[] parts)
        {
            for (var i = 0; i < parts.Length; i++)
            {
                if (!value.Contains(parts[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static string Shorten(string value, int max)
        {
            var trimmed = string.IsNullOrWhiteSpace(value) ? "?" : value.Trim();
            if (trimmed.Length <= max)
            {
                return trimmed;
            }
            return trimmed.Substring(0, max).Trim();
        }
    }

    public static class MazeVectorControlGlyphs
    {
        public static Vector2 SizeFor(MazeControlGlyph glyph)
        {
            return glyph.kind switch
            {
                MazeControlGlyphKind.KeyboardCluster => new Vector2(150f, 39f),
                MazeControlGlyphKind.KeyboardPairHorizontal => new Vector2(54f, 28f),
                MazeControlGlyphKind.KeyboardPairVertical => new Vector2(28f, 54f),
                MazeControlGlyphKind.MouseLeft => new Vector2(36f, 40f),
                MazeControlGlyphKind.MouseRight => new Vector2(36f, 40f),
                MazeControlGlyphKind.DPad => new Vector2(38f, 38f),
                MazeControlGlyphKind.DPadHorizontal => new Vector2(52f, 30f),
                MazeControlGlyphKind.DPadVertical => new Vector2(30f, 52f),
                MazeControlGlyphKind.GamepadButton => new Vector2(34f, 34f),
                MazeControlGlyphKind.GamepadStart => new Vector2(38f, 28f),
                _ => new Vector2(30f, 30f)
            };
        }

        public static MazeVectorGroup Build(RectTransform parent, MazeControlGlyph glyph, MazeVectorPool pool, MazeVectorRenderMode renderMode, MazeVectorProfile profile, Font font, Color textColor, MazeVectorState state = MazeVectorState.Idle)
        {
            var group = new MazeVectorGroup();
            if (parent == null || pool == null || glyph.kind == MazeControlGlyphKind.None)
            {
                return group;
            }

            switch (glyph.kind)
            {
                case MazeControlGlyphKind.KeyboardCluster:
                    BuildKey(group, parent, "W", new Vector2(-54f, 0f), 36.4f, pool, renderMode, profile, font, textColor, state);
                    BuildKey(group, parent, "A", new Vector2(-18f, 0f), 36.4f, pool, renderMode, profile, font, textColor, state);
                    BuildKey(group, parent, "S", new Vector2(18f, 0f), 36.4f, pool, renderMode, profile, font, textColor, state);
                    BuildKey(group, parent, "D", new Vector2(54f, 0f), 36.4f, pool, renderMode, profile, font, textColor, state);
                    break;
                case MazeControlGlyphKind.KeyboardPairHorizontal:
                    BuildKeyPair(group, parent, glyph.text, true, pool, renderMode, profile, font, textColor, state);
                    break;
                case MazeControlGlyphKind.KeyboardPairVertical:
                    BuildKeyPair(group, parent, glyph.text, false, pool, renderMode, profile, font, textColor, state);
                    break;
                case MazeControlGlyphKind.MouseLeft:
                case MazeControlGlyphKind.MouseRight:
                    BuildMouse(group, parent, glyph.kind == MazeControlGlyphKind.MouseRight, pool, renderMode, profile, state);
                    break;
                case MazeControlGlyphKind.DPad:
                    BuildDPad(group, parent, Vector2.zero, 36f, pool, renderMode, profile, state, 0);
                    break;
                case MazeControlGlyphKind.DPadHorizontal:
                    BuildDPad(group, parent, new Vector2(-13f, 0f), 27f, pool, renderMode, profile, state, 3);
                    BuildDPad(group, parent, new Vector2(13f, 0f), 27f, pool, renderMode, profile, state, 1);
                    break;
                case MazeControlGlyphKind.DPadVertical:
                    BuildDPad(group, parent, new Vector2(0f, 13f), 27f, pool, renderMode, profile, state, 4);
                    BuildDPad(group, parent, new Vector2(0f, -13f), 27f, pool, renderMode, profile, state, 2);
                    break;
                case MazeControlGlyphKind.GamepadButton:
                    BuildGamepadButton(group, parent, glyph.text, pool, renderMode, profile, font, textColor, state);
                    break;
                case MazeControlGlyphKind.GamepadStart:
                    BuildGamepadButton(group, parent, "MENU", pool, renderMode, profile, font, textColor, state);
                    break;
                default:
                    BuildKey(group, parent, SafeText(glyph.text), Vector2.zero, 30f, pool, renderMode, profile, font, textColor, state);
                    break;
            }

            return group;
        }

        private static void BuildKeyPair(MazeVectorGroup group, RectTransform parent, string text, bool horizontal, MazeVectorPool pool, MazeVectorRenderMode renderMode, MazeVectorProfile profile, Font font, Color textColor, MazeVectorState state)
        {
            var keys = SplitGlyphText(text, horizontal ? "A" : "W", horizontal ? "D" : "S");
            if (horizontal)
            {
                BuildKey(group, parent, keys.Item1, new Vector2(-13f, 0f), 25f, pool, renderMode, profile, font, textColor, state);
                BuildKey(group, parent, keys.Item2, new Vector2(13f, 0f), 25f, pool, renderMode, profile, font, textColor, state);
            }
            else
            {
                BuildKey(group, parent, keys.Item1, new Vector2(0f, 13f), 25f, pool, renderMode, profile, font, textColor, state);
                BuildKey(group, parent, keys.Item2, new Vector2(0f, -13f), 25f, pool, renderMode, profile, font, textColor, state);
            }
        }

        private static void BuildKey(MazeVectorGroup group, RectTransform parent, string text, Vector2 position, float size, MazeVectorPool pool, MazeVectorRenderMode renderMode, MazeVectorProfile profile, Font font, Color textColor, MazeVectorState state)
        {
            var root = CreateRect("Key " + SafeText(text), parent, new Vector2(size, size), position);
            if (MazeVectorControlIconAssets.TryGetKey(text, out var importedKey))
            {
                var graphic = pool.GetAssetGraphic(root, importedKey.name, importedKey, profile, renderMode, state);
                var rect = graphic.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(size, size);
                rect.anchoredPosition = Vector2.zero;
                group.Add(graphic);
                return;
            }
            var t = Mathf.Clamp(size / 40f, 0.45f, 1.2f);
            var outer = new Vector2(size, size);
            var middle = outer * 0.82f;
            var inner = outer * 0.56f;
            var parts = new[]
            {
                Part("Key Plate", MazeVectorShape.RoundedRect(outer, size * 0.16f), outer, Vector2.zero, PlateStyle(profile, state, t)),
                Part("Key Outer Ring", MazeVectorShape.RoundedRect(middle, size * 0.13f), middle, Vector2.zero, StrokeStyle(profile, state, 1.55f * t)),
                Part("Key Inner Ring", MazeVectorShape.RoundedRect(inner, size * 0.09f), inner, Vector2.zero, StrokeStyle(profile, state, 1.15f * t))
            };
            BuildDefinition(group, root, "locked_key_" + SafeText(text), outer, parts, pool, renderMode, state);
            BuildText(root, SafeText(text), Vector2.zero, outer, font, textColor, KeyFontSize(text, size));
        }

        private static void BuildMouse(MazeVectorGroup group, RectTransform parent, bool right, MazeVectorPool pool, MazeVectorRenderMode renderMode, MazeVectorProfile profile, MazeVectorState state)
        {
            var size = SizeFor(new MazeControlGlyph(right ? MazeControlGlyphKind.MouseRight : MazeControlGlyphKind.MouseLeft));
            var root = CreateRect(right ? "Mouse Right" : "Mouse Left", parent, size, Vector2.zero);
            if (MazeVectorControlIconAssets.TryGetMouse(right, out var importedMouse))
            {
                var graphic = pool.GetAssetGraphic(root, importedMouse.name, importedMouse, profile, renderMode, state);
                var rect = graphic.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                rect.anchoredPosition = Vector2.zero;
                group.Add(graphic);
                return;
            }
            var t = Mathf.Clamp(size.y / 40f, 0.45f, 1.2f);
            var body = new Vector2(size.x * 0.7f, size.y * 0.88f);
            var button = new Vector2(size.x * 0.22f, size.y * 0.24f);
            var buttonX = right ? body.x * 0.18f : -body.x * 0.18f;
            var parts = new List<MazeVectorIconPart>
            {
                Part("Mouse Body", MazeVectorShape.RoundedRect(body, size.x * 0.22f), body, Vector2.zero, PlateStyle(profile, state, t)),
                Part("Mouse Inner", MazeVectorShape.RoundedRect(body * 0.72f, size.x * 0.13f), body * 0.72f, new Vector2(0f, -size.y * 0.03f), StrokeStyle(profile, state, 1.05f * t)),
                Part("Mouse Split", MazeVectorShape.Line(new Vector2(0f, 0.82f), new Vector2(0f, 0.2f)), body, Vector2.zero, StrokeStyle(profile, state, 1f * t)),
                Part(right ? "Right Button" : "Left Button", MazeVectorShape.RoundedRect(button, size.x * 0.04f), button, new Vector2(buttonX, body.y * 0.25f), FillStyle(profile, state))
            };
            BuildDefinition(group, root, right ? "locked_mouse_right" : "locked_mouse_left", size, parts.ToArray(), pool, renderMode, state);
        }

        private static void BuildDPad(MazeVectorGroup group, RectTransform parent, Vector2 position, float size, MazeVectorPool pool, MazeVectorRenderMode renderMode, MazeVectorProfile profile, MazeVectorState state, int direction)
        {
            var root = CreateRect("DPad", parent, new Vector2(size, size), position);
            if (MazeVectorControlIconAssets.TryGetDPad(direction, out var importedDPad))
            {
                var graphic = pool.GetAssetGraphic(root, importedDPad.name, importedDPad, profile, renderMode, state);
                var rect = graphic.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(size, size);
                rect.anchoredPosition = Vector2.zero;
                group.Add(graphic);
                return;
            }
            var t = Mathf.Clamp(size / 38f, 0.45f, 1.2f);
            var parts = new List<MazeVectorIconPart>
            {
                Part("DPad Plate", MazeVectorShape.RoundedRect(new Vector2(size, size), size * 0.14f), new Vector2(size, size), Vector2.zero, PlateStyle(profile, state, t)),
                Part("DPad Horizontal", MazeVectorShape.Line(new Vector2(-0.62f, 0f), new Vector2(0.62f, 0f)), new Vector2(size, size), Vector2.zero, StrokeStyle(profile, state, 1.25f * t)),
                Part("DPad Vertical", MazeVectorShape.Line(new Vector2(0f, -0.62f), new Vector2(0f, 0.62f)), new Vector2(size, size), Vector2.zero, StrokeStyle(profile, state, 1.25f * t))
            };
            if (direction != 0)
            {
                parts.Add(Part("DPad Direction", Triangle(direction), new Vector2(size * 0.32f, size * 0.32f), DirectionPosition(direction, size * 0.25f), FillStyle(profile, state)));
            }
            BuildDefinition(group, root, "locked_dpad", new Vector2(size, size), parts.ToArray(), pool, renderMode, state);
        }

        private static void BuildGamepadButton(MazeVectorGroup group, RectTransform parent, string text, MazeVectorPool pool, MazeVectorRenderMode renderMode, MazeVectorProfile profile, Font font, Color textColor, MazeVectorState state)
        {
            var size = 34f;
            var root = CreateRect("Gamepad Button", parent, new Vector2(size, size), Vector2.zero);
            if (MazeVectorControlIconAssets.TryGetGamepadButton(text, out var importedButton))
            {
                var graphic = pool.GetAssetGraphic(root, importedButton.name, importedButton, profile, renderMode, state);
                var rect = graphic.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(size, size);
                rect.anchoredPosition = Vector2.zero;
                group.Add(graphic);
                return;
            }
            var t = size / 34f;
            var parts = new List<MazeVectorIconPart>
            {
                Part("Button Plate", MazeVectorShape.Circle(size * 0.45f, 32), new Vector2(size, size), Vector2.zero, PlateStyle(profile, state, t)),
                Part("Button Inner", MazeVectorShape.Circle(size * 0.28f, 32), new Vector2(size, size), Vector2.zero, StrokeStyle(profile, state, 1.05f * t))
            };
            var marker = FaceMarker(text, size * 0.16f);
            if (marker != Vector2.zero)
            {
                parts.Add(Part("Button Direction", MazeVectorShape.Circle(size * 0.08f, 16), new Vector2(size, size), marker, FillStyle(profile, state)));
            }
            BuildDefinition(group, root, "locked_gamepad_button", new Vector2(size, size), parts.ToArray(), pool, renderMode, state);
        }

        private static void BuildDefinition(MazeVectorGroup group, RectTransform root, string id, Vector2 size, MazeVectorIconPart[] parts, MazeVectorPool pool, MazeVectorRenderMode renderMode, MazeVectorState state)
        {
            var definition = new MazeVectorIconDefinition { id = id, size = size, parts = parts };
            var built = MazeVectorIconFactory.Build(root, definition, null, pool, renderMode, state);
            for (var i = 0; i < built.Graphics.Count; i++) group.Add(built.Graphics[i]);
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static MazeVectorIconPart Part(string name, MazeVectorShape shape, Vector2 size, Vector2 position, MazeVectorStyle style)
        {
            return new MazeVectorIconPart { name = name, shape = shape, size = size, position = position, role = MazeVectorRole.ButtonIcon, useStyleOverride = true, styleOverride = style.Clone() };
        }

        private static MazeVectorStyle PlateStyle(MazeVectorProfile profile, MazeVectorState state, float scale)
        {
            var line = profile != null ? profile.Line : new Color(0.76f, 0.32f, 1f, 1f);
            var background = profile != null ? profile.Background : new Color(0.01f, 0.004f, 0.02f, 0.9f);
            return new MazeVectorStyle
            {
                fill = true,
                stroke = true,
                fillColor = background,
                strokeColor = line,
                strokeThickness = Mathf.Max(0.65f, 1.55f * scale),
                selectedFillColor = background,
                selectedStrokeColor = line,
                selectedStrokeThickness = Mathf.Max(0.8f, 1.75f * scale),
                strokeCap = MazeVectorStrokeCap.Round,
                strokeJoin = MazeVectorStrokeJoin.Round,
                cornerMode = MazeVectorCornerMode.Rounded
            };
        }

        private static MazeVectorStyle StrokeStyle(MazeVectorProfile profile, MazeVectorState state, float thickness)
        {
            var line = profile != null ? profile.Line : new Color(0.76f, 0.32f, 1f, 1f);
            return new MazeVectorStyle
            {
                fill = false,
                stroke = true,
                strokeColor = line,
                strokeThickness = Mathf.Max(0.55f, thickness),
                selectedStrokeColor = line,
                selectedStrokeThickness = Mathf.Max(0.7f, thickness),
                strokeCap = MazeVectorStrokeCap.Round,
                strokeJoin = MazeVectorStrokeJoin.Round,
                cornerMode = MazeVectorCornerMode.Rounded
            };
        }

        private static MazeVectorStyle FillStyle(MazeVectorProfile profile, MazeVectorState state)
        {
            var line = profile != null ? profile.Line : new Color(0.76f, 0.32f, 1f, 1f);
            return new MazeVectorStyle
            {
                fill = true,
                stroke = false,
                fillColor = line,
                selectedFillColor = line,
                cornerMode = MazeVectorCornerMode.Rounded
            };
        }

        private static void BuildText(RectTransform parent, string value, Vector2 position, Vector2 size, Font font, Color color, int fontSize)
        {
            var go = new GameObject("Control Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
        }

        private static int KeyFontSize(string value, float size)
        {
            var length = SafeText(value).Length;
            var scale = length <= 1 ? 0.48f : length <= 3 ? 0.34f : 0.23f;
            return Mathf.RoundToInt(Mathf.Clamp(size * scale, 7f, 16f));
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

        private static MazeVectorShape Triangle(int direction)
        {
            var points = direction switch
            {
                1 => new List<Vector2> { new(0.48f, 0f), new(-0.36f, 0.46f), new(-0.36f, -0.46f) },
                2 => new List<Vector2> { new(0f, -0.48f), new(-0.46f, 0.36f), new(0.46f, 0.36f) },
                3 => new List<Vector2> { new(-0.48f, 0f), new(0.36f, 0.46f), new(0.36f, -0.46f) },
                _ => new List<Vector2> { new(0f, 0.48f), new(-0.46f, -0.36f), new(0.46f, -0.36f) }
            };
            return new MazeVectorShape { kind = MazeVectorShapeKind.Polygon, points = points, closed = true };
        }

        private static Vector2 DirectionPosition(int direction, float offset)
        {
            return direction switch
            {
                1 => new Vector2(offset, 0f),
                2 => new Vector2(0f, -offset),
                3 => new Vector2(-offset, 0f),
                _ => new Vector2(0f, offset)
            };
        }

        private static Vector2 FaceMarker(string text, float offset)
        {
            return MazeControlGlyphResolver.Normalize(text) switch
            {
                "y" or "north" => new Vector2(0f, offset),
                "b" or "east" => new Vector2(offset, 0f),
                "a" or "south" => new Vector2(0f, -offset),
                "x" or "west" => new Vector2(-offset, 0f),
                _ => Vector2.zero
            };
        }

        private static string SafeText(string text, string fallback = "?")
        {
            return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
        }
    }
}
