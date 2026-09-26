using UnityEngine;

namespace Maze
{
    public enum MazeLightFXQualityTier
    {
        Off = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Ultra = 4,
    }

    public enum MazeLightFXSourceMode
    {
        SceneSun = 0,
        ManualScreenPosition = 1,
        SourceTransform = 2,
    }

    [CreateAssetMenu(fileName = "MAZE_LightFXProfile", menuName = "MAZE/Environment/LightFX Profile")]
    public sealed class MazeLightFXProfile : ScriptableObject
    {
        [Header("LightFX")]
        public bool lightFXEnabled = true;
        public MazeLightFXQualityTier quality = MazeLightFXQualityTier.Medium;
        public bool renderInSceneView = true;

        [Header("Light Shafts")]
        public bool lightShaftsEnabled = true;
        public MazeLightFXSourceMode lightShaftSourceMode = MazeLightFXSourceMode.SceneSun;
        public Vector2 manualLightShaftViewportPosition = new(0.5f, 0.58f);
        [ColorUsage(false, true)] public Color lightShaftColor = new(1f, 0.58f, 0.34f, 1f);
        [Range(0f, 2f)] public float lightShaftIntensity = 0.12f;
        [Range(0.1f, 0.995f)] public float lightShaftDecay = 0.94f;
        [Min(0.01f)] public float lightShaftWeight = 0.08f;
        [Min(0.001f)] public float lightShaftExposure = 0.025f;
        [Range(8, 96)] public int lightShaftSamples = 16;
        [Min(0.001f)] public float lightShaftBlurRadius = 0.11f;
        [Range(0f, 1f)] public float lightShaftOcclusion = 0.85f;
        [Range(0.25f, 4f)] public float lightShaftSharpness = 1.1f;
        [Range(0f, 1f)] public float lightShaftiness = 0.04f;
        [Range(0f, 1f)] public float lightShaftGranularity = 0.02f;
        [Min(0.001f)] public float lightShaftNoiseScale = 5f;
        public bool lightShaftDither = true;

        [Header("Source Flare")]
        public bool sourceFlareEnabled = true;
        [ColorUsage(false, true)] public Color sourceFlareColor = new(1f, 0.62f, 0.34f, 1f);
        [Range(0f, 4f)] public float sourceFlareIntensity = 0.24f;
        [Range(0.01f, 1f)] public float haloRadius = 0.22f;
        [Range(0f, 4f)] public float haloIntensity = 0.55f;
        [Range(0f, 4f)] public float starIntensity = 0.55f;
        [Range(2, 12)] public int starSpikeCount = 6;
        [Range(0.01f, 2f)] public float starLength = 0.55f;
        [Range(0f, 360f)] public float starRotation = 0f;
        [Range(0f, 1f)] public float flareOcclusion = 0.8f;
        [Range(0f, 2f)] public float ghostIntensity = 0.16f;
        [Range(0, 8)] public int ghostCount = 4;
        [Range(0.05f, 1.5f)] public float ghostSpacing = 0.34f;
        [Range(0f, 6f)] public float chromaticOffsetPixels = 1.4f;

        public bool RendersLightFX => lightFXEnabled && quality != MazeLightFXQualityTier.Off && (RendersLightShafts || RendersSourceFlare);

        public bool RendersLightShafts => lightShaftsEnabled && lightShaftIntensity > 0.0001f;

        public bool RendersSourceFlare => sourceFlareEnabled && sourceFlareIntensity > 0.0001f;

        public int EffectiveGhostCount => Mathf.Clamp(ghostCount, 0, quality switch
        {
            MazeLightFXQualityTier.Low => 2,
            MazeLightFXQualityTier.Medium => 4,
            MazeLightFXQualityTier.High => 6,
            MazeLightFXQualityTier.Ultra => 8,
            _ => 0,
        });

        public int EffectiveLightShaftSamples
        {
            get
            {
                var limit = quality switch
                {
                    MazeLightFXQualityTier.Low => 16,
                    MazeLightFXQualityTier.Medium => 32,
                    MazeLightFXQualityTier.High => 64,
                    MazeLightFXQualityTier.Ultra => 96,
                    _ => 0,
                };
                return Mathf.Clamp(lightShaftSamples, 0, limit);
            }
        }
    }
}
