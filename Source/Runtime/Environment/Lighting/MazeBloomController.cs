using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maze
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MazeBloomController : MonoBehaviour, IMazeProfilingFeatureProvider
    {
        private static readonly int BloomParamsAId = Shader.PropertyToID("_MazeBloom_ParamsA");
        private static readonly int BloomParamsBId = Shader.PropertyToID("_MazeBloom_ParamsB");
        private static readonly int BloomTintId = Shader.PropertyToID("_MazeBloom_Tint");
        private static readonly List<MazeBloomController> Controllers = new();

        [SerializeField] private MazeBloomProfile profile;

        public MazeBloomProfile Profile => profile;
        public bool IsRenderingEnabled => isActiveAndEnabled && profile != null && profile.RendersBloom;

        public static MazeBloomController ResolveForCamera(Camera camera)
        {
            CleanupNullControllers();
            MazeBloomController first = null;
            MazeBloomController activeSceneMatch = null;
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
            var intensity = enabled ? Mathf.Clamp(profile.intensity, 0f, 5f) : 0f;
            material.SetVector(BloomParamsAId, new Vector4(
                Mathf.Max(0f, profile.threshold),
                Mathf.Clamp01(profile.softKnee),
                intensity,
                Mathf.Clamp01(profile.scatter)));
            material.SetVector(BloomParamsBId, new Vector4(
                Mathf.Max(0f, profile.clamp),
                profile.RendersStreaks ? Mathf.Clamp(profile.streakIntensity, 0f, 3f) : 0f,
                Mathf.Max(0.25f, profile.streakStretch),
                profile.streakAngle * Mathf.Deg2Rad));
            material.SetColor(BloomTintId, profile.tint.linear);
            LogState(enabled);
        }

        private void LogState(bool enabled)
        {
            if (!Application.isPlaying || profile == null)
            {
                return;
            }

            var state = $"{enabled}|{profile.quality}|{profile.threshold:0.###}|{profile.softKnee:0.###}|{profile.intensity:0.###}|{profile.EffectiveMipCount}|{profile.RendersStreaks}|{profile.streakIntensity:0.###}";
            MazeDiagnosticsLog.InfoOnChange(
                "graphics.bloom.state:" + GetEntityId(),
                state,
                "Graphics.Bloom",
                "state",
                "bloom material state applied",
                MazeDiagnosticsLog.JoinData(
                    MazeDiagnosticsLog.JsonBool("enabled", enabled),
                    MazeDiagnosticsLog.JsonString("quality", profile.quality.ToString()),
                    MazeDiagnosticsLog.JsonNumber("threshold", profile.threshold),
                    MazeDiagnosticsLog.JsonNumber("softKnee", profile.softKnee),
                    MazeDiagnosticsLog.JsonNumber("intensity", profile.intensity),
                    MazeDiagnosticsLog.JsonNumber("mips", profile.EffectiveMipCount),
                    MazeDiagnosticsLog.JsonBool("streaks", profile.RendersStreaks)));
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
                "environment.bloom",
                "Bloom",
                "Environment",
                () => IsRenderingEnabled ? $"{profile.quality} {profile.intensity:0.##}x" : "off",
                enabled => profile.bloomEnabled = enabled,
                () => profile.bloomEnabled,
                state =>
                {
                    if (state is bool value)
                    {
                        profile.bloomEnabled = value;
                    }
                },
                "off/quality"));
        }
    }
}
