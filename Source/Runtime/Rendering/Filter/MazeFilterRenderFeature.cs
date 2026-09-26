using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Maze
{
    public sealed class MazeFilterRenderFeature : ScriptableRendererFeature
    {
        private sealed class ObjectMaskFrameData : ContextItem
        {
            private const int MaxControllers = 16;
            private readonly EntityId[] controllerIds = new EntityId[MaxControllers];
            private readonly TextureHandle[] rawTextures = new TextureHandle[MaxControllers];
            private readonly TextureHandle[] edgeTextures = new TextureHandle[MaxControllers];
            private int count;

            public void SetRaw(EntityId controllerId, TextureHandle texture)
            {
                var index = FindOrAdd(controllerId);
                if (index >= 0)
                {
                    rawTextures[index] = texture;
                }
            }

            public void SetEdge(EntityId controllerId, TextureHandle texture)
            {
                var index = FindOrAdd(controllerId);
                if (index >= 0)
                {
                    edgeTextures[index] = texture;
                }
            }

            public bool TryGet(EntityId controllerId, bool preferEdge, out TextureHandle texture)
            {
                for (var i = 0; i < count; i++)
                {
                    if (controllerIds[i] != controllerId)
                    {
                        continue;
                    }

                    texture = preferEdge && edgeTextures[i].IsValid() ? edgeTextures[i] : rawTextures[i];
                    return texture.IsValid();
                }

                texture = TextureHandle.nullHandle;
                return false;
            }

            public bool HasEdge(EntityId controllerId)
            {
                for (var i = 0; i < count; i++)
                {
                    if (controllerIds[i] == controllerId)
                    {
                        return edgeTextures[i].IsValid();
                    }
                }

                return false;
            }

            public override void Reset()
            {
                for (var i = 0; i < count; i++)
                {
                    controllerIds[i] = EntityId.None;
                    rawTextures[i] = TextureHandle.nullHandle;
                    edgeTextures[i] = TextureHandle.nullHandle;
                }

                count = 0;
            }

            private int FindOrAdd(EntityId controllerId)
            {
                for (var i = 0; i < count; i++)
                {
                    if (controllerIds[i] == controllerId)
                    {
                        return i;
                    }
                }

                if (count >= MaxControllers)
                {
                    return -1;
                }

                var index = count++;
                controllerIds[index] = controllerId;
                rawTextures[index] = TextureHandle.nullHandle;
                edgeTextures[index] = TextureHandle.nullHandle;
                return index;
            }
        }

        private sealed class ObjectMaskPass : ScriptableRenderPass
        {
            private static readonly int ZTestId = Shader.PropertyToID("_ZTest");
            private static readonly int DepthParamsId = Shader.PropertyToID("_MazeFilterObjectMask_DepthParams");
            private readonly ProfilingSampler sampler;
            private readonly string markerName;
            private Material material;
            private MazeFilterController controller;

            public ObjectMaskPass(Material material, string controllerLabel)
            {
                this.material = material;
                markerName = controllerLabel + " Object Mask";
                sampler = new ProfilingSampler(markerName);
                profilingSampler = sampler;
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public void Setup(Material nextMaterial, MazeFilterController nextController)
            {
                material = nextMaterial;
                controller = nextController;
                var profile = controller != null ? controller.Profile : null;
                var useVisibleDepth = profile != null && profile.objectOcclusion == MazeFilterObjectOcclusion.VisibleOnly;
                material.SetFloat(ZTestId, useVisibleDepth ? (float)CompareFunction.LessEqual : (float)CompareFunction.Always);
                material.SetVector(DepthParamsId, Vector4.zero);
                ConfigureInput(useVisibleDepth ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null || controller == null || !controller.RequiresObjectSilhouette)
                {
                    return;
                }

                var resourceData = frameData.Get<UniversalResourceData>();
                var source = resourceData.activeColorTexture;
                if (!source.IsValid())
                {
                    return;
                }

                var desc = renderGraph.GetTextureDesc(source);
                var scale = controller.ObjectMaskResolutionScale;
                desc.name = markerName;
                desc.clearBuffer = true;
                desc.clearColor = Color.black;
                desc.format = GraphicsFormat.R8_UNorm;
                desc.width = Mathf.Max(1, Mathf.CeilToInt(desc.width * scale));
                desc.height = Mathf.Max(1, Mathf.CeilToInt(desc.height * scale));
                desc.msaaSamples = MSAASamples.None;
                desc.filterMode = FilterMode.Bilinear;
                desc.fallBackToBlackTexture = true;
                var mask = renderGraph.CreateTexture(desc);
                var draws = controller.GetObjectMaskDraws();
                var rendererCount = CountActiveRenderers(draws);
                var wantsVisibleOnly = controller.Profile != null
                    && controller.Profile.objectOcclusion == MazeFilterObjectOcclusion.VisibleOnly;
                var useDepthAttachment = wantsVisibleOnly && scale >= 0.999f && resourceData.activeDepthTexture.IsValid();
                var useDepthTexture = wantsVisibleOnly && !useDepthAttachment && resourceData.cameraDepthTexture.IsValid();
                material.SetFloat(ZTestId, useDepthAttachment ? (float)CompareFunction.LessEqual : (float)CompareFunction.Always);
                material.SetVector(DepthParamsId, new Vector4(useDepthTexture ? 1f : 0f, controller.ObjectMaskDepthBias, 0f, 0f));
                controller.LogObjectMaskState(rendererCount, desc.width, desc.height, useDepthAttachment || useDepthTexture, useDepthTexture);
                MazeRenderFrameServices.RecordPass(markerName, MazeRenderPassKind.Mask, 1);

                frameData.GetOrCreate<ObjectMaskFrameData>().SetRaw(ControllerFrameKey(controller), mask);

                using var builder = renderGraph.AddRasterRenderPass<ObjectMaskPassData>(markerName, out var passData, sampler);
                passData.material = material;
                passData.draws = draws;
                builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                if (useDepthAttachment)
                {
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                }
                else if (useDepthTexture)
                {
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                }

                builder.SetRenderFunc(static (ObjectMaskPassData data, RasterGraphContext context) => ExecuteObjectMaskPass(data, context));
            }

            private static int CountActiveRenderers(MazeFilterObjectMaskDraw[] draws)
            {
                if (draws == null)
                {
                    return 0;
                }

                var count = 0;
                for (var i = 0; i < draws.Length; i++)
                {
                    var renderer = draws[i].Renderer;
                    if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    {
                        count++;
                    }
                }

                return count;
            }

            private static void ExecuteObjectMaskPass(ObjectMaskPassData data, RasterGraphContext context)
            {
                if (data.draws == null || data.material == null)
                {
                    return;
                }

                for (var i = 0; i < data.draws.Length; i++)
                {
                    var draw = data.draws[i];
                    var renderer = draw.Renderer;
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    for (var submesh = 0; submesh < draw.SubmeshCount; submesh++)
                    {
                        context.cmd.DrawRenderer(renderer, data.material, submesh, 0);
                    }
                }
            }

            private sealed class ObjectMaskPassData
            {
                public Material material;
                public MazeFilterObjectMaskDraw[] draws;
            }
        }

        private sealed class ObjectEdgePass : ScriptableRenderPass
        {
            private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            private static readonly int EdgeParamsId = Shader.PropertyToID("_MazeFilterObjectEdge_Params");
            private static readonly MaterialPropertyBlock SharedPropertyBlock = new();
            private readonly ProfilingSampler sampler;
            private readonly string markerName;
            private Material material;
            private MazeFilterController controller;

            public ObjectEdgePass(Material material, string controllerLabel)
            {
                this.material = material;
                markerName = controllerLabel + " Object Edge Field";
                sampler = new ProfilingSampler(markerName);
                profilingSampler = sampler;
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            }

            public void Setup(Material nextMaterial, MazeFilterController nextController)
            {
                material = nextMaterial;
                controller = nextController;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null || controller == null || !controller.RequiresObjectSilhouette || !controller.RequiresObjectMaskEdgeField)
                {
                    return;
                }

                var maskData = frameData.GetOrCreate<ObjectMaskFrameData>();
                if (!maskData.TryGet(ControllerFrameKey(controller), false, out var rawMask))
                {
                    controller.LogObjectMaskFallback("raw object mask was not produced before edge expansion");
                    return;
                }

                var desc = renderGraph.GetTextureDesc(rawMask);
                desc.name = markerName;
                desc.clearBuffer = false;
                desc.format = GraphicsFormat.R8G8_UNorm;
                desc.filterMode = FilterMode.Bilinear;
                desc.fallBackToBlackTexture = true;
                var edgeField = renderGraph.CreateTexture(desc);
                maskData.SetEdge(ControllerFrameKey(controller), edgeField);
                MazeRenderFrameServices.RecordPass(markerName, MazeRenderPassKind.Mask, 1);

                using var builder = renderGraph.AddRasterRenderPass<ObjectEdgePassData>(markerName, out var passData, sampler);
                passData.material = material;
                passData.rawMask = rawMask;
                var profile = controller.Profile;
                passData.edgeParams = new Vector4(
                    profile != null ? Mathf.Max(0f, profile.objectExtensionPixels) : 0f,
                    profile != null ? Mathf.Max(0.25f, profile.objectSoftnessPixels) : 0.25f,
                    controller.MaskFidelity,
                    0f);
                builder.UseTexture(rawMask, AccessFlags.Read);
                builder.SetRenderAttachment(edgeField, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (ObjectEdgePassData data, RasterGraphContext context) => ExecuteEdgePass(data, context));
            }

            private static void ExecuteEdgePass(ObjectEdgePassData data, RasterGraphContext context)
            {
                SharedPropertyBlock.Clear();
                SharedPropertyBlock.SetTexture(BlitTextureId, data.rawMask);
                SharedPropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                SharedPropertyBlock.SetVector(EdgeParamsId, data.edgeParams);
                context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, SharedPropertyBlock);
            }

            private sealed class ObjectEdgePassData
            {
                public Material material;
                public TextureHandle rawMask;
                public Vector4 edgeParams;
            }
        }

        private sealed class FilterPass : MazeRenderGraphFullscreenPass
        {
            private MazeFilterController controller;
            private bool loggedMissingDepth;

            public FilterPass(Material material, string passName)
                : base(
                    passName,
                    material,
                    0,
                    RenderPassEvent.AfterRenderingPostProcessing,
                    ScriptableRenderPassInput.None,
                    true)
            {
            }

            public void Setup(Material nextMaterial, MazeFilterController nextController)
            {
                base.Setup(nextMaterial);
                controller = nextController;
                renderPassEvent = controller != null ? controller.FilterRenderPassEvent : RenderPassEvent.AfterRenderingPostProcessing;
                ConfigureInput(controller != null && controller.RequiresDepth ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);
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

                if (controller.RequiresDepth && !resourceData.cameraDepthTexture.IsValid())
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
                Debug.LogWarning("[MAZE FILTER] Skipping filter pass: camera depth texture is invalid.");
                if (Application.isPlaying)
                {
                    MazeDiagnosticsLog.Warn("Graphics.Filter", "missing_depth", "filter skipped because camera depth texture is invalid");
                }
            }

            protected override void ConfigurePass(
                IRasterRenderGraphBuilder builder,
                ContextContainer frameData,
                UniversalResourceData resourceData,
                UniversalCameraData cameraData,
                PassData passData)
            {
                if (controller != null && controller.RequiresDepth && resourceData.cameraDepthTexture.IsValid())
                {
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                }

                if (controller == null || !controller.RequiresObjectSilhouette)
                {
                    return;
                }

                passData.extraTexturePropertyId = MazeFilterController.ObjectMaskTextureId;
                passData.extraFallbackTexture = Texture2D.blackTexture;
                if (!frameData.Contains<ObjectMaskFrameData>())
                {
                    controller.LogObjectMaskFallback("mask texture was not produced this frame");
                    return;
                }

                var maskData = frameData.Get<ObjectMaskFrameData>();
                var wantsEdge = controller.RequiresObjectMaskEdgeField;
                if (!maskData.TryGet(ControllerFrameKey(controller), wantsEdge, out var maskTexture))
                {
                    controller.LogObjectMaskFallback("mask texture was not produced for this controller");
                    return;
                }

                if (wantsEdge && !maskData.HasEdge(ControllerFrameKey(controller)))
                {
                    controller.LogObjectMaskFallback("edge field was unavailable; using the raw object mask");
                }

                passData.extraTexture = maskTexture;
                builder.UseTexture(maskTexture, AccessFlags.Read);
            }
        }

        private readonly struct FilterKey : System.IEquatable<FilterKey>
        {
            public readonly EntityId Camera;
            public readonly EntityId Controller;

            public FilterKey(EntityId camera, EntityId controller)
            {
                Camera = camera;
                Controller = controller;
            }

            public bool Equals(FilterKey other)
            {
                return Camera == other.Camera && Controller == other.Controller;
            }

            public override bool Equals(object obj)
            {
                return obj is FilterKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Camera.GetHashCode() * 397) ^ Controller.GetHashCode();
                }
            }
        }

        [SerializeField] private Shader shader;
        [SerializeField] private Shader objectMaskShader;
        [SerializeField] private Shader objectEdgeShader;
        private readonly Dictionary<FilterKey, Material> passMaterials = new();
        private readonly Dictionary<FilterKey, Material> objectMaskMaterials = new();
        private readonly Dictionary<FilterKey, Material> objectEdgeMaterials = new();
        private readonly Dictionary<FilterKey, FilterPass> passes = new();
        private readonly Dictionary<FilterKey, ObjectMaskPass> objectMaskPasses = new();
        private readonly Dictionary<FilterKey, ObjectEdgePass> objectEdgePasses = new();
        private readonly List<MazeFilterController> activeControllers = new();
        private readonly HashSet<FilterKey> activeKeys = new();

        public override void Create()
        {
            shader ??= Shader.Find("Hidden/Maze/Rendering/Filter");
            objectMaskShader ??= Shader.Find("Hidden/Maze/Rendering/FilterObjectMask");
            objectEdgeShader ??= Shader.Find("Hidden/Maze/Rendering/FilterObjectEdge");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (shader == null || renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.isPreviewCamera)
            {
                return;
            }

            var camera = renderingData.cameraData.camera;
            if (!MazeSceneFxCameraPolicy.Allows(camera, MazeSceneFxPassMask.Filter))
            {
                activeKeys.Clear();
                PruneInactiveEntries(CameraKey(camera));
                return;
            }

            MazeFilterController.CollectForCamera(camera, renderingData.cameraData.isSceneViewCamera, activeControllers);
            activeKeys.Clear();
            var cameraKey = CameraKey(camera);
            for (var i = 0; i < activeControllers.Count; i++)
            {
                var controller = activeControllers[i];
                if (controller == null || !controller.IsRenderingEnabled || !controller.AllowsCamera(camera, renderingData.cameraData.isSceneViewCamera))
                {
                    continue;
                }

                var key = MakeKey(camera, controller);
                activeKeys.Add(key);
                if (controller.RequiresObjectSilhouette)
                {
                    if (objectMaskShader != null)
                    {
                        var maskMaterial = GetObjectMaskMaterial(key, camera, controller);
                        var maskPass = GetObjectMaskPass(key, maskMaterial, controller);
                        maskPass.Setup(maskMaterial, controller);
                        renderer.EnqueuePass(maskPass);
                    }
                    else
                    {
                        controller.LogObjectMaskFallback("Hidden/Maze/Rendering/FilterObjectMask shader was not found");
                    }

                    if (controller.RequiresObjectMaskEdgeField)
                    {
                        if (objectEdgeShader != null)
                        {
                            var edgeMaterial = GetObjectEdgeMaterial(key, camera, controller);
                            var edgePass = GetObjectEdgePass(key, edgeMaterial, controller);
                            edgePass.Setup(edgeMaterial, controller);
                            renderer.EnqueuePass(edgePass);
                        }
                        else
                        {
                            controller.LogObjectMaskFallback("Hidden/Maze/Rendering/FilterObjectEdge shader was not found");
                        }
                    }
                }

                var material = GetMaterial(key, camera, controller);
                var pass = GetPass(key, material, controller);
                pass.Setup(material, controller);
                renderer.EnqueuePass(pass);
            }

            PruneInactiveEntries(cameraKey);
        }

        private Material GetMaterial(FilterKey key, Camera camera, MazeFilterController controller)
        {
            if (!passMaterials.TryGetValue(key, out var material) || material == null)
            {
                material = CoreUtils.CreateEngineMaterial(shader);
                material.name = camera != null ? $"MAZE Filter ({camera.name}/{controller.name})" : $"MAZE Filter ({controller.name})";
                passMaterials[key] = material;
            }

            return material;
        }

        private Material GetObjectMaskMaterial(FilterKey key, Camera camera, MazeFilterController controller)
        {
            if (!objectMaskMaterials.TryGetValue(key, out var material) || material == null)
            {
                material = CoreUtils.CreateEngineMaterial(objectMaskShader);
                material.name = camera != null ? $"MAZE Filter Object Mask ({camera.name}/{controller.name})" : $"MAZE Filter Object Mask ({controller.name})";
                objectMaskMaterials[key] = material;
            }

            return material;
        }

        private Material GetObjectEdgeMaterial(FilterKey key, Camera camera, MazeFilterController controller)
        {
            if (!objectEdgeMaterials.TryGetValue(key, out var material) || material == null)
            {
                material = CoreUtils.CreateEngineMaterial(objectEdgeShader);
                material.name = camera != null ? $"MAZE Filter Object Edge ({camera.name}/{controller.name})" : $"MAZE Filter Object Edge ({controller.name})";
                objectEdgeMaterials[key] = material;
            }

            return material;
        }

        private ObjectMaskPass GetObjectMaskPass(FilterKey key, Material material, MazeFilterController controller)
        {
            if (!objectMaskPasses.TryGetValue(key, out var pass) || pass == null)
            {
                pass = new ObjectMaskPass(material, controller.ProfilingLabel);
                objectMaskPasses[key] = pass;
            }

            return pass;
        }

        private ObjectEdgePass GetObjectEdgePass(FilterKey key, Material material, MazeFilterController controller)
        {
            if (!objectEdgePasses.TryGetValue(key, out var pass) || pass == null)
            {
                pass = new ObjectEdgePass(material, controller.ProfilingLabel);
                objectEdgePasses[key] = pass;
            }

            return pass;
        }

        private FilterPass GetPass(FilterKey key, Material material, MazeFilterController controller)
        {
            if (!passes.TryGetValue(key, out var pass) || pass == null)
            {
                pass = new FilterPass(material, controller.ProfilingLabel);
                passes[key] = pass;
            }

            return pass;
        }

        private void PruneInactiveEntries(EntityId cameraKey)
        {
            using var materialKeys = UnityEngine.Pool.ListPool<FilterKey>.Get(out var keys);
            foreach (var key in passMaterials.Keys)
            {
                if (key.Camera == cameraKey && !activeKeys.Contains(key))
                {
                    keys.Add(key);
                }
            }

            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (passMaterials.TryGetValue(key, out var material))
                {
                    CoreUtils.Destroy(material);
                }

                if (objectMaskMaterials.TryGetValue(key, out var maskMaterial))
                {
                    CoreUtils.Destroy(maskMaterial);
                }

                if (objectEdgeMaterials.TryGetValue(key, out var edgeMaterial))
                {
                    CoreUtils.Destroy(edgeMaterial);
                }

                passMaterials.Remove(key);
                objectMaskMaterials.Remove(key);
                objectEdgeMaterials.Remove(key);
                passes.Remove(key);
                objectMaskPasses.Remove(key);
                objectEdgePasses.Remove(key);
            }
        }

        private static FilterKey MakeKey(Camera camera, MazeFilterController controller)
        {
            return new FilterKey(CameraKey(camera), ControllerFrameKey(controller));
        }

        private static EntityId CameraKey(Camera camera)
        {
            return camera != null ? camera.GetEntityId() : EntityId.None;
        }

        private static EntityId ControllerFrameKey(MazeFilterController controller)
        {
            return controller != null ? controller.GetEntityId() : EntityId.None;
        }

        protected override void Dispose(bool disposing)
        {
            foreach (var material in passMaterials.Values)
            {
                CoreUtils.Destroy(material);
            }

            foreach (var material in objectMaskMaterials.Values)
            {
                CoreUtils.Destroy(material);
            }

            foreach (var material in objectEdgeMaterials.Values)
            {
                CoreUtils.Destroy(material);
            }

            passMaterials.Clear();
            objectMaskMaterials.Clear();
            objectEdgeMaterials.Clear();
            passes.Clear();
            objectMaskPasses.Clear();
            objectEdgePasses.Clear();
            activeControllers.Clear();
            activeKeys.Clear();
        }
    }
}
