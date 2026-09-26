using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace Maze
{
    public abstract class MazeRenderGraphFullscreenPass : ScriptableRenderPass
    {
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly MaterialPropertyBlock SharedPropertyBlock = new();

        private readonly bool fetchActiveColor;
        private readonly string copyPassName;
        private readonly string passLabel;
        private readonly string sampleName;
        private readonly ProfilingSampler renderGraphSampler;
        private bool loggedInvalidMaterial;
        private bool loggedInvalidSource;
        private bool loggedBackBuffer;
        private bool loggedInvalidDestination;
        private bool loggedRecordException;
        private bool loggedFirstRecord;

        protected MazeRenderGraphFullscreenPass(
            string passName,
            Material material,
            int shaderPassIndex,
            RenderPassEvent passEvent,
            ScriptableRenderPassInput inputRequirements,
            bool fetchActiveColor)
        {
            this.fetchActiveColor = fetchActiveColor;
            copyPassName = passName + " Copy";
            passLabel = passName;
            sampleName = "MAZE.RenderGraph." + passName;
            renderGraphSampler = new ProfilingSampler(passName);
            profilingSampler = renderGraphSampler;
            Material = material;
            ShaderPassIndex = shaderPassIndex;
            renderPassEvent = passEvent;
            ConfigureInput(inputRequirements);
            requiresIntermediateTexture = fetchActiveColor;
        }

        protected Material Material { get; private set; }

        protected int ShaderPassIndex { get; private set; }

        public void Setup(Material material)
        {
            Material = material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using var sample = MazeProfiler.Sample(sampleName);
            try
            {
                RecordRenderGraphSafe(renderGraph, frameData);
            }
            catch (System.Exception exception)
            {
                LogRecordException(exception);
            }
        }

        private void RecordRenderGraphSafe(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (Material == null)
            {
                LogInvalidMaterial("material is missing");
                return;
            }

            if (ShaderPassIndex < 0 || ShaderPassIndex >= Material.passCount)
            {
                LogInvalidMaterial($"shader pass {ShaderPassIndex} is out of range for {Material.name} ({Material.passCount} passes)");
                return;
            }

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            if (!ShouldRecord(frameData, resourceData, cameraData))
            {
                return;
            }

            if (!loggedFirstRecord)
            {
                loggedFirstRecord = true;
                if (Application.isPlaying)
                {
                    MazeDiagnosticsLog.Info("RenderGraph", "pass_record", "fullscreen pass recorded", MazeDiagnosticsLog.JsonString("pass", passLabel));
                }
            }

            var destination = GetDestination(resourceData);
            var inputTexture = TextureHandle.nullHandle;
            if (fetchActiveColor)
            {
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

                inputTexture = source;
                var descriptor = renderGraph.GetTextureDesc(source);
                descriptor.name = passLabel + "_Output";
                descriptor.clearBuffer = false;
                destination = renderGraph.CreateTexture(descriptor);
            }

            if (!destination.IsValid())
            {
                LogInvalidDestination();
                return;
            }

            MazeRenderFrameServices.RecordPass(passLabel, MazeRenderPassKind.Fullscreen, 1);

            using var builder = renderGraph.AddRasterRenderPass<PassData>(passLabel, out var passData, renderGraphSampler);
            passData.material = Material;
            passData.shaderPassIndex = ShaderPassIndex;
            passData.inputTexture = inputTexture;

            if (inputTexture.IsValid())
            {
                builder.UseTexture(inputTexture, AccessFlags.Read);
            }

            ConfigurePass(builder, frameData, resourceData, cameraData, passData);
            builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
            builder.SetRenderFunc(static (PassData data, RasterGraphContext context) => ExecutePass(data, context));

            if (fetchActiveColor)
            {
                resourceData.cameraColor = destination;
            }
        }

        protected virtual bool ShouldRecord(ContextContainer frameData, UniversalResourceData resourceData, UniversalCameraData cameraData)
        {
            return true;
        }

        protected virtual TextureHandle GetDestination(UniversalResourceData resourceData)
        {
            return resourceData.activeColorTexture;
        }

        protected virtual void ConfigurePass(
            IRasterRenderGraphBuilder builder,
            ContextContainer frameData,
            UniversalResourceData resourceData,
            UniversalCameraData cameraData,
            PassData passData)
        {
        }

        private static void ExecutePass(PassData data, RasterGraphContext context)
        {
            SharedPropertyBlock.Clear();
            if (data.inputTexture.IsValid())
            {
                SharedPropertyBlock.SetTexture(BlitTextureId, data.inputTexture);
            }

            if (data.extraTexturePropertyId != 0)
            {
                if (data.extraTexture.IsValid())
                {
                    SharedPropertyBlock.SetTexture(data.extraTexturePropertyId, data.extraTexture);
                }
                else if (data.extraFallbackTexture != null)
                {
                    SharedPropertyBlock.SetTexture(data.extraTexturePropertyId, data.extraFallbackTexture);
                }
            }

            SharedPropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
            context.cmd.DrawProcedural(Matrix4x4.identity, data.material, data.shaderPassIndex, MeshTopology.Triangles, 3, 1, SharedPropertyBlock);
        }

        protected sealed class PassData
        {
            internal Material material;
            internal int shaderPassIndex;
            internal TextureHandle inputTexture;
            internal int extraTexturePropertyId;
            internal TextureHandle extraTexture;
            internal Texture extraFallbackTexture;
        }

        private void LogInvalidMaterial(string reason)
        {
            if (loggedInvalidMaterial)
            {
                return;
            }

            loggedInvalidMaterial = true;
            Debug.LogWarning($"[MAZE RENDER] Skipping '{passLabel}': {reason}.");
            if (Application.isPlaying)
            {
                MazeDiagnosticsLog.Warn("RenderGraph", "skip_invalid_material", "fullscreen pass skipped", MazeDiagnosticsLog.JoinData(
                    MazeDiagnosticsLog.JsonString("pass", passLabel),
                    MazeDiagnosticsLog.JsonString("reason", reason)));
            }
        }

        private void LogInvalidSource()
        {
            if (loggedInvalidSource)
            {
                return;
            }

            loggedInvalidSource = true;
            Debug.LogWarning($"[MAZE RENDER] Skipping '{passLabel}': active color texture is invalid.");
            if (Application.isPlaying)
            {
                MazeDiagnosticsLog.Warn("RenderGraph", "skip_invalid_source", "fullscreen pass skipped", MazeDiagnosticsLog.JsonString("pass", passLabel));
            }
        }

        private void LogBackBuffer()
        {
            if (loggedBackBuffer)
            {
                return;
            }

            loggedBackBuffer = true;
            Debug.LogWarning($"[MAZE RENDER] Skipping '{passLabel}': active target is the backbuffer and cannot be sampled safely.");
            if (Application.isPlaying)
            {
                MazeDiagnosticsLog.Warn("RenderGraph", "skip_backbuffer", "fullscreen pass skipped", MazeDiagnosticsLog.JsonString("pass", passLabel));
            }
        }

        private void LogInvalidDestination()
        {
            if (loggedInvalidDestination)
            {
                return;
            }

            loggedInvalidDestination = true;
            Debug.LogWarning($"[MAZE RENDER] Skipping '{passLabel}': destination color texture is invalid.");
            if (Application.isPlaying)
            {
                MazeDiagnosticsLog.Warn("RenderGraph", "skip_invalid_destination", "fullscreen pass skipped", MazeDiagnosticsLog.JsonString("pass", passLabel));
            }
        }

        private void LogRecordException(System.Exception exception)
        {
            if (loggedRecordException)
            {
                return;
            }

            loggedRecordException = true;
            Debug.LogError($"[MAZE RENDER] Skipping '{passLabel}' after Render Graph record error: {exception}");
            if (Application.isPlaying)
            {
                MazeDiagnosticsLog.Error("RenderGraph", "record_exception", "fullscreen pass record failed", MazeDiagnosticsLog.JoinData(
                    MazeDiagnosticsLog.JsonString("pass", passLabel),
                    MazeDiagnosticsLog.JsonString("exception", exception.Message)));
            }
        }
    }
}
