using System;
using UnityEngine;

namespace Maze
{
    [CreateAssetMenu(fileName = "MAZE_AtmosphereProfile", menuName = "MAZE/Environment/Atmosphere Profile")]
    public sealed class MazeAtmosphereProfile : ScriptableObject
    {
        public bool atmosphereEnabled = true;
        public SkySettings sky = new();
        public StarSettings stars = new();
        public AuroraSettings aurora = new();

        [Serializable]
        public sealed class SkySettings
        {
            [ColorUsage(false, true)] public Color zenithColor = new(0.12f, 0.27f, 0.65f, 1f);
            [ColorUsage(false, true)] public Color horizonColor = new(0.72f, 0.82f, 1f, 1f);
            [ColorUsage(false, true)] public Color groundColor = new(0.06f, 0.08f, 0.1f, 1f);
            [ColorUsage(false, true)] public Color sunHaloColor = new(1f, 0.8f, 0.52f, 1f);
            [Min(0.1f)] public float horizonExponent = 1.8f;
            [Min(0f)] public float sunHaloIntensity = 1.1f;
            [Min(0.01f)] public float sunHaloSize = 5f;
        }

        [Serializable]
        public sealed class StarSettings
        {
            public bool enabled = true;
            [Min(0f)] public float intensity = 0.65f;
            [Min(0.01f)] public float density = 1.2f;
            [Min(0f)] public float twinkle = 0.3f;
            [Min(0f)] public float driftSpeed = 0.005f;
            [ColorUsage(false, true)] public Color color = new(0.86f, 0.92f, 1f, 1f);
        }

        [Serializable]
        public sealed class AuroraSettings
        {
            public bool enabled = true;
            [ColorUsage(false, true)] public Color colorA = new(0.18f, 1f, 0.72f, 1f);
            [ColorUsage(false, true)] public Color colorB = new(0.18f, 0.55f, 1f, 1f);
            [Min(0f)] public float intensity = 0.45f;
            [Min(0.1f)] public float scale = 0.75f;
            [Min(0f)] public float speed = 0.08f;
            [Min(0f)] public float fluctuation = 0.6f;
            [Range(0f, 1f)] public float horizonFade = 0.2f;
        }
    }
}
