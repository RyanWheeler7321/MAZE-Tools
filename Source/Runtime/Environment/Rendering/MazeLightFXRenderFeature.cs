using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Maze
{
    public sealed class MazeLightFXRenderFeature : ScriptableRendererFeature
    {
        private sealed class LightFXPass : MazeRenderGraphFullscreenPass
        {
            private MazeLightFXController controller;
            private bool loggedMissingDepth;

            public LightFXPass(Material material)
                : base(
                    "MAZE LightFX",
                    material,
                    0,
                    RenderPassEvent.BeforeRenderingPostProcessing,
                    ScriptableRenderPassInput.Depth,
                    true)
            {
            }

            public void Setup(Material material, MazeLightFXController controller)
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

                if (!resourceData.cameraDepthTexture.IsValid())
                {
                    LogMissingDepth();
                    return false;
                }

                controller.ApplyToMaterial(Material, camera, cameraData.isSceneViewCamera);
                return true;
            }

            private void LogMissingDepth()
            {
                if (loggedMissingDepth)
                {
                    return;
                }

                loggedMissingDepth = true;
                Debug.LogWarning("[MAZE LIGHTFX] Skipping LightFX pass: camera depth texture is invalid.");
                if (Application.isPlaying)
                {
                    MazeDiagnosticsLog.Warn("Graphics.LightFX", "missing_depth", "lightfx skipped because camera depth texture is invalid");
                }
            }

            protected override void ConfigurePass(
                IRasterRenderGraphBuilder builder,
                ContextContainer frameData,
                UniversalResourceData resourceData,
                UniversalCameraData cameraData,
                PassData passData)
            {
                if (resourceData.cameraDepthTexture.IsValid())
                {
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                }
            }
        }

        [SerializeField] private Shader shader;
        private readonly Dictionary<EntityId, Material> cameraMaterials = new();
        private readonly Dictionary<EntityId, LightFXPass> cameraPasses = new();

        public override void Create()
        {
            shader ??= Shader.Find("Hidden/Maze/Environment/LightFX");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (shader == null || renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.isPreviewCamera)
            {
                return;
            }

            if (renderingData.cameraData.isSceneViewCamera && !MazeSceneViewGraphicsState.ShouldRenderLightFX(true))
            {
                return;
            }

            var camera = renderingData.cameraData.camera;
            if (!MazeSceneFxCameraPolicy.Allows(camera, MazeSceneFxPassMask.LightFX))
            {
                return;
            }

            var controller = MazeLightFXController.ResolveForCamera(camera);
            if (controller == null || !controller.IsRenderingEnabled || !controller.AllowsCamera(camera, renderingData.cameraData.isSceneViewCamera))
            {
                return;
            }

            var material = GetMaterial(camera);
            var pass = GetPass(camera, material);
            pass.Setup(material, controller);
            renderer.EnqueuePass(pass);
        }

        private Material GetMaterial(Camera camera)
        {
            var key = camera != null ? camera.GetEntityId() : EntityId.None;
            if (!cameraMaterials.TryGetValue(key, out var cameraMaterial) || cameraMaterial == null)
            {
                cameraMaterial = CoreUtils.CreateEngineMaterial(shader);
                cameraMaterial.name = camera != null ? $"MAZE LightFX ({camera.name})" : "MAZE LightFX";
                cameraMaterials[key] = cameraMaterial;
            }

            return cameraMaterial;
        }

        private LightFXPass GetPass(Camera camera, Material material)
        {
            var key = camera != null ? camera.GetEntityId() : EntityId.None;
            if (!cameraPasses.TryGetValue(key, out var cameraPass) || cameraPass == null)
            {
                cameraPass = new LightFXPass(material);
                cameraPasses[key] = cameraPass;
            }

            return cameraPass;
        }

        protected override void Dispose(bool disposing)
        {
            foreach (var cameraMaterial in cameraMaterials.Values)
            {
                CoreUtils.Destroy(cameraMaterial);
            }

            cameraMaterials.Clear();
            cameraPasses.Clear();
        }
    }
}
