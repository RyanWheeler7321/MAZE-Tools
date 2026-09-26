using System;
using UnityEngine;

namespace Maze
{
    public enum MazeVectorPaintRole
    {
        PreserveSource,
        SourceDark,
        SourceLight,
        Fill,
        Line,
        Accent,
        Disabled
    }

    public enum MazeVectorFillRule
    {
        NonZero,
        EvenOdd
    }

    public enum MazeVectorAssetFitMode
    {
        Contain,
        Cover,
        Stretch
    }

    [Serializable]
    public sealed class MazeVectorCompiledShape
    {
        public string name = "Shape";
        public MazeVectorPaintRole role = MazeVectorPaintRole.PreserveSource;
        public Color sourceColor = Color.white;
        public Vector2[] vertices = Array.Empty<Vector2>();
        public Vector2[] uvs = Array.Empty<Vector2>();
        public int[] triangles = Array.Empty<int>();
    }

    [CreateAssetMenu(menuName = "MAZE/Vector/Imported Asset", fileName = "MazeVectorAsset")]
    public sealed class MazeVectorAsset : ScriptableObject
    {
        [SerializeField] private string sourcePath;
        [SerializeField] private string sourceHash;
        [SerializeField] private Vector2 sourceSize = new(100f, 100f);
        [SerializeField] private Vector4 viewBox = new(0f, 0f, 100f, 100f);
        [SerializeField] private Rect geometryBounds;
        [SerializeField] private bool hasGeometryBounds;
        [SerializeField] private int compileSchemaVersion;
        [SerializeField] private string importSettingsHash;
        [SerializeField] private MazeVectorCompiledShape[] shapes = Array.Empty<MazeVectorCompiledShape>();
        [SerializeField] private string importReport;

        public string SourcePath => sourcePath;
        public string SourceHash => sourceHash;
        public Vector2 SourceSize => sourceSize;
        public Vector4 ViewBox => viewBox;
        public Rect GeometryBounds => geometryBounds;
        public bool HasGeometryBounds => hasGeometryBounds;
        public int CompileSchemaVersion => compileSchemaVersion;
        public string ImportSettingsHash => importSettingsHash;
        public MazeVectorCompiledShape[] Shapes => shapes;
        public string ImportReport => importReport;
        public int ShapeCount => shapes != null ? shapes.Length : 0;

        public void SetCompiledData(
            string nextSourcePath,
            string nextSourceHash,
            Vector2 nextSourceSize,
            Vector4 nextViewBox,
            MazeVectorCompiledShape[] nextShapes,
            string nextReport,
            int nextCompileSchemaVersion = 0,
            string nextImportSettingsHash = null)
        {
            sourcePath = nextSourcePath ?? string.Empty;
            sourceHash = nextSourceHash ?? string.Empty;
            sourceSize = nextSourceSize;
            viewBox = nextViewBox;
            shapes = nextShapes ?? Array.Empty<MazeVectorCompiledShape>();
            compileSchemaVersion = nextCompileSchemaVersion;
            importSettingsHash = nextImportSettingsHash ?? string.Empty;
            importReport = nextReport ?? string.Empty;
            RecalculateGeometryBounds();
        }

        private void RecalculateGeometryBounds()
        {
            hasGeometryBounds = false;
            var min = Vector2.zero;
            var max = Vector2.zero;
            for (var shapeIndex = 0; shapeIndex < shapes.Length; shapeIndex++)
            {
                var vertices = shapes[shapeIndex]?.vertices;
                for (var vertexIndex = 0; vertices != null && vertexIndex < vertices.Length; vertexIndex++)
                {
                    var vertex = vertices[vertexIndex];
                    if (!hasGeometryBounds)
                    {
                        min = max = vertex;
                        hasGeometryBounds = true;
                    }
                    else
                    {
                        min = Vector2.Min(min, vertex);
                        max = Vector2.Max(max, vertex);
                    }
                }
            }

            geometryBounds = hasGeometryBounds
                ? Rect.MinMaxRect(min.x, min.y, max.x, max.y)
                : default;
        }
    }
}
