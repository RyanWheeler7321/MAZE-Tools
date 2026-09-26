using UnityEngine;

namespace Maze
{
    public static class MazeVectorColorField
    {
        public static float Sample(Vector2 normalizedPoint, float scale, float seed, Vector2 offset, float sharpness = 1f)
        {
            var point = normalizedPoint * Mathf.Max(0.05f, scale) + offset;
            point += new Vector2(seed * 0.173f, seed * 0.619f);
            var broad = ValueNoise(point);
            var secondary = ValueNoise(point * 0.53f + new Vector2(11.7f, -8.3f));
            var value = Mathf.Clamp01(broad * 0.72f + secondary * 0.28f);
            value = Mathf.SmoothStep(0f, 1f, value);
            return Mathf.Pow(value, Mathf.Max(0.1f, sharpness));
        }

        public static Color Patch(Color baseColor, Color patchColor, float strength, float sample)
        {
            var target = patchColor;
            target.a = baseColor.a;
            return Color.Lerp(baseColor, target, Mathf.Clamp01(strength) * Mathf.Clamp01(sample));
        }

        private static float ValueNoise(Vector2 point)
        {
            var cellX = Mathf.FloorToInt(point.x);
            var cellY = Mathf.FloorToInt(point.y);
            var localX = point.x - cellX;
            var localY = point.y - cellY;
            var smoothX = localX * localX * (3f - 2f * localX);
            var smoothY = localY * localY * (3f - 2f * localY);
            var bottom = Mathf.Lerp(Hash(cellX, cellY), Hash(cellX + 1, cellY), smoothX);
            var top = Mathf.Lerp(Hash(cellX, cellY + 1), Hash(cellX + 1, cellY + 1), smoothX);
            return Mathf.Lerp(bottom, top, smoothY);
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                var hash = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ 0xcb1ab31fu;
                hash ^= hash >> 13;
                hash *= 0x85ebca6bu;
                hash ^= hash >> 16;
                return (hash & 0x00ffffffu) / 16777215f;
            }
        }
    }
}
