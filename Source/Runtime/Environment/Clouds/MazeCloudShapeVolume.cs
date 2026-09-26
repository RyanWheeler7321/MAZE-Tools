using UnityEngine;

namespace Maze
{
    [ExecuteAlways]
    public sealed class MazeCloudShapeVolume : MonoBehaviour
    {
        [SerializeField] private bool enabledShape = true;
        [SerializeField, Min(1f)] private float radiusX = 170f;
        [SerializeField, Min(1f)] private float radiusZ = 115f;
        [SerializeField, Min(1f)] private float height = 24f;
        [SerializeField, Range(0f, 2f)] private float density = 0.78f;
        [SerializeField, Range(0f, 1f)] private float softness = 0.58f;
        [SerializeField, Range(0f, 1f)] private float solidness = 0.32f;
        [SerializeField, Range(0f, 1f)] private float wispiness = 0.72f;
        [SerializeField, Range(0f, 1f)] private float granularity = 0.72f;
        [SerializeField] private int seed = 1;

        public bool EnabledShape => enabledShape && isActiveAndEnabled;

        public void AppendPoint(System.Collections.Generic.List<MazeLowerCloudPoint> points)
        {
            if (!EnabledShape)
            {
                return;
            }

            points.Add(new MazeLowerCloudPoint(
                transform.position,
                Mathf.Max(1f, radiusX * Mathf.Abs(transform.lossyScale.x)),
                Mathf.Max(1f, radiusZ * Mathf.Abs(transform.lossyScale.z)),
                Mathf.Max(1f, height * Mathf.Abs(transform.lossyScale.y)),
                density,
                softness,
                solidness,
                wispiness,
                granularity,
                seed));
        }

        private void OnValidate()
        {
            radiusX = Mathf.Max(1f, radiusX);
            radiusZ = Mathf.Max(1f, radiusZ);
            height = Mathf.Max(1f, height);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.22f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, new Vector3(radiusX * 2f, height * 2f, radiusZ * 2f));
            Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
