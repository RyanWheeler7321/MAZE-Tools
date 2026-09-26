using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Maze
{
    public static class MazeVectorCompiler
    {
        public static MazeVectorGeometry CompileShape(
            Rect rect,
            MazeVectorShape shape,
            MazeVectorStyle style,
            MazeVectorPaint paint = null,
            float progress = 1f,
            bool selected = false,
            int stableElementId = 0,
            MazeVectorLayer layer = MazeVectorLayer.Stroke,
            MazeVectorRole role = MazeVectorRole.Accent)
        {
            var built = new MazeVectorBuilder.MeshData();
            var painted = new MazeVectorPaintMesh.MeshData();
            MazeVectorBuilder.Build(built, rect, shape, style, progress, selected);
            MazeVectorPaintMesh.Build(built, rect, paint, painted);
            return FromPainted(painted, rect, stableElementId, (int)layer, (int)role);
        }

        public static MazeVectorGeometry CompileRecipe(
            Rect rect,
            MazeVectorRecipe recipe,
            MazeVectorProfile profile = null,
            MazeVectorLayerSet layers = null,
            MazeVectorState state = MazeVectorState.Idle,
            MazeVectorPaint paintOverride = null,
            float progress = 1f)
        {
            if (recipe?.Elements == null)
            {
                return MazeVectorGeometry.Empty;
            }

            var activeLayers = layers ?? new MazeVectorLayerSet();
            var selected = state is MazeVectorState.Selected or MazeVectorState.Pressed;
            var output = new MazeVectorPaintMesh.MeshData();
            var elementIds = new List<int>();
            var layerIds = new List<int>();
            var roleIds = new List<int>();

            for (var elementIndex = 0; elementIndex < recipe.Elements.Count; elementIndex++)
            {
                var element = recipe.Elements[elementIndex];
                if (element?.shape == null || !activeLayers.Allows(element.layer))
                {
                    continue;
                }

                var style = element.useStyleOverride && element.styleOverride != null
                    ? element.styleOverride
                    : profile != null
                        ? profile.Resolve(element.role, state)
                        : MazeVectorReferenceKit.Outline(2f);
                var elementSize = element.size == Vector2.zero ? recipe.Size : element.size;
                var elementRect = new Rect(rect.center + element.position - elementSize * 0.5f, elementSize);
                var built = new MazeVectorBuilder.MeshData();
                var painted = new MazeVectorPaintMesh.MeshData();
                MazeVectorBuilder.Build(built, elementRect, element.shape, style, progress, selected);
                MazeVectorPaintMesh.Build(built, elementRect, paintOverride ?? element.paint, painted);
                Append(painted, output, elementIds, layerIds, roleIds, elementIndex, (int)element.layer, (int)element.role);
            }

            return FromPainted(output, rect, elementIds.ToArray(), layerIds.ToArray(), roleIds.ToArray());
        }

        public static MazeVectorGeometry CompileAsset(
            Rect target,
            MazeVectorAsset asset,
            MazeVectorAssetFitMode fitMode = MazeVectorAssetFitMode.Contain,
            MazeVectorProfile profile = null,
            MazeVectorState state = MazeVectorState.Idle,
            MazeVectorPaint paint = null,
            float progress = 1f)
        {
            if (asset?.Shapes == null || progress <= 0f)
            {
                return MazeVectorGeometry.Empty;
            }

            var built = new MazeVectorBuilder.MeshData();
            var elementIds = new List<int>();
            var roleIds = new List<int>();
            var scale = MazeVectorAssetGraphic.ComputeLayoutScale(target, asset.ViewBox, fitMode);
            var center = target.center;
            for (var shapeIndex = 0; shapeIndex < asset.Shapes.Length; shapeIndex++)
            {
                var shape = asset.Shapes[shapeIndex];
                if (shape?.vertices == null || shape.triangles == null)
                {
                    continue;
                }

                var color = profile == null
                    ? shape.sourceColor
                    : profile.ResolveImportedColor(shape.sourceColor, shape.role, state);
                color.a *= Mathf.Clamp01(progress);
                var offset = built.vertices.Count;
                for (var vertexIndex = 0; vertexIndex < shape.vertices.Length; vertexIndex++)
                {
                    var point = shape.vertices[vertexIndex];
                    built.vertices.Add(new Vector3(center.x + point.x * scale.x, center.y + point.y * scale.y));
                    built.colors.Add(color);
                    built.uvs.Add(shape.uvs != null && shape.uvs.Length == shape.vertices.Length
                        ? shape.uvs[vertexIndex]
                        : MazeVectorPaintMesh.SvgUv(point, asset.ViewBox));
                    elementIds.Add(shapeIndex);
                    roleIds.Add((int)shape.role);
                }
                for (var triangleIndex = 0; triangleIndex < shape.triangles.Length; triangleIndex++)
                {
                    built.triangles.Add(offset + shape.triangles[triangleIndex]);
                }
            }

            var painted = new MazeVectorPaintMesh.MeshData();
            MazeVectorPaintMesh.Build(built, target, paint, painted, true);
            // Paint subdivision can change vertex counts. Preserve semantic channels when counts match;
            // otherwise derive deterministic element IDs from the painted vertex order.
            if (painted.vertices.Count != elementIds.Count)
            {
                elementIds.Clear();
                roleIds.Clear();
                for (var index = 0; index < painted.vertices.Count; index++)
                {
                    elementIds.Add(index);
                    roleIds.Add((int)MazeVectorPaintRole.PreserveSource);
                }
            }
            return FromPainted(painted, target, elementIds.ToArray(), null, roleIds.ToArray());
        }

        private static MazeVectorGeometry FromPainted(
            MazeVectorPaintMesh.MeshData data,
            Rect rect,
            int elementId,
            int layerId,
            int roleId)
        {
            var count = data?.vertices.Count ?? 0;
            var elements = new int[count];
            var layers = new int[count];
            var roles = new int[count];
            Array.Fill(elements, elementId);
            Array.Fill(layers, layerId);
            Array.Fill(roles, roleId);
            return FromPainted(data, rect, elements, layers, roles);
        }

        private static MazeVectorGeometry FromPainted(
            MazeVectorPaintMesh.MeshData data,
            Rect rect,
            int[] elementIds,
            int[] layerIds,
            int[] roleIds)
        {
            if (data == null || data.vertices.Count == 0 || data.triangles.Count == 0)
            {
                return MazeVectorGeometry.Empty;
            }

            var positions = data.vertices.ToArray();
            var indices = data.triangles.ToArray();
            var colors = new Color32[data.colors.Count];
            for (var index = 0; index < colors.Length; index++)
            {
                colors[index] = data.colors[index];
            }
            var uv0 = data.uv0.ToArray();
            var uv1 = data.uv1.ToArray();
            var normalized = new Vector2[positions.Length];
            var width = Mathf.Max(0.0001f, rect.width);
            var height = Mathf.Max(0.0001f, rect.height);
            for (var index = 0; index < positions.Length; index++)
            {
                normalized[index] = new Vector2(
                    (positions[index].x - rect.xMin) / width,
                    (positions[index].y - rect.yMin) / height);
            }

            var bounds = CalculateBounds(positions);
            var channels = new MazeVectorGeometryChannels
            {
                ElementIds = elementIds,
                LayerIds = layerIds,
                RoleIds = roleIds,
                StablePositions = Array.ConvertAll(positions, point => (Vector2)point),
                NormalizedPositions = normalized
            };
            return new MazeVectorGeometry(positions, indices, colors, uv0, uv1, bounds, StableHash(positions, indices, elementIds), channels);
        }

        private static void Append(
            MazeVectorPaintMesh.MeshData source,
            MazeVectorPaintMesh.MeshData target,
            List<int> elementIds,
            List<int> layerIds,
            List<int> roleIds,
            int elementId,
            int layerId,
            int roleId)
        {
            var offset = target.vertices.Count;
            for (var index = 0; index < source.vertices.Count; index++)
            {
                target.vertices.Add(source.vertices[index]);
                target.colors.Add(source.colors[index]);
                target.uv0.Add(source.uv0[index]);
                target.uv1.Add(source.uv1[index]);
                elementIds.Add(elementId);
                layerIds.Add(layerId);
                roleIds.Add(roleId);
            }
            for (var index = 0; index < source.triangles.Count; index++)
            {
                target.triangles.Add(offset + source.triangles[index]);
            }
        }

        private static Bounds CalculateBounds(IReadOnlyList<Vector3> positions)
        {
            if (positions == null || positions.Count == 0)
            {
                return default;
            }
            var bounds = new Bounds(positions[0], Vector3.zero);
            for (var index = 1; index < positions.Count; index++)
            {
                bounds.Encapsulate(positions[index]);
            }
            return bounds;
        }

        private static string StableHash(IReadOnlyList<Vector3> positions, IReadOnlyList<int> indices, IReadOnlyList<int> elementIds)
        {
            var hash = 14695981039346656037UL;
            for (var index = 0; index < positions.Count; index++)
            {
                Hash(ref hash, FloatBits(positions[index].x));
                Hash(ref hash, FloatBits(positions[index].y));
                Hash(ref hash, FloatBits(positions[index].z));
                if (elementIds != null && index < elementIds.Count)
                {
                    Hash(ref hash, unchecked((uint)elementIds[index]));
                }
            }
            for (var index = 0; index < indices.Count; index++)
            {
                Hash(ref hash, unchecked((uint)indices[index]));
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static void Hash(ref ulong hash, uint value)
        {
            for (var shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(value >> shift);
                hash *= 1099511628211UL;
            }
        }

        private static uint FloatBits(float value)
        {
            return BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
        }
    }
}
