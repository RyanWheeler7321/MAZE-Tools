using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maze
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MazeLightFXController : MonoBehaviour, IMazeProfilingFeatureProvider
    {
        private static readonly int ShaftColorId = Shader.PropertyToID("_MazeLightFX_ShaftColor");
        private static readonly int ShaftParamsAId = Shader.PropertyToID("_MazeLightFX_ShaftParamsA");
        private static readonly int ShaftParamsBId = Shader.PropertyToID("_MazeLightFX_ShaftParamsB");
        private static readonly int ShaftParamsCId = Shader.PropertyToID("_MazeLightFX_ShaftParamsC");
        private static readonly int SourceScreenPosId = Shader.PropertyToID("_MazeLightFX_SourceScreenPos");
        private static readonly int FlareColorId = Shader.PropertyToID("_MazeLightFX_FlareColor");
        private static readonly int FlareParamsAId = Shader.PropertyToID("_MazeLightFX_FlareParamsA");
        private static readonly int FlareParamsBId = Shader.PropertyToID("_MazeLightFX_FlareParamsB");
        private static readonly int FlareParamsCId = Shader.PropertyToID("_MazeLightFX_FlareParamsC");

        private static readonly List<MazeLightFXController> Controllers = new();

        [SerializeField] private MazeLightFXProfile profile;
        [SerializeField] private Transform lightShaftSource;

        public MazeLightFXProfile Profile => profile;
        public Transform LightShaftSource => lightShaftSource;
        public bool IsRenderingEnabled => isActiveAndEnabled && profile != null && profile.RendersLightFX;

        public static MazeLightFXController ResolveForCamera(Camera camera)
        {
            CleanupNullControllers();
            MazeLightFXController first = null;
            MazeLightFXController activeSceneMatch = null;
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
            var shaftSamples = enabled && profile.RendersLightShafts ? profile.EffectiveLightShaftSamples : 0;
            var shaftiness = Mathf.Clamp01(profile.lightShaftiness);
            var effectiveShaftBlur = Mathf.Max(0.001f, profile.lightShaftBlurRadius * Mathf.Lerp(1.25f, 0.42f, shaftiness));
            var effectiveShaftSharpness = Mathf.Max(0.25f, profile.lightShaftSharpness * Mathf.Lerp(0.75f, 3f, shaftiness));

            material.SetColor(ShaftColorId, profile.lightShaftColor.linear);
            material.SetVector(ShaftParamsAId, new Vector4(
                enabled ? Mathf.Clamp(profile.lightShaftIntensity, 0f, 2f) : 0f,
                Mathf.Clamp(profile.lightShaftDecay, 0.1f, 0.995f),
                Mathf.Max(0.01f, profile.lightShaftWeight),
                Mathf.Max(0.001f, profile.lightShaftExposure)));
            material.SetVector(ShaftParamsBId, new Vector4(
                Mathf.Max(0, shaftSamples),
                effectiveShaftBlur,
                Mathf.Clamp01(profile.lightShaftOcclusion),
                effectiveShaftSharpness));
            material.SetVector(ShaftParamsCId, new Vector4(
                shaftiness,
                Mathf.Clamp01(profile.lightShaftGranularity),
                Mathf.Max(0.001f, profile.lightShaftNoiseScale),
                profile.lightShaftDither ? 1f : 0f));
            var sourceScreenPosition = ComputeSourceScreenPosition(camera);
            material.SetVector(SourceScreenPosId, sourceScreenPosition);
            material.SetColor(FlareColorId, profile.sourceFlareColor.linear);
            material.SetVector(FlareParamsAId, new Vector4(
                enabled && profile.RendersSourceFlare ? Mathf.Clamp(profile.sourceFlareIntensity, 0f, 4f) : 0f,
                Mathf.Max(0.01f, profile.haloRadius),
                Mathf.Clamp(profile.haloIntensity, 0f, 4f),
                Mathf.Clamp(profile.starIntensity, 0f, 4f)));
            material.SetVector(FlareParamsBId, new Vector4(
                Mathf.Clamp(profile.starSpikeCount, 2, 12),
                Mathf.Max(0.01f, profile.starLength),
                profile.starRotation * Mathf.Deg2Rad,
                Mathf.Clamp01(profile.flareOcclusion)));
            material.SetVector(FlareParamsCId, new Vector4(
                Mathf.Clamp(profile.ghostIntensity, 0f, 2f),
                profile.EffectiveGhostCount,
                Mathf.Max(0.05f, profile.ghostSpacing),
                Mathf.Max(0f, profile.chromaticOffsetPixels)));

            LogState(enabled, shaftSamples);
        }

        private Vector4 ComputeSourceScreenPosition(Camera camera)
        {
            if (camera == null || profile == null)
            {
                return Vector4.zero;
            }

            if (profile.lightShaftSourceMode == MazeLightFXSourceMode.SourceTransform && lightShaftSource != null)
            {
                var sourcePoint = camera.WorldToViewportPoint(lightShaftSource.position);
                return new Vector4(sourcePoint.x, sourcePoint.y, sourcePoint.z > 0f ? 1f : 0f, 0f);
            }

            if (profile.lightShaftSourceMode == MazeLightFXSourceMode.ManualScreenPosition)
            {
                return new Vector4(profile.manualLightShaftViewportPosition.x, profile.manualLightShaftViewportPosition.y, 1f, 1f);
            }

            var context = MazeEnvironmentContext.Active;
            var sunDirection = context != null ? context.SunDirectionWS : new Vector3(0.3f, 0.8f, 0.2f).normalized;
            var screenPoint = camera.WorldToViewportPoint(camera.transform.position + sunDirection * 1000f);
            return new Vector4(screenPoint.x, screenPoint.y, screenPoint.z > 0f ? 1f : 0f, 0f);
        }

        private void LogState(bool enabled, int shaftSamples)
        {
            if (!Application.isPlaying || profile == null)
            {
                return;
            }

            var state = $"{enabled}|{profile.quality}|{profile.lightShaftIntensity:0.###}|{shaftSamples}|{profile.lightShaftSourceMode}|{profile.lightShaftiness:0.###}|{profile.lightShaftGranularity:0.###}|{profile.RendersSourceFlare}|{profile.sourceFlareIntensity:0.###}|{profile.EffectiveGhostCount}";
            MazeDiagnosticsLog.InfoOnChange(
                "graphics.lightfx.state:" + GetEntityId(),
                state,
                "Graphics.LightFX",
                "state",
                "lightfx material state applied",
                MazeDiagnosticsLog.JoinData(
                    MazeDiagnosticsLog.JsonBool("lightFX", enabled),
                    MazeDiagnosticsLog.JsonBool("lightShafts", profile.RendersLightShafts),
                    MazeDiagnosticsLog.JsonString("quality", profile.quality.ToString()),
                    MazeDiagnosticsLog.JsonString("sourceMode", profile.lightShaftSourceMode.ToString()),
                    MazeDiagnosticsLog.JsonNumber("intensity", profile.lightShaftIntensity),
                    MazeDiagnosticsLog.JsonNumber("samples", shaftSamples),
                    MazeDiagnosticsLog.JsonNumber("shaftiness", profile.lightShaftiness),
                    MazeDiagnosticsLog.JsonNumber("granularity", profile.lightShaftGranularity),
                    MazeDiagnosticsLog.JsonBool("sourceFlare", profile.RendersSourceFlare),
                    MazeDiagnosticsLog.JsonNumber("sourceFlareIntensity", profile.sourceFlareIntensity),
                    MazeDiagnosticsLog.JsonNumber("ghosts", profile.EffectiveGhostCount)));
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
                "environment.lightfx.light_shafts",
                "LightFX Shafts",
                "Environment",
                () => IsRenderingEnabled ? $"on samples {profile.EffectiveLightShaftSamples}" : "off",
                enabled => profile.lightShaftsEnabled = enabled,
                () => profile.lightShaftsEnabled,
                state =>
                {
                    if (state is bool value)
                    {
                        profile.lightShaftsEnabled = value;
                    }
                },
                "off/sample-count"));
        }
    }
}
