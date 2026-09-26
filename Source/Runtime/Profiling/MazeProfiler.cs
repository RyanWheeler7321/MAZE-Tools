using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Maze
{
    [DefaultExecutionOrder(-4900)]
    [DisallowMultipleComponent]
    public sealed class MazeProfiler : MonoBehaviour
    {
        private const double NanosecondsToMilliseconds = 1e-6;
        private const long BytesPerMegabyte = 1024L * 1024L;
        private const string LogFolderName = "MazeProfiling";
        private const int MaxUnityLogEventsPerRun = 64;
        private const int MaxUnityLogConditionChars = 700;
        private const int MaxUnityLogStackChars = 2500;
        private const int MaxAdaptiveCombinationTests = 8;

        private static readonly CounterRequest[] CounterRequests =
        {
            new("mainThreadMs", MetricKind.TimeMilliseconds, "CPU Main Thread Frame Time", "Main Thread", "CPU Main Thread"),
            new("renderThreadMs", MetricKind.TimeMilliseconds, "CPU Render Thread Frame Time", "Render Thread", "CPU Render Thread"),
            new("gpuFrameMs", MetricKind.TimeMilliseconds, "GPU Frame Time", "GPU Frame"),
            new(
                "drawCalls",
                MetricKind.Count,
                true,
                "Draw Calls Count",
                "Draw Calls",
                "Standard Draw Calls Count",
                "Standard Indirect Draw Calls Count",
                "Standard Instanced Draw Calls Count",
                "SRP Batcher Draw Calls Count",
                "BRG Draw Calls Count",
                "BRG Indirect Draw Calls Count",
                "Null Geometry Draw Calls Count",
                "Null Geometry Indirect Draw Calls Count"),
            new("setPassCalls", MetricKind.Count, "SetPass Calls Count", "SetPass Calls"),
            new("batches", MetricKind.Count, "Total Batches Count", "Batches Count", "Batches"),
            new("triangles", MetricKind.Count, "Triangles Count", "Triangles"),
            new("vertices", MetricKind.Count, "Vertices Count", "Vertices"),
            new("renderTextures", MetricKind.Count, "Render Textures Count", "RenderTexture Count"),
            new("renderTextureBytes", MetricKind.Bytes, "Render Textures Bytes", "RenderTexture Bytes"),
            new("gcAllocatedInFrameBytes", MetricKind.Bytes, "GC Allocated In Frame", "GC Allocation In Frame"),
            new("gcUsedMemoryBytes", MetricKind.Bytes, "GC Used Memory", "Mono Used Memory"),
            new("totalUsedMemoryBytes", MetricKind.Bytes, "Total Used Memory")
        };

        private readonly List<MetricRecorder> recorders = new();
        private readonly List<string> missingCounters = new();
        private readonly List<string> counterCandidates = new();
        private readonly List<string> resolvedCounters = new();
        private readonly HashSet<string> availableMetricKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ScopeStat> scopeStats = new(StringComparer.Ordinal);
        private readonly FrameTiming[] frameTimings = new FrameTiming[4];
        private readonly StringBuilder builder = new(4096);
        private readonly List<MazeProfilingFeatureHandle> features = new(16);
        private readonly List<MazeBenchmarkResult> benchmarkResults = new(16);
        private readonly List<MazeSkippedProfilingFeature> skippedFpsAnalysisFeatures = new(16);

        [SerializeField] private MazeProfilerSettings settingsAsset;

        private MazeProfilerSettings settings;
        private MazeProfilerSnapshot latest = new();
        private Coroutine benchmarkCoroutine;
        private bool captureActive;
        private bool captureCpuScopes = true;
        private bool logAnomalies = true;
        private bool stopBenchmarkRequested;
        private bool wroteStopEvent;
        private string logPath;
        private string latestPath;
        private string latestBenchmarkSummary = "none yet";
        private string latestFpsAnalysisJson = string.Empty;
        private string benchmarkStatus = "benchmark idle";
        private float benchmarkProgress;
        private float benchmarkProgressHideAt;
        private float sampleTimer;
        private float summaryLogTimer;
        private float anomalyCooldownTimer;
        private float captureStartedAt;
        private int intervalFrameCount;
        private int loggedUnityLogEvents;
        private int droppedUnityLogEvents;
        private int warningCount;
        private int errorCount;
        private int exceptionCount;
        private double intervalUnscaledSeconds;
        private int droppedFrameStreak;

        public static MazeProfiler Instance { get; private set; }
        public static int FpsAnalysisMaxObjectCandidatesOverride { get; set; } = -1;
        public static int FpsAnalysisTargetFpsOverride { get; set; } = -1;

        public bool CaptureActive => captureActive;
        public bool CaptureCpuScopes => captureCpuScopes;
        public bool LogAnomalies => logAnomalies;
        public bool BenchmarkActive => benchmarkCoroutine != null;
        public bool FpsAnalysisActive => benchmarkCoroutine != null;
        public string LogPath => logPath;
        public string LatestPath => latestPath;
        public string LatestBenchmarkSummary => latestBenchmarkSummary;
        public string LatestFpsAnalysisSummary => latestBenchmarkSummary;
        public string LatestFpsAnalysisJson => latestFpsAnalysisJson;
        public string BenchmarkStatus => benchmarkStatus;
        public string FpsAnalysisStatus => benchmarkStatus;
        public float BenchmarkProgress => benchmarkProgress;
        public float FpsAnalysisProgress => benchmarkProgress;
        public bool ShouldShowBenchmarkProgress => BenchmarkActive || Time.unscaledTime < benchmarkProgressHideAt;
        public bool ShouldShowFpsAnalysisProgress => ShouldShowBenchmarkProgress;
        public MazeProfilerSnapshot Latest => latest;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            settings = settingsAsset != null ? settingsAsset : MazeProfilerSettings.CreateRuntimeDefault();
            ApplySettings();
            captureActive = ShouldCaptureOnStart();
            captureStartedAt = Time.unscaledTime;
            ConfigureSession();
            MazeDiagnosticsLog.Info("Profiler", "awake", "profiler session configured", MazeDiagnosticsLog.JoinData(
                MazeDiagnosticsLog.JsonBool("capture", captureActive),
                MazeDiagnosticsLog.JsonBool("scopes", captureCpuScopes),
                MazeDiagnosticsLog.JsonString("profileLog", logPath),
                MazeDiagnosticsLog.JsonString("diagnosticsLog", MazeDiagnosticsLog.LogPath)));
            Application.logMessageReceived += HandleUnityLog;
        }

        private void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleUnityLog;

            if (benchmarkCoroutine != null)
            {
                stopBenchmarkRequested = true;
                StopCoroutine(benchmarkCoroutine);
                benchmarkCoroutine = null;
            }

            WriteStopEvent();
            DisposeRecorders();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            WriteStopEvent();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            FrameTimingManager.CaptureFrameTimings();

            var delta = Time.unscaledDeltaTime;
            sampleTimer += delta;
            summaryLogTimer += delta;
            anomalyCooldownTimer = Mathf.Max(0f, anomalyCooldownTimer - delta);
            intervalFrameCount++;
            intervalUnscaledSeconds += delta;

            if (sampleTimer < settings.sampleIntervalSeconds)
            {
                return;
            }

            sampleTimer = 0f;
            UpdateSnapshot(true);
            if (captureActive && logAnomalies)
            {
                MaybeLogAnomaly();
            }

            if (captureActive && summaryLogTimer >= settings.summaryLogIntervalSeconds)
            {
                summaryLogTimer = 0f;
                WriteJsonEvent("sample", BuildSnapshotJson(latest, false));
            }
        }

        public static MazeProfileScope Sample(string sampleName)
        {
            if (string.IsNullOrWhiteSpace(sampleName))
            {
                return default;
            }

            Profiler.BeginSample(sampleName);
            var profiler = Instance;
            var shouldRecord = profiler != null && profiler.captureActive && profiler.captureCpuScopes && Application.isPlaying;
            return new MazeProfileScope(profiler, sampleName, shouldRecord, true);
        }

        public void ToggleCapture()
        {
            SetCaptureActive(!captureActive, true);
        }

        public void SetCaptureActive(bool active)
        {
            SetCaptureActive(active, true);
        }

        public void ToggleAnomalyLogging()
        {
            logAnomalies = !logAnomalies;
            UpdateSnapshot(false);
            WriteJsonEvent("manual_snapshot", $"\"action\":\"{(logAnomalies ? "anomaly_log_on" : "anomaly_log_off")}\",{BuildSnapshotJson(latest, true)}");
            MazeDiagnosticsLog.Info("Profiler", "anomaly_toggle", "anomaly logging toggled", MazeDiagnosticsLog.JsonBool("enabled", logAnomalies));
        }

        public void LogManualSnapshot()
        {
            UpdateSnapshot(false);
            WriteJsonEvent("manual_snapshot", BuildSnapshotJson(latest, true));
            MazeDiagnosticsLog.Snapshot("Profiler", "manual_snapshot", MazeDiagnosticsLog.JoinData(
                MazeDiagnosticsLog.JsonNumber("fps", latest.fps),
                MazeDiagnosticsLog.JsonNumber("frameMs", latest.frameMs),
                MazeDiagnosticsLog.JsonNumber("drawCalls", latest.drawCalls),
                MazeDiagnosticsLog.JsonNumber("setPass", latest.setPassCalls),
                MazeDiagnosticsLog.JsonString("verdict", latest.verdict)));
        }

        public void StartBenchmark()
        {
            StartFpsAnalysis();
        }

        public void StartFpsAnalysis()
        {
            if (benchmarkCoroutine != null)
            {
                return;
            }

            benchmarkProgress = 0f;
            benchmarkStatus = "FPS Analysis starting";
            MazeDiagnosticsLog.Info("Profiler", "fps_analysis_start", "FPS Analysis requested");
            benchmarkCoroutine = StartCoroutine(RunBenchmark());
        }

        public void StopBenchmark()
        {
            StopFpsAnalysis();
        }

        public void StopFpsAnalysis()
        {
            stopBenchmarkRequested = true;
            benchmarkStatus = "FPS Analysis stopping";
            MazeDiagnosticsLog.Info("Profiler", "fps_analysis_stop", "FPS Analysis stop requested");
        }

        public string BuildOverlayText()
        {
            if (string.IsNullOrWhiteSpace(latest.verdict))
            {
                UpdateSnapshot(false);
            }

            builder.Clear();
            builder.AppendLine($"Capture: {(captureActive ? "ON" : "OFF")}   Mode: {(BenchmarkActive ? "FPS Analysis" : "normal")}   Anomaly log: {(logAnomalies ? "ON" : "OFF")}");
            builder.AppendLine($"Verdict: {latest.verdict}");
            builder.AppendLine($"FPS {latest.fps:0.0} | Frame {latest.frameMs:0.00} ms | CPU {latest.cpuFrameMs:0.00} ms | Main {latest.mainThreadMs:0.00} ms | Render {latest.renderThreadMs:0.00} ms | GPU {latest.gpuFrameMs:0.00} ms");
            builder.AppendLine($"Render: draw {latest.drawCalls:0} | batches {FormatMetricCount(latest.batches, latest.batchesAvailable)} | setpass {latest.setPassCalls:0} | tris {FormatCount(latest.triangles)} | verts {FormatCount(latest.vertices)} | RT {latest.renderTextures:0} / {FormatBytes(latest.renderTextureBytes)}");
            builder.AppendLine($"Memory: total {FormatBytes(latest.totalAllocatedMemoryBytes)} | reserved {FormatBytes(latest.totalReservedMemoryBytes)} | GC heap {FormatBytes(latest.gcUsedMemoryBytes)} | GC/frame {FormatMetricBytes(latest.gcAllocatedInFrameBytes, latest.gcAllocatedInFrameAvailable)}");
            builder.AppendLine($"Quality: {latest.qualityLevel} | vsync {latest.vSyncCount} | target {latest.targetFrameRate} | res {latest.resolution} | renderScale {latest.renderScale:0.00} | API {latest.graphicsApi}");
            builder.AppendLine();
            builder.AppendLine("FPS Analysis suspects:");
            builder.AppendLine(latestBenchmarkSummary);
            builder.AppendLine();
            builder.AppendLine("Feature handles:");

            RefreshFeatures();
            if (features.Count == 0)
            {
                builder.AppendLine("  none registered");
            }
            else
            {
                for (var i = 0; i < features.Count; i++)
                {
                    var feature = features[i];
                    builder.AppendLine($"  - {feature.Label}: {feature.CurrentState}");
                }
            }

            builder.AppendLine();
            builder.AppendLine("Top MAZE CPU scopes:");
            if (latest.topScopes.Count == 0)
            {
                builder.AppendLine("  none recorded yet");
            }
            else
            {
                for (var i = 0; i < latest.topScopes.Count; i++)
                {
                    var scope = latest.topScopes[i];
                    builder.AppendLine($"  {i + 1}. {scope.name} avg {scope.averageMs:0.000} ms peak {scope.peakMs:0.000} ms calls {scope.calls}");
                }
            }

            builder.AppendLine();
            builder.AppendLine($"Log: {logPath}");
            return builder.ToString().TrimEnd();
        }

        internal void RecordScopeSample(string sampleName, double elapsedMs)
        {
            if (!scopeStats.TryGetValue(sampleName, out var stat))
            {
                stat = new ScopeStat(sampleName);
                scopeStats.Add(sampleName, stat);
            }

            stat.windowTotalMs += elapsedMs;
            stat.windowCalls++;
            stat.windowPeakMs = Math.Max(stat.windowPeakMs, elapsedMs);
            stat.lifetimePeakMs = Math.Max(stat.lifetimePeakMs, elapsedMs);
        }

        private void ApplySettings()
        {
            captureCpuScopes = settings.captureCpuScopes;
            logAnomalies = settings.logAnomalies;
        }

        private bool ShouldCaptureOnStart()
        {
            if (HasCommandLineFlag(settings.commandLineEnableFlag))
            {
                return true;
            }

#if UNITY_EDITOR
            return settings.captureInEditor;
#else
            if (settings.allowSavedRuntimeToggle && PlayerPrefs.HasKey(settings.savedToggleKey))
            {
                return PlayerPrefs.GetInt(settings.savedToggleKey, 0) != 0;
            }

            return Debug.isDebugBuild ? settings.captureInDevelopmentBuild : settings.captureInReleaseBuild;
#endif
        }

        private void SetCaptureActive(bool active, bool persist)
        {
            if (captureActive == active)
            {
                return;
            }

            captureActive = active;
            if (captureActive)
            {
                captureStartedAt = Time.unscaledTime;
                sampleTimer = 0f;
                summaryLogTimer = 0f;
                intervalFrameCount = 0;
                intervalUnscaledSeconds = 0.0;
            }

            if (persist && settings.allowSavedRuntimeToggle)
            {
                PlayerPrefs.SetInt(settings.savedToggleKey, captureActive ? 1 : 0);
                PlayerPrefs.Save();
            }

            UpdateSnapshot(false);
            WriteJsonEvent("manual_snapshot", $"\"action\":\"{(captureActive ? "capture_on" : "capture_off")}\",{BuildSnapshotJson(latest, true)}");
            MazeDiagnosticsLog.Info("Profiler", "capture_toggle", "profile capture toggled", MazeDiagnosticsLog.JsonBool("enabled", captureActive));
        }

        private void ConfigureSession()
        {
            ConfigureLogPath();
            ConfigureRecorders();
            UpdateSnapshot(false);
            WriteJsonEvent("start", BuildStartJson());
        }

        private void ConfigureLogPath()
        {
            var folder = Path.Combine(Application.persistentDataPath, LogFolderName);
            Directory.CreateDirectory(folder);
            logPath = Path.Combine(folder, $"maze_profile_{DateTime.Now:yyyyMMdd_HHmmss}.jsonl");
            latestPath = Path.Combine(folder, "latest.json");
        }

        private void ConfigureRecorders()
        {
            DisposeRecorders();
            missingCounters.Clear();
            counterCandidates.Clear();
            resolvedCounters.Clear();
            availableMetricKeys.Clear();

            var available = new List<ProfilerRecorderHandle>(512);
            ProfilerRecorderHandle.GetAvailable(available);
            var byName = new Dictionary<string, ProfilerRecorderHandle>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < available.Count; i++)
            {
                var handle = available[i];
                if (!handle.Valid)
                {
                    continue;
                }

                var description = ProfilerRecorderHandle.GetDescription(handle);
                if (!string.IsNullOrWhiteSpace(description.Name) && !byName.ContainsKey(description.Name))
                {
                    byName.Add(description.Name, handle);
                    MaybeAddCounterCandidate(description.Name);
                }
            }

            for (var i = 0; i < CounterRequests.Length; i++)
            {
                var request = CounterRequests[i];
                var matchedAny = false;
                for (var n = 0; n < request.names.Length; n++)
                {
                    if (!byName.TryGetValue(request.names[n], out var handle))
                    {
                        continue;
                    }

                    matchedAny = true;
                    TryAddRecorder(request, request.names[n], handle);

                    if (!request.aggregateAllMatches)
                    {
                        break;
                    }
                }

                if (!matchedAny)
                {
                    missingCounters.Add($"{request.key}:{string.Join("|", request.names)}");
                }
            }
        }

        private void TryAddRecorder(CounterRequest request, string resolvedName, ProfilerRecorderHandle handle)
        {
            try
            {
                var recorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
                if (recorder.Valid)
                {
                    recorders.Add(new MetricRecorder(request.key, resolvedName, request.kind, recorder));
                    resolvedCounters.Add($"{request.key}:{resolvedName}");
                    availableMetricKeys.Add(request.key);
                }
                else
                {
                    recorder.Dispose();
                    missingCounters.Add($"{request.key}:{resolvedName}");
                }
            }
            catch (Exception exception)
            {
                missingCounters.Add($"{request.key}:{resolvedName}:{exception.GetType().Name}");
            }
        }

        private void MaybeAddCounterCandidate(string counterName)
        {
            if (counterCandidates.Count >= 80 || string.IsNullOrWhiteSpace(counterName))
            {
                return;
            }

            var lower = counterName.ToLowerInvariant();
            if (!lower.Contains("draw")
                && !lower.Contains("batch")
                && !lower.Contains("pass")
                && !lower.Contains("tri")
                && !lower.Contains("vert")
                && !lower.Contains("render texture")
                && !lower.Contains("gc"))
            {
                return;
            }

            counterCandidates.Add(counterName);
        }

        private void DisposeRecorders()
        {
            for (var i = 0; i < recorders.Count; i++)
            {
                recorders[i].Dispose();
            }

            recorders.Clear();
        }

        private void UpdateSnapshot(bool resetInterval)
        {
            latest = BuildSnapshot(resetInterval, true);
        }

        private MazeProfilerSnapshot BuildSnapshot(bool resetInterval, bool includeScopes)
        {
            var scene = SceneManager.GetActiveScene();
            var timing = ReadFrameTiming();
            var fps = intervalUnscaledSeconds > 0.0001 ? intervalFrameCount / intervalUnscaledSeconds : 1.0 / Math.Max(Time.unscaledDeltaTime, 0.0001f);
            if (resetInterval)
            {
                intervalFrameCount = 0;
                intervalUnscaledSeconds = 0.0;
            }

            var renderPipeline = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline
                : GraphicsSettings.defaultRenderPipeline;
            var urpAsset = renderPipeline as UniversalRenderPipelineAsset;

            var snapshot = new MazeProfilerSnapshot
            {
                frameIndex = Time.frameCount,
                utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                sceneName = scene.name,
                scenePath = scene.path,
                fps = fps,
                frameMs = 1000.0 / Math.Max(fps, 0.0001),
                cpuFrameMs = timing.cpuFrameMs,
                mainThreadMs = timing.mainThreadMs,
                renderThreadMs = timing.renderThreadMs,
                gpuFrameMs = timing.gpuFrameMs,
                drawCalls = ReadMetric("drawCalls"),
                setPassCalls = ReadMetric("setPassCalls"),
                batches = ReadMetric("batches"),
                batchesAvailable = IsMetricAvailable("batches"),
                triangles = ReadMetric("triangles"),
                vertices = ReadMetric("vertices"),
                renderTextures = ReadMetric("renderTextures"),
                renderTextureBytes = ReadMetric("renderTextureBytes"),
                gcAllocatedInFrameBytes = ReadMetric("gcAllocatedInFrameBytes"),
                gcAllocatedInFrameAvailable = IsMetricAvailable("gcAllocatedInFrameBytes"),
                gcUsedMemoryBytes = ReadMetric("gcUsedMemoryBytes"),
                totalUsedMemoryBytes = ReadMetric("totalUsedMemoryBytes"),
                totalAllocatedMemoryBytes = Profiler.GetTotalAllocatedMemoryLong(),
                totalReservedMemoryBytes = Profiler.GetTotalReservedMemoryLong(),
                qualityLevel = QualitySettings.names.Length > QualitySettings.GetQualityLevel() ? QualitySettings.names[QualitySettings.GetQualityLevel()] : QualitySettings.GetQualityLevel().ToString(CultureInfo.InvariantCulture),
                vSyncCount = QualitySettings.vSyncCount,
                targetFrameRate = Application.targetFrameRate,
                resolution = $"{Screen.width}x{Screen.height}@{Screen.currentResolution.refreshRateRatio.value:0.##}",
                renderScale = urpAsset != null ? urpAsset.renderScale : 1f,
                pipeline = renderPipeline != null ? renderPipeline.name : "Built-in / None",
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                unityWarnings = warningCount,
                unityErrors = errorCount,
                unityExceptions = exceptionCount,
                unityLogsLogged = loggedUnityLogEvents,
                unityLogsDropped = droppedUnityLogEvents,
                systemSummary = BuildSystemSummary()
            };

            if (snapshot.mainThreadMs <= 0.001)
            {
                snapshot.mainThreadMs = Math.Max(snapshot.cpuFrameMs, ReadMetric("mainThreadMs"));
            }

            if (snapshot.renderThreadMs <= 0.001)
            {
                snapshot.renderThreadMs = ReadMetric("renderThreadMs");
            }

            if (snapshot.gpuFrameMs <= 0.001)
            {
                snapshot.gpuFrameMs = ReadMetric("gpuFrameMs");
            }

            if (snapshot.cpuFrameMs <= 0.001)
            {
                snapshot.cpuFrameMs = snapshot.mainThreadMs > 0.001 ? snapshot.mainThreadMs : ReadMetric("mainThreadMs");
            }

            if (snapshot.gcUsedMemoryBytes <= 0.0)
            {
                snapshot.gcUsedMemoryBytes = Profiler.GetMonoUsedSizeLong();
            }

            snapshot.topScopes = includeScopes ? BuildTopScopeSamples() : latest.topScopes;
            snapshot.verdict = BuildVerdict(snapshot);
            return snapshot;
        }

        private FrameTimingValues ReadFrameTiming()
        {
            var count = FrameTimingManager.GetLatestTimings((uint)frameTimings.Length, frameTimings);
            if (count <= 0)
            {
                return default;
            }

            var newest = frameTimings[0];
            return new FrameTimingValues(newest.cpuFrameTime, newest.cpuMainThreadFrameTime, newest.cpuRenderThreadFrameTime, newest.gpuFrameTime);
        }

        private bool IsMetricAvailable(string key)
        {
            return availableMetricKeys.Contains(key);
        }

        private double ReadMetric(string key)
        {
            var found = false;
            var total = 0.0;
            for (var i = 0; i < recorders.Count; i++)
            {
                var metric = recorders[i];
                if (!string.Equals(metric.key, key, StringComparison.Ordinal) || !metric.recorder.Valid)
                {
                    continue;
                }

                var value = metric.recorder.LastValue;
                total += metric.kind == MetricKind.TimeMilliseconds ? value * NanosecondsToMilliseconds : value;
                found = true;
            }

            return found ? total : 0.0;
        }

        private List<MazeProfilerScopeSample> BuildTopScopeSamples()
        {
            var list = new List<MazeProfilerScopeSample>(scopeStats.Count);
            foreach (var pair in scopeStats)
            {
                var stat = pair.Value;
                if (stat.windowCalls <= 0)
                {
                    continue;
                }

                var sample = new MazeProfilerScopeSample
                {
                    name = stat.name,
                    averageMs = stat.windowTotalMs / stat.windowCalls,
                    peakMs = stat.windowPeakMs,
                    calls = stat.windowCalls
                };

                list.Add(sample);
                stat.lastAverageMs = sample.averageMs;
                stat.lastPeakMs = sample.peakMs;
                stat.lastCalls = sample.calls;
                stat.windowTotalMs = 0.0;
                stat.windowPeakMs = 0.0;
                stat.windowCalls = 0;
            }

            return list
                .OrderByDescending(sample => Math.Max(sample.peakMs, sample.averageMs))
                .ThenBy(sample => sample.name, StringComparer.Ordinal)
                .Take(8)
                .ToList();
        }

        private string BuildVerdict(MazeProfilerSnapshot snapshot)
        {
            if (benchmarkResults.Count > 0)
            {
                return latestBenchmarkSummary;
            }

            var frameMs = Math.Max(snapshot.frameMs, Math.Max(snapshot.cpuFrameMs, snapshot.gpuFrameMs));
            if (frameMs >= settings.severeFrameMs)
            {
                droppedFrameStreak++;
            }
            else if (frameMs < settings.slowFrameMs)
            {
                droppedFrameStreak = 0;
            }

            if (snapshot.gpuFrameMs > settings.slowGpuMs && snapshot.gpuFrameMs > snapshot.mainThreadMs * 1.15)
            {
                return "GPU-bound likely. Run FPS Analysis for ranked clouds/fog/light shafts/SSR/water deltas.";
            }

            if (snapshot.renderThreadMs > settings.slowCpuMs && snapshot.renderThreadMs > snapshot.mainThreadMs * 0.75)
            {
                return "Render-thread or draw-call bound likely. Run FPS Analysis and check SetPass, batches, camera passes.";
            }

            if (snapshot.mainThreadMs > settings.slowCpuMs || snapshot.cpuFrameMs > settings.slowCpuMs)
            {
                return "CPU main-thread bound likely. Check Top MAZE CPU scopes, physics, scripts, GC, and FPS Analysis deltas.";
            }

            if (snapshot.gcAllocatedInFrameAvailable && snapshot.gcAllocatedInFrameBytes >= settings.gcAllocWarningMb * BytesPerMegabyte)
            {
                return "GC allocation spike. Check scripts allocating during Update/LateUpdate/UI refresh.";
            }

            if (frameMs > settings.slowFrameMs && droppedFrameStreak >= 2)
            {
                return "Repeated frame pacing slowdown without a single obvious owner. Use FPS Analysis plus Unity Profiler/Frame Debugger.";
            }

            if (frameMs > settings.slowFrameMs)
            {
                return "single slow frame pacing sample without CPU/GPU owner.";
            }

            return "healthy at current sample.";
        }

        private void MaybeLogAnomaly()
        {
            if (settings.startupAnomalyIgnoreSeconds > 0f && Time.unscaledTime - captureStartedAt < settings.startupAnomalyIgnoreSeconds)
            {
                return;
            }

            var frameMs = Math.Max(latest.frameMs, Math.Max(latest.cpuFrameMs, latest.gpuFrameMs));
            var gcWarningBytes = settings.gcAllocWarningMb * BytesPerMegabyte;
            var hasSlowOwner = latest.cpuFrameMs >= settings.slowCpuMs
                || latest.mainThreadMs >= settings.slowCpuMs
                || latest.gpuFrameMs >= settings.slowGpuMs
                || latest.renderThreadMs >= settings.slowCpuMs;
            var suspicious = hasSlowOwner
                || (latest.gcAllocatedInFrameAvailable && latest.gcAllocatedInFrameBytes >= gcWarningBytes)
                || (frameMs >= settings.slowFrameMs && droppedFrameStreak >= 2);

            if (!suspicious || anomalyCooldownTimer > 0f)
            {
                return;
            }

            anomalyCooldownTimer = settings.anomalyLogCooldownSeconds;
            WriteJsonEvent("slow_frame", BuildSnapshotJson(latest, true));
        }

        private IEnumerator RunBenchmark()
        {
            stopBenchmarkRequested = false;
            benchmarkResults.Clear();
            latestFpsAnalysisJson = string.Empty;
            latestBenchmarkSummary = "FPS Analysis running";
            benchmarkProgress = 0f;
            benchmarkStatus = "discovering features";
            RefreshFeatures();
            skippedFpsAnalysisFeatures.Clear();
            for (var i = features.Count - 1; i >= 0; i--)
            {
                var feature = features[i];
                if (!ShouldAnalyzeFeature(feature, out var skipReason))
                {
                    skippedFpsAnalysisFeatures.Add(new MazeSkippedProfilingFeature(feature.Id, feature.Label, feature.Category, feature.CurrentState, skipReason));
                    features.RemoveAt(i);
                }
            }

            skippedFpsAnalysisFeatures.Reverse();
            var originals = new Dictionary<MazeProfilingFeatureHandle, object>();
            for (var i = 0; i < features.Count; i++)
            {
                originals[features[i]] = features[i].CaptureState();
            }

            var previousCapture = captureActive;
            var previousVSync = QualitySettings.vSyncCount;
            var previousTargetFps = Application.targetFrameRate;
            var forcedTargetFps = FpsAnalysisTargetFpsOverride >= 30
                ? FpsAnalysisTargetFpsOverride
                : GetCommandLineInt("-mazeFpsAnalysisTargetFps", Mathf.Max(30, settings.fpsAnalysisTargetFps));
            SetCaptureActive(true, false);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = forcedTargetFps;
            var startPayload = "\"phase\":\"start\",\"featureCount\":" + features.Count.ToString(CultureInfo.InvariantCulture)
                + ",\"skippedFeatureCount\":" + skippedFpsAnalysisFeatures.Count.ToString(CultureInfo.InvariantCulture)
                + ",\"previousVSync\":" + previousVSync.ToString(CultureInfo.InvariantCulture)
                + ",\"previousTargetFps\":" + previousTargetFps.ToString(CultureInfo.InvariantCulture)
                + ",\"forcedVSync\":0,\"forcedTargetFps\":" + forcedTargetFps.ToString(CultureInfo.InvariantCulture)
                + ",\"maxAutoObjectCandidates\":" + GetMaxAutoObjectCandidates().ToString(CultureInfo.InvariantCulture);
            WriteJsonEvent("fps_analysis_phase", startPayload);
            WriteJsonEvent("benchmark_phase", startPayload);

            MazeBenchmarkStats baseline = default;
            MazeBenchmarkStats finalBaseline = default;
            var completedTests = 0;
            var totalTests = Math.Max(1, features.Count + 2 + Math.Min(MaxAdaptiveCombinationTests, features.Count));
            try
            {
                SetBenchmarkProgress("baseline", completedTests, totalTests);
                yield return WaitBenchmark(settings.baselineWarmupSeconds);
                yield return MeasureBenchmarkPhase("baseline", (MazeBenchmarkPlan)null, settings.baselineMeasureSeconds, stats => baseline = stats);
                var baselinePayload = BuildBenchmarkPhaseJson("baseline", null, baseline, null);
                WriteJsonEvent("fps_analysis_phase", baselinePayload);
                WriteJsonEvent("benchmark_phase", baselinePayload);
                completedTests++;

                for (var i = 0; i < features.Count; i++)
                {
                    if (stopBenchmarkRequested)
                    {
                        break;
                    }

                    var feature = features[i];
                    var variantStartPayload = $"\"phase\":\"variant_start\",\"featureId\":\"{Escape(feature.Id)}\",\"label\":\"{Escape(feature.Label)}\",\"stateBefore\":\"{Escape(feature.CurrentState)}\"";
                    WriteJsonEvent("fps_analysis_phase", variantStartPayload);
                    WriteJsonEvent("benchmark_phase", variantStartPayload);
                    SetBenchmarkProgress("single: " + feature.Label, completedTests, totalTests);
                    MazeBenchmarkStats stats = default;
                    yield return RunBenchmarkPlan(MazeBenchmarkPlan.Single(feature), originals, "variant", settings.variantMeasureSeconds, value => stats = value);

                    var result = MazeBenchmarkResult.From(feature, baseline, stats);
                    benchmarkResults.Add(result);
                    latestBenchmarkSummary = BuildRankedSummary();
                    var variantPayload = BuildBenchmarkPhaseJson("variant", feature, stats, result);
                    WriteJsonEvent("fps_analysis_phase", variantPayload);
                    WriteJsonEvent("benchmark_phase", variantPayload);
                    completedTests++;
                    SetBenchmarkProgress("single done: " + feature.Label, completedTests, totalTests);
                }

                var combinationPlans = BuildAdaptiveCombinationPlans();
                totalTests = Math.Max(1, features.Count + combinationPlans.Count + 2);
                for (var i = 0; i < combinationPlans.Count; i++)
                {
                    if (stopBenchmarkRequested)
                    {
                        break;
                    }

                    var plan = combinationPlans[i];
                    var comboStartPayload = BuildBenchmarkPlanStartJson("combo_start", plan);
                    WriteJsonEvent("fps_analysis_phase", comboStartPayload);
                    WriteJsonEvent("benchmark_phase", comboStartPayload);
                    SetBenchmarkProgress("combo: " + plan.Label, completedTests, totalTests);
                    MazeBenchmarkStats stats = default;
                    yield return RunBenchmarkPlan(plan, originals, "combo", settings.variantMeasureSeconds, value => stats = value);

                    var result = MazeBenchmarkResult.From(plan, baseline, stats);
                    benchmarkResults.Add(result);
                    latestBenchmarkSummary = BuildRankedSummary();
                    var comboPayload = BuildBenchmarkPlanJson("combo", plan, stats, result);
                    WriteJsonEvent("fps_analysis_phase", comboPayload);
                    WriteJsonEvent("benchmark_phase", comboPayload);
                    completedTests++;
                    SetBenchmarkProgress("combo done: " + plan.Label, completedTests, totalTests);
                }

                for (var i = 0; i < features.Count; i++)
                {
                    features[i].RestoreState(originals[features[i]]);
                }

                SetBenchmarkProgress("final baseline", completedTests, totalTests);
                yield return WaitBenchmark(settings.variantWarmupSeconds);
                yield return MeasureBenchmarkPhase("final_baseline", (MazeBenchmarkPlan)null, settings.baselineMeasureSeconds, stats => finalBaseline = stats);
                latestBenchmarkSummary = BuildRankedSummary();
                var finalBaselinePayload = BuildBenchmarkPhaseJson("final_baseline", null, finalBaseline, null);
                WriteJsonEvent("fps_analysis_phase", finalBaselinePayload);
                WriteJsonEvent("benchmark_phase", finalBaselinePayload);
                latestFpsAnalysisJson = BuildBenchmarkFinalJson(baseline, finalBaseline);
                WriteJsonEvent("fps_analysis_final", latestFpsAnalysisJson);
                WriteJsonEvent("benchmark_final", latestFpsAnalysisJson);
                completedTests++;
                SetBenchmarkProgress(stopBenchmarkRequested ? "FPS Analysis stopped" : "FPS Analysis done", completedTests, totalTests);
            }
            finally
            {
                for (var i = 0; i < features.Count; i++)
                {
                    features[i].RestoreState(originals[features[i]]);
                }

                SetCaptureActive(previousCapture, false);
                QualitySettings.vSyncCount = previousVSync;
                Application.targetFrameRate = previousTargetFps;
                benchmarkCoroutine = null;
                stopBenchmarkRequested = false;
                benchmarkProgress = Mathf.Clamp01(benchmarkProgress);
                if (benchmarkProgress < 0.999f)
                {
                    benchmarkStatus = "FPS Analysis stopped";
                }

                benchmarkProgressHideAt = Time.unscaledTime + 3f;
            }
        }

        private IEnumerator RunBenchmarkPlan(MazeBenchmarkPlan plan, Dictionary<MazeProfilingFeatureHandle, object> originals, string phase, float seconds, Action<MazeBenchmarkStats> onComplete)
        {
            plan.SetEnabled(false);
            yield return WaitBenchmark(settings.variantWarmupSeconds);
            MazeBenchmarkStats stats = default;
            yield return MeasureBenchmarkPhase(phase, plan, seconds, value => stats = value);
            plan.Restore(originals);
            yield return null;
            onComplete?.Invoke(stats);
        }

        private void SetBenchmarkProgress(string status, int completedTests, int totalTests)
        {
            benchmarkStatus = string.IsNullOrWhiteSpace(status) ? "FPS Analysis running" : status;
            benchmarkProgress = totalTests <= 0 ? 0f : Mathf.Clamp01((float)completedTests / totalTests);
        }

        private IEnumerator WaitBenchmark(float seconds)
        {
            var end = Time.unscaledTime + seconds;
            while (!stopBenchmarkRequested && Time.unscaledTime < end)
            {
                yield return null;
            }
        }

        private IEnumerator MeasureBenchmarkPhase(string phase, MazeProfilingFeatureHandle feature, float seconds, Action<MazeBenchmarkStats> onComplete)
        {
            return MeasureBenchmarkPhase(phase, feature != null ? MazeBenchmarkPlan.Single(feature) : null, seconds, onComplete);
        }

        private IEnumerator MeasureBenchmarkPhase(string phase, MazeBenchmarkPlan plan, float seconds, Action<MazeBenchmarkStats> onComplete)
        {
            var samples = new List<MazeBenchmarkFrame>(Mathf.CeilToInt(seconds * 240f));
            var end = Time.unscaledTime + seconds;
            var totalFrames = 0;
            var unfocusedFrames = 0;
            var focusRecoveryFrames = 0;
            var focusRecoveryUntil = 0f;
            while (!stopBenchmarkRequested && Time.unscaledTime < end)
            {
                totalFrames++;
                if (!IsPerfTargetFocused())
                {
                    unfocusedFrames++;
                    focusRecoveryUntil = Time.unscaledTime + 2f;
                    yield return null;
                    continue;
                }

                if (Time.unscaledTime < focusRecoveryUntil)
                {
                    focusRecoveryFrames++;
                    yield return null;
                    continue;
                }

                FrameTimingManager.CaptureFrameTimings();
                var timing = ReadFrameTiming();
                var frameMs = Time.unscaledDeltaTime * 1000.0;
                var cpuMs = timing.cpuFrameMs > 0.001 ? timing.cpuFrameMs : Math.Max(frameMs, ReadMetric("mainThreadMs"));
                var mainMs = timing.mainThreadMs > 0.001 ? timing.mainThreadMs : ReadMetric("mainThreadMs");
                var renderMs = timing.renderThreadMs > 0.001 ? timing.renderThreadMs : ReadMetric("renderThreadMs");
                var gpuMs = timing.gpuFrameMs > 0.001 ? timing.gpuFrameMs : ReadMetric("gpuFrameMs");
                samples.Add(new MazeBenchmarkFrame(frameMs, cpuMs, mainMs, renderMs, gpuMs, ReadMetric("gcAllocatedInFrameBytes"), ReadMetric("renderTextureBytes")));
                yield return null;
            }

            var stats = MazeBenchmarkStats.From(phase, plan, samples);
            stats.totalFrames = totalFrames;
            stats.focusRejectedFrames = unfocusedFrames;
            stats.focusRecoveryFrames = focusRecoveryFrames;
            stats.focusValid = unfocusedFrames == 0 && focusRecoveryFrames == 0 && samples.Count > 0;
            onComplete?.Invoke(stats);
        }

        private static bool IsPerfTargetFocused()
        {
            if (Application.isBatchMode)
            {
                return true;
            }

            if (Application.isFocused)
            {
                return true;
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var foreground = GetForegroundWindow();
            var active = GetActiveWindow();
            return foreground != IntPtr.Zero && active != IntPtr.Zero && foreground == active;
#else
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();
#endif

        private void RefreshFeatures()
        {
            features.Clear();
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IMazeProfilingFeatureProvider provider)
                {
                    provider.AddMazeProfilingFeatures(features);
                }
            }

            MazeAutoProfilingCandidates.AddSceneRootFeatures(features, GetMaxAutoObjectCandidates());
        }

        private int GetMaxAutoObjectCandidates()
        {
            if (FpsAnalysisMaxObjectCandidatesOverride >= 0)
            {
                return FpsAnalysisMaxObjectCandidatesOverride;
            }

            return Mathf.Max(0, GetCommandLineInt("-mazeFpsAnalysisMaxObjects", settings.maxAutoObjectCandidates));
        }

        private static bool ShouldAnalyzeFeature(MazeProfilingFeatureHandle feature, out string reason)
        {
            var state = feature?.CurrentState?.Trim() ?? string.Empty;
            var lower = state.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(state))
            {
                reason = string.Empty;
                return true;
            }

            if (lower == "off"
                || lower.StartsWith("off ", StringComparison.Ordinal)
                || lower.StartsWith("inactive", StringComparison.Ordinal)
                || lower.StartsWith("disabled", StringComparison.Ordinal))
            {
                reason = "inactive before analysis";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool HasCommandLineFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag))
            {
                return false;
            }

            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetCommandLineInt(string name, int fallback)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return fallback;
            }

            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            return fallback;
        }

        private void WriteStopEvent()
        {
            if (wroteStopEvent)
            {
                return;
            }

            wroteStopEvent = true;
            if (string.IsNullOrWhiteSpace(latest.verdict))
            {
                UpdateSnapshot(false);
            }

            WriteJsonEvent("final_sample", BuildSnapshotJson(latest, false));
            WriteJsonEvent("stop", $"\"shutdown\":true,{BuildSnapshotJson(latest, false)}", false);
            MazeDiagnosticsLog.Info("Profiler", "stop", "profiler stopped", MazeDiagnosticsLog.JoinData(
                MazeDiagnosticsLog.JsonNumber("fps", latest.fps),
                MazeDiagnosticsLog.JsonNumber("frameMs", latest.frameMs)));
        }

        private void HandleUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log)
            {
                return;
            }

            switch (type)
            {
                case LogType.Warning:
                    warningCount++;
                    break;
                case LogType.Error:
                case LogType.Assert:
                    errorCount++;
                    break;
                case LogType.Exception:
                    exceptionCount++;
                    break;
            }

            if (condition != null && condition.StartsWith("[MAZE PROFILE] Failed to write profile log", StringComparison.Ordinal))
            {
                return;
            }

            var diagnosticData = MazeDiagnosticsLog.JoinData(
                MazeDiagnosticsLog.JsonString("type", type.ToString()),
                MazeDiagnosticsLog.JsonString("stack", string.IsNullOrWhiteSpace(stackTrace) ? string.Empty : stackTrace));
            if (type == LogType.Warning)
            {
                MazeDiagnosticsLog.WarnThrottled("unity_log:" + condition, 1f, "Unity", "log", condition, diagnosticData);
            }
            else
            {
                MazeDiagnosticsLog.ErrorThrottled("unity_log:" + condition, 1f, "Unity", "log", condition, diagnosticData);
            }

            if (string.IsNullOrWhiteSpace(logPath))
            {
                return;
            }

            if (loggedUnityLogEvents >= MaxUnityLogEventsPerRun)
            {
                droppedUnityLogEvents++;
                if (droppedUnityLogEvents == 1)
                {
                    WriteJsonEvent("unity_log_dropped", "\"reason\":\"max_unity_log_events_reached\"");
                }

                return;
            }

            loggedUnityLogEvents++;
            WriteJsonEvent("unity_log", BuildUnityLogJson(condition, stackTrace, type));
        }

        private void WriteJsonEvent(string eventName, string jsonPayload)
        {
            WriteJsonEvent(eventName, jsonPayload, true);
        }

        private void WriteJsonEvent(string eventName, string jsonPayload, bool updateLatest)
        {
            if (string.IsNullOrWhiteSpace(logPath))
            {
                return;
            }

            try
            {
                var timestamp = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                var payload = NormalizeJsonPayload(jsonPayload);
                var line = $"{{\"event\":\"{Escape(eventName)}\",\"utc\":\"{timestamp}\"{payload}}}";
                File.AppendAllText(logPath, line + Environment.NewLine);
                if (updateLatest)
                {
                    File.WriteAllText(latestPath, $"{{\"utc\":\"{timestamp}\",\"event\":\"{Escape(eventName)}\",\"logPath\":\"{Escape(logPath)}\",\"latest\":{line}}}");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MAZE PROFILE] Failed to write profile log. {exception.Message}");
            }
        }

        private static string NormalizeJsonPayload(string jsonPayload)
        {
            if (string.IsNullOrWhiteSpace(jsonPayload))
            {
                return string.Empty;
            }

            var trimmed = jsonPayload.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '{' && trimmed[^1] == '}')
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2);
            }

            return string.IsNullOrWhiteSpace(trimmed) ? string.Empty : "," + trimmed;
        }

        private string BuildStartJson()
        {
            builder.Clear();
            builder.Append($"\"unity\":\"{Escape(Application.unityVersion)}\",\"platform\":\"{Escape(Application.platform.ToString())}\",\"developmentBuild\":{Bool(Debug.isDebugBuild)},\"captureActive\":{Bool(captureActive)}");
            builder.Append($",\"device\":\"{Escape(SystemInfo.graphicsDeviceName)}\",\"graphicsApi\":\"{Escape(SystemInfo.graphicsDeviceType.ToString())}\",\"gpuRecorderSupported\":{Bool(SystemInfo.supportsGpuRecorder)}");
            builder.Append($",\"logPath\":\"{Escape(logPath)}\",\"latestPath\":\"{Escape(latestPath)}\"");
            builder.Append(",\"missingCounters\":[");
            for (var i = 0; i < missingCounters.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append('"').Append(Escape(missingCounters[i])).Append('"');
            }

            builder.Append(']');
            builder.Append(",\"resolvedCounters\":[");
            for (var i = 0; i < resolvedCounters.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append('"').Append(Escape(resolvedCounters[i])).Append('"');
            }

            builder.Append(']');
            if (missingCounters.Count > 0 && counterCandidates.Count > 0)
            {
                builder.Append(",\"counterCandidates\":[");
                for (var i = 0; i < counterCandidates.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    builder.Append('"').Append(Escape(counterCandidates[i])).Append('"');
                }

                builder.Append(']');
            }

            return builder.ToString();
        }

        private string BuildSnapshotJson(MazeProfilerSnapshot snapshot, bool includeScopes)
        {
            builder.Clear();
            builder.Append(FormattableString.Invariant($"\"frame\":{snapshot.frameIndex},\"scene\":\"{Escape(snapshot.sceneName)}\",\"scenePath\":\"{Escape(snapshot.scenePath)}\",\"fps\":{snapshot.fps:0.###},\"frameMs\":{snapshot.frameMs:0.###},\"cpuMs\":{snapshot.cpuFrameMs:0.###},\"mainMs\":{snapshot.mainThreadMs:0.###},\"renderMs\":{snapshot.renderThreadMs:0.###},\"gpuMs\":{snapshot.gpuFrameMs:0.###}"));
            builder.Append(FormattableString.Invariant($",\"drawCalls\":{snapshot.drawCalls:0},\"batches\":"));
            AppendNullableNumber(builder, snapshot.batches, snapshot.batchesAvailable, "0");
            builder.Append(FormattableString.Invariant($",\"setPass\":{snapshot.setPassCalls:0},\"triangles\":{snapshot.triangles:0},\"vertices\":{snapshot.vertices:0},\"renderTextures\":{snapshot.renderTextures:0},\"renderTextureBytes\":{snapshot.renderTextureBytes:0}"));
            builder.Append(FormattableString.Invariant($",\"memory\":{{\"allocated\":{snapshot.totalAllocatedMemoryBytes:0},\"reserved\":{snapshot.totalReservedMemoryBytes:0},\"used\":{snapshot.totalUsedMemoryBytes:0},\"gcUsed\":{snapshot.gcUsedMemoryBytes:0},\"gcFrame\":"));
            AppendNullableNumber(builder, snapshot.gcAllocatedInFrameBytes, snapshot.gcAllocatedInFrameAvailable, "0");
            builder.Append("}");
            builder.Append(FormattableString.Invariant($",\"metricsAvailable\":{{\"batches\":{Bool(snapshot.batchesAvailable)},\"gcFrame\":{Bool(snapshot.gcAllocatedInFrameAvailable)}}}"));
            builder.Append(FormattableString.Invariant($",\"unityLogs\":{{\"warnings\":{warningCount},\"errors\":{errorCount},\"exceptions\":{exceptionCount},\"logged\":{loggedUnityLogEvents},\"dropped\":{droppedUnityLogEvents}}}"));
            builder.Append($",\"quality\":\"{Escape(snapshot.qualityLevel)}\",\"vSync\":{snapshot.vSyncCount},\"targetFps\":{snapshot.targetFrameRate},\"resolution\":\"{Escape(snapshot.resolution)}\",\"renderScale\":{snapshot.renderScale.ToString("0.###", CultureInfo.InvariantCulture)},\"pipeline\":\"{Escape(snapshot.pipeline)}\",\"graphicsApi\":\"{Escape(snapshot.graphicsApi)}\",\"verdict\":\"{Escape(snapshot.verdict)}\",\"systems\":\"{Escape(snapshot.systemSummary)}\"");

            if (includeScopes)
            {
                builder.Append(",\"scopes\":[");
                for (var i = 0; i < snapshot.topScopes.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    var scope = snapshot.topScopes[i];
                    builder.Append(FormattableString.Invariant($"{{\"name\":\"{Escape(scope.name)}\",\"avgMs\":{scope.averageMs:0.###},\"peakMs\":{scope.peakMs:0.###},\"calls\":{scope.calls}}}"));
                }

                builder.Append(']');
            }

            return builder.ToString();
        }

        private string BuildUnityLogJson(string condition, string stackTrace, LogType type)
        {
            var scene = SceneManager.GetActiveScene();
            builder.Clear();
            builder.Append($"\"frame\":{Time.frameCount},\"scene\":\"{Escape(scene.name)}\",\"type\":\"{Escape(type.ToString())}\",\"condition\":\"{Escape(TrimForLog(condition, MaxUnityLogConditionChars))}\"");
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                builder.Append($",\"stack\":\"{Escape(TrimForLog(stackTrace, MaxUnityLogStackChars))}\"");
            }

            return builder.ToString();
        }

        private string BuildBenchmarkPhaseJson(string phase, MazeProfilingFeatureHandle feature, MazeBenchmarkStats stats, MazeBenchmarkResult result)
        {
            builder.Clear();
            builder.Append($"\"phase\":\"{Escape(phase)}\"");
            if (feature != null)
            {
                builder.Append($",\"featureId\":\"{Escape(feature.Id)}\",\"label\":\"{Escape(feature.Label)}\",\"category\":\"{Escape(feature.Category)}\"");
            }

            builder.Append(',').Append(stats.ToJsonFields());
            if (result != null)
            {
                builder.Append(",\"delta\":{").Append(result.ToDeltaJsonFields()).Append('}');
            }

            return builder.ToString();
        }

        private string BuildBenchmarkPlanStartJson(string phase, MazeBenchmarkPlan plan)
        {
            builder.Clear();
            builder.Append($"\"phase\":\"{Escape(phase)}\"");
            AppendPlanFields(builder, plan);
            return builder.ToString();
        }

        private string BuildBenchmarkPlanJson(string phase, MazeBenchmarkPlan plan, MazeBenchmarkStats stats, MazeBenchmarkResult result)
        {
            builder.Clear();
            builder.Append($"\"phase\":\"{Escape(phase)}\"");
            AppendPlanFields(builder, plan);
            builder.Append(',').Append(stats.ToJsonFields());
            if (result != null)
            {
                builder.Append(",\"delta\":{").Append(result.ToDeltaJsonFields()).Append('}');
            }

            return builder.ToString();
        }

        private void AppendPlanFields(StringBuilder target, MazeBenchmarkPlan plan)
        {
            target.Append($",\"featureId\":\"{Escape(plan.Id)}\",\"label\":\"{Escape(plan.Label)}\",\"category\":\"{Escape(plan.Category)}\",\"featureCount\":{plan.Features.Count.ToString(CultureInfo.InvariantCulture)}");
            target.Append(",\"featureIds\":[");
            for (var i = 0; i < plan.Features.Count; i++)
            {
                if (i > 0)
                {
                    target.Append(',');
                }

                target.Append('"').Append(Escape(plan.Features[i].Id)).Append('"');
            }

            target.Append(']');
        }

        private string BuildBenchmarkFinalJson(MazeBenchmarkStats baseline, MazeBenchmarkStats finalBaseline)
        {
            builder.Clear();
            builder.Append("\"summary\":\"").Append(Escape(latestBenchmarkSummary)).Append("\"");
            builder.Append(",\"capStatus\":\"").Append(Escape(BuildCapStatus(baseline))).Append("\"");
            builder.Append(FormattableString.Invariant($",\"analysisSettings\":{{\"vSync\":{QualitySettings.vSyncCount},\"targetFps\":{Application.targetFrameRate},\"refreshRate\":{Screen.currentResolution.refreshRateRatio.value:0.###},\"resolution\":\"{Escape(Screen.width + "x" + Screen.height)}\",\"maxAutoObjectCandidates\":{GetMaxAutoObjectCandidates()}}}"));
            builder.Append(",\"baseline\":{").Append(baseline.ToJsonFields()).Append('}');
            builder.Append(",\"finalBaseline\":{").Append(finalBaseline.ToJsonFields()).Append('}');
            builder.Append(",\"ranked\":[");
            var ranked = RankedResults().ToList();
            for (var i = 0; i < ranked.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(ranked[i].ToJson());
            }

            builder.Append(']');
            builder.Append(",\"skipped\":[");
            for (var i = 0; i < skippedFpsAnalysisFeatures.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(skippedFpsAnalysisFeatures[i].ToJson());
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string BuildCapStatus(MazeBenchmarkStats baseline)
        {
            if (QualitySettings.vSyncCount > 0)
            {
                return $"invalid: vSync is still {QualitySettings.vSyncCount}";
            }

            if (Application.targetFrameRate > 0 && baseline.averageFps > Application.targetFrameRate - 2.0)
            {
                return $"likely capped by targetFrameRate {Application.targetFrameRate}";
            }

            var refresh = Screen.currentResolution.refreshRateRatio.value;
            if (refresh > 1.0
                && baseline.averageFps > refresh - 2.0
                && baseline.averageFps < refresh + 2.0
                && baseline.averageFrameMs > baseline.averageGpuMs + 5.0
                && baseline.averageFrameMs > baseline.averageCpuMs + 5.0)
            {
                return $"likely externally capped near display refresh {refresh:0.##} Hz";
            }

            return "uncapped";
        }

        private IEnumerable<MazeBenchmarkResult> RankedResults()
        {
            var singles = benchmarkResults
                .Where(result => !result.isCombination)
                .OrderByDescending(result => result.gpuMsSaved)
                .ThenByDescending(result => result.cpuMsSaved)
                .ThenByDescending(result => result.fpsGained);
            var combinations = benchmarkResults
                .Where(result => result.isCombination)
                .OrderByDescending(result => result.gpuMsSaved)
                .ThenByDescending(result => result.cpuMsSaved)
                .ThenByDescending(result => result.fpsGained);
            return singles.Concat(combinations).Take(settings.maxRankedSuspects);
        }

        private List<MazeBenchmarkPlan> BuildAdaptiveCombinationPlans()
        {
            var plans = new List<MazeBenchmarkPlan>();
            if (features.Count < 2 || benchmarkResults.Count == 0)
            {
                return plans;
            }

            var added = new HashSet<string>(StringComparer.Ordinal);
            var topFeatureIds = benchmarkResults
                .Where(result => !result.isCombination && result.gpuMsSaved > 0.25)
                .OrderByDescending(result => result.gpuMsSaved)
                .Select(result => result.featureId)
                .Take(2)
                .ToList();

            foreach (var featureId in topFeatureIds)
            {
                var primary = features.FirstOrDefault(feature => string.Equals(feature.Id, featureId, StringComparison.Ordinal));
                if (primary == null)
                {
                    continue;
                }

                foreach (var other in features)
                {
                    if (other == primary || !string.Equals(other.Category, primary.Category, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    TryAddCombination(plans, added, new[] { primary, other });
                    if (plans.Count >= MaxAdaptiveCombinationTests)
                    {
                        return plans;
                    }
                }
            }

            foreach (var categoryGroup in features.GroupBy(feature => feature.Category ?? string.Empty))
            {
                var categoryFeatures = categoryGroup.ToList();
                if (categoryFeatures.Count <= 1 || categoryFeatures.Count > 10)
                {
                    continue;
                }

                TryAddCombination(plans, added, categoryFeatures);
                if (plans.Count >= MaxAdaptiveCombinationTests)
                {
                    break;
                }
            }

            return plans;
        }

        private static void TryAddCombination(List<MazeBenchmarkPlan> plans, HashSet<string> added, IEnumerable<MazeProfilingFeatureHandle> featureSet)
        {
            var plan = MazeBenchmarkPlan.Combination(featureSet);
            if (plan.Features.Count < 2 || !added.Add(plan.Id))
            {
                return;
            }

            plans.Add(plan);
        }

        private string BuildRankedSummary()
        {
            if (benchmarkResults.Count == 0)
            {
                return "FPS Analysis has no variant results yet";
            }

            builder.Clear();
            var index = 1;
            foreach (var result in RankedResults())
            {
                if (index > 1)
                {
                    builder.AppendLine();
                }

                builder.Append(FormattableString.Invariant($"{index}. {result.label}: GPU {result.gpuMsSaved:+0.00;-0.00;0.00} ms, CPU {result.cpuMsSaved:+0.00;-0.00;0.00} ms, FPS {result.fpsGained:+0.0;-0.0;0.0}"));
                index++;
            }

            return builder.ToString();
        }

        private static string BuildSystemSummary()
        {
            var featureHandles = new List<MazeProfilingFeatureHandle>(16);
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IMazeProfilingFeatureProvider provider)
                {
                    provider.AddMazeProfilingFeatures(featureHandles);
                }
            }

            if (featureHandles.Count == 0)
            {
                return "No profiling feature providers active.";
            }

            return string.Join("; ", featureHandles.Select(feature => $"{feature.Label} {feature.CurrentState}"));
        }

        private string BuildShortConsolePayload(MazeProfilerSnapshot snapshot)
        {
            return $"fps={snapshot.fps:0.0} cpu={snapshot.cpuFrameMs:0.0}ms main={snapshot.mainThreadMs:0.0}ms gpu={snapshot.gpuFrameMs:0.0}ms draw={snapshot.drawCalls:0} gc={FormatMetricBytes(snapshot.gcAllocatedInFrameBytes, snapshot.gcAllocatedInFrameAvailable)}";
        }

        private static void AppendNullableNumber(StringBuilder target, double value, bool available, string format)
        {
            if (!available)
            {
                target.Append("null");
                return;
            }

            target.Append(value.ToString(format, CultureInfo.InvariantCulture));
        }

        private static string FormatMetricBytes(double bytes, bool available)
        {
            return available ? FormatBytes(bytes) : "n/a";
        }

        private static string FormatMetricCount(double value, bool available)
        {
            return available ? FormatCount(value) : "n/a";
        }

        private static string FormatBytes(double bytes)
        {
            if (bytes <= 0.0)
            {
                return "0 B";
            }

            if (bytes >= 1024.0 * 1024.0 * 1024.0)
            {
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):0.00} GB";
            }

            if (bytes >= 1024.0 * 1024.0)
            {
                return $"{bytes / (1024.0 * 1024.0):0.00} MB";
            }

            if (bytes >= 1024.0)
            {
                return $"{bytes / 1024.0:0.0} KB";
            }

            return $"{bytes:0} B";
        }

        private static string FormatCount(double value)
        {
            if (value >= 1000000.0)
            {
                return $"{value / 1000000.0:0.00}M";
            }

            if (value >= 1000.0)
            {
                return $"{value / 1000.0:0.0}K";
            }

            return value.ToString("0", CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string TrimForLog(string value, int maxChars)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, Math.Max(0, maxChars)) + "...";
        }

        private static string Bool(bool value) => value ? "true" : "false";

        private enum MetricKind
        {
            TimeMilliseconds,
            Count,
            Bytes
        }

        private readonly struct CounterRequest
        {
            public readonly string key;
            public readonly MetricKind kind;
            public readonly bool aggregateAllMatches;
            public readonly string[] names;

            public CounterRequest(string key, MetricKind kind, params string[] names)
                : this(key, kind, false, names)
            {
            }

            public CounterRequest(string key, MetricKind kind, bool aggregateAllMatches, params string[] names)
            {
                this.key = key;
                this.kind = kind;
                this.aggregateAllMatches = aggregateAllMatches;
                this.names = names;
            }
        }

        private sealed class MetricRecorder : IDisposable
        {
            public readonly string key;
            public readonly string counterName;
            public readonly MetricKind kind;
            public ProfilerRecorder recorder;

            public MetricRecorder(string key, string counterName, MetricKind kind, ProfilerRecorder recorder)
            {
                this.key = key;
                this.counterName = counterName;
                this.kind = kind;
                this.recorder = recorder;
            }

            public void Dispose()
            {
                if (recorder.Valid)
                {
                    recorder.Dispose();
                }
            }
        }

        private sealed class ScopeStat
        {
            public readonly string name;
            public double windowTotalMs;
            public double windowPeakMs;
            public int windowCalls;
            public double lifetimePeakMs;
            public double lastAverageMs;
            public double lastPeakMs;
            public int lastCalls;

            public ScopeStat(string name)
            {
                this.name = name;
            }
        }

        private readonly struct FrameTimingValues
        {
            public readonly double cpuFrameMs;
            public readonly double mainThreadMs;
            public readonly double renderThreadMs;
            public readonly double gpuFrameMs;

            public FrameTimingValues(double cpuFrameMs, double mainThreadMs, double renderThreadMs, double gpuFrameMs)
            {
                this.cpuFrameMs = cpuFrameMs;
                this.mainThreadMs = mainThreadMs;
                this.renderThreadMs = renderThreadMs;
                this.gpuFrameMs = gpuFrameMs;
            }
        }
    }

    [Serializable]
    public sealed class MazeProfilerSnapshot
    {
        public int frameIndex;
        public string utc;
        public string sceneName;
        public string scenePath;
        public double fps;
        public double frameMs;
        public double cpuFrameMs;
        public double mainThreadMs;
        public double renderThreadMs;
        public double gpuFrameMs;
        public double drawCalls;
        public double setPassCalls;
        public double batches;
        public bool batchesAvailable;
        public double triangles;
        public double vertices;
        public double renderTextures;
        public double renderTextureBytes;
        public double gcAllocatedInFrameBytes;
        public bool gcAllocatedInFrameAvailable;
        public double gcUsedMemoryBytes;
        public double totalUsedMemoryBytes;
        public double totalAllocatedMemoryBytes;
        public double totalReservedMemoryBytes;
        public string qualityLevel;
        public int vSyncCount;
        public int targetFrameRate;
        public string resolution;
        public float renderScale;
        public string pipeline;
        public string graphicsApi;
        public int unityWarnings;
        public int unityErrors;
        public int unityExceptions;
        public int unityLogsLogged;
        public int unityLogsDropped;
        public string verdict;
        public string systemSummary;
        public List<MazeProfilerScopeSample> topScopes = new();
    }

    [Serializable]
    public struct MazeProfilerScopeSample
    {
        public string name;
        public double averageMs;
        public double peakMs;
        public int calls;
    }

    internal sealed class MazeBenchmarkPlan
    {
        private MazeBenchmarkPlan(string id, string label, string category, List<MazeProfilingFeatureHandle> features)
        {
            Id = id;
            Label = label;
            Category = category;
            Features = features;
        }

        public string Id { get; }
        public string Label { get; }
        public string Category { get; }
        public List<MazeProfilingFeatureHandle> Features { get; }

        public static MazeBenchmarkPlan Single(MazeProfilingFeatureHandle feature)
        {
            return new MazeBenchmarkPlan(feature.Id, feature.Label, feature.Category, new List<MazeProfilingFeatureHandle> { feature });
        }

        public static MazeBenchmarkPlan Combination(IEnumerable<MazeProfilingFeatureHandle> features)
        {
            var list = features
                .Where(feature => feature != null)
                .OrderBy(feature => feature.Id, StringComparer.Ordinal)
                .ToList();

            var id = "combo." + string.Join("+", list.Select(feature => feature.Id));
            var label = string.Join(" + ", list.Select(feature => feature.Label));
            var category = list.Select(feature => feature.Category).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1
                ? list[0].Category
                : "Combination";
            return new MazeBenchmarkPlan(id, label, category, list);
        }

        public void SetEnabled(bool enabled)
        {
            for (var i = 0; i < Features.Count; i++)
            {
                Features[i].SetEnabled(enabled);
            }
        }

        public void Restore(Dictionary<MazeProfilingFeatureHandle, object> originals)
        {
            for (var i = 0; i < Features.Count; i++)
            {
                var feature = Features[i];
                if (originals.TryGetValue(feature, out var state))
                {
                    feature.RestoreState(state);
                }
            }
        }
    }

    public sealed class MazeBenchmarkResult
    {
        public string featureId;
        public string label;
        public string category;
        public double fpsGained;
        public double frameMsSaved;
        public double cpuMsSaved;
        public double mainMsSaved;
        public double renderMsSaved;
        public double gpuMsSaved;
        public double gcBytesSaved;
        public double rtBytesSaved;
        public bool isCombination;
        public bool variantFocusValid;
        public int variantFocusRejectedFrames;
        public int variantFocusRecoveryFrames;

        public static MazeBenchmarkResult From(MazeProfilingFeatureHandle feature, MazeBenchmarkStats baseline, MazeBenchmarkStats variant)
        {
            return From(MazeBenchmarkPlan.Single(feature), baseline, variant);
        }

        internal static MazeBenchmarkResult From(MazeBenchmarkPlan plan, MazeBenchmarkStats baseline, MazeBenchmarkStats variant)
        {
            return new MazeBenchmarkResult
            {
                featureId = plan.Id,
                label = plan.Label,
                category = plan.Category,
                isCombination = plan.Features.Count > 1,
                fpsGained = variant.averageFps - baseline.averageFps,
                frameMsSaved = baseline.averageFrameMs - variant.averageFrameMs,
                cpuMsSaved = baseline.averageCpuMs - variant.averageCpuMs,
                mainMsSaved = baseline.averageMainMs - variant.averageMainMs,
                renderMsSaved = baseline.averageRenderMs - variant.averageRenderMs,
                gpuMsSaved = baseline.averageGpuMs - variant.averageGpuMs,
                gcBytesSaved = baseline.averageGcBytes - variant.averageGcBytes,
                rtBytesSaved = baseline.averageRenderTextureBytes - variant.averageRenderTextureBytes,
                variantFocusValid = variant.focusValid,
                variantFocusRejectedFrames = variant.focusRejectedFrames,
                variantFocusRecoveryFrames = variant.focusRecoveryFrames
            };
        }

        public string ToDeltaJsonFields()
        {
            return FormattableString.Invariant($"\"fpsGained\":{fpsGained:0.###},\"frameMsSaved\":{frameMsSaved:0.###},\"cpuMsSaved\":{cpuMsSaved:0.###},\"mainMsSaved\":{mainMsSaved:0.###},\"renderMsSaved\":{renderMsSaved:0.###},\"gpuMsSaved\":{gpuMsSaved:0.###},\"gcBytesSaved\":{gcBytesSaved:0},\"renderTextureBytesSaved\":{rtBytesSaved:0},\"variantFocusValid\":{(variantFocusValid ? "true" : "false")},\"variantFocusRejectedFrames\":{variantFocusRejectedFrames},\"variantFocusRecoveryFrames\":{variantFocusRecoveryFrames}");
        }

        public string ToJson()
        {
            return $"{{\"featureId\":\"{Escape(featureId)}\",\"label\":\"{Escape(label)}\",\"category\":\"{Escape(category)}\",\"combination\":{(isCombination ? "true" : "false")},{ToDeltaJsonFields()}}}";
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }

    public readonly struct MazeSkippedProfilingFeature
    {
        public readonly string featureId;
        public readonly string label;
        public readonly string category;
        public readonly string state;
        public readonly string reason;

        public MazeSkippedProfilingFeature(string featureId, string label, string category, string state, string reason)
        {
            this.featureId = featureId ?? string.Empty;
            this.label = label ?? string.Empty;
            this.category = category ?? string.Empty;
            this.state = state ?? string.Empty;
            this.reason = reason ?? string.Empty;
        }

        public string ToJson()
        {
            return $"{{\"featureId\":\"{Escape(featureId)}\",\"label\":\"{Escape(label)}\",\"category\":\"{Escape(category)}\",\"state\":\"{Escape(state)}\",\"reason\":\"{Escape(reason)}\"}}";
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }

    public struct MazeBenchmarkStats
    {
        public string phase;
        public string featureId;
        public string label;
        public int frameCount;
        public double averageFps;
        public double medianFps;
        public double p95FrameMs;
        public double averageFrameMs;
        public double medianFrameMs;
        public double averageCpuMs;
        public double medianCpuMs;
        public double p95CpuMs;
        public double averageMainMs;
        public double averageRenderMs;
        public double averageGpuMs;
        public double medianGpuMs;
        public double p95GpuMs;
        public double averageGcBytes;
        public double averageRenderTextureBytes;
        public int totalFrames;
        public int focusRejectedFrames;
        public int focusRecoveryFrames;
        public bool focusValid;

        public static MazeBenchmarkStats From(string phase, MazeProfilingFeatureHandle feature, List<MazeBenchmarkFrame> samples)
        {
            return From(phase, feature != null ? MazeBenchmarkPlan.Single(feature) : null, samples);
        }

        internal static MazeBenchmarkStats From(string phase, MazeBenchmarkPlan plan, List<MazeBenchmarkFrame> samples)
        {
            if (samples.Count == 0)
            {
                return new MazeBenchmarkStats { phase = phase, featureId = plan?.Id, label = plan?.Label };
            }

            var frameMs = samples.Select(sample => sample.frameMs).ToList();
            var cpuMs = samples.Select(sample => sample.cpuMs).ToList();
            var gpuMs = samples.Select(sample => sample.gpuMs).ToList();
            var fps = frameMs.Select(value => 1000.0 / Math.Max(value, 0.0001)).ToList();

            return new MazeBenchmarkStats
            {
                phase = phase,
                featureId = plan?.Id,
                label = plan?.Label,
                frameCount = samples.Count,
                averageFps = fps.Average(),
                medianFps = Percentile(fps, 0.5),
                p95FrameMs = Percentile(frameMs, 0.95),
                averageFrameMs = frameMs.Average(),
                medianFrameMs = Percentile(frameMs, 0.5),
                averageCpuMs = cpuMs.Average(),
                medianCpuMs = Percentile(cpuMs, 0.5),
                p95CpuMs = Percentile(cpuMs, 0.95),
                averageMainMs = samples.Average(sample => sample.mainMs),
                averageRenderMs = samples.Average(sample => sample.renderMs),
                averageGpuMs = gpuMs.Average(),
                medianGpuMs = Percentile(gpuMs, 0.5),
                p95GpuMs = Percentile(gpuMs, 0.95),
                averageGcBytes = samples.Average(sample => sample.gcBytes),
                averageRenderTextureBytes = samples.Average(sample => sample.renderTextureBytes),
                totalFrames = samples.Count,
                focusValid = true
            };
        }

        public string ToJsonFields()
        {
            return FormattableString.Invariant($"\"stats\":{{\"frames\":{frameCount},\"avgFps\":{averageFps:0.###},\"medianFps\":{medianFps:0.###},\"avgFrameMs\":{averageFrameMs:0.###},\"medianFrameMs\":{medianFrameMs:0.###},\"p95FrameMs\":{p95FrameMs:0.###},\"avgCpuMs\":{averageCpuMs:0.###},\"medianCpuMs\":{medianCpuMs:0.###},\"p95CpuMs\":{p95CpuMs:0.###},\"avgMainMs\":{averageMainMs:0.###},\"avgRenderMs\":{averageRenderMs:0.###},\"avgGpuMs\":{averageGpuMs:0.###},\"medianGpuMs\":{medianGpuMs:0.###},\"p95GpuMs\":{p95GpuMs:0.###},\"avgGcBytes\":{averageGcBytes:0},\"avgRenderTextureBytes\":{averageRenderTextureBytes:0},\"focusValid\":{(focusValid ? "true" : "false")},\"focusTotalFrames\":{totalFrames},\"focusRejectedFrames\":{focusRejectedFrames},\"focusRecoveryFrames\":{focusRecoveryFrames}}}");
        }

        private static double Percentile(List<double> values, double percentile)
        {
            if (values.Count == 0)
            {
                return 0.0;
            }

            values.Sort();
            var index = (int)Math.Round((values.Count - 1) * percentile);
            index = Math.Max(0, Math.Min(values.Count - 1, index));
            return values[index];
        }
    }

    public readonly struct MazeBenchmarkFrame
    {
        public readonly double frameMs;
        public readonly double cpuMs;
        public readonly double mainMs;
        public readonly double renderMs;
        public readonly double gpuMs;
        public readonly double gcBytes;
        public readonly double renderTextureBytes;

        public MazeBenchmarkFrame(double frameMs, double cpuMs, double mainMs, double renderMs, double gpuMs, double gcBytes, double renderTextureBytes)
        {
            this.frameMs = frameMs;
            this.cpuMs = cpuMs;
            this.mainMs = mainMs;
            this.renderMs = renderMs;
            this.gpuMs = gpuMs;
            this.gcBytes = gcBytes;
            this.renderTextureBytes = renderTextureBytes;
        }
    }
}
