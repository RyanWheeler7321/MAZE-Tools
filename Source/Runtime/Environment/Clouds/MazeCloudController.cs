using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maze
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("MAZE/Environment/Clouds")]
    public sealed class MazeCloudController : MonoBehaviour, IMazeProfilingFeatureProvider, IMazeRenderDebugProvider
    {
        private static readonly int CloudParamsAId = Shader.PropertyToID("_MazeAtmosphere_CloudParamsA");
        private static readonly int CloudParamsBId = Shader.PropertyToID("_MazeAtmosphere_CloudParamsB");
        private static readonly int CloudLightingParamsId = Shader.PropertyToID("_MazeAtmosphere_CloudLightingParams");
        private static readonly int CloudAmbientColorId = Shader.PropertyToID("_MazeAtmosphere_CloudAmbientColor");
        private static readonly int CloudLightColorId = Shader.PropertyToID("_MazeAtmosphere_CloudLightColor");
        private static readonly int CloudShadowParamsId = Shader.PropertyToID("_MazeAtmosphere_CloudShadowParams");
        private static readonly int CloudShadowExtraId = Shader.PropertyToID("_MazeAtmosphere_CloudShadowExtra");
        private static readonly int CloudQualityParamsId = Shader.PropertyToID("_MazeAtmosphere_CloudQualityParams");
        private static readonly int CloudStyleParamsId = Shader.PropertyToID("_MazeAtmosphere_CloudStyleParams");
        private static readonly int LowerCloudParamsId = Shader.PropertyToID("_MazeAtmosphere_LowerCloudParams");
        private static readonly int CloudDebugModeId = Shader.PropertyToID("_MazeRenderDebug_CloudMode");
        private static readonly int FakeShadowTextureId = Shader.PropertyToID("_MazeCloud_FakeShadowTex");
        private static readonly int FakeShadowParamsAId = Shader.PropertyToID("_MazeCloud_FakeShadowParamsA");
        private static readonly int FakeShadowParamsBId = Shader.PropertyToID("_MazeCloud_FakeShadowParamsB");
        private static readonly int FakeShadowTintId = Shader.PropertyToID("_MazeCloud_FakeShadowTint");
        private static readonly int FakeShadowWindId = Shader.PropertyToID("_MazeCloud_FakeShadowWind");
        private static readonly int EnvironmentWindDirectionId = Shader.PropertyToID("_MazeEnv_WindDirection");
        private static readonly int EnvironmentWindSpeedId = Shader.PropertyToID("_MazeEnv_WindSpeed");

        private static readonly int CardAtlasId = Shader.PropertyToID("_MazeCloudCards_Atlas");
        private static readonly int CardWindId = Shader.PropertyToID("_MazeCloudCards_Wind");
        private static readonly int CardRevealId = Shader.PropertyToID("_MazeCloudCards_Reveal");
        private static readonly int CardLookId = Shader.PropertyToID("_MazeCloudCards_Look");
        private static readonly int CardOutputId = Shader.PropertyToID("_MazeCloudCards_Output");
        private static readonly int CardShadowTintId = Shader.PropertyToID("_MazeCloudCards_ShadowTint");
        private static readonly int CardBodyTintId = Shader.PropertyToID("_MazeCloudCards_BodyTint");
        private static readonly int CardHighlightTintId = Shader.PropertyToID("_MazeCloudCards_HighlightTint");
        private static readonly int CardModeId = Shader.PropertyToID("_MazeCloudCards_Mode");
        private static readonly int CardFadeId = Shader.PropertyToID("_MazeCloudCards_Fade");

        private static readonly List<MazeCloudController> Controllers = new();

        [SerializeField] private MazeCloudProfile profile;
        [System.NonSerialized] private float renderDebugMode;

        public MazeCloudProfile Profile => profile;
        public bool IsRenderingEnabled => isActiveAndEnabled && profile != null && (profile.RendersAnyCards || profile.RendersFakeShadows || profile.RendersVolumetric);
        public bool HasCards => IsRenderingEnabled && profile.RendersAnyCards;
        public bool HasDistantCards => HasCards && profile.distantCards.enabled && profile.generatedDistantMesh != null;
        public bool HasOverheadCards => HasCards && profile.overheadCards.enabled && profile.HasOverheadMesh;
        public bool HasFakeShadows => IsRenderingEnabled && profile.RendersFakeShadows;
        public bool HasVolumetric => IsRenderingEnabled && profile.RendersVolumetric;

        public string RenderDebugProviderId => "clouds";
        public string RenderDebugDisplayName => "Clouds";
        public bool IsRenderDebugAvailable => profile != null && isActiveAndEnabled;

        public static MazeCloudController ResolveForCamera(Camera camera)
        {
            CleanupNullControllers();
            MazeCloudController first = null;
            MazeCloudController activeSceneMatch = null;
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

            ClearRenderDebugView();
        }

        private void OnDisable()
        {
            Controllers.Remove(this);
            ClearRenderDebugView();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled && !Controllers.Contains(this))
            {
                Controllers.Add(this);
            }
        }

        public void ApplyVolumeAndShadowToMaterial(Material material, bool allowVolume, bool allowFakeShadows)
        {
            if (material == null || profile == null)
            {
                return;
            }

            var volume = profile.volumetric;
            var volumeEnabled = HasVolumetric && allowVolume;
            var steps = volume.performanceMode == MazeCloudPerformanceMode.Performance
                ? Mathf.Clamp(volume.performanceStepCount, 8, 48)
                : (float)volume.quality;
            var performance = volume.performanceMode == MazeCloudPerformanceMode.Performance;
            material.SetVector(CloudParamsAId, new Vector4(volumeEnabled ? 1f : 0f, steps, Mathf.Clamp01(volume.coverage), volume.density));
            material.SetVector(CloudParamsBId, new Vector4(volume.primaryScale, volume.detailScale, volume.baseHeight, volume.thickness));
            material.SetVector(CloudLightingParamsId, new Vector4(volume.detailStrength, volume.ambientStrength, volume.lightStrength, volume.forwardScattering));
            material.SetColor(CloudAmbientColorId, volume.ambientColor.linear);
            material.SetColor(CloudLightColorId, volume.lightColor.linear);

            var volumeShadow = volume.shadows;
            var hasVolumeShadow = volumeEnabled && volumeShadow.enabled && volumeShadow.strength > 0.001f;
            material.SetVector(CloudShadowParamsId, new Vector4(hasVolumeShadow ? volumeShadow.strength : 0f, volumeShadow.scale, volumeShadow.speed, volumeShadow.softness));
            material.SetVector(CloudShadowExtraId, new Vector4(volumeShadow.contrast, volume.speed, volume.ditherStrength, volumeShadow.usePerformanceSampling ? 1f : 0f));
            material.SetVector(CloudQualityParamsId, new Vector4(performance ? 1f : 0f, steps, performance && volume.performanceCheapLighting ? 1f : 0f, volumeShadow.usePerformanceSampling ? 1f : 0f));
            material.SetVector(CloudStyleParamsId, new Vector4(volume.granularityStrength, volume.granularityScale, volume.billowStrength, volume.billowScale));
            material.SetVector(LowerCloudParamsId, Vector4.zero);
            material.SetFloat(CloudDebugModeId, renderDebugMode);
            var volumeWind = volume.windDirection.sqrMagnitude > 0.0001f ? volume.windDirection.normalized : Vector2.right;
            material.SetVector(EnvironmentWindDirectionId, new Vector4(volumeWind.x, volumeWind.y, 0f, 0f));
            material.SetFloat(EnvironmentWindSpeedId, volume.windSpeed);

            var fake = profile.fakeShadows;
            var fakeEnabled = HasFakeShadows && allowFakeShadows;
            material.SetTexture(FakeShadowTextureId, fakeEnabled ? fake.coverageTexture : Texture2D.whiteTexture);
            material.SetVector(FakeShadowParamsAId, new Vector4(fakeEnabled ? fake.strength * fake.cloudiness : 0f, Mathf.Max(1f, fake.worldSize), fake.cloudAltitude, fake.softness));
            material.SetVector(FakeShadowParamsBId, new Vector4(fake.contrast, fake.threshold, 0f, 0f));
            material.SetColor(FakeShadowTintId, fake.tint.linear);
            var fakeWind = fake.windDirection.sqrMagnitude > 0.0001f ? fake.windDirection.normalized : Vector2.right;
            material.SetVector(FakeShadowWindId, new Vector4(fakeWind.x, fakeWind.y, fake.windSpeed, fake.seed));
        }

        public void ApplyCardsToPropertyBlock(MaterialPropertyBlock block, bool overhead)
        {
            if (block == null || profile == null)
            {
                return;
            }

            var movement = overhead ? profile.overheadCards.movement : profile.distantCards.movement;
            var look = overhead ? profile.overheadCards.look : profile.distantCards.look;
            var cloudiness = overhead ? profile.overheadCards.cloudiness : profile.distantCards.cloudiness;
            var count = overhead ? profile.overheadCards.count : profile.distantCards.count;
            var direction = movement.direction.sqrMagnitude > 0.0001f ? movement.direction.normalized : Vector2.right;
            block.SetTexture(CardAtlasId, profile.generatedCardAtlas != null ? profile.generatedCardAtlas : Texture2D.whiteTexture);
            block.SetVector(CardWindId, new Vector4(direction.x, direction.y, movement.speed, movement.uniformity));
            block.SetVector(CardRevealId, new Vector4(Mathf.Clamp(count, 0, MazeCloudProfile.MaxCardCount) / (float)MazeCloudProfile.MaxCardCount, cloudiness, look.opacity, 0f));
            block.SetVector(CardLookId, new Vector4(look.exposure, look.sourceColorInfluence, look.sunResponse, look.ambientResponse));
            block.SetVector(CardOutputId, new Vector4(look.brightness, Mathf.Max(0.05f, look.highlightClamp), 0f, 0f));
            block.SetColor(CardShadowTintId, look.shadowTint.linear);
            block.SetColor(CardBodyTintId, look.bodyTint.linear);
            block.SetColor(CardHighlightTintId, look.highlightTint.linear);
            block.SetFloat(CardModeId, overhead ? 1f : 0f);
            block.SetVector(CardFadeId, overhead
                ? new Vector4(profile.overheadCards.altitudeFadeDistance, 0f, 0f, 0f)
                : new Vector4(profile.distantCards.nearFadeStart, profile.distantCards.nearFadeEnd, profile.distantCards.horizonFade, 0f));
        }

        public void SetPerformanceCloudMode(bool performance)
        {
            if (profile != null)
            {
                profile.volumetric.performanceMode = performance ? MazeCloudPerformanceMode.Performance : MazeCloudPerformanceMode.HighestFidelity;
            }
        }

        public void SetPerformanceCloudShadows(bool performance)
        {
            if (profile != null)
            {
                profile.volumetric.shadows.usePerformanceSampling = performance;
            }
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

            features.Add(new MazeProfilingFeatureHandle("environment.cloud_distant_cards", "Distant Cloud Cards", "Environment", () => HasDistantCards ? $"on {profile.distantCards.count}" : "off", value => profile.distantCards.enabled = value, () => profile.distantCards.enabled, state => { if (state is bool value) profile.distantCards.enabled = value; }));
            features.Add(new MazeProfilingFeatureHandle("environment.cloud_overhead_cards", "Overhead Cloud Cards", "Environment", () => HasOverheadCards ? $"on {profile.overheadCards.count}" : "off", value => profile.overheadCards.enabled = value, () => profile.overheadCards.enabled, state => { if (state is bool value) profile.overheadCards.enabled = value; }));
            features.Add(new MazeProfilingFeatureHandle("environment.cloud_fake_shadows", "Fake Cloud Shadows", "Environment", () => HasFakeShadows ? $"on {profile.fakeShadows.strength:0.00}" : "off", value => profile.fakeShadows.enabled = value, () => profile.fakeShadows.enabled, state => { if (state is bool value) profile.fakeShadows.enabled = value; }));
            features.Add(new MazeProfilingFeatureHandle("environment.cloud_volumetric", "Volumetric Clouds", "Environment", () => HasVolumetric ? $"on {profile.volumetric.performanceMode}" : "off", value => profile.volumetric.enabled = value, () => profile.volumetric.enabled, state => { if (state is bool value) profile.volumetric.enabled = value; }));
        }

        public void AddRenderDebugViews(List<MazeRenderDebugView> views)
        {
            views.Add(new MazeRenderDebugView(RenderDebugProviderId, "cloud_combined_density", "volumetric cloud density", "cloud_combined_density.png", true));
            views.Add(new MazeRenderDebugView(RenderDebugProviderId, "cloud_shadow_density", "cloud shadow density", "cloud_shadow_density.png", true));
        }

        public void AddRenderDebugProbeChannels(List<MazeRenderDebugProbeChannel> channels)
        {
            channels.Add(new MazeRenderDebugProbeChannel(RenderDebugProviderId, "cloud_combined_density", "R", "volumetric cloud density"));
            channels.Add(new MazeRenderDebugProbeChannel(RenderDebugProviderId, "cloud_shadow_density", "R", "cloud shadow density"));
        }

        public object CaptureRenderDebugState() => renderDebugMode;
        public void RestoreRenderDebugState(object state) => renderDebugMode = state is float value ? value : 0f;
        public void SetRenderDebugView(string viewId) => renderDebugMode = viewId == "cloud_combined_density" ? 2f : viewId == "cloud_shadow_density" ? 3f : 0f;
        public void ClearRenderDebugView() => renderDebugMode = 0f;

        public string BuildRenderDebugStateJson()
        {
            if (profile == null)
            {
                return "{\"available\":false}";
            }

            return string.Format(CultureInfo.InvariantCulture,
                "{{\"available\":true,\"enabled\":{0},\"cloudiness\":{1:0.###},\"distantCards\":{2},\"overheadCards\":{3},\"fakeShadows\":{4},\"volumetric\":{5}}}",
                IsRenderingEnabled ? "true" : "false", Mathf.Max(profile.distantCards.cloudiness, profile.overheadCards.cloudiness),
                HasDistantCards ? "true" : "false", HasOverheadCards ? "true" : "false",
                HasFakeShadows ? "true" : "false", HasVolumetric ? "true" : "false");
        }
    }
}
