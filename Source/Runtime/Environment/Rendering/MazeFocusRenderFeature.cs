using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Maze
{
    public sealed class MazeFocusRenderFeature : ScriptableRendererFeature
    {
        private const int PrefilterPass = 0;
        private const int CloseGatherPass = 1;
        private const int FarGatherPass = 2;
        private const int CompositePass = 3;
        private const int LensPass = 4;

        private sealed class FocusPass : ScriptableRenderPass
        {
            private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            private static readonly int FocusDepthTextureId = Shader.PropertyToID("_MazeFocusDepthTexture");
            private static readonly int FocusCloudMaskId = Shader.PropertyToID("_MazeFocusCloudMask");
            private static readonly int FocusCloseTextureId = Shader.PropertyToID("_MazeFocusCloseTexture");
            private static readonly int FocusFarTextureId = Shader.PropertyToID("_MazeFocusFarTexture");
            private static readonly MaterialPropertyBlock SharedPropertyBlock = new();
            private static readonly ProfilingSampler PrefilterSampler = new("MAZE Focus Band Prefilter");
            private static readonly ProfilingSampler CloseSampler = new("MAZE Focus Close Gather");
            private static readonly ProfilingSampler FarSampler = new("MAZE Focus Far Gather");
            private static readonly ProfilingSampler CompositeSampler = new("MAZE Focus Band Composite");
            private static readonly ProfilingSampler LensSampler = new("MAZE Focus Lens");

            private readonly ProfilingSampler sampler = new("MAZE Focus");
            private Material material;
            private MazeFocusController controller;
            private bool loggedMissingDepth;
            private bool loggedMissingCloudMask;
            private bool loggedInvalidSource;
            private bool loggedBackBuffer;
            private bool loggedMaterial;
            private bool loggedFirstRecord;

            public FocusPass(Material material)
            {
                this.material = material;
                profilingSampler = sampler;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public void Setup(Material material, MazeFocusController controller)
            {
                this.material = material;
                this.controller = controller;
                ConfigureInput(controller != null && controller.Profile != null && controller.Profile.RendersDepthBlur
                    ? ScriptableRenderPassInput.Depth
                    : ScriptableRenderPassInput.None);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null || material.passCount <= LensPass)
                {
                    LogMaterial();
                    return;
                }

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                if (controller == null || !controller.IsRenderingEnabled || !controller.AllowsCamera(cameraData.camera, cameraData.isSceneViewCamera))
                {
                    return;
                }

                if (resourceData.isActiveTargetBackBuffer)
                {
                    LogBackBuffer();
                    return;
                }

                var source = resourceData.activeColorTexture;
                if (!source.IsValid())
                {
                    LogInvalidSource();
                    return;
                }

                var profile = controller.Profile;
                var rendersDepth = profile != null && profile.RendersDepthBlur;
                var rendersLens = profile != null && profile.RendersLens;
                var depth = TextureHandle.nullHandle;
                if (rendersDepth)
                {
                    if (!MazeDepthSource.TryGetOpaqueDepth(resourceData, out depth))
                    {
                        LogMissingDepth(cameraData.camera);
                        rendersDepth = false;
                    }
                }

                if (!rendersDepth && !rendersLens)
                {
                    return;
                }

                var cloudMask = TextureHandle.nullHandle;
                var cloudController = rendersDepth ? MazeCloudController.ResolveForCamera(cameraData.camera) : null;
                if (rendersDepth && frameData.Contains<MazeFocusFrameData>())
                {
                    var cloudFrame = frameData.Get<MazeFocusFrameData>();
                    if (cloudController == null || cloudFrame.CloudControllerEntityId == cloudController.GetEntityId())
                    {
                        cloudMask = cloudFrame.CloudMask;
                    }
                }

                if (rendersDepth && cloudController is { HasCards: true } && !cloudMask.IsValid())
                {
                    LogMissingCloudMask();
                }

                controller.ApplyToMaterial(material, cameraData.camera, cameraData.isSceneViewCamera);
                var sourceDesc = renderGraph.GetTextureDesc(source);
                var current = source;
                var recordedStages = 0;
                var gatherWidth = 0;
                var gatherHeight = 0;
                var gatherFormat = GraphicsFormat.None;

                if (rendersDepth)
                {
                    var downsample = Mathf.Max(1, profile.EffectiveDownsample);
                    var gatherDesc = sourceDesc;
                    gatherDesc.name = "MAZE Focus Close Seed";
                    gatherDesc.clearBuffer = true;
                    gatherDesc.clearColor = Color.clear;
                    gatherDesc.msaaSamples = MSAASamples.None;
                    gatherDesc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
                    gatherDesc.width = Mathf.Max(2, sourceDesc.width / downsample);
                    gatherDesc.height = Mathf.Max(2, sourceDesc.height / downsample);
                    gatherDesc.filterMode = FilterMode.Bilinear;
                    gatherWidth = gatherDesc.width;
                    gatherHeight = gatherDesc.height;
                    gatherFormat = gatherDesc.colorFormat;
                    var closeSeed = renderGraph.CreateTexture(gatherDesc);

                    gatherDesc.name = "MAZE Focus Far Seed";
                    var farSeed = renderGraph.CreateTexture(gatherDesc);
                    gatherDesc.name = "MAZE Focus Close Coverage";
                    var closeGather = renderGraph.CreateTexture(gatherDesc);
                    gatherDesc.name = "MAZE Focus Far Coverage";
                    var farGather = renderGraph.CreateTexture(gatherDesc);

                    var outputDesc = sourceDesc;
                    outputDesc.name = "MAZE Focus Band Output";
                    outputDesc.clearBuffer = false;
                    outputDesc.msaaSamples = MSAASamples.None;
                    var focused = renderGraph.CreateTexture(outputDesc);

                    AddPrefilterPass(renderGraph, source, depth, cloudMask, closeSeed, farSeed);
                    AddGatherPass(renderGraph, "MAZE Focus Close Gather", CloseSampler, CloseGatherPass, closeSeed, depth, closeGather);
                    AddGatherPass(renderGraph, "MAZE Focus Far Gather", FarSampler, FarGatherPass, farSeed, depth, farGather);
                    AddCompositePass(renderGraph, source, depth, cloudMask, closeGather, farGather, focused);
                    current = focused;
                    recordedStages += 4;
                }

                if (rendersLens)
                {
                    var lensDesc = sourceDesc;
                    lensDesc.name = "MAZE Focus Lens Output";
                    lensDesc.clearBuffer = false;
                    lensDesc.msaaSamples = MSAASamples.None;
                    var lensOutput = renderGraph.CreateTexture(lensDesc);
                    AddLensPass(renderGraph, current, lensOutput);
                    current = lensOutput;
                    recordedStages++;
                }

                resourceData.cameraColor = current;
                MazeRenderFrameServices.RecordPass("MAZE Focus", MazeRenderPassKind.Fullscreen, recordedStages);

                if (!loggedFirstRecord)
                {
                    loggedFirstRecord = true;
                    if (Application.isPlaying)
                    {
                        MazeDiagnosticsLog.Info(
                            "RenderGraph",
                            "pass_record",
                            "focus passes recorded",
                            MazeDiagnosticsLog.JoinData(
                                MazeDiagnosticsLog.JsonBool("depthBlur", rendersDepth),
                                MazeDiagnosticsLog.JsonBool("lens", rendersLens),
                                MazeDiagnosticsLog.JsonString("camera", cameraData.camera != null ? cameraData.camera.name : "none"),
                                MazeDiagnosticsLog.JsonNumber("stages", recordedStages),
                                MazeDiagnosticsLog.JsonNumber("sourceWidth", sourceDesc.width),
                                MazeDiagnosticsLog.JsonNumber("sourceHeight", sourceDesc.height),
                                MazeDiagnosticsLog.JsonString("sourceFormat", sourceDesc.colorFormat.ToString()),
                                MazeDiagnosticsLog.JsonString("sourceMsaa", sourceDesc.msaaSamples.ToString()),
                                MazeDiagnosticsLog.JsonNumber("gatherWidth", gatherWidth),
                                MazeDiagnosticsLog.JsonNumber("gatherHeight", gatherHeight),
                                MazeDiagnosticsLog.JsonString("gatherFormat", gatherFormat.ToString()),
                                MazeDiagnosticsLog.JsonBool("coverageAlpha", !rendersDepth || gatherFormat == GraphicsFormat.R16G16B16A16_SFloat),
                                MazeDiagnosticsLog.JsonBool("depthValid", !rendersDepth || depth.IsValid()),
                                MazeDiagnosticsLog.JsonString("depthSource", rendersDepth ? MazeDepthSource.OpaqueDepthSourceName : "none"),
                                MazeDiagnosticsLog.JsonBool("cloudMaskValid", !rendersDepth || cloudMask.IsValid()),
                                MazeDiagnosticsLog.JsonNumber("downsample", rendersDepth ? profile.EffectiveDownsample : 1),
                                MazeDiagnosticsLog.JsonNumber("samples", rendersDepth ? profile.EffectiveSampleCount : 0)));
                    }
                }
            }

            private void AddPrefilterPass(
                RenderGraph renderGraph,
                TextureHandle source,
                TextureHandle depth,
                TextureHandle cloudMask,
                TextureHandle closeSeed,
                TextureHandle farSeed)
            {
                using var builder = renderGraph.AddRasterRenderPass<FocusPassData>("MAZE Focus Band Prefilter", out var passData, PrefilterSampler);
                PopulatePassData(passData, PrefilterPass, source, depth, cloudMask, TextureHandle.nullHandle, TextureHandle.nullHandle);
                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(depth, AccessFlags.Read);
                if (cloudMask.IsValid())
                {
                    builder.UseTexture(cloudMask, AccessFlags.Read);
                }

                builder.SetRenderAttachment(closeSeed, 0, AccessFlags.Write);
                builder.SetRenderAttachment(farSeed, 1, AccessFlags.Write);
                builder.SetRenderFunc(static (FocusPassData data, RasterGraphContext context) => Execute(data, context));
            }

            private void AddGatherPass(
                RenderGraph renderGraph,
                string name,
                ProfilingSampler profilingSampler,
                int shaderPass,
                TextureHandle seed,
                TextureHandle depth,
                TextureHandle output)
            {
                using var builder = renderGraph.AddRasterRenderPass<FocusPassData>(name, out var passData, profilingSampler);
                PopulatePassData(passData, shaderPass, seed, depth, TextureHandle.nullHandle, TextureHandle.nullHandle, TextureHandle.nullHandle);
                builder.UseTexture(seed, AccessFlags.Read);
                builder.UseTexture(depth, AccessFlags.Read);
                builder.SetRenderAttachment(output, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (FocusPassData data, RasterGraphContext context) => Execute(data, context));
            }

            private void AddCompositePass(
                RenderGraph renderGraph,
                TextureHandle source,
                TextureHandle depth,
                TextureHandle cloudMask,
                TextureHandle closeTexture,
                TextureHandle farTexture,
                TextureHandle output)
            {
                using var builder = renderGraph.AddRasterRenderPass<FocusPassData>("MAZE Focus Band Composite", out var passData, CompositeSampler);
                PopulatePassData(passData, CompositePass, source, depth, cloudMask, closeTexture, farTexture);
                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(depth, AccessFlags.Read);
                builder.UseTexture(closeTexture, AccessFlags.Read);
                builder.UseTexture(farTexture, AccessFlags.Read);
                if (cloudMask.IsValid())
                {
                    builder.UseTexture(cloudMask, AccessFlags.Read);
                }

                builder.SetRenderAttachment(output, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (FocusPassData data, RasterGraphContext context) => Execute(data, context));
            }

            private void AddLensPass(RenderGraph renderGraph, TextureHandle source, TextureHandle output)
            {
                using var builder = renderGraph.AddRasterRenderPass<FocusPassData>("MAZE Focus Lens", out var passData, LensSampler);
                PopulatePassData(passData, LensPass, source, TextureHandle.nullHandle, TextureHandle.nullHandle, TextureHandle.nullHandle, TextureHandle.nullHandle);
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(output, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (FocusPassData data, RasterGraphContext context) => Execute(data, context));
            }

            private void PopulatePassData(
                FocusPassData passData,
                int shaderPass,
                TextureHandle input,
                TextureHandle depth,
                TextureHandle cloudMask,
                TextureHandle closeTexture,
                TextureHandle farTexture)
            {
                passData.material = material;
                passData.shaderPass = shaderPass;
                passData.input = input;
                passData.depth = depth;
                passData.cloudMask = cloudMask;
                passData.closeTexture = closeTexture;
                passData.farTexture = farTexture;
            }

            private static void Execute(FocusPassData data, RasterGraphContext context)
            {
                SharedPropertyBlock.Clear();
                SharedPropertyBlock.SetTexture(BlitTextureId, data.input);
                if (data.depth.IsValid()) SharedPropertyBlock.SetTexture(FocusDepthTextureId, data.depth);
                else SharedPropertyBlock.SetTexture(FocusDepthTextureId, Texture2D.blackTexture);
                if (data.cloudMask.IsValid()) SharedPropertyBlock.SetTexture(FocusCloudMaskId, data.cloudMask);
                else SharedPropertyBlock.SetTexture(FocusCloudMaskId, Texture2D.blackTexture);
                if (data.closeTexture.IsValid()) SharedPropertyBlock.SetTexture(FocusCloseTextureId, data.closeTexture);
                else SharedPropertyBlock.SetTexture(FocusCloseTextureId, Texture2D.blackTexture);
                if (data.farTexture.IsValid()) SharedPropertyBlock.SetTexture(FocusFarTextureId, data.farTexture);
                else SharedPropertyBlock.SetTexture(FocusFarTextureId, Texture2D.blackTexture);
                SharedPropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                context.cmd.DrawProcedural(Matrix4x4.identity, data.material, data.shaderPass, MeshTopology.Triangles, 3, 1, SharedPropertyBlock);
            }

            private void LogMissingDepth(Camera camera)
            {
                if (loggedMissingDepth)
                {
                    return;
                }

                loggedMissingDepth = true;
                Debug.LogWarning("[MAZE FOCUS] Depth blur skipped because opaque scene depth was unavailable; Lens may still render and post-transparent active depth is not used as fallback.");
                if (Application.isPlaying)
                {
                    MazeDiagnosticsLog.Warn(
                        "Graphics.Focus",
                        "missing_opaque_depth",
                        "focus depth blur skipped because opaque scene depth was unavailable",
                        MazeDiagnosticsLog.JoinData(
                            MazeDiagnosticsLog.JsonString("consumer", "Focus"),
                            MazeDiagnosticsLog.JsonString("requiredSource", MazeDepthSource.OpaqueDepthSourceName),
                            MazeDiagnosticsLog.JsonString("camera", camera != null ? camera.name : "none"),
                            MazeDiagnosticsLog.JsonBool("activeDepthFallback", false),
                            MazeDiagnosticsLog.JsonBool("lensMayRender", true)));
                }
            }

            private void LogMissingCloudMask()
            {
                if (loggedMissingCloudMask)
                {
                    return;
                }

                loggedMissingCloudMask = true;
                Debug.LogWarning("[MAZE FOCUS] Active cloud cards did not provide a Focus classification mask this frame; background depth will classify them.");
            }

            private void LogInvalidSource()
            {
                if (loggedInvalidSource) return;
                loggedInvalidSource = true;
                Debug.LogWarning("[MAZE FOCUS] Focus skipped because the active color texture was invalid.");
            }

            private void LogBackBuffer()
            {
                if (loggedBackBuffer) return;
                loggedBackBuffer = true;
                Debug.LogWarning("[MAZE FOCUS] Focus skipped because the active target was the backbuffer.");
            }

            private void LogMaterial()
            {
                if (loggedMaterial) return;
                loggedMaterial = true;
                Debug.LogWarning("[MAZE FOCUS] Focus skipped because its material or shader passes were missing.");
            }

            private sealed class FocusPassData
            {
                public Material material;
                public int shaderPass;
                public TextureHandle input;
                public TextureHandle depth;
                public TextureHandle cloudMask;
                public TextureHandle closeTexture;
                public TextureHandle farTexture;
            }
        }

        [SerializeField] private Shader shader;
        private readonly Dictionary<EntityId, Material> cameraMaterials = new();
        private readonly Dictionary<EntityId, FocusPass> cameraPasses = new();

        public override void Create()
        {
            shader ??= Shader.Find("Hidden/Maze/Rendering/Focus");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (shader == null || renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.isPreviewCamera)
            {
                return;
            }

            if (renderingData.cameraData.isSceneViewCamera && !MazeSceneViewGraphicsState.ShouldRenderFocus(true))
            {
                return;
            }

            var camera = renderingData.cameraData.camera;
            if (!MazeSceneFxCameraPolicy.Allows(camera, MazeSceneFxPassMask.Focus))
            {
                return;
            }

            var controller = MazeFocusController.ResolveForCamera(camera);
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
            var key = CameraKey(camera);
            if (!cameraMaterials.TryGetValue(key, out var cameraMaterial) || cameraMaterial == null)
            {
                cameraMaterial = CoreUtils.CreateEngineMaterial(shader);
                cameraMaterial.name = camera != null ? $"MAZE Focus ({camera.name})" : "MAZE Focus";
                cameraMaterials[key] = cameraMaterial;
            }

            return cameraMaterial;
        }

        private FocusPass GetPass(Camera camera, Material material)
        {
            var key = CameraKey(camera);
            if (!cameraPasses.TryGetValue(key, out var cameraPass) || cameraPass == null)
            {
                cameraPass = new FocusPass(material);
                cameraPasses[key] = cameraPass;
            }

            return cameraPass;
        }

        private static EntityId CameraKey(Camera camera)
        {
            return camera != null ? camera.GetEntityId() : EntityId.None;
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
