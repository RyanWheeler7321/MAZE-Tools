using UnityEngine;

namespace Maze
{
    public enum MazeBloomQualityTier
    {
        Off = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Ultra = 4,
    }

    [CreateAssetMenu(fileName = "MAZE_BloomProfile", menuName = "MAZE/Rendering/Bloom Profile")]
    public sealed class MazeBloomProfile : ScriptableObject
    {
        [Header("Bloom")]
        public bool bloomEnabled = true;
        public MazeBloomQualityTier quality = MazeBloomQualityTier.Medium;
        public bool renderInSceneView = true;
        [Min(0f)] public float threshold = 1.0f;
        [Range(0f, 1f)] public float softKnee = 0.55f;
        [Range(0f, 5f)] public float intensity = 0.65f;
        [Range(0f, 1f)] public float scatter = 0.7f;
        [ColorUsage(false, true)] public Color tint = Color.white;
        [Min(0f)] public float clamp = 12f;

        [Header("Bright Streaks")]
        public bool streaksEnabled = false;
        [Range(0f, 3f)] public float streakIntensity = 0.35f;
        [Range(0.25f, 8f)] public float streakStretch = 2.2f;
        [Range(0f, 360f)] public float streakAngle = 0f;
        [Range(0f, 1f)] public float streakChromatic = 0.15f;

        public bool RendersBloom => bloomEnabled && quality != MazeBloomQualityTier.Off && intensity > 0.0001f;
        public bool RendersStreaks => RendersBloom && streaksEnabled && streakIntensity > 0.0001f;

        public int EffectiveMipCount
        {
            get
            {
                return quality switch
                {
                    MazeBloomQualityTier.Low => 3,
                    MazeBloomQualityTier.Medium => 4,
                    MazeBloomQualityTier.High => 5,
                    MazeBloomQualityTier.Ultra => 6,
                    _ => 0,
                };
            }
        }
    }
}
