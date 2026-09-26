using System.Collections.Generic;
using UnityEngine;

namespace Maze
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MazeAtmosphereController : MonoBehaviour, IMazeProfilingFeatureProvider
    {
        private static readonly int SkyZenithColorId = Shader.PropertyToID("_MazeAtmosphere_SkyZenithColor");
        private static readonly int SkyHorizonColorId = Shader.PropertyToID("_MazeAtmosphere_SkyHorizonColor");
        private static readonly int SkyGroundColorId = Shader.PropertyToID("_MazeAtmosphere_SkyGroundColor");
        private static readonly int SkySunHaloColorId = Shader.PropertyToID("_MazeAtmosphere_SkySunHaloColor");
        private static readonly int SkyParamsId = Shader.PropertyToID("_MazeAtmosphere_SkyParams");
        private static readonly int StarsParamsId = Shader.PropertyToID("_MazeAtmosphere_StarsParams");
        private static readonly int StarsColorId = Shader.PropertyToID("_MazeAtmosphere_StarsColor");
        private static readonly int AuroraParamsId = Shader.PropertyToID("_MazeAtmosphere_AuroraParams");
        private static readonly int AuroraColorAId = Shader.PropertyToID("_MazeAtmosphere_AuroraColorA");
        private static readonly int AuroraColorBId = Shader.PropertyToID("_MazeAtmosphere_AuroraColorB");
        private static readonly int AuroraHorizonFadeId = Shader.PropertyToID("_MazeAtmosphere_AuroraHorizonFade");

        [SerializeField] private MazeAtmosphereProfile profile;

        public static MazeAtmosphereController Active { get; private set; }
        public MazeAtmosphereProfile Profile => profile;
        public bool IsRenderingEnabled => isActiveAndEnabled && profile != null && profile.atmosphereEnabled;

        private void OnEnable()
        {
            Active = this;
            ApplyGlobalShaderState();
        }

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        private void OnValidate() => ApplyGlobalShaderState();

        public void ApplyGlobalShaderState()
        {
            if (profile == null)
            {
                Shader.SetGlobalVector(SkyParamsId, Vector4.zero);
                Shader.SetGlobalVector(StarsParamsId, Vector4.zero);
                Shader.SetGlobalVector(AuroraParamsId, Vector4.zero);
                return;
            }

            var sky = profile.sky;
            Shader.SetGlobalColor(SkyZenithColorId, sky.zenithColor.linear);
            Shader.SetGlobalColor(SkyHorizonColorId, sky.horizonColor.linear);
            Shader.SetGlobalColor(SkyGroundColorId, sky.groundColor.linear);
            Shader.SetGlobalColor(SkySunHaloColorId, sky.sunHaloColor.linear);
            Shader.SetGlobalVector(SkyParamsId, new Vector4(sky.horizonExponent, sky.sunHaloIntensity, sky.sunHaloSize, profile.atmosphereEnabled ? 1f : 0f));

            var stars = profile.stars;
            Shader.SetGlobalVector(StarsParamsId, new Vector4(stars.enabled ? stars.intensity : 0f, stars.density, stars.twinkle, stars.driftSpeed));
            Shader.SetGlobalColor(StarsColorId, stars.color.linear);

            var aurora = profile.aurora;
            Shader.SetGlobalVector(AuroraParamsId, new Vector4(aurora.enabled ? aurora.intensity : 0f, aurora.scale, aurora.speed, aurora.fluctuation));
            Shader.SetGlobalColor(AuroraColorAId, aurora.colorA.linear);
            Shader.SetGlobalColor(AuroraColorBId, aurora.colorB.linear);
            Shader.SetGlobalFloat(AuroraHorizonFadeId, aurora.horizonFade);
        }

        public void AddMazeProfilingFeatures(List<MazeProfilingFeatureHandle> features)
        {
            if (profile == null)
            {
                return;
            }

            features.Add(new MazeProfilingFeatureHandle(
                "environment.atmosphere", "Atmosphere", "Environment",
                () => IsRenderingEnabled ? "on" : "off",
                enabled => { profile.atmosphereEnabled = enabled; ApplyGlobalShaderState(); },
                () => profile.atmosphereEnabled,
                state => { if (state is bool value) { profile.atmosphereEnabled = value; ApplyGlobalShaderState(); } }));
        }
    }
}
