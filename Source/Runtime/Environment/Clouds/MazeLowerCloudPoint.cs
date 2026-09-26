using UnityEngine;

namespace Maze
{
    public readonly struct MazeLowerCloudPoint
    {
        public MazeLowerCloudPoint(Vector3 position, float radiusX, float radiusZ, float height, float density, float softness, float solidness, float wispiness, float granularity, int seed)
        {
            Position = position;
            RadiusX = Mathf.Max(1f, radiusX);
            RadiusZ = Mathf.Max(1f, radiusZ);
            Height = Mathf.Max(1f, height);
            Density = Mathf.Max(0f, density);
            Softness = Mathf.Clamp01(softness);
            Solidness = Mathf.Clamp01(solidness);
            Wispiness = Mathf.Clamp01(wispiness);
            Granularity = Mathf.Clamp01(granularity);
            Seed = seed;
        }

        public Vector3 Position { get; }
        public float RadiusX { get; }
        public float RadiusZ { get; }
        public float Height { get; }
        public float Density { get; }
        public float Softness { get; }
        public float Solidness { get; }
        public float Wispiness { get; }
        public float Granularity { get; }
        public int Seed { get; }
    }
}
