using System;
using UnityEngine;

namespace Maze
{
    [Serializable]
    public sealed class MazeVectorStyle
    {
        public bool fill = true;
        public bool stroke = false;
        public Color fillColor = Color.white;
        public Color strokeColor = Color.white;
        [Min(0f)] public float strokeThickness = 4f;
        public MazeVectorStrokeCap strokeCap = MazeVectorStrokeCap.Butt;
        public MazeVectorStrokeJoin strokeJoin = MazeVectorStrokeJoin.Bevel;
        public MazeVectorCornerMode cornerMode = MazeVectorCornerMode.Rounded;
        public MazeVectorShadingMode shading = MazeVectorShadingMode.Flat;
        public Color gradientTopColor = Color.white;
        public Color gradientBottomColor = Color.white;
        [Min(0.05f)] public float amorphousScale = 1.35f;
        [Range(0.1f, 4f)] public float amorphousSharpness = 0.8f;
        public float amorphousSeed = 0f;
        public Vector2 amorphousOffset = Vector2.zero;
        [Range(2, 8)] public int amorphousDetail = 4;
        public Color strokePatchColor = new(0.32f, 0.3f, 0.36f, 1f);
        [Range(0f, 1f)] public float strokePatchStrength = 0f;
        [Min(0.05f)] public float strokePatchScale = 2f;
        [Range(0.1f, 4f)] public float strokePatchSharpness = 1.25f;
        public float strokePatchSeed = 0f;
        public Vector2 strokePatchOffset = Vector2.zero;
        public MazeVectorStrokeGradientMode strokeGradientMode = MazeVectorStrokeGradientMode.None;
        public Color strokeGradientColor = new(0.9f, 0.92f, 1f, 1f);
        [Range(0f, 1f)] public float strokeGradientStrength = 0f;
        [Range(0f, 1f)] public float strokeGradientCenter = 0f;
        [Range(0.02f, 1f)] public float strokeGradientWidth = 0.18f;
        public Vector2 strokeGradientAnchor = new(0.82f, 0.82f);
        [Min(4f)] public float strokeGradientSampleLength = 24f;
        public MazeVectorStrokeGradientStop[] strokeGradientStops = Array.Empty<MazeVectorStrokeGradientStop>();
        public Color shadowColor = new(0f, 0f, 0f, 0f);
        public Vector2 shadowOffset = Vector2.zero;
        [Min(0f)] public float shadowSpread = 0f;
        [Min(0f)] public float shadowSoftness = 0f;
        public Color softGlowColor = new(1f, 1f, 1f, 0f);
        [Min(0f)] public float softGlowSpread = 0f;
        public Color rimColor = new(1f, 1f, 1f, 0f);
        [Min(0f)] public float rimThickness = 0f;
        public Color innerStrokeColor = new(1f, 1f, 1f, 0f);
        [Min(0f)] public float innerStrokeThickness = 0f;
        [Range(0f, 8f)] public float glow = 0f;
        public Color selectedFillColor = new(0.72f, 0.24f, 1f, 0.45f);
        public Color selectedStrokeColor = new(1f, 0.78f, 1.65f, 1f);
        [Min(0f)] public float selectedStrokeThickness = 6f;

        public MazeVectorStyle Clone()
        {
            return new MazeVectorStyle
            {
                fill = fill,
                stroke = stroke,
                fillColor = fillColor,
                strokeColor = strokeColor,
                strokeThickness = strokeThickness,
                strokeCap = strokeCap,
                strokeJoin = strokeJoin,
                cornerMode = cornerMode,
                shading = shading,
                gradientTopColor = gradientTopColor,
                gradientBottomColor = gradientBottomColor,
                amorphousScale = amorphousScale,
                amorphousSharpness = amorphousSharpness,
                amorphousSeed = amorphousSeed,
                amorphousOffset = amorphousOffset,
                amorphousDetail = amorphousDetail,
                strokePatchColor = strokePatchColor,
                strokePatchStrength = strokePatchStrength,
                strokePatchScale = strokePatchScale,
                strokePatchSharpness = strokePatchSharpness,
                strokePatchSeed = strokePatchSeed,
                strokePatchOffset = strokePatchOffset,
                strokeGradientMode = strokeGradientMode,
                strokeGradientColor = strokeGradientColor,
                strokeGradientStrength = strokeGradientStrength,
                strokeGradientCenter = strokeGradientCenter,
                strokeGradientWidth = strokeGradientWidth,
                strokeGradientAnchor = strokeGradientAnchor,
                strokeGradientSampleLength = strokeGradientSampleLength,
                strokeGradientStops = strokeGradientStops != null
                    ? (MazeVectorStrokeGradientStop[])strokeGradientStops.Clone()
                    : Array.Empty<MazeVectorStrokeGradientStop>(),
                shadowColor = shadowColor,
                shadowOffset = shadowOffset,
                shadowSpread = shadowSpread,
                shadowSoftness = shadowSoftness,
                softGlowColor = softGlowColor,
                softGlowSpread = softGlowSpread,
                rimColor = rimColor,
                rimThickness = rimThickness,
                innerStrokeColor = innerStrokeColor,
                innerStrokeThickness = innerStrokeThickness,
                glow = glow,
                selectedFillColor = selectedFillColor,
                selectedStrokeColor = selectedStrokeColor,
                selectedStrokeThickness = selectedStrokeThickness
            };
        }

        public static MazeVectorStyle Filled(Color color)
        {
            return new MazeVectorStyle { fill = true, stroke = false, fillColor = color };
        }

        public static MazeVectorStyle Stroke(Color color, float thickness)
        {
            return new MazeVectorStyle { fill = false, stroke = true, strokeColor = color, strokeThickness = thickness };
        }
    }
}
