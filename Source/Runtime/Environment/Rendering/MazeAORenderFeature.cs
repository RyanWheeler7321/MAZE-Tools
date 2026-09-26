using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Maze
{
    public sealed class MazeAORenderFeature : ScriptableRendererFeature
    {
        private static readonly int AODepthTextureId = Shader.PropertyToID("_MazeAODepthTexture");

        private sealed class AOPass : MazeRenderGraphFullscreenPass
        {
            private MazeAOController controller;
            private bool loggedMissingDepth;
            private bool loggedOpaqueDepth;

            public AOPass(Material material)
                : base(
                    "MAZE AO",
                    material,
                    0,
                    RenderPassEvent.BeforeRenderingPostProcessing,
                    ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal,
                    true)
            {
            }

            public void Setup(Material material, MazeAOController controller)
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

                if (!MazeDepthSource.TryGetOpaqueDepth(resourceData, out _))
                {
                    LogMissingDepth(camera);
                    return false;
                }

                LogOpaqueDepth(camera);
                controller.ApplyToMaterial(Material, camera, cameraData.isSceneViewCamera);
                return true;
            }

            private void LogMissingDepth(Camera camera)
            {
                if (loggedMissingDepth)
                {
                    return;
                }

                loggedMissingDepth = true;
                Debug.LogWarning("[MAZE AO] Skipping AO pass: opaque scene depth is unavailable; post-transparent active depth is not a safe fallback.");
                if (Application.isPlaying)
                {
                    MazeDiagnosticsLog.Warn(
                        "Graphics.AO",
                        "missing_opaque_depth",
                        "AO skipped because opaque scene depth was unavailable",
                        MazeDiagnosticsLog.JoinData(
                            MazeDiagnosticsLog.JsonString("consumer", "AO"),
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
                    "AO bound the opaque scene depth contract",
                    MazeDiagnosticsLog.JoinData(
                        MazeDiagnosticsLog.JsonString("consumer", "AO"),
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
                    passData.extraTexturePropertyId = AODepthTextureId;
                    passData.extraTexture = depthTexture;
                }

                if (resourceData.cameraNormalsTexture.IsValid())
                {
                    builder.UseTexture(resourceData.cameraNormalsTexture, AccessFlags.Read);
                }
            }
        }

        [SerializeField] private Shader shader;
        private readonly Dictionary<EntityId, Material> cameraMaterials = new();
        private readonly Dictionary<EntityId, AOPass> cameraPasses = new();

        public override void Create()
        {
            shader ??= Shader.Find("Hidden/Maze/Rendering/AO");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (shader == null || renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.isPreviewCamera)
            {
                return;
            }

            if (renderingData.cameraData.isSceneViewCamera && !MazeSceneViewGraphicsState.ShouldRenderAO(true))
            {
                return;
            }

            var camera = renderingData.cameraData.camera;
            if (!MazeSceneFxCameraPolicy.Allows(camera, MazeSceneFxPassMask.AO))
            {
                return;
            }

            var controller = MazeAOController.ResolveForCamera(camera);
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
                cameraMaterial.name = camera != null ? $"MAZE AO ({camera.name})" : "MAZE AO";
                cameraMaterials[key] = cameraMaterial;
            }

            return cameraMaterial;
        }

        private AOPass GetPass(Camera camera, Material material)
        {
            var key = camera != null ? camera.GetEntityId() : EntityId.None;
            if (!cameraPasses.TryGetValue(key, out var cameraPass) || cameraPass == null)
            {
                cameraPass = new AOPass(material);
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
