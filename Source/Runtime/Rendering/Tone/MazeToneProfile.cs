using UnityEngine;

namespace Maze
{
    public enum MazeToneMode
    {
        None = 0,
        Neutral = 1,
        ACES = 2,
        ReinhardExtended = 3,
        GT = 4,
    }

    public enum MazeToneQualityTier
    {
        Off = 0,
        Fast = 1,
        Standard = 2,
        Dithered = 3,
    }

    [CreateAssetMenu(fileName = "MAZE_ToneProfile", menuName = "MAZE/Rendering/Tone Profile")]
    public sealed class MazeToneProfile : ScriptableObject
    {
        [Header("Tone")]
        public bool toneEnabled = true;
        public MazeToneMode mode = MazeToneMode.ACES;
        public MazeToneQualityTier quality = MazeToneQualityTier.Standard;
        public bool renderInSceneView = true;
        [Range(-12f, 12f)] public float exposureEV = 0f;
        [Range(0f, 1f)] public float strength = 1f;
        [Range(0f, 4f)] public float ditherStrength = 0.5f;

        [Header("Neutral")]
        [Range(0.05f, 8f)] public float neutralContrast = 1f;
        [Range(0.25f, 64f)] public float neutralWhitePoint = 5.3f;
        [Range(0.05f, 8f)] public float neutralWhiteClip = 1f;

        [Header("ACES")]
        [Range(0.05f, 8f)] public float acesContrast = 1f;
        [Range(0f, 4f)] public float acesSaturation = 1f;

        [Header("Reinhard Extended")]
        [Range(0.25f, 64f)] public float reinhardWhitePoint = 4f;

        [Header("GT")]
        [Range(0.25f, 8f)] public float gtMaxBrightness = 1f;
        [Range(0.05f, 8f)] public float gtContrast = 1f;
        [Range(0.001f, 4f)] public float gtLinearStart = 0.22f;
        [Range(0.001f, 4f)] public float gtLinearLength = 0.4f;
        [Range(0.05f, 8f)] public float gtBlackTightness = 1.33f;

        public bool RendersTone => toneEnabled && quality != MazeToneQualityTier.Off && mode != MazeToneMode.None && strength > 0.0001f;
        public bool UsesDither => RendersTone && quality == MazeToneQualityTier.Dithered && ditherStrength > 0.0001f;
        public float ExposureMultiplier => Mathf.Pow(2f, exposureEV);
    }
}
