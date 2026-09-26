using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Maze
{
    public sealed class MazeToneRenderFeature : ScriptableRendererFeature
    {
        private sealed class TonePass : MazeRenderGraphFullscreenPass
        {
            private MazeToneController controller;

            public TonePass(Material material)
                : base(
                    "MAZE Tone",
                    material,
                    0,
                    RenderPassEvent.BeforeRenderingPostProcessing,
                    ScriptableRenderPassInput.None,
                    true)
            {
            }

            public void Setup(Material material, MazeToneController controller)
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

                controller.ApplyToMaterial(Material, camera, cameraData.isSceneViewCamera);
                return true;
            }
        }

        [SerializeField] private Shader shader;
        private Material material;
        private TonePass pass;

        public override void Create()
        {
            shader ??= Shader.Find("Hidden/Maze/Rendering/Tone");
            EnsureMaterial();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection || cameraData.isPreviewCamera)
            {
                return;
            }

            if (!EnsureMaterial())
            {
                return;
            }

            var camera = cameraData.camera;
            if (!MazeSceneFxCameraPolicy.Allows(camera, MazeSceneFxPassMask.Tone))
            {
                return;
            }

            var controller = MazeToneController.ResolveForCamera(camera);
            if (controller == null || !controller.IsRenderingEnabled || !controller.AllowsCamera(camera, cameraData.isSceneViewCamera))
            {
                return;
            }

            pass ??= new TonePass(material);
            pass.Setup(material, controller);
            renderer.EnqueuePass(pass);
        }

        private bool EnsureMaterial()
        {
            shader ??= Shader.Find("Hidden/Maze/Rendering/Tone");
            if (shader == null)
            {
                return false;
            }

            if (material == null)
            {
                material = CoreUtils.CreateEngineMaterial(shader);
                material.name = "MAZE Tone";
            }

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
            pass = null;
        }
    }
}
