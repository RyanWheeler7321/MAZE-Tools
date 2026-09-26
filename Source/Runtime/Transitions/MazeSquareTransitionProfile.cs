using UnityEngine;

namespace Maze
{
    public struct MazeSquareTransitionRuntimeSettings
    {
        public int Columns;
        public int Rows;
        public float OverscanPixels;
        public float GapPixels;
        public float AlternateRowOffsetTiles;
        public MazeSquareTransitionPatternKind MotionPattern;
        public MazeTransitionOrigin InOrigin;
        public MazeTransitionOrigin OutOrigin;
        public MazeTransitionOrder Order;
        public MazeTransitionEase MoveEase;
        public MazeTransitionEase FadeEase;
        public MazeTransitionEase SettleEase;
        public Vector2 CustomPointNormalized;
        public float StartOffsetPixels;
        public float MoveSeconds;
        public float FadeSeconds;
        public float SettleSeconds;
        public float StaggerSeconds;
        public float HoldSeconds;
        public float RandomDelayJitter;
        public float DirectionalSpreadSeconds;
        public MazeTransitionSpreadDirection SpreadDirection;
        public MazeTransitionEase SpreadEase;
        public float RandomTessellateStartScale;
        public float RandomTessellateShimmer;
        public float LineThicknessPixels;
        public float SecondaryDelayScale;
        public float EffectIntensity;
        public bool ShrinkOutWithoutAlphaFade;
        public int Seed;
        public bool RandomizeEachPlay;
        public Color MovingColor;
        public Color AccentColor;
        public Color SettledColor;
        public Color OutColor;
    }

    // Grid of squares that move and fade into cover, can settle into another color, then clear or reveal.
    [CreateAssetMenu(menuName = "MAZE/Transitions/Square Transition", fileName = "MAZE_SquareTransition")]
    public sealed class MazeSquareTransitionProfile : MazeTransitionProfile
    {
        [Header("Grid")]
        [SerializeField] private int columns = 24;
        [SerializeField] private int rows = 14;
        [SerializeField] private float overscanPixels = 36f;
        [SerializeField] private float gapPixels = 0f;
        [SerializeField] private float alternateRowOffsetTiles;

        [Header("Motion")]
        [SerializeField] private MazeSquareTransitionPatternKind motionPattern = MazeSquareTransitionPatternKind.RandomTessellate;
        [SerializeField] private MazeTransitionOrigin inOrigin = MazeTransitionOrigin.Left;
        [SerializeField] private MazeTransitionOrigin outOrigin = MazeTransitionOrigin.Right;
        [SerializeField] private MazeTransitionOrder order = MazeTransitionOrder.Distance;
        [SerializeField] private MazeTransitionEase moveEase = MazeTransitionEase.EaseOut;
        [SerializeField] private MazeTransitionEase fadeEase = MazeTransitionEase.Smooth;
        [SerializeField] private MazeTransitionEase settleEase = MazeTransitionEase.Smooth;
        [SerializeField] private Vector2 customPointNormalized = new Vector2(0.5f, 0.5f);
        [SerializeField] private float startOffsetPixels = 140f;
        [SerializeField] private float moveSeconds = 0.24f;
        [SerializeField] private float fadeSeconds = 0.24f;
        [SerializeField] private float settleSeconds = 0f;
        [SerializeField] private float staggerSeconds = 0.46f;
        [SerializeField] private float holdSeconds = 0.05f;
        [SerializeField] private float randomDelayJitter = 1f;
        [Tooltip("Extra screen-position delay layered after the pattern's native timing. 0 keeps the current timing.")]
        [SerializeField] private float directionalSpreadSeconds = 0f;
        [SerializeField] private MazeTransitionSpreadDirection spreadDirection = MazeTransitionSpreadDirection.FromOrigin;
        [SerializeField] private MazeTransitionEase spreadEase = MazeTransitionEase.Linear;
        [SerializeField] private float randomTessellateStartScale = 0.94f;
        [SerializeField] private float randomTessellateShimmer = 0.28f;
        [SerializeField] private float lineThicknessPixels = 7f;
        [SerializeField] private float secondaryDelayScale = 0.55f;
        [SerializeField] private float effectIntensity = 1f;
        [SerializeField] private bool shrinkOutWithoutAlphaFade;
        [SerializeField] private int seed = 7321;
        [SerializeField] private bool randomizeEachPlay;

        [Header("Color")]
        [SerializeField] private Color movingColor = new Color(1.35f, 0.28f, 2.7f, 0f);
        [SerializeField] private Color accentColor = new Color(1.8f, 0.5f, 3.4f, 0.85f);
        [SerializeField] private Color settledColor = Color.black;
        [SerializeField] private Color outColor = new Color(0f, 0f, 0f, 0f);

        public override string TransitionId => "square";
        public int Columns => Mathf.Max(1, columns);
        public int Rows => Mathf.Max(1, rows);
        public int TileCount => Columns * Rows;
        public float OverscanPixels => Mathf.Max(0f, overscanPixels);
        public float GapPixels => Mathf.Max(0f, gapPixels);
        public float AlternateRowOffsetTiles => Mathf.Clamp(alternateRowOffsetTiles, -1f, 1f);
        public MazeSquareTransitionPatternKind MotionPattern => motionPattern;
        public MazeTransitionOrigin InOrigin => inOrigin;
        public MazeTransitionOrigin OutOrigin => outOrigin;
        public MazeTransitionOrder Order => order;
        public MazeTransitionEase MoveEase => moveEase;
        public MazeTransitionEase FadeEase => fadeEase;
        public MazeTransitionEase SettleEase => settleEase;
        public Vector2 CustomPointNormalized => customPointNormalized;
        public float StartOffsetPixels => Mathf.Max(0f, startOffsetPixels);
        public float MoveSeconds => Mathf.Max(0.01f, moveSeconds);
        public float FadeSeconds => Mathf.Max(0.01f, fadeSeconds);
        public float SettleSeconds => Mathf.Max(0f, settleSeconds);
        public float StaggerSeconds => Mathf.Max(0f, staggerSeconds);
        public float HoldSeconds => Mathf.Max(0f, holdSeconds);
        public float RandomDelayJitter => Mathf.Max(0f, randomDelayJitter);
        public float DirectionalSpreadSeconds => Mathf.Max(0f, directionalSpreadSeconds);
        public MazeTransitionSpreadDirection SpreadDirection => spreadDirection;
        public MazeTransitionEase SpreadEase => spreadEase;
        public float RandomTessellateStartScale => Mathf.Clamp(randomTessellateStartScale, 0.5f, 1f);
        public float RandomTessellateShimmer => Mathf.Max(0f, randomTessellateShimmer);
        public float LineThicknessPixels => Mathf.Max(1f, lineThicknessPixels);
        public float SecondaryDelayScale => Mathf.Max(0f, secondaryDelayScale);
        public float EffectIntensity => Mathf.Max(0f, effectIntensity);
        public bool ShrinkOutWithoutAlphaFade => shrinkOutWithoutAlphaFade;
        public int Seed => seed;
        public bool RandomizesPerPlay => randomizeEachPlay || UsesPerPlayRandomSeed(MotionPattern);
        public Color MovingColor => movingColor;
        public Color AccentColor => accentColor;
        public Color SettledColor => settledColor;
        public Color OutColor => outColor;

        public float BaseDelaySpanSeconds => MotionPattern == MazeSquareTransitionPatternKind.RandomTessellate ? RandomDelayJitter : StaggerSeconds + RandomDelayJitter;
        public float DelaySpanSeconds => BaseDelaySpanSeconds + DirectionalSpreadSeconds;
        public float InCoveredSeconds => DelaySpanSeconds + Mathf.Max(MoveSeconds, FadeSeconds) + SettleSeconds;
        public float InCompleteSeconds => InCoveredSeconds + HoldSeconds;
        public float OutCompleteSeconds => DelaySpanSeconds + Mathf.Max(MoveSeconds, FadeSeconds);

        public static MazeSquareTransitionProfile CreateRuntimeDefault(string label = "MAZE Square Transition", MazeTransitionBackend backend = MazeTransitionBackend.Overlay)
        {
            var profile = CreateInstance<MazeSquareTransitionProfile>();
            profile.name = label;
            profile.SetBackendForRuntime(backend);
            return profile;
        }

        public MazeSquareTransitionRuntimeSettings CaptureRuntimeSettings()
        {
            return new MazeSquareTransitionRuntimeSettings
            {
                Columns = columns,
                Rows = rows,
                OverscanPixels = overscanPixels,
                GapPixels = gapPixels,
                AlternateRowOffsetTiles = alternateRowOffsetTiles,
                MotionPattern = motionPattern,
                InOrigin = inOrigin,
                OutOrigin = outOrigin,
                Order = order,
                MoveEase = moveEase,
                FadeEase = fadeEase,
                SettleEase = settleEase,
                CustomPointNormalized = customPointNormalized,
                StartOffsetPixels = startOffsetPixels,
                MoveSeconds = moveSeconds,
                FadeSeconds = fadeSeconds,
                SettleSeconds = settleSeconds,
                StaggerSeconds = staggerSeconds,
                HoldSeconds = holdSeconds,
                RandomDelayJitter = randomDelayJitter,
                DirectionalSpreadSeconds = directionalSpreadSeconds,
                SpreadDirection = spreadDirection,
                SpreadEase = spreadEase,
                RandomTessellateStartScale = randomTessellateStartScale,
                RandomTessellateShimmer = randomTessellateShimmer,
                LineThicknessPixels = lineThicknessPixels,
                SecondaryDelayScale = secondaryDelayScale,
                EffectIntensity = effectIntensity,
                ShrinkOutWithoutAlphaFade = shrinkOutWithoutAlphaFade,
                Seed = seed,
                RandomizeEachPlay = randomizeEachPlay,
                MovingColor = movingColor,
                AccentColor = accentColor,
                SettledColor = settledColor,
                OutColor = outColor,
            };
        }

        public void ApplyRuntimeSettings(MazeSquareTransitionRuntimeSettings settings)
        {
            columns = settings.Columns;
            rows = settings.Rows;
            overscanPixels = settings.OverscanPixels;
            gapPixels = settings.GapPixels;
            alternateRowOffsetTiles = settings.AlternateRowOffsetTiles;
            motionPattern = settings.MotionPattern;
            inOrigin = settings.InOrigin;
            outOrigin = settings.OutOrigin;
            order = settings.Order;
            moveEase = settings.MoveEase;
            fadeEase = settings.FadeEase;
            settleEase = settings.SettleEase;
            customPointNormalized = settings.CustomPointNormalized;
            startOffsetPixels = settings.StartOffsetPixels;
            moveSeconds = settings.MoveSeconds;
            fadeSeconds = settings.FadeSeconds;
            settleSeconds = settings.SettleSeconds;
            staggerSeconds = settings.StaggerSeconds;
            holdSeconds = settings.HoldSeconds;
            randomDelayJitter = settings.RandomDelayJitter;
            directionalSpreadSeconds = settings.DirectionalSpreadSeconds;
            spreadDirection = settings.SpreadDirection;
            spreadEase = settings.SpreadEase;
            randomTessellateStartScale = settings.RandomTessellateStartScale;
            randomTessellateShimmer = settings.RandomTessellateShimmer;
            lineThicknessPixels = settings.LineThicknessPixels;
            secondaryDelayScale = settings.SecondaryDelayScale;
            effectIntensity = settings.EffectIntensity;
            shrinkOutWithoutAlphaFade = settings.ShrinkOutWithoutAlphaFade;
            seed = settings.Seed;
            randomizeEachPlay = settings.RandomizeEachPlay;
            movingColor = settings.MovingColor;
            accentColor = settings.AccentColor;
            settledColor = settings.SettledColor;
            outColor = settings.OutColor;
        }

        public static MazeSquareTransitionProfile CreateRuntimeBlackSquareReveal(string label = "MAZE Black Square Reveal", MazeTransitionSpreadDirection direction = MazeTransitionSpreadDirection.RightToLeft, float totalSeconds = 0.5f)
        {
            var profile = CreateRuntimeDefault(label);
            profile.columns = 42;
            profile.rows = 24;
            profile.overscanPixels = 36f;
            profile.gapPixels = 0f;
            profile.alternateRowOffsetTiles = 0.5f;
            profile.motionPattern = MazeSquareTransitionPatternKind.InkGridLock;
            profile.inOrigin = MazeTransitionOrigin.Center;
            profile.outOrigin = MazeTransitionOrigin.Center;
            profile.order = MazeTransitionOrder.Distance;
            profile.moveEase = MazeTransitionEase.Smooth;
            profile.fadeEase = MazeTransitionEase.Smooth;
            profile.settleEase = MazeTransitionEase.Smooth;
            profile.startOffsetPixels = 0f;
            profile.moveSeconds = Mathf.Clamp(totalSeconds * 0.28f, 0.08f, 0.18f);
            profile.fadeSeconds = profile.moveSeconds;
            profile.settleSeconds = 0f;
            profile.staggerSeconds = 0f;
            profile.holdSeconds = 0f;
            profile.randomDelayJitter = 0f;
            profile.directionalSpreadSeconds = Mathf.Max(0f, totalSeconds - profile.moveSeconds);
            profile.spreadDirection = direction;
            profile.spreadEase = MazeTransitionEase.Linear;
            profile.randomTessellateStartScale = 0.5f;
            profile.randomTessellateShimmer = 0f;
            profile.lineThicknessPixels = 2f;
            profile.secondaryDelayScale = 0f;
            profile.effectIntensity = 0f;
            profile.shrinkOutWithoutAlphaFade = true;
            profile.movingColor = new Color(0f, 0f, 0f, 0f);
            profile.accentColor = new Color(0f, 0f, 0f, 0f);
            profile.settledColor = Color.black;
            profile.outColor = new Color(0f, 0f, 0f, 0f);
            profile.randomizeEachPlay = false;
            profile.seed = 7321;
            return profile;
        }


        public void ConfigureDirectionalSpreadForRuntime(MazeTransitionSpreadDirection direction, float seconds, MazeTransitionEase ease = MazeTransitionEase.Linear)
        {
            spreadDirection = direction;
            directionalSpreadSeconds = Mathf.Max(0f, seconds);
            spreadEase = ease;
        }

        public static bool UsesPerPlayRandomSeed(MazeSquareTransitionPatternKind pattern)
        {
            return pattern == MazeSquareTransitionPatternKind.RandomTessellate
                || pattern == MazeSquareTransitionPatternKind.SignalCorruptionBlocks
                || pattern == MazeSquareTransitionPatternKind.BlockfallLock
                || pattern == MazeSquareTransitionPatternKind.CourierBlocks
                || pattern == MazeSquareTransitionPatternKind.SquaredCircuitDecay
                || pattern == MazeSquareTransitionPatternKind.VideowallSnap;
        }
    }
}
