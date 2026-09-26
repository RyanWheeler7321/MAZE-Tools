using UnityEngine;

namespace Maze
{
    public struct MazeVectorDiagnosticsReport
    {
        public int total;
        public int active;
        public int built;
        public int vertices;
        public int triangles;
        public int zeroGeometry;
        public int importedAssets;
        public int paintedVectors;
        public int overflowAssets;
        public int oldestBuildFrame;
        public int newestBuildFrame;

        public string ToJsonFields(string label = null)
        {
            return MazeDiagnosticsLog.JoinData(
                MazeDiagnosticsLog.JsonString("label", label ?? string.Empty),
                MazeDiagnosticsLog.JsonNumber("totalVectors", total),
                MazeDiagnosticsLog.JsonNumber("activeVectors", active),
                MazeDiagnosticsLog.JsonNumber("builtVectors", built),
                MazeDiagnosticsLog.JsonNumber("vertices", vertices),
                MazeDiagnosticsLog.JsonNumber("triangles", triangles),
                MazeDiagnosticsLog.JsonNumber("zeroGeometry", zeroGeometry),
                MazeDiagnosticsLog.JsonNumber("importedAssets", importedAssets),
                MazeDiagnosticsLog.JsonNumber("paintedVectors", paintedVectors),
                MazeDiagnosticsLog.JsonNumber("overflowAssets", overflowAssets),
                MazeDiagnosticsLog.JsonNumber("oldestBuildFrame", oldestBuildFrame),
                MazeDiagnosticsLog.JsonNumber("newestBuildFrame", newestBuildFrame));
        }
    }

    public static class MazeVectorDiagnostics
    {
        public static MazeVectorDiagnosticsReport Count(Transform root)
        {
            var report = new MazeVectorDiagnosticsReport { oldestBuildFrame = -1, newestBuildFrame = -1 };
            if (root == null)
            {
                return report;
            }

            var graphics = root.GetComponentsInChildren<MazeVectorGraphic>(true);
            report.total = graphics.Length;
            for (var i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                if (graphic != null && graphic.Paint != null && graphic.Paint.RequiresShader)
                {
                    report.paintedVectors++;
                }
                if (graphic == null || !graphic.gameObject.activeInHierarchy || !graphic.isActiveAndEnabled)
                {
                    continue;
                }

                report.active++;
                report.vertices += graphic.LastVertexCount;
                report.triangles += graphic.LastTriangleCount;
                if (graphic.LastBuildFrame >= 0)
                {
                    report.oldestBuildFrame = report.oldestBuildFrame < 0 ? graphic.LastBuildFrame : Mathf.Min(report.oldestBuildFrame, graphic.LastBuildFrame);
                    report.newestBuildFrame = Mathf.Max(report.newestBuildFrame, graphic.LastBuildFrame);
                }

                if (graphic.HasBuiltVisibleGeometry)
                {
                    report.built++;
                }
                else
                {
                    report.zeroGeometry++;
                }
            }

            var batches = root.GetComponentsInChildren<MazeVectorBatchGraphic>(true);
            report.total += batches.Length;
            for (var i = 0; i < batches.Length; i++)
            {
                var batch = batches[i];
                if (batch != null && batch.Paint != null && batch.Paint.RequiresShader)
                {
                    report.paintedVectors++;
                }
                if (batch == null || !batch.gameObject.activeInHierarchy || !batch.isActiveAndEnabled)
                {
                    continue;
                }

                report.active++;
                report.vertices += batch.LastVertexCount;
                report.triangles += batch.LastTriangleCount;
                if (batch.LastBuildFrame >= 0)
                {
                    report.oldestBuildFrame = report.oldestBuildFrame < 0 ? batch.LastBuildFrame : Mathf.Min(report.oldestBuildFrame, batch.LastBuildFrame);
                    report.newestBuildFrame = Mathf.Max(report.newestBuildFrame, batch.LastBuildFrame);
                }

                if (batch.HasBuiltVisibleGeometry)
                {
                    report.built++;
                }
                else
                {
                    report.zeroGeometry++;
                }
            }

            var assets = root.GetComponentsInChildren<MazeVectorAssetGraphic>(true);
            report.total += assets.Length;
            report.importedAssets += assets.Length;
            for (var i = 0; i < assets.Length; i++)
            {
                var graphic = assets[i];
                if (graphic != null && graphic.Paint != null && graphic.Paint.RequiresShader)
                {
                    report.paintedVectors++;
                }
                if (graphic == null || !graphic.gameObject.activeInHierarchy || !graphic.isActiveAndEnabled)
                {
                    continue;
                }
                report.active++;
                report.vertices += graphic.LastVertexCount;
                report.triangles += graphic.LastTriangleCount;
                if (graphic.LastBuildFrame >= 0)
                {
                    report.oldestBuildFrame = report.oldestBuildFrame < 0 ? graphic.LastBuildFrame : Mathf.Min(report.oldestBuildFrame, graphic.LastBuildFrame);
                    report.newestBuildFrame = Mathf.Max(report.newestBuildFrame, graphic.LastBuildFrame);
                }
                if (graphic.LastVertexCount > 0)
                {
                    report.built++;
                }
                else
                {
                    report.zeroGeometry++;
                }
                if (HasOverflow(graphic.Asset))
                {
                    report.overflowAssets++;
                }
            }

            return report;
        }

        public static void LogOnChange(string key, Transform root, string label)
        {
            var report = Count(root);
            var state = report.total + ":" + report.active + ":" + report.built + ":" + report.vertices + ":" + report.zeroGeometry + ":" + report.overflowAssets;
            MazeDiagnosticsLog.InfoOnChange(key, state, "Maze.Vector", "surface_state", "vector surface state", report.ToJsonFields(label));
        }


        public static string DescribeZeroGeometry(Transform root, int max = 8)
        {
            if (root == null || max <= 0)
            {
                return string.Empty;
            }
            var parts = new System.Text.StringBuilder();
            var count = 0;
            var graphics = root.GetComponentsInChildren<MazeVectorGraphic>(true);
            for (var i = 0; i < graphics.Length && count < max; i++)
            {
                var graphic = graphics[i];
                if (graphic == null || !graphic.gameObject.activeInHierarchy || !graphic.isActiveAndEnabled || graphic.HasBuiltVisibleGeometry)
                {
                    continue;
                }
                if (parts.Length > 0) parts.Append(", ");
                parts.Append(graphic.name).Append("(").Append(graphic.LastShapeKind).Append(")");
                count++;
            }
            var batches = root.GetComponentsInChildren<MazeVectorBatchGraphic>(true);
            for (var i = 0; i < batches.Length && count < max; i++)
            {
                var batch = batches[i];
                if (batch == null || !batch.gameObject.activeInHierarchy || !batch.isActiveAndEnabled || batch.HasBuiltVisibleGeometry)
                {
                    continue;
                }
                if (parts.Length > 0) parts.Append(", ");
                parts.Append(batch.name).Append("(Batch)");
                count++;
            }
            return parts.ToString();
        }

        private static bool HasOverflow(MazeVectorAsset asset)
        {
            if (asset == null || !asset.HasGeometryBounds)
            {
                return false;
            }
            var viewBox = asset.ViewBox;
            var longest = Mathf.Max(Mathf.Abs(viewBox.z), Mathf.Abs(viewBox.w), 0.0001f);
            var halfWidth = Mathf.Abs(viewBox.z) / longest * 0.5f;
            var halfHeight = Mathf.Abs(viewBox.w) / longest * 0.5f;
            var bounds = asset.GeometryBounds;
            const float epsilon = 0.0001f;
            return bounds.xMin < -halfWidth - epsilon
                || bounds.xMax > halfWidth + epsilon
                || bounds.yMin < -halfHeight - epsilon
                || bounds.yMax > halfHeight + epsilon;
        }

        public static void ValidateRecipe(MazeVectorRecipe recipe, string key)
        {
            if (recipe == null)
            {
                MazeDiagnosticsLog.WarnOnce("maze.vector.recipe.null:" + key, "Maze.Vector", "recipe_invalid", "MazeVector recipe is null", MazeDiagnosticsLog.JsonString("key", key));
                return;
            }
            if (recipe.Size.x <= 0f || recipe.Size.y <= 0f)
            {
                MazeDiagnosticsLog.WarnOnce("maze.vector.recipe.size:" + key, "Maze.Vector", "recipe_invalid", "MazeVector recipe has non-positive size", MazeDiagnosticsLog.JoinData(MazeDiagnosticsLog.JsonString("key", key), MazeDiagnosticsLog.JsonNumber("width", recipe.Size.x), MazeDiagnosticsLog.JsonNumber("height", recipe.Size.y)));
            }
            if (recipe.Count == 0)
            {
                MazeDiagnosticsLog.WarnOnce("maze.vector.recipe.empty:" + key, "Maze.Vector", "recipe_invalid", "MazeVector recipe has no elements", MazeDiagnosticsLog.JsonString("key", key));
            }
            for (var i = 0; i < recipe.Count; i++)
            {
                var element = recipe.Elements[i];
                if (element == null)
                {
                    MazeDiagnosticsLog.WarnOnce("maze.vector.recipe.element.null:" + key + ":" + i, "Maze.Vector", "recipe_invalid", "MazeVector recipe has a null element", MazeDiagnosticsLog.JoinData(MazeDiagnosticsLog.JsonString("key", key), MazeDiagnosticsLog.JsonNumber("index", i)));
                    continue;
                }
                ValidateShape(element.shape, key + ":shape:" + i);
                if (element.useStyleOverride)
                {
                    ValidateStyle(element.styleOverride, key + ":style:" + i);
                }
            }
        }

        public static void ValidateShape(MazeVectorShape shape, string key)
        {
            if (shape == null)
            {
                MazeDiagnosticsLog.WarnOnce("maze.vector.shape.null:" + key, "Maze.Vector", "shape_invalid", "MazeVector shape is null", MazeDiagnosticsLog.JsonString("key", key));
                return;
            }
            if (shape.size.x < 0f || shape.size.y < 0f)
            {
                MazeDiagnosticsLog.WarnOnce("maze.vector.shape.size:" + key, "Maze.Vector", "shape_invalid", "MazeVector shape has negative size", MazeDiagnosticsLog.JoinData(MazeDiagnosticsLog.JsonString("key", key), MazeDiagnosticsLog.JsonNumber("width", shape.size.x), MazeDiagnosticsLog.JsonNumber("height", shape.size.y)));
            }
            if (shape.kind == MazeVectorShapeKind.LineStrip && (shape.points == null || shape.points.Count < 2))
            {
                MazeDiagnosticsLog.WarnOnce("maze.vector.shape.points:" + key, "Maze.Vector", "shape_invalid", "MazeVector line has too few points", MazeDiagnosticsLog.JoinData(MazeDiagnosticsLog.JsonString("key", key), MazeDiagnosticsLog.JsonString("kind", shape.kind.ToString())));
            }
            if ((shape.kind == MazeVectorShapeKind.Circle || shape.kind == MazeVectorShapeKind.Ellipse) && shape.segments < 3)
            {
                MazeDiagnosticsLog.WarnOnce("maze.vector.shape.segments:" + key, "Maze.Vector", "shape_invalid", "MazeVector rounded shape has too few segments", MazeDiagnosticsLog.JoinData(MazeDiagnosticsLog.JsonString("key", key), MazeDiagnosticsLog.JsonNumber("segments", shape.segments)));
            }
        }

        public static void ValidateStyle(MazeVectorStyle style, string key)
        {
            if (style == null)
            {
                MazeDiagnosticsLog.WarnOnce("maze.vector.style.null:" + key, "Maze.Vector", "style_invalid", "MazeVector style is null", MazeDiagnosticsLog.JsonString("key", key));
                return;
            }
            if (!style.fill && !style.stroke)
            {
                MazeDiagnosticsLog.WarnOnce("maze.vector.style.invisible:" + key, "Maze.Vector", "style_invalid", "MazeVector style has no fill or stroke", MazeDiagnosticsLog.JsonString("key", key));
            }
            if (style.stroke && style.strokeThickness <= 0f)
            {
                MazeDiagnosticsLog.WarnOnce("maze.vector.style.stroke:" + key, "Maze.Vector", "style_invalid", "MazeVector style has non-positive stroke thickness", MazeDiagnosticsLog.JoinData(MazeDiagnosticsLog.JsonString("key", key), MazeDiagnosticsLog.JsonNumber("strokeThickness", style.strokeThickness)));
            }
            if (style.fill && style.fillColor.a <= 0f && style.shading == MazeVectorShadingMode.Flat)
            {
                MazeDiagnosticsLog.WarnOnce("maze.vector.style.alpha:" + key, "Maze.Vector", "style_invalid", "MazeVector flat fill has zero alpha", MazeDiagnosticsLog.JsonString("key", key));
            }
        }
    }
}
