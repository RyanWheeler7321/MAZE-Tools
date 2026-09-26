using UnityEngine;

namespace Maze
{
    internal sealed class MazeSquareTransitionHookLinePattern
    {
        private struct FlashRect
        {
            public Rect Rect;
            public float Delay;
            public float Duration;
            public Color Color;
        }

        private FlashRect[] flashes = new FlashRect[0];
        private int elementCount;
        private float maxDelaySeconds;
        private int buildSerial;

        public int ElementCount => elementCount;
        public float MaxDelaySeconds => maxDelaySeconds;

        public void Build(MazeSquareTransitionProfile profile, Rect screen)
        {
            elementCount = 0;
            maxDelaySeconds = 0f;
            if (!ShouldBuild(profile))
            {
                return;
            }

            buildSerial++;
            var salt = profile.Seed ^ (buildSerial * 73856093) ^ Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f);
            var intensity = Mathf.Clamp01(profile.EffectIntensity);
            var hookCount = RandomRangeInt(salt + 17, 1, Mathf.RoundToInt(Mathf.Lerp(3f, 5f, intensity)));
            var greyCount = RandomRangeInt(salt + 43, Mathf.RoundToInt(Mathf.Lerp(7f, 11f, intensity)), Mathf.RoundToInt(Mathf.Lerp(16f, 28f, intensity)));
            var needed = hookCount * 2 + greyCount;
            if (flashes.Length != needed)
            {
                flashes = new FlashRect[needed];
            }

            var write = 0;
            var thickness = Mathf.Max(1.5f, profile.LineThicknessPixels * Mathf.Lerp(0.8f, 1.25f, intensity));
            var effectSeconds = Mathf.Max(0.08f, profile.OutCompleteSeconds);
            for (var i = 0; i < hookCount; i++)
            {
                var key = salt + i * 928371;
                var side = HashInt(key + 7919) & 3;
                var lengthBase = Mathf.Lerp(90f, 310f, Hash01(key + 3571)) * Mathf.Lerp(0.9f, 1.45f, intensity);
                var length = lengthBase * Mathf.Lerp(0.2f, 1.3f, Hash01(key + 3583));
                var hookLength = Mathf.Lerp(8f, 34f, Hash01(key + 1531));
                var inset = Mathf.Lerp(0.04f, 0.96f, Hash01(key + 6113));
                var duration = Mathf.Lerp(0.025f, 0.07f, Hash01(key + 3253));
                var delay = RandomDelay(key + 4447, effectSeconds, duration);
                CreateHook(profile, screen, side, inset, length, hookLength, thickness, delay, duration, key, ref write);
                maxDelaySeconds = Mathf.Max(maxDelaySeconds, delay + duration * 1.16f);
            }

            for (var i = 0; i < greyCount; i++)
            {
                var key = salt + i * 492113 + 1000003;
                var horizontal = Hash01(key + 19) > 0.28f;
                var wide = Mathf.Lerp(screen.width * 0.16f, screen.width * 0.58f, Hash01(key + 37));
                var longSide = Mathf.Max(160f, wide);
                var shortSide = Mathf.Lerp(8f, 38f, Hash01(key + 53));
                var size = horizontal ? new Vector2(longSide, shortSide) : new Vector2(shortSide, Mathf.Lerp(screen.height * 0.12f, screen.height * 0.42f, Hash01(key + 71)));
                var x = Mathf.Lerp(screen.xMin - size.x * 0.15f, screen.xMax - size.x * 0.85f, Hash01(key + 89));
                var y = Mathf.Lerp(screen.yMin - size.y * 0.15f, screen.yMax - size.y * 0.85f, Hash01(key + 107));
                var duration = Mathf.Lerp(0.012f, 0.04f, Hash01(key + 131));
                var delay = RandomDelay(key + 149, effectSeconds, duration);
                var alpha = Mathf.Lerp(0.14f, 0.42f, Hash01(key + 167));
                var warmth = Mathf.Lerp(0.025f, 0.075f, Hash01(key + 181));
                flashes[write++] = new FlashRect
                {
                    Rect = new Rect(x, y, size.x, size.y),
                    Delay = delay,
                    Duration = duration,
                    Color = new Color(warmth, warmth * 0.85f, warmth * 1.15f, alpha),
                };
                maxDelaySeconds = Mathf.Max(maxDelaySeconds, delay + duration);
            }

            elementCount = write;
        }

        public MazeTransitionVectorFrame Evaluate(int elementIndex, float time)
        {
            if (elementIndex < 0 || elementIndex >= elementCount)
            {
                return default;
            }

            var flashRect = flashes[elementIndex];
            var localTime = time - flashRect.Delay;
            if (localTime < 0f || localTime > flashRect.Duration)
            {
                return new MazeTransitionVectorFrame(default, Color.clear, Color.clear, 1f, false, MazeVectorTransitionElementKind.FillRect, 0f, 0f, 0f);
            }

            var t = Mathf.Clamp01(localTime / flashRect.Duration);
            var flash = 1f - Mathf.Abs(t * 2f - 1f);
            flash = Mathf.SmoothStep(0f, 1f, flash);
            var color = flashRect.Color;
            color.a *= flash;
            return new MazeTransitionVectorFrame(flashRect.Rect, color, Color.clear, 1f, color.a > 0.002f, MazeVectorTransitionElementKind.FillRect, 0f, 0f, 0f);
        }

        private static bool ShouldBuild(MazeSquareTransitionProfile profile)
        {
            return profile != null
                && profile.MotionPattern == MazeSquareTransitionPatternKind.ShutterApertureRects
                && profile.AccentColor.a > 0.001f
                && (profile.AccentColor.r > 0.001f || profile.AccentColor.g > 0.001f || profile.AccentColor.b > 0.001f);
        }

        private void CreateHook(MazeSquareTransitionProfile profile, Rect screen, int side, float inset, float length, float hookLength, float thickness, float delay, float duration, int key, ref int write)
        {
            var color = profile.AccentColor;
            var pulse = Mathf.Lerp(1.05f, 2.15f, Hash01(key + 7717));
            if (Hash01(key + 8819) > 0.82f)
            {
                pulse *= Mathf.Lerp(1.25f, 1.85f, Hash01(key + 8831));
            }
            color.r *= pulse;
            color.g *= Mathf.Lerp(0.75f, 1.05f, Hash01(key + 7723)) * pulse;
            color.b *= Mathf.Lerp(1.2f, 1.75f, Hash01(key + 7727)) * pulse;
            color.a *= Mathf.Lerp(0.28f, 1f, Hash01(key + 9901));

            Rect stem;
            Rect hookRect;
            var hookForward = Hash01(key + 4567) > 0.5f;
            switch (side)
            {
                case 0:
                    {
                        var y = Mathf.Lerp(screen.yMin, screen.yMax, inset);
                        stem = new Rect(screen.xMin, y - thickness * 0.5f, length, thickness);
                        var hookY = y + (hookForward ? 0f : -hookLength);
                        hookRect = new Rect(screen.xMin + length - thickness * 0.5f, hookY, thickness, hookLength);
                        break;
                    }
                case 1:
                    {
                        var y = Mathf.Lerp(screen.yMin, screen.yMax, inset);
                        stem = new Rect(screen.xMax - length, y - thickness * 0.5f, length, thickness);
                        var hookY = y + (hookForward ? 0f : -hookLength);
                        hookRect = new Rect(screen.xMax - length - thickness * 0.5f, hookY, thickness, hookLength);
                        break;
                    }
                case 2:
                    {
                        var x = Mathf.Lerp(screen.xMin, screen.xMax, inset);
                        stem = new Rect(x - thickness * 0.5f, screen.yMax - length, thickness, length);
                        var hookX = x + (hookForward ? 0f : -hookLength);
                        hookRect = new Rect(hookX, screen.yMax - length - thickness * 0.5f, hookLength, thickness);
                        break;
                    }
                default:
                    {
                        var x = Mathf.Lerp(screen.xMin, screen.xMax, inset);
                        stem = new Rect(x - thickness * 0.5f, screen.yMin, thickness, length);
                        var hookX = x + (hookForward ? 0f : -hookLength);
                        hookRect = new Rect(hookX, screen.yMin + length - thickness * 0.5f, hookLength, thickness);
                        break;
                    }
            }

            var stemColor = color;
            var hookColor = color;
            hookColor.a *= Mathf.Lerp(0.72f, 1f, Hash01(key + 3037));
            flashes[write++] = new FlashRect
            {
                Rect = stem,
                Delay = delay,
                Duration = duration,
                Color = stemColor,
            };
            flashes[write++] = new FlashRect
            {
                Rect = hookRect,
                Delay = delay + Mathf.Lerp(0f, duration * 0.16f, Hash01(key + 3041)),
                Duration = duration * Mathf.Lerp(0.72f, 1f, Hash01(key + 3049)),
                Color = hookColor,
            };
        }

        private static float RandomDelay(int key, float effectSeconds, float duration)
        {
            return Hash01(key) * Mathf.Max(0f, effectSeconds - duration);
        }

        private static int RandomRangeInt(int key, int minInclusive, int maxInclusive)
        {
            minInclusive = Mathf.Max(0, minInclusive);
            maxInclusive = Mathf.Max(minInclusive, maxInclusive);
            return minInclusive + Mathf.FloorToInt(Hash01(key) * (maxInclusive - minInclusive + 1));
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                return (HashUInt(value) & 0x00ffffff) / 16777215f;
            }
        }

        private static int HashInt(int value)
        {
            unchecked
            {
                return (int)(HashUInt(value) & 0x7fffffff);
            }
        }

        private static uint HashUInt(int value)
        {
            unchecked
            {
                var x = (uint)value;
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;
                return x;
            }
        }
    }
}
