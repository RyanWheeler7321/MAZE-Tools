using System;
using UnityEngine;

namespace Maze
{
    [CreateAssetMenu(menuName = "MAZE/Vector/Profile", fileName = "MazeVectorProfile")]
    public sealed class MazeVectorProfile : ScriptableObject
    {
        [Header("Core Colors")]
        [SerializeField] private Color background = new(0.01f, 0.004f, 0.02f, 0.78f);
        [SerializeField] private Color line = new(0.76f, 0.32f, 1.25f, 1f);
        [SerializeField] private Color selectedLine = new(1f, 0.74f, 1.55f, 1f);
        [SerializeField] private Color mutedLine = new(0.38f, 0.22f, 0.58f, 0.9f);
        [SerializeField] private Color selectedFill = new(0.055f, 0.014f, 0.095f, 0.7f);
        [SerializeField] private Color accent = new(0.58f, 0.28f, 0.94f, 1f);
        [SerializeField] private Color selectedAccent = new(0.76f, 0.49f, 1f, 1f);

        [Header("Gradient")]
        [SerializeField] private Color gradientTop = new(0.48f, 0.12f, 0.88f, 0.58f);
        [SerializeField] private Color gradientBottom = new(0.018f, 0.004f, 0.035f, 0.72f);
        [SerializeField] private MazeVectorShadingMode gradientMode = MazeVectorShadingMode.VerticalGradient;

        [Header("Weights")]
        [SerializeField, Min(0.1f)] private float thin = 1.4f;
        [SerializeField, Min(0.1f)] private float normal = 2.5f;
        [SerializeField, Min(0.1f)] private float thick = 4.8f;
        [SerializeField, Range(0f, 4f)] private float iconVariation = 0.22f;

        [Header("Shape")]
        [SerializeField] private MazeVectorCornerMode cornerMode = MazeVectorCornerMode.Sharp;
        [SerializeField] private MazeVectorStrokeCap strokeCap = MazeVectorStrokeCap.Square;
        [SerializeField] private MazeVectorStrokeJoin strokeJoin = MazeVectorStrokeJoin.Bevel;
        [SerializeField] private MazeVectorStyleKey[] overrides = Array.Empty<MazeVectorStyleKey>();

        public Color Background => background;
        public Color Line => line;
        public Color SelectedLine => selectedLine;
        public Color MutedLine => mutedLine;
        public Color Accent => accent;
        public Color SelectedAccent => selectedAccent;
        public Color GradientTop => gradientTop;
        public Color GradientBottom => gradientBottom;
        public MazeVectorShadingMode GradientMode => gradientMode;
        public float Thin => thin;
        public float Normal => normal;
        public float Thick => thick;

        public void ConfigureRuntime(Color background, Color line, Color selectedLine, Color mutedLine, Color selectedFill, float thin, float normal, float thick)
        {
            this.background = background;
            this.line = line;
            this.selectedLine = selectedLine;
            this.mutedLine = mutedLine;
            this.selectedFill = selectedFill;
            this.thin = Mathf.Max(0.1f, thin);
            this.normal = Mathf.Max(0.1f, normal);
            this.thick = Mathf.Max(0.1f, thick);
        }

        public void ConfigureImportedPalette(Color nextAccent, Color nextSelectedAccent)
        {
            accent = nextAccent;
            selectedAccent = nextSelectedAccent;
        }

        public MazeVectorStyle Resolve(MazeVectorRole role, MazeVectorState state)
        {
            if (overrides != null)
            {
                for (var i = 0; i < overrides.Length; i++)
                {
                    if (overrides[i].role == role && overrides[i].state == state && overrides[i].style != null)
                    {
                        return overrides[i].style.Clone();
                    }
                }
            }

            var selected = state == MazeVectorState.Selected || state == MazeVectorState.Pressed;
            var disabled = state == MazeVectorState.Disabled;
            var fill = background;
            var stroke = selected ? selectedLine : mutedLine;
            var width = selected ? thick : normal;
            var fillEnabled = role == MazeVectorRole.ButtonPlate || role == MazeVectorRole.PanelFrame || role == MazeVectorRole.HintBar || role == MazeVectorRole.LabelBacker || role == MazeVectorRole.GalleryCard;

            if (role == MazeVectorRole.ButtonIcon)
            {
                fillEnabled = false;
                stroke = selected ? selectedLine : line;
                width = selected ? thick : normal;
            }
            else if (role == MazeVectorRole.Accent)
            {
                fillEnabled = false;
                stroke = selected ? selectedAccent : accent;
                width = selected ? thick : normal;
            }

            if (disabled)
            {
                fill.a *= 0.36f;
                stroke.a *= 0.36f;
            }

            return new MazeVectorStyle
            {
                fill = fillEnabled,
                stroke = true,
                fillColor = fill,
                strokeColor = stroke,
                strokeThickness = width,
                selectedFillColor = selectedFill,
                selectedStrokeColor = selectedLine,
                selectedStrokeThickness = Mathf.Max(width, thick),
                strokeCap = strokeCap,
                strokeJoin = strokeJoin,
                cornerMode = cornerMode,
                shading = MazeVectorShadingMode.Flat,
                gradientTopColor = gradientTop,
                gradientBottomColor = gradientBottom,
                innerStrokeColor = new Color(selectedLine.r, selectedLine.g, selectedLine.b, selected ? 0.18f : 0.07f),
                innerStrokeThickness = fillEnabled ? thin : 0f
            };
        }

        public MazeVectorStyle Stroke(MazeVectorStrokePersonality personality = MazeVectorStrokePersonality.Plain, float thickness = -1f, float alpha = 1f, float intensity = 1f)
        {
            return MazeVectorReferenceKit.StrokeStyle(personality, thickness > 0f ? thickness : normal, alpha, intensity, line, selectedLine, mutedLine);
        }

        public MazeVectorStyle Fill(float alpha = 0.3f, MazeVectorShadingMode? shading = null)
        {
            var top = gradientTop;
            var bottom = gradientBottom;
            top.a *= alpha;
            bottom.a *= alpha;
            return new MazeVectorStyle
            {
                fill = true,
                stroke = false,
                fillColor = bottom,
                selectedFillColor = new Color(top.r, top.g, top.b, Mathf.Clamp01(top.a + 0.1f)),
                shading = shading ?? gradientMode,
                gradientTopColor = top,
                gradientBottomColor = bottom,
                cornerMode = cornerMode
            };
        }

        public MazeVectorIconDefinition StyleIcon(MazeVectorIconDefinition source, MazeVectorState state = MazeVectorState.Idle, float thicknessScale = 1f, float alpha = 0.9f)
        {
            var clone = CloneIcon(source);
            if (clone == null || clone.parts == null)
            {
                return clone;
            }

            var count = Mathf.Max(1, clone.parts.Length);
            for (var i = 0; i < clone.parts.Length; i++)
            {
                var part = clone.parts[i];
                if (part == null)
                {
                    continue;
                }
                var style = Resolve(part.role, state);
                if (part.role == MazeVectorRole.ButtonIcon || part.role == MazeVectorRole.Accent)
                {
                    var t = count == 1 ? 0.5f : i / (float)(count - 1);
                    style.strokeThickness = Mathf.Max(0.5f, normal * thicknessScale + (t - 0.5f) * iconVariation * 2f);
                    style.selectedStrokeThickness = Mathf.Max(style.strokeThickness, thick * thicknessScale);
                    style.strokeColor = new Color(style.strokeColor.r, style.strokeColor.g, style.strokeColor.b, alpha);
                }
                part.useStyleOverride = true;
                part.styleOverride = style;
            }
            return clone;
        }

        public MazeVectorIconDefinition ColorIcon(MazeVectorIconDefinition source, MazeVectorState state = MazeVectorState.Idle, float alpha = 0.9f)
        {
            var clone = CloneIcon(source);
            if (clone == null || clone.parts == null)
            {
                return clone;
            }

            var selected = state == MazeVectorState.Selected || state == MazeVectorState.Pressed;
            var strokeColor = selected ? selectedLine : line;
            strokeColor.a *= alpha;
            var fillColor = background;
            fillColor.a *= alpha;

            for (var i = 0; i < clone.parts.Length; i++)
            {
                var part = clone.parts[i];
                if (part == null)
                {
                    continue;
                }

                var style = part.useStyleOverride && part.styleOverride != null
                    ? part.styleOverride.Clone()
                    : MazeVectorStyle.Stroke(Color.white, 4f);
                if (style.stroke)
                {
                    style.strokeColor = strokeColor;
                    style.selectedStrokeColor = selectedLine;
                }
                if (style.fill)
                {
                    style.fillColor = fillColor;
                    style.selectedFillColor = selectedFill;
                }
                part.useStyleOverride = true;
                part.styleOverride = style;
            }
            return clone;
        }

        public MazeVectorIconDefinition ColorSourceIcon(MazeVectorIconDefinition source, MazeVectorState state = MazeVectorState.Idle, float alpha = 1f, float lightThreshold = 0.45f)
        {
            var clone = CloneIcon(source);
            if (clone == null || clone.parts == null)
            {
                return clone;
            }

            var selected = state == MazeVectorState.Selected || state == MazeVectorState.Pressed;
            var lightColor = selected ? selectedLine : line;
            lightColor.a *= alpha;
            var darkColor = background;
            darkColor.a *= alpha;

            for (var i = 0; i < clone.parts.Length; i++)
            {
                var part = clone.parts[i];
                if (part == null)
                {
                    continue;
                }

                var style = part.useStyleOverride && part.styleOverride != null
                    ? part.styleOverride.Clone()
                    : MazeVectorStyle.Filled(Color.white);
                var sourceColor = style.fill ? style.fillColor : style.strokeColor;
                var sourceAlpha = sourceColor.a;
                var target = Luminance(sourceColor) >= lightThreshold ? lightColor : darkColor;
                target.a *= sourceAlpha;
                if (style.fill)
                {
                    style.fillColor = target;
                    style.selectedFillColor = target;
                }
                if (style.stroke)
                {
                    style.strokeColor = target;
                    style.selectedStrokeColor = target;
                }
                part.useStyleOverride = true;
                part.styleOverride = style;
            }
            return clone;
        }

        public Color ResolveImportedColor(Color sourceColor, MazeVectorPaintRole role, MazeVectorState state = MazeVectorState.Idle, float alpha = 1f)
        {
            var selected = state == MazeVectorState.Selected || state == MazeVectorState.Pressed;
            var disabled = state == MazeVectorState.Disabled;
            var result = role switch
            {
                MazeVectorPaintRole.SourceDark or MazeVectorPaintRole.Fill => background,
                MazeVectorPaintRole.SourceLight or MazeVectorPaintRole.Line => selected ? selectedLine : line,
                MazeVectorPaintRole.Accent => selected ? selectedAccent : accent,
                MazeVectorPaintRole.Disabled => mutedLine,
                _ => sourceColor
            };
            result.a *= sourceColor.a * Mathf.Clamp01(alpha);
            if (disabled)
            {
                result.a *= 0.36f;
            }
            return result;
        }

        public MazeVectorIconPart FillPart(string name, MazeVectorShape shape, float alpha, Vector2 position, Vector2 size)
        {
            var fill = new Color(background.r, background.g, background.b, Mathf.Clamp01(alpha));
            return MazeVectorReferenceKit.StyledPart(name, shape, size, position, new MazeVectorStyle
            {
                fill = true,
                stroke = false,
                fillColor = fill,
                selectedFillColor = fill,
                shading = MazeVectorShadingMode.Flat,
                cornerMode = cornerMode
            });
        }

        public MazeVectorStyle AccentStyle(float thickness, float intensity = 1f, float alpha = 0.95f)
        {
            var stroke = Color.Lerp(accent, selectedAccent, Mathf.Clamp01(intensity - 1f));
            stroke.a = alpha;
            return new MazeVectorStyle
            {
                fill = false,
                stroke = true,
                strokeColor = stroke,
                strokeThickness = thickness,
                selectedStrokeColor = selectedAccent,
                selectedStrokeThickness = Mathf.Max(thickness, thick),
                strokeCap = strokeCap,
                strokeJoin = strokeJoin,
                cornerMode = cornerMode
            };
        }

        private static MazeVectorIconDefinition CloneIcon(MazeVectorIconDefinition source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new MazeVectorIconDefinition
            {
                id = source.id,
                size = source.size,
                parts = new MazeVectorIconPart[source.parts != null ? source.parts.Length : 0]
            };
            for (var i = 0; i < clone.parts.Length; i++)
            {
                var part = source.parts[i];
                if (part == null)
                {
                    continue;
                }
                clone.parts[i] = new MazeVectorIconPart
                {
                    name = part.name,
                    shape = part.shape,
                    size = part.size,
                    position = part.position,
                    role = part.role,
                    useStyleOverride = part.useStyleOverride,
                    styleOverride = part.styleOverride != null ? part.styleOverride.Clone() : null
                };
            }
            return clone;
        }

        private static float Luminance(Color color)
        {
            return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        }
    }
}
