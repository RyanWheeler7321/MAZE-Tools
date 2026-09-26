using System;
using UnityEngine;

namespace Maze
{
    public enum MazeSvgPaintMode
    {
        PreserveSource,
        ExplicitPalette,
        LightDark
    }

    [Serializable]
    public sealed class MazeSvgPaintToken
    {
        public Color32 source = new(255, 255, 255, 255);
        public MazeVectorPaintRole role = MazeVectorPaintRole.PreserveSource;
    }

    [Serializable]
    public sealed class MazeSvgImportSettings
    {
        public MazeSvgPaintMode paintMode = MazeSvgPaintMode.PreserveSource;
        public MazeSvgPaintToken[] paintTokens = Array.Empty<MazeSvgPaintToken>();
        [Range(0, 8)] public int paintTolerance = 2;
        public bool strictPaintTokens;
        [Min(2)] public int curveSegments = 12;
        [Min(0.0001f)] public float pointEpsilon = 0.001f;
        [Range(0f, 1f)] public float lightRoleThreshold = 0.46f;
        public bool warnUnsupported = true;
        public bool useUnityVectorGraphicsTessellator = true;
        public bool allowLegacyFallback = true;
        public bool clipViewport = true;
        [Min(0f)] public float svgDpi;
        [Min(0.0001f)] public float svgPixelsPerUnit = 1f;
        [Min(0.0001f)] public float stepDistance = 2f;
        [Min(0.0001f)] public float maxCordDeviation = 0.5f;
        [Min(0.0001f)] public float maxTanAngleDeviation = 0.2f;
        [Min(0.0001f)] public float samplingStepSize = 0.1f;

        public static MazeSvgImportSettings Default => new();

        public string StableSignature()
        {
            var tokens = new System.Text.StringBuilder();
            for (var i = 0; paintTokens != null && i < paintTokens.Length; i++)
            {
                var token = paintTokens[i];
                if (token == null)
                {
                    continue;
                }
                tokens.Append('|')
                    .Append(token.source.r).Append(',')
                    .Append(token.source.g).Append(',')
                    .Append(token.source.b).Append(',')
                    .Append(token.source.a).Append(':')
                    .Append((int)token.role);
            }

            return string.Join(";",
                (int)paintMode,
                paintTolerance,
                strictPaintTokens,
                curveSegments,
                pointEpsilon,
                lightRoleThreshold,
                warnUnsupported,
                useUnityVectorGraphicsTessellator,
                allowLegacyFallback,
                clipViewport,
                svgDpi,
                svgPixelsPerUnit,
                stepDistance,
                maxCordDeviation,
                maxTanAngleDeviation,
                samplingStepSize,
                tokens.ToString());
        }
    }
}
