using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Maze
{
    public sealed class MazeFogRenderFeature : ScriptableRendererFeature
    {
        private static readonly int FogDepthTextureId = Shader.PropertyToID("_MazeFogDepthTexture");
        private static readonly int FogDebugModeId = Shader.PropertyToID("_MazeFog_DebugView");

        private sealed class FogPass : MazeRenderGraphFullscreenPass
        {
            private MazeFogController controller;
            private bool loggedMissingDepth;
            private bool loggedOpaqueDepth;

            public FogPass(Material material)
                : base(
                    "MAZE Fog",
                    material,
                    0,
                    RenderPassEvent.BeforeRenderingPostProcessing,
                    ScriptableRenderPassInput.Depth,
                    true)
            {
            }

            public void Setup(Material material, MazeFogController controller)
            {
                base.Setup(material);
                this.controller = controller;
            }

            protected override bool ShouldRecord(ContextContainer frameData, UniversalResourceData resourceData, UniversalCameraData cameraData)
            {
                if (!base.ShouldRecord(frameData, resourceData, cameraData) || controller == null)
                {
                    return false;
                }

                var camera = cameraData.camera;
                if (!controller.IsRenderingEnabled || !controller.AllowsCamera(camera, cameraData.isSceneViewCamera))
                {
                    return false;
                }

                var fogSceneViewAllowed = MazeSceneViewGraphicsState.ShouldRenderFog(cameraData.isSceneViewCamera);
                if (fogSceneViewAllowed && !MazeDepthSource.TryGetOpaqueDepth(resourceData, out _))
                {
                    LogMissingDepth(camera);
                    return false;
                }

                if (fogSceneViewAllowed)
                {
                    LogOpaqueDepth(camera);
                }

                controller.ApplyToMaterial(Material, camera, cameraData.isSceneViewCamera, fogSceneViewAllowed);
                return true;
            }

            private void LogMissingDepth(Camera camera)
            {
                if (loggedMissingDepth)
                {
                    return;
                }

                loggedMissingDepth = true;
                Debug.LogWarning("[MAZE FOG] Skipping fog pass: opaque scene depth is unavailable; post-transparent active depth is not a safe fallback.");
                if (Application.isPlaying)
                {
                    MazeDiagnosticsLog.Warn(
                        "Graphics.Fog",
                        "missing_opaque_depth",
                        "fog skipped because opaque scene depth was unavailable",
                        MazeDiagnosticsLog.JoinData(
                            MazeDiagnosticsLog.JsonString("consumer", "Fog"),
                            MazeDiagnosticsLog.JsonString("requiredSource", MazeDepthSource.OpaqueDepthSourceName),
                            MazeDiagnosticsLog.JsonString("camera", camera != null ? camera.name : "none"),
                            MazeDiagnosticsLog.JsonBool("activeDepthFallback", false)));
                }
            }

            private void LogOpaqueDepth(Camera camera)
            {
                if (loggedOpaqueDepth || !Application.isPlaying)
                {
                    return;
                }

                loggedOpaqueDepth = true;
                MazeDiagnosticsLog.Info(
                    "Graphics.Depth",
                    "opaque_depth_bound",
                    "Fog bound the opaque scene depth contract",
                    MazeDiagnosticsLog.JoinData(
                        MazeDiagnosticsLog.JsonString("consumer", "Fog"),
                        MazeDiagnosticsLog.JsonString("source", MazeDepthSource.OpaqueDepthSourceName),
                        MazeDiagnosticsLog.JsonString("camera", camera != null ? camera.name : "none")));
            }

            protected override void ConfigurePass(
                IRasterRenderGraphBuilder builder,
                ContextContainer frameData,
                UniversalResourceData resourceData,
                UniversalCameraData cameraData,
                PassData passData)
            {
                if (MazeDepthSource.TryGetOpaqueDepth(resourceData, out var depthTexture))
                {
                    builder.UseTexture(depthTexture, AccessFlags.Read);
                    passData.extraTexturePropertyId = FogDepthTextureId;
                    passData.extraTexture = depthTexture;
                }
            }
        }

        [SerializeField] private Shader shader;
        private Material material;
        private FogPass pass;

        public override void Create()
        {
            shader ??= Shader.Find("Hidden/Maze/Environment/Fog");
            if (shader != null && material == null)
            {
                material = CoreUtils.CreateEngineMaterial(shader);
            }

            pass ??= new FogPass(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.isPreviewCamera)
            {
                return;
            }

            if (renderingData.cameraData.isSceneViewCamera && !MazeSceneViewGraphicsState.ShouldRenderFog(true))
            {
                return;
            }

            var camera = renderingData.cameraData.camera;
            if (!MazeSceneFxCameraPolicy.Allows(camera, MazeSceneFxPassMask.Fog))
            {
                return;
            }

            var controller = MazeFogController.ResolveForCamera(camera);
            if (controller == null)
            {
                return;
            }

            pass.Setup(material, controller);
            renderer.EnqueuePass(pass);
        }

        public static int ForceLoadedDebugMaterialReset()
        {
            var controller = Resources.FindObjectsOfTypeAll<MazeFogController>();
            const float finalColorMaterialMode = 15f;
            var effectiveMode = finalColorMaterialMode;
            for (var i = 0; i < controller.Length; i++)
            {
                var candidate = controller[i];
                if (candidate != null && candidate.gameObject.scene.IsValid())
                {
                    effectiveMode = candidate.EffectiveMaterialDebugValue;
                    break;
                }
            }

            var resetCount = 0;
            var features = Resources.FindObjectsOfTypeAll<MazeFogRenderFeature>();
            for (var i = 0; i < features.Length; i++)
            {
                var feature = features[i];
                if (feature == null || feature.material == null)
                {
                    continue;
                }

                // Unity can preserve the previous provider view in the visible GameView after
                // the logical debug state is cleared. Reapply the controller's explicit final-
                // color material mode rather than relying on an ambiguous zero/default state.
                feature.material.SetFloat(FogDebugModeId, effectiveMode);
                resetCount++;
            }

            return resetCount;
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }
    }
}
