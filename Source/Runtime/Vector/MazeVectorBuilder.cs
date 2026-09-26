using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Maze
{
    public static class MazeVectorBuilder
    {
        public sealed class MeshData
        {
            public readonly List<Vector3> vertices = new(128);
            public readonly List<int> triangles = new(256);
            public readonly List<Color32> colors = new(128);
            public readonly List<Vector2> uvs = new(128);

            public void Clear()
            {
                vertices.Clear();
                triangles.Clear();
                colors.Clear();
                uvs.Clear();
            }
        }

        private static readonly Vector2[] RectPoints = new Vector2[4];
        private static readonly MeshData UiScratch = new();

        public static void BuildUI(VertexHelper helper, Rect rect, MazeVectorShape shape, MazeVectorStyle style, float progress, bool selected)
        {
            BuildUI(helper, UiScratch, rect, shape, style, progress, selected);
        }

        public static void BuildUI(VertexHelper helper, MeshData data, Rect rect, MazeVectorShape shape, MazeVectorStyle style, float progress, bool selected)
        {
            if (helper == null || data == null)
            {
                return;
            }
            helper.Clear();
            Build(data, rect, shape, style, Mathf.Clamp01(progress), selected);
            for (var i = 0; i < data.vertices.Count; i++)
            {
                helper.AddVert(data.vertices[i], data.colors[i], data.uvs[i]);
            }
            for (var i = 0; i + 2 < data.triangles.Count; i += 3)
            {
                helper.AddTriangle(data.triangles[i], data.triangles[i + 1], data.triangles[i + 2]);
            }
        }

        public static void Build(MeshData data, Rect rect, MazeVectorShape shape, MazeVectorStyle style, float progress, bool selected)
        {
            data.Clear();
            if (shape == null || style == null || progress <= 0f)
            {
                return;
            }

            var fillColor = selected ? style.selectedFillColor : style.fillColor;
            var strokeColor = selected ? style.selectedStrokeColor : style.strokeColor;
            var strokeThickness = selected ? Mathf.Max(style.strokeThickness, style.selectedStrokeThickness) : style.strokeThickness;
            fillColor.a *= progress;
            strokeColor.a *= progress;
            var topColor = style.gradientTopColor;
            var bottomColor = style.gradientBottomColor;
            var shadowColor = style.shadowColor;
            var rimColor = style.rimColor;
            var innerStrokeColor = style.innerStrokeColor;
            topColor.a *= progress;
            bottomColor.a *= progress;
            shadowColor.a *= progress;
            rimColor.a *= progress;
            innerStrokeColor.a *= progress;
            AddSoftEffects(data, rect, shape, style, progress);

            switch (shape.kind)
            {
                case MazeVectorShapeKind.Rect:
                    AddRectStyled(data, FitRect(rect, shape.size), fillColor, topColor, bottomColor, shadowColor, style.shadowOffset, style.shadowSpread, style.shadowSoftness, rimColor, style.rimThickness, innerStrokeColor, style.innerStrokeThickness, style.shading, style, style.fill);
                    AddRectOutline(data, FitRect(rect, shape.size), strokeColor, strokeThickness, style.stroke, style.strokeCap, style.strokeJoin, style);
                    break;
                case MazeVectorShapeKind.RoundedRect:
                    var roundedRect = FitRect(rect, shape.size);
                    var roundedRadius = Mathf.Min(shape.cornerRadius, Mathf.Min(rect.width, rect.height) * 0.5f);
                    AddRoundedRectStyled(data, roundedRect, roundedRadius, fillColor, topColor, bottomColor, shadowColor, style.shadowOffset, style.shadowSpread, style.shadowSoftness, rimColor, style.rimThickness, innerStrokeColor, style.innerStrokeThickness, style.shading, style, style.fill, Mathf.Max(4, shape.segments), style.cornerMode);
                    AddRoundedRectOutline(data, roundedRect, roundedRadius, strokeColor, strokeThickness, style.stroke, Mathf.Max(4, shape.segments), style.strokeCap, style.strokeJoin, style.cornerMode, style);
                    break;
                case MazeVectorShapeKind.Circle:
                    var circleRadius = Vector2.one * Mathf.Min(shape.radius, Mathf.Min(rect.width, rect.height) * 0.5f);
                    AddEllipseStyled(data, rect.center, circleRadius, fillColor, topColor, bottomColor, style.shading, style, style.fill, Mathf.Max(8, shape.segments));
                    AddEllipseOutline(data, rect.center, circleRadius, strokeColor, strokeThickness, style.stroke, Mathf.Max(8, shape.segments), style.strokeCap, style.strokeJoin, style);
                    break;
                case MazeVectorShapeKind.Ellipse:
                    var ellipseRadius = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
                    AddEllipseStyled(data, rect.center, ellipseRadius, fillColor, topColor, bottomColor, style.shading, style, style.fill, Mathf.Max(8, shape.segments));
                    AddEllipseOutline(data, rect.center, ellipseRadius, strokeColor, strokeThickness, style.stroke, Mathf.Max(8, shape.segments), style.strokeCap, style.strokeJoin, style);
                    break;
                case MazeVectorShapeKind.Triangle:
                    var tri = RegularPolygon(rect.center, Mathf.Min(rect.width, rect.height) * 0.45f, 3, -90f);
                    AddPolygonStyled(data, tri, fillColor, topColor, bottomColor, style.shading, style, style.fill);
                    AddPolyline(data, tri, true, strokeColor, strokeThickness, style.stroke, style.strokeCap, style.strokeJoin, style);
                    break;
                case MazeVectorShapeKind.Polygon:
                    var poly = shape.points != null && shape.points.Count >= 3 ? ShapePointsToRect(shape.points, rect) : RegularPolygon(rect.center, Mathf.Min(rect.width, rect.height) * 0.45f, Mathf.Max(3, shape.sides), -90f);
                    AddPolygonStyled(data, poly, fillColor, topColor, bottomColor, style.shading, style, style.fill);
                    AddPolyline(data, poly, shape.closed, strokeColor, strokeThickness, style.stroke, style.strokeCap, style.strokeJoin, style);
                    break;
                case MazeVectorShapeKind.LineStrip:
                    AddPolyline(data, ShapePointsToRect(shape.points, rect), shape.closed, strokeColor, Mathf.Max(0.5f, strokeThickness), true, style.strokeCap, style.strokeJoin, style);
                    break;
                case MazeVectorShapeKind.OutlineBand:
                    AddRectOutline(data, FitRect(rect, shape.size), strokeColor, Mathf.Max(0.5f, strokeThickness), true, style.strokeCap, style.strokeJoin, style);
                    break;
                case MazeVectorShapeKind.BrokenRectOutline:
                    AddBrokenRectOutline(data, FitRect(rect, shape.size), strokeColor, Mathf.Max(0.5f, strokeThickness), shape.outlineBreaks, style);
                    break;
                case MazeVectorShapeKind.DashedOutline:
                    AddDashedRectOutline(data, FitRect(rect, shape.size), strokeColor, Mathf.Max(0.5f, strokeThickness), Mathf.Max(1f, shape.dashLength), Mathf.Max(0f, shape.dashGap));
                    break;
                case MazeVectorShapeKind.CornerAccents:
                    AddCornerAccents(data, FitRect(rect, shape.size), strokeColor, Mathf.Max(0.5f, strokeThickness), Mathf.Max(1f, shape.cornerLength), style.strokeCap);
                    break;
                case MazeVectorShapeKind.GridCells:
                    AddGridCells(data, FitRect(rect, shape.size), fillColor, topColor, bottomColor, strokeColor, style.shading, style, style.fill, style.stroke, Mathf.Max(0.5f, strokeThickness), Mathf.Max(1, shape.gridColumns), Mathf.Max(1, shape.gridRows), Mathf.Max(0f, shape.gridGap));
                    break;
            }
        }

        private static Rect FitRect(Rect rect, Vector2 requested)
        {
            var width = requested.x > 0f ? Mathf.Min(rect.width, requested.x) : rect.width;
            var height = requested.y > 0f ? Mathf.Min(rect.height, requested.y) : rect.height;
            return new Rect(rect.center.x - width * 0.5f, rect.center.y - height * 0.5f, width, height);
        }

        private static void AddSoftEffects(MeshData data, Rect rect, MazeVectorShape shape, MazeVectorStyle style, float progress)
        {
            if (style == null || shape == null)
            {
                return;
            }

            if (style.shadowColor.a > 0f && style.shadowSoftness > 0f)
            {
                var shadow = style.shadowColor;
                shadow.a *= progress;
                AddSoftEffectLayers(data, rect, shape, shadow, style.shadowOffset, style.shadowSpread, style.shadowSoftness, 3, style);
            }

            if (style.softGlowColor.a > 0f && style.glow > 0f && style.softGlowSpread > 0f)
            {
                var glow = style.softGlowColor;
                glow.a *= progress * Mathf.Clamp(style.glow, 0f, 2f);
                AddSoftEffectLayers(data, rect, shape, glow, Vector2.zero, 0f, style.softGlowSpread, 3, style);
            }
        }

        private static void AddSoftEffectLayers(MeshData data, Rect rect, MazeVectorShape shape, Color color, Vector2 offset, float spread, float softness, int layers, MazeVectorStyle style)
        {
            layers = Mathf.Clamp(layers, 1, 4);
            for (var layer = layers; layer >= 1; layer--)
            {
                var t = layer / (float)layers;
                var layerColor = color;
                layerColor.a *= Mathf.Lerp(0.48f, 0.12f, t);
                AddEffectShape(data, rect, shape, layerColor, offset, spread + softness * t, style);
            }
        }

        private static void AddEffectShape(MeshData data, Rect rect, MazeVectorShape shape, Color color, Vector2 offset, float expansion, MazeVectorStyle style)
        {
            if (color.a <= 0f)
            {
                return;
            }

            var fitted = FitRect(rect, shape.size);
            fitted.position += offset - Vector2.one * expansion;
            fitted.size += Vector2.one * expansion * 2f;
            switch (shape.kind)
            {
                case MazeVectorShapeKind.Rect:
                case MazeVectorShapeKind.OutlineBand:
                case MazeVectorShapeKind.BrokenRectOutline:
                case MazeVectorShapeKind.DashedOutline:
                case MazeVectorShapeKind.CornerAccents:
                    AddRect(data, fitted, color, true);
                    break;
                case MazeVectorShapeKind.RoundedRect:
                    AddPolygon(data, CornerRectPoints(fitted, shape.cornerRadius + expansion, Mathf.Max(8, shape.segments), style.cornerMode), color, true);
                    break;
                case MazeVectorShapeKind.Circle:
                    AddEllipse(data, rect.center + offset, Vector2.one * (Mathf.Min(shape.radius, Mathf.Min(rect.width, rect.height) * 0.5f) + expansion), color, true, Mathf.Max(12, shape.segments));
                    break;
                case MazeVectorShapeKind.Ellipse:
                    AddEllipse(data, rect.center + offset, new Vector2(rect.width * 0.5f + expansion, rect.height * 0.5f + expansion), color, true, Mathf.Max(12, shape.segments));
                    break;
                case MazeVectorShapeKind.Triangle:
                    AddPolygon(data, ExpandPoints(RegularPolygon(rect.center + offset, Mathf.Min(rect.width, rect.height) * 0.45f, 3, -90f), expansion), color, true);
                    break;
                case MazeVectorShapeKind.Polygon:
                    var points = shape.points != null && shape.points.Count >= 3
                        ? ShapePointsToRect(shape.points, rect)
                        : RegularPolygon(rect.center, Mathf.Min(rect.width, rect.height) * 0.45f, Mathf.Max(3, shape.sides), -90f);
                    AddPolygon(data, ExpandPoints(OffsetPoints(points, offset), expansion), color, true);
                    break;
                case MazeVectorShapeKind.LineStrip:
                    AddPolyline(data, OffsetPoints(ShapePointsToRect(shape.points, rect), offset), shape.closed, color, Mathf.Max(0.5f, style.strokeThickness + expansion * 2f), true, style.strokeCap, style.strokeJoin);
                    break;
            }
        }

        private static Vector2[] OffsetPoints(IReadOnlyList<Vector2> points, Vector2 offset)
        {
            var output = new Vector2[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                output[i] = points[i] + offset;
            }
            return output;
        }

        private static Vector2[] ExpandPoints(IReadOnlyList<Vector2> points, float expansion)
        {
            var center = Vector2.zero;
            for (var i = 0; i < points.Count; i++)
            {
                center += points[i];
            }
            center /= Mathf.Max(1, points.Count);
            var output = new Vector2[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var direction = (points[i] - center).normalized;
                output[i] = points[i] + direction * expansion;
            }
            return output;
        }

        private static void AddRect(MeshData data, Rect rect, Color color, bool enabled)
        {
            if (!enabled || color.a <= 0f || rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }
            RectPoints[0] = new Vector2(rect.xMin, rect.yMin);
            RectPoints[1] = new Vector2(rect.xMin, rect.yMax);
            RectPoints[2] = new Vector2(rect.xMax, rect.yMax);
            RectPoints[3] = new Vector2(rect.xMax, rect.yMin);
            AddPolygon(data, RectPoints, color, true);
        }

        private static void AddRectStyled(MeshData data, Rect rect, Color fill, Color top, Color bottom, Color shadow, Vector2 shadowOffset, float shadowSpread, float shadowSoftness, Color rim, float rimThickness, Color inner, float innerThickness, MazeVectorShadingMode shading, MazeVectorStyle gradientStyle, bool enabled)
        {
            if (!enabled)
            {
                return;
            }
            if (shadowSoftness <= 0f)
            {
                AddShadowRect(data, rect, shadow, shadowOffset, shadowSpread);
            }
            if (IsGradient(shading) && top.a > 0f && bottom.a > 0f)
            {
                AddGradientRect(data, rect, top, bottom, shading, gradientStyle);
            }
            else
            {
                AddRect(data, rect, fill, true);
            }
            AddRim(data, rect, rim, rimThickness);
            AddInnerRect(data, rect, inner, innerThickness);
        }

        private static void AddRoundedRectStyled(MeshData data, Rect rect, float radius, Color fill, Color top, Color bottom, Color shadow, Vector2 shadowOffset, float shadowSpread, float shadowSoftness, Color rim, float rimThickness, Color inner, float innerThickness, MazeVectorShadingMode shading, MazeVectorStyle gradientStyle, bool enabled, int segments, MazeVectorCornerMode cornerMode)
        {
            if (!enabled)
            {
                return;
            }
            if (shadowSoftness <= 0f)
            {
                AddShadowRect(data, rect, shadow, shadowOffset, shadowSpread);
            }
            var points = CornerRectPoints(rect, radius, segments, cornerMode);
            if (IsGradient(shading) && top.a > 0f && bottom.a > 0f)
            {
                AddGradientPolygon(data, points, top, bottom, shading, gradientStyle);
            }
            else
            {
                AddPolygon(data, points, fill, true);
            }
            AddRim(data, rect, rim, rimThickness);
            AddInnerRect(data, rect, inner, innerThickness);
        }

        private static void AddShadowRect(MeshData data, Rect rect, Color color, Vector2 offset, float spread)
        {
            if (color.a <= 0f)
            {
                return;
            }
            var shadowRect = new Rect(rect.xMin + offset.x - spread, rect.yMin + offset.y - spread, rect.width + spread * 2f, rect.height + spread * 2f);
            AddRect(data, shadowRect, color, true);
        }

        private static void AddRim(MeshData data, Rect rect, Color color, float thickness)
        {
            if (color.a <= 0f || thickness <= 0f)
            {
                return;
            }
            var rimRect = new Rect(rect.xMin - thickness * 0.5f, rect.yMin - thickness * 0.5f, rect.width + thickness, rect.height + thickness);
            AddRectOutline(data, rimRect, color, thickness, true, MazeVectorStrokeCap.Butt, MazeVectorStrokeJoin.Bevel);
        }

        private static void AddInnerRect(MeshData data, Rect rect, Color color, float thickness)
        {
            if (color.a <= 0f || thickness <= 0f)
            {
                return;
            }
            var innerRect = new Rect(rect.xMin + thickness, rect.yMin + thickness, rect.width - thickness * 2f, rect.height - thickness * 2f);
            AddRectOutline(data, innerRect, color, thickness, innerRect.width > 0f && innerRect.height > 0f, MazeVectorStrokeCap.Butt, MazeVectorStrokeJoin.Bevel);
        }

        private static void AddGradientRect(MeshData data, Rect rect, Color top, Color bottom, MazeVectorShadingMode shading, MazeVectorStyle style = null)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }
            if (shading == MazeVectorShadingMode.AmorphousGradient)
            {
                AddAmorphousRect(data, rect, top, bottom, style);
                return;
            }
            var start = data.vertices.Count;
            var min = new Vector2(rect.xMin, rect.yMin);
            var max = new Vector2(rect.xMax, rect.yMax);
            var p0 = new Vector2(rect.xMin, rect.yMin);
            var p1 = new Vector2(rect.xMin, rect.yMax);
            var p2 = new Vector2(rect.xMax, rect.yMax);
            var p3 = new Vector2(rect.xMax, rect.yMin);
            AddVertex(data, p0, GradientColor(p0, min, max, top, bottom, shading, style));
            AddVertex(data, p1, GradientColor(p1, min, max, top, bottom, shading, style));
            AddVertex(data, p2, GradientColor(p2, min, max, top, bottom, shading, style));
            AddVertex(data, p3, GradientColor(p3, min, max, top, bottom, shading, style));
            data.triangles.Add(start);
            data.triangles.Add(start + 1);
            data.triangles.Add(start + 2);
            data.triangles.Add(start);
            data.triangles.Add(start + 2);
            data.triangles.Add(start + 3);
        }

        private static void AddAmorphousRect(MeshData data, Rect rect, Color top, Color bottom, MazeVectorStyle style)
        {
            var detail = Mathf.Clamp(style != null ? style.amorphousDetail : 4, 2, 8);
            var aspect = Mathf.Max(0.1f, rect.width / Mathf.Max(0.1f, rect.height));
            var aspectRoot = Mathf.Sqrt(aspect);
            var columns = Mathf.Clamp(Mathf.RoundToInt(detail * aspectRoot), 2, 12);
            var rows = Mathf.Clamp(Mathf.RoundToInt(detail / aspectRoot), 2, 8);
            var start = data.vertices.Count;
            for (var y = 0; y <= rows; y++)
            {
                var ny = y / (float)rows;
                for (var x = 0; x <= columns; x++)
                {
                    var nx = x / (float)columns;
                    var point = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, nx), Mathf.Lerp(rect.yMin, rect.yMax, ny));
                    var sample = AmorphousSample(new Vector2(nx, ny), style);
                    AddVertex(data, point, Color.Lerp(bottom, top, sample));
                }
            }
            var stride = columns + 1;
            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < columns; x++)
                {
                    var a = start + y * stride + x;
                    var b = a + stride;
                    data.triangles.Add(a);
                    data.triangles.Add(b);
                    data.triangles.Add(b + 1);
                    data.triangles.Add(a);
                    data.triangles.Add(b + 1);
                    data.triangles.Add(a + 1);
                }
            }
        }

        private static void AddGradientPolygon(MeshData data, IReadOnlyList<Vector2> points, Color top, Color bottom, MazeVectorShadingMode shading, MazeVectorStyle style = null)
        {
            if (points == null || points.Count < 3)
            {
                return;
            }
            var minY = points[0].y;
            var maxY = points[0].y;
            var minX = points[0].x;
            var maxX = points[0].x;
            var center = Vector2.zero;
            for (var i = 0; i < points.Count; i++)
            {
                center += points[i];
                minX = Mathf.Min(minX, points[i].x);
                maxX = Mathf.Max(maxX, points[i].x);
                minY = Mathf.Min(minY, points[i].y);
                maxY = Mathf.Max(maxY, points[i].y);
            }
            center /= points.Count;
            var min = new Vector2(minX, minY);
            var max = new Vector2(maxX, maxY);
            var start = data.vertices.Count;
            AddVertex(data, center, GradientColor(center, min, max, top, bottom, shading, style));
            for (var i = 0; i < points.Count; i++)
            {
                AddVertex(data, points[i], GradientColor(points[i], min, max, top, bottom, shading, style));
            }
            for (var i = 0; i < points.Count; i++)
            {
                data.triangles.Add(start);
                data.triangles.Add(start + 1 + i);
                data.triangles.Add(start + 1 + ((i + 1) % points.Count));
            }
        }

        private static void AddRectOutline(MeshData data, Rect rect, Color color, float thickness, bool enabled, MazeVectorStrokeCap cap = MazeVectorStrokeCap.Butt, MazeVectorStrokeJoin join = MazeVectorStrokeJoin.Bevel, MazeVectorStyle variationStyle = null)
        {
            if (!enabled || color.a <= 0f || thickness <= 0f || rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            if (HasStrokeVariation(variationStyle))
            {
                AddPolyline(data, CornerRectPoints(rect, 0f, 4, MazeVectorCornerMode.Sharp), true, color, thickness, true, cap, join, variationStyle);
                return;
            }

            AddRectOutlineBand(data, rect, color, thickness);
            if (join == MazeVectorStrokeJoin.Round)
            {
                var radius = thickness * 0.5f;
                AddCircle(data, new Vector2(rect.xMin, rect.yMin), radius, color, 12);
                AddCircle(data, new Vector2(rect.xMin, rect.yMax), radius, color, 12);
                AddCircle(data, new Vector2(rect.xMax, rect.yMax), radius, color, 12);
                AddCircle(data, new Vector2(rect.xMax, rect.yMin), radius, color, 12);
            }
        }

        private static void AddRectOutlineBand(MeshData data, Rect rect, Color color, float thickness)
        {
            var half = thickness * 0.5f;
            var outerXMin = rect.xMin - half;
            var outerXMax = rect.xMax + half;
            var outerYMin = rect.yMin - half;
            var outerYMax = rect.yMax + half;
            var innerXMin = rect.xMin + half;
            var innerXMax = rect.xMax - half;
            var innerYMin = rect.yMin + half;
            var innerYMax = rect.yMax - half;

            if (innerXMin >= innerXMax || innerYMin >= innerYMax)
            {
                AddRect(data, new Rect(outerXMin, outerYMin, outerXMax - outerXMin, outerYMax - outerYMin), color, true);
                return;
            }

            AddRect(data, new Rect(outerXMin, innerYMax, outerXMax - outerXMin, outerYMax - innerYMax), color, true);
            AddRect(data, new Rect(outerXMin, outerYMin, outerXMax - outerXMin, innerYMin - outerYMin), color, true);
            AddRect(data, new Rect(outerXMin, innerYMin, innerXMin - outerXMin, innerYMax - innerYMin), color, true);
            AddRect(data, new Rect(innerXMax, innerYMin, outerXMax - innerXMax, innerYMax - innerYMin), color, true);
        }

        private static void AddRoundedRect(MeshData data, Rect rect, float radius, Color color, bool enabled, int segments)
        {
            if (!enabled || color.a <= 0f)
            {
                return;
            }
            AddPolygon(data, RoundedRectPoints(rect, radius, segments), color, true);
        }

        private static void AddRoundedRectOutline(MeshData data, Rect rect, float radius, Color color, float thickness, bool enabled, int segments, MazeVectorStrokeCap cap = MazeVectorStrokeCap.Butt, MazeVectorStrokeJoin join = MazeVectorStrokeJoin.Bevel, MazeVectorCornerMode cornerMode = MazeVectorCornerMode.Rounded, MazeVectorStyle variationStyle = null)
        {
            if (!enabled || color.a <= 0f || thickness <= 0f)
            {
                return;
            }
            AddPolyline(data, CornerRectPoints(rect, radius, segments, cornerMode), true, color, thickness, true, cap, join, variationStyle);
        }

        private static Vector2[] RoundedRectPoints(Rect rect, float radius, int segments)
        {
            return CornerRectPoints(rect, radius, segments, MazeVectorCornerMode.Rounded);
        }

        private static Vector2[] CornerRectPoints(Rect rect, float radius, int segments, MazeVectorCornerMode cornerMode)
        {
            radius = Mathf.Max(0f, Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f));
            if (cornerMode == MazeVectorCornerMode.Sharp || radius <= 0.001f)
            {
                return new[]
                {
                    new Vector2(rect.xMax, rect.yMax),
                    new Vector2(rect.xMin, rect.yMax),
                    new Vector2(rect.xMin, rect.yMin),
                    new Vector2(rect.xMax, rect.yMin)
                };
            }
            if (cornerMode == MazeVectorCornerMode.Cut)
            {
                return new[]
                {
                    new Vector2(rect.xMax - radius, rect.yMax),
                    new Vector2(rect.xMin + radius, rect.yMax),
                    new Vector2(rect.xMin, rect.yMax - radius),
                    new Vector2(rect.xMin, rect.yMin + radius),
                    new Vector2(rect.xMin + radius, rect.yMin),
                    new Vector2(rect.xMax - radius, rect.yMin),
                    new Vector2(rect.xMax, rect.yMin + radius),
                    new Vector2(rect.xMax, rect.yMax - radius)
                };
            }
            if (cornerMode == MazeVectorCornerMode.Stepped || cornerMode == MazeVectorCornerMode.Bracket)
            {
                var step = radius * 0.55f;
                return new[]
                {
                    new Vector2(rect.xMax - radius, rect.yMax),
                    new Vector2(rect.xMin + radius, rect.yMax),
                    new Vector2(rect.xMin + radius, rect.yMax - step),
                    new Vector2(rect.xMin + step, rect.yMax - step),
                    new Vector2(rect.xMin + step, rect.yMax - radius),
                    new Vector2(rect.xMin, rect.yMax - radius),
                    new Vector2(rect.xMin, rect.yMin + radius),
                    new Vector2(rect.xMin + step, rect.yMin + radius),
                    new Vector2(rect.xMin + step, rect.yMin + step),
                    new Vector2(rect.xMin + radius, rect.yMin + step),
                    new Vector2(rect.xMin + radius, rect.yMin),
                    new Vector2(rect.xMax - radius, rect.yMin),
                    new Vector2(rect.xMax - radius, rect.yMin + step),
                    new Vector2(rect.xMax - step, rect.yMin + step),
                    new Vector2(rect.xMax - step, rect.yMin + radius),
                    new Vector2(rect.xMax, rect.yMin + radius),
                    new Vector2(rect.xMax, rect.yMax - radius),
                    new Vector2(rect.xMax - step, rect.yMax - radius),
                    new Vector2(rect.xMax - step, rect.yMax - step),
                    new Vector2(rect.xMax - radius, rect.yMax - step)
                };
            }
            var perCorner = Mathf.Max(2, segments / 4);
            var points = new List<Vector2>(perCorner * 4 + 4);
            AddCorner(points, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f, perCorner);
            AddCorner(points, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f, perCorner);
            AddCorner(points, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f, perCorner);
            AddCorner(points, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f, perCorner);
            return points.ToArray();
        }

        private static void AddCorner(List<Vector2> points, Vector2 center, float radius, float start, float end, int steps)
        {
            for (var i = 0; i <= steps; i++)
            {
                var angle = Mathf.Lerp(start, end, i / (float)steps) * Mathf.Deg2Rad;
                points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private static void AddEllipse(MeshData data, Vector2 center, Vector2 radius, Color color, bool enabled, int segments)
        {
            if (!enabled || color.a <= 0f)
            {
                return;
            }
            AddPolygon(data, EllipsePoints(center, radius, segments), color, true);
        }

        private static void AddEllipseStyled(MeshData data, Vector2 center, Vector2 radius, Color fill, Color top, Color bottom, MazeVectorShadingMode shading, MazeVectorStyle gradientStyle, bool enabled, int segments)
        {
            if (!enabled)
            {
                return;
            }
            AddPolygonStyled(data, EllipsePoints(center, radius, segments), fill, top, bottom, shading, gradientStyle, true);
        }

        private static void AddEllipseOutline(MeshData data, Vector2 center, Vector2 radius, Color color, float thickness, bool enabled, int segments, MazeVectorStrokeCap cap = MazeVectorStrokeCap.Butt, MazeVectorStrokeJoin join = MazeVectorStrokeJoin.Bevel, MazeVectorStyle variationStyle = null)
        {
            if (!enabled || color.a <= 0f || thickness <= 0f)
            {
                return;
            }
            if (HasStrokeVariation(variationStyle))
            {
                AddPolyline(data, EllipsePoints(center, radius, segments), true, color, thickness, true, cap, join, variationStyle);
                return;
            }
            AddEllipseOutlineBand(data, center, radius, color, thickness, segments);
        }

        private static void AddEllipseOutlineBand(MeshData data, Vector2 center, Vector2 radius, Color color, float thickness, int segments)
        {
            segments = Mathf.Max(8, segments);
            var half = thickness * 0.5f;
            var outerRadius = new Vector2(radius.x + half, radius.y + half);
            var innerRadius = new Vector2(Mathf.Max(0f, radius.x - half), Mathf.Max(0f, radius.y - half));
            if (innerRadius.x <= 0f || innerRadius.y <= 0f)
            {
                AddPolygon(data, EllipsePoints(center, outerRadius, segments), color, true);
                return;
            }

            var start = data.vertices.Count;
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                var unit = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AddVertex(data, center + new Vector2(unit.x * outerRadius.x, unit.y * outerRadius.y), color);
                AddVertex(data, center + new Vector2(unit.x * innerRadius.x, unit.y * innerRadius.y), color);
            }
            for (var i = 0; i < segments; i++)
            {
                var next = (i + 1) % segments;
                var outerA = start + i * 2;
                var innerA = outerA + 1;
                var outerB = start + next * 2;
                var innerB = outerB + 1;
                data.triangles.Add(outerA);
                data.triangles.Add(outerB);
                data.triangles.Add(innerB);
                data.triangles.Add(outerA);
                data.triangles.Add(innerB);
                data.triangles.Add(innerA);
            }
        }

        private static Vector2[] EllipsePoints(Vector2 center, Vector2 radius, int segments)
        {
            segments = Mathf.Max(8, segments);
            var points = new Vector2[segments];
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                points[i] = center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y);
            }
            return points;
        }

        private static Vector2[] RegularPolygon(Vector2 center, float radius, int sides, float angleOffset)
        {
            sides = Mathf.Max(3, sides);
            var points = new Vector2[sides];
            for (var i = 0; i < sides; i++)
            {
                var angle = (angleOffset + (360f * i / sides)) * Mathf.Deg2Rad;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }

        private static Vector2[] ShapePointsToRect(IReadOnlyList<Vector2> points, Rect rect)
        {
            if (points == null || points.Count == 0)
            {
                return new[] { rect.center };
            }
            var output = new Vector2[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                output[i] = new Vector2(rect.center.x + points[i].x * rect.width * 0.5f, rect.center.y + points[i].y * rect.height * 0.5f);
            }
            return output;
        }

        private static void AddPolygon(MeshData data, IReadOnlyList<Vector2> points, Color color, bool enabled)
        {
            if (!enabled || color.a <= 0f || points == null || points.Count < 3)
            {
                return;
            }
            var start = data.vertices.Count;
            var center = Vector2.zero;
            for (var i = 0; i < points.Count; i++) center += points[i];
            center /= points.Count;
            AddVertex(data, center, color);
            for (var i = 0; i < points.Count; i++) AddVertex(data, points[i], color);
            for (var i = 0; i < points.Count; i++)
            {
                data.triangles.Add(start);
                data.triangles.Add(start + 1 + i);
                data.triangles.Add(start + 1 + ((i + 1) % points.Count));
            }
        }

        private static void AddPolygonStyled(MeshData data, IReadOnlyList<Vector2> points, Color fill, Color top, Color bottom, MazeVectorShadingMode shading, MazeVectorStyle gradientStyle, bool enabled)
        {
            if (!enabled || points == null || points.Count < 3)
            {
                return;
            }
            if (IsGradient(shading) && top.a > 0f && bottom.a > 0f)
            {
                AddGradientPolygon(data, points, top, bottom, shading, gradientStyle);
            }
            else
            {
                AddPolygon(data, points, fill, true);
            }
        }

        private static void AddPolyline(MeshData data, IReadOnlyList<Vector2> points, bool closed, Color color, float thickness, bool enabled, MazeVectorStrokeCap cap = MazeVectorStrokeCap.Butt, MazeVectorStrokeJoin join = MazeVectorStrokeJoin.Bevel, MazeVectorStyle variationStyle = null)
        {
            if (!enabled || color.a <= 0f || points == null || points.Count < 2 || thickness <= 0f)
            {
                return;
            }
            var segmentCount = closed ? points.Count : points.Count - 1;
            var bounds = BoundsOf(points);
            var hasGradient = HasStrokeGradient(variationStyle);
            var hasPatches = HasStrokePatches(variationStyle);
            var pointDistances = hasGradient ? new float[points.Count] : null;
            var totalLength = 0f;
            for (var i = 0; i < segmentCount; i++)
            {
                if (pointDistances != null && i < points.Count)
                {
                    pointDistances[i] = totalLength;
                }
                totalLength += Vector2.Distance(points[i], points[(i + 1) % points.Count]);
            }
            totalLength = Mathf.Max(0.0001f, totalLength);

            for (var i = 0; i < segmentCount; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                if (!hasGradient && !hasPatches)
                {
                    AddSegment(data, a, b, color, thickness, cap);
                    continue;
                }

                var segmentLength = Vector2.Distance(a, b);
                var subdivisions = 1;
                if (hasPatches)
                {
                    var relativeLength = segmentLength / Mathf.Max(1f, Mathf.Min(bounds.width, bounds.height));
                    subdivisions = Mathf.Max(
                        subdivisions,
                        Mathf.Clamp(Mathf.CeilToInt(relativeLength * variationStyle.strokePatchScale * 2.5f), 1, 10));
                }
                if (hasGradient)
                {
                    subdivisions = Mathf.Max(
                        subdivisions,
                        Mathf.Clamp(
                            Mathf.CeilToInt(segmentLength / Mathf.Max(4f, variationStyle.strokeGradientSampleLength)),
                            1,
                            64));
                }

                var distanceAtA = pointDistances != null ? pointDistances[i] : 0f;
                for (var subdivision = 0; subdivision < subdivisions; subdivision++)
                {
                    var partA = subdivision / (float)subdivisions;
                    var partB = (subdivision + 1f) / subdivisions;
                    var sampleA = Vector2.Lerp(a, b, partA);
                    var sampleB = Vector2.Lerp(a, b, partB);
                    var colorA = color;
                    var colorB = color;
                    if (hasPatches)
                    {
                        colorA = StrokePatchColor(colorA, sampleA, bounds, variationStyle);
                        colorB = StrokePatchColor(colorB, sampleB, bounds, variationStyle);
                    }
                    if (hasGradient)
                    {
                        colorA = StrokeGradientColor(
                            colorA,
                            sampleA,
                            bounds,
                            (distanceAtA + segmentLength * partA) / totalLength,
                            closed,
                            variationStyle);
                        colorB = StrokeGradientColor(
                            colorB,
                            sampleB,
                            bounds,
                            (distanceAtA + segmentLength * partB) / totalLength,
                            closed,
                            variationStyle);
                    }
                    AddSegment(data, sampleA, sampleB, colorA, colorB, thickness, cap);
                }
            }
            if (join == MazeVectorStrokeJoin.Round)
            {
                var joinCount = closed ? points.Count : points.Count - 2;
                var startIndex = closed ? 0 : 1;
                for (var i = 0; i < joinCount; i++)
                {
                    var pointIndex = (startIndex + i) % points.Count;
                    var joinColor = StrokeVariationColor(
                        color,
                        points[pointIndex],
                        bounds,
                        pointDistances != null ? pointDistances[pointIndex] / totalLength : 0f,
                        closed,
                        variationStyle);
                    AddCircle(data, points[pointIndex], thickness * 0.5f, joinColor, 12);
                }
            }
            else
            {
                var joinCount = closed ? points.Count : points.Count - 2;
                var startIndex = closed ? 0 : 1;
                for (var i = 0; i < joinCount; i++)
                {
                    var pointIndex = (startIndex + i) % points.Count;
                    var joinColor = StrokeVariationColor(
                        color,
                        points[pointIndex],
                        bounds,
                        pointDistances != null ? pointDistances[pointIndex] / totalLength : 0f,
                        closed,
                        variationStyle);
                    AddJoinPatch(data, points[pointIndex], joinColor, thickness);
                }
            }
            if (!closed && cap == MazeVectorStrokeCap.Round)
            {
                AddCircle(
                    data,
                    points[0],
                    thickness * 0.5f,
                    StrokeVariationColor(color, points[0], bounds, 0f, false, variationStyle),
                    12);
                AddCircle(
                    data,
                    points[points.Count - 1],
                    thickness * 0.5f,
                    StrokeVariationColor(color, points[points.Count - 1], bounds, 1f, false, variationStyle),
                    12);
            }
        }

        private static void AddJoinPatch(MeshData data, Vector2 point, Color color, float thickness)
        {
            var size = Mathf.Max(0.5f, thickness);
            AddRect(data, new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size), color, true);
        }

        private static void AddSegment(MeshData data, Vector2 a, Vector2 b, Color color, float thickness, MazeVectorStrokeCap cap = MazeVectorStrokeCap.Butt)
        {
            AddSegment(data, a, b, color, color, thickness, cap);
        }

        private static void AddSegment(MeshData data, Vector2 a, Vector2 b, Color colorA, Color colorB, float thickness, MazeVectorStrokeCap cap = MazeVectorStrokeCap.Butt)
        {
            var delta = b - a;
            if (delta.sqrMagnitude < 0.001f)
            {
                return;
            }
            if (cap == MazeVectorStrokeCap.Square)
            {
                var extend = delta.normalized * (thickness * 0.5f);
                a -= extend;
                b += extend;
            }
            var normal = new Vector2(-delta.y, delta.x).normalized * (thickness * 0.5f);
            var start = data.vertices.Count;
            AddVertex(data, a - normal, colorA);
            AddVertex(data, a + normal, colorA);
            AddVertex(data, b + normal, colorB);
            AddVertex(data, b - normal, colorB);
            data.triangles.Add(start);
            data.triangles.Add(start + 1);
            data.triangles.Add(start + 2);
            data.triangles.Add(start);
            data.triangles.Add(start + 2);
            data.triangles.Add(start + 3);
        }

        private static void AddCircle(MeshData data, Vector2 center, float radius, Color color, int segments)
        {
            AddEllipse(data, center, Vector2.one * Mathf.Max(0.01f, radius), color, true, Mathf.Max(8, segments));
        }

        private static void AddDashedRectOutline(MeshData data, Rect rect, Color color, float thickness, float dash, float gap)
        {
            RectPoints[0] = new Vector2(rect.xMin, rect.yMin);
            RectPoints[1] = new Vector2(rect.xMin, rect.yMax);
            RectPoints[2] = new Vector2(rect.xMax, rect.yMax);
            RectPoints[3] = new Vector2(rect.xMax, rect.yMin);
            for (var i = 0; i < 4; i++)
            {
                var a = RectPoints[i];
                var b = RectPoints[(i + 1) % 4];
                var length = Vector2.Distance(a, b);
                var cursor = 0f;
                while (cursor < length)
                {
                    var end = Mathf.Min(cursor + dash, length);
                    AddSegment(data, Vector2.Lerp(a, b, cursor / length), Vector2.Lerp(a, b, end / length), color, thickness);
                    cursor = end + gap;
                }
            }
        }

        private static void AddBrokenRectOutline(
            MeshData data,
            Rect rect,
            Color color,
            float thickness,
            IReadOnlyList<MazeVectorOutlineBreak> marks,
            MazeVectorStyle variationStyle)
        {
            if (color.a <= 0f || thickness <= 0f || rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }
            if (marks == null || marks.Count == 0)
            {
                AddRectOutline(data, rect, color, thickness, true, MazeVectorStrokeCap.Square, MazeVectorStrokeJoin.Bevel, variationStyle);
                return;
            }

            var perimeter = Mathf.Max(0.0001f, (rect.width + rect.height) * 2f);
            var breakpoints = new List<float>(marks.Count * 2 + 6)
            {
                0f,
                rect.width / perimeter,
                (rect.width + rect.height) / perimeter,
                (rect.width * 2f + rect.height) / perimeter,
                1f
            };
            for (var i = 0; i < marks.Count; i++)
            {
                var mark = marks[i];
                if (mark == null || mark.length01 <= 0f)
                {
                    continue;
                }
                breakpoints.Add(Mathf.Repeat(mark.start01, 1f));
                breakpoints.Add(Mathf.Repeat(mark.start01 + Mathf.Min(mark.length01, 1f), 1f));
            }
            breakpoints.Sort();

            var previous = breakpoints[0];
            for (var i = 1; i < breakpoints.Count; i++)
            {
                var next = breakpoints[i];
                if (next - previous <= 0.00001f)
                {
                    continue;
                }

                var midpoint = (previous + next) * 0.5f;
                var kind = MazeVectorOutlineBreakKind.Chip;
                var depth = 0f;
                var marked = false;
                for (var markIndex = 0; markIndex < marks.Count; markIndex++)
                {
                    var mark = marks[markIndex];
                    if (mark == null || !ContainsPerimeter01(mark, midpoint))
                    {
                        continue;
                    }
                    marked = true;
                    if (mark.kind == MazeVectorOutlineBreakKind.Gap)
                    {
                        kind = MazeVectorOutlineBreakKind.Gap;
                        depth = 1f;
                        break;
                    }
                    depth = Mathf.Max(depth, mark.depth);
                }

                if (!marked || kind != MazeVectorOutlineBreakKind.Gap)
                {
                    var intervalThickness = marked
                        ? Mathf.Max(0.35f, thickness * (1f - Mathf.Clamp01(depth) * 0.82f))
                        : thickness;
                    var points = new[]
                    {
                        PointOnRectPerimeter(rect, previous),
                        PointOnRectPerimeter(rect, next)
                    };
                    AddPolyline(data, points, false, color, intervalThickness, true, MazeVectorStrokeCap.Square, MazeVectorStrokeJoin.Bevel, variationStyle);
                }
                previous = next;
            }
        }

        private static bool ContainsPerimeter01(MazeVectorOutlineBreak mark, float value)
        {
            var length = Mathf.Clamp01(mark.length01);
            if (length >= 0.99999f)
            {
                return true;
            }
            return Mathf.Repeat(value - Mathf.Repeat(mark.start01, 1f), 1f) < length;
        }

        private static Vector2 PointOnRectPerimeter(Rect rect, float normalized)
        {
            var width = rect.width;
            var height = rect.height;
            var distance = Mathf.Clamp01(normalized) * ((width + height) * 2f);
            if (distance <= width)
            {
                return new Vector2(rect.xMax - distance, rect.yMax);
            }
            distance -= width;
            if (distance <= height)
            {
                return new Vector2(rect.xMin, rect.yMax - distance);
            }
            distance -= height;
            if (distance <= width)
            {
                return new Vector2(rect.xMin + distance, rect.yMin);
            }
            distance -= width;
            return new Vector2(rect.xMax, rect.yMin + Mathf.Min(distance, height));
        }

        private static void AddCornerAccents(MeshData data, Rect rect, Color color, float thickness, float length, MazeVectorStrokeCap cap = MazeVectorStrokeCap.Butt)
        {
            var l = Mathf.Min(length, Mathf.Min(rect.width, rect.height) * 0.45f);
            AddSegment(data, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin + l, rect.yMin), color, thickness, cap);
            AddSegment(data, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMin + l), color, thickness, cap);
            AddSegment(data, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin + l, rect.yMax), color, thickness, cap);
            AddSegment(data, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMax - l), color, thickness, cap);
            AddSegment(data, new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax - l, rect.yMax), color, thickness, cap);
            AddSegment(data, new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMax - l), color, thickness, cap);
            AddSegment(data, new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax - l, rect.yMin), color, thickness, cap);
            AddSegment(data, new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMin + l), color, thickness, cap);
        }

        private static void AddGridCells(MeshData data, Rect rect, Color fill, Color top, Color bottom, Color stroke, MazeVectorShadingMode shading, MazeVectorStyle gradientStyle, bool fillEnabled, bool strokeEnabled, float strokeThickness, int columns, int rows, float gap)
        {
            var cellWidth = (rect.width - gap * (columns - 1)) / columns;
            var cellHeight = (rect.height - gap * (rows - 1)) / rows;
            if (cellWidth <= 0f || cellHeight <= 0f)
            {
                return;
            }
            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < columns; x++)
                {
                    var cell = new Rect(rect.xMin + x * (cellWidth + gap), rect.yMin + y * (cellHeight + gap), cellWidth, cellHeight);
                    if (fillEnabled && IsGradient(shading) && top.a > 0f && bottom.a > 0f)
                    {
                        AddGradientRect(data, cell, top, bottom, shading, gradientStyle);
                    }
                    else
                    {
                        AddRect(data, cell, fill, fillEnabled);
                    }
                    AddRectOutline(data, cell, stroke, strokeThickness, strokeEnabled, MazeVectorStrokeCap.Butt, MazeVectorStrokeJoin.Bevel, gradientStyle);
                }
            }
        }

        private static void AddVertex(MeshData data, Vector2 point, Color color)
        {
            data.vertices.Add(new Vector3(point.x, point.y, 0f));
            data.colors.Add(color);
            data.uvs.Add(Vector2.zero);
        }

        private static bool IsGradient(MazeVectorShadingMode shading)
        {
            return shading == MazeVectorShadingMode.VerticalGradient
                || shading == MazeVectorShadingMode.HorizontalGradient
                || shading == MazeVectorShadingMode.DiagonalGradient
                || shading == MazeVectorShadingMode.InverseDiagonalGradient
                || shading == MazeVectorShadingMode.AmorphousGradient;
        }

        private static Color GradientColor(Vector2 point, Vector2 min, Vector2 max, Color top, Color bottom, MazeVectorShadingMode shading, MazeVectorStyle style = null)
        {
            var normalized = new Vector2(
                Mathf.InverseLerp(min.x, max.x, point.x),
                Mathf.InverseLerp(min.y, max.y, point.y));
            var t = shading switch
            {
                MazeVectorShadingMode.HorizontalGradient => normalized.x,
                MazeVectorShadingMode.DiagonalGradient => (normalized.x + normalized.y) * 0.5f,
                MazeVectorShadingMode.InverseDiagonalGradient => (normalized.x + (1f - normalized.y)) * 0.5f,
                MazeVectorShadingMode.AmorphousGradient => AmorphousSample(normalized, style),
                _ => normalized.y
            };
            return Color.Lerp(bottom, top, Mathf.Clamp01(t));
        }

        private static float AmorphousSample(Vector2 normalized, MazeVectorStyle style)
        {
            return MazeVectorColorField.Sample(
                normalized,
                style != null ? style.amorphousScale : 1.35f,
                style != null ? style.amorphousSeed : 0f,
                style != null ? style.amorphousOffset : Vector2.zero,
                style != null ? style.amorphousSharpness : 0.8f);
        }

        private static bool HasStrokePatches(MazeVectorStyle style)
        {
            return style != null && style.strokePatchStrength > 0.001f;
        }

        private static bool HasStrokeGradient(MazeVectorStyle style)
        {
            return style != null
                && style.strokeGradientMode != MazeVectorStrokeGradientMode.None
                && ((style.strokeGradientMode == MazeVectorStrokeGradientMode.AlongPath
                        && style.strokeGradientStops != null
                        && style.strokeGradientStops.Length >= 2)
                    || (style.strokeGradientStrength > 0.001f
                        && style.strokeGradientColor.a > 0.001f));
        }

        private static bool HasStrokeVariation(MazeVectorStyle style)
        {
            return HasStrokePatches(style) || HasStrokeGradient(style);
        }

        private static Color StrokeVariationColor(
            Color baseColor,
            Vector2 point,
            Rect bounds,
            float pathPosition,
            bool closed,
            MazeVectorStyle style)
        {
            var varied = HasStrokePatches(style)
                ? StrokePatchColor(baseColor, point, bounds, style)
                : baseColor;
            return HasStrokeGradient(style)
                ? StrokeGradientColor(varied, point, bounds, pathPosition, closed, style)
                : varied;
        }

        private static Color StrokeGradientColor(
            Color baseColor,
            Vector2 point,
            Rect bounds,
            float pathPosition,
            bool closed,
            MazeVectorStyle style)
        {
            if (style.strokeGradientMode == MazeVectorStrokeGradientMode.AlongPath
                && style.strokeGradientStops != null
                && style.strokeGradientStops.Length >= 2)
            {
                return StrokeGradientStopsColor(pathPosition, closed, style.strokeGradientStops);
            }

            var width = Mathf.Max(0.02f, style.strokeGradientWidth);
            float distance;
            if (style.strokeGradientMode == MazeVectorStrokeGradientMode.Spatial)
            {
                var normalized = new Vector2(
                    Mathf.InverseLerp(bounds.xMin, bounds.xMax, point.x),
                    Mathf.InverseLerp(bounds.yMin, bounds.yMax, point.y));
                distance = Vector2.Distance(normalized, style.strokeGradientAnchor);
            }
            else
            {
                distance = Mathf.Abs(pathPosition - Mathf.Repeat(style.strokeGradientCenter, 1f));
                if (closed)
                {
                    distance = Mathf.Min(distance, 1f - distance);
                }
            }

            var normalizedDistance = distance / width;
            var gaussian = Mathf.Exp(-0.5f * normalizedDistance * normalizedDistance);
            return Color.Lerp(
                baseColor,
                style.strokeGradientColor,
                Mathf.Clamp01(style.strokeGradientStrength) * gaussian);
        }

        private static Color StrokeGradientStopsColor(
            float pathPosition,
            bool closed,
            IReadOnlyList<MazeVectorStrokeGradientStop> stops)
        {
            var first = stops[0];
            var last = stops[stops.Count - 1];
            var position = closed ? Mathf.Repeat(pathPosition, 1f) : Mathf.Clamp01(pathPosition);
            var firstPosition = Mathf.Clamp01(first.position);
            var previousPosition = firstPosition;
            var previousColor = first.color;

            if (closed && position < firstPosition)
            {
                return LerpGradientStops(
                    last.color,
                    first.color,
                    Mathf.Clamp01(last.position) - 1f,
                    firstPosition,
                    position);
            }

            for (var i = 1; i < stops.Count; i++)
            {
                var stop = stops[i];
                var nextPosition = Mathf.Max(previousPosition, Mathf.Clamp01(stop.position));
                if (position <= nextPosition)
                {
                    return LerpGradientStops(
                        previousColor,
                        stop.color,
                        previousPosition,
                        nextPosition,
                        position);
                }

                previousPosition = nextPosition;
                previousColor = stop.color;
            }

            return closed
                ? LerpGradientStops(
                    previousColor,
                    first.color,
                    previousPosition,
                    firstPosition + 1f,
                    position)
                : last.color;
        }

        private static Color LerpGradientStops(
            Color colorA,
            Color colorB,
            float positionA,
            float positionB,
            float position)
        {
            var span = positionB - positionA;
            if (span <= 0.0001f)
            {
                return colorB;
            }
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((position - positionA) / span));
            return Color.Lerp(colorA, colorB, t);
        }

        private static Color StrokePatchColor(Color baseColor, Vector2 point, Rect bounds, MazeVectorStyle style)
        {
            var normalized = new Vector2(
                Mathf.InverseLerp(bounds.xMin, bounds.xMax, point.x),
                Mathf.InverseLerp(bounds.yMin, bounds.yMax, point.y));
            var sample = MazeVectorColorField.Sample(
                normalized,
                style.strokePatchScale,
                style.strokePatchSeed,
                style.strokePatchOffset,
                style.strokePatchSharpness);
            return MazeVectorColorField.Patch(baseColor, style.strokePatchColor, style.strokePatchStrength, sample);
        }

        private static Rect BoundsOf(IReadOnlyList<Vector2> points)
        {
            var min = points[0];
            var max = points[0];
            for (var i = 1; i < points.Count; i++)
            {
                min = Vector2.Min(min, points[i]);
                max = Vector2.Max(max, points[i]);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
