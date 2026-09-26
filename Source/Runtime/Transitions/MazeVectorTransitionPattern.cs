using System.Collections.Generic;
using UnityEngine;

namespace Maze
{
    internal sealed class MazeVectorTransitionPattern
    {
        private struct Element
        {
            public Rect Start;
            public Rect Target;
            public Color StartFill;
            public Color TargetFill;
            public Color StartStroke;
            public Color TargetStroke;
            public float StrokeThickness;
            public float Delay;
            public float Duration;
            public MazeVectorTransitionElementKind Kind;
            public float CornerLength;
            public float DashLength;
            public float DashGap;
            public bool FlickerActive;
        }

        private readonly List<Element> elements = new(128);
        private Rect screenRect;
        private Rect coverRect;
        private Vector2 spreadOrigin;
        private int seed;
        private bool flickerMode;
        private float flickerDensity;
        private MazeTransitionFlickerWeighting flickerWeighting;
        private MazeVectorTransitionProfile activeProfile;

        public int ElementCount => elements.Count;
        public float MaxDelaySeconds { get; private set; }
        public Rect CoverBounds => coverRect;

        public float GetDelaySeconds(int index)
        {
            return index >= 0 && index < elements.Count ? elements[index].Delay : 0f;
        }

        public void Build(MazeVectorTransitionProfile profile, Rect screen, MazeTransitionContext context)
        {
            elements.Clear();
            screenRect = screen;
            var overscan = profile.OverscanPixels;
            coverRect = new Rect(screen.xMin - overscan, screen.yMin - overscan, screen.width + overscan * 2f, screen.height + overscan * 2f);
            spreadOrigin = OriginPoint(profile.InOrigin, profile, context);
            MaxDelaySeconds = 0f;
            seed = profile.RandomizesPerPlay && context.HasRandomSeed ? context.RandomSeed : profile.Seed;
            flickerMode = context.Flicker;
            flickerDensity = context.FlickerDensity > 0f ? Mathf.Clamp01(context.FlickerDensity) : 0.42f;
            flickerWeighting = ResolveFlickerWeighting(context.FlickerWeighting, seed);
            activeProfile = profile;

            switch (profile.Pattern)
            {
                case MazeVectorTransitionPatternKind.BlackPageTurn:
                    BuildBlackPageTurn(profile);
                    break;
                case MazeVectorTransitionPatternKind.RuneGuillotine:
                    BuildRuneGuillotine(profile);
                    break;
                case MazeVectorTransitionPatternKind.BoxIris:
                    BuildBoxIris(profile, context);
                    break;
                case MazeVectorTransitionPatternKind.GridWireframeCollapse:
                    BuildGridWireframeCollapse(profile);
                    break;
                case MazeVectorTransitionPatternKind.DiagonalDraftingWipe:
                    BuildDiagonalDraftingWipe(profile);
                    break;
                case MazeVectorTransitionPatternKind.DoorLattice:
                    BuildDoorLattice(profile);
                    break;
                case MazeVectorTransitionPatternKind.PulseFrameCrush:
                    BuildPulseFrameCrush(profile, context);
                    break;
                case MazeVectorTransitionPatternKind.GlyphSlotMachine:
                    BuildGlyphSlotMachine(profile);
                    break;
                case MazeVectorTransitionPatternKind.HardCutAfterimage:
                    BuildHardCutAfterimage(profile);
                    break;
                default:
                    BuildVectorSigilSnap(profile, context);
                    break;
            }

            if (flickerMode)
            {
                ApplyFlicker(profile);
            }
        }

        public MazeTransitionVectorFrame Evaluate(MazeVectorTransitionProfile profile, int index, float time, MazeTransitionDirection direction)
        {
            if (index < 0 || index >= elements.Count)
            {
                return default;
            }

            var element = elements[index];
            if (flickerMode && !element.FlickerActive)
            {
                return default;
            }

            var coveredSeconds = Mathf.Max(0.01f, MaxDelaySeconds + profile.InSeconds);
            var outSeconds = Mathf.Max(0.01f, MaxDelaySeconds + profile.OutSeconds);
            var timeline = flickerMode || direction == MazeTransitionDirection.In
                ? time
                : Mathf.Clamp01(1f - time / outSeconds) * coveredSeconds;
            var duration = element.Duration + (element.Kind == MazeVectorTransitionElementKind.FillRect ? 0f : profile.AccentLingeringSeconds);
            var t = Mathf.Clamp01((timeline - element.Delay) / Mathf.Max(0.01f, duration));
            if (flickerMode && (timeline <= element.Delay || timeline > element.Delay + duration))
            {
                return default;
            }

            var moveT = ApplyEase(t, profile.MotionEase);
            var fadeT = ApplyEase(t, profile.FadeEase);
            var rect = LerpRect(element.Start, element.Target, moveT);
            var fill = Color.LerpUnclamped(element.StartFill, element.TargetFill, fadeT);
            var stroke = Color.LerpUnclamped(element.StartStroke, element.TargetStroke, fadeT);
            if (flickerMode)
            {
                var envelope = FlickerEnvelope(t);
                fill = Color.Lerp(fill, element.TargetFill, Mathf.Clamp01((1f - envelope) * 0.35f));
                stroke = Color.Lerp(stroke, element.TargetStroke, Mathf.Clamp01((1f - envelope) * 0.35f));
                fill.a *= envelope;
                stroke.a *= envelope;
            }

            var visible = (fill.a > 0.001f || stroke.a > 0.001f) && rect.width > 0.5f && rect.height > 0.5f;
            return new MazeTransitionVectorFrame(
                rect,
                fill,
                stroke,
                element.StrokeThickness,
                visible,
                element.Kind,
                element.CornerLength,
                element.DashLength,
                element.DashGap,
                1f);
        }

        private void BuildVectorSigilSnap(MazeVectorTransitionProfile profile, MazeTransitionContext context)
        {
            AddCoverFill(profile, profile.FillDelayNormalized, 0.34f);
            var center = OriginPoint(profile.InOrigin, profile, context);
            var maxSize = Mathf.Max(screenRect.width, screenRect.height);
            var rings = Mathf.Max(3, profile.SecondaryCount + 2);
            for (var i = 0; i < rings; i++)
            {
                var k = (i + 1f) / rings;
                var size = new Vector2(Mathf.Lerp(maxSize * 0.35f, coverRect.width * 0.95f, k), Mathf.Lerp(maxSize * 0.22f, coverRect.height * 0.9f, k));
                var target = RectCentered(screenRect.center, size);
                var start = RectCentered(center, size * 0.18f);
                var color = Accent(profile, 1f - i * 0.07f);
                Add(start, target, Clear(color), color, MazeVectorTransitionElementKind.StrokeRect, Delay(profile, i, rings, 0.42f), profile.InSeconds * 0.48f, profile.LineThicknessPixels, 0f);
            }

            AddLine(Vector2.up, screenRect.center.x, coverRect.height, profile, 0.08f, profile.AccentColor);
            AddLine(Vector2.right, screenRect.center.y, coverRect.width, profile, 0.11f, profile.SecondaryColor);
            Add(RectCentered(screenRect.center, coverRect.size * 0.92f), RectCentered(screenRect.center, coverRect.size * 0.92f), Clear(profile.AccentColor), profile.AccentColor, MazeVectorTransitionElementKind.CornerAccents, profile.DelaySpanSeconds * 0.35f, profile.InSeconds * 0.34f, profile.LineThicknessPixels, Mathf.Min(screenRect.width, screenRect.height) * 0.09f);
        }

        private void BuildBlackPageTurn(MazeVectorTransitionProfile profile)
        {
            var start = coverRect;
            start.center += EdgeOffset(profile.InOrigin, coverRect.size * 1.05f);
            Add(start, coverRect, Clear(profile.FillColor), profile.FillColor, MazeVectorTransitionElementKind.FillRect, 0f, profile.InSeconds * 0.72f, profile.LineThicknessPixels, 0f);
            AddCoverFill(profile, 0.72f, 0.18f);
            var trails = Mathf.Max(3, profile.SecondaryCount + 2);
            var vertical = Mathf.Abs(EdgeOffset(profile.InOrigin, Vector2.one).x) > 0.1f;
            for (var i = 0; i < trails; i++)
            {
                var p = (i + 1f) / (trails + 1f);
                var rect = vertical
                    ? new Rect(Mathf.Lerp(screenRect.xMin, screenRect.xMax, p), coverRect.yMin, profile.AccentThicknessPixels, coverRect.height)
                    : new Rect(coverRect.xMin, Mathf.Lerp(screenRect.yMin, screenRect.yMax, p), coverRect.width, profile.AccentThicknessPixels);
                var moving = rect;
                moving.center += EdgeOffset(profile.InOrigin, new Vector2(profile.SlantPixels, profile.SlantPixels));
                Add(moving, rect, Clear(profile.AccentColor), Accent(profile, 0.85f), MazeVectorTransitionElementKind.FillRect, Delay(profile, i, trails, 0.5f), profile.InSeconds * 0.38f, profile.AccentThicknessPixels, 0f);
            }
        }

        private void BuildRuneGuillotine(MazeVectorTransitionProfile profile)
        {
            AddCoverFill(profile, 0.64f, 0.24f);
            var blades = Mathf.Max(4, profile.PrimaryCount);
            for (var i = 0; i < blades; i++)
            {
                var horizontal = (i & 1) == 0;
                var p = (i + 0.5f) / blades;
                var target = horizontal
                    ? new Rect(coverRect.xMin, Mathf.Lerp(screenRect.yMin, screenRect.yMax, p) - profile.LineThicknessPixels * 0.5f, coverRect.width, profile.LineThicknessPixels)
                    : new Rect(Mathf.Lerp(screenRect.xMin, screenRect.xMax, p) - profile.LineThicknessPixels * 0.5f, coverRect.yMin, profile.LineThicknessPixels, coverRect.height);
                var start = target;
                start.center += horizontal ? Vector2.up * coverRect.height * 0.35f : Vector2.left * coverRect.width * 0.35f;
                Add(start, target, Clear(profile.AccentColor), Accent(profile, 1f), MazeVectorTransitionElementKind.FillRect, Delay(profile, i, blades, 0.35f), profile.InSeconds * 0.28f, profile.LineThicknessPixels, 0f);

                var band = target;
                if (horizontal)
                {
                    band.height = coverRect.height / blades + profile.GapPixels;
                    band.y = target.center.y - band.height * 0.5f;
                }
                else
                {
                    band.width = coverRect.width / blades + profile.GapPixels;
                    band.x = target.center.x - band.width * 0.5f;
                }
                Add(target, band, Clear(profile.FillColor), profile.FillColor, MazeVectorTransitionElementKind.FillRect, profile.InSeconds * 0.32f + Delay(profile, i, blades, 0.3f), profile.InSeconds * 0.36f, profile.LineThicknessPixels, 0f);
            }
        }

        private void BuildBoxIris(MazeVectorTransitionProfile profile, MazeTransitionContext context)
        {
            var center = OriginPoint(profile.InOrigin, profile, context);
            var tiny = RectCentered(center, Vector2.one * Mathf.Max(2f, profile.LineThicknessPixels));
            Add(tiny, coverRect, Clear(profile.FillColor), profile.FillColor, MazeVectorTransitionElementKind.FillRect, profile.InSeconds * profile.FillDelayNormalized, profile.InSeconds * 0.42f, profile.LineThicknessPixels, 0f);
            var rings = Mathf.Max(4, profile.PrimaryCount);
            for (var i = 0; i < rings; i++)
            {
                var k = (i + 1f) / rings;
                var target = RectCentered(center, Vector2.Lerp(Vector2.one * 24f, coverRect.size, k));
                var start = RectCentered(center, target.size * 1.28f);
                Add(start, target, Clear(profile.AccentColor), Accent(profile, 1f - k * 0.35f), MazeVectorTransitionElementKind.StrokeRect, Delay(profile, rings - i, rings, 0.45f), profile.InSeconds * 0.34f, profile.AccentThicknessPixels + i % 2, 0f);
            }
        }

        private void BuildGridWireframeCollapse(MazeVectorTransitionProfile profile)
        {
            AddCoverFill(profile, 0.68f, 0.22f);
            var columns = Mathf.Max(4, profile.PrimaryCount);
            var rows = Mathf.Max(3, profile.SecondaryCount + 3);
            for (var c = 0; c <= columns; c++)
            {
                var x = Mathf.Lerp(coverRect.xMin, coverRect.xMax, c / (float)columns);
                var line = new Rect(x - profile.AccentThicknessPixels * 0.5f, coverRect.yMin, profile.AccentThicknessPixels, coverRect.height);
                var start = RectCentered(screenRect.center, new Vector2(profile.AccentThicknessPixels, coverRect.height * 0.1f));
                Add(start, line, Clear(profile.AccentColor), profile.AccentColor, MazeVectorTransitionElementKind.FillRect, Delay(profile, c, columns, 0.35f), profile.InSeconds * 0.42f, profile.AccentThicknessPixels, 0f);
            }
            for (var r = 0; r <= rows; r++)
            {
                var y = Mathf.Lerp(coverRect.yMin, coverRect.yMax, r / (float)rows);
                var line = new Rect(coverRect.xMin, y - profile.AccentThicknessPixels * 0.5f, coverRect.width, profile.AccentThicknessPixels);
                var start = RectCentered(screenRect.center, new Vector2(coverRect.width * 0.1f, profile.AccentThicknessPixels));
                Add(start, line, Clear(profile.SecondaryColor), profile.SecondaryColor, MazeVectorTransitionElementKind.FillRect, Delay(profile, r, rows, 0.35f), profile.InSeconds * 0.42f, profile.AccentThicknessPixels, 0f);
            }
        }

        private void BuildDiagonalDraftingWipe(MazeVectorTransitionProfile profile)
        {
            AddCoverFill(profile, 0.74f, 0.2f);
            var steps = Mathf.Max(6, profile.PrimaryCount);
            var stepW = coverRect.width / steps * 1.45f;
            for (var i = 0; i < steps; i++)
            {
                var x = coverRect.xMin + i * coverRect.width / steps;
                var target = new Rect(x - stepW * 0.2f, coverRect.yMin, stepW, coverRect.height);
                var start = target;
                start.center += new Vector2(-coverRect.width * 0.35f, coverRect.height * 0.35f);
                Add(start, target, Clear(profile.FillColor), profile.FillColor, MazeVectorTransitionElementKind.FillRect, Delay(profile, i, steps, 0.75f), profile.InSeconds * 0.34f, profile.LineThicknessPixels, 0f);
                var mark = new Rect(target.xMin + profile.LineThicknessPixels * 2f, coverRect.yMax - (i % 4 + 1) * profile.LineThicknessPixels * 5f, profile.LineThicknessPixels * 7f, profile.AccentThicknessPixels);
                Add(mark, mark, Clear(profile.AccentColor), profile.AccentColor, MazeVectorTransitionElementKind.FillRect, Delay(profile, i, steps, 0.75f) + profile.InSeconds * 0.08f, profile.InSeconds * 0.2f, profile.AccentThicknessPixels, 0f);
            }
        }

        private void BuildDoorLattice(MazeVectorTransitionProfile profile)
        {
            AddCoverFill(profile, 0.78f, 0.18f);
            var bars = Mathf.Max(4, profile.PrimaryCount);
            for (var i = 0; i < bars; i++)
            {
                var p = (i + 0.5f) / bars;
                var w = coverRect.width / bars * 0.62f;
                var h = coverRect.height / bars * 0.62f;
                var vertical = new Rect(Mathf.Lerp(coverRect.xMin, coverRect.xMax, p) - w * 0.5f, coverRect.yMin, w, coverRect.height);
                var horizontal = new Rect(coverRect.xMin, Mathf.Lerp(coverRect.yMin, coverRect.yMax, p) - h * 0.5f, coverRect.width, h);
                var vStart = vertical;
                vStart.center += ((i & 1) == 0 ? Vector2.left : Vector2.right) * coverRect.width * 0.55f;
                var hStart = horizontal;
                hStart.center += ((i & 1) == 0 ? Vector2.up : Vector2.down) * coverRect.height * 0.55f;
                Add(vStart, vertical, Clear(profile.FillColor), profile.FillColor, MazeVectorTransitionElementKind.FillRect, Delay(profile, i, bars, 0.5f), profile.InSeconds * 0.42f, profile.LineThicknessPixels, 0f);
                Add(hStart, horizontal, Clear(profile.SecondaryColor), Muted(profile.FillColor, 0.88f), MazeVectorTransitionElementKind.FillRect, Delay(profile, bars - i, bars, 0.5f), profile.InSeconds * 0.42f, profile.LineThicknessPixels, 0f);
            }
        }

        private void BuildPulseFrameCrush(MazeVectorTransitionProfile profile, MazeTransitionContext context)
        {
            var center = OriginPoint(profile.InOrigin, profile, context);
            Add(RectCentered(center, coverRect.size * 0.05f), coverRect, Clear(profile.FillColor), profile.FillColor, MazeVectorTransitionElementKind.FillRect, profile.InSeconds * profile.FillDelayNormalized, profile.InSeconds * 0.34f, profile.LineThicknessPixels, 0f);
            var frames = Mathf.Max(4, profile.PrimaryCount);
            for (var i = 0; i < frames; i++)
            {
                var k = i / (float)Mathf.Max(1, frames - 1);
                var start = RectCentered(screenRect.center, coverRect.size * Mathf.Lerp(1.15f, 0.35f, k));
                var target = RectCentered(center, coverRect.size * Mathf.Lerp(0.72f, 0.06f, k));
                Add(start, target, Clear(profile.AccentColor), Accent(profile, Mathf.Lerp(0.35f, 1f, k)), MazeVectorTransitionElementKind.StrokeRect, Delay(profile, i, frames, 0.42f), profile.InSeconds * 0.32f, profile.LineThicknessPixels + k * profile.LineThicknessPixels, 0f);
            }
        }

        private void BuildGlyphSlotMachine(MazeVectorTransitionProfile profile)
        {
            AddCoverFill(profile, 0.72f, 0.18f);
            var rows = Mathf.Max(4, profile.PrimaryCount);
            var blocks = Mathf.Max(4, profile.SecondaryCount + 4);
            var rowH = coverRect.height / rows;
            var blockW = coverRect.width / blocks;
            for (var r = 0; r < rows; r++)
            {
                var rowDir = (r & 1) == 0 ? -1f : 1f;
                for (var b = 0; b < blocks; b++)
                {
                    var rect = new Rect(coverRect.xMin + b * blockW + blockW * 0.16f, coverRect.yMin + r * rowH + rowH * 0.18f, blockW * 0.68f, rowH * 0.58f);
                    var start = rect;
                    start.center += Vector2.right * rowDir * coverRect.width * 0.22f;
                    var color = ((b + r) & 1) == 0 ? profile.AccentColor : profile.SecondaryColor;
                    Add(start, rect, Clear(color), color, MazeVectorTransitionElementKind.FillRect, Delay(profile, r * blocks + b, rows * blocks, 0.75f), profile.InSeconds * 0.22f, profile.LineThicknessPixels, 0f);
                }
            }
        }

        private void BuildHardCutAfterimage(MazeVectorTransitionProfile profile)
        {
            Add(coverRect, coverRect, Clear(profile.FillColor), profile.FillColor, MazeVectorTransitionElementKind.FillRect, 0f, Mathf.Max(0.05f, profile.InSeconds * 0.16f), profile.LineThicknessPixels, 0f);
            var fragments = Mathf.Max(8, profile.PrimaryCount + profile.SecondaryCount);
            for (var i = 0; i < fragments; i++)
            {
                var horizontal = Hash01(seed + i * 17) > 0.5f;
                var length = Mathf.Lerp(50f, 260f, Hash01(seed + i * 31)) * Mathf.Max(0.6f, profile.Intensity);
                var thickness = Mathf.Lerp(profile.AccentThicknessPixels, profile.LineThicknessPixels, Hash01(seed + i * 43));
                var center = new Vector2(Mathf.Lerp(screenRect.xMin, screenRect.xMax, Hash01(seed + i * 59)), Mathf.Lerp(screenRect.yMin, screenRect.yMax, Hash01(seed + i * 71)));
                var rect = RectCentered(center, horizontal ? new Vector2(length, thickness) : new Vector2(thickness, length));
                var start = rect;
                start.center += new Vector2(HashSigned(seed + i * 83), HashSigned(seed + i * 97)) * profile.JitterPixels;
                var color = ((i & 1) == 0 ? profile.AccentColor : profile.SecondaryColor);
                Add(start, rect, Clear(color), color, MazeVectorTransitionElementKind.FillRect, profile.InSeconds * 0.12f + Delay(profile, i, fragments, 0.55f), profile.InSeconds * 0.18f, thickness, 0f);
            }
        }

        private void AddCoverFill(MazeVectorTransitionProfile profile, float delayNormalized, float durationNormalized)
        {
            var delay = profile.InSeconds * Mathf.Clamp01(delayNormalized);
            var duration = Mathf.Max(0.03f, profile.InSeconds * Mathf.Max(0.05f, durationNormalized));
            Add(coverRect, coverRect, Clear(profile.FillColor), profile.FillColor, MazeVectorTransitionElementKind.FillRect, delay, duration, profile.LineThicknessPixels, 0f);
        }

        private void AddLine(Vector2 axis, float coordinate, float length, MazeVectorTransitionProfile profile, float delay, Color color)
        {
            Rect target;
            if (Mathf.Abs(axis.y) > 0.5f)
            {
                target = new Rect(coordinate - profile.AccentThicknessPixels * 0.5f, coverRect.yMin, profile.AccentThicknessPixels, length);
            }
            else
            {
                target = new Rect(coverRect.xMin, coordinate - profile.AccentThicknessPixels * 0.5f, length, profile.AccentThicknessPixels);
            }
            Add(RectCentered(screenRect.center, target.size * 0.05f), target, Clear(color), color, MazeVectorTransitionElementKind.FillRect, delay, profile.InSeconds * 0.35f, profile.AccentThicknessPixels, 0f);
        }

        private void Add(Rect start, Rect target, Color startColor, Color targetColor, MazeVectorTransitionElementKind kind, float delay, float duration, float thickness, float cornerLength)
        {
            Add(start, target, startColor, targetColor, Clear(startColor), kind == MazeVectorTransitionElementKind.FillRect ? Clear(targetColor) : targetColor, kind, delay, duration, thickness, cornerLength);
        }

        private void Add(Rect start, Rect target, Color startFill, Color targetFill, Color startStroke, Color targetStroke, MazeVectorTransitionElementKind kind, float delay, float duration, float thickness, float cornerLength)
        {
            var element = new Element
            {
                Start = start,
                Target = target,
                StartFill = kind == MazeVectorTransitionElementKind.FillRect ? startFill : Clear(startFill),
                TargetFill = kind == MazeVectorTransitionElementKind.FillRect ? targetFill : Clear(targetFill),
                StartStroke = kind == MazeVectorTransitionElementKind.FillRect ? Clear(startStroke) : startStroke,
                TargetStroke = kind == MazeVectorTransitionElementKind.FillRect ? Clear(targetStroke) : targetStroke,
                StrokeThickness = Mathf.Max(0.5f, thickness),
                Delay = Mathf.Max(0f, delay) + SpreadDelay(activeProfile, target.center),
                Duration = Mathf.Max(0.01f, duration),
                Kind = kind,
                CornerLength = Mathf.Max(0f, cornerLength),
                DashLength = Mathf.Max(4f, thickness * 5f),
                DashGap = Mathf.Max(2f, thickness * 3f),
                FlickerActive = true,
            };
            MaxDelaySeconds = Mathf.Max(MaxDelaySeconds, element.Delay);
            elements.Add(element);
        }

        private void ApplyFlicker(MazeVectorTransitionProfile profile)
        {
            MaxDelaySeconds = 0f;
            for (var i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                var isCoverFill = element.Kind == MazeVectorTransitionElementKind.FillRect
                    && element.Target.width >= screenRect.width * 0.92f
                    && element.Target.height >= screenRect.height * 0.92f;
                var chance = Mathf.Clamp01(flickerDensity * FlickerWeightFor(flickerWeighting, element.Target.center, screenRect));
                element.FlickerActive = !isCoverFill && Hash01(seed + i * 4513) <= chance;
                element.Delay = Hash01(seed + i * 8111) * Mathf.Max(0.08f, profile.DelaySpanSeconds);
                element.Duration = Mathf.Min(element.Duration, Mathf.Max(0.04f, profile.InSeconds * Mathf.Lerp(0.32f, 0.62f, Hash01(seed + i * 2221))));
                if (element.FlickerActive)
                {
                    MaxDelaySeconds = Mathf.Max(MaxDelaySeconds, element.Delay);
                }
                elements[i] = element;
            }
        }

        private float Delay(MazeVectorTransitionProfile profile, int index, int count, float scale)
        {
            if (count <= 1 || profile.DelaySpanSeconds <= 0f)
            {
                return 0f;
            }
            var ordered = index / (float)(count - 1);
            var random = Hash01(seed + index * 92821);
            var orderBalance = profile.OrderedDelayBalance;
            return Mathf.Lerp(random, ordered, orderBalance) * profile.DelaySpanSeconds * scale;
        }

        private float SpreadDelay(MazeVectorTransitionProfile profile, Vector2 center)
        {
            if (profile.DirectionalSpreadSeconds <= 0f || flickerMode || profile.Pattern == MazeVectorTransitionPatternKind.HardCutAfterimage)
            {
                return 0f;
            }

            var metric = SpreadMetric(profile.SpreadDirection, center, profile);
            return ApplyEase(metric, profile.SpreadEase) * profile.DirectionalSpreadSeconds;
        }

        private float SpreadMetric(MazeTransitionSpreadDirection direction, Vector2 center, MazeVectorTransitionProfile profile)
        {
            if (direction == MazeTransitionSpreadDirection.RandomSide)
            {
                direction = RandomSpreadDirection(seed);
            }

            var x = Mathf.InverseLerp(screenRect.xMin, screenRect.xMax, center.x);
            var y = Mathf.InverseLerp(screenRect.yMin, screenRect.yMax, center.y);
            return direction switch
            {
                MazeTransitionSpreadDirection.LeftToRight => x,
                MazeTransitionSpreadDirection.RightToLeft => 1f - x,
                MazeTransitionSpreadDirection.BottomToTop => y,
                MazeTransitionSpreadDirection.TopToBottom => 1f - y,
                MazeTransitionSpreadDirection.TopLeftToBottomRight => (x + (1f - y)) * 0.5f,
                MazeTransitionSpreadDirection.TopRightToBottomLeft => ((1f - x) + (1f - y)) * 0.5f,
                MazeTransitionSpreadDirection.BottomLeftToTopRight => (x + y) * 0.5f,
                MazeTransitionSpreadDirection.BottomRightToTopLeft => ((1f - x) + y) * 0.5f,
                MazeTransitionSpreadDirection.CenterOut => Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f)) / 0.70710678f),
                MazeTransitionSpreadDirection.EdgesIn => Mathf.Clamp01(Mathf.Min(Mathf.Min(x, 1f - x), Mathf.Min(y, 1f - y)) * 2f),
                MazeTransitionSpreadDirection.CornersIn => Mathf.Clamp01(Mathf.Min(Mathf.Min(x + y, (1f - x) + y), Mathf.Min(x + (1f - y), (1f - x) + (1f - y)))),
                _ => Mathf.Clamp01(Vector2.Distance(center, spreadOrigin) / Mathf.Max(1f, screenRect.size.magnitude)),
            };
        }

        private static MazeTransitionSpreadDirection RandomSpreadDirection(int seed)
        {
            var pick = HashInt(seed ^ 0x43f3a1) % 8;
            return pick switch
            {
                1 => MazeTransitionSpreadDirection.RightToLeft,
                2 => MazeTransitionSpreadDirection.BottomToTop,
                3 => MazeTransitionSpreadDirection.TopToBottom,
                4 => MazeTransitionSpreadDirection.TopLeftToBottomRight,
                5 => MazeTransitionSpreadDirection.TopRightToBottomLeft,
                6 => MazeTransitionSpreadDirection.BottomLeftToTopRight,
                7 => MazeTransitionSpreadDirection.BottomRightToTopLeft,
                _ => MazeTransitionSpreadDirection.LeftToRight,
            };
        }

        private Vector2 OriginPoint(MazeTransitionOrigin origin, MazeVectorTransitionProfile profile, MazeTransitionContext context)
        {
            if (origin == MazeTransitionOrigin.Point && context.HasScreenPoint)
            {
                return context.ScreenPoint;
            }

            if (origin == MazeTransitionOrigin.Point)
            {
                return new Vector2(
                    Mathf.Lerp(screenRect.xMin, screenRect.xMax, profile.CustomPointNormalized.x),
                    Mathf.Lerp(screenRect.yMin, screenRect.yMax, profile.CustomPointNormalized.y));
            }

            return origin switch
            {
                MazeTransitionOrigin.Left => new Vector2(screenRect.xMin, screenRect.center.y),
                MazeTransitionOrigin.Right => new Vector2(screenRect.xMax, screenRect.center.y),
                MazeTransitionOrigin.Top => new Vector2(screenRect.center.x, screenRect.yMax),
                MazeTransitionOrigin.Bottom => new Vector2(screenRect.center.x, screenRect.yMin),
                MazeTransitionOrigin.TopLeft => new Vector2(screenRect.xMin, screenRect.yMax),
                MazeTransitionOrigin.TopRight => new Vector2(screenRect.xMax, screenRect.yMax),
                MazeTransitionOrigin.BottomLeft => new Vector2(screenRect.xMin, screenRect.yMin),
                MazeTransitionOrigin.BottomRight => new Vector2(screenRect.xMax, screenRect.yMin),
                _ => screenRect.center,
            };
        }

        private Vector2 EdgeOffset(MazeTransitionOrigin origin, Vector2 size)
        {
            return origin switch
            {
                MazeTransitionOrigin.Left => Vector2.left * size.x,
                MazeTransitionOrigin.Right => Vector2.right * size.x,
                MazeTransitionOrigin.Top => Vector2.up * size.y,
                MazeTransitionOrigin.Bottom => Vector2.down * size.y,
                MazeTransitionOrigin.TopLeft => new Vector2(-size.x, size.y),
                MazeTransitionOrigin.TopRight => new Vector2(size.x, size.y),
                MazeTransitionOrigin.BottomLeft => new Vector2(-size.x, -size.y),
                MazeTransitionOrigin.BottomRight => new Vector2(size.x, -size.y),
                _ => Vector2.left * size.x,
            };
        }

        private static Rect RectCentered(Vector2 center, Vector2 size)
        {
            return new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));
        }

        private static Rect LerpRect(Rect a, Rect b, float t)
        {
            var center = Vector2.LerpUnclamped(a.center, b.center, t);
            var size = Vector2.LerpUnclamped(a.size, b.size, t);
            return RectCentered(center, size);
        }

        private static Color Clear(Color color)
        {
            color.a = 0f;
            return color;
        }

        private static Color Accent(MazeVectorTransitionProfile profile, float alphaScale)
        {
            var color = profile.AccentColor;
            color.a *= Mathf.Clamp01(alphaScale) * Mathf.Lerp(0.75f, 1.25f, Mathf.Clamp01(profile.Intensity));
            return color;
        }

        private static Color Muted(Color color, float alpha)
        {
            color.a *= Mathf.Clamp01(alpha);
            return color;
        }

        private static float ApplyEase(float t, MazeTransitionEase ease)
        {
            t = Mathf.Clamp01(t);
            return ease switch
            {
                MazeTransitionEase.Smooth => t * t * (3f - 2f * t),
                MazeTransitionEase.EaseIn => t * t,
                MazeTransitionEase.EaseOut => 1f - (1f - t) * (1f - t),
                _ => t,
            };
        }

        private static float FlickerEnvelope(float t)
        {
            t = Mathf.Clamp01(t);
            var attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.28f));
            var release = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.48f) / 0.52f));
            return Mathf.Clamp01(attack * release);
        }

        private static MazeTransitionFlickerWeighting ResolveFlickerWeighting(MazeTransitionFlickerWeighting requested, int seed)
        {
            if (requested != MazeTransitionFlickerWeighting.Auto)
            {
                return requested;
            }

            var pick = HashInt(seed ^ 0x2f4a71) % 3;
            return pick switch
            {
                1 => MazeTransitionFlickerWeighting.Center,
                2 => MazeTransitionFlickerWeighting.Edges,
                _ => MazeTransitionFlickerWeighting.Even,
            };
        }

        private static float FlickerWeightFor(MazeTransitionFlickerWeighting weighting, Vector2 center, Rect screen)
        {
            if (weighting == MazeTransitionFlickerWeighting.Even)
            {
                return 1f;
            }

            var x = Mathf.InverseLerp(screen.xMin, screen.xMax, center.x);
            var y = Mathf.InverseLerp(screen.yMin, screen.yMax, center.y);
            var centerDistance = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f)) / 0.7071f);
            return weighting == MazeTransitionFlickerWeighting.Center
                ? Mathf.Lerp(0.35f, 1.55f, 1f - centerDistance)
                : Mathf.Lerp(0.35f, 1.55f, centerDistance);
        }

        private static float HashSigned(int value)
        {
            return Hash01(value) * 2f - 1f;
        }

        private static int HashInt(int value)
        {
            unchecked
            {
                return (int)(HashUInt(value) & 0x7fffffff);
            }
        }

        private static float Hash01(int value)
        {
            return (HashUInt(value) & 0x00ffffff) / 16777215f;
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
