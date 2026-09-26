using System;
using UnityEngine;

namespace Maze
{
    public enum MazeVectorFrameVariant
    {
        ThinBolts,
        CornerOnly,
        DiamondCorners,
        SelectionCorners,
        CrosshairFrame
    }

    public enum MazeVectorDividerVariant
    {
        SolidDiamond,
        DoubleLine,
        Dashed,
        VerticalTicks,
        CenterGap,
        EndHooks,
        EndCaps
    }

    public enum MazeVectorSelectorVariant
    {
        CornerBrackets,
        HookedCorners,
        GappedSides,
        SideTicks
    }

    public enum MazeVectorGridVariant
    {
        Plain,
        Selected,
        DarkLines,
        InventoryCell
    }

    public enum MazeVectorBadgeVariant
    {
        Square,
        Hex,
        Circle,
        Diamond
    }

    public enum MazeVectorDetailFamily
    {
        Square,
        Round,
        Sharp
    }

    public enum MazeVectorDetailMass
    {
        Thin,
        Balanced,
        Bulky
    }

    public enum MazeVectorDetailSpacing
    {
        Tight,
        Balanced,
        Spacious
    }

    public static class MazeVectorReferenceKit
    {
        public static readonly Color Purple = new(0.62f, 0.28f, 1f, 1f);
        public static readonly Color BrightPurple = new(0.92f, 0.46f, 1.35f, 1f);
        public static readonly Color DimPurple = new(0.34f, 0.16f, 0.56f, 0.86f);
        public static readonly Color PanelFill = new(0.025f, 0.012f, 0.042f, 0.72f);

        public static MazeVectorStyle Outline(float thickness = 2f, float alpha = 1f, float intensity = 1f)
        {
            var stroke = Scale(Purple, intensity);
            stroke.a *= alpha;
            return new MazeVectorStyle
            {
                fill = false,
                stroke = true,
                strokeColor = stroke,
                strokeThickness = thickness,
                selectedStrokeColor = Scale(BrightPurple, intensity),
                selectedStrokeThickness = thickness + 2f,
                strokeCap = MazeVectorStrokeCap.Square,
                strokeJoin = MazeVectorStrokeJoin.Bevel,
                cornerMode = MazeVectorCornerMode.Sharp
            };
        }

        public static MazeVectorStyle Panel(float thickness = 2f, float fillAlpha = 0.35f, float intensity = 1f, float gradientIntensity = 0f, float rimIntensity = 0f)
        {
            var fill = Scale(PanelFill, intensity);
            fill.a = fillAlpha;
            return new MazeVectorStyle
            {
                fill = fillAlpha > 0f,
                stroke = true,
                fillColor = fill,
                strokeColor = Scale(Purple, intensity),
                strokeThickness = thickness,
                selectedFillColor = new Color(0.18f, 0.035f, 0.32f, 0.62f),
                selectedStrokeColor = Scale(BrightPurple, intensity),
                selectedStrokeThickness = thickness + 2f,
                strokeCap = MazeVectorStrokeCap.Square,
                strokeJoin = MazeVectorStrokeJoin.Bevel,
                cornerMode = MazeVectorCornerMode.Sharp,
                innerStrokeColor = new Color(1f, 0.75f, 1.3f, 0.14f),
                innerStrokeThickness = 1f,
                shading = gradientIntensity > 0f ? MazeVectorShadingMode.VerticalGradient : MazeVectorShadingMode.Flat,
                gradientTopColor = new Color(0.22f, 0.05f, 0.42f, fillAlpha * gradientIntensity),
                gradientBottomColor = new Color(0.018f, 0.006f, 0.035f, fillAlpha),
                rimColor = new Color(1f, 0.62f, 1.2f, 0.22f * rimIntensity),
                rimThickness = rimIntensity > 0f ? thickness : 0f
            };
        }

        public static MazeVectorStyle DimOutline(float thickness = 2f)
        {
            return new MazeVectorStyle
            {
                fill = false,
                stroke = true,
                strokeColor = DimPurple,
                strokeThickness = thickness,
                selectedStrokeColor = BrightPurple,
                selectedStrokeThickness = thickness + 1.5f,
                strokeCap = MazeVectorStrokeCap.Square,
                strokeJoin = MazeVectorStrokeJoin.Bevel,
                cornerMode = MazeVectorCornerMode.Sharp
            };
        }

        public static MazeVectorStyle Button(MazeVectorState state, float thickness = 2f, float fillAlpha = -1f, float intensity = 1f, float gradientIntensity = 0.65f)
        {
            var selected = state == MazeVectorState.Selected || state == MazeVectorState.Pressed;
            var fill = selected ? new Color(0.2f, 0.04f, 0.34f, fillAlpha >= 0f ? fillAlpha : 0.62f) : new Color(0.02f, 0.008f, 0.035f, fillAlpha >= 0f ? fillAlpha : 0.25f);
            var stroke = Scale(selected ? BrightPurple : Purple, intensity);
            return new MazeVectorStyle
            {
                fill = true,
                stroke = true,
                fillColor = fill,
                strokeColor = stroke,
                strokeThickness = selected ? thickness + 1.5f : thickness,
                selectedFillColor = new Color(0.36f, 0.08f, 0.62f, 0.72f),
                selectedStrokeColor = Scale(BrightPurple, intensity),
                selectedStrokeThickness = thickness + 3f,
                strokeCap = MazeVectorStrokeCap.Square,
                strokeJoin = MazeVectorStrokeJoin.Bevel,
                cornerMode = MazeVectorCornerMode.Sharp,
                rimColor = selected ? new Color(1f, 0.7f, 1.2f, 0.22f) : new Color(0f, 0f, 0f, 0f),
                rimThickness = selected ? thickness : 0f,
                shading = gradientIntensity > 0f ? MazeVectorShadingMode.VerticalGradient : MazeVectorShadingMode.Flat,
                gradientTopColor = selected ? new Color(0.38f, 0.085f, 0.68f, 0.42f * gradientIntensity) : new Color(0.1f, 0.022f, 0.18f, 0.22f * gradientIntensity),
                gradientBottomColor = selected ? new Color(0.08f, 0.012f, 0.16f, 0.7f) : new Color(0.012f, 0.004f, 0.025f, 0.32f)
            };
        }

        public static MazeVectorIconDefinition Frame(Vector2 size, MazeVectorFrameVariant variant = MazeVectorFrameVariant.ThinBolts, float thickness = 2f, float cornerLength = 18f, float boltSize = 8f)
        {
            var parts = new System.Collections.Generic.List<MazeVectorIconPart>(12)
            {
                StyledPart("Frame", MazeVectorShape.Rect(size), size, Vector2.zero, Outline(thickness))
            };
            if (variant == MazeVectorFrameVariant.ThinBolts || variant == MazeVectorFrameVariant.DiamondCorners)
            {
                var x = size.x * 0.5f - boltSize * 1.35f;
                var y = size.y * 0.5f - boltSize * 1.35f;
                var boltShape = variant == MazeVectorFrameVariant.DiamondCorners ? DiamondShape(boltSize * 1.6f) : MazeVectorShape.Rect(Vector2.one * boltSize);
                parts.Add(StyledPart("Bolt TL", boltShape, Vector2.one * boltSize * 1.6f, new Vector2(-x, y), Outline(thickness)));
                parts.Add(StyledPart("Bolt TR", boltShape, Vector2.one * boltSize * 1.6f, new Vector2(x, y), Outline(thickness)));
                parts.Add(StyledPart("Bolt BL", boltShape, Vector2.one * boltSize * 1.6f, new Vector2(-x, -y), Outline(thickness)));
                parts.Add(StyledPart("Bolt BR", boltShape, Vector2.one * boltSize * 1.6f, new Vector2(x, -y), Outline(thickness)));
            }
            if (variant == MazeVectorFrameVariant.CornerOnly || variant == MazeVectorFrameVariant.SelectionCorners || variant == MazeVectorFrameVariant.CrosshairFrame)
            {
                parts.Clear();
                parts.Add(StyledPart("Corners", new MazeVectorShape { kind = MazeVectorShapeKind.CornerAccents, size = size, cornerLength = cornerLength }, size, Vector2.zero, Outline(thickness)));
            }
            if (variant == MazeVectorFrameVariant.CrosshairFrame)
            {
                parts.Add(StyledPart("Center Cross H", MazeVectorShape.Line(new Vector2(-0.08f, 0f), new Vector2(0.08f, 0f)), size, Vector2.zero, Outline(thickness)));
                parts.Add(StyledPart("Center Cross V", MazeVectorShape.Line(new Vector2(0f, -0.08f), new Vector2(0f, 0.08f)), size, Vector2.zero, Outline(thickness)));
            }
            return new MazeVectorIconDefinition { id = "frame_" + variant, size = size, parts = parts.ToArray() };
        }

        public static MazeVectorIconDefinition DetailedFrame(
            Vector2 size,
            MazeVectorDetailFamily family = MazeVectorDetailFamily.Square,
            MazeVectorDetailMass mass = MazeVectorDetailMass.Balanced,
            MazeVectorDetailSpacing spacing = MazeVectorDetailSpacing.Balanced,
            bool includeFill = true)
        {
            var thickness = DetailThickness(mass);
            var inset = DetailInset(spacing);
            var node = DetailNodeSize(mass);
            var cornerLength = DetailCornerLength(spacing);
            var outerStyle = DetailStroke(family, thickness, 0.58f, 0.86f);
            var innerStyle = DetailStroke(family, Mathf.Max(0.75f, thickness * 0.62f), 0.26f, 0.58f);
            var accentStyle = DetailStroke(family, Mathf.Max(0.9f, thickness * 0.78f), 0.88f, 1.14f);
            var parts = new System.Collections.Generic.List<MazeVectorIconPart>(12);

            if (includeFill)
            {
                parts.Add(StyledPart(
                    "Dark Fill",
                    DetailRect(size - Vector2.one * 2f, family),
                    size - Vector2.one * 2f,
                    Vector2.zero,
                    new MazeVectorStyle
                    {
                        fill = true,
                        stroke = false,
                        fillColor = new Color(0.003f, 0.003f, 0.01f, 0.95f),
                        shading = MazeVectorShadingMode.AmorphousGradient,
                        gradientTopColor = new Color(0.012f, 0.011f, 0.029f, 0.90f),
                        gradientBottomColor = new Color(0.0015f, 0.0015f, 0.006f, 0.98f),
                        amorphousScale = 0.78f,
                        amorphousSharpness = 0.54f,
                        amorphousSeed = 13.7f,
                        amorphousOffset = new Vector2(0.21f, -0.14f),
                        amorphousDetail = 5
                    }));
            }

            ApplyStrokePalette(
                outerStyle,
                12f,
                new MazeVectorStrokeGradientStop(0f, new Color(0.38f, 0.32f, 0.72f, 0.34f)),
                new MazeVectorStrokeGradientStop(0.18f, new Color(0.25f, 0.22f, 0.56f, 0.23f)),
                new MazeVectorStrokeGradientStop(0.43f, new Color(0.50f, 0.45f, 0.94f, 0.44f)),
                new MazeVectorStrokeGradientStop(0.485f, new Color(0.82f, 0.80f, 1.22f, 0.72f)),
                new MazeVectorStrokeGradientStop(0.535f, new Color(0.43f, 0.39f, 0.84f, 0.38f)),
                new MazeVectorStrokeGradientStop(0.76f, new Color(0.22f, 0.20f, 0.50f, 0.20f)),
                new MazeVectorStrokeGradientStop(0.92f, new Color(0.49f, 0.44f, 0.92f, 0.40f)),
                new MazeVectorStrokeGradientStop(1f, new Color(0.38f, 0.32f, 0.72f, 0.34f)));
            ApplyStrokePalette(
                innerStyle,
                16f,
                new MazeVectorStrokeGradientStop(0f, new Color(0.22f, 0.20f, 0.50f, 0.16f)),
                new MazeVectorStrokeGradientStop(0.31f, new Color(0.39f, 0.35f, 0.76f, 0.27f)),
                new MazeVectorStrokeGradientStop(0.62f, new Color(0.18f, 0.17f, 0.43f, 0.13f)),
                new MazeVectorStrokeGradientStop(0.86f, new Color(0.46f, 0.41f, 0.86f, 0.29f)),
                new MazeVectorStrokeGradientStop(1f, new Color(0.22f, 0.20f, 0.50f, 0.16f)));
            var haloStyle = outerStyle.Clone();
            haloStyle.strokeColor = new Color(0.22f, 0.28f, 0.72f, 0.02f);
            haloStyle.strokeThickness = Mathf.Max(3.2f, thickness * 3.2f);
            haloStyle.strokeGradientStops = DimStrokePalette(outerStyle.strokeGradientStops, 0.08f);
            parts.Add(StyledPart("Outer Flow Halo", DetailRect(size, family), size, Vector2.zero, haloStyle));
            parts.Add(StyledPart("Outer Keyline", DetailRect(size, family), size, Vector2.zero, outerStyle));
            var innerSize = Vector2.Max(Vector2.one * 4f, size - Vector2.one * inset * 2f);
            parts.Add(StyledPart("Inset Keyline", DetailRect(innerSize, family), innerSize, Vector2.zero, innerStyle));

            var halfX = size.x * 0.5f;
            var halfY = size.y * 0.5f;
            var junctionLength = Mathf.Min(cornerLength, Mathf.Min(size.x, size.y) * 0.28f);
            if (includeFill)
            {
                parts.Add(StyledPart(
                    "Top Rail Gap",
                    MazeVectorShape.Rect(new Vector2(Mathf.Max(5f, node * 1.45f), Mathf.Max(3f, thickness * 2.8f))),
                    new Vector2(Mathf.Max(5f, node * 1.45f), Mathf.Max(3f, thickness * 2.8f)),
                    new Vector2(halfX - junctionLength * 1.95f, halfY),
                    new MazeVectorStyle
                    {
                        fill = true,
                        stroke = false,
                        fillColor = new Color(0.004f, 0.003f, 0.012f, 1f)
                    }));
            }
            AddRail(parts, "Top Junction Approach", halfX - junctionLength * 2.2f, halfY, halfX - junctionLength * 0.58f, halfY, size, innerStyle);
            AddRail(parts, "Top Junction Peak", halfX - junctionLength * 0.72f, halfY, halfX - node * 0.9f, halfY, size, accentStyle);
            AddRail(parts, "Right Junction", halfX, halfY - node * 0.9f, halfX, halfY - junctionLength * 0.72f, size, accentStyle);
            parts.Add(StyledPart(
                "Top Right Junction Node",
                DetailNodeShape(node, family),
                Vector2.one * node,
                new Vector2(halfX, halfY),
                DetailStroke(family, Mathf.Max(0.8f, thickness * 0.7f), 0.94f, 1.25f)));

            AddRail(parts, "Bottom Left Accent H", -halfX, -halfY, -halfX + junctionLength, -halfY, size, innerStyle);
            AddRail(parts, "Bottom Left Accent V", -halfX, -halfY, -halfX, -halfY + junctionLength, size, innerStyle);

            return new MazeVectorIconDefinition
            {
                id = "detailed_frame_" + family + "_" + mass + "_" + spacing,
                size = size,
                parts = parts.ToArray()
            };
        }

        public static MazeVectorIconDefinition DetailedCell(
            Vector2 size,
            bool selected = false,
            MazeVectorDetailFamily family = MazeVectorDetailFamily.Square,
            MazeVectorDetailMass mass = MazeVectorDetailMass.Thin,
            MazeVectorDetailSpacing spacing = MazeVectorDetailSpacing.Tight)
        {
            var thickness = DetailThickness(mass);
            var inset = DetailInset(spacing);
            var node = DetailNodeSize(mass);
            var outer = DetailStroke(family, thickness, selected ? 0.96f : 0.72f, selected ? 1.2f : 0.98f);
            var inner = DetailStroke(family, Mathf.Max(0.7f, thickness * 0.55f), selected ? 0.46f : 0.25f, selected ? 0.86f : 0.54f);
            if (selected)
            {
                outer.softGlowColor = new Color(0.56f, 0.28f, 1f, 0.12f);
                outer.softGlowSpread = Mathf.Max(2f, thickness * 2f);
                outer.glow = 0.08f;
            }

            var innerSize = Vector2.Max(Vector2.one * 4f, size - Vector2.one * inset * 2f);
            var halfX = size.x * 0.5f - inset * 0.65f;
            var halfY = size.y * 0.5f - inset * 0.65f;
            var parts = new System.Collections.Generic.List<MazeVectorIconPart>(6)
            {
                StyledPart("Cell Fill", DetailRect(size - Vector2.one * 2f, family), size - Vector2.one * 2f, Vector2.zero, new MazeVectorStyle
                {
                    fill = true,
                    stroke = false,
                    fillColor = selected ? new Color(0.018f, 0.01f, 0.045f, 0.96f) : new Color(0.002f, 0.002f, 0.009f, 0.95f),
                    shading = MazeVectorShadingMode.AmorphousGradient,
                    gradientTopColor = selected ? new Color(0.036f, 0.023f, 0.076f, 0.82f) : new Color(0.009f, 0.008f, 0.022f, 0.76f),
                    gradientBottomColor = new Color(0.001f, 0.001f, 0.005f, 0.985f),
                    amorphousScale = selected ? 0.96f : 0.72f,
                    amorphousSharpness = 0.48f,
                    amorphousSeed = selected ? 8.2f : 3.6f,
                    amorphousOffset = selected ? new Vector2(-0.18f, 0.24f) : new Vector2(0.12f, -0.19f),
                    amorphousDetail = 4
                }),
                StyledPart("Cell Outer Keyline", DetailRect(size, family), size, Vector2.zero, outer),
                StyledPart("Cell Inset Keyline", DetailRect(innerSize, family), innerSize, Vector2.zero, inner),
                StyledPart("Cell Top Left Node", DetailNodeShape(node, family), Vector2.one * node, new Vector2(-halfX, halfY), outer),
                StyledPart("Cell Bottom Right Node", DetailNodeShape(node, family), Vector2.one * node, new Vector2(halfX, -halfY), outer)
            };
            return new MazeVectorIconDefinition
            {
                id = "detailed_cell_" + family + "_" + mass + "_" + spacing + (selected ? "_selected" : string.Empty),
                size = size,
                parts = parts.ToArray()
            };
        }

        public static MazeVectorIconDefinition JunctionRail(
            Vector2 size,
            bool vertical = false,
            bool brightAtPositiveEnd = true,
            MazeVectorDetailFamily family = MazeVectorDetailFamily.Square,
            MazeVectorDetailMass mass = MazeVectorDetailMass.Thin,
            MazeVectorDetailSpacing spacing = MazeVectorDetailSpacing.Balanced)
        {
            var thickness = DetailThickness(mass);
            var node = DetailNodeSize(mass);
            var railOffset = DetailInset(spacing) * 0.35f;
            var direction = brightAtPositiveEnd ? 1f : -1f;
            var dim = DetailStroke(family, thickness, 0.34f, 0.58f);
            var medium = DetailStroke(family, Mathf.Max(0.75f, thickness * 0.92f), 0.58f, 0.86f);
            var bright = DetailStroke(family, Mathf.Max(0.8f, thickness * 0.78f), 0.94f, 1.28f);
            var parts = new System.Collections.Generic.List<MazeVectorIconPart>(7);

            AddRampRail(parts, "Primary", size, vertical, direction, 0f, dim, medium, bright);
            if (railOffset > 0.1f)
            {
                var secondary = DetailStroke(family, Mathf.Max(0.65f, thickness * 0.52f), 0.18f, 0.5f);
                AddSingleRail(parts, "Inset Rail", size, vertical, -0.86f, 0.58f * direction, vertical ? railOffset : -railOffset, secondary);
            }

            var nodePosition = vertical
                ? new Vector2(0f, direction * (size.y * 0.5f - node * 0.65f))
                : new Vector2(direction * (size.x * 0.5f - node * 0.65f), 0f);
            parts.Add(StyledPart("Junction Node", DetailNodeShape(node, family), Vector2.one * node, nodePosition, bright));
            return new MazeVectorIconDefinition
            {
                id = "junction_rail_" + family + "_" + mass + "_" + spacing,
                size = size,
                parts = parts.ToArray()
            };
        }

        public static MazeVectorIconDefinition Divider(float length, MazeVectorDividerVariant variant, float thickness = 2f)
        {
            var size = new Vector2(length, 36f);
            var parts = new System.Collections.Generic.List<MazeVectorIconPart>(8);
            if (variant == MazeVectorDividerVariant.DoubleLine)
            {
                parts.Add(StyledPart("Top", MazeVectorShape.Line(new Vector2(-0.9f, 0.24f), new Vector2(0.9f, 0.24f)), size, Vector2.zero, DimOutline(thickness)));
                parts.Add(StyledPart("Bottom", MazeVectorShape.Line(new Vector2(-0.9f, -0.24f), new Vector2(0.9f, -0.24f)), size, Vector2.zero, DimOutline(thickness)));
            }
            else if (variant == MazeVectorDividerVariant.Dashed)
            {
                parts.Add(StyledPart("Dash", new MazeVectorShape { kind = MazeVectorShapeKind.DashedOutline, size = new Vector2(length, 4f), dashLength = 20f, dashGap = 14f }, size, Vector2.zero, Outline(thickness)));
            }
            else if (variant == MazeVectorDividerVariant.VerticalTicks)
            {
                for (var i = 0; i < 5; i++)
                {
                    parts.Add(StyledPart("Tick " + i, MazeVectorShape.Line(new Vector2(0f, -0.62f), new Vector2(0f, 0.62f)), new Vector2(24f, 36f), new Vector2((i - 2) * length / 5f, 0f), Outline(thickness)));
                }
            }
            else if (variant == MazeVectorDividerVariant.CenterGap)
            {
                parts.Add(StyledPart("Left", MazeVectorShape.Line(new Vector2(-0.9f, 0f), new Vector2(-0.18f, 0f)), size, Vector2.zero, DimOutline(thickness)));
                parts.Add(StyledPart("Right", MazeVectorShape.Line(new Vector2(0.18f, 0f), new Vector2(0.9f, 0f)), size, Vector2.zero, DimOutline(thickness)));
                parts.Add(StyledPart("Diamond", DiamondShape(12f), new Vector2(24f, 24f), Vector2.zero, Outline(thickness + 0.8f)));
            }
            else if (variant == MazeVectorDividerVariant.EndHooks)
            {
                parts.Add(StyledPart("Line", MazeVectorShape.Line(new Vector2(-0.82f, 0f), new Vector2(0.82f, 0f)), size, Vector2.zero, DimOutline(thickness)));
                parts.Add(StyledPart("Left Hook", MazeVectorShape.Line(new Vector2(-0.9f, -0.28f), new Vector2(-0.9f, 0f), new Vector2(-0.74f, 0f)), size, Vector2.zero, Outline(thickness + 0.8f)));
                parts.Add(StyledPart("Right Hook", MazeVectorShape.Line(new Vector2(0.9f, -0.28f), new Vector2(0.9f, 0f), new Vector2(0.74f, 0f)), size, Vector2.zero, Outline(thickness + 0.8f)));
            }
            else if (variant == MazeVectorDividerVariant.EndCaps)
            {
                parts.Add(StyledPart("Line", MazeVectorShape.Line(new Vector2(-0.82f, 0f), new Vector2(0.82f, 0f)), size, Vector2.zero, DimOutline(thickness)));
                parts.Add(StyledPart("Left Cap", MazeVectorShape.Line(new Vector2(-0.88f, -0.36f), new Vector2(-0.88f, 0.36f)), size, Vector2.zero, Outline(thickness + 1.2f)));
                parts.Add(StyledPart("Right Cap", MazeVectorShape.Line(new Vector2(0.88f, -0.36f), new Vector2(0.88f, 0.36f)), size, Vector2.zero, Outline(thickness + 1.2f)));
            }
            else
            {
                parts.Add(StyledPart("Line", MazeVectorShape.Line(new Vector2(-0.9f, 0f), new Vector2(0.9f, 0f)), size, Vector2.zero, DimOutline(thickness)));
                parts.Add(StyledPart("Diamond", DiamondShape(12f), new Vector2(24f, 24f), Vector2.zero, Outline(thickness)));
            }
            return new MazeVectorIconDefinition { id = "divider_" + variant, size = size, parts = parts.ToArray() };
        }

        public static MazeVectorIconDefinition Selector(Vector2 size, MazeVectorSelectorVariant variant = MazeVectorSelectorVariant.GappedSides, float thickness = 2.5f, float accentThickness = 5f, float cornerLength = 30f, float sideGap = 34f, float hookLength = 16f)
        {
            var halfX = size.x * 0.5f;
            var halfY = size.y * 0.5f;
            var gapX = Mathf.Min(Mathf.Max(0f, sideGap), Mathf.Max(0f, size.x - cornerLength * 2f) * 0.5f);
            var gapY = Mathf.Min(Mathf.Max(0f, sideGap), Mathf.Max(0f, size.y - cornerLength * 2f) * 0.5f);
            var l = Mathf.Min(cornerLength, Mathf.Min(size.x, size.y) * 0.44f);
            var hook = Mathf.Min(hookLength, l);
            var parts = new System.Collections.Generic.List<MazeVectorIconPart>(24);
            var dim = DimOutline(thickness);
            dim.selectedStrokeColor = dim.strokeColor;
            dim.selectedStrokeThickness = thickness;
            var bright = Outline(accentThickness, 0.94f, 1.08f);
            bright.selectedStrokeThickness = accentThickness + 0.8f;

            if (variant == MazeVectorSelectorVariant.CornerBrackets)
            {
                parts.Add(StyledPart("Corners", new MazeVectorShape { kind = MazeVectorShapeKind.CornerAccents, size = size, cornerLength = l }, size, Vector2.zero, bright));
                return new MazeVectorIconDefinition { id = "selector_" + variant, size = size, parts = parts.ToArray() };
            }

            AddSelectorCorner(parts, "TL", -halfX, halfY, 1f, -1f, l, hook, bright, variant == MazeVectorSelectorVariant.HookedCorners);
            AddSelectorCorner(parts, "TR", halfX, halfY, -1f, -1f, l, hook, bright, variant == MazeVectorSelectorVariant.HookedCorners);
            AddSelectorCorner(parts, "BL", -halfX, -halfY, 1f, 1f, l, hook, bright, variant == MazeVectorSelectorVariant.HookedCorners);
            AddSelectorCorner(parts, "BR", halfX, -halfY, -1f, 1f, l, hook, bright, variant == MazeVectorSelectorVariant.HookedCorners);

            if (variant == MazeVectorSelectorVariant.GappedSides || variant == MazeVectorSelectorVariant.SideTicks)
            {
                AddRail(parts, "Top Left Rail", -halfX + l, halfY, -gapX, halfY, size, dim);
                AddRail(parts, "Top Right Rail", gapX, halfY, halfX - l, halfY, size, dim);
                AddRail(parts, "Bottom Left Rail", -halfX + l, -halfY, -gapX, -halfY, size, dim);
                AddRail(parts, "Bottom Right Rail", gapX, -halfY, halfX - l, -halfY, size, dim);
                AddRail(parts, "Left Top Rail", -halfX, halfY - l, -halfX, gapY, size, dim);
                AddRail(parts, "Left Bottom Rail", -halfX, -gapY, -halfX, -halfY + l, size, dim);
                AddRail(parts, "Right Top Rail", halfX, halfY - l, halfX, gapY, size, dim);
                AddRail(parts, "Right Bottom Rail", halfX, -gapY, halfX, -halfY + l, size, dim);
            }

            if (variant == MazeVectorSelectorVariant.SideTicks)
            {
                parts.Add(StyledPart("Top Tick", NormalizedLine(-0.12f * size.x, halfY, 0.12f * size.x, halfY, size), size, Vector2.zero, bright));
                parts.Add(StyledPart("Bottom Tick", NormalizedLine(-0.12f * size.x, -halfY, 0.12f * size.x, -halfY, size), size, Vector2.zero, bright));
                parts.Add(StyledPart("Left Tick", NormalizedLine(-halfX, -0.12f * size.y, -halfX, 0.12f * size.y, size), size, Vector2.zero, bright));
                parts.Add(StyledPart("Right Tick", NormalizedLine(halfX, -0.12f * size.y, halfX, 0.12f * size.y, size), size, Vector2.zero, bright));
            }

            return new MazeVectorIconDefinition { id = "selector_" + variant, size = size, parts = parts.ToArray() };
        }

        public static MazeVectorIconDefinition Grid(Vector2 cellSize, int columns, int rows, float gap, MazeVectorGridVariant variant = MazeVectorGridVariant.Plain, float thickness = 2f)
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);
            var size = new Vector2(columns * cellSize.x + (columns - 1) * gap, rows * cellSize.y + (rows - 1) * gap);
            var style = variant == MazeVectorGridVariant.Selected ? Button(MazeVectorState.Selected) : Outline(thickness, variant == MazeVectorGridVariant.DarkLines ? 0.45f : 1f);
            var parts = new System.Collections.Generic.List<MazeVectorIconPart>(columns * rows + 2);
            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < columns; x++)
                {
                    var pos = new Vector2((x - (columns - 1) * 0.5f) * (cellSize.x + gap), ((rows - 1) * 0.5f - y) * (cellSize.y + gap));
                    parts.Add(StyledPart("Cell " + x + " " + y, variant == MazeVectorGridVariant.InventoryCell ? MazeVectorShape.RoundedRect(cellSize, 3f) : MazeVectorShape.Rect(cellSize), cellSize, pos, style));
                }
            }
            return new MazeVectorIconDefinition { id = "grid_" + columns + "x" + rows + "_" + variant, size = size, parts = parts.ToArray() };
        }

        public static MazeVectorIconDefinition ResourceBar(Vector2 size, float fill01, int ticks = 6, bool plusBox = true)
        {
            fill01 = Mathf.Clamp01(fill01);
            var parts = new System.Collections.Generic.List<MazeVectorIconPart>(ticks + 4)
            {
                StyledPart("Bar Frame", MazeVectorShape.Rect(size), size, Vector2.zero, Outline(2f)),
                StyledPart("Bar Fill", MazeVectorShape.Rect(new Vector2(size.x * fill01, size.y - 8f)), new Vector2(size.x * fill01, size.y - 8f), new Vector2((fill01 - 1f) * size.x * 0.5f, 0f), Fill(new Color(0.54f, 0.16f, 0.88f, 0.72f)))
            };
            for (var i = 1; i < ticks; i++)
            {
                var x = Mathf.Lerp(-size.x * 0.5f, size.x * 0.5f, i / (float)ticks);
                parts.Add(StyledPart("Tick " + i, MazeVectorShape.Line(new Vector2(0f, -0.5f), new Vector2(0f, 0.5f)), new Vector2(12f, size.y), new Vector2(x, 0f), DimOutline(1.2f)));
            }
            if (plusBox)
            {
                parts.Add(StyledPart("Plus Box", MazeVectorShape.Rect(Vector2.one * size.y), Vector2.one * size.y, new Vector2(-size.x * 0.5f - size.y * 0.75f, 0f), Outline(2f)));
                parts.Add(StyledPart("Plus", MazeVectorShape.Line(new Vector2(-0.32f, 0f), new Vector2(0.32f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0.32f), new Vector2(0f, -0.32f)), Vector2.one * size.y, new Vector2(-size.x * 0.5f - size.y * 0.75f, 0f), Outline(2f)));
            }
            return new MazeVectorIconDefinition { id = "resource_bar", size = size, parts = parts.ToArray() };
        }

        public static MazeVectorIconDefinition QuickInfoStrip(Vector2 size, float thickness = 1.5f)
        {
            var strip = Frame(size, MazeVectorFrameVariant.ThinBolts, thickness, 12f, 5f);
            var parts = new System.Collections.Generic.List<MazeVectorIconPart>(strip.parts)
            {
                StyledPart("Heart", MazeVectorIconLibrary.Heart().parts[0].shape, new Vector2(38f, 38f), new Vector2(-size.x * 0.37f, 0f), Outline(2.5f)),
                StyledPart("Divider A", MazeVectorShape.Line(new Vector2(0f, -0.55f), new Vector2(0f, 0.55f)), new Vector2(20f, size.y * 0.65f), new Vector2(-size.x * 0.15f, 0f), DimOutline(1.2f)),
                StyledPart("Diamond", DiamondShape(18f), new Vector2(30f, 30f), new Vector2(size.x * 0.11f, 0f), Outline(2f)),
                StyledPart("Divider B", MazeVectorShape.Line(new Vector2(0f, -0.55f), new Vector2(0f, 0.55f)), new Vector2(20f, size.y * 0.65f), new Vector2(size.x * 0.3f, 0f), DimOutline(1.2f)),
                StyledPart("Clock", MazeVectorShape.Circle(12f, 24), new Vector2(36f, 36f), new Vector2(size.x * 0.44f, 0f), Outline(2f))
            };
            strip.id = "quick_info_strip";
            strip.parts = parts.ToArray();
            return strip;
        }

        public static MazeVectorIconDefinition StatusStrip(Vector2 size, float barA01 = 0.78f, float barB01 = 0.58f, float thickness = 2f)
        {
            barA01 = Mathf.Clamp01(barA01);
            barB01 = Mathf.Clamp01(barB01);
            var strip = Frame(size, MazeVectorFrameVariant.CornerOnly, thickness, 18f);
            var parts = new System.Collections.Generic.List<MazeVectorIconPart>(strip.parts)
            {
                StyledPart("Bar A", MazeVectorShape.Rect(new Vector2(size.x * 0.58f * barA01, 12f)), new Vector2(size.x * 0.58f * barA01, 12f), new Vector2(size.x * 0.09f, size.y * 0.21f), Fill(new Color(0.48f, 0.15f, 0.8f, 0.72f))),
                StyledPart("Bar B", MazeVectorShape.Rect(new Vector2(size.x * 0.58f * barB01, 12f)), new Vector2(size.x * 0.58f * barB01, 12f), new Vector2(size.x * 0.015f, -size.y * 0.17f), Fill(new Color(0.58f, 0.22f, 0.9f, 0.72f))),
                StyledPart("Diamond", DiamondShape(24f), new Vector2(32f, 32f), new Vector2(-size.x * 0.39f, 0f), Outline(2.5f))
            };
            strip.id = "status_strip";
            strip.parts = parts.ToArray();
            return strip;
        }

        public static MazeVectorIconDefinition Badge(string textId, MazeVectorBadgeVariant variant, Vector2 size)
        {
            var shape = variant switch
            {
                MazeVectorBadgeVariant.Hex => new MazeVectorShape { kind = MazeVectorShapeKind.Polygon, sides = 6, size = size },
                MazeVectorBadgeVariant.Circle => MazeVectorShape.Circle(Mathf.Min(size.x, size.y) * 0.5f, 32),
                MazeVectorBadgeVariant.Diamond => DiamondShape(Mathf.Min(size.x, size.y)),
                _ => MazeVectorShape.Rect(size)
            };
            return new MazeVectorIconDefinition { id = "badge_" + textId + "_" + variant, size = size, parts = new[] { StyledPart("Badge", shape, size, Vector2.zero, Outline(2f)) } };
        }

        public static MazeVectorIconDefinition PointerTriangle(Vector2 size)
        {
            return new MazeVectorIconDefinition { id = "pointer_triangle", size = size, parts = new[] { StyledPart("Pointer", new MazeVectorShape { kind = MazeVectorShapeKind.Triangle, size = size }, size, Vector2.zero, Fill(Purple)) } };
        }

        public static MazeVectorIconPart StyledPart(string name, MazeVectorShape shape, Vector2 size, Vector2 position, MazeVectorStyle style)
        {
            return new MazeVectorIconPart { name = name, shape = shape, size = size, position = position, role = MazeVectorRole.Accent, useStyleOverride = true, styleOverride = style };
        }

        public static MazeVectorStyle IconLine(float thickness = 3f, float alpha = 0.9f, float intensity = 1f, float selectedIntensity = 1.22f, float selectedThicknessBonus = 1.5f)
        {
            var stroke = Scale(Purple, intensity);
            stroke.a *= alpha;
            var selected = Scale(BrightPurple, selectedIntensity);
            selected.a = Mathf.Clamp01(alpha + 0.14f);
            return new MazeVectorStyle
            {
                fill = false,
                stroke = true,
                strokeColor = stroke,
                strokeThickness = thickness,
                selectedStrokeColor = selected,
                selectedStrokeThickness = thickness + selectedThicknessBonus,
                strokeCap = MazeVectorStrokeCap.Square,
                strokeJoin = MazeVectorStrokeJoin.Bevel,
                glow = selectedIntensity > 1f ? 0.35f : 0f
            };
        }

        public static MazeVectorStyle StrokeStyle(MazeVectorStrokePersonality personality, float thickness = 2.5f, float alpha = 1f, float intensity = 1f, Color? line = null, Color? selectedLine = null, Color? mutedLine = null)
        {
            var baseColor = line ?? Purple;
            var selectedColor = selectedLine ?? BrightPurple;
            var mutedColor = mutedLine ?? DimPurple;
            var width = Mathf.Max(0.5f, thickness);
            var selectedWidth = width + 1.2f;
            var cap = MazeVectorStrokeCap.Square;
            var join = MazeVectorStrokeJoin.Bevel;

            switch (personality)
            {
                case MazeVectorStrokePersonality.ThinBright:
                    width *= 0.62f;
                    selectedWidth = width + 0.8f;
                    baseColor = selectedColor;
                    break;
                case MazeVectorStrokePersonality.ThickAccent:
                    width *= 1.75f;
                    selectedWidth = width + 1.8f;
                    break;
                case MazeVectorStrokePersonality.SplitGapped:
                    width *= 0.78f;
                    selectedWidth = width + 1f;
                    baseColor = mutedColor;
                    break;
                case MazeVectorStrokePersonality.Hooked:
                case MazeVectorStrokePersonality.Ticked:
                case MazeVectorStrokePersonality.Stepped:
                    selectedWidth = width + 1.4f;
                    break;
                case MazeVectorStrokePersonality.Doubled:
                    width *= 0.72f;
                    selectedWidth = width + 1f;
                    break;
                case MazeVectorStrokePersonality.UnevenAccent:
                    width *= 1.12f;
                    selectedWidth = width + 1.6f;
                    baseColor = Scale(baseColor, 1.08f);
                    break;
            }

            baseColor = Scale(baseColor, intensity);
            selectedColor = Scale(selectedColor, Mathf.Max(1f, intensity));
            baseColor.a *= alpha;
            selectedColor.a = Mathf.Clamp01(selectedColor.a * alpha + 0.08f);
            return new MazeVectorStyle
            {
                fill = false,
                stroke = true,
                strokeColor = baseColor,
                strokeThickness = width,
                selectedStrokeColor = selectedColor,
                selectedStrokeThickness = selectedWidth,
                strokeCap = cap,
                strokeJoin = join,
                cornerMode = personality == MazeVectorStrokePersonality.Stepped ? MazeVectorCornerMode.Stepped : MazeVectorCornerMode.Sharp,
                glow = personality == MazeVectorStrokePersonality.ThickAccent || personality == MazeVectorStrokePersonality.ThinBright ? 0.25f : 0f
            };
        }

        public static MazeVectorStyle IconFillGradient(Color top, Color bottom, float alpha = 0.52f, MazeVectorShadingMode shading = MazeVectorShadingMode.VerticalGradient)
        {
            top.a *= alpha;
            bottom.a *= alpha;
            var selectedFill = bottom;
            selectedFill.a = Mathf.Clamp01(bottom.a + 0.1f);
            return new MazeVectorStyle
            {
                fill = true,
                stroke = false,
                fillColor = bottom,
                selectedFillColor = selectedFill,
                shading = shading,
                gradientTopColor = top,
                gradientBottomColor = bottom
            };
        }

        public static MazeVectorStyle AmorphousFill(Color light, Color dark, float alpha = 0.52f, float scale = 1.35f, float sharpness = 0.8f, float seed = 0f, int detail = 4)
        {
            var style = IconFillGradient(light, dark, alpha, MazeVectorShadingMode.AmorphousGradient);
            style.amorphousScale = Mathf.Max(0.05f, scale);
            style.amorphousSharpness = Mathf.Max(0.1f, sharpness);
            style.amorphousSeed = seed;
            style.amorphousDetail = Mathf.Clamp(detail, 2, 8);
            return style;
        }

        public static MazeVectorStyle WithSurfaceNuance(
            MazeVectorStyle source,
            Color darkerStrokePatch,
            float strokePatchStrength = 0.16f,
            float strokePatchScale = 2.5f,
            float strokePatchSharpness = 1.2f,
            float seed = 0f,
            Color? shadow = null,
            Vector2? shadowOffset = null,
            float shadowSpread = 0f,
            float shadowSoftness = 0f,
            Color? softGlow = null,
            float softGlowSpread = 0f)
        {
            var style = source != null ? source.Clone() : new MazeVectorStyle();
            style.strokePatchColor = darkerStrokePatch;
            style.strokePatchStrength = Mathf.Clamp01(strokePatchStrength);
            style.strokePatchScale = Mathf.Max(0.05f, strokePatchScale);
            style.strokePatchSharpness = Mathf.Max(0.1f, strokePatchSharpness);
            style.strokePatchSeed = seed;
            if (shadow.HasValue)
            {
                style.shadowColor = shadow.Value;
                style.shadowOffset = shadowOffset ?? Vector2.zero;
                style.shadowSpread = Mathf.Max(0f, shadowSpread);
                style.shadowSoftness = Mathf.Max(0f, shadowSoftness);
            }
            if (softGlow.HasValue)
            {
                style.softGlowColor = softGlow.Value;
                style.softGlowSpread = Mathf.Max(0f, softGlowSpread);
                style.glow = Mathf.Max(style.glow, 0.01f);
            }
            return style;
        }

        public static MazeVectorIconPart GradientPart(string name, MazeVectorShape shape, Vector2 size, Vector2 position, Color top, Color bottom, float alpha = 0.52f, MazeVectorShadingMode shading = MazeVectorShadingMode.VerticalGradient)
        {
            return StyledPart(name, shape, size, position, IconFillGradient(top, bottom, alpha, shading));
        }

        public static void ApplyIconTones(MazeVectorIconDefinition definition, float thickness = 3f, float alpha = 0.86f, float intensity = 1f, float variation = 0.18f, float selectedBonus = 1.5f)
        {
            if (definition == null || definition.parts == null)
            {
                return;
            }
            var count = Mathf.Max(1, definition.parts.Length);
            for (var i = 0; i < definition.parts.Length; i++)
            {
                var t = count == 1 ? 0.5f : i / (float)(count - 1);
                var alternate = (i % 2 == 0 ? -0.04f : 0.06f) * variation;
                var partIntensity = intensity + (t - 0.5f) * variation + alternate;
                var partThickness = Mathf.Max(0.5f, thickness + (t - 0.5f) * variation * 1.8f);
                definition.parts[i].useStyleOverride = true;
                definition.parts[i].styleOverride = IconLine(partThickness, alpha, partIntensity, 1.12f + t * 0.18f, selectedBonus);
            }
        }

        public static void StylePartByName(MazeVectorIconDefinition definition, string nameContains, MazeVectorStyle style)
        {
            if (definition == null || definition.parts == null || string.IsNullOrWhiteSpace(nameContains) || style == null)
            {
                return;
            }
            for (var i = 0; i < definition.parts.Length; i++)
            {
                if (definition.parts[i] != null && !string.IsNullOrEmpty(definition.parts[i].name) && definition.parts[i].name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    definition.parts[i].useStyleOverride = true;
                    definition.parts[i].styleOverride = style.Clone();
                }
            }
        }

        public static void AppendParts(MazeVectorIconDefinition definition, params MazeVectorIconPart[] extraParts)
        {
            if (definition == null || extraParts == null || extraParts.Length == 0)
            {
                return;
            }
            var existing = definition.parts ?? Array.Empty<MazeVectorIconPart>();
            var parts = new MazeVectorIconPart[existing.Length + extraParts.Length];
            Array.Copy(existing, parts, existing.Length);
            Array.Copy(extraParts, 0, parts, existing.Length, extraParts.Length);
            definition.parts = parts;
        }

        public static void PrependParts(MazeVectorIconDefinition definition, params MazeVectorIconPart[] extraParts)
        {
            if (definition == null || extraParts == null || extraParts.Length == 0)
            {
                return;
            }
            var existing = definition.parts ?? Array.Empty<MazeVectorIconPart>();
            var parts = new MazeVectorIconPart[existing.Length + extraParts.Length];
            Array.Copy(extraParts, parts, extraParts.Length);
            Array.Copy(existing, 0, parts, extraParts.Length, existing.Length);
            definition.parts = parts;
        }


        private static void AddRail(System.Collections.Generic.List<MazeVectorIconPart> parts, string name, float x0, float y0, float x1, float y1, Vector2 size, MazeVectorStyle style)
        {
            if ((new Vector2(x1 - x0, y1 - y0)).sqrMagnitude < 1f)
            {
                return;
            }
            parts.Add(StyledPart(name, NormalizedLine(x0, y0, x1, y1, size), size, Vector2.zero, style));
        }

        private static void AddRampRail(
            System.Collections.Generic.List<MazeVectorIconPart> parts,
            string name,
            Vector2 size,
            bool vertical,
            float direction,
            float perpendicularOffset,
            MazeVectorStyle dim,
            MazeVectorStyle medium,
            MazeVectorStyle bright)
        {
            AddSingleRail(parts, name + " Dim", size, vertical, -0.92f * direction, 0.48f * direction, perpendicularOffset, dim);
            AddSingleRail(parts, name + " Medium", size, vertical, 0.4f * direction, 0.78f * direction, perpendicularOffset, medium);
            AddSingleRail(parts, name + " Peak", size, vertical, 0.72f * direction, 0.93f * direction, perpendicularOffset, bright);
        }

        private static void AddSingleRail(
            System.Collections.Generic.List<MazeVectorIconPart> parts,
            string name,
            Vector2 size,
            bool vertical,
            float start,
            float end,
            float perpendicularOffset,
            MazeVectorStyle style)
        {
            var halfX = size.x * 0.5f;
            var halfY = size.y * 0.5f;
            if (vertical)
            {
                AddRail(parts, name, perpendicularOffset, start * halfY, perpendicularOffset, end * halfY, size, style);
            }
            else
            {
                AddRail(parts, name, start * halfX, perpendicularOffset, end * halfX, perpendicularOffset, size, style);
            }
        }

        private static MazeVectorShape DetailRect(Vector2 size, MazeVectorDetailFamily family)
        {
            return family switch
            {
                MazeVectorDetailFamily.Round => MazeVectorShape.RoundedRect(size, Mathf.Min(7f, Mathf.Min(size.x, size.y) * 0.14f)),
                MazeVectorDetailFamily.Sharp => MazeVectorShape.RoundedRect(size, Mathf.Min(9f, Mathf.Min(size.x, size.y) * 0.18f)),
                _ => MazeVectorShape.Rect(size)
            };
        }

        private static MazeVectorShape DetailNodeShape(float size, MazeVectorDetailFamily family)
        {
            return family switch
            {
                MazeVectorDetailFamily.Round => MazeVectorShape.Circle(size * 0.5f, 16),
                MazeVectorDetailFamily.Sharp => DiamondShape(size),
                _ => MazeVectorShape.Rect(Vector2.one * size)
            };
        }

        private static MazeVectorStyle DetailStroke(MazeVectorDetailFamily family, float thickness, float alpha, float intensity)
        {
            var stroke = Scale(new Color(0.46f, 0.52f, 0.92f, 1f), intensity);
            stroke.a *= alpha;
            var selected = Scale(new Color(0.78f, 0.82f, 1.24f, 1f), Mathf.Max(1f, intensity));
            selected.a = Mathf.Clamp01(alpha + 0.12f);
            var style = new MazeVectorStyle
            {
                fill = false,
                stroke = true,
                strokeColor = stroke,
                strokeThickness = thickness,
                selectedStrokeColor = selected,
                selectedStrokeThickness = thickness + 0.8f
            };
            switch (family)
            {
                case MazeVectorDetailFamily.Round:
                    style.strokeCap = MazeVectorStrokeCap.Round;
                    style.strokeJoin = MazeVectorStrokeJoin.Round;
                    style.cornerMode = MazeVectorCornerMode.Rounded;
                    break;
                case MazeVectorDetailFamily.Sharp:
                    style.strokeCap = MazeVectorStrokeCap.Butt;
                    style.strokeJoin = MazeVectorStrokeJoin.Miter;
                    style.cornerMode = MazeVectorCornerMode.Cut;
                    break;
                default:
                    style.strokeCap = MazeVectorStrokeCap.Square;
                    style.strokeJoin = MazeVectorStrokeJoin.Bevel;
                    style.cornerMode = MazeVectorCornerMode.Sharp;
                    break;
            }
            return style;
        }

        public static MazeVectorStyle ApplyStrokePalette(
            MazeVectorStyle style,
            float sampleLength,
            params MazeVectorStrokeGradientStop[] stops)
        {
            if (style == null)
            {
                return null;
            }

            style.strokeGradientMode = stops != null && stops.Length >= 2
                ? MazeVectorStrokeGradientMode.AlongPath
                : MazeVectorStrokeGradientMode.None;
            style.strokeGradientStops = stops ?? Array.Empty<MazeVectorStrokeGradientStop>();
            style.strokeGradientSampleLength = Mathf.Max(4f, sampleLength);
            return style;
        }

        private static MazeVectorStrokeGradientStop[] DimStrokePalette(
            MazeVectorStrokeGradientStop[] source,
            float alphaScale)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<MazeVectorStrokeGradientStop>();
            }

            var result = new MazeVectorStrokeGradientStop[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var color = source[i].color;
                color.a *= alphaScale;
                result[i] = new MazeVectorStrokeGradientStop(source[i].position, color);
            }
            return result;
        }

        private static float DetailThickness(MazeVectorDetailMass mass)
        {
            return mass switch
            {
                MazeVectorDetailMass.Thin => 1.1f,
                MazeVectorDetailMass.Bulky => 2.4f,
                _ => 1.6f
            };
        }

        private static float DetailInset(MazeVectorDetailSpacing spacing)
        {
            return spacing switch
            {
                MazeVectorDetailSpacing.Tight => 5f,
                MazeVectorDetailSpacing.Spacious => 12f,
                _ => 8f
            };
        }

        private static float DetailNodeSize(MazeVectorDetailMass mass)
        {
            return mass switch
            {
                MazeVectorDetailMass.Thin => 3f,
                MazeVectorDetailMass.Bulky => 6f,
                _ => 4f
            };
        }

        private static float DetailCornerLength(MazeVectorDetailSpacing spacing)
        {
            return spacing switch
            {
                MazeVectorDetailSpacing.Tight => 12f,
                MazeVectorDetailSpacing.Spacious => 26f,
                _ => 18f
            };
        }

        private static void AddSelectorCorner(System.Collections.Generic.List<MazeVectorIconPart> parts, string name, float x, float y, float xDir, float yDir, float length, float hook, MazeVectorStyle style, bool hooked)
        {
            var size = new Vector2(Mathf.Abs(x) * 2f, Mathf.Abs(y) * 2f);
            parts.Add(StyledPart(name + " H", NormalizedLine(x, y, x + xDir * length, y, size), size, Vector2.zero, style));
            parts.Add(StyledPart(name + " V", NormalizedLine(x, y, x, y + yDir * length, size), size, Vector2.zero, style));
            if (hooked)
            {
                parts.Add(StyledPart(name + " H Hook", NormalizedLine(x + xDir * length, y, x + xDir * length, y + yDir * hook, size), size, Vector2.zero, style));
                parts.Add(StyledPart(name + " V Hook", NormalizedLine(x, y + yDir * length, x + xDir * hook, y + yDir * length, size), size, Vector2.zero, style));
            }
        }

        private static MazeVectorShape NormalizedLine(float x0, float y0, float x1, float y1, Vector2 size)
        {
            var halfX = Mathf.Max(0.001f, size.x * 0.5f);
            var halfY = Mathf.Max(0.001f, size.y * 0.5f);
            return MazeVectorShape.Line(new Vector2(x0 / halfX, y0 / halfY), new Vector2(x1 / halfX, y1 / halfY));
        }

        public static MazeVectorShape DiamondShape(float size)
        {
            return new MazeVectorShape { kind = MazeVectorShapeKind.Polygon, points = { new Vector2(0f, 0.8f), new Vector2(0.8f, 0f), new Vector2(0f, -0.8f), new Vector2(-0.8f, 0f) }, closed = true, size = Vector2.one * size };
        }

        public static MazeVectorStyle Fill(Color color)
        {
            return new MazeVectorStyle { fill = true, stroke = false, fillColor = color };
        }

        private static Color Scale(Color color, float intensity)
        {
            intensity = Mathf.Max(0f, intensity);
            return new Color(color.r * intensity, color.g * intensity, color.b * intensity, color.a);
        }
    }
}
