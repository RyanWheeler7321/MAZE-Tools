using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maze
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MazeToneController : MonoBehaviour, IMazeProfilingFeatureProvider
    {
        private static readonly int ParamsAId = Shader.PropertyToID("_MazeTone_ParamsA");
        private static readonly int ParamsBId = Shader.PropertyToID("_MazeTone_ParamsB");
        private static readonly int ParamsCId = Shader.PropertyToID("_MazeTone_ParamsC");
        private static readonly int ParamsDId = Shader.PropertyToID("_MazeTone_ParamsD");
        private static readonly List<MazeToneController> Controllers = new();

        [SerializeField] private MazeToneProfile profile;

        private string lastLoggedState;

        public MazeToneProfile Profile => profile;
        public bool IsRenderingEnabled => isActiveAndEnabled && profile != null && profile.RendersTone;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetControllerCache()
        {
            Controllers.Clear();
        }

        public static MazeToneController ResolveForCamera(Camera camera)
        {
            CleanupNullControllers();
            if (Controllers.Count == 0)
            {
                RegisterLoadedControllers();
            }

            MazeToneController first = null;
            MazeToneController activeSceneMatch = null;
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
            var mode = enabled ? profile.mode : MazeToneMode.None;
            var strength = enabled ? Mathf.Clamp01(profile.strength) : 0f;
            var exposure = enabled ? Mathf.Max(0f, profile.ExposureMultiplier) : 1f;
            var dither = enabled && profile.UsesDither ? Mathf.Clamp(profile.ditherStrength, 0f, 4f) : 0f;

            material.SetVector(ParamsAId, new Vector4((float)mode, strength, exposure, dither));
            material.SetVector(ParamsBId, new Vector4(
                Mathf.Clamp(profile.neutralContrast, 0.05f, 8f),
                Mathf.Clamp(profile.neutralWhitePoint, 0.25f, 64f),
                Mathf.Clamp(profile.neutralWhiteClip, 0.05f, 8f),
                Mathf.Clamp(profile.acesContrast, 0.05f, 8f)));
            material.SetVector(ParamsCId, new Vector4(
                Mathf.Clamp(profile.acesSaturation, 0f, 4f),
                Mathf.Clamp(profile.reinhardWhitePoint, 0.25f, 64f),
                Mathf.Clamp(profile.gtMaxBrightness, 0.25f, 8f),
                Mathf.Clamp(profile.gtContrast, 0.05f, 8f)));
            material.SetVector(ParamsDId, new Vector4(
                Mathf.Clamp(profile.gtLinearStart, 0.001f, 4f),
                Mathf.Clamp(profile.gtLinearLength, 0.001f, 4f),
                Mathf.Clamp(profile.gtBlackTightness, 0.05f, 8f),
                (float)profile.quality));

            LogState(enabled, mode, strength, exposure, dither);
        }

        private void LogState(bool enabled, MazeToneMode mode, float strength, float exposure, float dither)
        {
            if (!Application.isPlaying || profile == null)
            {
                return;
            }

            var state = $"{enabled}|{mode}|{profile.quality}|{strength:0.###}|{profile.exposureEV:0.###}|{dither:0.###}|{profile.neutralContrast:0.###}|{profile.acesContrast:0.###}|{profile.acesSaturation:0.###}";
            if (state == lastLoggedState)
            {
                return;
            }

            lastLoggedState = state;
            MazeDiagnosticsLog.InfoOnChange(
                "graphics.tone.state:" + GetEntityId(),
                state,
                "Graphics.Tone",
                "state",
                "tone material state applied",
                MazeDiagnosticsLog.JoinData(
                    MazeDiagnosticsLog.JsonBool("enabled", enabled),
                    MazeDiagnosticsLog.JsonString("mode", mode.ToString()),
                    MazeDiagnosticsLog.JsonString("quality", profile.quality.ToString()),
                    MazeDiagnosticsLog.JsonNumber("strength", strength),
                    MazeDiagnosticsLog.JsonNumber("exposure", exposure),
                    MazeDiagnosticsLog.JsonNumber("dither", dither)));
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

        private static void RegisterLoadedControllers()
        {
            var loaded = Object.FindObjectsByType<MazeToneController>(FindObjectsInactive.Exclude);
            for (var i = 0; i < loaded.Length; i++)
            {
                var controller = loaded[i];
                if (controller != null && !Controllers.Contains(controller))
                {
                    Controllers.Add(controller);
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
                "rendering.tone",
                "Tone",
                "Rendering",
                () => IsRenderingEnabled ? $"{profile.mode} {profile.strength:0.##}x" : "off",
                enabled => profile.toneEnabled = enabled,
                () => profile.toneEnabled,
                state =>
                {
                    if (state is bool value)
                    {
                        profile.toneEnabled = value;
                    }
                },
                "off/mode"));
        }
    }
}
