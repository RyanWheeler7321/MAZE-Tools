using UnityEngine;

namespace Maze
{
    internal sealed class MazeSquareTransitionPattern
    {
        private struct Tile
        {
            public Rect Target;
            public Vector2 InOffset;
            public Vector2 OutOffset;
            public float InDelay;
            public float OutDelay;
            public float InOrderMetric;
            public float OutOrderMetric;
            public int Column;
            public int Row;
            public bool FlickerActive;
        }

        private Tile[] tiles = new Tile[0];
        private Rect screenRect;
        private Rect targetBounds;
        private Vector2 tileSize;
        private float maxDelaySeconds;
        private float maxInDelaySeconds;
        private float maxOutDelaySeconds;
        private int duplicateStartCount;
        private int cardinalOffsetFailures;
        private int unpairedStartCount;
        private int directionUpCount;
        private int directionRightCount;
        private int directionDownCount;
        private int directionLeftCount;
        private int seed;
        private bool flickerMode;
        private float flickerDensity;
        private MazeTransitionFlickerWeighting flickerWeighting;
        private int flickerActiveCount;

        public int TileCount => tiles.Length;
        public Rect ScreenRect => screenRect;
        public Rect TargetBounds => targetBounds;
        public Vector2 TileSize => tileSize;
        public float MaxDelaySeconds => maxDelaySeconds;
        public float MaxInDelaySeconds => maxInDelaySeconds;
        public float MaxOutDelaySeconds => maxOutDelaySeconds;
        public int DuplicateStartCount => duplicateStartCount;
        public int CardinalOffsetFailures => cardinalOffsetFailures;
        public int UnpairedStartCount => unpairedStartCount;
        public int DirectionUpCount => directionUpCount;
        public int DirectionRightCount => directionRightCount;
        public int DirectionDownCount => directionDownCount;
        public int DirectionLeftCount => directionLeftCount;
        public int FlickerActiveCount => flickerActiveCount;

        public float GetDelaySeconds(int index)
        {
            return GetDelaySeconds(index, MazeTransitionDirection.In);
        }

        public float GetDelaySeconds(int index, MazeTransitionDirection direction)
        {
            if (index < 0 || index >= tiles.Length)
            {
                return 0f;
            }

            return direction == MazeTransitionDirection.Out ? tiles[index].OutDelay : tiles[index].InDelay;
        }

        public int[] CreateOldestOnTopDrawOrder()
        {
            var order = new int[tiles.Length];
            for (var i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            System.Array.Sort(order, (a, b) =>
            {
                var delayCompare = tiles[b].InDelay.CompareTo(tiles[a].InDelay);
                return delayCompare != 0 ? delayCompare : b.CompareTo(a);
            });
            return order;
        }

        public void Build(MazeSquareTransitionProfile profile, Rect rect, MazeTransitionContext context)
        {
            screenRect = rect;
            targetBounds = new Rect(0f, 0f, 0f, 0f);
            maxDelaySeconds = 0f;
            maxInDelaySeconds = 0f;
            maxOutDelaySeconds = 0f;
            duplicateStartCount = 0;
            cardinalOffsetFailures = 0;
            unpairedStartCount = 0;
            directionUpCount = 0;
            directionRightCount = 0;
            directionDownCount = 0;
            directionLeftCount = 0;
            flickerActiveCount = 0;
            var columns = profile.Columns;
            var rows = profile.Rows;
            var count = columns * rows;

            if (tiles.Length != count)
            {
                tiles = new Tile[count];
            }

            var overscan = profile.OverscanPixels;
            var gap = profile.GapPixels;
            var fullWidth = rect.width + overscan * 2f;
            var fullHeight = rect.height + overscan * 2f;
            var tileWidth = (fullWidth - gap * (columns - 1)) / columns;
            var tileHeight = (fullHeight - gap * (rows - 1)) / rows;
            tileSize = new Vector2(tileWidth, tileHeight);
            var startX = rect.xMin - overscan;
            var startY = rect.yMin - overscan;
            var alternateRowOffset = profile.AlternateRowOffsetTiles * (tileWidth + gap);
            var baseRowOffset = -alternateRowOffset * 0.5f;
            var inPoint = OriginPoint(profile.InOrigin, profile, rect, context);
            var outPoint = OriginPoint(profile.OutOrigin, profile, rect, context);
            var maxInMetric = 0.0001f;
            var maxOutMetric = 0.0001f;
            seed = profile.RandomizesPerPlay && context.HasRandomSeed ? context.RandomSeed : profile.Seed;
            flickerMode = context.Flicker;
            flickerDensity = context.FlickerDensity > 0f ? Mathf.Clamp01(context.FlickerDensity) : 0.42f;
            flickerWeighting = ResolveFlickerWeighting(context.FlickerWeighting, seed);

            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var index = row * columns + column;
                    var rowOffset = baseRowOffset + (((row & 1) == 1) ? alternateRowOffset : 0f);
                    var target = new Rect(
                        startX + rowOffset + column * (tileWidth + gap),
                        startY + row * (tileHeight + gap),
                        tileWidth + 0.5f,
                        tileHeight + 0.5f);
                    var center = target.center;
                    var inMetric = OrderMetric(profile.Order, column, row, columns, rows, center, inPoint, seed);
                    var outMetric = OrderMetric(profile.Order, column, row, columns, rows, center, outPoint, seed);
                    maxInMetric = Mathf.Max(maxInMetric, inMetric);
                    maxOutMetric = Mathf.Max(maxOutMetric, outMetric);
                    tiles[index].Target = target;
                    tiles[index].Column = column;
                    tiles[index].Row = row;
                    tiles[index].InOrderMetric = inMetric;
                    tiles[index].OutOrderMetric = outMetric;
                    tiles[index].InOffset = StartOffset(profile.InOrigin, center, inPoint, profile.StartOffsetPixels, seed + index * 17);
                    tiles[index].OutOffset = StartOffset(profile.OutOrigin, center, outPoint, profile.StartOffsetPixels, seed + index * 31);
                    tiles[index].InDelay = inMetric;
                    tiles[index].OutDelay = outMetric;
                    tiles[index].FlickerActive = true;
                    targetBounds = index == 0 ? target : Union(targetBounds, target);
                }
            }

            if (profile.MotionPattern == MazeSquareTransitionPatternKind.RandomTessellate)
            {
                AssignRandomTessellateOffsets(columns, rows, seed, false, true);
                AssignRandomTessellateOffsets(columns, rows, seed ^ 0x6c8e9cf5, true, false);
            }

            for (var index = 0; index < count; index++)
            {
                float inDelay;
                float outDelay;
                if (profile.MotionPattern == MazeSquareTransitionPatternKind.RandomTessellate)
                {
                    inDelay = profile.RandomDelayJitter <= 0f ? 0f : Hash01(seed + index * 97) * profile.RandomDelayJitter;
                    outDelay = inDelay;
                }
                else if (profile.MotionPattern != MazeSquareTransitionPatternKind.TessellateWave)
                {
                    inDelay = SpecialtyDelay(profile, tiles[index], columns, rows, index, seed);
                    outDelay = inDelay;
                }
                else
                {
                    var jitter = profile.RandomDelayJitter <= 0f ? 0f : Hash01(seed + index * 97) * profile.RandomDelayJitter;
                    var inNormalized = Mathf.Clamp01(tiles[index].InOrderMetric / maxInMetric);
                    var outNormalized = Mathf.Clamp01(tiles[index].OutOrderMetric / maxOutMetric);
                    inDelay = inNormalized * profile.StaggerSeconds + jitter;
                    outDelay = outNormalized * profile.StaggerSeconds + jitter;
                }

                if (flickerMode)
                {
                    var flickerWeight = FlickerWeightFor(flickerWeighting, tiles[index], columns, rows);
                    var chance = Mathf.Clamp01(flickerDensity * flickerWeight);
                    tiles[index].FlickerActive = Hash01(seed + index * 4513) <= chance;
                    inDelay = Hash01(seed + index * 8111) * Mathf.Max(0.08f, profile.BaseDelaySpanSeconds);
                    outDelay = inDelay;
                    if (tiles[index].FlickerActive)
                    {
                        flickerActiveCount++;
                    }
                }
                else if (profile.DirectionalSpreadSeconds > 0f)
                {
                    inDelay += SpreadDelay(profile, tiles[index], columns, rows, rect, inPoint, true, seed);
                    outDelay += SpreadDelay(profile, tiles[index], columns, rows, rect, outPoint, false, seed);
                }

                tiles[index].InDelay = inDelay;
                tiles[index].OutDelay = outDelay;
                maxInDelaySeconds = Mathf.Max(maxInDelaySeconds, inDelay);
                maxOutDelaySeconds = Mathf.Max(maxOutDelaySeconds, outDelay);
                maxDelaySeconds = Mathf.Max(maxDelaySeconds, Mathf.Max(inDelay, outDelay));
            }
        }

        public MazeTransitionTileFrame Evaluate(MazeSquareTransitionProfile profile, int index, float time, MazeTransitionDirection direction)
        {
            if (index < 0 || index >= tiles.Length)
            {
                return new MazeTransitionTileFrame(default, default, false);
            }

            var tile = tiles[index];
            var localTime = time - (direction == MazeTransitionDirection.Out ? tile.OutDelay : tile.InDelay);
            if (flickerMode)
            {
                return EvaluateFlicker(profile, index, tile, localTime);
            }

            if (localTime <= 0f)
            {
                return direction == MazeTransitionDirection.Out
                    ? new MazeTransitionTileFrame(tile.Target, profile.SettledColor, profile.SettledColor.a > 0.001f)
                    : new MazeTransitionTileFrame(tile.Target, Color.clear, false);
            }

            if (direction == MazeTransitionDirection.In)
            {
                return EvaluateCoverIn(profile, index, tile, localTime);
            }
            else
            {
                if (profile.MotionPattern != MazeSquareTransitionPatternKind.RandomTessellate
                    && profile.MotionPattern != MazeSquareTransitionPatternKind.TessellateWave)
                {
                    return EvaluateSpecialtyOut(profile, index, tile, localTime);
                }

                var moveT = ApplyEase(Mathf.Clamp01(localTime / profile.MoveSeconds), profile.MoveEase);
                var fadeT = ApplyEase(Mathf.Clamp01(localTime / profile.FadeSeconds), profile.FadeEase);
                var rect = OffsetRect(tile.Target, Vector2.Lerp(Vector2.zero, tile.OutOffset, moveT));
                var color = Color.Lerp(profile.SettledColor, profile.OutColor, fadeT);
                return new MazeTransitionTileFrame(rect, color, color.a > 0.001f);
            }
        }

        private MazeTransitionTileFrame EvaluateCoverIn(MazeSquareTransitionProfile profile, int index, Tile tile, float localTime)
        {
            if (profile.MotionPattern != MazeSquareTransitionPatternKind.RandomTessellate
                && profile.MotionPattern != MazeSquareTransitionPatternKind.TessellateWave)
            {
                return EvaluateSpecialtyIn(profile, index, tile, localTime);
            }

            var moveT = ApplyEase(Mathf.Clamp01(localTime / profile.MoveSeconds), profile.MoveEase);
            var fadeT = ApplyEase(Mathf.Clamp01(localTime / profile.FadeSeconds), profile.FadeEase);
            var colorT = profile.MotionPattern == MazeSquareTransitionPatternKind.RandomTessellate
                ? fadeT
                : (profile.SettleSeconds <= 0f
                    ? 1f
                    : ApplyEase(Mathf.Clamp01((localTime - profile.MoveSeconds) / profile.SettleSeconds), profile.SettleEase));

            var rect = OffsetRect(tile.Target, Vector2.Lerp(tile.InOffset, Vector2.zero, moveT));
            if (profile.MotionPattern == MazeSquareTransitionPatternKind.RandomTessellate)
            {
                var scale = Mathf.Lerp(profile.RandomTessellateStartScale, 1f, fadeT);
                rect = ScaleRect(rect, scale);
            }

            var movingColor = profile.MovingColor;
            if (profile.MotionPattern == MazeSquareTransitionPatternKind.RandomTessellate && profile.RandomTessellateShimmer > 0f)
            {
                var shimmerPhase = Hash01(seed + index * 8191) * Mathf.PI * 2f;
                var shimmer = 1f + Mathf.Sin(localTime * 18f + shimmerPhase) * profile.RandomTessellateShimmer * (1f - fadeT);
                movingColor.r *= shimmer;
                movingColor.g *= shimmer;
                movingColor.b *= shimmer;
            }

            var color = Color.Lerp(movingColor, profile.SettledColor, colorT);
            color.a = Mathf.Lerp(profile.MovingColor.a, profile.SettledColor.a, fadeT);
            return new MazeTransitionTileFrame(rect, color, color.a > 0.001f);
        }

        private MazeTransitionTileFrame EvaluateFlicker(MazeSquareTransitionProfile profile, int index, Tile tile, float localTime)
        {
            var duration = Mathf.Max(profile.MoveSeconds, profile.FadeSeconds);
            if (!tile.FlickerActive || localTime <= 0f || localTime > duration)
            {
                return new MazeTransitionTileFrame(tile.Target, Color.clear, false);
            }

            var frame = EvaluateCoverIn(profile, index, tile, localTime);
            var t = Mathf.Clamp01(localTime / duration);
            var envelope = FlickerEnvelope(t);
            var color = frame.Color;
            color = Color.Lerp(color, profile.AccentColor, Mathf.Clamp01((1f - envelope) * 0.35f));
            color.a *= envelope;
            return new MazeTransitionTileFrame(frame.Rect, color, color.a > 0.001f);
        }

        private MazeTransitionTileFrame EvaluateSpecialtyOut(MazeSquareTransitionProfile profile, int index, Tile tile, float localTime)
        {
            var duration = Mathf.Max(profile.MoveSeconds, profile.FadeSeconds);
            if (localTime > duration)
            {
                return new MazeTransitionTileFrame(tile.Target, Color.clear, false);
            }

            if (profile.ShrinkOutWithoutAlphaFade)
            {
                var shrinkT = ApplyEase(Mathf.Clamp01(localTime / profile.MoveSeconds), profile.MoveEase);
                var rect = ScaleRect(tile.Target, Mathf.Lerp(1f, 0f, shrinkT));
                return new MazeTransitionTileFrame(rect, profile.SettledColor, shrinkT < 0.999f && profile.SettledColor.a > 0.001f);
            }

            return EvaluateSpecialtyIn(profile, index, tile, Mathf.Max(0f, duration - localTime));
        }

        private MazeTransitionTileFrame EvaluateSpecialtyIn(MazeSquareTransitionProfile profile, int index, Tile tile, float localTime)
        {
            var moveT = ApplyEase(Mathf.Clamp01(localTime / profile.MoveSeconds), profile.MoveEase);
            var fadeT = ApplyEase(Mathf.Clamp01(localTime / profile.FadeSeconds), profile.FadeEase);
            var rect = tile.Target;
            var color = Color.Lerp(Boost(profile.MovingColor, profile, index, localTime, fadeT, seed), profile.SettledColor, fadeT);
            color.a = Mathf.Lerp(profile.MovingColor.a, profile.SettledColor.a, fadeT);
            var hash = Hash01(seed + index * 12601);
            var vertical = hash < 0.5f;
            var lineX = Mathf.Clamp01(profile.LineThicknessPixels / Mathf.Max(1f, rect.width));
            var lineY = Mathf.Clamp01(profile.LineThicknessPixels / Mathf.Max(1f, rect.height));
            var minScale = Mathf.Clamp01(profile.RandomTessellateStartScale);

            switch (profile.MotionPattern)
            {
                case MazeSquareTransitionPatternKind.InkGridLock:
                    rect = ScaleRectAxes(rect, Mathf.Lerp(lineX, 1f, fadeT), Mathf.Lerp(lineY, 1f, fadeT));
                    color = Color.Lerp(profile.AccentColor, profile.SettledColor, fadeT);
                    color.a = fadeT;
                    break;

                case MazeSquareTransitionPatternKind.CrosshairSweep:
                    rect = ScaleRectAxes(rect, vertical ? Mathf.Lerp(lineX, 1f, moveT) : 1f, vertical ? 1f : Mathf.Lerp(lineY, 1f, moveT));
                    color = Color.Lerp(profile.AccentColor, profile.SettledColor, fadeT);
                    color.a = fadeT;
                    break;

                case MazeSquareTransitionPatternKind.VenetianRuneBlinds:
                    rect = ScaleRectAxes(rect, vertical ? Mathf.Lerp(0.18f, 1f, moveT) : 1f, vertical ? 1f : Mathf.Lerp(0.18f, 1f, moveT));
                    color = Color.Lerp(profile.MovingColor, profile.SettledColor, fadeT);
                    color.a = fadeT;
                    break;

                case MazeSquareTransitionPatternKind.CardFlipSquares:
                    rect = ScaleRectAxes(rect, vertical ? Mathf.Lerp(0.04f, 1f, moveT) : 1f, vertical ? 1f : Mathf.Lerp(0.04f, 1f, moveT));
                    color = Color.Lerp(profile.MovingColor, profile.SettledColor, fadeT);
                    color.a = fadeT;
                    break;

                case MazeSquareTransitionPatternKind.CornerBracketCollapse:
                    rect = CornerRect(rect, hash, Mathf.Lerp(0.18f, 1f, moveT));
                    color = Color.Lerp(profile.AccentColor, profile.SettledColor, fadeT);
                    color.a = fadeT;
                    break;

                case MazeSquareTransitionPatternKind.CreepingBlueprintLines:
                    rect = fadeT < 0.55f
                        ? ScaleRectAxes(rect, vertical ? lineX : Mathf.Lerp(lineX, 1f, fadeT / 0.55f), vertical ? Mathf.Lerp(lineY, 1f, fadeT / 0.55f) : lineY)
                        : ScaleRect(rect, Mathf.Lerp(0.35f, 1f, (fadeT - 0.55f) / 0.45f));
                    color = Color.Lerp(profile.AccentColor, profile.SettledColor, fadeT);
                    color.a = Mathf.Clamp01(fadeT * 1.4f);
                    break;

                case MazeSquareTransitionPatternKind.ShutterApertureRects:
                    var edgeOffset = EdgeOffset(tile.Target.center, screenRect, Mathf.Max(tile.Target.width, tile.Target.height) * (1.4f + profile.EffectIntensity));
                    rect = OffsetRect(tile.Target, Vector2.Lerp(edgeOffset, Vector2.zero, moveT));
                    rect = ScaleRect(rect, Mathf.Lerp(0.78f, 1f, fadeT));
                    color = Color.Lerp(profile.MovingColor, profile.SettledColor, fadeT);
                    color.a = fadeT;
                    break;

                case MazeSquareTransitionPatternKind.CheckeredDesync:
                    rect = ScaleRect(rect, Mathf.Lerp(0.7f, 1f, moveT));
                    var checker = ((tile.Column + tile.Row) & 1) == 0;
                    color = Color.Lerp(checker ? profile.MovingColor : profile.AccentColor, profile.SettledColor, fadeT);
                    color.a = fadeT;
                    break;

                case MazeSquareTransitionPatternKind.PageCutSlits:
                    rect = ScaleRectAxes(rect, vertical ? Mathf.Lerp(lineX, 1f, moveT) : 1f, vertical ? 1f : Mathf.Lerp(lineY, 1f, moveT));
                    rect = OffsetRect(rect, new Vector2(Mathf.Sin(hash * 40f) * profile.LineThicknessPixels * (1f - moveT), 0f));
                    color = Color.Lerp(profile.AccentColor, profile.SettledColor, fadeT);
                    color.a = fadeT;
                    break;

                case MazeSquareTransitionPatternKind.SignalCorruptionBlocks:
                    var blink = Hash01(seed + index * 37 + Mathf.FloorToInt(localTime * 24f) * 911);
                    rect = ScaleRect(OffsetRect(rect, JitterOffset(profile, index, localTime, seed) * (1f - fadeT)), Mathf.Lerp(minScale, 1f, moveT));
                    color = Color.Lerp(blink > 0.7f ? profile.AccentColor : profile.MovingColor, profile.SettledColor, fadeT);
                    color.a = Mathf.Clamp01(fadeT + (blink > 0.82f ? 0.25f * fadeT * (1f - fadeT) : 0f));
                    break;

                case MazeSquareTransitionPatternKind.BlockfallLock:
                    var cluster = (tile.Column / 2) + (tile.Row / 2) * 47;
                    var fallSide = Hash01(seed + cluster * 239);
                    var fallDown = Hash01(seed + cluster * 719) > 0.42f;
                    var fallOffset = new Vector2((fallSide - 0.5f) * tile.Target.width * 1.4f, (fallDown ? 1f : -1f) * (screenRect.height * 0.38f + tile.Target.height * (1f + tile.Row % 3)));
                    rect = OffsetRect(tile.Target, Vector2.Lerp(fallOffset, Vector2.zero, moveT));
                    rect = ScaleRect(rect, Mathf.Lerp(0.76f, 1f + 0.08f * (1f - Mathf.Abs(moveT - 0.72f) / 0.28f), Mathf.Clamp01(moveT)));
                    color = Color.Lerp(profile.AccentColor, profile.SettledColor, fadeT);
                    color.a = Mathf.Lerp(0f, profile.SettledColor.a, fadeT);
                    break;

                case MazeSquareTransitionPatternKind.CircuitTraceFill:
                    var traceT = Mathf.Clamp01(fadeT / 0.62f);
                    var fillT = Mathf.Clamp01((fadeT - 0.46f) / 0.54f);
                    var traceVertical = ((tile.Column + tile.Row + Mathf.FloorToInt(hash * 3f)) & 1) == 0;
                    var traceWidth = traceVertical ? Mathf.Lerp(lineX, 1f, fillT) : Mathf.Lerp(0.12f, 1f, traceT);
                    var traceHeight = traceVertical ? Mathf.Lerp(0.12f, 1f, traceT) : Mathf.Lerp(lineY, 1f, fillT);
                    rect = ScaleRectAxes(rect, traceWidth, traceHeight);
                    rect = OffsetRect(rect, traceVertical
                        ? new Vector2(0f, Mathf.Lerp(-tile.Target.height * 0.36f, 0f, traceT))
                        : new Vector2(Mathf.Lerp(-tile.Target.width * 0.36f, 0f, traceT), 0f));
                    color = Color.Lerp(profile.AccentColor, profile.SettledColor, fillT);
                    color.a = Mathf.Clamp01(Mathf.Lerp(0f, 0.95f, traceT) + fillT * 0.45f);
                    break;

                case MazeSquareTransitionPatternKind.GatefoldGrid:
                    var edgeX = Mathf.Abs(tile.Target.center.x - screenRect.center.x) / Mathf.Max(1f, screenRect.width * 0.5f);
                    var edgeY = Mathf.Abs(tile.Target.center.y - screenRect.center.y) / Mathf.Max(1f, screenRect.height * 0.5f);
                    var horizontalGate = edgeX >= edgeY;
                    var anchorX = tile.Target.center.x < screenRect.center.x ? -1f : 1f;
                    var anchorY = tile.Target.center.y < screenRect.center.y ? -1f : 1f;
                    var gateScale = Mathf.Lerp(0.04f, 1f, moveT);
                    rect = horizontalGate
                        ? AnchoredScaleRect(rect, gateScale, 1f, anchorX, 0f)
                        : AnchoredScaleRect(rect, 1f, gateScale, 0f, anchorY);
                    var bite = Hash01(seed + index * 3457) > 0.7f ? Mathf.Lerp(0.82f, 1f, moveT) : 1f;
                    rect = ScaleRect(rect, bite);
                    color = Color.Lerp(profile.AccentColor, profile.SettledColor, fadeT);
                    color.a = fadeT;
                    break;

                case MazeSquareTransitionPatternKind.ZoomLattice:
                    var zoomStartSize = tile.Target.size * Mathf.Lerp(0.08f, 0.18f, hash);
                    var zoomStart = RectCentered(screenRect.center + (tile.Target.center - screenRect.center) * 0.08f, zoomStartSize);
                    rect = LerpRect(zoomStart, tile.Target, moveT);
                    rect = ScaleRect(rect, Mathf.Lerp(0.85f, 1f, fadeT));
                    color = Color.Lerp(profile.AccentColor, profile.SettledColor, fadeT);
                    color.a = fadeT;
                    break;

                case MazeSquareTransitionPatternKind.DominoColumns:
                    var dominoVertical = (tile.Column & 1) == 0;
                    var dominoScale = Mathf.Lerp(0.035f, 1f, moveT);
                    var dominoAnchor = (dominoVertical ? tile.Row : tile.Column) % 2 == 0 ? -1f : 1f;
                    rect = dominoVertical
                        ? AnchoredScaleRect(rect, dominoScale, 1f, dominoAnchor, 0f)
                        : AnchoredScaleRect(rect, 1f, dominoScale, 0f, dominoAnchor);
                    rect = OffsetRect(rect, (dominoVertical ? Vector2.right : Vector2.up) * dominoAnchor * profile.LineThicknessPixels * (1f - moveT));
                    color = Color.Lerp(profile.AccentColor, profile.SettledColor, fadeT);
                    color.a = fadeT;
                    break;

                case MazeSquareTransitionPatternKind.CourierBlocks:
                    var fromLeft = ((tile.Row + Mathf.FloorToInt(hash * 5f)) & 1) == 0;
                    var routeStart = new Vector2(fromLeft ? screenRect.xMin - tile.Target.width : screenRect.xMax + tile.Target.width, Mathf.Lerp(screenRect.yMin, screenRect.yMax, Hash01(seed + tile.Row * 881)));
                    var routeCorner = new Vector2(tile.Target.center.x, routeStart.y);
                    var routeT = Mathf.Clamp01(moveT / 0.72f);
                    var routeCenter = routeT < 0.58f
                        ? Vector2.Lerp(routeStart, routeCorner, routeT / 0.58f)
                        : Vector2.Lerp(routeCorner, tile.Target.center, (routeT - 0.58f) / 0.42f);
                    var courierScale = Mathf.Lerp(0.28f, 1f, Mathf.Clamp01((moveT - 0.55f) / 0.45f));
                    rect = RectCentered(routeCenter, tile.Target.size * courierScale);
                    color = Color.Lerp(profile.AccentColor, profile.SettledColor, fadeT);
                    color.a = Mathf.Clamp01(fadeT * 1.2f);
                    break;

                case MazeSquareTransitionPatternKind.SquaredCircuitDecay:
                    var noiseTick = Mathf.FloorToInt(localTime * 30f);
                    var noise = Hash01(seed + index * 631 + noiseTick * 1543);
                    var chunkScaleX = noise > 0.64f ? Mathf.Lerp(0.18f, 1f, fadeT) : Mathf.Lerp(0.45f, 1f, moveT);
                    var chunkScaleY = noise < 0.36f ? Mathf.Lerp(0.12f, 1f, fadeT) : Mathf.Lerp(0.45f, 1f, moveT);
                    rect = ScaleRectAxes(rect, chunkScaleX, chunkScaleY);
                    rect = OffsetRect(rect, JitterOffset(profile, index, localTime, seed) * (1f - moveT) * 1.8f);
                    color = Color.Lerp(noise > 0.58f ? profile.AccentColor : profile.MovingColor, profile.SettledColor, fadeT);
                    color.a = Mathf.Clamp01(fadeT + (noise > 0.82f ? 0.35f * fadeT * (1f - fadeT) : 0f));
                    break;

                case MazeSquareTransitionPatternKind.VideowallSnap:
                    var snapTick = Mathf.FloorToInt(localTime * 22f);
                    var snapNoise = Hash01(seed + index * 997 + snapTick * 61);
                    var snapOffset = new Vector2((Hash01(seed + index * 233) - 0.5f) * tile.Target.width * 0.7f, (Hash01(seed + index * 877) - 0.5f) * tile.Target.height * 0.7f);
                    var snapT = moveT < 0.72f ? moveT * 0.35f : Mathf.Clamp01((moveT - 0.72f) / 0.28f);
                    rect = OffsetRect(ScaleRect(tile.Target, Mathf.Lerp(1.12f, 1f, moveT)), Vector2.Lerp(snapOffset, Vector2.zero, snapT));
                    var grey = new Color(0.08f + snapNoise * 0.16f, 0.07f + snapNoise * 0.1f, 0.12f + snapNoise * 0.18f, 1f);
                    color = Color.Lerp(snapNoise > 0.82f ? profile.AccentColor : grey, profile.SettledColor, fadeT);
                    color.a = Mathf.Clamp01(fadeT + (snapNoise > 0.82f ? 0.22f * fadeT * (1f - fadeT) : 0f));
                    break;
            }

            return new MazeTransitionTileFrame(rect, color, color.a > 0.001f);
        }

        private static float SpecialtyDelay(MazeSquareTransitionProfile profile, Tile tile, int columns, int rows, int index, int seed)
        {
            var random = Hash01(seed + index * 97) * profile.RandomDelayJitter;
            var x = columns <= 1 ? 0f : tile.Column / (float)(columns - 1);
            var y = rows <= 1 ? 0f : tile.Row / (float)(rows - 1);
            var centerDistance = Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f));
            var cornerDistance = Mathf.Min(Mathf.Min(x + y, (1f - x) + y), Mathf.Min(x + (1f - y), (1f - x) + (1f - y))) * 0.5f;
            var edgeDistance = Mathf.Min(Mathf.Min(x, 1f - x), Mathf.Min(y, 1f - y));
            var wave = profile.MotionPattern switch
            {
                MazeSquareTransitionPatternKind.InkGridLock => centerDistance,
                MazeSquareTransitionPatternKind.CrosshairSweep => Mathf.Min(Mathf.Abs(x - 0.5f), Mathf.Abs(y - 0.5f)) * 2f,
                MazeSquareTransitionPatternKind.VenetianRuneBlinds => x,
                MazeSquareTransitionPatternKind.CardFlipSquares => Hash01(seed + index * 1889),
                MazeSquareTransitionPatternKind.CornerBracketCollapse => cornerDistance,
                MazeSquareTransitionPatternKind.CreepingBlueprintLines => (x + (1f - y)) * 0.5f,
                MazeSquareTransitionPatternKind.ShutterApertureRects => edgeDistance * 2f,
                MazeSquareTransitionPatternKind.CheckeredDesync => ((tile.Column + tile.Row) & 1) == 0 ? 0f : 0.45f,
                MazeSquareTransitionPatternKind.PageCutSlits => y,
                MazeSquareTransitionPatternKind.SignalCorruptionBlocks => Hash01(seed + index * 4099),
                MazeSquareTransitionPatternKind.BlockfallLock => Mathf.Clamp01((1f - y) * 0.78f + Hash01(seed + (tile.Column / 2) * 733 + (tile.Row / 2) * 983) * 0.22f),
                MazeSquareTransitionPatternKind.CircuitTraceFill => Mathf.Clamp01((x + y) * 0.5f + Hash01(seed + tile.Row * 173) * 0.16f),
                MazeSquareTransitionPatternKind.GatefoldGrid => Mathf.Clamp01(1f - Mathf.Max(Mathf.Abs(x - 0.5f), Mathf.Abs(y - 0.5f)) * 2f),
                MazeSquareTransitionPatternKind.ZoomLattice => centerDistance,
                MazeSquareTransitionPatternKind.DominoColumns => x,
                MazeSquareTransitionPatternKind.CourierBlocks => Hash01(seed + tile.Row * 911 + tile.Column * 31),
                MazeSquareTransitionPatternKind.SquaredCircuitDecay => Hash01(seed + index * 6151),
                MazeSquareTransitionPatternKind.VideowallSnap => Mathf.Floor(Hash01(seed + index * 409) * 5f) / 4f,
                _ => 0f,
            };
            return Mathf.Clamp01(wave) * profile.StaggerSeconds * profile.SecondaryDelayScale + random;
        }

        private static Rect OffsetRect(Rect rect, Vector2 offset)
        {
            rect.position += offset;
            return rect;
        }

        private static Rect ScaleRectAxes(Rect rect, float scaleX, float scaleY)
        {
            var center = rect.center;
            rect.size = new Vector2(Mathf.Max(0.01f, rect.width * Mathf.Max(0.01f, scaleX)), Mathf.Max(0.01f, rect.height * Mathf.Max(0.01f, scaleY)));
            rect.center = center;
            return rect;
        }

        private static Rect ScaleRect(Rect rect, float scale)
        {
            scale = Mathf.Max(0.01f, scale);
            var center = rect.center;
            rect.size *= scale;
            rect.center = center;
            return rect;
        }

        private static Rect AnchoredScaleRect(Rect rect, float scaleX, float scaleY, float anchorX, float anchorY)
        {
            var xMin = rect.xMin;
            var xMax = rect.xMax;
            var yMin = rect.yMin;
            var yMax = rect.yMax;
            var width = Mathf.Max(0.01f, rect.width * Mathf.Max(0.01f, scaleX));
            var height = Mathf.Max(0.01f, rect.height * Mathf.Max(0.01f, scaleY));

            if (anchorX < -0.1f)
            {
                xMax = xMin + width;
            }
            else if (anchorX > 0.1f)
            {
                xMin = xMax - width;
            }
            else
            {
                var centerX = rect.center.x;
                xMin = centerX - width * 0.5f;
                xMax = centerX + width * 0.5f;
            }

            if (anchorY < -0.1f)
            {
                yMax = yMin + height;
            }
            else if (anchorY > 0.1f)
            {
                yMin = yMax - height;
            }
            else
            {
                var centerY = rect.center.y;
                yMin = centerY - height * 0.5f;
                yMax = centerY + height * 0.5f;
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static Rect RectCentered(Vector2 center, Vector2 size)
        {
            return new Rect(center - size * 0.5f, size);
        }

        private static Rect LerpRect(Rect from, Rect to, float t)
        {
            t = Mathf.Clamp01(t);
            return new Rect(
                Mathf.Lerp(from.x, to.x, t),
                Mathf.Lerp(from.y, to.y, t),
                Mathf.Lerp(from.width, to.width, t),
                Mathf.Lerp(from.height, to.height, t));
        }

        private static Rect CornerRect(Rect rect, float hash, float scale)
        {
            scale = Mathf.Clamp01(scale);
            var size = rect.size * Mathf.Lerp(0.12f, 1f, scale);
            var left = hash < 0.5f;
            var bottom = hash < 0.25f || hash > 0.75f;
            var x = left ? rect.xMin : rect.xMax - size.x;
            var y = bottom ? rect.yMin : rect.yMax - size.y;
            return new Rect(x, y, size.x, size.y);
        }

        private static Vector2 EdgeOffset(Vector2 center, Rect screen, float distance)
        {
            var left = center.x - screen.xMin;
            var right = screen.xMax - center.x;
            var bottom = center.y - screen.yMin;
            var top = screen.yMax - center.y;
            var min = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
            if (Mathf.Approximately(min, left)) return Vector2.left * distance;
            if (Mathf.Approximately(min, right)) return Vector2.right * distance;
            if (Mathf.Approximately(min, bottom)) return Vector2.down * distance;
            return Vector2.up * distance;
        }

        private static Vector2 JitterOffset(MazeSquareTransitionProfile profile, int index, float localTime, int seed)
        {
            var tick = Mathf.FloorToInt(localTime * 18f);
            var x = Hash01(seed + index * 9283 + tick * 193) - 0.5f;
            var y = Hash01(seed + index * 1171 + tick * 389) - 0.5f;
            return new Vector2(x, y) * profile.LineThicknessPixels * profile.EffectIntensity;
        }

        private static Color Boost(Color color, MazeSquareTransitionProfile profile, int index, float localTime, float fadeT, int seed)
        {
            if (profile.RandomTessellateShimmer <= 0f)
            {
                return color;
            }

            var phase = Hash01(seed + index * 8191) * Mathf.PI * 2f;
            var shimmer = 1f + Mathf.Sin(localTime * 18f + phase) * profile.RandomTessellateShimmer * profile.EffectIntensity * (1f - fadeT);
            color.r *= shimmer;
            color.g *= shimmer;
            color.b *= shimmer;
            return color;
        }

        private static float SpreadDelay(MazeSquareTransitionProfile profile, Tile tile, int columns, int rows, Rect rect, Vector2 phaseOrigin, bool inPhase, int seed)
        {
            var seconds = profile.DirectionalSpreadSeconds;
            if (seconds <= 0f)
            {
                return 0f;
            }

            var metric = SpreadMetric(profile.SpreadDirection, tile, columns, rows, rect, phaseOrigin, inPhase, seed);
            return ApplyEase(metric, profile.SpreadEase) * seconds;
        }

        private static float SpreadMetric(MazeTransitionSpreadDirection direction, Tile tile, int columns, int rows, Rect rect, Vector2 phaseOrigin, bool inPhase, int seed)
        {
            if (direction == MazeTransitionSpreadDirection.RandomSide)
            {
                direction = RandomSpreadDirection(seed, inPhase);
            }

            var x = columns <= 1 ? 0f : tile.Column / (float)(columns - 1);
            var y = rows <= 1 ? 0f : tile.Row / (float)(rows - 1);
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
                _ => Mathf.Clamp01(Vector2.Distance(tile.Target.center, phaseOrigin) / Mathf.Max(1f, rect.size.magnitude)),
            };
        }

        private static MazeTransitionSpreadDirection RandomSpreadDirection(int seed, bool inPhase)
        {
            var pick = HashInt(seed ^ (inPhase ? 0x43f3a1 : 0x6f12db)) % 8;
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

        private static Rect Union(Rect a, Rect b)
        {
            var xMin = Mathf.Min(a.xMin, b.xMin);
            var yMin = Mathf.Min(a.yMin, b.yMin);
            var xMax = Mathf.Max(a.xMax, b.xMax);
            var yMax = Mathf.Max(a.yMax, b.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static Vector2 OriginPoint(MazeTransitionOrigin origin, MazeSquareTransitionProfile profile, Rect rect, MazeTransitionContext context)
        {
            if (origin == MazeTransitionOrigin.Point && context.HasScreenPoint)
            {
                return context.ScreenPoint;
            }

            if (origin == MazeTransitionOrigin.Point)
            {
                return new Vector2(
                    Mathf.Lerp(rect.xMin, rect.xMax, profile.CustomPointNormalized.x),
                    Mathf.Lerp(rect.yMin, rect.yMax, profile.CustomPointNormalized.y));
            }

            return origin switch
            {
                MazeTransitionOrigin.Left => new Vector2(rect.xMin, rect.center.y),
                MazeTransitionOrigin.Right => new Vector2(rect.xMax, rect.center.y),
                MazeTransitionOrigin.Top => new Vector2(rect.center.x, rect.yMax),
                MazeTransitionOrigin.Bottom => new Vector2(rect.center.x, rect.yMin),
                MazeTransitionOrigin.TopLeft => new Vector2(rect.xMin, rect.yMax),
                MazeTransitionOrigin.TopRight => new Vector2(rect.xMax, rect.yMax),
                MazeTransitionOrigin.BottomLeft => new Vector2(rect.xMin, rect.yMin),
                MazeTransitionOrigin.BottomRight => new Vector2(rect.xMax, rect.yMin),
                _ => rect.center,
            };
        }

        private static Vector2 StartOffset(MazeTransitionOrigin origin, Vector2 center, Vector2 originPoint, float distance, int seed)
        {
            if (distance <= 0f)
            {
                return Vector2.zero;
            }

            var direction = origin switch
            {
                MazeTransitionOrigin.Left => Vector2.left,
                MazeTransitionOrigin.Right => Vector2.right,
                MazeTransitionOrigin.Top => Vector2.up,
                MazeTransitionOrigin.Bottom => Vector2.down,
                MazeTransitionOrigin.TopLeft => new Vector2(-1f, 1f).normalized,
                MazeTransitionOrigin.TopRight => new Vector2(1f, 1f).normalized,
                MazeTransitionOrigin.BottomLeft => new Vector2(-1f, -1f).normalized,
                MazeTransitionOrigin.BottomRight => new Vector2(1f, -1f).normalized,
                MazeTransitionOrigin.Edges => EdgeDirection(center, originPoint),
                MazeTransitionOrigin.Corners => CornerDirection(center, originPoint),
                MazeTransitionOrigin.Random => RandomDirection(seed),
                _ => (center - originPoint).sqrMagnitude < 0.0001f ? Vector2.up : (center - originPoint).normalized,
            };

            return direction * distance;
        }

        private static Vector2 EdgeDirection(Vector2 center, Vector2 originPoint)
        {
            var delta = center - originPoint;
            return Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? new Vector2(Mathf.Sign(delta.x), 0f)
                : new Vector2(0f, Mathf.Sign(delta.y));
        }

        private static Vector2 CornerDirection(Vector2 center, Vector2 originPoint)
        {
            var delta = center - originPoint;
            return new Vector2(Mathf.Sign(delta.x == 0f ? 1f : delta.x), Mathf.Sign(delta.y == 0f ? 1f : delta.y)).normalized;
        }

        private static Vector2 RandomDirection(int seed)
        {
            var angle = Hash01(seed) * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private void AssignRandomTessellateOffsets(int columns, int rows, int seed, bool assignOutOffset, bool collectDiagnostics)
        {
            var count = columns * rows;
            var startCells = new int[count];
            for (var i = 0; i < count; i++)
            {
                startCells[i] = -1;
            }

            var blackIndices = new int[(count + 1) / 2];
            var blackCount = 0;
            var whiteCount = 0;
            for (var index = 0; index < count; index++)
            {
                var column = index % columns;
                var row = index / columns;
                if (((column + row) & 1) == 0)
                {
                    blackIndices[blackCount++] = index;
                }
                else
                {
                    whiteCount++;
                }
            }

            if (blackCount == whiteCount)
            {
                var blackOrder = new int[blackCount];
                for (var i = 0; i < blackCount; i++)
                {
                    blackOrder[i] = blackIndices[i];
                }

                Shuffle(blackOrder, blackOrder.Length, seed ^ 0x5273);
                var whiteMatch = new int[count];
                for (var i = 0; i < count; i++)
                {
                    whiteMatch[i] = -1;
                }

                for (var i = 0; i < blackOrder.Length; i++)
                {
                    var seenWhite = new bool[count];
                    if (!TryMatchBlack(blackOrder[i], columns, rows, seed, whiteMatch, seenWhite) && collectDiagnostics)
                    {
                        unpairedStartCount++;
                    }
                }

                for (var white = 0; white < count; white++)
                {
                    var black = whiteMatch[white];
                    if (black < 0)
                    {
                        continue;
                    }

                    startCells[black] = white;
                    startCells[white] = black;
                }
            }
            else
            {
                if (collectDiagnostics)
                {
                    unpairedStartCount += Mathf.Abs(blackCount - whiteCount);
                }
            }

            ApplyStartCells(startCells, columns, count, assignOutOffset, collectDiagnostics);
        }

        private void ApplyStartCells(int[] startCells, int columns, int count, bool assignOutOffset, bool collectDiagnostics)
        {
            var usedStarts = new bool[count];
            for (var index = 0; index < count; index++)
            {
                var startIndex = startCells[index];
                if (startIndex < 0 || startIndex >= count)
                {
                    if (collectDiagnostics)
                    {
                        unpairedStartCount++;
                    }
                    if (assignOutOffset)
                    {
                        tiles[index].OutOffset = Vector2.zero;
                    }
                    else
                    {
                        tiles[index].InOffset = Vector2.zero;
                    }
                    continue;
                }

                if (collectDiagnostics && usedStarts[startIndex])
                {
                    duplicateStartCount++;
                }
                usedStarts[startIndex] = true;

                var indexColumn = index % columns;
                var indexRow = index / columns;
                var startColumn = startIndex % columns;
                var startRow = startIndex / columns;
                var columnDelta = startColumn - indexColumn;
                var rowDelta = startRow - indexRow;
                if (collectDiagnostics && Mathf.Abs(columnDelta) + Mathf.Abs(rowDelta) != 1)
                {
                    cardinalOffsetFailures++;
                }

                if (!collectDiagnostics)
                {
                    tiles[index].OutOffset = tiles[startIndex].Target.center - tiles[index].Target.center;
                    continue;
                }

                if (rowDelta > 0)
                {
                    directionUpCount++;
                }
                else if (columnDelta > 0)
                {
                    directionRightCount++;
                }
                else if (rowDelta < 0)
                {
                    directionDownCount++;
                }
                else if (columnDelta < 0)
                {
                    directionLeftCount++;
                }

                tiles[index].InOffset = tiles[startIndex].Target.center - tiles[index].Target.center;
            }
        }

        private static bool TryMatchBlack(int black, int columns, int rows, int seed, int[] whiteMatch, bool[] seenWhite)
        {
            var blackColumn = black % columns;
            var blackRow = black / columns;
            var directions = new[] { 0, 1, 2, 3 };
            Shuffle(directions, directions.Length, seed + black * 104729);
            for (var i = 0; i < directions.Length; i++)
            {
                var column = blackColumn;
                var row = blackRow;
                switch (directions[i])
                {
                    case 0:
                        row += 1;
                        break;
                    case 1:
                        column += 1;
                        break;
                    case 2:
                        row -= 1;
                        break;
                    default:
                        column -= 1;
                        break;
                }

                if (column < 0 || column >= columns || row < 0 || row >= rows)
                {
                    continue;
                }

                var white = row * columns + column;
                if (seenWhite[white])
                {
                    continue;
                }

                seenWhite[white] = true;
                var matchedBlack = whiteMatch[white];
                if (matchedBlack < 0 || TryMatchBlack(matchedBlack, columns, rows, seed, whiteMatch, seenWhite))
                {
                    whiteMatch[white] = black;
                    return true;
                }
            }

            return false;
        }

        private static void Shuffle(int[] values, int count, int seed)
        {
            for (var i = count - 1; i > 0; i--)
            {
                var j = HashInt(seed + i * 334214459) % (i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        private static float OrderMetric(MazeTransitionOrder order, int column, int row, int columns, int rows, Vector2 center, Vector2 originPoint, int seed)
        {
            return order switch
            {
                MazeTransitionOrder.RowScan => column + row * columns,
                MazeTransitionOrder.ColumnScan => row + column * rows,
                MazeTransitionOrder.ReverseRowScan => (columns - 1 - column) + (rows - 1 - row) * columns,
                MazeTransitionOrder.ReverseColumnScan => (rows - 1 - row) + (columns - 1 - column) * rows,
                MazeTransitionOrder.RandomSeeded => Hash01(seed + column * 73856093 + row * 19349663),
                _ => Vector2.Distance(center, originPoint),
            };
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

        private static float FlickerWeightFor(MazeTransitionFlickerWeighting weighting, Tile tile, int columns, int rows)
        {
            if (weighting == MazeTransitionFlickerWeighting.Even)
            {
                return 1f;
            }

            var x = columns <= 1 ? 0.5f : tile.Column / (float)(columns - 1);
            var y = rows <= 1 ? 0.5f : tile.Row / (float)(rows - 1);
            var centerDistance = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f)) / 0.7071f);
            return weighting == MazeTransitionFlickerWeighting.Center
                ? Mathf.Lerp(0.35f, 1.55f, 1f - centerDistance)
                : Mathf.Lerp(0.35f, 1.55f, centerDistance);
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
