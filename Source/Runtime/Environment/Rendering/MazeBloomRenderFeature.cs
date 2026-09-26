using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Maze
{
    public sealed class MazeBloomRenderFeature : ScriptableRendererFeature
    {
        private const int MaxMips = 6;
        private const int PrefilterPass = 0;
        private const int DownsamplePass = 1;
        private const int UpsamplePass = 2;
        private const int CompositePass = 3;

        private sealed class BloomPass : ScriptableRenderPass
        {
            private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            private static readonly int BloomTextureId = Shader.PropertyToID("_MazeBloom_Texture");
            private static readonly int BloomHighTextureId = Shader.PropertyToID("_MazeBloom_HighTexture");
            private static readonly MaterialPropertyBlock SharedPropertyBlock = new();
            private static readonly ProfilingSampler ThresholdSampler = new("MAZE Bloom Threshold");
            private static readonly ProfilingSampler CompositeSampler = new("MAZE Bloom Composite");
            private static readonly ProfilingSampler[] DownSamplers = CreateSamplers("MAZE Bloom Down ", MaxMips);
            private static readonly ProfilingSampler[] UpSamplers = CreateSamplers("MAZE Bloom Up ", MaxMips);

            private readonly ProfilingSampler sampler = new("MAZE Bloom");
            private Material material;
            private MazeBloomController controller;
            private bool loggedFirstRecord;
            private bool loggedInvalidSource;
            private bool loggedBackBuffer;
            private bool loggedMaterial;

            public BloomPass(Material material)
            {
                this.material = material;
                profilingSampler = sampler;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public void Setup(Material material, MazeBloomController controller)
            {
                this.material = material;
                this.controller = controller;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null || material.passCount <= CompositePass)
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

                controller.ApplyToMaterial(material, cameraData.camera, cameraData.isSceneViewCamera);
                var sourceDesc = renderGraph.GetTextureDesc(source);

                var profile = controller.Profile;
                var mipCount = Mathf.Clamp(profile.EffectiveMipCount, 1, MaxMips);
                MazeRenderFrameServices.RecordPass("MAZE Bloom", MazeRenderPassKind.Bloom, mipCount * 2);
                var mips = new TextureHandle[MaxMips];
                var upMips = new TextureHandle[MaxMips];
                for (var i = 0; i < mipCount; i++)
                {
                    var desc = sourceDesc;
                    desc.name = $"MAZE Bloom Mip {i}";
                    desc.clearBuffer = true;
                    desc.width = Mathf.Max(2, sourceDesc.width >> (i + 1));
                    desc.height = Mathf.Max(2, sourceDesc.height >> (i + 1));
                    mips[i] = renderGraph.CreateTexture(desc);
                }

                AddSingleInputPass(renderGraph, "MAZE Bloom Threshold", ThresholdSampler, material, PrefilterPass, source, mips[0]);
                for (var i = 1; i < mipCount; i++)
                {
                    AddSingleInputPass(renderGraph, $"MAZE Bloom Down {i}", DownSamplers[i], material, DownsamplePass, mips[i - 1], mips[i]);
                }

                var current = mips[mipCount - 1];
                for (var i = mipCount - 2; i >= 0; i--)
                {
                    var desc = renderGraph.GetTextureDesc(mips[i]);
                    desc.name = $"MAZE Bloom Up {i}";
                    desc.clearBuffer = true;
                    upMips[i] = renderGraph.CreateTexture(desc);
                    AddUpsamplePass(renderGraph, $"MAZE Bloom Up {i}", UpSamplers[i], material, current, mips[i], upMips[i]);
                    current = upMips[i];
                }

                var outputDesc = sourceDesc;
                outputDesc.name = "MAZE Bloom Output";
                outputDesc.clearBuffer = false;
                var output = renderGraph.CreateTexture(outputDesc);
                AddCompositePass(renderGraph, material, source, current, output);
                resourceData.cameraColor = output;

                if (!loggedFirstRecord)
                {
                    loggedFirstRecord = true;
                    if (Application.isPlaying)
                    {
                        MazeDiagnosticsLog.Info("RenderGraph", "pass_record", "bloom pass recorded", MazeDiagnosticsLog.JsonNumber("mips", mipCount));
                    }
                }
            }

            private static ProfilingSampler[] CreateSamplers(string prefix, int count)
            {
                var samplers = new ProfilingSampler[count];
                for (var i = 0; i < count; i++)
                {
                    samplers[i] = new ProfilingSampler(prefix + i);
                }

                return samplers;
            }

            private static void AddSingleInputPass(RenderGraph renderGraph, string name, ProfilingSampler sampler, Material material, int shaderPass, TextureHandle input, TextureHandle output)
            {
                using var builder = renderGraph.AddRasterRenderPass<BloomPassData>(name, out var passData, sampler);
                passData.material = material;
                passData.shaderPassIndex = shaderPass;
                passData.inputTexture = input;
                passData.highTexture = TextureHandle.nullHandle;
                passData.bloomTexture = TextureHandle.nullHandle;
                builder.UseTexture(input, AccessFlags.Read);
                builder.SetRenderAttachment(output, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (BloomPassData data, RasterGraphContext context) => ExecuteSingle(data, context));
            }

            private static void AddUpsamplePass(RenderGraph renderGraph, string name, ProfilingSampler sampler, Material material, TextureHandle low, TextureHandle high, TextureHandle output)
            {
                using var builder = renderGraph.AddRasterRenderPass<BloomPassData>(name, out var passData, sampler);
                passData.material = material;
                passData.shaderPassIndex = UpsamplePass;
                passData.inputTexture = low;
                passData.highTexture = high;
                passData.bloomTexture = TextureHandle.nullHandle;
                builder.UseTexture(low, AccessFlags.Read);
                builder.UseTexture(high, AccessFlags.Read);
                builder.SetRenderAttachment(output, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (BloomPassData data, RasterGraphContext context) => ExecuteUpsample(data, context));
            }

            private static void AddCompositePass(RenderGraph renderGraph, Material material, TextureHandle source, TextureHandle bloom, TextureHandle output)
            {
                using var builder = renderGraph.AddRasterRenderPass<BloomPassData>("MAZE Bloom Composite", out var passData, CompositeSampler);
                passData.material = material;
                passData.shaderPassIndex = CompositePass;
                passData.inputTexture = source;
                passData.highTexture = TextureHandle.nullHandle;
                passData.bloomTexture = bloom;
                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(bloom, AccessFlags.Read);
                builder.SetRenderAttachment(output, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (BloomPassData data, RasterGraphContext context) => ExecuteComposite(data, context));
            }

            private static void ExecuteSingle(BloomPassData data, RasterGraphContext context)
            {
                SharedPropertyBlock.Clear();
                SharedPropertyBlock.SetTexture(BlitTextureId, data.inputTexture);
                SharedPropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                context.cmd.DrawProcedural(Matrix4x4.identity, data.material, data.shaderPassIndex, MeshTopology.Triangles, 3, 1, SharedPropertyBlock);
            }

            private static void ExecuteUpsample(BloomPassData data, RasterGraphContext context)
            {
                SharedPropertyBlock.Clear();
                SharedPropertyBlock.SetTexture(BlitTextureId, data.inputTexture);
                SharedPropertyBlock.SetTexture(BloomHighTextureId, data.highTexture);
                SharedPropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                context.cmd.DrawProcedural(Matrix4x4.identity, data.material, data.shaderPassIndex, MeshTopology.Triangles, 3, 1, SharedPropertyBlock);
            }

            private static void ExecuteComposite(BloomPassData data, RasterGraphContext context)
            {
                SharedPropertyBlock.Clear();
                SharedPropertyBlock.SetTexture(BlitTextureId, data.inputTexture);
                SharedPropertyBlock.SetTexture(BloomTextureId, data.bloomTexture);
                SharedPropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                context.cmd.DrawProcedural(Matrix4x4.identity, data.material, data.shaderPassIndex, MeshTopology.Triangles, 3, 1, SharedPropertyBlock);
            }

            private void LogInvalidSource()
            {
                if (loggedInvalidSource) return;
                loggedInvalidSource = true;
                Debug.LogWarning("[MAZE BLOOM] Skipping Bloom pass: active color texture is invalid.");
            }

            private void LogBackBuffer()
            {
                if (loggedBackBuffer) return;
                loggedBackBuffer = true;
                Debug.LogWarning("[MAZE BLOOM] Skipping Bloom pass: active target is the backbuffer and cannot be sampled safely.");
            }

            private void LogMaterial()
            {
                if (loggedMaterial) return;
                loggedMaterial = true;
                Debug.LogWarning("[MAZE BLOOM] Skipping Bloom pass: material or shader passes are missing.");
            }

            private sealed class BloomPassData
            {
                public Material material;
                public int shaderPassIndex;
                public TextureHandle inputTexture;
                public TextureHandle highTexture;
                public TextureHandle bloomTexture;
            }
        }

        [SerializeField] private Shader shader;
        private readonly Dictionary<EntityId, Material> cameraMaterials = new();
        private readonly Dictionary<EntityId, BloomPass> cameraPasses = new();

        public override void Create()
        {
            shader ??= Shader.Find("Hidden/Maze/Rendering/Bloom");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (shader == null || renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.isPreviewCamera)
            {
                return;
            }

            var camera = renderingData.cameraData.camera;
            if (!MazeSceneFxCameraPolicy.Allows(camera, MazeSceneFxPassMask.Bloom))
            {
                return;
            }

            var controller = MazeBloomController.ResolveForCamera(camera);
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
                cameraMaterial.name = camera != null ? $"MAZE Bloom ({camera.name})" : "MAZE Bloom";
                cameraMaterials[key] = cameraMaterial;
            }

            return cameraMaterial;
        }

        private BloomPass GetPass(Camera camera, Material material)
        {
            var key = camera != null ? camera.GetEntityId() : EntityId.None;
            if (!cameraPasses.TryGetValue(key, out var cameraPass) || cameraPass == null)
            {
                cameraPass = new BloomPass(material);
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
