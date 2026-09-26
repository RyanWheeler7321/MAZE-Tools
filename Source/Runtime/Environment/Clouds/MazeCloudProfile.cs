using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maze
{
    public enum MazeCloudQuality { Low = 12, Medium = 20, High = 28, Ultra = 40 }
    public enum MazeCloudPerformanceMode { Performance, HighestFidelity }

    [CreateAssetMenu(fileName = "MAZE_CloudProfile", menuName = "MAZE/Environment/Cloud Profile")]
    public sealed class MazeCloudProfile : ScriptableObject
    {
        public const int MaxCardCount = 256;

        public DistantCardSettings distantCards = new();
        public OverheadCardSettings overheadCards = new();
        public FakeShadowSettings fakeShadows = new();
        public VolumetricSettings volumetric = new();

        [HideInInspector] public Texture2D generatedCardAtlas;
        [HideInInspector] public Mesh generatedDistantMesh;
        [HideInInspector] public Mesh[] generatedOverheadMeshes = new Mesh[4];
        [HideInInspector] public string generatedSignature = string.Empty;

        public bool RendersAnyCards =>
            (distantCards.enabled && distantCards.cloudiness > 0.001f && distantCards.count > 0 && generatedDistantMesh != null)
            || (overheadCards.enabled && overheadCards.cloudiness > 0.001f && overheadCards.count > 0 && HasOverheadMesh);
        public bool RendersFakeShadows => fakeShadows.enabled && fakeShadows.cloudiness > 0.001f && fakeShadows.strength > 0.001f && fakeShadows.coverageTexture != null;
        public bool RendersVolumetric => volumetric.enabled && volumetric.density > 0.001f;

        public bool HasOverheadMesh
        {
            get
            {
                if (generatedOverheadMeshes == null) return false;
                for (var i = 0; i < generatedOverheadMeshes.Length; i++)
                {
                    if (generatedOverheadMeshes[i] != null) return true;
                }
                return false;
            }
        }

        [Serializable]
        public sealed class CardSource
        {
            public Texture2D texture;
            [Min(0.001f)] public float weight = 1f;
            [Min(0.01f)] public float scale = 1f;
            [Range(0f, 2f)] public float opacity = 1f;
            [HideInInspector] public Rect atlasRect = new(0f, 0f, 1f, 1f);
            [HideInInspector] public float aspect = 1f;
        }

        [Serializable]
        public sealed class CardMovementSettings
        {
            public Vector2 direction = new(1f, 0.2f);
            [Min(0f)] public float speed = 2f;
            [Range(0f, 1f)] public float uniformity = 0.75f;
            [Range(0f, 90f)] public float directionVariation = 18f;
            [Range(0f, 1f)] public float speedVariation = 0.25f;
            [Range(0f, 1f)] public float wander = 0.12f;
        }

        [Serializable]
        public sealed class CardLookSettings
        {
            [Range(0f, 2f)] public float opacity = 1f;
            [Range(-8f, 8f)] public float exposure = -0.35f;
            [Range(0f, 2f)] public float brightness = 0.75f;
            [Min(0.05f)] public float highlightClamp = 1.15f;
            [Range(0f, 1f)] public float sourceColorInfluence = 1f;
            [ColorUsage(false, true)] public Color shadowTint = new(0.58f, 0.62f, 0.68f, 1f);
            [ColorUsage(false, true)] public Color bodyTint = Color.white;
            [ColorUsage(false, true)] public Color highlightTint = new(1.08f, 1.04f, 0.98f, 1f);
            [Range(0f, 2f)] public float sunResponse = 0.2f;
            [Range(0f, 2f)] public float ambientResponse = 0.8f;
        }

        [Serializable]
        public sealed class DistantCardSettings
        {
            public bool enabled = true;
            [Range(0f, 1f)] public float cloudiness = 0.75f;
            [Range(0, MaxCardCount)] public int count = 64;
            public int seed = 6969;
            public bool renderInSceneView = true;
            public List<CardSource> sources = new();
            public Vector2 radiusRange = new(1500f, 3000f);
            public Vector2 heightRange = new(300f, 800f);
            public Vector2 widthRange = new(350f, 900f);
            public Vector2 rollRange = new(-6f, 6f);
            [Min(1f)] public float nearFadeStart = 900f;
            [Min(1f)] public float nearFadeEnd = 1250f;
            [Range(0f, 1f)] public float horizonFade = 0.2f;
            public CardMovementSettings movement = new() { speed = 1.8f };
            public CardLookSettings look = new();
        }

        [Serializable]
        public sealed class OverheadCardSettings
        {
            public bool enabled = true;
            [Range(0f, 1f)] public float cloudiness = 0.75f;
            [Range(0, MaxCardCount)] public int count = 48;
            public int seed = 6970;
            public bool renderInSceneView = true;
            public List<CardSource> sources = new();
            public Vector2 radiusRange = new(1200f, 3200f);
            public Vector2 heightByDistance = new(1500f, 500f);
            [Range(0f, 1f)] public float overheadReach = 0.2f;
            public Vector2 widthRange = new(450f, 1100f);
            public Vector2 rollRange = new(-8f, 8f);
            [Min(1f)] public float altitudeFadeDistance = 220f;
            public CardMovementSettings movement = new() { speed = 2.8f };
            public CardLookSettings look = new() { opacity = 0.85f };
        }

        [Serializable]
        public sealed class FakeShadowSettings
        {
            public bool enabled = true;
            [Range(0f, 1f)] public float cloudiness = 0.55f;
            public int seed = 6969;
            public bool renderInSceneView = true;
            public Texture2D coverageTexture;
            [Range(0f, 1f)] public float strength = 0.16f;
            [ColorUsage(false, true)] public Color tint = new(0.72f, 0.74f, 0.78f, 1f);
            [Min(1f)] public float worldSize = 620f;
            [Min(0f)] public float cloudAltitude = 650f;
            [Range(0f, 8f)] public float softness = 2.25f;
            [Range(0.1f, 4f)] public float contrast = 1.2f;
            [Range(0f, 1f)] public float threshold = 0.48f;
            public Vector2 windDirection = new(1f, 0.2f);
            [Min(0f)] public float windSpeed = 3.2f;
        }

        [Serializable]
        public sealed class VolumetricSettings
        {
            public bool enabled;
            public bool renderInSceneView = true;
            public Vector2 windDirection = new(1f, 0.2f);
            [Min(0f)] public float windSpeed = 4f;
            public MazeCloudPerformanceMode performanceMode = MazeCloudPerformanceMode.Performance;
            public MazeCloudQuality quality = MazeCloudQuality.High;
            [Range(8, 48)] public int performanceStepCount = 14;
            public bool performanceCheapLighting = true;
            [Range(0f, 1f)] public float granularityStrength = 0.18f;
            [Min(0.1f)] public float granularityScale = 12f;
            [Range(0f, 1f)] public float billowStrength = 0.18f;
            [Min(0.1f)] public float billowScale = 1.8f;
            [Range(0f, 1f)] public float coverage = 0.66f;
            [Min(0f)] public float density = 1.24f;
            [Min(0.00001f)] public float primaryScale = 0.0038f;
            [Min(0.00001f)] public float detailScale = 0.016f;
            [Min(1f)] public float baseHeight = 225f;
            [Min(1f)] public float thickness = 58f;
            [Min(0f)] public float detailStrength = 0.65f;
            [Min(0f)] public float ambientStrength = 0.82f;
            [Min(0f)] public float lightStrength = 1.45f;
            [Min(0f)] public float forwardScattering = 3.5f;
            [Min(0f)] public float ditherStrength = 1f;
            [Min(0f)] public float speed = 1f;
            [ColorUsage(false, true)] public Color ambientColor = new(0.6f, 0.69f, 0.78f, 1f);
            [ColorUsage(false, true)] public Color lightColor = new(1f, 0.94f, 0.87f, 1f);
            public VolumetricShadowSettings shadows = new();
        }

        [Serializable]
        public sealed class VolumetricShadowSettings
        {
            public bool enabled;
            public bool usePerformanceSampling = true;
            [Range(0f, 1f)] public float strength = 0.35f;
            [Min(0.00001f)] public float scale = 0.004f;
            [Min(0f)] public float speed = 1f;
            [Min(0.25f)] public float softness = 1.5f;
            [Min(0.1f)] public float contrast = 1.3f;
        }
    }
}
