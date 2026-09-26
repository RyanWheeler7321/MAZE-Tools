using System.Collections.Generic;
using UnityEngine;

namespace Maze
{
    [ExecuteAlways]
    public sealed class MazeCloudScatterField : MonoBehaviour
    {
        [SerializeField] private bool enabledField = true;
        [SerializeField, Range(0, 128)] private int count = 12;
        [SerializeField, Min(1f)] private float areaRadius = 900f;
        [SerializeField] private int seed = 1001;
        [SerializeField] private bool randomizeEachRun;
        [SerializeField] private Vector2 heightOffsetRange = new(-120f, -40f);
        [SerializeField] private Vector2 radiusRange = new(120f, 360f);
        [SerializeField] private Vector2 heightRange = new(20f, 70f);
        [SerializeField] private Vector2 densityRange = new(0.35f, 0.9f);
        [SerializeField] private Vector2 softnessRange = new(0.25f, 0.85f);
        [SerializeField] private Vector2 solidnessRange = new(0.25f, 0.75f);
        [SerializeField] private Vector2 wispinessRange = new(0.1f, 0.75f);
        [SerializeField] private Vector2 granularityRange = new(0.15f, 0.85f);

        public bool EnabledField => enabledField && isActiveAndEnabled && count > 0;

        public void AppendPoints(List<MazeLowerCloudPoint> points, float mainCloudBaseHeight)
        {
            if (!EnabledField)
            {
                return;
            }

            var rng = new System.Random(randomizeEachRun && Application.isPlaying ? unchecked(seed + (int)Time.realtimeSinceStartup * 1009) : seed);
            for (var i = 0; i < count; i++)
            {
                points.Add(MakePoint(rng, i, transform.position, mainCloudBaseHeight));
            }
        }

        private MazeLowerCloudPoint MakePoint(System.Random rng, int index, Vector3 center, float mainCloudBaseHeight)
        {
            var angle = Next01(rng) * Mathf.PI * 2f;
            var distance = Mathf.Sqrt(Next01(rng)) * areaRadius;
            var radius = Lerp(radiusRange, Next01(rng));
            var height = Lerp(heightRange, Next01(rng));
            var position = new Vector3(
                center.x + Mathf.Cos(angle) * distance,
                mainCloudBaseHeight + Lerp(heightOffsetRange, Next01(rng)),
                center.z + Mathf.Sin(angle) * distance);

            return new MazeLowerCloudPoint(
                position,
                radius * Mathf.Lerp(0.75f, 1.35f, Next01(rng)),
                radius * Mathf.Lerp(0.75f, 1.35f, Next01(rng)),
                height,
                Lerp(densityRange, Next01(rng)),
                Lerp(softnessRange, Next01(rng)),
                Lerp(solidnessRange, Next01(rng)),
                Lerp(wispinessRange, Next01(rng)),
                Lerp(granularityRange, Next01(rng)),
                unchecked(seed * 92821 + index * 7919));
        }

        private static float Next01(System.Random rng)
        {
            return (float)rng.NextDouble();
        }

        private static float Lerp(Vector2 range, float t)
        {
            return Mathf.Lerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), t);
        }

        private void OnValidate()
        {
            areaRadius = Mathf.Max(1f, areaRadius);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.9f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, areaRadius);
        }
    }
}
