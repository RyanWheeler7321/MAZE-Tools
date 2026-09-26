using UnityEngine;

namespace Maze
{
    public enum MazeFocusQualityTier
    {
        Off = 0,
        Fast = 1,
        Balanced = 2,
        High = 3,
        Cinematic = 4,
    }

    public enum MazeFocusDebugMode
    {
        Off = 0,
        RawDepth = 1,
        LinearDepth = 2,
        SkyMask = 3,
        FocusBand = 4,
        BlurRadius = 5,
        CloudMask = 6,
        CloseCoverage = 7,
        FarCoverage = 8,
        LensDistortion = 9,
        ChromaticAberration = 10,
        SourceColor = 11,
    }

    public enum MazeFocusBackgroundBand
    {
        FarBlur = 0,
        Focus = 1,
    }

    [CreateAssetMenu(fileName = "MAZE_FocusProfile", menuName = "MAZE/Rendering/Focus Profile")]
    public sealed class MazeFocusProfile : ScriptableObject
    {
        [Header("Focus")]
        public bool focusEnabled = true;
        public bool depthBlurEnabled = true;
        public MazeFocusQualityTier quality = MazeFocusQualityTier.Balanced;

        [Header("Close Blur Band")]
        [Range(0f, 32f)] public float closeBlurRadius = 6f;
        [Min(0.01f)] public float focusStart = 8f;
        [Min(0f)] public float closeTransition = 3f;

        [Header("Focus Band")]
        [Min(0.02f)] public float focusEnd = 34f;

        [Header("Far Blur Band")]
        [Min(0f)] public float farTransition = 5f;
        [Range(0f, 32f)] public float farBlurRadius = 7f;

        [Header("Band Edges")]
        [Range(0.5f, 2f)] public float silhouetteSpread = 1f;
        [Range(0.01f, 20f)] public float layerSeparation = 1f;

        [Header("Sky And Clouds")]
        public MazeFocusBackgroundBand skyBand = MazeFocusBackgroundBand.FarBlur;
        public MazeFocusBackgroundBand cloudBand = MazeFocusBackgroundBand.Focus;

        [Header("Lens")]
        public bool lensEnabled;
        public Vector2 lensCenter = new(0.5f, 0.5f);

        [Header("Lens Distortion")]
        public bool distortionEnabled = true;
        [Range(-0.5f, 0.5f)] public float distortionAmount = 0.0012f;
        [Range(0f, 2f)] public float distortionX = 1f;
        [Range(0f, 2f)] public float distortionY = 1f;
        [Range(0.25f, 4f)] public float distortionFalloff = 1.35f;
        [Range(1f, 2f)] public float cropScale = 1f;

        [Header("Chromatic Aberration")]
        public bool chromaticEnabled = true;
        [Range(-8f, 8f)] public float chromaticSpreadPixels = 0.12f;
        [Range(0f, 0.99f)] public float chromaticStart = 0.19f;
        [Range(0.25f, 8f)] public float chromaticFalloff = 3.06f;

        [Header("Quality")]
        public bool renderInSceneView = true;

        [Header("Debug")]
        public MazeFocusDebugMode debugMode = MazeFocusDebugMode.Off;

        public float EffectiveFocusEnd => Mathf.Max(focusStart + 0.01f, focusEnd);
        public float MaxBlurRadius => Mathf.Max(closeBlurRadius, farBlurRadius);
        public bool RendersDepthBlur => focusEnabled
            && depthBlurEnabled
            && quality != MazeFocusQualityTier.Off
            && MaxBlurRadius > 0.001f;
        public bool RendersDistortion => focusEnabled
            && lensEnabled
            && distortionEnabled
            && Mathf.Abs(distortionAmount) > 0.000001f;
        public bool RendersChromatic => focusEnabled
            && lensEnabled
            && chromaticEnabled
            && Mathf.Abs(chromaticSpreadPixels) > 0.0001f;
        public bool RendersLens => RendersDistortion || RendersChromatic;
        public bool RendersFocus => RendersDepthBlur || RendersLens;
        public bool RequiresCloudMask => RendersDepthBlur;

        public int EffectiveSampleCount => quality switch
        {
            MazeFocusQualityTier.Fast => 6,
            MazeFocusQualityTier.Balanced => 12,
            MazeFocusQualityTier.High => 20,
            MazeFocusQualityTier.Cinematic => 32,
            _ => 0,
        };

        public int EffectiveDownsample => quality switch
        {
            MazeFocusQualityTier.Fast => 4,
            MazeFocusQualityTier.Balanced => 2,
            MazeFocusQualityTier.High => 2,
            MazeFocusQualityTier.Cinematic => 1,
            _ => 1,
        };
    }
}
