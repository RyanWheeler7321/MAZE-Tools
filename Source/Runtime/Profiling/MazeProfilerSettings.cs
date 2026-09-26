using UnityEngine;

namespace Maze
{
    [CreateAssetMenu(fileName = "MAZE_ProfilerSettings", menuName = "MAZE/Profiling/Profiler Settings")]
    public sealed class MazeProfilerSettings : ScriptableObject
    {
        public const string DefaultAssetName = "MAZE_ProfilerSettings";

        [Header("Capture Defaults")]
        public bool captureInEditor = true;
        public bool captureInDevelopmentBuild = false;
        public bool captureInReleaseBuild = false;
        public bool allowSavedRuntimeToggle = true;
        public string commandLineEnableFlag = "-mazeProfile";
        public string savedToggleKey = "MAZE.Profile.CaptureEnabled";

        [Header("Sampling")]
        [Min(0.1f)] public float sampleIntervalSeconds = 0.25f;
        [Min(1f)] public float summaryLogIntervalSeconds = 10f;
        [Min(0f)] public float startupAnomalyIgnoreSeconds = 1.5f;
        [Min(0.25f)] public float anomalyLogCooldownSeconds = 2f;
        public bool captureCpuScopes = true;
        public bool logAnomalies = true;

        [Header("Thresholds")]
        [Min(1f)] public float slowFrameMs = 24f;
        [Min(1f)] public float severeFrameMs = 40f;
        [Min(1f)] public float slowCpuMs = 18f;
        [Min(1f)] public float slowGpuMs = 18f;
        [Min(0f)] public float gcAllocWarningMb = 0.5f;

        [Header("Benchmark")]
        [Min(0.1f)] public float baselineWarmupSeconds = 1f;
        [Min(0.25f)] public float baselineMeasureSeconds = 3f;
        [Min(0.05f)] public float variantWarmupSeconds = 0.5f;
        [Min(0.25f)] public float variantMeasureSeconds = 3f;
        [Min(1)] public int maxRankedSuspects = 8;
        [Min(0)] public int maxAutoObjectCandidates = 8;
        [Min(30)] public int fpsAnalysisTargetFps = 500;

        public static MazeProfilerSettings CreateRuntimeDefault()
        {
            var settings = CreateInstance<MazeProfilerSettings>();
            settings.name = DefaultAssetName + "_RuntimeFallback";
            return settings;
        }
    }
}
