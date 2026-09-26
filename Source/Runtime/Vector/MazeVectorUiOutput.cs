using UnityEngine;
using UnityEngine.UI;

namespace Maze
{
    public static class MazeVectorUiOutput
    {
        public const float NeutralIntensity = 1f;
        public const float MaximumIntensity = 8f;

        public static float ClampIntensity(float intensity)
        {
            return Mathf.Clamp(intensity, NeutralIntensity, MaximumIntensity);
        }

        public static Material ResolveMaterial(MazeVectorPaint paint, float outputIntensity)
        {
            if (paint != null && paint.RequiresShader)
            {
                return MazeVectorPaintMaterials.GetUi(paint);
            }

            return ClampIntensity(outputIntensity) > NeutralIntensity
                ? MazeVectorRuntimeResources.GetUiOutputMaterial()
                : null;
        }

        public static void EnsureCanvasChannels(Graphic graphic, MazeVectorPaint paint, float outputIntensity)
        {
            if (paint != null && paint.RequiresShader)
            {
                MazeVectorCanvasUtility.EnsurePaintChannels(graphic);
            }

            if (ClampIntensity(outputIntensity) > NeutralIntensity)
            {
                MazeVectorCanvasUtility.EnsureOutputIntensityChannels(graphic);
            }
        }
    }
}
