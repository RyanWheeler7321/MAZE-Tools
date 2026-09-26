using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maze
{
    // Shared screen-transition host, projects provide the profile assets.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4750)]
    public sealed class MazeTransitionHost : MonoBehaviour
    {
        private const string DiagnosticsCategory = "Interface.Transition";
        private const int DefaultMaxAdditiveTransitions = 8;

        private static MazeTransitionHost instance;

        private readonly MazeSquareTransitionPattern pattern = new();
        private readonly MazeVectorTransitionPattern vectorPattern = new();
        private readonly MazeSquareTransitionHookLinePattern hookLinePattern = new();
        private readonly MazeTransitionOverlayRenderer overlayRenderer = new();
        private readonly MazeTransitionMeshRenderer meshRenderer = new();
        private readonly MazeTransitionGpuFullscreenRenderer gpuRenderer = new();
        private readonly MazeVectorTransitionOverlayRenderer vectorOverlayRenderer = new();
        // Additive playbacks are separate overlays. The main handle/state stays on the cover transition.
        private readonly List<TransitionPlayback> additivePlaybacks = new(DefaultMaxAdditiveTransitions);

        private MazeTransitionProfile activeProfile;
        private MazeSquareTransitionProfile activeSquareProfile;
        private MazeSquareTransitionProfile activeOutSquareProfile;
        private MazeVectorTransitionProfile activeVectorProfile;
        private MazeVectorTransitionProfile activeOutVectorProfile;
        private MazeTransitionContext activeContext;
        private MazeTransitionHandle activeHandle;
        private IMazeTransitionRenderer activeRenderer;
        private bool usingVectorRenderer;
        private MazeTransitionState state = MazeTransitionState.Hidden;
        private float elapsed;
        private bool coveredSent;
        private IDisposable inputBlockClaim;
        private int lastScreenWidth;
        private int lastScreenHeight;
        private float activeInCoveredSeconds;
        private float activeInCompleteSeconds;
        private float activeOutCompleteSeconds;
        private int transitionSerial;
        private long operationSerial;
        private long mainRequestSerial;
        private long activeOperationId;
        private bool activeCoveredGate;
        private bool activeGateReleaseRequested;

        public static MazeTransitionHost Instance => instance;
        public MazeTransitionState State => state;
        public bool IsVisible => state != MazeTransitionState.Hidden;
        public bool IsCovered => state == MazeTransitionState.Covered || (coveredSent && state == MazeTransitionState.PlayingIn);
        public int ActiveTileCount => pattern.TileCount;
        public string ActiveBackend => usingVectorRenderer ? "VectorOverlay" : RendererBackendLabel(activeRenderer);
        public int ActiveVectorElementCount => vectorPattern.ElementCount;
        public int ActiveAdditiveTransitionCount => additivePlaybacks.Count;

        public Func<string, IDisposable> AcquireInputBlock { get; set; }

        public static MazeTransitionHost Ensure(Transform parent = null)
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindAnyObjectByType<MazeTransitionHost>();
            if (instance != null)
            {
                return instance;
            }

            var created = new GameObject("MAZE Transitions");
            if (parent != null)
            {
                created.transform.SetParent(parent, false);
            }

            return created.AddComponent<MazeTransitionHost>();
        }

        // Stays covered until cleared or played out.
        public MazeTransitionHandle PlayIn(MazeSquareTransitionProfile profile, MazeTransitionContext context = default)
        {
            return Begin(profile, context, MazeTransitionDirection.In);
        }

        public MazeTransitionHandle PlayIn(MazeVectorTransitionProfile profile, MazeTransitionContext context = default)
        {
            return Begin(profile, context, MazeTransitionDirection.In);
        }

        public MazeTransitionHandle PlayOut(MazeSquareTransitionProfile profile, MazeTransitionContext context = default)
        {
            return Begin(profile, context, MazeTransitionDirection.Out);
        }

        public MazeTransitionHandle PlayOut(MazeVectorTransitionProfile profile, MazeTransitionContext context = default)
        {
            return Begin(profile, context, MazeTransitionDirection.Out);
        }

        public MazeTransitionHandle PlayFlicker(MazeSquareTransitionProfile profile, MazeTransitionContext context = default)
        {
            context.Flicker = true;
            context.ClearOnCovered = true;
            context.Additive = true;
            return PlayAdditiveIn(profile, context);
        }

        public MazeTransitionHandle PlayFlicker(MazeVectorTransitionProfile profile, MazeTransitionContext context = default)
        {
            context.Flicker = true;
            context.ClearOnCovered = true;
            context.Additive = true;
            return PlayAdditiveIn(profile, context);
        }

        public MazeTransitionHandle PlayAdditiveIn(MazeSquareTransitionProfile profile, MazeTransitionContext context = default)
        {
            context.Additive = true;
            return BeginAdditive(profile, context, MazeTransitionDirection.In);
        }

        public MazeTransitionHandle PlayAdditiveIn(MazeVectorTransitionProfile profile, MazeTransitionContext context = default)
        {
            context.Additive = true;
            return BeginAdditive(profile, context, MazeTransitionDirection.In);
        }

        public MazeTransitionHandle PlayAdditiveOut(MazeSquareTransitionProfile profile, MazeTransitionContext context = default)
        {
            context.Additive = true;
            return BeginAdditive(profile, context, MazeTransitionDirection.Out);
        }

        public MazeTransitionHandle PlayAdditiveOut(MazeVectorTransitionProfile profile, MazeTransitionContext context = default)
        {
            context.Additive = true;
            return BeginAdditive(profile, context, MazeTransitionDirection.Out);
        }

        public MazeTransitionHandle PlayAdditiveSwap(MazeSquareTransitionProfile profile, System.Action onCovered = null, string label = null)
        {
            var context = MazeTransitionContext.Default(label);
            context.Additive = true;
            context.PlayOutAfterCovered = true;
            context.OnCovered = onCovered;
            return BeginAdditive(profile, context, MazeTransitionDirection.In);
        }

        public MazeTransitionHandle PlayAdditiveSwap(MazeVectorTransitionProfile profile, System.Action onCovered = null, string label = null)
        {
            var context = MazeTransitionContext.Default(label);
            context.Additive = true;
            context.PlayOutAfterCovered = true;
            context.OnCovered = onCovered;
            return BeginAdditive(profile, context, MazeTransitionDirection.In);
        }

        // Covers, calls onCovered, then clears immediately.
        public MazeTransitionHandle PlayInThenClear(MazeSquareTransitionProfile profile, System.Action onCovered = null, string label = null)
        {
            var context = MazeTransitionContext.Default(label);
            context.ClearOnCovered = true;
            context.OnCovered = onCovered;
            return PlayIn(profile, context);
        }

        public MazeTransitionHandle PlayInThenClear(MazeVectorTransitionProfile profile, System.Action onCovered = null, string label = null)
        {
            var context = MazeTransitionContext.Default(label);
            context.ClearOnCovered = true;
            context.OnCovered = onCovered;
            return PlayIn(profile, context);
        }

        // Covers, calls onCovered, then plays the matching out motion.
        public MazeTransitionHandle PlaySwap(MazeSquareTransitionProfile profile, System.Action onCovered, string label = null)
        {
            var context = MazeTransitionContext.Default(label);
            context.PlayOutAfterCovered = true;
            context.OnCovered = onCovered;
            return PlayIn(profile, context);
        }

        public MazeTransitionHandle PlaySwap(MazeSquareTransitionProfile inProfile, MazeSquareTransitionProfile outProfile, System.Action onCovered, string label = null)
        {
            var context = MazeTransitionContext.Default(label);
            context.PlayOutAfterCovered = true;
            context.OnCovered = onCovered;
            activeOutSquareProfile = outProfile;
            return Begin(inProfile, context, MazeTransitionDirection.In, outProfile);
        }

        public MazeTransitionHandle PlaySwap(MazeVectorTransitionProfile profile, System.Action onCovered, string label = null)
        {
            var context = MazeTransitionContext.Default(label);
            context.PlayOutAfterCovered = true;
            context.OnCovered = onCovered;
            return PlayIn(profile, context);
        }

        // Stays covered until the handle is released. Releasing early queues the reveal.
        public MazeTransitionHandle PlayGatedSwap(MazeSquareTransitionProfile profile, System.Action onCovered = null, string label = null)
        {
            return PlayGatedSwap(profile, null, onCovered, label);
        }

        public MazeTransitionHandle PlayGatedSwap(MazeSquareTransitionProfile inProfile, MazeSquareTransitionProfile outProfile, System.Action onCovered = null, string label = null)
        {
            var context = MazeTransitionContext.Default(label);
            context.OnCovered = onCovered;
            return Begin(inProfile, context, MazeTransitionDirection.In, outProfile, true);
        }

        public MazeTransitionHandle PlayGatedSwap(MazeVectorTransitionProfile profile, System.Action onCovered = null, string label = null)
        {
            var context = MazeTransitionContext.Default(label);
            context.OnCovered = onCovered;
            return Begin(profile, context, MazeTransitionDirection.In, true);
        }

        public void CoverInstant(MazeSquareTransitionProfile profile = null, MazeTransitionContext context = default)
        {
            var requestSerial = NextMainRequest();
            EndMainInstant(MazeTransitionResult.Replaced);
            if (mainRequestSerial != requestSerial)
            {
                return;
            }

            activeProfile = profile != null ? profile : MazeSquareTransitionProfile.CreateRuntimeDefault("MAZE Instant Cover");
            activeSquareProfile = activeProfile as MazeSquareTransitionProfile;
            activeVectorProfile = null;
            activeContext = PrepareContext(context, activeSquareProfile, null);
            activeRenderer = SelectRenderer(activeSquareProfile, activeContext);
            usingVectorRenderer = false;
            BuildPatternAndRenderer(activeSquareProfile, activeContext);
            Render(activeSquareProfile.InCompleteSeconds, MazeTransitionDirection.In);
            state = MazeTransitionState.Covered;
            coveredSent = true;
            activeOperationId = NextOperationId();
            activeHandle = CreateMainHandle(activeOperationId, LabelFor(activeProfile, activeContext), false);
            activeHandle.MarkCovered();
            MazeDiagnosticsLog.Info(DiagnosticsCategory, "instant_cover", "transition covered instantly", DiagnosticsData("instant_cover"));
        }

        public void ClearInstant()
        {
            NextMainRequest();
            EndMainInstant(MazeTransitionResult.Cancelled);
            ClearAdditiveTransitions();
        }

        private void EndMainInstant(MazeTransitionResult result)
        {
            var hadVisibleTransition = state != MazeTransitionState.Hidden || activeHandle != null;
            var finishedHandle = activeHandle;
            var finishedLabel = finishedHandle != null ? finishedHandle.Label : LabelFor(activeProfile, activeContext);
            DetachMainState();
            if (hadVisibleTransition)
            {
                MazeDiagnosticsLog.Info(DiagnosticsCategory, result == MazeTransitionResult.Replaced ? "replaced" : "instant_clear", result == MazeTransitionResult.Replaced ? "transition replaced" : "transition cleared instantly", MazeDiagnosticsLog.JoinData(
                    MazeDiagnosticsLog.JsonString("host", name),
                    MazeDiagnosticsLog.JsonString("label", finishedLabel),
                    MazeDiagnosticsLog.JsonString("result", result.ToString())));
            }
            finishedHandle?.MarkFinished(result);
        }

        private void DetachMainState()
        {
            activeRenderer?.Release();
            vectorOverlayRenderer.Release();
            activeRenderer = null;
            activeHandle = null;
            activeProfile = null;
            activeSquareProfile = null;
            activeOutSquareProfile = null;
            activeVectorProfile = null;
            activeOutVectorProfile = null;
            activeContext = default;
            usingVectorRenderer = false;
            state = MazeTransitionState.Hidden;
            elapsed = 0f;
            coveredSent = false;
            activeOperationId = 0;
            activeCoveredGate = false;
            activeGateReleaseRequested = false;
            ReleaseInputClaim();
        }

        private void ClearAdditiveTransitions()
        {
            var clearedPlaybacks = additivePlaybacks.ToArray();
            additivePlaybacks.Clear();
            for (var i = clearedPlaybacks.Length - 1; i >= 0; i--)
            {
                clearedPlaybacks[i].FinishAndRelease(MazeTransitionResult.Cancelled);
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            MazeDiagnosticsLog.Info(DiagnosticsCategory, "host_awake", "transition host ready", MazeDiagnosticsLog.JsonString("host", name));
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
            EndMainInstant(MazeTransitionResult.Cancelled);
            ClearAdditiveTransitions();
        }

        private void Update()
        {
            UpdateAdditiveTransitions();

            if (state == MazeTransitionState.Hidden || activeProfile == null || (!usingVectorRenderer && activeRenderer == null))
            {
                return;
            }

            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            {
                BuildActivePatternAndRenderer();
            }

            elapsed += activeProfile.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (state == MazeTransitionState.PlayingIn)
            {
                Render(elapsed, MazeTransitionDirection.In);

                if (!coveredSent && elapsed >= activeInCoveredSeconds)
                {
                    var coveredOperationId = activeOperationId;
                    SendCovered();
                    if (activeOperationId != coveredOperationId || state != MazeTransitionState.PlayingIn)
                    {
                        return;
                    }
                }

                if (activeContext.PlayOutAfterCovered && elapsed >= activeInCompleteSeconds)
                {
                    BeginOutFromCovered();
                    return;
                }

                if (!activeContext.PlayOutAfterCovered && !activeContext.ClearOnCovered && elapsed >= activeInCompleteSeconds)
                {
                    CompleteInAndRemainCovered();
                }
            }
            else if (state == MazeTransitionState.PlayingOut)
            {
                Render(elapsed, MazeTransitionDirection.Out);
                if (elapsed >= activeOutCompleteSeconds)
                {
                    FinishAndClear();
                }
            }
        }

        private MazeTransitionHandle Begin(MazeSquareTransitionProfile profile, MazeTransitionContext context, MazeTransitionDirection direction, MazeSquareTransitionProfile outProfile = null, bool coveredGate = false)
        {
            if (profile == null)
            {
                profile = MazeSquareTransitionProfile.CreateRuntimeDefault();
            }

            var requestSerial = NextMainRequest();
            var operationId = NextOperationId();
            EndMainInstant(MazeTransitionResult.Replaced);
            if (mainRequestSerial != requestSerial)
            {
                var replacedHandle = new MazeTransitionHandle(operationId, LabelFor(profile, context));
                replacedHandle.MarkFinished(MazeTransitionResult.Replaced);
                return replacedHandle;
            }

            activeProfile = profile;
            activeSquareProfile = profile;
            activeOutSquareProfile = outProfile;
            activeVectorProfile = null;
            activeOutVectorProfile = null;
            activeContext = PrepareContext(context, profile, outProfile);
            activeRenderer = SelectRenderer(profile, activeContext);
            usingVectorRenderer = false;
            activeOperationId = operationId;
            activeCoveredGate = coveredGate;
            activeGateReleaseRequested = false;
            activeHandle = CreateMainHandle(activeOperationId, LabelFor(profile, context), coveredGate);
            elapsed = 0f;
            coveredSent = false;
            state = direction == MazeTransitionDirection.In ? MazeTransitionState.PlayingIn : MazeTransitionState.PlayingOut;
            CaptureAndBlockInput(profile);
            BuildPatternAndRenderer(profile, activeContext);
            Render(0f, direction);
            MazeDiagnosticsLog.Info(DiagnosticsCategory, "play_start", "transition started", DiagnosticsData(direction == MazeTransitionDirection.In ? "play_in" : "play_out"));
            return activeHandle;
        }

        private MazeTransitionHandle Begin(MazeVectorTransitionProfile profile, MazeTransitionContext context, MazeTransitionDirection direction, bool coveredGate = false)
        {
            if (profile == null)
            {
                profile = MazeVectorTransitionProfile.CreateRuntimeDefault();
            }

            var requestSerial = NextMainRequest();
            var operationId = NextOperationId();
            EndMainInstant(MazeTransitionResult.Replaced);
            if (mainRequestSerial != requestSerial)
            {
                var replacedHandle = new MazeTransitionHandle(operationId, LabelFor(profile, context));
                replacedHandle.MarkFinished(MazeTransitionResult.Replaced);
                return replacedHandle;
            }

            activeProfile = profile;
            activeSquareProfile = null;
            activeOutSquareProfile = null;
            activeVectorProfile = profile;
            activeOutVectorProfile = null;
            activeContext = PrepareContext(context, profile, null);
            activeRenderer = null;
            usingVectorRenderer = true;
            activeOperationId = operationId;
            activeCoveredGate = coveredGate;
            activeGateReleaseRequested = false;
            activeHandle = CreateMainHandle(activeOperationId, LabelFor(profile, context), coveredGate);
            elapsed = 0f;
            coveredSent = false;
            state = direction == MazeTransitionDirection.In ? MazeTransitionState.PlayingIn : MazeTransitionState.PlayingOut;
            CaptureAndBlockInput(profile);
            BuildPatternAndRenderer(profile, activeContext);
            Render(0f, direction);
            MazeDiagnosticsLog.Info(DiagnosticsCategory, "play_start", "transition started", DiagnosticsData(direction == MazeTransitionDirection.In ? "play_in" : "play_out"));
            return activeHandle;
        }

        private MazeTransitionHandle BeginAdditive(MazeSquareTransitionProfile profile, MazeTransitionContext context, MazeTransitionDirection direction)
        {
            if (profile == null)
            {
                profile = MazeSquareTransitionProfile.CreateRuntimeDefault();
            }

            context.Additive = true;
            context = PrepareContext(context, profile, null);
            EnforceAdditiveCapacity(context);
            var sortingOffset = ResolveAdditiveSortingOffset(context);
            var playback = TransitionPlayback.CreateSquare(this, NextOperationId(), profile, context, direction, sortingOffset);
            additivePlaybacks.Add(playback);
            MazeDiagnosticsLog.Info(DiagnosticsCategory, "additive_start", "additive transition started", AdditiveDiagnosticsData(playback, "additive_start"));
            return playback.Handle;
        }

        private MazeTransitionHandle BeginAdditive(MazeVectorTransitionProfile profile, MazeTransitionContext context, MazeTransitionDirection direction)
        {
            if (profile == null)
            {
                profile = MazeVectorTransitionProfile.CreateRuntimeDefault();
            }

            context.Additive = true;
            context = PrepareContext(context, profile, null);
            EnforceAdditiveCapacity(context);
            var sortingOffset = ResolveAdditiveSortingOffset(context);
            var playback = TransitionPlayback.CreateVector(this, NextOperationId(), profile, context, direction, sortingOffset);
            additivePlaybacks.Add(playback);
            MazeDiagnosticsLog.Info(DiagnosticsCategory, "additive_start", "additive transition started", AdditiveDiagnosticsData(playback, "additive_start"));
            return playback.Handle;
        }

        private long NextOperationId()
        {
            unchecked
            {
                operationSerial++;
                if (operationSerial <= 0)
                {
                    operationSerial = 1;
                }
                return operationSerial;
            }
        }

        private long NextMainRequest()
        {
            unchecked
            {
                mainRequestSerial++;
                if (mainRequestSerial <= 0)
                {
                    mainRequestSerial = 1;
                }
                return mainRequestSerial;
            }
        }

        private MazeTransitionHandle CreateMainHandle(long operationId, string label, bool releasable)
        {
            System.Action<long> releaseRequest = releasable ? RequestMainRelease : null;
            return new MazeTransitionHandle(
                operationId,
                label,
                releaseRequest,
                RequestMainCancel);
        }

        private void RequestMainRelease(long operationId)
        {
            if (!activeCoveredGate || activeHandle == null || activeOperationId != operationId || activeHandle.OperationId != operationId)
            {
                return;
            }

            activeGateReleaseRequested = true;
            MazeDiagnosticsLog.Info(DiagnosticsCategory, "gate_release", "transition gate released", DiagnosticsData(state == MazeTransitionState.Covered ? "gate_release" : "gate_release_queued"));
            if (state == MazeTransitionState.Covered)
            {
                BeginOutFromCovered();
            }
        }

        private void RequestMainCancel(long operationId)
        {
            if (activeHandle == null || activeOperationId != operationId || activeHandle.OperationId != operationId)
            {
                return;
            }

            NextMainRequest();
            EndMainInstant(MazeTransitionResult.Cancelled);
        }

        private void RequestAdditiveCancel(long operationId)
        {
            for (var i = additivePlaybacks.Count - 1; i >= 0; i--)
            {
                var playback = additivePlaybacks[i];
                if (playback.Handle == null || playback.Handle.OperationId != operationId)
                {
                    continue;
                }

                MazeDiagnosticsLog.Info(DiagnosticsCategory, "additive_cancel", "additive transition cancelled", AdditiveDiagnosticsData(playback, "additive_cancel"));
                additivePlaybacks.RemoveAt(i);
                playback.FinishAndRelease(MazeTransitionResult.Cancelled);
                return;
            }
        }

        private void BeginOutFromCovered()
        {
            elapsed = 0f;
            state = MazeTransitionState.PlayingOut;
            coveredSent = true;
            if (activeOutSquareProfile != null)
            {
                activeProfile = activeOutSquareProfile;
                activeSquareProfile = activeOutSquareProfile;
                activeRenderer?.Release();
                activeRenderer = SelectRenderer(activeSquareProfile, activeContext);
                usingVectorRenderer = false;
                BuildPatternAndRenderer(activeSquareProfile, activeContext);
            }
            else if (activeOutVectorProfile != null)
            {
                activeProfile = activeOutVectorProfile;
                activeVectorProfile = activeOutVectorProfile;
                usingVectorRenderer = true;
                BuildPatternAndRenderer(activeVectorProfile, activeContext);
            }
            Render(0f, MazeTransitionDirection.Out);
            MazeDiagnosticsLog.Info(DiagnosticsCategory, "play_out", "transition out started", DiagnosticsData("play_out"));
        }

        private void SendCovered()
        {
            var operationId = activeOperationId;
            var handle = activeHandle;
            var context = activeContext;
            coveredSent = true;
            MazeDiagnosticsLog.Info(DiagnosticsCategory, "covered", "transition covered screen", DiagnosticsData("covered"));
            handle?.MarkCovered();
            if (activeOperationId != operationId)
            {
                return;
            }

            context.OnCovered?.Invoke();
            if (activeOperationId != operationId)
            {
                return;
            }

            if (context.ClearOnCovered)
            {
                CompleteAndClear(handle, context, "complete_on_covered");
                return;
            }

            if (activeCoveredGate)
            {
                if (activeGateReleaseRequested)
                {
                    BeginOutFromCovered();
                }
                else
                {
                    state = MazeTransitionState.Covered;
                    MazeDiagnosticsLog.Info(DiagnosticsCategory, "gate_wait", "transition gate waiting under cover", DiagnosticsData("gate_wait"));
                }
            }
        }

        private void FinishAndClear()
        {
            var handle = activeHandle;
            var context = activeContext;
            var diagnostics = DiagnosticsData("complete_out");
            DetachMainState();
            MazeDiagnosticsLog.Info(DiagnosticsCategory, "complete", "transition complete", diagnostics);
            handle?.MarkFinished(MazeTransitionResult.Completed);
            context.OnComplete?.Invoke();
        }

        private void CompleteAndClear(MazeTransitionHandle handle, MazeTransitionContext context, string eventName)
        {
            var diagnostics = DiagnosticsData(eventName);
            DetachMainState();
            MazeDiagnosticsLog.Info(DiagnosticsCategory, "complete", "transition complete", diagnostics);
            handle?.MarkFinished(MazeTransitionResult.Completed);
            context.OnComplete?.Invoke();
        }

        private void CompleteInAndRemainCovered()
        {
            var handle = activeHandle;
            var context = activeContext;
            var diagnostics = DiagnosticsData("complete_in");
            state = MazeTransitionState.Covered;
            activeHandle = null;
            activeOperationId = 0;
            activeCoveredGate = false;
            activeGateReleaseRequested = false;
            activeContext.OnCovered = null;
            activeContext.OnComplete = null;
            ReleaseInputClaim();
            MazeDiagnosticsLog.Info(DiagnosticsCategory, "complete", "transition in complete", diagnostics);
            handle?.MarkFinished(MazeTransitionResult.Completed);
            context.OnComplete?.Invoke();
        }

        private void UpdateAdditiveTransitions()
        {
            var updatePlaybacks = additivePlaybacks.ToArray();
            for (var i = updatePlaybacks.Length - 1; i >= 0; i--)
            {
                var playback = updatePlaybacks[i];
                if (!additivePlaybacks.Contains(playback))
                {
                    continue;
                }

                if (playback.Update(this))
                {
                    MazeDiagnosticsLog.Info(DiagnosticsCategory, "additive_complete", "additive transition complete", AdditiveDiagnosticsData(playback, "additive_complete"));
                    var currentIndex = additivePlaybacks.IndexOf(playback);
                    if (currentIndex >= 0)
                    {
                        additivePlaybacks.RemoveAt(currentIndex);
                        playback.FinishAndRelease(MazeTransitionResult.Completed, true);
                    }
                }
            }
        }

        private void EnforceAdditiveCapacity(MazeTransitionContext context)
        {
            var maxActive = context.AdditiveMaxActive > 0 ? context.AdditiveMaxActive : DefaultMaxAdditiveTransitions;
            while (additivePlaybacks.Count >= maxActive && additivePlaybacks.Count > 0)
            {
                var dropped = additivePlaybacks[0];
                MazeDiagnosticsLog.Info(DiagnosticsCategory, "additive_drop", "additive transition dropped by cap", AdditiveDiagnosticsData(dropped, "additive_drop"));
                additivePlaybacks.RemoveAt(0);
                dropped.FinishAndRelease(MazeTransitionResult.Replaced);
            }
        }

        private int ResolveAdditiveSortingOffset(MazeTransitionContext context)
        {
            return context.SortingOrderOffset != 0
                ? context.SortingOrderOffset
                : 20 + additivePlaybacks.Count * 2;
        }

        private void BuildActivePatternAndRenderer()
        {
            if (activeVectorProfile != null)
            {
                BuildPatternAndRenderer(activeVectorProfile, activeContext);
                return;
            }

            BuildPatternAndRenderer(activeSquareProfile, activeContext);
        }

        private void BuildPatternAndRenderer(MazeSquareTransitionProfile profile, MazeTransitionContext context)
        {
            var width = Mathf.Max(1, Screen.width);
            var height = Mathf.Max(1, Screen.height);
            lastScreenWidth = width;
            lastScreenHeight = height;
            var rect = new Rect(0f, 0f, width, height);
            pattern.Build(profile, rect, context);
            hookLinePattern.Build(profile, rect);
            activeInCoveredSeconds = pattern.MaxInDelaySeconds + Mathf.Max(profile.MoveSeconds, profile.FadeSeconds) + profile.SettleSeconds;
            activeInCompleteSeconds = activeInCoveredSeconds + profile.HoldSeconds;
            activeOutCompleteSeconds = Mathf.Max(pattern.MaxOutDelaySeconds + Mathf.Max(profile.MoveSeconds, profile.FadeSeconds), hookLinePattern.MaxDelaySeconds);
            activeRenderer.Build(profile, rect, context.Parent != null ? context.Parent : transform, context.Camera);
            if (activeRenderer is MazeTransitionOverlayRenderer overlay)
            {
                overlay.SetDrawOrder(pattern.CreateOldestOnTopDrawOrder());
            }
            if (hookLinePattern.ElementCount > 0)
            {
                vectorOverlayRenderer.Build(profile, rect, context.Parent != null ? context.Parent : transform, hookLinePattern.ElementCount);
                vectorOverlayRenderer.SetSortingOrder(profile.SortingOrder + 1);
            }
            else if (!usingVectorRenderer)
            {
                vectorOverlayRenderer.Release();
            }
            MazeDiagnosticsLog.Info(DiagnosticsCategory, "build", "transition surface built", BuildDiagnosticsData(profile, rect));
        }

        private MazeTransitionContext PrepareContext(MazeTransitionContext context, MazeSquareTransitionProfile profile, MazeSquareTransitionProfile outProfile)
        {
            if (!context.HasRandomSeed && (context.Flicker || (profile != null && profile.RandomizesPerPlay) || (outProfile != null && outProfile.RandomizesPerPlay)))
            {
                context.HasRandomSeed = true;
                context.RandomSeed = NextTransitionSeed(profile != null ? profile.Seed : outProfile.Seed);
            }

            return context;
        }

        private MazeTransitionContext PrepareContext(MazeTransitionContext context, MazeVectorTransitionProfile profile, MazeVectorTransitionProfile outProfile)
        {
            if (!context.HasRandomSeed && (context.Flicker || (profile != null && profile.RandomizesPerPlay) || (outProfile != null && outProfile.RandomizesPerPlay)))
            {
                context.HasRandomSeed = true;
                context.RandomSeed = NextTransitionSeed(profile != null ? profile.Seed : outProfile.Seed);
            }

            return context;
        }

        private int NextTransitionSeed(int baseSeed)
        {
            unchecked
            {
                transitionSerial++;
                return baseSeed
                    ^ transitionSerial * 73856093
                    ^ Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f)
                    ^ Time.frameCount * 19349663;
            }
        }

        private void BuildPatternAndRenderer(MazeVectorTransitionProfile profile, MazeTransitionContext context)
        {
            var width = Mathf.Max(1, Screen.width);
            var height = Mathf.Max(1, Screen.height);
            lastScreenWidth = width;
            lastScreenHeight = height;
            var rect = new Rect(0f, 0f, width, height);
            vectorPattern.Build(profile, rect, context);
            activeInCoveredSeconds = vectorPattern.MaxDelaySeconds + profile.InSeconds;
            activeInCompleteSeconds = activeInCoveredSeconds + profile.HoldSeconds;
            activeOutCompleteSeconds = vectorPattern.MaxDelaySeconds + profile.OutSeconds;
            vectorOverlayRenderer.Build(profile, rect, context.Parent != null ? context.Parent : transform, vectorPattern.ElementCount);
            MazeDiagnosticsLog.Info(DiagnosticsCategory, "build", "vector transition surface built", BuildDiagnosticsData(profile, rect));
        }

        private void Render(float time, MazeTransitionDirection direction)
        {
            if (usingVectorRenderer && activeVectorProfile != null)
            {
                for (var i = 0; i < vectorPattern.ElementCount; i++)
                {
                    vectorOverlayRenderer.SetElement(i, vectorPattern.Evaluate(activeVectorProfile, i, time, direction));
                }
                return;
            }

            if (activeRenderer is MazeTransitionGpuFullscreenRenderer gpu)
            {
                gpu.Render(activeSquareProfile, activeContext, time, direction);
                return;
            }

            for (var i = 0; i < pattern.TileCount; i++)
            {
                activeRenderer.SetTile(i, pattern.Evaluate(activeSquareProfile, i, time, direction));
            }

            if (hookLinePattern.ElementCount > 0)
            {
                for (var i = 0; i < hookLinePattern.ElementCount; i++)
                {
                    vectorOverlayRenderer.SetElement(i, direction == MazeTransitionDirection.Out || activeContext.Flicker
                        ? hookLinePattern.Evaluate(i, time)
                        : default);
                }
            }

            activeRenderer.Upload();
        }

        private IMazeTransitionRenderer SelectRenderer(MazeSquareTransitionProfile profile, MazeTransitionContext context)
        {
            if (profile.Backend == MazeTransitionBackend.GpuFullscreen)
            {
                if (MazeTransitionGpuFullscreenRenderer.Supports(profile, context, out var reason))
                {
                    return gpuRenderer;
                }

                MazeDiagnosticsLog.Info(DiagnosticsCategory, "gpu_fallback", "GPU transition backend fell back to batched CPU overlay", MazeDiagnosticsLog.JoinData(
                    MazeDiagnosticsLog.JsonString("profile", profile != null ? profile.name : string.Empty),
                    MazeDiagnosticsLog.JsonString("reason", reason),
                    MazeDiagnosticsLog.JsonString("fallback", MazeTransitionBackend.Overlay.ToString())));
            }

            if (profile.Backend == MazeTransitionBackend.CameraMesh)
            {
                return meshRenderer;
            }

            return overlayRenderer;
        }

        private static IMazeTransitionRenderer CreateRenderer(MazeSquareTransitionProfile profile, MazeTransitionContext context)
        {
            if (profile.Backend == MazeTransitionBackend.GpuFullscreen
                && MazeTransitionGpuFullscreenRenderer.Supports(profile, context, out _))
            {
                return new MazeTransitionGpuFullscreenRenderer();
            }

            return profile.Backend == MazeTransitionBackend.CameraMesh
                ? new MazeTransitionMeshRenderer()
                : new MazeTransitionOverlayRenderer();
        }


        private static string RendererBackendLabel(IMazeTransitionRenderer renderer)
        {
            return renderer switch
            {
                MazeTransitionGpuFullscreenRenderer => MazeTransitionBackend.GpuFullscreen.ToString(),
                MazeTransitionMeshRenderer => MazeTransitionBackend.CameraMesh.ToString(),
                MazeTransitionOverlayRenderer => "OverlayBatch",
                null => string.Empty,
                _ => renderer.GetType().Name,
            };
        }

        private void CaptureAndBlockInput(MazeTransitionProfile profile)
        {
            ReleaseInputClaim();
            if (!profile.BlockInput)
            {
                return;
            }

            inputBlockClaim = AcquireInputBlock?.Invoke($"transition:{activeOperationId}:{LabelFor(activeProfile, activeContext)}");
            if (inputBlockClaim == null)
            {
                MazeDiagnosticsLog.WarnOnce(
                    "maze_transition_input_provider_missing",
                    DiagnosticsCategory,
                    "input_provider_missing",
                    "transition requested input blocking, but no input-block provider is configured");
            }
        }

        private void ReleaseInputClaim()
        {
            var claim = inputBlockClaim;
            inputBlockClaim = null;
            claim?.Dispose();
        }

        private static string LabelFor(MazeTransitionProfile profile, MazeTransitionContext context)
        {
            if (!string.IsNullOrWhiteSpace(context.Label))
            {
                return context.Label;
            }

            return profile != null ? profile.DiagnosticsLabel : "transition";
        }

        private string DiagnosticsData(string eventName)
        {
            return MazeDiagnosticsLog.JoinData(
                MazeDiagnosticsLog.JsonString("event", eventName),
                MazeDiagnosticsLog.JsonString("label", activeHandle != null ? activeHandle.Label : LabelFor(activeProfile, activeContext)),
                MazeDiagnosticsLog.JsonString("profile", activeProfile != null ? activeProfile.name : string.Empty),
                MazeDiagnosticsLog.JsonString("backend", activeProfile != null ? activeProfile.Backend.ToString() : string.Empty),
                MazeDiagnosticsLog.JsonString("actualBackend", RendererBackendLabel(activeRenderer)),
                MazeDiagnosticsLog.JsonString("transitionId", activeProfile != null ? activeProfile.TransitionId : string.Empty),
                MazeDiagnosticsLog.JsonString("pattern", ActivePatternLabel()),
                MazeDiagnosticsLog.JsonNumber("operationId", activeOperationId),
                MazeDiagnosticsLog.JsonBool("coveredGate", activeCoveredGate),
                MazeDiagnosticsLog.JsonBool("gateReleaseRequested", activeGateReleaseRequested),
                MazeDiagnosticsLog.JsonBool("flicker", activeContext.Flicker),
                MazeDiagnosticsLog.JsonNumber("tiles", activeSquareProfile != null ? pattern.TileCount : 0),
                MazeDiagnosticsLog.JsonNumber("hookLines", activeSquareProfile != null ? hookLinePattern.ElementCount : 0),
                MazeDiagnosticsLog.JsonNumber("vectorElements", activeVectorProfile != null ? vectorPattern.ElementCount : 0));
        }

        private string ActivePatternLabel()
        {
            if (activeSquareProfile != null)
            {
                return activeSquareProfile.MotionPattern.ToString();
            }

            return activeVectorProfile != null ? activeVectorProfile.Pattern.ToString() : string.Empty;
        }

        private string BuildDiagnosticsData(MazeSquareTransitionProfile profile, Rect screen)
        {
            var bounds = pattern.TargetBounds;
            return MazeDiagnosticsLog.JoinData(
                MazeDiagnosticsLog.JsonString("profile", profile != null ? profile.name : string.Empty),
                MazeDiagnosticsLog.JsonString("backend", profile != null ? profile.Backend.ToString() : string.Empty),
                MazeDiagnosticsLog.JsonString("actualBackend", RendererBackendLabel(activeRenderer)),
                MazeDiagnosticsLog.JsonString("pattern", profile != null ? profile.MotionPattern.ToString() : string.Empty),
                MazeDiagnosticsLog.JsonNumber("screenW", screen.width),
                MazeDiagnosticsLog.JsonNumber("screenH", screen.height),
                MazeDiagnosticsLog.JsonNumber("columns", profile != null ? profile.Columns : 0),
                MazeDiagnosticsLog.JsonNumber("rows", profile != null ? profile.Rows : 0),
                MazeDiagnosticsLog.JsonNumber("tileW", pattern.TileSize.x),
                MazeDiagnosticsLog.JsonNumber("tileH", pattern.TileSize.y),
                MazeDiagnosticsLog.JsonNumber("overscan", profile != null ? profile.OverscanPixels : 0f),
                MazeDiagnosticsLog.JsonNumber("gap", profile != null ? profile.GapPixels : 0f),
                MazeDiagnosticsLog.JsonNumber("alternateRowOffsetTiles", profile != null ? profile.AlternateRowOffsetTiles : 0f),
                MazeDiagnosticsLog.JsonNumber("baseDelay", profile != null ? profile.BaseDelaySpanSeconds : 0f),
                MazeDiagnosticsLog.JsonNumber("maxDelay", pattern.MaxDelaySeconds),
                MazeDiagnosticsLog.JsonNumber("maxInDelay", pattern.MaxInDelaySeconds),
                MazeDiagnosticsLog.JsonNumber("maxOutDelay", pattern.MaxOutDelaySeconds),
                MazeDiagnosticsLog.JsonString("spreadDirection", profile != null ? profile.SpreadDirection.ToString() : string.Empty),
                MazeDiagnosticsLog.JsonNumber("spreadSeconds", profile != null ? profile.DirectionalSpreadSeconds : 0f),
                MazeDiagnosticsLog.JsonString("spreadEase", profile != null ? profile.SpreadEase.ToString() : string.Empty),
                MazeDiagnosticsLog.JsonNumber("firstInDelay", pattern.GetDelaySeconds(0, MazeTransitionDirection.In)),
                MazeDiagnosticsLog.JsonNumber("lastInDelay", pattern.GetDelaySeconds(Mathf.Max(0, pattern.TileCount - 1), MazeTransitionDirection.In)),
                MazeDiagnosticsLog.JsonNumber("firstOutDelay", pattern.GetDelaySeconds(0, MazeTransitionDirection.Out)),
                MazeDiagnosticsLog.JsonNumber("lastOutDelay", pattern.GetDelaySeconds(Mathf.Max(0, pattern.TileCount - 1), MazeTransitionDirection.Out)),
                MazeDiagnosticsLog.JsonNumber("startScale", profile != null ? profile.RandomTessellateStartScale : 1f),
                MazeDiagnosticsLog.JsonNumber("shimmer", profile != null ? profile.RandomTessellateShimmer : 0f),
                MazeDiagnosticsLog.JsonBool("shrinkOutWithoutAlphaFade", profile != null && profile.ShrinkOutWithoutAlphaFade),
                MazeDiagnosticsLog.JsonBool("randomizesPerPlay", profile != null && profile.RandomizesPerPlay),
                MazeDiagnosticsLog.JsonNumber("randomSeed", activeContext.HasRandomSeed ? activeContext.RandomSeed : (profile != null ? profile.Seed : 0)),
                MazeDiagnosticsLog.JsonBool("flicker", activeContext.Flicker),
                MazeDiagnosticsLog.JsonNumber("flickerDensity", activeContext.Flicker ? activeContext.FlickerDensity : 0f),
                MazeDiagnosticsLog.JsonNumber("flickerActiveTiles", activeContext.Flicker ? pattern.FlickerActiveCount : 0),
                MazeDiagnosticsLog.JsonNumber("duplicateStarts", pattern.DuplicateStartCount),
                MazeDiagnosticsLog.JsonNumber("cardinalFailures", pattern.CardinalOffsetFailures),
                MazeDiagnosticsLog.JsonNumber("unpairedStarts", pattern.UnpairedStartCount),
                MazeDiagnosticsLog.JsonNumber("up", pattern.DirectionUpCount),
                MazeDiagnosticsLog.JsonNumber("right", pattern.DirectionRightCount),
                MazeDiagnosticsLog.JsonNumber("down", pattern.DirectionDownCount),
                MazeDiagnosticsLog.JsonNumber("left", pattern.DirectionLeftCount),
                MazeDiagnosticsLog.JsonNumber("hookLines", hookLinePattern.ElementCount),
                MazeDiagnosticsLog.JsonBool("coversScreen", bounds.xMin <= screen.xMin && bounds.yMin <= screen.yMin && bounds.xMax >= screen.xMax && bounds.yMax >= screen.yMax));
        }

        private string BuildDiagnosticsData(MazeVectorTransitionProfile profile, Rect screen)
        {
            var bounds = vectorPattern.CoverBounds;
            return MazeDiagnosticsLog.JoinData(
                MazeDiagnosticsLog.JsonString("profile", profile != null ? profile.name : string.Empty),
                MazeDiagnosticsLog.JsonString("backend", "Overlay"),
                MazeDiagnosticsLog.JsonString("actualBackend", "VectorOverlay"),
                MazeDiagnosticsLog.JsonString("pattern", profile != null ? profile.Pattern.ToString() : string.Empty),
                MazeDiagnosticsLog.JsonNumber("screenW", screen.width),
                MazeDiagnosticsLog.JsonNumber("screenH", screen.height),
                MazeDiagnosticsLog.JsonNumber("elements", vectorPattern.ElementCount),
                MazeDiagnosticsLog.JsonNumber("maxDelay", vectorPattern.MaxDelaySeconds),
                MazeDiagnosticsLog.JsonNumber("firstDelay", vectorPattern.GetDelaySeconds(0)),
                MazeDiagnosticsLog.JsonNumber("lastDelay", vectorPattern.GetDelaySeconds(Mathf.Max(0, vectorPattern.ElementCount - 1))),
                MazeDiagnosticsLog.JsonNumber("inSeconds", profile != null ? profile.InSeconds : 0f),
                MazeDiagnosticsLog.JsonNumber("outSeconds", profile != null ? profile.OutSeconds : 0f),
                MazeDiagnosticsLog.JsonNumber("orderedDelayBalance", profile != null ? profile.OrderedDelayBalance : 0f),
                MazeDiagnosticsLog.JsonString("spreadDirection", profile != null ? profile.SpreadDirection.ToString() : string.Empty),
                MazeDiagnosticsLog.JsonNumber("spreadSeconds", profile != null ? profile.DirectionalSpreadSeconds : 0f),
                MazeDiagnosticsLog.JsonString("spreadEase", profile != null ? profile.SpreadEase.ToString() : string.Empty),
                MazeDiagnosticsLog.JsonNumber("line", profile != null ? profile.LineThicknessPixels : 0f),
                MazeDiagnosticsLog.JsonNumber("accent", profile != null ? profile.AccentThicknessPixels : 0f),
                MazeDiagnosticsLog.JsonNumber("intensity", profile != null ? profile.Intensity : 0f),
                MazeDiagnosticsLog.JsonBool("randomizesPerPlay", profile != null && profile.RandomizesPerPlay),
                MazeDiagnosticsLog.JsonNumber("randomSeed", activeContext.HasRandomSeed ? activeContext.RandomSeed : (profile != null ? profile.Seed : 0)),
                MazeDiagnosticsLog.JsonBool("flicker", activeContext.Flicker),
                MazeDiagnosticsLog.JsonNumber("flickerDensity", activeContext.Flicker ? activeContext.FlickerDensity : 0f),
                MazeDiagnosticsLog.JsonBool("coversScreen", bounds.xMin <= screen.xMin && bounds.yMin <= screen.yMin && bounds.xMax >= screen.xMax && bounds.yMax >= screen.yMax));
        }

        private string AdditiveDiagnosticsData(TransitionPlayback playback, string eventName)
        {
            return MazeDiagnosticsLog.JoinData(
                MazeDiagnosticsLog.JsonString("event", eventName),
                MazeDiagnosticsLog.JsonString("mode", "additive"),
                MazeDiagnosticsLog.JsonString("label", playback.Handle != null ? playback.Handle.Label : string.Empty),
                MazeDiagnosticsLog.JsonString("profile", playback.ActiveProfile != null ? playback.ActiveProfile.name : string.Empty),
                MazeDiagnosticsLog.JsonString("transitionId", playback.ActiveProfile != null ? playback.ActiveProfile.TransitionId : string.Empty),
                MazeDiagnosticsLog.JsonString("pattern", playback.PatternLabel),
                MazeDiagnosticsLog.JsonBool("flicker", playback.Context.Flicker),
                MazeDiagnosticsLog.JsonNumber("sortingOrder", playback.SortingOrder),
                MazeDiagnosticsLog.JsonNumber("activeAdditives", additivePlaybacks.Count),
                MazeDiagnosticsLog.JsonNumber("randomSeed", playback.Context.HasRandomSeed ? playback.Context.RandomSeed : 0));
        }

        private sealed class TransitionPlayback
        {
            private readonly MazeSquareTransitionPattern squarePattern = new();
            private readonly MazeVectorTransitionPattern vectorPattern = new();
            private readonly MazeSquareTransitionHookLinePattern hookLinePattern = new();
            private readonly MazeVectorTransitionOverlayRenderer vectorRenderer = new();
            private IMazeTransitionRenderer squareRenderer;
            private MazeSquareTransitionProfile squareProfile;
            private MazeVectorTransitionProfile vectorProfile;
            private bool usingVector;
            private MazeTransitionState state;
            private float elapsed;
            private bool coveredSent;
            private float inCoveredSeconds;
            private float inCompleteSeconds;
            private float outCompleteSeconds;
            private int lastScreenWidth;
            private int lastScreenHeight;

            public MazeTransitionHandle Handle { get; private set; }
            public MazeTransitionProfile ActiveProfile { get; private set; }
            public MazeTransitionContext Context { get; private set; }
            public int SortingOrder { get; private set; }
            public string PatternLabel => squareProfile != null ? squareProfile.MotionPattern.ToString() : (vectorProfile != null ? vectorProfile.Pattern.ToString() : string.Empty);

            public static TransitionPlayback CreateSquare(MazeTransitionHost host, long operationId, MazeSquareTransitionProfile profile, MazeTransitionContext context, MazeTransitionDirection direction, int sortingOffset)
            {
                var playback = new TransitionPlayback
                {
                    squareProfile = profile,
                    ActiveProfile = profile,
                    Context = context,
                    usingVector = false,
                    state = direction == MazeTransitionDirection.In ? MazeTransitionState.PlayingIn : MazeTransitionState.PlayingOut,
                    Handle = new MazeTransitionHandle(operationId, LabelFor(profile, context), null, host.RequestAdditiveCancel),
                    SortingOrder = profile.SortingOrder + sortingOffset,
                };
                playback.squareRenderer = CreateRenderer(profile, context);
                playback.Build(host);
                playback.Render(0f, direction);
                return playback;
            }

            public static TransitionPlayback CreateVector(MazeTransitionHost host, long operationId, MazeVectorTransitionProfile profile, MazeTransitionContext context, MazeTransitionDirection direction, int sortingOffset)
            {
                var playback = new TransitionPlayback
                {
                    vectorProfile = profile,
                    ActiveProfile = profile,
                    Context = context,
                    usingVector = true,
                    state = direction == MazeTransitionDirection.In ? MazeTransitionState.PlayingIn : MazeTransitionState.PlayingOut,
                    Handle = new MazeTransitionHandle(operationId, LabelFor(profile, context), null, host.RequestAdditiveCancel),
                    SortingOrder = profile.SortingOrder + sortingOffset,
                };
                playback.Build(host);
                playback.Render(0f, direction);
                return playback;
            }

            public bool Update(MazeTransitionHost host)
            {
                if (ActiveProfile == null || Handle == null)
                {
                    return true;
                }

                if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
                {
                    Build(host);
                }

                elapsed += ActiveProfile.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                if (state == MazeTransitionState.PlayingIn)
                {
                    Render(elapsed, MazeTransitionDirection.In);
                    if (!coveredSent && elapsed >= inCoveredSeconds)
                    {
                        coveredSent = true;
                        var handle = Handle;
                        Handle.MarkCovered();
                        if (Handle != handle || ActiveProfile == null)
                        {
                            return false;
                        }

                        Context.OnCovered?.Invoke();
                        if (Handle != handle || ActiveProfile == null)
                        {
                            return false;
                        }

                        if (Context.ClearOnCovered)
                        {
                            return true;
                        }
                    }

                    if (Context.PlayOutAfterCovered && elapsed >= inCompleteSeconds)
                    {
                        elapsed = 0f;
                        state = MazeTransitionState.PlayingOut;
                        coveredSent = true;
                        Render(0f, MazeTransitionDirection.Out);
                        return false;
                    }

                    if (!Context.PlayOutAfterCovered && !Context.ClearOnCovered && elapsed >= inCompleteSeconds)
                    {
                        return true;
                    }
                }
                else if (state == MazeTransitionState.PlayingOut)
                {
                    Render(elapsed, MazeTransitionDirection.Out);
                    if (elapsed >= outCompleteSeconds)
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Release()
            {
                squareRenderer?.Release();
                vectorRenderer.Release();
            }

            public void FinishAndRelease(MazeTransitionResult result, bool invokeOnComplete = false)
            {
                var handle = Handle;
                var context = Context;
                Handle = null;
                ActiveProfile = null;
                Release();
                handle?.MarkFinished(result);
                if (invokeOnComplete && result == MazeTransitionResult.Completed)
                {
                    context.OnComplete?.Invoke();
                }
            }

            private void Build(MazeTransitionHost host)
            {
                var width = Mathf.Max(1, Screen.width);
                var height = Mathf.Max(1, Screen.height);
                lastScreenWidth = width;
                lastScreenHeight = height;
                var rect = new Rect(0f, 0f, width, height);
                var parent = Context.Parent != null ? Context.Parent : host.transform;

                if (usingVector)
                {
                    vectorPattern.Build(vectorProfile, rect, Context);
                    inCoveredSeconds = vectorPattern.MaxDelaySeconds + vectorProfile.InSeconds;
                    inCompleteSeconds = inCoveredSeconds + vectorProfile.HoldSeconds;
                    outCompleteSeconds = vectorPattern.MaxDelaySeconds + vectorProfile.OutSeconds;
                    vectorRenderer.Build(vectorProfile, rect, parent, vectorPattern.ElementCount);
                    vectorRenderer.SetSortingOrder(SortingOrder);
                    return;
                }

                squarePattern.Build(squareProfile, rect, Context);
                hookLinePattern.Build(squareProfile, rect);
                inCoveredSeconds = squarePattern.MaxInDelaySeconds + Mathf.Max(squareProfile.MoveSeconds, squareProfile.FadeSeconds) + squareProfile.SettleSeconds;
                inCompleteSeconds = inCoveredSeconds + squareProfile.HoldSeconds;
                outCompleteSeconds = Mathf.Max(squarePattern.MaxOutDelaySeconds + Mathf.Max(squareProfile.MoveSeconds, squareProfile.FadeSeconds), hookLinePattern.MaxDelaySeconds);
                squareRenderer.Build(squareProfile, rect, parent, Context.Camera);
                squareRenderer.SetSortingOrder(SortingOrder);
                if (squareRenderer is MazeTransitionOverlayRenderer overlay)
                {
                    overlay.SetDrawOrder(squarePattern.CreateOldestOnTopDrawOrder());
                }

                if (hookLinePattern.ElementCount > 0)
                {
                    vectorRenderer.Build(squareProfile, rect, parent, hookLinePattern.ElementCount);
                    vectorRenderer.SetSortingOrder(SortingOrder + 1);
                }
                else
                {
                    vectorRenderer.Release();
                }
            }

            private void Render(float time, MazeTransitionDirection direction)
            {
                if (usingVector)
                {
                    for (var i = 0; i < vectorPattern.ElementCount; i++)
                    {
                        vectorRenderer.SetElement(i, vectorPattern.Evaluate(vectorProfile, i, time, direction));
                    }
                    return;
                }

                if (squareRenderer is MazeTransitionGpuFullscreenRenderer gpu)
                {
                    gpu.Render(squareProfile, Context, time, direction);
                    return;
                }

                for (var i = 0; i < squarePattern.TileCount; i++)
                {
                    squareRenderer.SetTile(i, squarePattern.Evaluate(squareProfile, i, time, direction));
                }

                if (hookLinePattern.ElementCount > 0)
                {
                    for (var i = 0; i < hookLinePattern.ElementCount; i++)
                    {
                        vectorRenderer.SetElement(i, direction == MazeTransitionDirection.Out || Context.Flicker
                            ? hookLinePattern.Evaluate(i, time)
                            : default);
                    }
                }

                squareRenderer.Upload();
            }
        }
    }
}
