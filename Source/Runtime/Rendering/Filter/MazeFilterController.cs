using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Maze
{
    internal readonly struct MazeFilterObjectMaskDraw
    {
        public MazeFilterObjectMaskDraw(Renderer renderer, int submeshCount)
        {
            Renderer = renderer;
            SubmeshCount = Mathf.Max(1, submeshCount);
        }

        public Renderer Renderer { get; }
        public int SubmeshCount { get; }
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MazeFilterController : MonoBehaviour, IMazeProfilingFeatureProvider
    {
        private static readonly int LayerCountId = Shader.PropertyToID("_MazeFilter_LayerCount");
        private static readonly int TargetModeId = Shader.PropertyToID("_MazeFilter_TargetMode");
        private static readonly int TargetCountId = Shader.PropertyToID("_MazeFilter_TargetCount");
        private static readonly int ObjectMaskTexId = Shader.PropertyToID("_MazeFilter_ObjectMaskTex");
        private static readonly int SamplingId = Shader.PropertyToID("_MazeFilter_Sampling");
        private static readonly int TimeId = Shader.PropertyToID("_MazeFilter_Time");
        private static readonly int MaskAId = Shader.PropertyToID("_MazeFilter_MaskA");
        private static readonly int MaskBId = Shader.PropertyToID("_MazeFilter_MaskB");
        private static readonly int MaskCId = Shader.PropertyToID("_MazeFilter_MaskC");
        private static readonly int MaskDId = Shader.PropertyToID("_MazeFilter_MaskD");
        private static readonly int MaskEId = Shader.PropertyToID("_MazeFilter_MaskE");
        private static readonly int LayersAId = Shader.PropertyToID("_MazeFilter_LayersA");
        private static readonly int LayersBId = Shader.PropertyToID("_MazeFilter_LayersB");
        private static readonly int LayersCId = Shader.PropertyToID("_MazeFilter_LayersC");
        private static readonly int LayersDId = Shader.PropertyToID("_MazeFilter_LayersD");
        private static readonly int LayersEId = Shader.PropertyToID("_MazeFilter_LayersE");
        private static readonly int LayersFId = Shader.PropertyToID("_MazeFilter_LayersF");
        private static readonly int LayersGId = Shader.PropertyToID("_MazeFilter_LayersG");
        private static readonly int TargetsId = Shader.PropertyToID("_MazeFilter_Targets");

        private static readonly List<MazeFilterController> Controllers = new();

        [SerializeField] private MazeFilterProfile profile;
        [SerializeField] private List<MazeFilterTarget> targets = new();
        [SerializeField] private List<Transform> objectSilhouetteRoots = new();
        [SerializeField] private List<Renderer> objectSilhouetteProxyRenderers = new();

        private readonly Vector4[] layerParamsA = new Vector4[MazeFilterProfile.MaxLayers];
        private readonly Vector4[] layerParamsB = new Vector4[MazeFilterProfile.MaxLayers];
        private readonly Vector4[] layerParamsC = new Vector4[MazeFilterProfile.MaxLayers];
        private readonly Vector4[] layerParamsD = new Vector4[MazeFilterProfile.MaxLayers];
        private readonly Vector4[] layerParamsE = new Vector4[MazeFilterProfile.MaxLayers];
        private readonly Vector4[] layerParamsF = new Vector4[MazeFilterProfile.MaxLayers];
        private readonly Vector4[] layerParamsG = new Vector4[MazeFilterProfile.MaxLayers];
        private readonly Vector4[] targetParams = new Vector4[MazeFilterProfile.MaxTargets];
        private readonly List<MazeFilterObjectMaskDraw> cachedObjectMaskDrawList = new();
        private readonly HashSet<Renderer> cachedObjectMaskRendererSet = new();
        private MazeFilterObjectMaskDraw[] cachedObjectMaskDraws = System.Array.Empty<MazeFilterObjectMaskDraw>();
        private MazeFilterObjectMaskSource cachedObjectMaskSource = (MazeFilterObjectMaskSource)(-1);
        private bool objectMaskCacheDirty = true;
        private int nextObjectMaskCacheRefreshFrame;
        private string lastObjectMaskState;
        private string lastObjectMaskFallback;
        private string lastObjectMaskSourceFallback;

        public MazeFilterProfile Profile => profile;
        public IReadOnlyList<MazeFilterTarget> Targets => targets;
        public IReadOnlyList<Transform> ObjectSilhouetteRoots => objectSilhouetteRoots;
        public IReadOnlyList<Renderer> ObjectSilhouetteProxyRenderers => objectSilhouetteProxyRenderers;
        public bool IsRenderingEnabled => isActiveAndEnabled && profile != null && profile.RendersFilters;
        public bool RequiresDepth => IsRenderingEnabled && profile.RequiresDepth;
        public bool RequiresTargets => IsRenderingEnabled && profile.RequiresTargets;
        public bool RequiresObjectSilhouette => IsRenderingEnabled && profile.RequiresObjectSilhouette;
        public bool RequiresObjectMaskEdgeField => RequiresObjectSilhouette && profile.objectSilhouetteMode != MazeFilterObjectSilhouetteMode.Filled;
        public float Sampling => profile != null ? Mathf.Clamp(profile.sampling, 0.25f, 1f) : 1f;
        public float MaskFidelity => profile != null ? Mathf.Clamp(profile.maskFidelity, 0.25f, 1f) : 1f;
        public float ObjectMaskResolutionScale => profile != null ? Mathf.Clamp(profile.objectMaskResolutionScale * MaskFidelity, 0.02f, 1f) : 0.5f;
        public float ObjectMaskDepthBias => profile != null ? Mathf.Clamp(profile.objectDepthBias, 0f, 0.1f) : 0.03f;
        public string ProfilingLabel => profile != null ? $"MAZE Filter ({profile.name})" : $"MAZE Filter ({name})";
        public int StackOrder => profile != null ? profile.stackOrder : 0;
        public RenderPassEvent FilterRenderPassEvent
        {
            get
            {
                if (profile == null)
                {
                    return RenderPassEvent.AfterRenderingPostProcessing;
                }

                return profile.applyOrder switch
                {
                    MazeFilterApplyOrder.EarlyBeforeSceneFX => RenderPassEvent.AfterRenderingOpaques,
                    MazeFilterApplyOrder.AfterSceneFXBeforePost => RenderPassEvent.BeforeRenderingPostProcessing,
                    _ => RenderPassEvent.AfterRenderingPostProcessing,
                };
            }
        }

        public static void CollectForCamera(Camera camera, bool isSceneViewCamera, List<MazeFilterController> results)
        {
            results.Clear();
            CleanupNullControllers();
            var cameraScene = camera != null ? camera.gameObject.scene : default;
            var activeScene = SceneManager.GetActiveScene();

            for (var i = 0; i < Controllers.Count; i++)
            {
                var controller = Controllers[i];
                if (controller == null || !controller.IsRenderingEnabled || !controller.AllowsCamera(camera, isSceneViewCamera))
                {
                    continue;
                }

                var scene = controller.gameObject.scene;
                if (camera != null && scene.IsValid() && cameraScene.IsValid())
                {
                    if (scene == cameraScene)
                    {
                        results.Add(controller);
                    }
                }
                else if (scene.IsValid() && scene == activeScene)
                {
                    results.Add(controller);
                }
            }

            results.Sort(CompareControllers);
        }

        private static int CompareControllers(MazeFilterController a, MazeFilterController b)
        {
            var order = a.StackOrder.CompareTo(b.StackOrder);
            if (order != 0)
            {
                return order;
            }

            var aScene = a.gameObject.scene.path;
            var bScene = b.gameObject.scene.path;
            var sceneOrder = string.CompareOrdinal(aScene, bScene);
            if (sceneOrder != 0)
            {
                return sceneOrder;
            }

            return a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex());
        }

        private void OnEnable()
        {
            InvalidateObjectMaskCache();
            if (!Controllers.Contains(this))
            {
                Controllers.Add(this);
            }
        }

        private void OnDisable()
        {
            Controllers.Remove(this);
            InvalidateObjectMaskCache();
        }

        private void OnValidate()
        {
            InvalidateObjectMaskCache();
            if (!Controllers.Contains(this) && isActiveAndEnabled)
            {
                Controllers.Add(this);
            }
        }

        public bool AllowsCamera(Camera camera, bool isSceneViewCamera)
        {
            return !isSceneViewCamera || profile == null || profile.renderInSceneView;
        }

        public void ApplyToMaterial(Material material, Camera camera, bool isSceneViewCamera)
        {
            if (material == null || profile == null)
            {
                return;
            }

            var layerCount = FillLayers();
            var targetCount = RequiresTargets ? FillTargets(camera) : ClearTargets();
            material.SetFloat(LayerCountId, layerCount);
            material.SetFloat(TargetModeId, (float)profile.targetMode);
            material.SetFloat(TargetCountId, targetCount);
            material.SetFloat(SamplingId, Sampling);
            material.SetFloat(TimeId, Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartup);
            material.SetVector(MaskAId, new Vector4(
                Mathf.Max(0f, profile.depthStart),
                Mathf.Max(profile.depthStart + 0.001f, profile.depthEnd),
                Mathf.Max(0.01f, profile.depthSoftness),
                profile.targetMode == MazeFilterTargetMode.ObjectSilhouette ? Mathf.Max(0.2f, profile.objectEdgeCurve) : Mathf.Clamp01(profile.skyAmount)));
            material.SetVector(MaskBId, new Vector4(
                Mathf.Max(0f, profile.screenEdgeWidthPixels),
                Mathf.Max(0f, profile.screenEdgeSoftnessPixels),
                (float)profile.regionShape,
                Mathf.Max(0f, profile.regionSoftness)));
            material.SetVector(MaskCId, new Vector4(
                profile.regionCenter.x,
                profile.regionCenter.y,
                Mathf.Max(0.001f, profile.regionSize.x),
                Mathf.Max(0.001f, profile.regionSize.y)));
            material.SetVector(MaskDId, new Vector4(
                Mathf.Max(0f, profile.maskStrength),
                Mathf.Max(0.01f, profile.maskContrast),
                profile.maskBias,
                profile.invertMask ? 1f : 0f));
            material.SetVector(MaskEId, new Vector4(
                profile.regionRotationDegrees,
                (float)profile.objectSilhouetteMode,
                Mathf.Max(0f, profile.objectExtensionPixels),
                Mathf.Max(0.25f, profile.objectSoftnessPixels)));
            material.SetVectorArray(LayersAId, layerParamsA);
            material.SetVectorArray(LayersBId, layerParamsB);
            material.SetVectorArray(LayersCId, layerParamsC);
            material.SetVectorArray(LayersDId, layerParamsD);
            material.SetVectorArray(LayersEId, layerParamsE);
            material.SetVectorArray(LayersFId, layerParamsF);
            material.SetVectorArray(LayersGId, layerParamsG);
            material.SetVectorArray(TargetsId, targetParams);
            LogState(layerCount, targetCount);
        }

        private int FillLayers()
        {
            var count = 0;
            var sourceLayers = profile.layers;
            var limit = sourceLayers != null ? Mathf.Min(sourceLayers.Count, MazeFilterProfile.MaxLayers) : 0;
            for (var i = 0; i < limit; i++)
            {
                var layer = sourceLayers[i];
                if (layer == null || !layer.enabled || layer.intensity <= 0.001f || layer.sourceBlend <= 0.001f)
                {
                    continue;
                }

                var modeParam = layer.colorShift;
                var sampleOrModeParam = (float)EffectiveSampleCount(layer.blurSamples);
                var toneParam = Mathf.Max(0f, layer.contrast);
                var angleParam = layer.angleDegrees;
                var layerParamC0 = Mathf.Max(0f, layer.blurRadiusPixels);
                var layerParamC3 = layer.gridPushPixels;
                var layerParamD0 = Mathf.Max(1f, layer.colorSteps);

                switch (layer.effect)
                {
                    case MazeFilterEffectType.Pixelize:
                        modeParam = (float)layer.pixelShape;
                        toneParam = layer.colorShift;
                        angleParam = layer.rowShear;
                        sampleOrModeParam = Mathf.Clamp(layer.sizeVariation, 0f, 10f);
                        layerParamC0 = Mathf.Clamp(layer.tileSizeMinMultiplier, 1f, 8f);
                        layerParamD0 = Mathf.Clamp(layer.tileSizeMaxMultiplier, layerParamC0, 8f);
                        break;
                    case MazeFilterEffectType.HalftoneShade:
                        modeParam = (float)layer.halftoneShape;
                        sampleOrModeParam = (float)layer.toneIsolation;
                        angleParam = layer.rowShear;
                        layerParamC0 = Mathf.Clamp01(layer.halftoneAreaThreshold);
                        layerParamC3 = Mathf.Clamp(layer.halftoneAreaSoftness, 0.001f, 1f);
                        break;
                    case MazeFilterEffectType.ClarityHighPass:
                        modeParam = (float)layer.blendMode;
                        sampleOrModeParam = EffectiveSampleCount(layer.blurSamples);
                        toneParam = Mathf.Max(0f, layer.contrast);
                        layerParamC0 = Mathf.Max(0f, layer.blurRadiusPixels);
                        layerParamC3 = Mathf.Clamp(layer.highPassSoftness, 0.001f, 0.1f);
                        layerParamD0 = layer.highPassLumaOnly ? 1f : 0f;
                        break;
                    case MazeFilterEffectType.ColorFill:
                        modeParam = (float)layer.blendMode;
                        sampleOrModeParam = (float)layer.gradientMode;
                        angleParam = layer.angleDegrees;
                        break;
                    case MazeFilterEffectType.ChromaticSlip:
                        modeParam = (float)layer.slipPalette;
                        break;
                }

                layerParamsA[count] = new Vector4((float)layer.effect, modeParam, Mathf.Clamp01(layer.intensity), Mathf.Clamp01(layer.sourceBlend));
                layerParamsB[count] = new Vector4(Mathf.Max(1f, layer.cellSizePixels), Mathf.Max(0f, layer.jitter), layer.seed, Mathf.Max(0f, layer.timeSpeed));
                layerParamsC[count] = new Vector4(layerParamC0, sampleOrModeParam, layer.effect == MazeFilterEffectType.ClarityHighPass ? Mathf.Clamp(layer.highPassThreshold, 0f, 0.1f) : Mathf.Clamp01(layer.gridEdgeWidth), layerParamC3);
                layerParamsD[count] = new Vector4(
                    layerParamD0,
                    toneParam,
                    Mathf.Clamp01(layer.softness),
                    angleParam);
                layerParamsE[count] = new Vector4(
                    layer.rotationDegrees,
                    Mathf.Max(0.01f, layer.shapeAspect),
                    layer.effect == MazeFilterEffectType.Pixelize ? layer.motionRange : layer.effect is MazeFilterEffectType.ClarityHighPass or MazeFilterEffectType.ColorFill ? (float)layer.blendMode : (float)layer.colorMode,
                    layer.gradientAngleDegrees);
                layerParamsF[count] = new Vector4(
                    layer.primaryColor.r,
                    layer.primaryColor.g,
                    layer.primaryColor.b,
                    layer.primaryColor.a);
                layerParamsG[count] = new Vector4(
                    layer.secondaryColor.r,
                    layer.secondaryColor.g,
                    layer.secondaryColor.b,
                    layer.gradientOffset);
                count++;
            }

            for (var i = count; i < MazeFilterProfile.MaxLayers; i++)
            {
                layerParamsA[i] = Vector4.zero;
                layerParamsB[i] = Vector4.zero;
                layerParamsC[i] = Vector4.zero;
                layerParamsD[i] = Vector4.zero;
                layerParamsE[i] = Vector4.zero;
                layerParamsF[i] = Vector4.zero;
                layerParamsG[i] = Vector4.zero;
            }

            return count;
        }


        internal MazeFilterObjectMaskDraw[] GetObjectMaskDraws()
        {
            var source = profile != null ? profile.objectMaskSource : MazeFilterObjectMaskSource.ExactRenderers;
            if (objectMaskCacheDirty || cachedObjectMaskSource != source || Application.isPlaying && Time.frameCount >= nextObjectMaskCacheRefreshFrame)
            {
                RebuildObjectMaskCache(source);
            }

            return cachedObjectMaskDraws;
        }

        public void InvalidateObjectMaskCache()
        {
            objectMaskCacheDirty = true;
        }

        private void RebuildObjectMaskCache(MazeFilterObjectMaskSource source)
        {
            cachedObjectMaskDrawList.Clear();
            cachedObjectMaskRendererSet.Clear();
            cachedObjectMaskSource = source;

            if (source == MazeFilterObjectMaskSource.ProxyRenderers)
            {
                if (objectSilhouetteProxyRenderers != null)
                {
                    for (var i = 0; i < objectSilhouetteProxyRenderers.Count; i++)
                    {
                        AddObjectMaskRenderer(objectSilhouetteProxyRenderers[i]);
                    }
                }

                if (cachedObjectMaskDrawList.Count == 0)
                {
                    LogObjectMaskSourceFallback("Proxy Renderers is selected but no proxy renderer is assigned; using Exact Renderers");
                    CollectExactObjectMaskRenderers();
                }
            }
            else
            {
                CollectExactObjectMaskRenderers();
            }

            if (cachedObjectMaskDraws.Length != cachedObjectMaskDrawList.Count)
            {
                System.Array.Resize(ref cachedObjectMaskDraws, cachedObjectMaskDrawList.Count);
            }

            cachedObjectMaskDrawList.CopyTo(cachedObjectMaskDraws);
            objectMaskCacheDirty = false;
            nextObjectMaskCacheRefreshFrame = Time.frameCount + 120;
        }

        private void CollectExactObjectMaskRenderers()
        {
            if (objectSilhouetteRoots == null)
            {
                return;
            }

            using var pooled = UnityEngine.Pool.ListPool<Renderer>.Get(out var rootRenderers);
            for (var i = 0; i < objectSilhouetteRoots.Count; i++)
            {
                var root = objectSilhouetteRoots[i];
                if (root == null)
                {
                    continue;
                }

                rootRenderers.Clear();
                root.GetComponentsInChildren(true, rootRenderers);
                for (var r = 0; r < rootRenderers.Count; r++)
                {
                    AddObjectMaskRenderer(rootRenderers[r]);
                }
            }
        }

        private void AddObjectMaskRenderer(Renderer renderer)
        {
            if (renderer == null || !cachedObjectMaskRendererSet.Add(renderer))
            {
                return;
            }

            var submeshCount = 1;
            if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                submeshCount = skinned.sharedMesh.subMeshCount;
            }
            else if (renderer.TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh != null)
            {
                submeshCount = meshFilter.sharedMesh.subMeshCount;
            }
            else
            {
                using var pooled = UnityEngine.Pool.ListPool<Material>.Get(out var materials);
                renderer.GetSharedMaterials(materials);
                submeshCount = materials.Count;
            }

            cachedObjectMaskDrawList.Add(new MazeFilterObjectMaskDraw(renderer, submeshCount));
        }

        private int EffectiveSampleCount(int authoredSamples)
        {
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(authoredSamples, 1, 12) * Sampling), 1, 12);
        }

        private void LogObjectMaskSourceFallback(string reason)
        {
            if (reason == lastObjectMaskSourceFallback)
            {
                return;
            }

            lastObjectMaskSourceFallback = reason;
            Debug.LogWarning($"[MAZE FILTER] Object mask source fallback on '{name}': {reason}.");
            if (Application.isPlaying)
            {
                MazeDiagnosticsLog.Warn("Graphics.Filter", "object_mask_source_fallback", "filter object mask source fell back", MazeDiagnosticsLog.JsonString("reason", reason));
            }
        }

        public static int ObjectMaskTextureId => ObjectMaskTexId;

        public void LogObjectMaskState(int rendererCount, int maskWidth, int maskHeight, bool depthUsed, bool shaderDepth)
        {
            if (profile == null)
            {
                return;
            }

            var rootCount = 0;
            if (objectSilhouetteRoots != null)
            {
                for (var i = 0; i < objectSilhouetteRoots.Count; i++)
                {
                    if (objectSilhouetteRoots[i] != null)
                    {
                        rootCount++;
                    }
                }
            }

            var depthMode = !depthUsed ? "off" : shaderDepth ? "texture" : "attachment";
            var state = $"{profile.name}|{profile.objectSilhouetteMode}|{profile.objectOcclusion}|{profile.objectMaskSource}|{ObjectMaskResolutionScale:0.###}|{rootCount}|{rendererCount}|{depthMode}";
            if (state == lastObjectMaskState)
            {
                return;
            }

            lastObjectMaskState = state;
            Debug.Log($"[MAZE FILTER] Object mask '{name}' profile='{profile.name}' area={profile.objectSilhouetteMode} occlusion={profile.objectOcclusion} source={profile.objectMaskSource} roots={rootCount} renderers={rendererCount} mask={maskWidth}x{maskHeight} scale={ObjectMaskResolutionScale:0.##} depth={depthMode}");
            if (Application.isPlaying)
            {
                MazeDiagnosticsLog.InfoOnChange(
                    "graphics.filter.object_mask:" + GetEntityId(),
                    state,
                    "Graphics.Filter",
                    "object_mask",
                    "filter object mask recorded",
                    MazeDiagnosticsLog.JoinData(
                        MazeDiagnosticsLog.JsonString("profile", profile.name),
                        MazeDiagnosticsLog.JsonString("area", profile.objectSilhouetteMode.ToString()),
                        MazeDiagnosticsLog.JsonString("occlusion", profile.objectOcclusion.ToString()),
                        MazeDiagnosticsLog.JsonNumber("roots", rootCount),
                        MazeDiagnosticsLog.JsonNumber("renderers", rendererCount),
                        MazeDiagnosticsLog.JsonNumber("maskWidth", maskWidth),
                        MazeDiagnosticsLog.JsonNumber("maskHeight", maskHeight),
                        MazeDiagnosticsLog.JsonString("source", profile.objectMaskSource.ToString()),
                        MazeDiagnosticsLog.JsonNumber("resolutionScale", ObjectMaskResolutionScale),
                        MazeDiagnosticsLog.JsonNumber("sampling", Sampling),
                        MazeDiagnosticsLog.JsonString("depth", depthMode)));
            }
        }

        public void LogObjectMaskFallback(string reason)
        {
            if (profile == null)
            {
                return;
            }

            var state = $"{profile.name}|{reason}";
            if (state == lastObjectMaskFallback)
            {
                return;
            }

            lastObjectMaskFallback = state;
            Debug.LogWarning($"[MAZE FILTER] Object mask fallback on '{name}' profile='{profile.name}': {reason}. Binding black mask so the effect cannot hit the whole screen.");
            if (Application.isPlaying)
            {
                MazeDiagnosticsLog.Warn("Graphics.Filter", "object_mask_fallback", "filter object mask fell back to black", MazeDiagnosticsLog.JoinData(
                    MazeDiagnosticsLog.JsonString("profile", profile.name),
                    MazeDiagnosticsLog.JsonString("reason", reason)));
            }
        }

        private int FillTargets(Camera camera)
        {
            var count = 0;
            var sourceTargets = targets;
            var limit = sourceTargets != null ? Mathf.Min(sourceTargets.Count, MazeFilterProfile.MaxTargets) : 0;
            for (var i = 0; i < limit; i++)
            {
                var target = sourceTargets[i];
                if (target != null && target.TryWriteScreenData(camera, out var data))
                {
                    targetParams[count++] = data;
                }
            }

            for (var i = count; i < MazeFilterProfile.MaxTargets; i++)
            {
                targetParams[i] = Vector4.zero;
            }

            return count;
        }

        private int ClearTargets()
        {
            for (var i = 0; i < MazeFilterProfile.MaxTargets; i++)
            {
                targetParams[i] = Vector4.zero;
            }

            return 0;
        }

        private void LogState(int layerCount, int targetCount)
        {
            if (!Application.isPlaying || profile == null)
            {
                return;
            }

            var state = $"{profile.filterEnabled}|{profile.applyOrder}|{profile.stackOrder}|{profile.targetMode}|{layerCount}|{targetCount}|{profile.RequiresDepth}|{Sampling:0.###}|{MaskFidelity:0.###}";
            MazeDiagnosticsLog.InfoOnChange(
                "graphics.filter.state:" + GetEntityId(),
                state,
                "Graphics.Filter",
                "state",
                "filter material state applied",
                MazeDiagnosticsLog.JoinData(
                    MazeDiagnosticsLog.JsonBool("enabled", profile.filterEnabled),
                    MazeDiagnosticsLog.JsonString("applyOrder", profile.applyOrder.ToString()),
                    MazeDiagnosticsLog.JsonNumber("stackOrder", profile.stackOrder),
                    MazeDiagnosticsLog.JsonString("targetMode", profile.targetMode.ToString()),
                    MazeDiagnosticsLog.JsonNumber("layers", layerCount),
                    MazeDiagnosticsLog.JsonNumber("targets", targetCount),
                    MazeDiagnosticsLog.JsonBool("requiresDepth", profile.RequiresDepth),
                    MazeDiagnosticsLog.JsonNumber("sampling", Sampling),
                    MazeDiagnosticsLog.JsonNumber("maskFidelity", MaskFidelity)));
        }

        private static void CleanupNullControllers()
        {
            for (var i = Controllers.Count - 1; i >= 0; i--)
            {
                if (Controllers[i] == null)
                {
                    Controllers.RemoveAt(i);
                }
            }
        }

        public void AddMazeProfilingFeatures(List<MazeProfilingFeatureHandle> features)
        {
            if (profile == null)
            {
                return;
            }

            features.Add(new MazeProfilingFeatureHandle(
                "rendering.filter:" + GetEntityId(),
                profile.name,
                "Rendering",
                () => IsRenderingEnabled ? $"{profile.ActiveLayerCount} layers {profile.targetMode}" : "off",
                enabled => profile.filterEnabled = enabled,
                () => profile.filterEnabled,
                state =>
                {
                    if (state is bool value)
                    {
                        profile.filterEnabled = value;
                    }
                },
                "off/layers"));
        }
    }
}
