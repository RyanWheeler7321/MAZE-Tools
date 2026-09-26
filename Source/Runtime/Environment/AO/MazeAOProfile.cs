using UnityEngine;

namespace Maze
{
    public enum MazeAOQualityTier
    {
        Off = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Ultra = 4,
    }

    public enum MazeAODebugMode
    {
        Off = 0,
        RawAO = 1,
        FinalAO = 2,
    }

    [CreateAssetMenu(fileName = "MAZE_AOProfile", menuName = "MAZE/Rendering/AO Profile")]
    public sealed class MazeAOProfile : ScriptableObject
    {
        [Header("Core")]
        public bool aoEnabled = true;
        public MazeAOQualityTier quality = MazeAOQualityTier.Medium;
        public MazeAODebugMode debugMode = MazeAODebugMode.Off;
        [Range(0f, 4f)] public float intensity = 1.15f;
        [Range(0.1f, 4f)] public float power = 1.15f;
        [Range(0f, 1f)] public float directLightingStrength = 0.18f;

        [Header("Shape")]
        [Range(1f, 80f)] public float radiusPixels = 22f;
        [Range(0.02f, 8f)] public float radiusWorld = 1.2f;
        [Range(0f, 1f)] public float bias = 0.055f;
        [Range(0.05f, 8f)] public float thickness = 2.2f;
        [Range(1f, 250f)] public float distanceFadeStart = 70f;
        [Range(1f, 500f)] public float distanceFadeEnd = 180f;

        [Header("Denoise")]
        [Range(0f, 1f)] public float denoiseStrength = 0.65f;
        [Range(0f, 4f)] public float denoiseRadiusPixels = 1.25f;
        [Range(0.01f, 20f)] public float depthRejection = 3f;

        [Header("Advanced")]
        [Range(4, 40)] public int sampleCount = 14;
        public bool renderInSceneView = true;

        public bool RendersAO => aoEnabled && quality != MazeAOQualityTier.Off && intensity > 0.001f && radiusPixels > 0.1f && radiusWorld > 0.001f;

        public int EffectiveSampleCount
        {
            get
            {
                var limit = quality switch
                {
                    MazeAOQualityTier.Low => 8,
                    MazeAOQualityTier.Medium => 14,
                    MazeAOQualityTier.High => 24,
                    MazeAOQualityTier.Ultra => 40,
                    _ => 0,
                };
                return Mathf.Clamp(sampleCount, 0, limit);
            }
        }
    }
}
