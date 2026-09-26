using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;
using Unity.VectorGraphics;

namespace Maze
{
    public static class MazeSvgImporter
    {
        public const int CompileSchemaVersion = 3;
        private static readonly Regex NumberRegex = new(@"[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?", RegexOptions.Compiled);
        private static readonly Regex PathTokenRegex = new(@"[AaCcHhLlMmQqSsTtVvZz]|[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?", RegexOptions.Compiled);

        private sealed class ImportContext
        {
            public readonly MazeSvgImportSettings settings;
            public readonly List<MazeVectorCompiledShape> shapes = new();
            public readonly List<string> warnings = new();
            public int unmappedPaints;
            public Vector4 viewBox;
            public Vector2 sourceSize;

            public ImportContext(MazeSvgImportSettings settings)
            {
                this.settings = settings ?? MazeSvgImportSettings.Default;
            }
        }

        private struct SvgStyle
        {
            public bool hasFill;
            public Color fill;
            public bool hasStroke;
            public Color stroke;
            public float strokeWidth;
            public MazeVectorFillRule fillRule;
        }

        private struct SvgTransform
        {
            public float a;
            public float b;
            public float c;
            public float d;
            public float e;
            public float f;

            public static SvgTransform Identity => new() { a = 1f, d = 1f };

            public Vector2 TransformPoint(Vector2 p)
            {
                return new Vector2(a * p.x + c * p.y + e, b * p.x + d * p.y + f);
            }

            public static SvgTransform operator *(SvgTransform left, SvgTransform right)
            {
                return new SvgTransform
                {
                    a = left.a * right.a + left.c * right.b,
                    b = left.b * right.a + left.d * right.b,
                    c = left.a * right.c + left.c * right.d,
                    d = left.b * right.c + left.d * right.d,
                    e = left.a * right.e + left.c * right.f + left.e,
                    f = left.b * right.e + left.d * right.f + left.f
                };
            }
        }

        private sealed class SvgSubpath
        {
            public readonly List<Vector2> points = new();
            public bool closed;
        }

        public static MazeVectorAsset CompileToAsset(string svgText, string sourcePath = null, MazeSvgImportSettings settings = null)
        {
            var asset = ScriptableObject.CreateInstance<MazeVectorAsset>();
            var result = Compile(svgText, sourcePath, settings);
            settings ??= MazeSvgImportSettings.Default;
            asset.SetCompiledData(
                sourcePath,
                HashText(svgText),
                result.sourceSize,
                result.viewBox,
                result.shapes,
                result.report,
                CompileSchemaVersion,
                HashText(settings.StableSignature()));
            return asset;
        }

        public static string HashText(string text)
        {
            return Hash(text);
        }

        public static (Vector2 sourceSize, Vector4 viewBox, MazeVectorCompiledShape[] shapes, string report) Compile(string svgText, string sourcePath = null, MazeSvgImportSettings settings = null)
        {
            settings ??= MazeSvgImportSettings.Default;
            if (string.IsNullOrWhiteSpace(svgText))
            {
                return (Vector2.zero, Vector4.zero, Array.Empty<MazeVectorCompiledShape>(), "empty svg");
            }

            var xml = new XmlDocument { XmlResolver = null };
            xml.LoadXml(svgText);
            var root = xml.DocumentElement;
            if (root == null || StripNs(root.Name) != "svg")
            {
                return (Vector2.zero, Vector4.zero, Array.Empty<MazeVectorCompiledShape>(), "missing svg root");
            }

            var viewBox = ReadViewBox(root, out var sourceSize);
            if (settings.useUnityVectorGraphicsTessellator)
            {
                var unityResult = TryCompileWithUnityVectorGraphics(svgText, sourcePath, settings, sourceSize, viewBox);
                if (unityResult.shapes.Length > 0)
                {
                    return unityResult;
                }
                if (!settings.allowLegacyFallback)
                {
                    return unityResult;
                }
            }

            var context = new ImportContext(settings)
            {
                viewBox = viewBox,
                sourceSize = sourceSize
            };
            Walk(context, root, SvgStyleDefault(), SvgTransform.Identity);

            var report = new StringBuilder();
            report.Append("source=").Append(sourcePath ?? string.Empty)
                .Append(" mode=legacy-earclip")
                .Append(" schema=").Append(CompileSchemaVersion)
                .Append(" shapes=").Append(context.shapes.Count)
                .Append(" paintMode=").Append(settings.paintMode)
                .Append(" unmappedPaints=").Append(context.unmappedPaints)
                .Append(" warnings=").Append(context.warnings.Count);
            for (var i = 0; i < context.warnings.Count; i++)
            {
                report.Append('\n').Append(context.warnings[i]);
            }

            var legacyShapes = context.shapes.ToArray();
            PopulateStableUvs(legacyShapes, context.viewBox);
            return (context.sourceSize, context.viewBox, legacyShapes, report.ToString());
        }

        private static (Vector2 sourceSize, Vector4 viewBox, MazeVectorCompiledShape[] shapes, string report) TryCompileWithUnityVectorGraphics(string svgText, string sourcePath, MazeSvgImportSettings settings, Vector2 sourceSize, Vector4 viewBox)
        {
            try
            {
                using var reader = new System.IO.StringReader(svgText);
                var sceneInfo = SVGParser.ImportSVG(reader, settings.svgDpi, settings.svgPixelsPerUnit, Mathf.CeilToInt(sourceSize.x), Mathf.CeilToInt(sourceSize.y), settings.clipViewport);
                var options = new VectorUtils.TessellationOptions
                {
                    StepDistance = settings.stepDistance,
                    MaxCordDeviation = settings.maxCordDeviation,
                    MaxTanAngleDeviation = settings.maxTanAngleDeviation,
                    SamplingStepSize = settings.samplingStepSize
                };
                var geometries = VectorUtils.TessellateScene(sceneInfo.Scene, options, sceneInfo.NodeOpacity);
                var shapes = new List<MazeVectorCompiledShape>(geometries.Count);
                var geometryBounds = new Bounds(Vector3.zero, Vector3.zero);
                var rawBounds = new Bounds(Vector3.zero, Vector3.zero);
                var hasBounds = false;
                var hasRawBounds = false;
                var unmappedPaints = 0;
                var warnings = new List<string>();

                for (var g = 0; g < geometries.Count; g++)
                {
                    var geometry = geometries[g];
                    if (geometry.Vertices == null || geometry.Indices == null || geometry.Vertices.Length < 3 || geometry.Indices.Length < 3)
                    {
                        continue;
                    }

                    var transformedVertices = TransformGeometryVertices(geometry.Vertices, geometry.WorldTransform);
                    AccumulateRawBounds(transformedVertices, ref rawBounds, ref hasRawBounds);
                    BuildNormalizedGeometry(transformedVertices, geometry.Indices, viewBox, out var vertices, out var triangles, out var localBounds);
                    if (vertices.Length < 3 || triangles.Length < 3)
                    {
                        continue;
                    }
                    if (!hasBounds)
                    {
                        geometryBounds = localBounds;
                        hasBounds = true;
                    }
                    else
                    {
                        geometryBounds.Encapsulate(localBounds.min);
                        geometryBounds.Encapsulate(localBounds.max);
                    }

                    shapes.Add(new MazeVectorCompiledShape
                    {
                        name = "svg geometry " + g.ToString(CultureInfo.InvariantCulture),
                        sourceColor = geometry.Color,
                        role = ClassifyRole(geometry.Color, settings, warnings, ref unmappedPaints),
                        vertices = vertices,
                        triangles = triangles
                    });
                }

                var report = new StringBuilder();
                report.Append("source=").Append(sourcePath ?? string.Empty)
                    .Append(" mode=unity-vectorgraphics")
                    .Append(" schema=").Append(CompileSchemaVersion)
                    .Append(" shapes=").Append(shapes.Count)
                    .Append(" rawGeometries=").Append(geometries.Count)
                    .Append(" paintMode=").Append(settings.paintMode)
                    .Append(" unmappedPaints=").Append(unmappedPaints)
                    .Append(" clipViewport=").Append(settings.clipViewport)
                    .Append(" viewport=").Append(sceneInfo.SceneViewport);
                if (hasRawBounds)
                {
                    report.Append(" rawBoundsMin=").Append(rawBounds.min)
                        .Append(" rawBoundsMax=").Append(rawBounds.max);
                }
                if (hasBounds)
                {
                    report.Append(" normalizedBoundsMin=").Append(geometryBounds.min)
                        .Append(" normalizedBoundsMax=").Append(geometryBounds.max);
                }
                for (var i = 0; i < warnings.Count; i++)
                {
                    report.Append('\n').Append(warnings[i]);
                }
                var compiledShapes = shapes.ToArray();
                PopulateStableUvs(compiledShapes, viewBox);
                return (sourceSize, viewBox, compiledShapes, report.ToString());
            }
            catch (Exception ex)
            {
                return (sourceSize, viewBox, Array.Empty<MazeVectorCompiledShape>(), "unity-vectorgraphics failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }


        private static Vector2[] TransformGeometryVertices(Vector2[] vertices, Matrix2D transform)
        {
            var result = new Vector2[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                result[i] = transform.MultiplyPoint(vertices[i]);
            }
            return result;
        }

        private static void PopulateStableUvs(MazeVectorCompiledShape[] shapes, Vector4 viewBox)
        {
            for (var shapeIndex = 0; shapes != null && shapeIndex < shapes.Length; shapeIndex++)
            {
                var shape = shapes[shapeIndex];
                if (shape?.vertices == null)
                {
                    continue;
                }
                shape.uvs = new Vector2[shape.vertices.Length];
                for (var vertexIndex = 0; vertexIndex < shape.vertices.Length; vertexIndex++)
                {
                    shape.uvs[vertexIndex] = MazeVectorPaintMesh.SvgUv(shape.vertices[vertexIndex], viewBox);
                }
            }
        }

        private static void BuildNormalizedGeometry(Vector2[] sourceVertices, ushort[] sourceIndices, Vector4 viewBox, out Vector2[] vertices, out int[] triangles, out Bounds localBounds)
        {
            var source = new List<Vector2>(sourceVertices.Length);
            source.AddRange(sourceVertices);
            vertices = NormalizeSvgSpaceVertices(source, viewBox, out localBounds);
            triangles = new int[sourceIndices.Length];
            for (var i = 0; i < sourceIndices.Length; i++)
            {
                triangles[i] = sourceIndices[i];
            }
        }

        private static void AccumulateRawBounds(Vector2[] vertices, ref Bounds bounds, ref bool hasBounds)
        {
            for (var i = 0; i < vertices.Length; i++)
            {
                var v = new Vector3(vertices[i].x, vertices[i].y, 0f);
                if (!hasBounds)
                {
                    bounds = new Bounds(v, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(v);
                }
            }
        }

        private static Vector2[] NormalizeSvgSpaceVertices(List<Vector2> sourceVertices, Vector4 viewBox, out Bounds localBounds)
        {
            var result = new Vector2[sourceVertices.Count];
            var cx = viewBox.x + viewBox.z * 0.5f;
            var cy = viewBox.y + viewBox.w * 0.5f;
            var scale = Mathf.Max(viewBox.z, viewBox.w);
            if (scale <= 0f) scale = 1f;

            localBounds = new Bounds(Vector3.zero, Vector3.zero);
            var hasBounds = false;
            for (var i = 0; i < sourceVertices.Count; i++)
            {
                var p = sourceVertices[i];
                var normalized = new Vector2((p.x - cx) / scale, -(p.y - cy) / scale);
                result[i] = normalized;
                var v3 = new Vector3(normalized.x, normalized.y, 0f);
                if (!hasBounds)
                {
                    localBounds = new Bounds(v3, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(v3);
                }
            }
            return result;
        }

        private static void Walk(ImportContext context, XmlNode node, SvgStyle inheritedStyle, SvgTransform inheritedTransform)
        {
            if (node == null)
            {
                return;
            }

            var tag = StripNs(node.Name);
            var style = MergeStyle(inheritedStyle, node);
            var transform = inheritedTransform * ParseTransform(Attr(node, "transform"));

            if (tag == "path")
            {
                AddPath(context, node, style, transform);
            }
            else if (tag == "rect")
            {
                AddPolygonElement(context, node, style, transform, RectPoints(node));
            }
            else if (tag == "circle")
            {
                AddPolygonElement(context, node, style, transform, EllipsePoints(ReadFloat(node, "cx"), ReadFloat(node, "cy"), ReadFloat(node, "r"), ReadFloat(node, "r"), context.settings.curveSegments * 3));
            }
            else if (tag == "ellipse")
            {
                AddPolygonElement(context, node, style, transform, EllipsePoints(ReadFloat(node, "cx"), ReadFloat(node, "cy"), ReadFloat(node, "rx"), ReadFloat(node, "ry"), context.settings.curveSegments * 3));
            }
            else if (tag == "polygon" || tag == "polyline")
            {
                var points = ParsePointList(Attr(node, "points"));
                AddPolygonElement(context, node, style, transform, points, tag == "polygon");
            }
            else if (tag is "text" or "image" or "filter" or "mask" or "clipPath" or "linearGradient" or "radialGradient" or "pattern" or "use")
            {
                Warn(context, "unsupported <" + tag + "> in " + NodeLabel(node));
            }

            for (var i = 0; i < node.ChildNodes.Count; i++)
            {
                var child = node.ChildNodes[i];
                if (child.NodeType == XmlNodeType.Element)
                {
                    Walk(context, child, style, transform);
                }
            }
        }

        private static void AddPath(ImportContext context, XmlNode node, SvgStyle style, SvgTransform transform)
        {
            var d = Attr(node, "d");
            if (string.IsNullOrWhiteSpace(d))
            {
                return;
            }

            var subpaths = ParsePath(context, d);
            AddContours(context, NodeLabel(node), style, transform, subpaths);
        }

        private static void AddPolygonElement(ImportContext context, XmlNode node, SvgStyle style, SvgTransform transform, List<Vector2> points, bool closed = true)
        {
            if (points == null || points.Count < 2)
            {
                return;
            }
            var subpath = new SvgSubpath { closed = closed };
            subpath.points.AddRange(points);
            AddContours(context, NodeLabel(node), style, transform, new List<SvgSubpath> { subpath });
        }

        private static void AddContours(ImportContext context, string name, SvgStyle style, SvgTransform transform, List<SvgSubpath> subpaths)
        {
            if (subpaths == null || subpaths.Count == 0)
            {
                return;
            }

            if (style.hasFill)
            {
                var contours = new List<List<Vector2>>();
                for (var i = 0; i < subpaths.Count; i++)
                {
                    if (!subpaths[i].closed || subpaths[i].points.Count < 3)
                    {
                        continue;
                    }
                    contours.Add(NormalizeContour(context, subpaths[i].points, transform));
                }
                AddFilledContours(context, name, contours, style.fill, style.fillRule);
            }

            if (style.hasStroke && style.stroke.a > 0f && style.strokeWidth > 0f)
            {
                for (var i = 0; i < subpaths.Count; i++)
                {
                    var points = NormalizeContour(context, subpaths[i].points, transform);
                    AddStrokeContour(context, name + " stroke", points, subpaths[i].closed, style.stroke, style.strokeWidth / Mathf.Max(1f, Mathf.Max(context.viewBox.z, context.viewBox.w)));
                }
            }
        }

        private static List<Vector2> NormalizeContour(ImportContext context, List<Vector2> points, SvgTransform transform)
        {
            var result = new List<Vector2>(points.Count);
            var vb = context.viewBox;
            var cx = vb.x + vb.z * 0.5f;
            var cy = vb.y + vb.w * 0.5f;
            var scale = Mathf.Max(vb.z, vb.w);
            if (scale <= 0f) scale = 1f;
            for (var i = 0; i < points.Count; i++)
            {
                var p = transform.TransformPoint(points[i]);
                result.Add(new Vector2((p.x - cx) / scale, -(p.y - cy) / scale));
            }
            return CleanPoints(result, context.settings.pointEpsilon);
        }

        private static void AddFilledContours(ImportContext context, string name, List<List<Vector2>> contours, Color color, MazeVectorFillRule fillRule)
        {
            contours.RemoveAll(c => c == null || c.Count < 3 || Mathf.Abs(SignedArea(c)) < 0.000001f);
            if (contours.Count == 0)
            {
                return;
            }

            var groups = GroupContours(contours, fillRule);
            for (var i = 0; i < groups.Count; i++)
            {
                var merged = MergeHoles(groups[i].outer, groups[i].holes);
                var triangles = MazeVectorEarClip.Triangulate(merged);
                if (triangles.Count == 0)
                {
                    Warn(context, "triangulation failed for " + name);
                    continue;
                }
                context.shapes.Add(new MazeVectorCompiledShape
                {
                    name = name,
                    sourceColor = color,
                    role = ClassifyRole(color, context.settings, context.warnings, ref context.unmappedPaints),
                    vertices = merged.ToArray(),
                    triangles = triangles.ToArray()
                });
            }
        }

        private static void AddStrokeContour(ImportContext context, string name, List<Vector2> points, bool closed, Color color, float thickness)
        {
            if (points == null || points.Count < 2)
            {
                return;
            }
            var vertices = new List<Vector2>();
            var triangles = new List<int>();
            var half = Mathf.Max(0.0005f, thickness * 0.5f);
            for (var i = 0; i < points.Count - 1; i++)
            {
                AddStrokeSegment(points[i], points[i + 1], half, vertices, triangles);
            }
            if (closed)
            {
                AddStrokeSegment(points[^1], points[0], half, vertices, triangles);
            }
            context.shapes.Add(new MazeVectorCompiledShape
            {
                name = name,
                sourceColor = color,
                role = ClassifyRole(color, context.settings, context.warnings, ref context.unmappedPaints),
                vertices = vertices.ToArray(),
                triangles = triangles.ToArray()
            });
        }

        private static void AddStrokeSegment(Vector2 a, Vector2 b, float half, List<Vector2> vertices, List<int> triangles)
        {
            var delta = b - a;
            if (delta.sqrMagnitude <= 0.0000001f)
            {
                return;
            }
            var n = new Vector2(-delta.y, delta.x).normalized * half;
            var start = vertices.Count;
            vertices.Add(a - n);
            vertices.Add(a + n);
            vertices.Add(b + n);
            vertices.Add(b - n);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private sealed class ContourGroup
        {
            public List<Vector2> outer;
            public readonly List<List<Vector2>> holes = new();
        }

        private static List<ContourGroup> GroupContours(List<List<Vector2>> contours, MazeVectorFillRule fillRule)
        {
            var groups = new List<ContourGroup>();
            var depth = new int[contours.Count];
            for (var i = 0; i < contours.Count; i++)
            {
                var p = contours[i][0];
                for (var j = 0; j < contours.Count; j++)
                {
                    if (i != j && Mathf.Abs(SignedArea(contours[j])) > Mathf.Abs(SignedArea(contours[i])) && PointInPolygon(p, contours[j]))
                    {
                        depth[i]++;
                    }
                }
            }

            for (var i = 0; i < contours.Count; i++)
            {
                var isHole = fillRule == MazeVectorFillRule.EvenOdd ? depth[i] % 2 == 1 : depth[i] % 2 == 1;
                if (isHole)
                {
                    continue;
                }
                groups.Add(new ContourGroup { outer = contours[i] });
            }

            for (var i = 0; i < contours.Count; i++)
            {
                var isHole = fillRule == MazeVectorFillRule.EvenOdd ? depth[i] % 2 == 1 : depth[i] % 2 == 1;
                if (!isHole)
                {
                    continue;
                }
                var best = -1;
                var bestArea = float.PositiveInfinity;
                for (var g = 0; g < groups.Count; g++)
                {
                    var area = Mathf.Abs(SignedArea(groups[g].outer));
                    if (area < bestArea && PointInPolygon(contours[i][0], groups[g].outer))
                    {
                        best = g;
                        bestArea = area;
                    }
                }
                if (best >= 0)
                {
                    groups[best].holes.Add(contours[i]);
                }
            }
            return groups;
        }

        private static List<Vector2> MergeHoles(List<Vector2> outer, List<List<Vector2>> holes)
        {
            var merged = new List<Vector2>(outer);
            if (SignedArea(merged) < 0f)
            {
                merged.Reverse();
            }
            for (var h = 0; h < holes.Count; h++)
            {
                var hole = new List<Vector2>(holes[h]);
                if (SignedArea(hole) > 0f)
                {
                    hole.Reverse();
                }
                var hi = RightmostIndex(hole);
                var oi = VisibleBridgeIndex(merged, hole[hi], hole);
                var next = new List<Vector2>(merged.Count + hole.Count + 2);
                for (var i = 0; i <= oi; i++) next.Add(merged[i]);
                for (var i = 0; i < hole.Count; i++) next.Add(hole[(hi + i) % hole.Count]);
                next.Add(hole[hi]);
                next.Add(merged[oi]);
                for (var i = oi + 1; i < merged.Count; i++) next.Add(merged[i]);
                merged = CleanPoints(next, 0.000001f);
            }
            return merged;
        }

        private static int VisibleBridgeIndex(List<Vector2> outer, Vector2 holePoint, List<Vector2> hole)
        {
            var best = 0;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < outer.Count; i++)
            {
                var candidate = outer[i];
                var visible = true;
                for (var j = 0; j < outer.Count; j++)
                {
                    var a = outer[j];
                    var b = outer[(j + 1) % outer.Count];
                    if ((a - candidate).sqrMagnitude < 0.0000001f || (b - candidate).sqrMagnitude < 0.0000001f)
                    {
                        continue;
                    }
                    if (SegmentsIntersect(holePoint, candidate, a, b))
                    {
                        visible = false;
                        break;
                    }
                }
                if (visible)
                {
                    for (var j = 0; j < hole.Count; j++)
                    {
                        var a = hole[j];
                        var b = hole[(j + 1) % hole.Count];
                        if ((a - holePoint).sqrMagnitude < 0.0000001f || (b - holePoint).sqrMagnitude < 0.0000001f)
                        {
                            continue;
                        }
                        if (SegmentsIntersect(holePoint, candidate, a, b))
                        {
                            visible = false;
                            break;
                        }
                    }
                }
                if (!visible)
                {
                    continue;
                }
                var dist = (candidate - holePoint).sqrMagnitude;
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    best = i;
                }
            }
            return best;
        }

        private static int RightmostIndex(List<Vector2> points)
        {
            var best = 0;
            for (var i = 1; i < points.Count; i++)
            {
                if (points[i].x > points[best].x || (Mathf.Approximately(points[i].x, points[best].x) && points[i].y < points[best].y))
                {
                    best = i;
                }
            }
            return best;
        }

        private static List<SvgSubpath> ParsePath(ImportContext context, string d)
        {
            var tokens = PathTokenRegex.Matches(d);
            var subpaths = new List<SvgSubpath>();
            SvgSubpath currentPath = null;
            var i = 0;
            var cmd = '\0';
            var current = Vector2.zero;
            var start = Vector2.zero;
            var lastCubic = Vector2.zero;
            var lastQuad = Vector2.zero;
            var hasLastCubic = false;
            var hasLastQuad = false;

            bool IsCommand()
            {
                return i < tokens.Count && tokens[i].Value.Length == 1 && char.IsLetter(tokens[i].Value[0]);
            }

            float Num()
            {
                if (i >= tokens.Count || IsCommand())
                {
                    throw new FormatException("expected path number");
                }
                return ParseFloat(tokens[i++].Value);
            }

            Vector2 Point(bool relative)
            {
                var p = new Vector2(Num(), Num());
                return relative ? current + p : p;
            }

            void EnsurePath()
            {
                if (currentPath == null)
                {
                    currentPath = new SvgSubpath();
                    subpaths.Add(currentPath);
                }
            }

            void AddPoint(Vector2 p)
            {
                EnsurePath();
                if (currentPath.points.Count == 0 || (currentPath.points[^1] - p).sqrMagnitude > context.settings.pointEpsilon * context.settings.pointEpsilon)
                {
                    currentPath.points.Add(p);
                }
            }

            while (i < tokens.Count)
            {
                if (IsCommand())
                {
                    cmd = tokens[i++].Value[0];
                }
                if (cmd == '\0')
                {
                    break;
                }

                var rel = char.IsLower(cmd);
                var c = char.ToUpperInvariant(cmd);
                try
                {
                    if (c == 'M')
                    {
                        current = Point(rel);
                        start = current;
                        currentPath = new SvgSubpath();
                        subpaths.Add(currentPath);
                        AddPoint(current);
                        cmd = rel ? 'l' : 'L';
                        hasLastCubic = hasLastQuad = false;
                    }
                    else if (c == 'L')
                    {
                        current = Point(rel);
                        AddPoint(current);
                        hasLastCubic = hasLastQuad = false;
                    }
                    else if (c == 'H')
                    {
                        var x = Num();
                        current = new Vector2(rel ? current.x + x : x, current.y);
                        AddPoint(current);
                        hasLastCubic = hasLastQuad = false;
                    }
                    else if (c == 'V')
                    {
                        var y = Num();
                        current = new Vector2(current.x, rel ? current.y + y : y);
                        AddPoint(current);
                        hasLastCubic = hasLastQuad = false;
                    }
                    else if (c == 'C')
                    {
                        var p0 = current;
                        var p1 = Point(rel);
                        var p2 = Point(rel);
                        var p3 = Point(rel);
                        AddCubic(context, AddPoint, p0, p1, p2, p3);
                        current = p3;
                        lastCubic = p2;
                        hasLastCubic = true;
                        hasLastQuad = false;
                    }
                    else if (c == 'S')
                    {
                        var p0 = current;
                        var p1 = hasLastCubic ? current + (current - lastCubic) : current;
                        var p2 = Point(rel);
                        var p3 = Point(rel);
                        AddCubic(context, AddPoint, p0, p1, p2, p3);
                        current = p3;
                        lastCubic = p2;
                        hasLastCubic = true;
                        hasLastQuad = false;
                    }
                    else if (c == 'Q')
                    {
                        var p0 = current;
                        var p1 = Point(rel);
                        var p2 = Point(rel);
                        AddQuad(context, AddPoint, p0, p1, p2);
                        current = p2;
                        lastQuad = p1;
                        hasLastQuad = true;
                        hasLastCubic = false;
                    }
                    else if (c == 'T')
                    {
                        var p0 = current;
                        var p1 = hasLastQuad ? current + (current - lastQuad) : current;
                        var p2 = Point(rel);
                        AddQuad(context, AddPoint, p0, p1, p2);
                        current = p2;
                        lastQuad = p1;
                        hasLastQuad = true;
                        hasLastCubic = false;
                    }
                    else if (c == 'A')
                    {
                        var p0 = current;
                        var rx = Num();
                        var ry = Num();
                        var angle = Num();
                        var large = !Mathf.Approximately(Num(), 0f);
                        var sweep = !Mathf.Approximately(Num(), 0f);
                        var p = Point(rel);
                        AddArc(context, AddPoint, p0, p, rx, ry, angle, large, sweep);
                        current = p;
                        hasLastCubic = hasLastQuad = false;
                    }
                    else if (c == 'Z')
                    {
                        EnsurePath();
                        currentPath.closed = true;
                        current = start;
                        AddPoint(start);
                        currentPath = null;
                        hasLastCubic = hasLastQuad = false;
                    }
                    else
                    {
                        Warn(context, "unsupported path command " + c);
                        break;
                    }
                }
                catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException)
                {
                    Warn(context, "malformed path data: " + ex.Message);
                    break;
                }
            }

            return subpaths;
        }

        private static void AddCubic(ImportContext context, Action<Vector2> add, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            var segments = Mathf.Max(2, context.settings.curveSegments);
            for (var s = 1; s <= segments; s++)
            {
                var t = s / (float)segments;
                var u = 1f - t;
                add(u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3);
            }
        }

        private static void AddQuad(ImportContext context, Action<Vector2> add, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            var segments = Mathf.Max(2, context.settings.curveSegments);
            for (var s = 1; s <= segments; s++)
            {
                var t = s / (float)segments;
                var u = 1f - t;
                add(u * u * p0 + 2f * u * t * p1 + t * t * p2);
            }
        }

        private static void AddArc(ImportContext context, Action<Vector2> add, Vector2 p0, Vector2 p1, float rx, float ry, float angleDegrees, bool largeArc, bool sweep)
        {
            if (rx <= 0f || ry <= 0f || (p1 - p0).sqrMagnitude <= 0.000001f)
            {
                add(p1);
                return;
            }

            var phi = angleDegrees * Mathf.Deg2Rad;
            var cosPhi = Mathf.Cos(phi);
            var sinPhi = Mathf.Sin(phi);
            var dx = (p0.x - p1.x) * 0.5f;
            var dy = (p0.y - p1.y) * 0.5f;
            var x1p = cosPhi * dx + sinPhi * dy;
            var y1p = -sinPhi * dx + cosPhi * dy;
            rx = Mathf.Abs(rx);
            ry = Mathf.Abs(ry);
            var lambda = x1p * x1p / (rx * rx) + y1p * y1p / (ry * ry);
            if (lambda > 1f)
            {
                var root = Mathf.Sqrt(lambda);
                rx *= root;
                ry *= root;
            }
            var sign = largeArc == sweep ? -1f : 1f;
            var numerator = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p;
            var denominator = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
            var coef = sign * Mathf.Sqrt(Mathf.Max(0f, numerator / Mathf.Max(0.000001f, denominator)));
            var cxp = coef * rx * y1p / ry;
            var cyp = coef * -ry * x1p / rx;
            var cx = cosPhi * cxp - sinPhi * cyp + (p0.x + p1.x) * 0.5f;
            var cy = sinPhi * cxp + cosPhi * cyp + (p0.y + p1.y) * 0.5f;

            var start = VectorAngle(new Vector2(1f, 0f), new Vector2((x1p - cxp) / rx, (y1p - cyp) / ry));
            var delta = VectorAngle(new Vector2((x1p - cxp) / rx, (y1p - cyp) / ry), new Vector2((-x1p - cxp) / rx, (-y1p - cyp) / ry));
            if (!sweep && delta > 0f) delta -= Mathf.PI * 2f;
            if (sweep && delta < 0f) delta += Mathf.PI * 2f;
            var segments = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(delta) / (Mathf.PI / 8f)));
            for (var i = 1; i <= segments; i++)
            {
                var theta = start + delta * (i / (float)segments);
                var x = cx + rx * Mathf.Cos(theta) * cosPhi - ry * Mathf.Sin(theta) * sinPhi;
                var y = cy + rx * Mathf.Cos(theta) * sinPhi + ry * Mathf.Sin(theta) * cosPhi;
                add(new Vector2(x, y));
            }
        }

        private static float VectorAngle(Vector2 u, Vector2 v)
        {
            var sign = u.x * v.y - u.y * v.x < 0f ? -1f : 1f;
            var dot = Mathf.Clamp(Vector2.Dot(u.normalized, v.normalized), -1f, 1f);
            return sign * Mathf.Acos(dot);
        }

        private static SvgStyle SvgStyleDefault()
        {
            return new SvgStyle
            {
                hasFill = true,
                fill = Color.black,
                hasStroke = false,
                stroke = Color.clear,
                strokeWidth = 1f,
                fillRule = MazeVectorFillRule.NonZero
            };
        }

        private static SvgStyle MergeStyle(SvgStyle inherited, XmlNode node)
        {
            var style = inherited;
            var inline = Attr(node, "style");
            if (!string.IsNullOrWhiteSpace(inline))
            {
                var chunks = inline.Split(';');
                for (var i = 0; i < chunks.Length; i++)
                {
                    var parts = chunks[i].Split(':');
                    if (parts.Length == 2)
                    {
                        ApplyStyle(ref style, parts[0].Trim(), parts[1].Trim());
                    }
                }
            }
            foreach (XmlAttribute attr in node.Attributes ?? EmptyAttrs())
            {
                ApplyStyle(ref style, attr.Name, attr.Value);
            }
            return style;
        }

        private static XmlAttributeCollection EmptyAttrs()
        {
            var doc = new XmlDocument();
            return doc.CreateElement("x").Attributes;
        }

        private static void ApplyStyle(ref SvgStyle style, string key, string value)
        {
            key = key.ToLowerInvariant();
            if (key == "fill")
            {
                if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
                {
                    style.hasFill = false;
                    return;
                }
                if (TryColor(value, out var color))
                {
                    style.hasFill = color.a > 0f;
                    style.fill = color;
                }
            }
            else if (key == "stroke")
            {
                if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
                {
                    style.hasStroke = false;
                    return;
                }
                if (TryColor(value, out var color))
                {
                    style.hasStroke = color.a > 0f;
                    style.stroke = color;
                }
            }
            else if (key == "stroke-width")
            {
                style.strokeWidth = Mathf.Max(0f, ParseFloat(value));
            }
            else if (key == "fill-rule")
            {
                style.fillRule = value.Trim().Equals("evenodd", StringComparison.OrdinalIgnoreCase) ? MazeVectorFillRule.EvenOdd : MazeVectorFillRule.NonZero;
            }
            else if (key == "opacity")
            {
                var opacity = Mathf.Clamp01(ParseFloat(value, 1f));
                style.fill.a *= opacity;
                style.stroke.a *= opacity;
            }
            else if (key == "fill-opacity")
            {
                style.fill.a *= Mathf.Clamp01(ParseFloat(value, 1f));
            }
            else if (key == "stroke-opacity")
            {
                style.stroke.a *= Mathf.Clamp01(ParseFloat(value, 1f));
            }
        }

        private static SvgTransform ParseTransform(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return SvgTransform.Identity;
            }
            var result = SvgTransform.Identity;
            var matches = Regex.Matches(value, @"([a-zA-Z]+)\s*\(([^)]*)\)");
            foreach (Match match in matches)
            {
                var name = match.Groups[1].Value.ToLowerInvariant();
                var nums = ParseNumbers(match.Groups[2].Value);
                SvgTransform next = SvgTransform.Identity;
                if (name == "translate")
                {
                    next.e = nums.Count > 0 ? nums[0] : 0f;
                    next.f = nums.Count > 1 ? nums[1] : 0f;
                }
                else if (name == "scale")
                {
                    next.a = nums.Count > 0 ? nums[0] : 1f;
                    next.d = nums.Count > 1 ? nums[1] : next.a;
                }
                else if (name == "rotate")
                {
                    var degrees = nums.Count > 0 ? nums[0] : 0f;
                    var r = degrees * Mathf.Deg2Rad;
                    var cos = Mathf.Cos(r);
                    var sin = Mathf.Sin(r);
                    next = new SvgTransform { a = cos, b = sin, c = -sin, d = cos };
                    if (nums.Count >= 3)
                    {
                        var toOrigin = SvgTransform.Identity;
                        toOrigin.e = -nums[1];
                        toOrigin.f = -nums[2];
                        var back = SvgTransform.Identity;
                        back.e = nums[1];
                        back.f = nums[2];
                        next = back * next * toOrigin;
                    }
                }
                else if (name == "matrix" && nums.Count >= 6)
                {
                    next = new SvgTransform { a = nums[0], b = nums[1], c = nums[2], d = nums[3], e = nums[4], f = nums[5] };
                }
                else if (name == "skewx" && nums.Count > 0)
                {
                    next = SvgTransform.Identity;
                    next.c = Mathf.Tan(nums[0] * Mathf.Deg2Rad);
                }
                else if (name == "skewy" && nums.Count > 0)
                {
                    next = SvgTransform.Identity;
                    next.b = Mathf.Tan(nums[0] * Mathf.Deg2Rad);
                }
                result = result * next;
            }
            return result;
        }

        private static Vector4 ReadViewBox(XmlNode root, out Vector2 sourceSize)
        {
            var width = ReadLength(Attr(root, "width"), 100f);
            var height = ReadLength(Attr(root, "height"), 100f);
            sourceSize = new Vector2(width, height);
            var viewBox = Attr(root, "viewBox");
            var nums = ParseNumbers(viewBox);
            if (nums.Count >= 4)
            {
                return new Vector4(nums[0], nums[1], Mathf.Max(0.001f, nums[2]), Mathf.Max(0.001f, nums[3]));
            }
            return new Vector4(0f, 0f, Mathf.Max(0.001f, width), Mathf.Max(0.001f, height));
        }

        private static List<Vector2> RectPoints(XmlNode node)
        {
            var x = ReadFloat(node, "x");
            var y = ReadFloat(node, "y");
            var w = ReadFloat(node, "width");
            var h = ReadFloat(node, "height");
            return new List<Vector2> { new(x, y), new(x + w, y), new(x + w, y + h), new(x, y + h) };
        }

        private static List<Vector2> EllipsePoints(float cx, float cy, float rx, float ry, int segments)
        {
            var points = new List<Vector2>();
            segments = Mathf.Max(12, segments);
            for (var i = 0; i < segments; i++)
            {
                var t = i / (float)segments * Mathf.PI * 2f;
                points.Add(new Vector2(cx + Mathf.Cos(t) * rx, cy + Mathf.Sin(t) * ry));
            }
            return points;
        }

        private static List<Vector2> ParsePointList(string value)
        {
            var nums = ParseNumbers(value);
            var points = new List<Vector2>();
            for (var i = 0; i + 1 < nums.Count; i += 2)
            {
                points.Add(new Vector2(nums[i], nums[i + 1]));
            }
            return points;
        }

        private static List<float> ParseNumbers(string value)
        {
            var result = new List<float>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return result;
            }
            foreach (Match match in NumberRegex.Matches(value))
            {
                result.Add(ParseFloat(match.Value));
            }
            return result;
        }

        private static float ReadFloat(XmlNode node, string attr, float fallback = 0f)
        {
            return ParseFloat(Attr(node, attr), fallback);
        }

        private static float ReadLength(string value, float fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }
            return ParseFloat(Regex.Match(value, @"[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?").Value, fallback);
        }

        private static float ParseFloat(string value, float fallback = 0f)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        }

        private static string Attr(XmlNode node, string name)
        {
            return node?.Attributes?[name]?.Value ?? string.Empty;
        }

        private static string StripNs(string name)
        {
            var index = name.LastIndexOf(':');
            return index >= 0 ? name[(index + 1)..] : name;
        }

        private static string NodeLabel(XmlNode node)
        {
            var id = Attr(node, "id");
            return string.IsNullOrWhiteSpace(id) ? StripNs(node.Name) : id;
        }

        private static void Warn(ImportContext context, string warning)
        {
            if (context.settings.warnUnsupported)
            {
                context.warnings.Add(warning);
            }
        }

        private static bool TryColor(string value, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            value = value.Trim();
            if (value.StartsWith("#", StringComparison.Ordinal))
            {
                var hex = value[1..];
                if (hex.Length == 3)
                {
                    hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
                }
                if (hex.Length == 6 || hex.Length == 8)
                {
                    var r = int.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f;
                    var g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f;
                    var b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f;
                    var a = hex.Length == 8 ? int.Parse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f : 1f;
                    color = new Color(r, g, b, a);
                    return true;
                }
            }
            if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                var nums = ParseNumbers(value);
                if (nums.Count >= 3)
                {
                    color = new Color(nums[0] / 255f, nums[1] / 255f, nums[2] / 255f, nums.Count >= 4 ? nums[3] : 1f);
                    return true;
                }
            }
            return NamedColor(value, out color);
        }

        private static bool NamedColor(string value, out Color color)
        {
            color = value.ToLowerInvariant() switch
            {
                "black" => Color.black,
                "white" => Color.white,
                "red" => Color.red,
                "green" => Color.green,
                "blue" => Color.blue,
                "yellow" => Color.yellow,
                "cyan" => Color.cyan,
                "magenta" => Color.magenta,
                "transparent" => Color.clear,
                _ => default
            };
            return value.Equals("black", StringComparison.OrdinalIgnoreCase)
                || value.Equals("white", StringComparison.OrdinalIgnoreCase)
                || value.Equals("red", StringComparison.OrdinalIgnoreCase)
                || value.Equals("green", StringComparison.OrdinalIgnoreCase)
                || value.Equals("blue", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yellow", StringComparison.OrdinalIgnoreCase)
                || value.Equals("cyan", StringComparison.OrdinalIgnoreCase)
                || value.Equals("magenta", StringComparison.OrdinalIgnoreCase)
                || value.Equals("transparent", StringComparison.OrdinalIgnoreCase);
        }

        private static MazeVectorPaintRole ClassifyRole(Color color, MazeSvgImportSettings settings, List<string> warnings, ref int unmappedPaints)
        {
            if (settings.paintMode == MazeSvgPaintMode.PreserveSource)
            {
                return MazeVectorPaintRole.PreserveSource;
            }

            if (settings.paintMode == MazeSvgPaintMode.LightDark)
            {
                var luminance = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
                return luminance >= settings.lightRoleThreshold
                    ? MazeVectorPaintRole.SourceLight
                    : MazeVectorPaintRole.SourceDark;
            }

            var source = (Color32)color;
            for (var i = 0; settings.paintTokens != null && i < settings.paintTokens.Length; i++)
            {
                var token = settings.paintTokens[i];
                if (token == null || !PaintMatches(source, token.source, settings.paintTolerance))
                {
                    continue;
                }
                return token.role;
            }

            unmappedPaints++;
            var message = $"unmapped paint #{source.r:X2}{source.g:X2}{source.b:X2}{source.a:X2}; preserving source";
            if (settings.strictPaintTokens)
            {
                throw new InvalidOperationException(message);
            }
            if (warnings != null && !warnings.Contains(message))
            {
                warnings.Add(message);
            }
            return MazeVectorPaintRole.PreserveSource;
        }

        private static bool PaintMatches(Color32 left, Color32 right, int tolerance)
        {
            tolerance = Mathf.Clamp(tolerance, 0, 8);
            return Mathf.Abs(left.r - right.r) <= tolerance
                && Mathf.Abs(left.g - right.g) <= tolerance
                && Mathf.Abs(left.b - right.b) <= tolerance;
        }

        private static List<Vector2> CleanPoints(List<Vector2> points, float epsilon)
        {
            var result = new List<Vector2>();
            if (points == null)
            {
                return result;
            }
            var eps2 = epsilon * epsilon;
            for (var i = 0; i < points.Count; i++)
            {
                if (result.Count == 0 || (result[^1] - points[i]).sqrMagnitude > eps2)
                {
                    result.Add(points[i]);
                }
            }
            if (result.Count > 1 && (result[0] - result[^1]).sqrMagnitude <= eps2)
            {
                result.RemoveAt(result.Count - 1);
            }
            return result;
        }

        private static float SignedArea(List<Vector2> points)
        {
            var area = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                area += a.x * b.y - b.x * a.y;
            }
            return area * 0.5f;
        }

        private static bool PointInPolygon(Vector2 p, List<Vector2> poly)
        {
            var inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / Mathf.Max(0.0000001f, poly[j].y - poly[i].y) + poly[i].x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var o1 = Orient(a, b, c);
            var o2 = Orient(a, b, d);
            var o3 = Orient(c, d, a);
            var o4 = Orient(c, d, b);
            return o1 * o2 < 0f && o3 * o4 < 0f;
        }

        private static float Orient(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        private static string Hash(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            var sb = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }

    internal static class MazeVectorEarClip
    {
        public static List<int> Triangulate(List<Vector2> input)
        {
            var indices = new List<int>();
            if (input == null || input.Count < 3)
            {
                return indices;
            }

            var verts = new List<int>(input.Count);
            for (var i = 0; i < input.Count; i++)
            {
                verts.Add(i);
            }
            if (SignedArea(input, verts) < 0f)
            {
                verts.Reverse();
            }

            var guard = 0;
            while (verts.Count > 3 && guard++ < input.Count * input.Count)
            {
                var cut = false;
                for (var i = 0; i < verts.Count; i++)
                {
                    var prev = verts[(i + verts.Count - 1) % verts.Count];
                    var curr = verts[i];
                    var next = verts[(i + 1) % verts.Count];
                    if (!IsConvex(input[prev], input[curr], input[next]))
                    {
                        continue;
                    }
                    var contains = false;
                    for (var j = 0; j < verts.Count; j++)
                    {
                        var idx = verts[j];
                        if (idx == prev || idx == curr || idx == next)
                        {
                            continue;
                        }
                        if (PointInTriangle(input[idx], input[prev], input[curr], input[next]))
                        {
                            contains = true;
                            break;
                        }
                    }
                    if (contains)
                    {
                        continue;
                    }
                    indices.Add(prev);
                    indices.Add(curr);
                    indices.Add(next);
                    verts.RemoveAt(i);
                    cut = true;
                    break;
                }
                if (!cut)
                {
                    break;
                }
            }

            if (verts.Count == 3)
            {
                indices.Add(verts[0]);
                indices.Add(verts[1]);
                indices.Add(verts[2]);
            }
            return indices;
        }

        private static bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
        {
            return ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) > 0.0000001f;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var ab = Sign(p, a, b);
            var bc = Sign(p, b, c);
            var ca = Sign(p, c, a);
            var hasNeg = ab < 0f || bc < 0f || ca < 0f;
            var hasPos = ab > 0f || bc > 0f || ca > 0f;
            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        private static float SignedArea(List<Vector2> points, List<int> indices)
        {
            var area = 0f;
            for (var i = 0; i < indices.Count; i++)
            {
                var a = points[indices[i]];
                var b = points[indices[(i + 1) % indices.Count]];
                area += a.x * b.y - b.x * a.y;
            }
            return area * 0.5f;
        }
    }
}
