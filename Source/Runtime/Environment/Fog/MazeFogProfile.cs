using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Maze
{
    public enum MazeFogStyleMode
    {
        Smooth = 0,
        Sharper = 1,
        Graphic = 2,
    }

    public enum MazeFogQualityTier
    {
        Off = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Ultra = 4,
    }

    [System.Serializable]
    public sealed class MazeFogBand
    {
        public string name = "Fog Band";
        public bool enabled = true;
        [Range(0f, 1f)] public float density = 0.1f;
        [Min(0f)] public float startDistance = 0f;
        [FormerlySerializedAs("bottomColor")]
        [ColorUsage(false, true)] public Color color = new(0.86f, 0.52f, 0.42f, 1f);
    }

    [CreateAssetMenu(fileName = "MAZE_FogProfile", menuName = "MAZE/Environment/Fog Profile")]
    public sealed class MazeFogProfile : ScriptableObject
    {
        public const int MaxFogBands = 8;

        [Header("Core")]
        public bool fogEnabled = true;
        public MazeFogQualityTier quality = MazeFogQualityTier.Medium;
        public MazeFogStyleMode style = MazeFogStyleMode.Smooth;
        [Range(0f, 1f)] public float fogIntensity = 1f;
        [Min(1f)] public float maxDistance = 90f;
        [Range(0f, 1f)] public float maxOpacity = 0.35f;
        [Range(0f, 1f)] public float wispiness = 0f;
        [Range(0f, 1f)] public float wispinessGranularity = 0.45f;
        [Range(0f, 1f)] public float closeFogAmount = 0f;
        [Range(0f, 1f)] public float veryCloseFogAmount = 0f;
        [Range(0f, 1f)] public float closeFogGranularity = 0f;
        [Range(0f, 1f)] public float skyFogAmount = 0.2f;
        [Range(0f, 1f)] public float skyBlend = 0.25f;

        [Header("Distance Bands")]
        [Min(0f)] public float bandTransitionDistance = 10f;
        public List<MazeFogBand> bands = new()
        {
            new MazeFogBand { name = "Global Band", density = 0.1f, startDistance = 0f },
            new MazeFogBand { name = "Final Band", density = 0f, startDistance = 60f },
        };

        [Header("Optional Horizon Haze")]
        public bool horizonHazeEnabled = false;
        [Range(0f, 1f)] public float horizonHazeStrength = 0.35f;
        [Min(0f)] public float horizonHazeStartDistance = 1000f;
        [Min(0.001f)] public float horizonHazeFullDistance = 3000f;
        [Range(0.001f, 1f)] public float horizonHazeVerticalSize = 0.12f;

        [Header("Ground / Sky Bias")]
        [Tooltip("World-space height at the center of the smooth ground-to-sky fog transition.")]
        public float groundSkyHeight = 16f;
        [Tooltip("Vertical distance over which Ground Thickness blends into Sky Thickness.")]
        [Range(0.1f, 200f)] public float groundSkyTransition = 12f;
        [Tooltip("Fog density multiplier below Ground / Sky Height. This also shapes close fog.")]
        [Range(0f, 4f)] public float groundThickness = 1f;
        [Tooltip("Fog density multiplier above Ground / Sky Height.")]
        [Range(0f, 4f)] public float skyThickness = 1f;

        [Header("Volume Height Shape")]
        public float baseHeight = -3f;
        [Min(0.001f)] public float heightThickness = 24f;
        [Min(0.001f)] public float heightFalloff = 0.045f;
        [Range(0f, 2f)] public float groundBias = 0.6f;

        [Header("Texture")]
        [Min(0.1f)] public float nearNoiseSize = 35f;
        [Range(0f, 1f)] public float nearNoiseStrength = 0.15f;
        [Range(0.25f, 4f)] public float nearNoiseContrast = 1f;
        [Min(0.1f)] public float farNoiseSize = 280f;
        [Range(0f, 1f)] public float farNoiseStrength = 0.15f;
        [Range(0.25f, 4f)] public float farNoiseContrast = 1f;
        [Min(0f)] public float farNoiseStartDistance = 250f;
        [Min(0.001f)] public float farNoiseFullDistance = 1200f;
        public float noiseScrollSpeed = 0.03f;

        [Header("Volume Lighting")]
        [Range(0f, 1f)] public float anisotropy = 0.58f;
        [Range(0f, 3f)] public float lightResponse = 1.15f;
        [Range(0f, 1f)] public float ambientResponse = 0.18f;
        [Range(0f, 1f)] public float cloudShadowStrength = 0.35f;

        [HideInInspector]
        public bool lightShaftsEnabled = true;
        [HideInInspector]
        [ColorUsage(false, true)] public Color lightShaftColor = new(1f, 0.78f, 0.55f, 1f);
        [HideInInspector]
        [Range(0f, 2f)] public float lightShaftIntensity = 0.35f;
        [HideInInspector]
        [Range(0.1f, 0.995f)] public float lightShaftDecay = 0.94f;
        [HideInInspector]
        [Min(0.01f)] public float lightShaftWeight = 0.12f;
        [HideInInspector]
        [Min(0.001f)] public float lightShaftExposure = 0.08f;
        [HideInInspector]
        [Range(8, 96)] public int lightShaftSamples = 32;
        [HideInInspector]
        [Min(0.001f)] public float lightShaftBlurRadius = 0.12f;
        [HideInInspector]
        [Range(0f, 1f)] public float lightShaftOcclusion = 1f;
        [HideInInspector]
        [Range(0.25f, 4f)] public float lightShaftSharpness = 1f;
        [HideInInspector]
        [Range(0f, 1f)] public float lightShaftiness = 0f;
        [HideInInspector]
        [Range(0f, 1f)] public float lightShaftGranularity = 0f;
        [HideInInspector]
        [Min(0.001f)] public float lightShaftNoiseScale = 5f;
        [HideInInspector]
        public bool useManualLightShaftPosition = false;
        [HideInInspector]
        public Vector2 manualLightShaftViewportPosition = new(0.5f, 0.72f);

        [Header("Advanced")]
        [Range(4, 64)] public int stepCount = 16;
        public bool renderInSceneView = true;

        public bool RendersFog => fogEnabled && quality != MazeFogQualityTier.Off && fogIntensity > 0.0001f && HasConfiguredBands && (HasActiveBands || HasHorizonHaze);

        public bool HasHorizonHaze => horizonHazeEnabled && horizonHazeStrength > 0.0001f;

        public bool HasConfiguredBands
        {
            get
            {
                if (bands == null)
                {
                    return false;
                }

                var limit = Mathf.Min(bands.Count, MaxFogBands);
                for (var i = 0; i < limit; i++)
                {
                    var band = bands[i];
                    if (band != null && band.enabled)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public int ActiveBandCount
        {
            get
            {
                if (bands == null)
                {
                    return 0;
                }

                var count = 0;
                var limit = Mathf.Min(bands.Count, MaxFogBands);
                for (var i = 0; i < limit; i++)
                {
                    var band = bands[i];
                    if (band != null && band.enabled && band.density > 0.0001f)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool HasActiveBands
        {
            get
            {
                if (bands == null)
                {
                    return false;
                }

                var limit = Mathf.Min(bands.Count, MaxFogBands);
                for (var i = 0; i < limit; i++)
                {
                    var band = bands[i];
                    if (band != null && band.enabled && band.density > 0.0001f)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public int EffectiveStepCount
        {
            get
            {
                var limit = quality switch
                {
                    MazeFogQualityTier.Low => 8,
                    MazeFogQualityTier.Medium => 16,
                    MazeFogQualityTier.High => 32,
                    MazeFogQualityTier.Ultra => 64,
                    _ => 0,
                };
                return Mathf.Clamp(stepCount, 0, limit);
            }
        }

    }
}
