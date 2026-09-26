using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Maze
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MazeFocusController : MonoBehaviour, IMazeProfilingFeatureProvider
    {
        private static readonly int FocusParamsAId = Shader.PropertyToID("_MazeFocus_ParamsA");
        private static readonly int FocusParamsBId = Shader.PropertyToID("_MazeFocus_ParamsB");
        private static readonly int FocusParamsCId = Shader.PropertyToID("_MazeFocus_ParamsC");
        private static readonly int FocusParamsDId = Shader.PropertyToID("_MazeFocus_ParamsD");
        private static readonly int LensParamsAId = Shader.PropertyToID("_MazeFocus_LensParamsA");
        private static readonly int LensParamsBId = Shader.PropertyToID("_MazeFocus_LensParamsB");
        private static readonly int LensParamsCId = Shader.PropertyToID("_MazeFocus_LensParamsC");
        private static readonly int LensParamsDId = Shader.PropertyToID("_MazeFocus_LensParamsD");
        private static readonly List<MazeFocusController> Controllers = new();

        [SerializeField] private MazeFocusProfile profile;

        public MazeFocusProfile Profile => profile;
        public bool IsRenderingEnabled => isActiveAndEnabled && profile != null && profile.RendersFocus;

        public static MazeFocusController ResolveForCamera(Camera camera)
        {
            CleanupNullControllers();
            MazeFocusController first = null;
            MazeFocusController activeSceneMatch = null;
            var activeScene = SceneManager.GetActiveScene();
            var cameraScene = camera != null ? camera.gameObject.scene : default;

            for (var i = 0; i < Controllers.Count; i++)
            {
                var controller = Controllers[i];
                if (controller == null || !controller.IsRenderingEnabled)
                {
                    continue;
                }

                first ??= controller;
                var scene = controller.gameObject.scene;
                if (camera != null && scene.IsValid() && cameraScene.IsValid() && scene == cameraScene)
                {
                    return controller;
                }

                if (activeSceneMatch == null && scene.IsValid() && scene == activeScene)
                {
                    activeSceneMatch = controller;
                }
            }

            return activeSceneMatch != null ? activeSceneMatch : first;
        }

        private void OnEnable()
        {
            if (!Controllers.Contains(this))
            {
                Controllers.Add(this);
            }
        }

        private void OnDisable()
        {
            Controllers.Remove(this);
        }

        private void OnValidate()
        {
            if (!Controllers.Contains(this) && isActiveAndEnabled)
            {
                Controllers.Add(this);
            }
        }

        public bool AllowsCamera(Camera camera, bool isSceneViewCamera)
        {
            return !isSceneViewCamera || profile == null || profile.renderInSceneView;
        }

        public void ApplyToMaterial(Material material, Camera camera, bool isSceneViewCamera)
        {
            if (material == null || profile == null)
            {
                return;
            }

            var enabled = IsRenderingEnabled && AllowsCamera(camera, isSceneViewCamera);
            var depthEnabled = enabled && profile.RendersDepthBlur;
            var lensEnabled = enabled && profile.RendersLens;
            material.SetVector(FocusParamsAId, new Vector4(
                depthEnabled ? 1f : 0f,
                depthEnabled ? profile.MaxBlurRadius : 0f,
                (float)profile.debugMode,
                profile.cloudBand == MazeFocusBackgroundBand.FarBlur ? 1f : 0f));
            material.SetVector(FocusParamsBId, new Vector4(
                depthEnabled ? profile.EffectiveSampleCount : 0,
                Mathf.Clamp(profile.silhouetteSpread, 0.5f, 2f),
                Mathf.Max(0.01f, profile.layerSeparation),
                depthEnabled ? profile.EffectiveDownsample : 1));
            material.SetVector(FocusParamsCId, new Vector4(
                Mathf.Max(0.01f, profile.focusStart),
                profile.EffectiveFocusEnd,
                Mathf.Max(0f, profile.closeTransition),
                Mathf.Max(0f, profile.farTransition)));
            material.SetVector(FocusParamsDId, new Vector4(
                Mathf.Max(0f, profile.closeBlurRadius),
                Mathf.Max(0f, profile.farBlurRadius),
                profile.skyBand == MazeFocusBackgroundBand.FarBlur ? 1f : 0f,
                0f));

            material.SetVector(LensParamsAId, new Vector4(
                lensEnabled && profile.RendersDistortion ? 1f : 0f,
                lensEnabled && profile.RendersChromatic ? 1f : 0f,
                0f,
                0f));
            material.SetVector(LensParamsBId, new Vector4(
                Mathf.Clamp01(profile.lensCenter.x),
                Mathf.Clamp01(profile.lensCenter.y),
                profile.distortionAmount,
                Mathf.Max(1f, profile.cropScale)));
            material.SetVector(LensParamsCId, new Vector4(
                Mathf.Max(0f, profile.distortionX),
                Mathf.Max(0f, profile.distortionY),
                Mathf.Max(0.25f, profile.distortionFalloff),
                profile.chromaticSpreadPixels));
            material.SetVector(LensParamsDId, new Vector4(
                Mathf.Clamp(profile.chromaticStart, 0f, 0.99f),
                Mathf.Max(0.25f, profile.chromaticFalloff),
                0f,
                0f));

            LogState(enabled, depthEnabled, lensEnabled);
        }

        private void LogState(bool enabled, bool depthEnabled, bool lensEnabled)
        {
            if (!Application.isPlaying || profile == null)
            {
                return;
            }

            var state = $"{enabled}|depth={depthEnabled}|lens={lensEnabled}|{profile.quality}|close={profile.closeBlurRadius:0.###}|focus={profile.focusStart:0.###}-{profile.EffectiveFocusEnd:0.###}|far={profile.farBlurRadius:0.###}|samples={profile.EffectiveSampleCount}";
            MazeDiagnosticsLog.InfoOnChange(
                "graphics.focus.state:" + GetEntityId(),
                state,
                "Graphics.Focus",
                "state",
                "focus material state applied",
                MazeDiagnosticsLog.JoinData(
                    MazeDiagnosticsLog.JsonBool("enabled", enabled),
                    MazeDiagnosticsLog.JsonBool("depthBlur", depthEnabled),
                    MazeDiagnosticsLog.JsonBool("lens", lensEnabled),
                    MazeDiagnosticsLog.JsonString("quality", profile.quality.ToString()),
                    MazeDiagnosticsLog.JsonNumber("closeBlurRadius", profile.closeBlurRadius),
                    MazeDiagnosticsLog.JsonNumber("focusStart", profile.focusStart),
                    MazeDiagnosticsLog.JsonNumber("focusEnd", profile.EffectiveFocusEnd),
                    MazeDiagnosticsLog.JsonNumber("farBlurRadius", profile.farBlurRadius),
                    MazeDiagnosticsLog.JsonNumber("samples", profile.EffectiveSampleCount)));
        }

        private static void CleanupNullControllers()
        {
            for (var i = Controllers.Count - 1; i >= 0; i--)
            {
                if (Controllers[i] == null)
                {
                    Controllers.RemoveAt(i);
                }
            }
        }

        public void AddMazeProfilingFeatures(List<MazeProfilingFeatureHandle> features)
        {
            if (profile == null)
            {
                return;
            }

            features.Add(new MazeProfilingFeatureHandle(
                "environment.focus",
                "Focus",
                "Environment",
                () => IsRenderingEnabled ? $"depth {profile.RendersDepthBlur}, lens {profile.RendersLens}, {profile.quality}" : "off",
                enabled => profile.focusEnabled = enabled,
                () => profile.focusEnabled,
                state =>
                {
                    if (state is bool value)
                    {
                        profile.focusEnabled = value;
                    }
                },
                "off/modules"));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (profile == null || !profile.depthBlurEnabled)
            {
                return;
            }

            var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
            var drawn = false;
            foreach (var camera in cameras)
            {
                if (camera == null || camera.cameraType == CameraType.Preview)
                {
                    continue;
                }

                var controllerScene = gameObject.scene;
                var cameraScene = camera.gameObject.scene;
                if (controllerScene.IsValid() && cameraScene.IsValid() && controllerScene != cameraScene)
                {
                    continue;
                }

                DrawFocusGizmosForCamera(camera);
                drawn = true;
            }

            if (!drawn && Camera.main != null)
            {
                DrawFocusGizmosForCamera(Camera.main);
            }
        }

        private void DrawFocusGizmosForCamera(Camera camera)
        {
            var focusStart = Mathf.Max(0.01f, profile.focusStart);
            var focusEnd = profile.EffectiveFocusEnd;
            DrawFocusBand(camera, 0f, focusStart, profile.closeBlurRadius, new Color(1f, 0.42f, 0.16f, 0.82f), "Close Blur Band");
            DrawFocusBand(camera, focusStart, focusEnd, 0f, new Color(0.18f, 1f, 0.38f, 0.82f), "Focus Band");
            DrawFocusBand(camera, focusEnd, Mathf.Max(focusEnd + 0.01f, camera.farClipPlane), profile.farBlurRadius, new Color(0.28f, 0.58f, 1f, 0.82f), "Far Blur Band");
        }

        private static void DrawFocusBand(Camera camera, float startDistance, float endDistance, float blurRadius, Color color, string label)
        {
            var cameraTransform = camera.transform;
            var forward = cameraTransform.forward;
            var origin = cameraTransform.position;
            var start = origin + forward * Mathf.Max(0.01f, startDistance);
            var end = origin + forward * Mathf.Max(startDistance + 0.01f, endDistance);
            var middle = origin + forward * Mathf.Lerp(Mathf.Max(0.01f, startDistance), Mathf.Max(startDistance + 0.01f, endDistance), 0.5f);

            Handles.color = color;
            Handles.DrawAAPolyLine(2f, start, end);
            DrawCameraDistanceDisc(camera, start, color * new Color(1f, 1f, 1f, 0.35f));
            DrawCameraDistanceDisc(camera, end, color * new Color(1f, 1f, 1f, 0.35f));
            Handles.Label(middle, $"{label} {startDistance:0.#}-{endDistance:0.#} blur {blurRadius:0.#}");
        }

        private static void DrawCameraDistanceDisc(Camera camera, Vector3 center, Color color)
        {
            var cameraTransform = camera.transform;
            var distance = Vector3.Dot(center - cameraTransform.position, cameraTransform.forward);
            var radius = camera.orthographic
                ? camera.orthographicSize
                : Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Max(0.01f, distance);
            Handles.color = color;
            Handles.DrawWireDisc(center, cameraTransform.forward, Mathf.Max(0.01f, radius));
        }
#endif
    }
}
