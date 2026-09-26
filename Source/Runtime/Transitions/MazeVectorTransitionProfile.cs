using UnityEngine;

namespace Maze
{
    // Built from pooled MazeVector rects, outlines, corners and dashed frames, no sprites.
    [CreateAssetMenu(menuName = "MAZE/Transitions/Vector Transition", fileName = "MAZE_VectorTransition")]
    public sealed class MazeVectorTransitionProfile : MazeTransitionProfile
    {
        [Header("Pattern")]
        [SerializeField] private MazeVectorTransitionPatternKind pattern = MazeVectorTransitionPatternKind.VectorSigilSnap;
        [SerializeField] private MazeTransitionOrigin inOrigin = MazeTransitionOrigin.Center;
        [SerializeField] private MazeTransitionOrigin outOrigin = MazeTransitionOrigin.Center;
        [SerializeField] private MazeTransitionEase motionEase = MazeTransitionEase.EaseOut;
        [SerializeField] private MazeTransitionEase fadeEase = MazeTransitionEase.Smooth;
        [SerializeField] private Vector2 customPointNormalized = new(0.5f, 0.5f);
        [SerializeField] private int seed = 7321;
        [SerializeField] private bool randomizeEachPlay;

        [Header("Timing")]
        [SerializeField] private float inSeconds = 0.48f;
        [SerializeField] private float outSeconds = 0.36f;
        [SerializeField] private float delaySpanSeconds = 0.18f;
        [SerializeField] private float holdSeconds = 0.04f;
        [SerializeField] private float fillDelayNormalized = 0.58f;
        [SerializeField] private float accentLingeringSeconds = 0.16f;
        [Range(0f, 1f)]
        [SerializeField] private float orderedDelayBalance = 0.55f;
        [Tooltip("Extra screen-position delay layered after the pattern's native timing. 0 keeps the current timing.")]
        [SerializeField] private float directionalSpreadSeconds = 0f;
        [SerializeField] private MazeTransitionSpreadDirection spreadDirection = MazeTransitionSpreadDirection.FromOrigin;
        [SerializeField] private MazeTransitionEase spreadEase = MazeTransitionEase.Linear;

        [Header("Shape")]
        [SerializeField] private int primaryCount = 12;
        [SerializeField] private int secondaryCount = 5;
        [SerializeField] private float lineThicknessPixels = 6f;
        [SerializeField] private float accentThicknessPixels = 3f;
        [SerializeField] private float gapPixels = 0f;
        [SerializeField] private float overscanPixels = 36f;
        [SerializeField] private float intensity = 1f;
        [SerializeField] private float slantPixels = 100f;
        [SerializeField] private float jitterPixels = 20f;

        [Header("Color")]
        [SerializeField] private Color fillColor = Color.black;
        [SerializeField] private Color accentColor = new(1.6f, 0.42f, 3.1f, 0.95f);
        [SerializeField] private Color secondaryColor = new(0.75f, 0.22f, 1.35f, 0.65f);
        [SerializeField] private Color clearColor = new(0f, 0f, 0f, 0f);

        public override string TransitionId => "vector";
        public MazeVectorTransitionPatternKind Pattern => pattern;
        public MazeTransitionOrigin InOrigin => inOrigin;
        public MazeTransitionOrigin OutOrigin => outOrigin;
        public MazeTransitionEase MotionEase => motionEase;
        public MazeTransitionEase FadeEase => fadeEase;
        public Vector2 CustomPointNormalized => customPointNormalized;
        public int Seed => seed;
        public bool RandomizesPerPlay => randomizeEachPlay || UsesPerPlayRandomSeed(Pattern);
        public float InSeconds => Mathf.Max(0.05f, inSeconds);
        public float OutSeconds => Mathf.Max(0.05f, outSeconds);
        public float DelaySpanSeconds => Mathf.Max(0f, delaySpanSeconds);
        public float HoldSeconds => Mathf.Max(0f, holdSeconds);
        public float FillDelayNormalized => Mathf.Clamp01(fillDelayNormalized);
        public float AccentLingeringSeconds => Mathf.Max(0f, accentLingeringSeconds);
        public float OrderedDelayBalance => Mathf.Clamp01(orderedDelayBalance);
        public float DirectionalSpreadSeconds => Mathf.Max(0f, directionalSpreadSeconds);
        public MazeTransitionSpreadDirection SpreadDirection => spreadDirection;
        public MazeTransitionEase SpreadEase => spreadEase;
        public int PrimaryCount => Mathf.Max(1, primaryCount);
        public int SecondaryCount => Mathf.Max(0, secondaryCount);
        public float LineThicknessPixels => Mathf.Max(1f, lineThicknessPixels);
        public float AccentThicknessPixels => Mathf.Max(1f, accentThicknessPixels);
        public float GapPixels => Mathf.Max(0f, gapPixels);
        public float OverscanPixels => Mathf.Max(0f, overscanPixels);
        public float Intensity => Mathf.Max(0f, intensity);
        public float SlantPixels => slantPixels;
        public float JitterPixels => Mathf.Max(0f, jitterPixels);
        public Color FillColor => fillColor;
        public Color AccentColor => accentColor;
        public Color SecondaryColor => secondaryColor;
        public Color ClearColor => clearColor;

        public float InCoveredSeconds => DelaySpanSeconds + DirectionalSpreadSeconds + InSeconds;
        public float InCompleteSeconds => InCoveredSeconds + HoldSeconds;
        public float OutCompleteSeconds => DelaySpanSeconds + DirectionalSpreadSeconds + OutSeconds;

        public static MazeVectorTransitionProfile CreateRuntimeDefault(string label = "MAZE Vector Transition")
        {
            var profile = CreateInstance<MazeVectorTransitionProfile>();
            profile.name = label;
            return profile;
        }

        public static MazeVectorTransitionProfile CreateRuntimePreset(MazeVectorTransitionPatternKind patternKind, string label = null)
        {
            var profile = CreateRuntimeDefault(string.IsNullOrWhiteSpace(label) ? $"MAZE {patternKind} Transition" : label);
            profile.pattern = patternKind;
            profile.inSeconds = patternKind == MazeVectorTransitionPatternKind.HardCutAfterimage ? 0.22f : 0.42f;
            profile.outSeconds = patternKind == MazeVectorTransitionPatternKind.HardCutAfterimage ? 0.18f : 0.3f;
            profile.delaySpanSeconds = patternKind == MazeVectorTransitionPatternKind.HardCutAfterimage ? 0.12f : 0.18f;
            profile.primaryCount = patternKind == MazeVectorTransitionPatternKind.HardCutAfterimage ? 14 : 8;
            profile.secondaryCount = 5;
            profile.lineThicknessPixels = 6f;
            profile.accentThicknessPixels = 2.5f;
            profile.overscanPixels = 44f;
            profile.fillDelayNormalized = patternKind == MazeVectorTransitionPatternKind.HardCutAfterimage ? 0.05f : 0.65f;
            profile.intensity = 1f;
            profile.accentColor = new Color(1.8f, 0.45f, 3.35f, 0.95f);
            profile.secondaryColor = new Color(0.75f, 0.22f, 1.45f, 0.6f);
            profile.fillColor = Color.black;
            profile.randomizeEachPlay = UsesPerPlayRandomSeed(patternKind);
            return profile;
        }

        public void ConfigureDirectionalSpreadForRuntime(MazeTransitionSpreadDirection direction, float seconds, MazeTransitionEase ease = MazeTransitionEase.Linear)
        {
            spreadDirection = direction;
            directionalSpreadSeconds = Mathf.Max(0f, seconds);
            spreadEase = ease;
        }

        public void ConfigureOrderedDelayBalanceForRuntime(float balance)
        {
            orderedDelayBalance = Mathf.Clamp01(balance);
        }

        public static bool UsesPerPlayRandomSeed(MazeVectorTransitionPatternKind patternKind)
        {
            return patternKind == MazeVectorTransitionPatternKind.HardCutAfterimage;
        }
    }
}
