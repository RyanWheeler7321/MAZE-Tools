using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Maze
{
    internal static class MazeAutoProfilingCandidates
    {
        public static void AddSceneRootFeatures(List<MazeProfilingFeatureHandle> features, int maxCandidates)
        {
            AddAutomaticFeatures(features, maxCandidates);
        }

        public static void AddAutomaticFeatures(List<MazeProfilingFeatureHandle> features, int maxCandidates)
        {
            if (features == null || maxCandidates <= 0)
            {
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return;
            }

            var existingIds = new HashSet<string>(features.Select(feature => feature.Id), StringComparer.Ordinal);
            var candidates = new List<AutoCandidate>(64);

            AddRenderFeatureCandidates(candidates);
            AddLightCandidates(candidates, scene);
            AddRendererBucketCandidates(candidates, scene);
            AddScriptBucketCandidates(candidates, scene);
            AddRootAndSubtreeCandidates(candidates, scene);

            foreach (var candidate in PickPrioritizedCandidates(candidates, maxCandidates))
            {
                if (existingIds.Add(candidate.Handle.Id))
                {
                    features.Add(candidate.Handle);
                }
            }
        }

        private static List<AutoCandidate> PickPrioritizedCandidates(List<AutoCandidate> candidates, int maxCandidates)
        {
            var result = new List<AutoCandidate>(maxCandidates);
            var used = new HashSet<string>(StringComparer.Ordinal);
            AddCategory(result, used, candidates, "Render Feature", 4, maxCandidates);
            AddCategory(result, used, candidates, "Shadow Stack", 1, maxCandidates);
            AddCategory(result, used, candidates, "Light Shadows", 3, maxCandidates);
            AddCategory(result, used, candidates, "Mesh Bucket", 4, maxCandidates);
            AddCategory(result, used, candidates, "Material Bucket", 3, maxCandidates);
            AddCategory(result, used, candidates, "Script Bucket", 3, maxCandidates);
            AddCategory(result, used, candidates, "Scene Subtree", 4, maxCandidates);
            AddCategory(result, used, candidates, "Scene Root", 2, maxCandidates);

            foreach (var candidate in candidates
                         .Where(candidate => candidate.Score > 0f)
                         .OrderByDescending(candidate => candidate.Score)
                         .ThenBy(candidate => candidate.Handle.Category, StringComparer.Ordinal)
                         .ThenBy(candidate => candidate.Handle.Label, StringComparer.Ordinal))
            {
                if (result.Count >= maxCandidates)
                {
                    break;
                }

                if (used.Add(candidate.Handle.Id))
                {
                    result.Add(candidate);
                }
            }

            return result;
        }

        private static void AddCategory(List<AutoCandidate> result, HashSet<string> used, List<AutoCandidate> candidates, string category, int count, int maxCandidates)
        {
            if (result.Count >= maxCandidates)
            {
                return;
            }

            foreach (var candidate in candidates
                         .Where(candidate => candidate.Score > 0f && string.Equals(candidate.Handle.Category, category, StringComparison.Ordinal))
                         .OrderByDescending(candidate => candidate.Score)
                         .ThenBy(candidate => candidate.Handle.Label, StringComparer.Ordinal)
                         .Take(count))
            {
                if (result.Count >= maxCandidates)
                {
                    return;
                }

                if (used.Add(candidate.Handle.Id))
                {
                    result.Add(candidate);
                }
            }
        }

        private static void AddRootAndSubtreeCandidates(List<AutoCandidate> candidates, Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (!IsValidRoot(root))
                {
                    continue;
                }

                var candidate = ObjectCandidate.From(root, "Scene Root");
                if (candidate.Score <= 0)
                {
                    continue;
                }

                candidates.Add(MakeObjectCandidate(scene, candidate));

                var children = new List<ObjectCandidate>();
                for (var childIndex = 0; childIndex < root.transform.childCount; childIndex++)
                {
                    var child = root.transform.GetChild(childIndex);
                    if (child == null || !child.gameObject.activeSelf)
                    {
                        continue;
                    }

                    var childCandidate = ObjectCandidate.From(child.gameObject, "Scene Subtree");
                    if (childCandidate.Score > 0f)
                    {
                        children.Add(childCandidate);
                    }
                }

                foreach (var childCandidate in children
                             .OrderByDescending(child => child.Score)
                             .ThenBy(child => child.Target.name, StringComparer.Ordinal)
                             .Take(4))
                {
                    candidates.Add(MakeObjectCandidate(scene, childCandidate));
                }
            }
        }

        private static AutoCandidate MakeObjectCandidate(Scene scene, ObjectCandidate candidate)
        {
            var target = candidate.Target;
            var id = $"auto.{Normalize(candidate.Category)}.{Normalize(scene.name)}.{Normalize(GameObjectPath(target))}";
            var suffix = string.Equals(candidate.Category, "Scene Root", StringComparison.Ordinal) ? "root" : "subtree";
            return new AutoCandidate(candidate.Score, new MazeProfilingFeatureHandle(
                id,
                $"{GameObjectPath(target)} {suffix}",
                candidate.Category,
                () => target != null && target.activeSelf ? $"active score {candidate.Score:0}" : "inactive",
                enabled =>
                {
                    if (target != null)
                    {
                        target.SetActive(enabled);
                    }
                },
                () => target != null ? target.activeSelf : false,
                state =>
                {
                    if (target != null && state is bool active)
                    {
                        target.SetActive(active);
                    }
                }));
        }

        private static void AddRendererBucketCandidates(List<AutoCandidate> candidates, Scene scene)
        {
            var infos = CollectRendererInfos(scene);
            foreach (var bucket in infos
                         .Where(info => !info.IsSkinned && info.Mesh != null)
                         .GroupBy(info => MeshMaterialKey(info))
                         .Where(group => group.Count() >= 2)
                         .Select(group => RendererBucket.From("Mesh Bucket", group.ToList()))
                         .Where(bucket => bucket.Score > 0f)
                         .OrderByDescending(bucket => bucket.Score)
                         .Take(8))
            {
                candidates.Add(MakeRendererBucketCandidate(scene, bucket));
            }

            foreach (var bucket in infos
                         .SelectMany(info => info.Materials.Select(material => new { info, material }))
                         .Where(item => item.material != null)
                         .GroupBy(item => item.material)
                         .Where(group => group.Count() >= 2)
                         .Select(group => RendererBucket.From("Material Bucket", group.Select(item => item.info).Distinct().ToList(), group.Key))
                         .Where(bucket => bucket.Score > 0f)
                         .OrderByDescending(bucket => bucket.Score)
                         .Take(8))
            {
                candidates.Add(MakeRendererBucketCandidate(scene, bucket));
            }
        }

        private static void AddScriptBucketCandidates(List<AutoCandidate> candidates, Scene scene)
        {
            var scripts = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                .Where(script => script != null
                    && script.gameObject.scene == scene
                    && script.enabled
                    && script.gameObject.activeInHierarchy
                    && IsSafeScriptCandidate(script))
                .GroupBy(script => script.GetType())
                .Where(group => group.Count() >= 2)
                .Select(group => ScriptBucket.From(group.Key, group.ToList()))
                .Where(bucket => bucket.Score > 0f)
                .OrderByDescending(bucket => bucket.Score)
                .ThenBy(bucket => bucket.Label, StringComparer.Ordinal)
                .Take(8);

            foreach (var bucket in scripts)
            {
                candidates.Add(MakeScriptBucketCandidate(scene, bucket));
            }
        }

        private static bool IsSafeScriptCandidate(MonoBehaviour script)
        {
            if (script is MazeProfiler || script is IMazeProfilingFeatureProvider)
            {
                return false;
            }

            var type = script.GetType();
            var fullName = type.FullName ?? type.Name;
            return !fullName.Contains("Automation", StringComparison.OrdinalIgnoreCase)
                && !fullName.Contains("Diagnostics", StringComparison.OrdinalIgnoreCase)
                && !fullName.Contains("Profiler", StringComparison.OrdinalIgnoreCase);
        }

        private static AutoCandidate MakeScriptBucketCandidate(Scene scene, ScriptBucket bucket)
        {
            var id = $"auto.script_bucket.{Normalize(scene.name)}.{Normalize(bucket.Type.FullName ?? bucket.Type.Name)}";
            return new AutoCandidate(bucket.Score, new MazeProfilingFeatureHandle(
                id,
                $"{bucket.Label} ({bucket.Scripts.Count} scripts)",
                "Script Bucket",
                () => bucket.Scripts.Any(script => script != null && script.enabled && script.gameObject.activeInHierarchy)
                    ? $"enabled scripts {bucket.Scripts.Count}"
                    : "inactive",
                enabled =>
                {
                    for (var i = 0; i < bucket.Scripts.Count; i++)
                    {
                        if (bucket.Scripts[i] != null)
                        {
                            bucket.Scripts[i].enabled = enabled;
                        }
                    }
                },
                () => bucket.Scripts.Select(script => script != null && script.enabled).ToArray(),
                state =>
                {
                    if (state is not bool[] states)
                    {
                        return;
                    }

                    var count = Math.Min(states.Length, bucket.Scripts.Count);
                    for (var i = 0; i < count; i++)
                    {
                        if (bucket.Scripts[i] != null)
                        {
                            bucket.Scripts[i].enabled = states[i];
                        }
                    }
                }));
        }

        private static AutoCandidate MakeRendererBucketCandidate(Scene scene, RendererBucket bucket)
        {
            var id = $"auto.{Normalize(bucket.Category)}.{Normalize(scene.name)}.{Normalize(bucket.Key)}";
            var label = $"{bucket.Label} ({bucket.Renderers.Count} renderers)";
            return new AutoCandidate(bucket.Score, new MazeProfilingFeatureHandle(
                id,
                label,
                bucket.Category,
                () => bucket.Renderers.Any(renderer => renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    ? $"renderers {bucket.Renderers.Count} tris {bucket.TotalTriangles}"
                    : "inactive",
                enabled =>
                {
                    for (var i = 0; i < bucket.Renderers.Count; i++)
                    {
                        if (bucket.Renderers[i] != null)
                        {
                            bucket.Renderers[i].enabled = enabled;
                        }
                    }
                },
                () => bucket.Renderers.Select(renderer => renderer != null && renderer.enabled).ToArray(),
                state =>
                {
                    if (state is not bool[] states)
                    {
                        return;
                    }

                    var count = Math.Min(states.Length, bucket.Renderers.Count);
                    for (var i = 0; i < count; i++)
                    {
                        if (bucket.Renderers[i] != null)
                        {
                            bucket.Renderers[i].enabled = states[i];
                        }
                    }
                }));
        }

        private static void AddLightCandidates(List<AutoCandidate> candidates, Scene scene)
        {
            var lights = Resources.FindObjectsOfTypeAll<Light>()
                .Where(light => light != null && light.gameObject.scene == scene && light.enabled && light.gameObject.activeInHierarchy)
                .OrderBy(light => GameObjectPath(light.gameObject), StringComparer.Ordinal)
                .ToArray();

            var shadowLights = lights.Where(light => light.shadows != LightShadows.None).ToArray();
            if (shadowLights.Length > 1)
            {
                var score = shadowLights.Sum(light => EstimatedShadowFaces(light)) * 300f + shadowLights.Length * 30f;
                candidates.Add(new AutoCandidate(score, new MazeProfilingFeatureHandle(
                    $"auto.shadow_stack.{Normalize(scene.name)}",
                    "All shadowed lights",
                    "Shadow Stack",
                    () => shadowLights.Any(light => light != null && light.shadows != LightShadows.None) ? $"shadowed lights {shadowLights.Length}" : "inactive",
                    enabled =>
                    {
                        if (enabled)
                        {
                            return;
                        }

                        for (var i = 0; i < shadowLights.Length; i++)
                        {
                            if (shadowLights[i] != null)
                            {
                                shadowLights[i].shadows = LightShadows.None;
                            }
                        }
                    },
                    () => shadowLights.Select(light => light != null ? light.shadows : LightShadows.None).ToArray(),
                    state =>
                    {
                        if (state is not LightShadows[] shadows)
                        {
                            return;
                        }

                        var count = Math.Min(shadows.Length, shadowLights.Length);
                        for (var i = 0; i < count; i++)
                        {
                            if (shadowLights[i] != null)
                            {
                                shadowLights[i].shadows = shadows[i];
                            }
                        }
                    })));
            }

            foreach (var light in shadowLights)
            {
                var capturedLight = light;
                var score = EstimatedShadowFaces(light) * 250f + light.intensity * 10f;
                candidates.Add(new AutoCandidate(score, new MazeProfilingFeatureHandle(
                    $"auto.light_shadows.{Normalize(scene.name)}.{Normalize(GameObjectPath(capturedLight.gameObject))}",
                    $"{GameObjectPath(capturedLight.gameObject)} shadows",
                    "Light Shadows",
                    () => capturedLight != null && capturedLight.shadows != LightShadows.None ? $"{capturedLight.type} {capturedLight.shadows}" : "inactive",
                    enabled =>
                    {
                        if (capturedLight != null && !enabled)
                        {
                            capturedLight.shadows = LightShadows.None;
                        }
                    },
                    () => capturedLight != null ? capturedLight.shadows : LightShadows.None,
                    state =>
                    {
                        if (capturedLight != null && state is LightShadows shadows)
                        {
                            capturedLight.shadows = shadows;
                        }
                    })));
            }
        }

        private static void AddRenderFeatureCandidates(List<AutoCandidate> candidates)
        {
            foreach (var rendererData in ResolveRendererAssets())
            {
                if (rendererData == null)
                {
                    continue;
                }

                var features = rendererData.rendererFeatures;
                for (var i = 0; i < features.Count; i++)
                {
                    var feature = features[i];
                    if (feature == null || !feature.isActive || IsLowValueRenderFeature(feature) || !HasSceneWork(feature))
                    {
                        continue;
                    }

                    var capturedFeature = feature;
                    var score = EstimateRenderFeatureScore(feature);
                    candidates.Add(new AutoCandidate(score, new MazeProfilingFeatureHandle(
                        $"auto.render_feature.{Normalize(rendererData.name)}.{Normalize(feature.name)}",
                        $"{feature.name} renderer feature",
                        "Render Feature",
                        () => capturedFeature != null && capturedFeature.isActive ? $"{capturedFeature.GetType().Name} active" : "inactive",
                        enabled =>
                        {
                            if (capturedFeature != null)
                            {
                                capturedFeature.SetActive(enabled);
                            }
                        },
                        () => capturedFeature != null && capturedFeature.isActive,
                        state =>
                        {
                            if (capturedFeature != null && state is bool active)
                            {
                                capturedFeature.SetActive(active);
                            }
                        })));
                }
            }
        }

        private static bool IsLowValueRenderFeature(ScriptableRendererFeature feature)
        {
            var typeName = feature.GetType().Name;
            return typeName.Contains("DebugCapture", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasSceneWork(ScriptableRendererFeature feature)
        {
            var typeName = feature.GetType().Name;
            if (typeName.Contains("Filter", StringComparison.OrdinalIgnoreCase))
            {
                return Resources.FindObjectsOfTypeAll<MazeFilterController>()
                    .Any(controller => controller != null
                        && controller.isActiveAndEnabled
                        && controller.IsRenderingEnabled);
            }

            return true;
        }

        private static float EstimateRenderFeatureScore(ScriptableRendererFeature feature)
        {
            var typeName = feature.GetType().Name;
            var name = feature.name ?? string.Empty;
            if (typeName.Contains("RenderObjects", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Outline", StringComparison.OrdinalIgnoreCase))
            {
                return 1300f;
            }

            if (typeName.Contains("Decal", StringComparison.OrdinalIgnoreCase))
            {
                return 900f;
            }

            if (typeName.Contains("Bloom", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("Filter", StringComparison.OrdinalIgnoreCase))
            {
                return 800f;
            }

            if (typeName.Contains("Tone", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("AO", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("Fog", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("Focus", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("Lens", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("LightFX", StringComparison.OrdinalIgnoreCase))
            {
                return 600f;
            }

            return 400f;
        }

        private static List<ScriptableRendererData> ResolveRendererAssets()
        {
            var result = new List<ScriptableRendererData>();
            var pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            pipeline ??= GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            pipeline ??= GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                return result;
            }

            var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field?.GetValue(pipeline) is ScriptableRendererData[] dataArray)
            {
                foreach (var rendererData in dataArray)
                {
                    if (rendererData != null && !result.Contains(rendererData))
                    {
                        result.Add(rendererData);
                    }
                }
            }

            return result;
        }

        private static bool IsValidRoot(GameObject root)
        {
            if (root == null || !root.activeSelf)
            {
                return false;
            }

            var camera = Camera.main;
            if (camera != null && (root == camera.gameObject || camera.transform.IsChildOf(root.transform)))
            {
                return false;
            }

            var name = root.name.Trim();
            return !string.Equals(name, "MAZE", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, "Slice", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, "Player", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Automation", StringComparison.OrdinalIgnoreCase);
        }

        private static List<RendererInfo> CollectRendererInfos(Scene scene)
        {
            var result = new List<RendererInfo>(128);
            var renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.gameObject.scene != scene || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var mesh = ResolveMesh(renderer, out var isSkinned);
                if (mesh == null)
                {
                    continue;
                }

                result.Add(new RendererInfo(renderer, mesh, EstimateTriangles(mesh), isSkinned, renderer.sharedMaterials ?? Array.Empty<Material>()));
            }

            return result;
        }

        private static Mesh ResolveMesh(Renderer renderer, out bool isSkinned)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                isSkinned = true;
                return skinned.sharedMesh;
            }

            isSkinned = false;
            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        private static string MeshMaterialKey(RendererInfo info)
        {
            var materialIds = string.Join(",", info.Materials.Select(material => material != null ? material.GetEntityId().ToString() : "none"));
            return $"{info.Mesh.GetEntityId()}|{materialIds}";
        }

        private static int EstimatedShadowFaces(Light light)
        {
            if (light == null || light.shadows == LightShadows.None)
            {
                return 0;
            }

            return light.type switch
            {
                LightType.Point => 6,
                LightType.Directional => ResolveDirectionalShadowCascadeCount(),
                _ => 1,
            };
        }

        private static int ResolveDirectionalShadowCascadeCount()
        {
            var pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            pipeline ??= GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            pipeline ??= GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                return 1;
            }

            var property = pipeline.GetType().GetProperty("shadowCascadeCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            return property?.GetValue(pipeline) is int count && count > 0 ? count : 1;
        }

        private static string GameObjectPath(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return "missing";
            }

            var parts = new Stack<string>();
            var current = gameObject.transform;
            while (current != null)
            {
                parts.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", parts);
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unnamed";
            }

            var chars = new char[value.Length];
            var count = 0;
            for (var i = 0; i < value.Length; i++)
            {
                var c = char.ToLowerInvariant(value[i]);
                chars[count++] = char.IsLetterOrDigit(c) ? c : '_';
            }

            return new string(chars, 0, count).Trim('_');
        }

        private readonly struct AutoCandidate
        {
            public readonly float Score;
            public readonly MazeProfilingFeatureHandle Handle;

            public AutoCandidate(float score, MazeProfilingFeatureHandle handle)
            {
                Score = score;
                Handle = handle;
            }
        }

        private readonly struct ObjectCandidate
        {
            public readonly GameObject Target;
            public readonly string Category;
            public readonly float Score;

            private ObjectCandidate(GameObject target, string category, float score)
            {
                Target = target;
                Category = category;
                Score = score;
            }

            public static ObjectCandidate From(GameObject target, string category)
            {
                var renderers = target.GetComponentsInChildren<Renderer>(true);
                var lights = target.GetComponentsInChildren<Light>(true);
                var particles = target.GetComponentsInChildren<ParticleSystem>(true);
                var triangles = 0L;
                var boundsVolume = 0f;

                for (var i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    var size = renderer.bounds.size;
                    boundsVolume += Mathf.Max(0f, size.x * size.y * size.z);
                    if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                    {
                        triangles += EstimateTriangles(skinned.sharedMesh);
                    }
                    else
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (filter != null && filter.sharedMesh != null)
                        {
                            triangles += EstimateTriangles(filter.sharedMesh);
                        }
                    }
                }

                var score = renderers.Length * 10f
                    + lights.Length * 18f
                    + particles.Length * 20f
                    + Mathf.Min(triangles / 1000f, 2000f)
                    + Mathf.Min(boundsVolume / 1000f, 500f);
                return new ObjectCandidate(target, category, score);
            }
        }

        private sealed class RendererInfo
        {
            public readonly Renderer Renderer;
            public readonly Mesh Mesh;
            public readonly int Triangles;
            public readonly bool IsSkinned;
            public readonly Material[] Materials;

            public RendererInfo(Renderer renderer, Mesh mesh, int triangles, bool isSkinned, Material[] materials)
            {
                Renderer = renderer;
                Mesh = mesh;
                Triangles = triangles;
                IsSkinned = isSkinned;
                Materials = materials;
            }
        }

        private sealed class RendererBucket
        {
            public string Category;
            public string Key;
            public string Label;
            public List<Renderer> Renderers;
            public int TotalTriangles;
            public float Score;

            public static RendererBucket From(string category, List<RendererInfo> infos, Material material = null)
            {
                var first = infos.FirstOrDefault();
                var key = material != null
                    ? $"material.{material.GetEntityId()}"
                    : first != null ? MeshMaterialKey(first) : category;
                var label = material != null
                    ? $"Material {material.name}"
                    : first?.Mesh != null ? $"Mesh {first.Mesh.name}" : category;
                var totalTriangles = infos.Sum(info => info.Triangles);
                return new RendererBucket
                {
                    Category = category,
                    Key = key,
                    Label = label,
                    Renderers = infos.Select(info => info.Renderer).Where(renderer => renderer != null).Distinct().ToList(),
                    TotalTriangles = totalTriangles,
                    Score = infos.Count * 35f + Mathf.Min(totalTriangles / 100f, 3000f)
                };
            }
        }

        private sealed class ScriptBucket
        {
            public Type Type;
            public string Label;
            public List<MonoBehaviour> Scripts;
            public float Score;

            public static ScriptBucket From(Type type, List<MonoBehaviour> scripts)
            {
                return new ScriptBucket
                {
                    Type = type,
                    Label = type.Name,
                    Scripts = scripts,
                    Score = scripts.Count * 25f
                };
            }
        }

        private static int EstimateTriangles(Mesh mesh)
        {
            if (mesh == null)
            {
                return 0;
            }

            long total = 0L;
            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                total += mesh.GetIndexCount(i) / 3L;
            }

            return total > int.MaxValue ? int.MaxValue : (int)total;
        }
    }
}
