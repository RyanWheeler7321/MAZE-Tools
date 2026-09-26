using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Maze
{
    public sealed class MazeAtmosphereRenderFeature : ScriptableRendererFeature
    {
        private const int SkyOnlyPassIndex = 4;

        private sealed class SkyPass : MazeRenderGraphFullscreenPass
        {
            public SkyPass(Material material)
                : base("MAZE Atmosphere Sky", material, SkyOnlyPassIndex,
                    RenderPassEvent.AfterRenderingSkybox,
                    ScriptableRenderPassInput.Depth, true)
            {
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
        private Material material;
        private SkyPass pass;

        public override void Create()
        {
            shader ??= Shader.Find("Hidden/Maze/Environment/AtmosphereComposite");
            if (shader != null && material == null)
            {
                material = CoreUtils.CreateEngineMaterial(shader);
            }

            pass ??= new SkyPass(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.isPreviewCamera)
            {
                return;
            }

            var atmosphere = MazeAtmosphereController.Active;
            if (atmosphere == null || !atmosphere.IsRenderingEnabled || !MazeSceneViewGraphicsState.ShouldRenderSkyClouds(renderingData.cameraData.isSceneViewCamera))
            {
                return;
            }

            atmosphere.ApplyGlobalShaderState();
            pass.Setup(material);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
            pass = null;
        }
    }
}
