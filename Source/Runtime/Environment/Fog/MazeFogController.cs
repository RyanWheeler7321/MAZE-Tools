using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maze
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MazeFogController : MonoBehaviour, IMazeProfilingFeatureProvider, IMazeRenderDebugProvider
    {
        private static readonly int FogParamsAId = Shader.PropertyToID("_MazeFog_ParamsA");
        private static readonly int FogParamsBId = Shader.PropertyToID("_MazeFog_ParamsB");
        private static readonly int FogParamsCId = Shader.PropertyToID("_MazeFog_ParamsC");
        private static readonly int FogLocalParamsId = Shader.PropertyToID("_MazeFog_LocalParams");
        private static readonly int FogHeightParamsId = Shader.PropertyToID("_MazeFog_HeightParams");
        private static readonly int FogGroundSkyParamsId = Shader.PropertyToID("_MazeFog_GroundSkyParams");
        private static readonly int FogNoiseParamsId = Shader.PropertyToID("_MazeFog_NoiseParams");
        private static readonly int FogFarNoiseParamsId = Shader.PropertyToID("_MazeFog_FarNoiseParams");
        private static readonly int FogNoiseDistanceParamsId = Shader.PropertyToID("_MazeFog_NoiseDistanceParams");
        private static readonly int FogLightingParamsId = Shader.PropertyToID("_MazeFog_LightingParams");
        private static readonly int FogHorizonParamsId = Shader.PropertyToID("_MazeFog_HorizonParams");
        private static readonly int FogFallbackSkyColorId = Shader.PropertyToID("_MazeFog_FallbackSkyColor");
        private static readonly int FogBandCountId = Shader.PropertyToID("_MazeFog_BandCount");
        private static readonly int FogBandsAId = Shader.PropertyToID("_MazeFog_BandsA");
        private static readonly int FogBandsBId = Shader.PropertyToID("_MazeFog_BandsB");
        private static readonly int FogBandColorsId = Shader.PropertyToID("_MazeFog_BandColors");
        private static readonly int FogDebugModeId = Shader.PropertyToID("_MazeFog_DebugView");
        private const float FinalColorMaterialMode = 15f;
        private static readonly List<MazeFogController> Controllers = new();

        [SerializeField] private MazeFogProfile profile;

        private readonly Vector4[] bandParamsA = new Vector4[MazeFogProfile.MaxFogBands];
        private readonly Vector4[] bandParamsB = new Vector4[MazeFogProfile.MaxFogBands];
        private readonly Vector4[] bandColors = new Vector4[MazeFogProfile.MaxFogBands];
        private readonly List<MazeFogBand> activeBands = new();
        [System.NonSerialized] private float renderDebugMode;

        public MazeFogProfile Profile => profile;
        public bool IsRenderingEnabled => isActiveAndEnabled && profile != null && profile.RendersFog;
        // Logical mode zero means no diagnostic is selected. The material still uses the
        // explicit final-color branch so clearing a diagnostic cannot leave Unity's visible
        // GameView presenting the previous render-pass value.
        public float EffectiveMaterialDebugValue => renderDebugMode > 0.5f ? renderDebugMode : FinalColorMaterialMode;

        public static MazeFogController ResolveForCamera(Camera camera)
        {
            CleanupNullControllers();
            MazeFogController first = null;
            MazeFogController activeSceneMatch = null;
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
            ClearRenderDebugView();
            if (!Controllers.Contains(this))
            {
                Controllers.Add(this);
            }
        }

        private void OnDisable()
        {
            Controllers.Remove(this);
            ClearRenderDebugView();
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

        public void ApplyToMaterial(Material material, Camera camera, bool isSceneViewCamera, bool fogSceneViewAllowed = true)
        {
            if (material == null || profile == null)
            {
                return;
            }

            var cameraAllowed = AllowsCamera(camera, isSceneViewCamera);
            var fogEnabled = IsRenderingEnabled && profile.RendersFog && cameraAllowed && (!isSceneViewCamera || fogSceneViewAllowed);
            var stepCount = fogEnabled ? profile.EffectiveStepCount : 0;
            var intensity = Mathf.Clamp01(profile.fogIntensity);
            var hasConfiguredBands = profile.HasConfiguredBands;

            material.SetFloat(FogDebugModeId, EffectiveMaterialDebugValue);
            material.SetVector(FogParamsAId, new Vector4(
                fogEnabled ? 1f : 0f,
                Mathf.Max(1f, profile.maxDistance),
                Mathf.Clamp01(profile.skyFogAmount),
                Mathf.Max(0f, profile.bandTransitionDistance)));
            material.SetVector(FogParamsBId, new Vector4(
                Mathf.Clamp01(profile.maxOpacity),
                Mathf.Clamp01(profile.skyBlend),
                Mathf.Max(0, stepCount),
                (float)profile.style));
            material.SetVector(FogParamsCId, new Vector4(
                fogEnabled ? intensity : 0f,
                Mathf.Clamp01(profile.closeFogAmount),
                hasConfiguredBands ? 1f : 0f,
                0f));
            material.SetVector(FogLocalParamsId, new Vector4(
                Mathf.Clamp01(profile.wispiness),
                Mathf.Clamp01(profile.veryCloseFogAmount),
                Mathf.Clamp01(profile.closeFogGranularity),
                Mathf.Clamp01(profile.wispinessGranularity)));
            material.SetVector(FogHeightParamsId, new Vector4(
                profile.baseHeight,
                Mathf.Max(0.001f, profile.heightThickness),
                Mathf.Max(0.001f, profile.heightFalloff),
                Mathf.Max(0f, profile.groundBias)));
            material.SetVector(FogGroundSkyParamsId, new Vector4(
                profile.groundSkyHeight,
                Mathf.Max(0.1f, profile.groundSkyTransition),
                Mathf.Max(0f, profile.groundThickness),
                Mathf.Max(0f, profile.skyThickness)));
            material.SetVector(FogNoiseParamsId, new Vector4(
                1f / Mathf.Max(0.1f, profile.nearNoiseSize),
                Mathf.Clamp01(profile.nearNoiseStrength),
                Mathf.Max(0.25f, profile.nearNoiseContrast),
                profile.noiseScrollSpeed));
            material.SetVector(FogFarNoiseParamsId, new Vector4(
                1f / Mathf.Max(0.1f, profile.farNoiseSize),
                Mathf.Clamp01(profile.farNoiseStrength),
                Mathf.Max(0.25f, profile.farNoiseContrast),
                Mathf.Max(0f, profile.farNoiseStartDistance)));
            material.SetVector(FogNoiseDistanceParamsId, new Vector4(
                Mathf.Max(profile.farNoiseStartDistance + 0.001f, profile.farNoiseFullDistance),
                0f,
                0f,
                0f));
            material.SetVector(FogLightingParamsId, new Vector4(
                Mathf.Clamp01(profile.anisotropy),
                Mathf.Max(0f, profile.lightResponse),
                Mathf.Clamp01(profile.ambientResponse),
                Mathf.Clamp01(profile.cloudShadowStrength)));
            material.SetVector(FogHorizonParamsId, new Vector4(
                profile.HasHorizonHaze ? Mathf.Clamp01(profile.horizonHazeStrength) : 0f,
                Mathf.Max(0f, profile.horizonHazeStartDistance),
                Mathf.Max(profile.horizonHazeStartDistance + 0.001f, profile.horizonHazeFullDistance),
                Mathf.Clamp(profile.horizonHazeVerticalSize, 0.001f, 1f)));
            material.SetColor(FogFallbackSkyColorId, ResolveFallbackSkyColor(camera));
            ApplyBandsToMaterial(material, fogEnabled);

            LogState(fogEnabled, stepCount, hasConfiguredBands, camera);
        }

        private static Color ResolveFallbackSkyColor(Camera camera)
        {
            // Fog can be used without the custom Atmosphere pass, including solid-color camera skies.
            if (camera != null && camera.clearFlags == CameraClearFlags.SolidColor)
            {
                return camera.backgroundColor.linear;
            }

            return RenderSettings.ambientSkyColor.linear;
        }

        private void ApplyBandsToMaterial(Material material, bool fogEnabled)
        {
            activeBands.Clear();
            if (fogEnabled && profile.bands != null)
            {
                var limit = Mathf.Min(profile.bands.Count, MazeFogProfile.MaxFogBands);
                for (var i = 0; i < limit; i++)
                {
                    var band = profile.bands[i];
                    var fixedBoundaryBand = i == 0 || i == limit - 1;
                    if (band == null || (!fixedBoundaryBand && !band.enabled))
                    {
                        continue;
                    }

                    activeBands.Add(band);
                }
            }

            var count = 0;
            var previousStart = 0f;
            var maxDistance = Mathf.Max(1f, profile.maxDistance);
            for (var i = 0; i < activeBands.Count; i++)
            {
                var band = activeBands[i];
                var start = count == 0 ? 0f : Mathf.Clamp(band.startDistance, previousStart + 0.001f, maxDistance);
                var density = 0.05f * Mathf.Clamp01(band.density) * Mathf.Clamp01(band.density);
                bandParamsA[count] = new Vector4(
                    density,
                    start,
                    0f,
                    0f);
                bandParamsB[count] = Vector4.zero;
                bandColors[count] = band.color.linear;
                previousStart = start;
                count++;
            }

            for (var i = count; i < MazeFogProfile.MaxFogBands; i++)
            {
                bandParamsA[i] = Vector4.zero;
                bandParamsB[i] = Vector4.zero;
                bandColors[i] = Vector4.zero;
            }

            material.SetFloat(FogBandCountId, count);
            material.SetVectorArray(FogBandsAId, bandParamsA);
            material.SetVectorArray(FogBandsBId, bandParamsB);
            material.SetVectorArray(FogBandColorsId, bandColors);
        }

        private void LogState(bool fogEnabled, int stepCount, bool hasConfiguredBands, Camera camera)
        {
            if (!Application.isPlaying || profile == null)
            {
                return;
            }

            var atmosphereSky = MazeAtmosphereController.Active != null && MazeAtmosphereController.Active.IsRenderingEnabled;
            var fallbackSky = ResolveFallbackSkyColor(camera);
            var state = $"{fogEnabled}|{profile.quality}|{profile.fogIntensity:0.###}|wisp={profile.wispiness:0.###}|wispGrain={profile.wispinessGranularity:0.###}|close={profile.closeFogAmount:0.###}|veryClose={profile.veryCloseFogAmount:0.###}|grain={profile.closeFogGranularity:0.###}|skyMix={profile.skyBlend:0.###}|skySource={(atmosphereSky ? "atmosphere" : "fallback")}|height={profile.groundSkyHeight:0.###}|blend={profile.groundSkyTransition:0.###}|ground={profile.groundThickness:0.###}|sky={profile.skyThickness:0.###}|{profile.ActiveBandCount}|{hasConfiguredBands}|{profile.HasHorizonHaze}|{profile.horizonHazeStrength:0.###}|{stepCount}|{renderDebugMode:0.###}|{EffectiveMaterialDebugValue:0.###}";
            MazeDiagnosticsLog.InfoOnChange(
                "graphics.fog.state:" + GetEntityId(),
                state,
                "Graphics.Fog",
                "state",
                "fog material state applied",
                MazeDiagnosticsLog.JoinData(
                    MazeDiagnosticsLog.JsonBool("fog", fogEnabled),
                    MazeDiagnosticsLog.JsonString("quality", profile.quality.ToString()),
                    MazeDiagnosticsLog.JsonNumber("intensity", profile.fogIntensity),
                    MazeDiagnosticsLog.JsonNumber("wispiness", profile.wispiness),
                    MazeDiagnosticsLog.JsonNumber("wispinessGranularity", profile.wispinessGranularity),
                    MazeDiagnosticsLog.JsonNumber("closeFog", profile.closeFogAmount),
                    MazeDiagnosticsLog.JsonNumber("veryCloseFog", profile.veryCloseFogAmount),
                    MazeDiagnosticsLog.JsonNumber("closeGranularity", profile.closeFogGranularity),
                    MazeDiagnosticsLog.JsonNumber("skyMix", profile.skyBlend),
                    MazeDiagnosticsLog.JsonString("skyTintSource", atmosphereSky ? "atmosphere" : "fallback"),
                    MazeDiagnosticsLog.JsonString("fallbackSkyColor", $"{fallbackSky.r:0.###},{fallbackSky.g:0.###},{fallbackSky.b:0.###}"),
                    MazeDiagnosticsLog.JsonNumber("groundSkyHeight", profile.groundSkyHeight),
                    MazeDiagnosticsLog.JsonNumber("groundSkyTransition", profile.groundSkyTransition),
                    MazeDiagnosticsLog.JsonNumber("groundThickness", profile.groundThickness),
                    MazeDiagnosticsLog.JsonNumber("skyThickness", profile.skyThickness),
                    MazeDiagnosticsLog.JsonNumber("activeBands", profile.ActiveBandCount),
                    MazeDiagnosticsLog.JsonBool("configuredBands", hasConfiguredBands),
                    MazeDiagnosticsLog.JsonBool("horizonHaze", profile.HasHorizonHaze),
                    MazeDiagnosticsLog.JsonNumber("horizonStrength", profile.horizonHazeStrength),
                    MazeDiagnosticsLog.JsonNumber("steps", stepCount),
                    MazeDiagnosticsLog.JsonNumber("debugMode", renderDebugMode),
                    MazeDiagnosticsLog.JsonNumber("materialDebug", EffectiveMaterialDebugValue)));
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
                "environment.fog",
                "Fog",
                "Environment",
                () => IsRenderingEnabled ? $"{profile.quality} steps {profile.EffectiveStepCount}" : "off",
                enabled => profile.fogEnabled = enabled,
                () => profile.fogEnabled,
                state =>
                {
                    if (state is bool value)
                    {
                        profile.fogEnabled = value;
                    }
                },
                "off/quality"));

        }

        public string RenderDebugProviderId => "fog";
        public string RenderDebugDisplayName => "Fog";
        public bool IsRenderDebugAvailable => isActiveAndEnabled && profile != null;

        public void AddRenderDebugViews(List<MazeRenderDebugView> views)
        {
            views.Add(new MazeRenderDebugView(RenderDebugProviderId, "fog_sky_mask", "fog sky mask", "fog_sky_mask.png", true));
            views.Add(new MazeRenderDebugView(RenderDebugProviderId, "fog_distance", "fog radial distance", "fog_distance.png", true));
            views.Add(new MazeRenderDebugView(RenderDebugProviderId, "fog_view_direction", "fog view direction", "fog_view_direction.png", true));
            views.Add(new MazeRenderDebugView(RenderDebugProviderId, "fog_world_height", "fog reconstructed world height", "fog_world_height.png", true));
            views.Add(new MazeRenderDebugView(RenderDebugProviderId, "fog_band_membership", "fog integrated band contribution", "fog_band_membership.png", true));
            views.Add(new MazeRenderDebugView(RenderDebugProviderId, "fog_height_density", "fog height density", "fog_height_density.png", true));
            views.Add(new MazeRenderDebugView(RenderDebugProviderId, "fog_final_amount", "fog final amount", "fog_final_amount.png", true));
            views.Add(new MazeRenderDebugView(RenderDebugProviderId, "fog_source_color", "fog source color", "fog_source_color.png", true));
            views.Add(new MazeRenderDebugView(RenderDebugProviderId, "fog_authored_color", "fog authored color", "fog_authored_color.png", true));
            views.Add(new MazeRenderDebugView(RenderDebugProviderId, "fog_volume_color", "fog volume color", "fog_volume_color.png", true));
            views.Add(new MazeRenderDebugView(RenderDebugProviderId, "fog_horizon_color", "fog horizon color", "fog_horizon_color.png", true));
            views.Add(new MazeRenderDebugView(RenderDebugProviderId, "fog_final_color", "fog final color", "fog_final_color.png", true));
        }

        public void AddRenderDebugProbeChannels(List<MazeRenderDebugProbeChannel> channels)
        {
            channels.Add(new MazeRenderDebugProbeChannel(RenderDebugProviderId, "fog_sky_mask", "R", "sky/no-depth pixel"));
            channels.Add(new MazeRenderDebugProbeChannel(RenderDebugProviderId, "fog_sky_mask", "G", "geometry/depth pixel"));
            channels.Add(new MazeRenderDebugProbeChannel(RenderDebugProviderId, "fog_distance", "R", "radial camera distance normalized by Max Distance"));
            channels.Add(new MazeRenderDebugProbeChannel(RenderDebugProviderId, "fog_view_direction", "RGB", "world view direction remapped from -1..1"));
            channels.Add(new MazeRenderDebugProbeChannel(RenderDebugProviderId, "fog_world_height", "R", "reconstructed endpoint world height"));
            channels.Add(new MazeRenderDebugProbeChannel(RenderDebugProviderId, "fog_band_membership", "RGB", "distance bands accumulated along the visible ray"));
            channels.Add(new MazeRenderDebugProbeChannel(RenderDebugProviderId, "fog_height_density", "R", "height density at the assessed endpoint"));
            channels.Add(new MazeRenderDebugProbeChannel(RenderDebugProviderId, "fog_final_amount", "R", "combined volumetric and analytic fog amount"));
            channels.Add(new MazeRenderDebugProbeChannel(RenderDebugProviderId, "fog_source_color", "RGB", "input color sampled by the fog pass"));
            channels.Add(new MazeRenderDebugProbeChannel(RenderDebugProviderId, "fog_authored_color", "RGB", "nearest authored fog band color"));
            channels.Add(new MazeRenderDebugProbeChannel(RenderDebugProviderId, "fog_volume_color", "RGB", "composed color after volume fog"));
            channels.Add(new MazeRenderDebugProbeChannel(RenderDebugProviderId, "fog_horizon_color", "RGB", "composed color after horizon haze"));
            channels.Add(new MazeRenderDebugProbeChannel(RenderDebugProviderId, "fog_final_color", "RGB", "final composed fog color"));
        }

        public object CaptureRenderDebugState() => renderDebugMode;

        public void RestoreRenderDebugState(object state)
        {
            SetRenderDebugMode(state is float mode ? mode : 0f);
        }

        public void SetRenderDebugView(string viewId)
        {
            SetRenderDebugMode(viewId switch
            {
                "fog_sky_mask" => 1f,
                "fog_distance" => 2f,
                "fog_view_direction" => 3f,
                "fog_world_height" => 4f,
                "fog_band_membership" => 5f,
                "fog_height_density" => 6f,
                "fog_final_amount" => 7f,
                "fog_source_color" => 11f,
                "fog_authored_color" => 12f,
                "fog_volume_color" => 13f,
                "fog_horizon_color" => 14f,
                "fog_final_color" => 15f,
                _ => 0f,
            });
        }

        public void ClearRenderDebugView() => SetRenderDebugMode(0f);

        private void SetRenderDebugMode(float mode)
        {
            renderDebugMode = mode;
        }

        public string BuildRenderDebugStateJson()
        {
            if (profile == null)
            {
                return "{\"available\":false}";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"available\":true,\"enabled\":{0},\"intensity\":{1:0.###},\"closeFog\":{2:0.###},\"maxDistance\":{3:0.###},\"maxOpacity\":{4:0.###},\"activeBands\":{5},\"horizonHaze\":{6},\"groundSkyHeight\":{7:0.###},\"groundSkyTransition\":{8:0.###},\"groundThickness\":{9:0.###},\"skyThickness\":{10:0.###},\"debugMode\":{11:0.###},\"materialDebug\":{12:0.###}}}",
                IsRenderingEnabled ? "true" : "false",
                profile.fogIntensity,
                profile.closeFogAmount,
                profile.maxDistance,
                profile.maxOpacity,
                profile.ActiveBandCount,
                profile.HasHorizonHaze ? "true" : "false",
                profile.groundSkyHeight,
                profile.groundSkyTransition,
                profile.groundThickness,
                profile.skyThickness,
                renderDebugMode,
                EffectiveMaterialDebugValue);
        }
    }
}
