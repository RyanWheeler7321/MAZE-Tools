using System;
using UnityEngine;
using UnityEngine.UI;

namespace Maze
{
    // Compiled vector geometry shared by all renderers. Channels are only allocated when used.
    public sealed class MazeVectorGeometry
    {
        public static readonly MazeVectorGeometry Empty = new(
            Array.Empty<Vector3>(),
            Array.Empty<int>(),
            Array.Empty<Color32>(),
            Array.Empty<Vector4>(),
            Array.Empty<Vector4>(),
            default,
            string.Empty,
            null);

        public MazeVectorGeometry(
            Vector3[] positions,
            int[] indices,
            Color32[] colors,
            Vector4[] uv0,
            Vector4[] uv1,
            Bounds bounds,
            string stableHash,
            MazeVectorGeometryChannels channels)
        {
            Positions = positions ?? Array.Empty<Vector3>();
            Indices = indices ?? Array.Empty<int>();
            Colors = colors ?? Array.Empty<Color32>();
            Uv0 = uv0 ?? Array.Empty<Vector4>();
            Uv1 = uv1 ?? Array.Empty<Vector4>();
            Bounds = bounds;
            StableHash = stableHash ?? string.Empty;
            Channels = channels;
        }

        public Vector3[] Positions { get; }
        public int[] Indices { get; }
        public Color32[] Colors { get; }
        public Vector4[] Uv0 { get; }
        public Vector4[] Uv1 { get; }
        public Bounds Bounds { get; }
        public string StableHash { get; }
        public MazeVectorGeometryChannels Channels { get; }
        public int VertexCount => Positions.Length;
        public int TriangleCount => Indices.Length / 3;
        public bool HasGeometry => Positions.Length > 0 && Indices.Length >= 3;

        public void WriteTo(VertexHelper helper, float outputIntensity = MazeVectorUiOutput.NeutralIntensity)
        {
            helper.Clear();
            outputIntensity = MazeVectorUiOutput.ClampIntensity(outputIntensity);
            for (var index = 0; index < Positions.Length; index++)
            {
                var color = index < Colors.Length ? Colors[index] : (Color32)Color.white;
                var firstUv = index < Uv0.Length ? Uv0[index] : Vector4.zero;
                var secondUv = index < Uv1.Length ? Uv1[index] : Vector4.zero;
                var vertex = UIVertex.simpleVert;
                vertex.position = Positions[index];
                vertex.color = color;
                vertex.uv0 = firstUv;
                vertex.uv1 = secondUv;
                vertex.uv2 = new Vector4(outputIntensity, 0f, 0f, 0f);
                helper.AddVert(vertex);
            }

            for (var index = 0; index + 2 < Indices.Length; index += 3)
            {
                helper.AddTriangle(Indices[index], Indices[index + 1], Indices[index + 2]);
            }
        }
    }

    // Optional export metadata, kept out of the render vertices.
    public sealed class MazeVectorGeometryChannels
    {
        public int[] ElementIds;
        public int[] LayerIds;
        public int[] RoleIds;
        public int[] SpanIds;
        public uint[] Seeds;
        public Vector2[] StablePositions;
        public Vector2[] NormalizedPositions;
    }
}
