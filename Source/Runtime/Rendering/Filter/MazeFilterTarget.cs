using UnityEngine;

namespace Maze
{
    [ExecuteAlways]
    public sealed class MazeFilterTarget : MonoBehaviour
    {
        [Range(1f, 1024f)] public float screenRadiusPixels = 96f;
        [Range(0f, 512f)] public float softnessPixels = 64f;
        [Range(0f, 1f)] public float strength = 1f;

        public bool TryWriteScreenData(Camera camera, out Vector4 data)
        {
            data = Vector4.zero;
            if (!isActiveAndEnabled || camera == null || strength <= 0.001f)
            {
                return false;
            }

            var viewport = camera.WorldToViewportPoint(transform.position);
            if (viewport.z <= camera.nearClipPlane)
            {
                return false;
            }

            var minDimension = Mathf.Max(1f, Mathf.Min(camera.pixelWidth, camera.pixelHeight));
            var radius = Mathf.Max(0.0001f, screenRadiusPixels / minDimension);
            var softness = Mathf.Max(0f, softnessPixels / minDimension);
            data = new Vector4(viewport.x, viewport.y, radius, softness);
            return true;
        }
    }
}
