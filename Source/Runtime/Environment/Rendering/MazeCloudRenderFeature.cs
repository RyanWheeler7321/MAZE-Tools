using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Maze
{
    public sealed class MazeCloudRenderFeature : ScriptableRendererFeature
    {
        private const int VolumePassIndex = 0;
        private const int FocusMaskPassIndex = 1;
        private const int ShadowPassIndex = 3;

        private sealed class VolumePass : MazeRenderGraphFullscreenPass
        {
            public VolumePass(Material material)
                : base("MAZE Volumetric Clouds", material, VolumePassIndex,
                    (RenderPassEvent)((int)RenderPassEvent.AfterRenderingSkybox + 1),
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

        private sealed class ShadowPass : MazeRenderGraphFullscreenPass
        {
            public ShadowPass(Material material)
                : base("MAZE Cloud Shadows", material, ShadowPassIndex,
                    (RenderPassEvent)((int)RenderPassEvent.AfterRenderingSkybox + 2),
                    ScriptableRenderPassInput.Depth, false)
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

        private sealed class CardPass : ScriptableRenderPass
        {
            private readonly ProfilingSampler sampler = new("MAZE Cloud Cards");
            private Material material;
            private MazeCloudController controller;
            private bool drawDistant;
            private bool drawOverhead;
            private readonly MaterialPropertyBlock distantBlock = new();
            private readonly MaterialPropertyBlock overheadBlock = new();

            public CardPass(Material material)
            {
                this.material = material;
                renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingSkybox + 3);
                ConfigureInput(ScriptableRenderPassInput.Depth);
                profilingSampler = sampler;
            }

            public void Setup(Material newMaterial, MazeCloudController newController, bool newDrawDistant, bool newDrawOverhead)
            {
                material = newMaterial;
                controller = newController;
                drawDistant = newDrawDistant;
                drawOverhead = newDrawOverhead;
                distantBlock.Clear();
                overheadBlock.Clear();
                controller?.ApplyCardsToPropertyBlock(distantBlock, false);
                controller?.ApplyCardsToPropertyBlock(overheadBlock, true);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null || controller == null || !controller.HasCards)
                {
                    return;
                }

                var profile = controller.Profile;
                var resourceData = frameData.Get<UniversalResourceData>();
                if (profile == null || !resourceData.activeColorTexture.IsValid())
                {
                    return;
                }

                var overheadMeshes = profile.generatedOverheadMeshes;
                using var builder = renderGraph.AddRasterRenderPass<CardPassData>("MAZE Cloud Cards", out var passData, sampler);
                passData.material = material;
                passData.distantMesh = drawDistant && controller.HasDistantCards ? profile.generatedDistantMesh : null;
                passData.overheadMeshes = drawOverhead && controller.HasOverheadCards ? overheadMeshes : null;
                passData.distantBlock = distantBlock;
                passData.overheadBlock = overheadBlock;
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                if (resourceData.activeDepthTexture.IsValid())
                {
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                }
                else if (resourceData.cameraDepthTexture.IsValid())
                {
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                }

                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (CardPassData data, RasterGraphContext context) => Execute(data, context));

                var draws = (passData.distantMesh != null ? 1 : 0) + CountMeshes(passData.overheadMeshes);
                MazeRenderFrameServices.RecordPass("MAZE Cloud Cards", MazeRenderPassKind.GeometryRedraw, draws);
            }

            private static void Execute(CardPassData data, RasterGraphContext context)
            {
                if (data.material == null)
                {
                    return;
                }

                if (data.distantMesh != null)
                {
                    context.cmd.DrawMesh(data.distantMesh, Matrix4x4.identity, data.material, 0, 0, data.distantBlock);
                }

                if (data.overheadMeshes == null)
                {
                    return;
                }

                // The four meshes are distance strata. Drawing the farthest first gives
                // stable bounded transparency ordering without a per-frame sort.
                for (var i = data.overheadMeshes.Length - 1; i >= 0; i--)
                {
                    var mesh = data.overheadMeshes[i];
                    if (mesh != null)
                    {
                        context.cmd.DrawMesh(mesh, Matrix4x4.identity, data.material, 0, 0, data.overheadBlock);
                    }
                }
            }

            private static int CountMeshes(Mesh[] meshes)
            {
                var count = 0;
                if (meshes == null)
                {
                    return count;
                }

                for (var i = 0; i < meshes.Length; i++)
                {
                    if (meshes[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }

            private sealed class CardPassData
            {
                public Material material;
                public Mesh distantMesh;
                public Mesh[] overheadMeshes;
                public MaterialPropertyBlock distantBlock;
                public MaterialPropertyBlock overheadBlock;
            }
        }

        private sealed class FocusMaskPass : ScriptableRenderPass
        {
            private readonly ProfilingSampler sampler = new("MAZE Cloud Focus Mask");
            private readonly MaterialPropertyBlock distantBlock = new();
            private readonly MaterialPropertyBlock overheadBlock = new();
            private Material material;
            private MazeCloudController controller;
            private bool drawDistant;
            private bool drawOverhead;
            private bool loggedMissingDepth;

            public FocusMaskPass(Material material)
            {
                this.material = material;
                // Classify cloud cards before arbitrary transparent shaders can modify active depth.
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                profilingSampler = sampler;
            }

            public void Setup(Material newMaterial, MazeCloudController newController, bool newDrawDistant, bool newDrawOverhead)
            {
                material = newMaterial;
                controller = newController;
                drawDistant = newDrawDistant;
                drawOverhead = newDrawOverhead;
                distantBlock.Clear();
                overheadBlock.Clear();
                controller?.ApplyCardsToPropertyBlock(distantBlock, false);
                controller?.ApplyCardsToPropertyBlock(overheadBlock, true);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null || material.passCount <= FocusMaskPassIndex || controller == null || !controller.HasCards)
                {
                    return;
                }

                var profile = controller.Profile;
                var resourceData = frameData.Get<UniversalResourceData>();
                if (profile == null || !resourceData.activeColorTexture.IsValid() || !resourceData.activeDepthTexture.IsValid())
                {
                    if (!loggedMissingDepth)
                    {
                        loggedMissingDepth = true;
                        Debug.LogWarning("[MAZE CLOUDS] Focus cloud classification skipped because the active depth attachment was unavailable.");
                    }

                    return;
                }

                var desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
                desc.name = "MAZE Cloud Focus Mask";
                desc.clearBuffer = true;
                desc.clearColor = Color.black;
                desc.format = GraphicsFormat.R8_UNorm;
                desc.msaaSamples = MSAASamples.None;
                desc.filterMode = FilterMode.Bilinear;
                desc.fallBackToBlackTexture = true;
                var mask = renderGraph.CreateTexture(desc);

                var frameMask = frameData.GetOrCreate<MazeFocusFrameData>();
                frameMask.CloudMask = mask;
                frameMask.CloudControllerEntityId = controller.GetEntityId();

                using var builder = renderGraph.AddRasterRenderPass<FocusMaskPassData>("MAZE Cloud Focus Mask", out var passData, sampler);
                passData.material = material;
                passData.distantMesh = drawDistant && controller.HasDistantCards ? profile.generatedDistantMesh : null;
                passData.overheadMeshes = drawOverhead && controller.HasOverheadCards ? profile.generatedOverheadMeshes : null;
                passData.distantBlock = distantBlock;
                passData.overheadBlock = overheadBlock;
                builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (FocusMaskPassData data, RasterGraphContext context) => Execute(data, context));

                var draws = (passData.distantMesh != null ? 1 : 0) + CountMeshes(passData.overheadMeshes);
                MazeRenderFrameServices.RecordPass("MAZE Cloud Focus Mask", MazeRenderPassKind.Mask, draws);
            }

            private static void Execute(FocusMaskPassData data, RasterGraphContext context)
            {
                context.cmd.ClearRenderTarget(RTClearFlags.Color, Color.black, 1f, 0);
                if (data.material == null)
                {
                    return;
                }

                if (data.distantMesh != null)
                {
                    context.cmd.DrawMesh(data.distantMesh, Matrix4x4.identity, data.material, 0, FocusMaskPassIndex, data.distantBlock);
                }

                if (data.overheadMeshes == null)
                {
                    return;
                }

                for (var i = data.overheadMeshes.Length - 1; i >= 0; i--)
                {
                    var mesh = data.overheadMeshes[i];
                    if (mesh != null)
                    {
                        context.cmd.DrawMesh(mesh, Matrix4x4.identity, data.material, 0, FocusMaskPassIndex, data.overheadBlock);
                    }
                }
            }

            private static int CountMeshes(Mesh[] meshes)
            {
                var count = 0;
                if (meshes == null)
                {
                    return count;
                }

                for (var i = 0; i < meshes.Length; i++)
                {
                    if (meshes[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }

            private sealed class FocusMaskPassData
            {
                public Material material;
                public Mesh distantMesh;
                public Mesh[] overheadMeshes;
                public MaterialPropertyBlock distantBlock;
                public MaterialPropertyBlock overheadBlock;
            }
        }

        [SerializeField] private Shader atmosphereShader;
        [SerializeField] private Shader cardShader;
        private readonly Dictionary<EntityId, Material> atmosphereMaterials = new();
        private readonly Dictionary<EntityId, Material> cardMaterials = new();
        private readonly Dictionary<EntityId, VolumePass> volumePasses = new();
        private readonly Dictionary<EntityId, ShadowPass> shadowPasses = new();
        private readonly Dictionary<EntityId, CardPass> cardPasses = new();
        private readonly Dictionary<EntityId, FocusMaskPass> focusMaskPasses = new();
        private bool loggedVolumeFocusMaskUnavailable;

        public override void Create()
        {
            atmosphereShader ??= Shader.Find("Hidden/Maze/Environment/AtmosphereComposite");
            cardShader ??= Shader.Find("Hidden/Maze/Environment/CloudCards");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if (cameraData.cameraType == CameraType.Preview || cameraData.isPreviewCamera)
            {
                return;
            }

            if (cameraData.isSceneViewCamera && !MazeSceneViewGraphicsState.ShouldRenderSkyClouds(true))
            {
                return;
            }

            var camera = cameraData.camera;
            if (!MazeSceneFxCameraPolicy.Allows(camera, MazeSceneFxPassMask.Clouds))
            {
                return;
            }

            var controller = MazeCloudController.ResolveForCamera(camera);
            if (controller == null || !controller.IsRenderingEnabled)
            {
                return;
            }

            var key = camera != null ? camera.GetEntityId() : EntityId.None;
            var sceneView = cameraData.isSceneViewCamera;
            var allowVolume = !sceneView || controller.Profile.volumetric.renderInSceneView;
            var allowFakeShadows = !sceneView || controller.Profile.fakeShadows.renderInSceneView;
            var drawDistant = controller.HasDistantCards && (!sceneView || controller.Profile.distantCards.renderInSceneView);
            var drawOverhead = controller.HasOverheadCards && (!sceneView || controller.Profile.overheadCards.renderInSceneView);
            if (((controller.HasVolumetric && allowVolume) || (controller.HasFakeShadows && allowFakeShadows)) && atmosphereShader != null)
            {
                var atmosphereMaterial = GetMaterial(atmosphereMaterials, key, atmosphereShader, "MAZE Clouds");
                controller.ApplyVolumeAndShadowToMaterial(atmosphereMaterial, allowVolume, allowFakeShadows);
                if (controller.HasVolumetric && allowVolume)
                {
                    var pass = GetPass(volumePasses, key, () => new VolumePass(atmosphereMaterial));
                    pass.Setup(atmosphereMaterial);
                    renderer.EnqueuePass(pass);
                }

                if ((controller.HasFakeShadows && allowFakeShadows) || (controller.Profile.volumetric.shadows.enabled && controller.HasVolumetric && allowVolume))
                {
                    var pass = GetPass(shadowPasses, key, () => new ShadowPass(atmosphereMaterial));
                    pass.Setup(atmosphereMaterial);
                    renderer.EnqueuePass(pass);
                }
            }

            if ((drawDistant || drawOverhead) && cardShader != null)
            {
                var cardMaterial = GetMaterial(cardMaterials, key, cardShader, "MAZE Cloud Cards");
                var pass = GetPass(cardPasses, key, () => new CardPass(cardMaterial));
                pass.Setup(cardMaterial, controller, drawDistant, drawOverhead);
                renderer.EnqueuePass(pass);

                var focus = MazeFocusController.ResolveForCamera(camera);
                var focusMaskNeeded = focus != null
                    && focus.Profile != null
                    && focus.Profile.RequiresCloudMask
                    && focus.AllowsCamera(camera, sceneView)
                    && (!sceneView || MazeSceneViewGraphicsState.ShouldRenderFocus(true));
                if (focusMaskNeeded)
                {
                    var maskPass = GetPass(focusMaskPasses, key, () => new FocusMaskPass(cardMaterial));
                    maskPass.Setup(cardMaterial, controller, drawDistant, drawOverhead);
                    renderer.EnqueuePass(maskPass);

                    if (controller.HasVolumetric && allowVolume && !loggedVolumeFocusMaskUnavailable)
                    {
                        loggedVolumeFocusMaskUnavailable = true;
                        Debug.LogWarning("[MAZE CLOUDS] Volumetric clouds do not yet provide a Focus classification mask; cloud cards are classified correctly, but active volume clouds remain depth-classified.");
                    }
                }
            }
        }

        private static Material GetMaterial(Dictionary<EntityId, Material> materials, EntityId key, Shader shader, string label)
        {
            if (!materials.TryGetValue(key, out var material) || material == null)
            {
                material = CoreUtils.CreateEngineMaterial(shader);
                material.name = label;
                materials[key] = material;
            }

            return material;
        }

        private static T GetPass<T>(Dictionary<EntityId, T> passes, EntityId key, System.Func<T> create) where T : class
        {
            if (!passes.TryGetValue(key, out var pass) || pass == null)
            {
                pass = create();
                passes[key] = pass;
            }

            return pass;
        }

        protected override void Dispose(bool disposing)
        {
            DestroyMaterials(atmosphereMaterials);
            DestroyMaterials(cardMaterials);
            volumePasses.Clear();
            shadowPasses.Clear();
            cardPasses.Clear();
            focusMaskPasses.Clear();
        }

        private static void DestroyMaterials(Dictionary<EntityId, Material> materials)
        {
            foreach (var material in materials.Values)
            {
                CoreUtils.Destroy(material);
            }

            materials.Clear();
        }
    }
}
