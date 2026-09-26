using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maze
{
    public enum MazeVectorShapeKind
    {
        Rect,
        RoundedRect,
        Circle,
        Ellipse,
        Triangle,
        Polygon,
        LineStrip,
        OutlineBand,
        DashedOutline,
        BrokenRectOutline,
        CornerAccents,
        GridCells
    }

    public enum MazeVectorOutlineBreakKind
    {
        Gap,
        Chip
    }

    [Serializable]
    public sealed class MazeVectorOutlineBreak
    {
        [Range(0f, 1f)] public float start01;
        [Range(0.001f, 1f)] public float length01 = 0.06f;
        public MazeVectorOutlineBreakKind kind = MazeVectorOutlineBreakKind.Gap;
        [Range(0f, 1f)] public float depth = 0.55f;

        public MazeVectorOutlineBreak Clone()
        {
            return new MazeVectorOutlineBreak
            {
                start01 = start01,
                length01 = length01,
                kind = kind,
                depth = depth
            };
        }
    }

    [Serializable]
    public sealed class MazeVectorShape
    {
        public MazeVectorShapeKind kind = MazeVectorShapeKind.Rect;
        public Vector2 size = new(120f, 80f);
        [Min(0f)] public float radius = 24f;
        [Min(0f)] public float cornerRadius = 16f;
        [Min(3)] public int segments = 24;
        [Min(3)] public int sides = 6;
        [Min(1)] public int gridColumns = 4;
        [Min(1)] public int gridRows = 4;
        [Min(0f)] public float gridGap = 4f;
        [Min(0f)] public float cornerLength = 22f;
        [Min(0f)] public float dashLength = 18f;
        [Min(0f)] public float dashGap = 10f;
        public bool closed = true;
        public List<Vector2> points = new();
        public List<MazeVectorOutlineBreak> outlineBreaks = new();

        public MazeVectorShape Clone()
        {
            return new MazeVectorShape
            {
                kind = kind,
                size = size,
                radius = radius,
                cornerRadius = cornerRadius,
                segments = segments,
                sides = sides,
                gridColumns = gridColumns,
                gridRows = gridRows,
                gridGap = gridGap,
                cornerLength = cornerLength,
                dashLength = dashLength,
                dashGap = dashGap,
                closed = closed,
                points = points != null ? new List<Vector2>(points) : new List<Vector2>(),
                outlineBreaks = CloneOutlineBreaks(outlineBreaks)
            };
        }

        private static List<MazeVectorOutlineBreak> CloneOutlineBreaks(List<MazeVectorOutlineBreak> source)
        {
            var result = new List<MazeVectorOutlineBreak>(source != null ? source.Count : 0);
            for (var i = 0; source != null && i < source.Count; i++)
            {
                if (source[i] != null)
                {
                    result.Add(source[i].Clone());
                }
            }
            return result;
        }

        public static MazeVectorShape Rect(Vector2 size)
        {
            return new MazeVectorShape { kind = MazeVectorShapeKind.Rect, size = size };
        }

        public static MazeVectorShape RoundedRect(Vector2 size, float radius)
        {
            return new MazeVectorShape { kind = MazeVectorShapeKind.RoundedRect, size = size, cornerRadius = radius };
        }

        public static MazeVectorShape Circle(float radius, int segments = 32)
        {
            return new MazeVectorShape { kind = MazeVectorShapeKind.Circle, radius = radius, segments = segments, size = Vector2.one * radius * 2f };
        }

        public static MazeVectorShape Line(params Vector2[] points)
        {
            return new MazeVectorShape { kind = MazeVectorShapeKind.LineStrip, closed = false, points = points != null ? new List<Vector2>(points) : new List<Vector2>() };
        }
    }
}
