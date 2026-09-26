using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maze
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MazeAOController : MonoBehaviour, IMazeProfilingFeatureProvider
    {
        private static readonly int AOParamsAId = Shader.PropertyToID("_MazeAO_ParamsA");
        private static readonly int AOParamsBId = Shader.PropertyToID("_MazeAO_ParamsB");
        private static readonly int AOParamsCId = Shader.PropertyToID("_MazeAO_ParamsC");

        private static readonly List<MazeAOController> Controllers = new();

        [SerializeField] private MazeAOProfile profile;

        public MazeAOProfile Profile => profile;
        public bool IsRenderingEnabled => isActiveAndEnabled && profile != null && profile.RendersAO;

        public static MazeAOController ResolveForCamera(Camera camera)
        {
            CleanupNullControllers();
            MazeAOController first = null;
            MazeAOController activeSceneMatch = null;
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
            var sampleCount = enabled ? profile.EffectiveSampleCount : 0;
            material.SetVector(AOParamsAId, new Vector4(
                enabled ? 1f : 0f,
                Mathf.Max(0f, profile.intensity),
                Mathf.Max(0.1f, profile.power),
                Mathf.Clamp01(profile.directLightingStrength)));
            material.SetVector(AOParamsBId, new Vector4(
                Mathf.Max(0f, profile.radiusPixels),
                Mathf.Max(0.001f, profile.radiusWorld),
                Mathf.Clamp01(profile.bias),
                Mathf.Max(0f, profile.thickness)));
            material.SetVector(AOParamsCId, new Vector4(
                Mathf.Max(0f, sampleCount),
                Mathf.Clamp01(profile.denoiseStrength),
                Mathf.Max(0f, profile.denoiseRadiusPixels),
                (float)profile.debugMode));
            material.SetVector("_MazeAO_ParamsD", new Vector4(
                Mathf.Min(profile.distanceFadeStart, profile.distanceFadeEnd - 0.001f),
                Mathf.Max(profile.distanceFadeEnd, profile.distanceFadeStart + 0.001f),
                Mathf.Max(0.01f, profile.depthRejection),
                0f));

            LogState(enabled, sampleCount);
        }

        private void LogState(bool enabled, int sampleCount)
        {
            if (!Application.isPlaying || profile == null)
            {
                return;
            }

            var state = $"{enabled}|{profile.quality}|{profile.intensity:0.###}|{profile.radiusPixels:0.###}|{profile.radiusWorld:0.###}|{sampleCount}|{profile.denoiseStrength:0.###}";
            MazeDiagnosticsLog.InfoOnChange(
                "graphics.ao.state:" + GetEntityId(),
                state,
                "Graphics.AO",
                "state",
                "AO material state applied",
                MazeDiagnosticsLog.JoinData(
                    MazeDiagnosticsLog.JsonBool("enabled", enabled),
                    MazeDiagnosticsLog.JsonString("quality", profile.quality.ToString()),
                    MazeDiagnosticsLog.JsonNumber("intensity", profile.intensity),
                    MazeDiagnosticsLog.JsonNumber("radiusPixels", profile.radiusPixels),
                    MazeDiagnosticsLog.JsonNumber("radiusWorld", profile.radiusWorld),
                    MazeDiagnosticsLog.JsonNumber("samples", sampleCount)));
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
                "environment.ao",
                "AO",
                "Environment",
                () => IsRenderingEnabled ? $"{profile.quality} samples {profile.EffectiveSampleCount}" : "off",
                enabled => profile.aoEnabled = enabled,
                () => profile.aoEnabled,
                state =>
                {
                    if (state is bool value)
                    {
                        profile.aoEnabled = value;
                    }
                },
                "off/quality"));
        }
    }
}
