using System;
using UnityEngine;

namespace Maze
{
    [Flags]
    public enum MazeSceneFxPassMask
    {
        None = 0,
        AO = 1 << 0,
        Fog = 1 << 1,
        LightFX = 1 << 2,
        Focus = 1 << 3,
        Bloom = 1 << 4,
        Filter = 1 << 5,
        Tone = 1 << 6,
        Clouds = 1 << 7,
        All = AO | Fog | LightFX | Focus | Bloom | Filter | Tone | Clouds,
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("MAZE/Rendering/Scene FX Camera Policy")]
    public sealed class MazeSceneFxCameraPolicy : MonoBehaviour
    {
        [SerializeField] private MazeSceneFxPassMask allowedPasses = MazeSceneFxPassMask.All;

        public MazeSceneFxPassMask AllowedPasses
        {
            get => allowedPasses;
            set => allowedPasses = value;
        }

        public bool Allows(MazeSceneFxPassMask pass)
        {
            return (allowedPasses & pass) == pass;
        }

        public static bool Allows(Camera camera, MazeSceneFxPassMask pass)
        {
            return camera == null
                || !camera.TryGetComponent<MazeSceneFxCameraPolicy>(out var policy)
                || policy.Allows(pass);
        }
    }
}
