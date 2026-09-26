using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Maze
{
    public enum MazeVectorPaintSourceMode
    {
        None = 0,
        Rgba = 1,
        AlphaMask = 2,
        LuminanceMask = 3
    }

    [Serializable]
    public struct MazeVectorUvTransform
    {
        public Vector2 scale;
        public Vector2 offset;
        public float rotationDegrees;

        public static MazeVectorUvTransform Identity => new()
        {
            scale = Vector2.one,
            offset = Vector2.zero,
            rotationDegrees = 0f
        };

        public readonly Vector2 Apply(Vector2 uv)
        {
            var actualScale = scale == Vector2.zero ? Vector2.one : scale;
            var point = Vector2.Scale(uv - Vector2.one * 0.5f, actualScale);
            var radians = rotationDegrees * Mathf.Deg2Rad;
            var sine = Mathf.Sin(radians);
            var cosine = Mathf.Cos(radians);
            point = new Vector2(
                point.x * cosine - point.y * sine,
                point.x * sine + point.y * cosine);
            return point + Vector2.one * 0.5f + offset;
        }
    }

    [Serializable]
    public sealed class MazeVectorPaint
    {
        public MazeVectorPaintSourceMode sourceMode;
        public Texture2D source;
        public Texture2D controlMap;
        public MazeVectorUvTransform uv = MazeVectorUvTransform.Identity;
        [Min(0f)] public float uvDisplacement;
        public Vector2 geometryDisplacement;
        [Range(0, 2)] public int geometrySubdivisions;
        [Range(0f, 2f)] public float detailStrength;
        [Range(0f, 1f)] public float coverageStrength;
        public Vector2 controlScroll;

        public bool HasSource => sourceMode != MazeVectorPaintSourceMode.None && source != null;
        public bool HasControlMap => controlMap != null;
        public bool HasGeometryDisplacement => HasControlMap && geometryDisplacement.sqrMagnitude > 0.0000001f;
        public bool RequiresShader => HasSource
            || (HasControlMap && (uvDisplacement > 0.0001f || detailStrength > 0.0001f || coverageStrength > 0.0001f || HasGeometryDisplacement));
        public float GeometryPadding => HasGeometryDisplacement
            ? Mathf.Max(Mathf.Abs(geometryDisplacement.x), Mathf.Abs(geometryDisplacement.y))
            : 0f;

        public MazeVectorPaint Clone()
        {
            return new MazeVectorPaint
            {
                sourceMode = sourceMode,
                source = source,
                controlMap = controlMap,
                uv = uv,
                uvDisplacement = Mathf.Max(0f, uvDisplacement),
                geometryDisplacement = geometryDisplacement,
                geometrySubdivisions = Mathf.Clamp(geometrySubdivisions, 0, 2),
                detailStrength = Mathf.Clamp(detailStrength, 0f, 2f),
                coverageStrength = Mathf.Clamp01(coverageStrength),
                controlScroll = controlScroll
            };
        }

        public static MazeVectorPaint Rgba(Texture2D texture)
        {
            return new MazeVectorPaint
            {
                sourceMode = MazeVectorPaintSourceMode.Rgba,
                source = texture,
                uv = MazeVectorUvTransform.Identity
            };
        }

        public static MazeVectorPaint Mask(Texture2D texture, bool luminance = false)
        {
            return new MazeVectorPaint
            {
                sourceMode = luminance ? MazeVectorPaintSourceMode.LuminanceMask : MazeVectorPaintSourceMode.AlphaMask,
                source = texture,
                uv = MazeVectorUvTransform.Identity
            };
        }

        public static bool MaterialCompatible(MazeVectorPaint left, MazeVectorPaint right)
        {
            if (left == null || !left.RequiresShader || right == null || !right.RequiresShader)
            {
                return true;
            }

            var leftSource = left.HasSource ? left.source : null;
            var rightSource = right.HasSource ? right.source : null;
            if (leftSource != null && rightSource != null && leftSource != rightSource)
            {
                return false;
            }
            if (left.controlMap != null && right.controlMap != null && left.controlMap != right.controlMap)
            {
                return false;
            }
            return left.controlMap == null
                || right.controlMap == null
                || left.controlScroll == right.controlScroll;
        }
    }

    public static class MazeVectorPaintMesh
    {
        public sealed class MeshData
        {
            public readonly List<Vector3> vertices = new(128);
            public readonly List<int> triangles = new(256);
            public readonly List<Color32> colors = new(128);
            public readonly List<Vector4> uv0 = new(128);
            public readonly List<Vector4> uv1 = new(128);

            public void Clear()
            {
                vertices.Clear();
                triangles.Clear();
                colors.Clear();
                uv0.Clear();
                uv1.Clear();
            }
        }

        private readonly struct Vertex
        {
            public readonly Vector3 position;
            public readonly Color32 color;
            public readonly Vector2 uv;

            public Vertex(Vector3 position, Color32 color, Vector2 uv)
            {
                this.position = position;
                this.color = color;
                this.uv = uv;
            }

            public static Vertex Midpoint(Vertex left, Vertex right)
            {
                return new Vertex(
                    (left.position + right.position) * 0.5f,
                    (Color32)Color.Lerp(left.color, right.color, 0.5f),
                    (left.uv + right.uv) * 0.5f);
            }
        }

        public static void Build(
            MazeVectorBuilder.MeshData source,
            Rect carrierRect,
            MazeVectorPaint paint,
            MeshData output,
            bool useSourceUvs = false)
        {
            output.Clear();
            if (source == null || source.vertices.Count == 0)
            {
                return;
            }

            var subdivisions = paint != null && paint.HasGeometryDisplacement
                ? Mathf.Clamp(paint.geometrySubdivisions, 0, 2)
                : 0;
            if (subdivisions == 0)
            {
                for (var i = 0; i < source.vertices.Count; i++)
                {
                    var stableUv = useSourceUvs && source.uvs.Count == source.vertices.Count
                        ? source.uvs[i]
                        : NormalizeUv(source.vertices[i], carrierRect);
                    AddVertex(output, new Vertex(source.vertices[i], source.colors[i], stableUv), paint);
                }
                output.triangles.AddRange(source.triangles);
                return;
            }

            for (var i = 0; i + 2 < source.triangles.Count; i += 3)
            {
                var aIndex = source.triangles[i];
                var bIndex = source.triangles[i + 1];
                var cIndex = source.triangles[i + 2];
                var a = CreateVertex(source, aIndex, carrierRect, paint, useSourceUvs);
                var b = CreateVertex(source, bIndex, carrierRect, paint, useSourceUvs);
                var c = CreateVertex(source, cIndex, carrierRect, paint, useSourceUvs);
                AddSubdividedTriangle(output, a, b, c, paint, subdivisions);
            }
        }

        public static Vector2 NormalizeUv(Vector3 point, Rect rect)
        {
            return new Vector2(
                Mathf.InverseLerp(rect.xMin, rect.xMax, point.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, point.y));
        }

        public static Vector2 SvgUv(Vector2 normalizedPoint, Vector4 viewBox)
        {
            var longest = Mathf.Max(Mathf.Abs(viewBox.z), Mathf.Abs(viewBox.w), 0.0001f);
            return new Vector2(
                0.5f + normalizedPoint.x * longest / Mathf.Max(Mathf.Abs(viewBox.z), 0.0001f),
                0.5f + normalizedPoint.y * longest / Mathf.Max(Mathf.Abs(viewBox.w), 0.0001f));
        }

        public static void AddToVertexHelper(VertexHelper helper, MeshData data)
        {
            for (var i = 0; i < data.vertices.Count; i++)
            {
                var vertex = UIVertex.simpleVert;
                vertex.position = data.vertices[i];
                vertex.color = data.colors[i];
                vertex.uv0 = data.uv0[i];
                vertex.uv1 = data.uv1[i];
                helper.AddVert(vertex);
            }
            for (var i = 0; i + 2 < data.triangles.Count; i += 3)
            {
                helper.AddTriangle(data.triangles[i], data.triangles[i + 1], data.triangles[i + 2]);
            }
        }

        private static Vertex CreateVertex(MazeVectorBuilder.MeshData source, int index, Rect rect, MazeVectorPaint paint, bool useSourceUvs)
        {
            var stableUv = useSourceUvs && source.uvs.Count == source.vertices.Count
                ? source.uvs[index]
                : NormalizeUv(source.vertices[index], rect);
            return new Vertex(source.vertices[index], source.colors[index], stableUv);
        }

        private static void AddSubdividedTriangle(MeshData output, Vertex a, Vertex b, Vertex c, MazeVectorPaint paint, int depth)
        {
            if (depth <= 0)
            {
                var start = output.vertices.Count;
                AddVertex(output, a, paint);
                AddVertex(output, b, paint);
                AddVertex(output, c, paint);
                output.triangles.Add(start);
                output.triangles.Add(start + 1);
                output.triangles.Add(start + 2);
                return;
            }

            var ab = Vertex.Midpoint(a, b);
            var bc = Vertex.Midpoint(b, c);
            var ca = Vertex.Midpoint(c, a);
            var next = depth - 1;
            AddSubdividedTriangle(output, a, ab, ca, paint, next);
            AddSubdividedTriangle(output, ab, b, bc, paint, next);
            AddSubdividedTriangle(output, ca, bc, c, paint, next);
            AddSubdividedTriangle(output, ab, bc, ca, paint, next);
        }

        private static void AddVertex(MeshData output, Vertex source, MazeVectorPaint paint)
        {
            var mode = paint != null && paint.HasSource ? (float)paint.sourceMode : 0f;
            var uv = paint != null ? paint.uv.Apply(source.uv) : source.uv;
            output.vertices.Add(source.position);
            output.colors.Add(source.color);
            output.uv0.Add(new Vector4(
                uv.x,
                uv.y,
                mode,
                paint != null ? Mathf.Max(0f, paint.uvDisplacement) : 0f));
            output.uv1.Add(new Vector4(
                paint != null ? paint.geometryDisplacement.x : 0f,
                paint != null ? paint.geometryDisplacement.y : 0f,
                paint != null ? Mathf.Clamp(paint.detailStrength, 0f, 2f) : 0f,
                paint != null ? Mathf.Clamp01(paint.coverageStrength) : 0f));
        }
    }

    public static class MazeVectorPaintMaterials
    {
        private const string UiShaderResource = "MazeVectorPaintUI";
        private const string WorldShaderResource = "MazeVectorPaintWorld";
        private static readonly Dictionary<string, Material> UiMaterials = new();
        private static Material worldMaterial;
        private static Texture2D neutralControlMap;

        public static int GeneratedMaterialCount => UiMaterials.Count + (worldMaterial != null ? 1 : 0);
        public static int GeneratedTextureCount => neutralControlMap != null ? 1 : 0;

        public static Texture2D NeutralControlMap
        {
            get
            {
                if (neutralControlMap != null)
                {
                    return neutralControlMap;
                }
                neutralControlMap = new Texture2D(1, 1, TextureFormat.RGBA32, false, true)
                {
                    name = "MazeVector Neutral Control Map",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                neutralControlMap.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f, 1f));
                neutralControlMap.Apply(false, true);
                return neutralControlMap;
            }
        }

        public static Material GetUi(MazeVectorPaint paint)
        {
            if (paint == null || !paint.RequiresShader)
            {
                return null;
            }

            var control = paint.controlMap != null ? paint.controlMap : NeutralControlMap;
            var key = control.GetEntityId().ToString()
                + ":" + paint.controlScroll.x.ToString("R", CultureInfo.InvariantCulture)
                + ":" + paint.controlScroll.y.ToString("R", CultureInfo.InvariantCulture);
            if (UiMaterials.TryGetValue(key, out var material) && material != null)
            {
                return material;
            }

            var shader = Resources.Load<Shader>(UiShaderResource) ?? Shader.Find("Hidden/MAZE/VectorPaintUI");
            if (shader == null)
            {
                MazeDiagnosticsLog.WarnOnce("maze.vector.paint.ui_shader_missing", "Maze.Vector", "paint_shader_missing", "MazeVector UI paint shader is missing", MazeDiagnosticsLog.JsonString("resource", UiShaderResource));
                return null;
            }

            material = new Material(shader)
            {
                name = "MazeVector Paint UI " + key,
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetTexture("_ControlMap", control);
            material.SetVector("_ControlScroll", paint.controlScroll);
            UiMaterials[key] = material;
            return material;
        }

        public static Material GetWorld()
        {
            if (worldMaterial != null)
            {
                return worldMaterial;
            }

            var shader = Resources.Load<Shader>(WorldShaderResource) ?? Shader.Find("Hidden/MAZE/VectorPaintWorld");
            if (shader == null)
            {
                MazeDiagnosticsLog.WarnOnce("maze.vector.paint.world_shader_missing", "Maze.Vector", "paint_shader_missing", "MazeVector world paint shader is missing", MazeDiagnosticsLog.JsonString("resource", WorldShaderResource));
                return null;
            }
            worldMaterial = new Material(shader)
            {
                name = "MazeVector Paint World",
                hideFlags = HideFlags.HideAndDontSave
            };
            return worldMaterial;
        }

        public static void ReleaseGeneratedResources()
        {
            foreach (var pair in UiMaterials)
            {
                MazeVectorRuntimeResources.DestroyTransient(pair.Value);
            }
            UiMaterials.Clear();
            MazeVectorRuntimeResources.DestroyTransient(worldMaterial);
            MazeVectorRuntimeResources.DestroyTransient(neutralControlMap);
            worldMaterial = null;
            neutralControlMap = null;
        }
    }
}
