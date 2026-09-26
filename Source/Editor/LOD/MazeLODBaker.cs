using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Maze.Editor
{
    public static class MazeLODBaker
    {
        private const string LogPrefix = "[MazeLOD]";

        public static void RebuildCurrentSceneFromBatchMode()
        {
            var scene = SceneManager.GetActiveScene();
            var controller = FindActiveSceneController(scene);
            RebuildScene(controller);
            EditorSceneManager.SaveScene(scene);
        }

        public static void ValidateCurrentSceneFromBatchMode()
        {
            var scene = SceneManager.GetActiveScene();
            var controller = FindActiveSceneController(scene);
            var objects = FindSceneObjects(controller);
            if (objects.Length == 0)
            {
                throw new InvalidOperationException($"Scene '{scene.path}' has no MazeLODObject owned by its LOD controller.");
            }

            for (var i = 0; i < objects.Length; i++)
            {
                ValidateBakedObject(objects[i]);
            }

            Debug.Log($"{LogPrefix} validation ok scene={scene.path} objects={objects.Length}");
        }

        public static void RebuildScene(MazeLODController controller)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            EnsureEditorIdle();
            controller.EnsureDefaults();
            var scene = controller.gameObject.scene;
            var objects = FindSceneObjects(controller);
            for (var i = 0; i < objects.Length; i++)
            {
                Rebuild(objects[i]);
            }

            Debug.Log($"{LogPrefix} scene rebuild ok scene={scene.path} objects={objects.Length}");
        }

        public static void Rebuild(MazeLODObject target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            EnsureEditorIdle();
            target.EnsureDefaults();
            if (!TryValidate(target, out var error))
            {
                throw new InvalidOperationException(error);
            }

            var sourceMesh = target.SourceMeshFilter.sharedMesh;
            var settings = target.EffectiveSettings;
            var generatedCount = settings.LevelCount - 1;
            var sourceHash = GetSourceHash(sourceMesh);
            var outputFolder = GetOutputFolder(target);
            var outputBaseName = SanitizeFileName(target.gameObject.name);
            var triangleTargets = new int[generatedCount];
            for (var i = 0; i < triangleTargets.Length; i++)
            {
                triangleTargets[i] = settings.GetTriangleTarget(i);
            }

            var generated = generatedCount > 0
                ? MazeLODMeshBaker.GenerateCompactLevels(sourceMesh, triangleTargets, outputBaseName)
                : Array.Empty<MazeLODGeneratedLevel>();

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild Maze LOD");
            var backups = new List<(Mesh Existing, Mesh Backup)>();
            var createdAssetPaths = new List<string>();
            var committedMeshes = new List<Mesh>(generatedCount);
            try
            {
                for (var i = 0; i < generated.Count; i++)
                {
                    var level = generated[i];
                    var outputPath = ResolveOutputPath(
                        target,
                        outputFolder,
                        outputBaseName,
                        level.Stats.Level,
                        i);
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(outputPath);
                    if (existing != null)
                    {
                        backups.Add((existing, Object.Instantiate(existing)));
                        Undo.RegisterCompleteObjectUndo(existing, "Rebuild Maze LOD Mesh");
                        EditorUtility.CopySerialized(level.Mesh, existing);
                        existing.name = level.Mesh.name;
                        EditorUtility.SetDirty(existing);
                        committedMeshes.Add(existing);
                        Object.DestroyImmediate(level.Mesh);
                    }
                    else
                    {
                        AssetDatabase.CreateAsset(level.Mesh, outputPath);
                        createdAssetPaths.Add(outputPath);
                        committedMeshes.Add(level.Mesh);
                    }
                }

                var renderers = ConfigureHierarchy(target, committedMeshes, settings, sourceHash);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                Undo.CollapseUndoOperations(undoGroup);

                var stats = new List<string>
                {
                    $"object={target.name}",
                    $"source_tris={MazeLODMeshBaker.CountTriangles(sourceMesh)}",
                    $"source_verts={sourceMesh.vertexCount}",
                    $"levels={settings.LevelCount}",
                    $"renderers={renderers.Count}",
                    $"crossfade={settings.crossFade.ToString().ToLowerInvariant()}",
                    $"cull={settings.cull.ToString().ToLowerInvariant()}"
                };
                for (var i = 0; i < generated.Count; i++)
                {
                    stats.Add($"lod{generated[i].Stats.Level}_target={generated[i].Stats.TargetTriangles}");
                    stats.Add($"lod{generated[i].Stats.Level}_tris={generated[i].Stats.Triangles}");
                    stats.Add($"lod{generated[i].Stats.Level}_verts={generated[i].Stats.Vertices}");
                    stats.Add($"lod{generated[i].Stats.Level}_source_level={generated[i].Stats.SourceLevel}");
                }

                Debug.Log($"{LogPrefix} bake ok {string.Join(" ", stats)}");
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                for (var i = 0; i < backups.Count; i++)
                {
                    EditorUtility.CopySerialized(backups[i].Backup, backups[i].Existing);
                    EditorUtility.SetDirty(backups[i].Existing);
                }

                for (var i = 0; i < createdAssetPaths.Count; i++)
                {
                    AssetDatabase.DeleteAsset(createdAssetPaths[i]);
                }

                AssetDatabase.SaveAssets();
                Debug.LogError($"{LogPrefix} bake failed object={target.name} error={exception.Message}");
                throw;
            }
            finally
            {
                for (var i = 0; i < backups.Count; i++)
                {
                    Object.DestroyImmediate(backups[i].Backup);
                }

                for (var i = 0; i < generated.Count; i++)
                {
                    var mesh = generated[i].Mesh;
                    if (mesh != null && !AssetDatabase.Contains(mesh))
                    {
                        Object.DestroyImmediate(mesh);
                    }
                }
            }
        }

        public static bool TryValidate(MazeLODObject target, out string error)
        {
            if (target == null)
            {
                error = "Missing Maze LOD Object.";
                return false;
            }

            if (target.SourceMeshFilter == null || target.SourceRenderer == null)
            {
                if (!target.FindSourceRenderer())
                {
                    error = "A static MeshFilter and MeshRenderer source is required.";
                    return false;
                }
            }

            if (target.SourceMeshFilter.gameObject != target.SourceRenderer.gameObject)
            {
                error = "Source MeshFilter and MeshRenderer must be on the same GameObject.";
                return false;
            }

            var sourceMesh = target.SourceMeshFilter.sharedMesh;
            if (sourceMesh == null)
            {
                error = "Source MeshFilter has no mesh.";
                return false;
            }

            if (sourceMesh.lodCount > 1)
            {
                error = "Source mesh already contains native Mesh LOD data.";
                return false;
            }

            if (!target.EffectiveSettings.TryValidate(out error))
            {
                return false;
            }

            if ((GameObjectUtility.GetStaticEditorFlags(target.SourceRenderer.gameObject) & StaticEditorFlags.BatchingStatic) != 0)
            {
                error = "Batching Static is incompatible with GPU Resident Drawer participation.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool TryGetStateWarning(MazeLODObject target, out string warning)
        {
            warning = string.Empty;
            if (!TryValidate(target, out var validationError))
            {
                warning = validationError;
                return true;
            }

            if (target.BakedLevelCount <= 0)
            {
                warning = "LODs have not been generated.";
                return true;
            }

            if (target.BakedLevelCount != target.EffectiveSettings.LevelCount)
            {
                warning = "LOD settings changed. Rebuild the object.";
                return true;
            }

            if (!string.Equals(
                    target.BakedSettingsHash,
                    GetSettingsHash(target.EffectiveSettings),
                    StringComparison.Ordinal))
            {
                warning = "LOD settings changed. Rebuild the object.";
                return true;
            }

            if (!string.Equals(target.BakedSourceHash, GetSourceHash(target.SourceMeshFilter.sharedMesh), StringComparison.Ordinal))
            {
                warning = "Source mesh changed. Rebuild the object.";
                return true;
            }

            return false;
        }

        private static IReadOnlyList<MeshRenderer> ConfigureHierarchy(
            MazeLODObject target,
            IReadOnlyList<Mesh> meshes,
            MazeLODSettings settings,
            string sourceHash)
        {
            Undo.RecordObject(target, "Configure Maze LOD Object");
            var group = target.GetComponent<LODGroup>();
            if (group == null)
            {
                group = Undo.AddComponent<LODGroup>(target.gameObject);
            }
            else
            {
                Undo.RecordObject(group, "Configure Maze LOD Group");
            }

            var renderers = new List<MeshRenderer>(meshes.Count);
            for (var i = 0; i < meshes.Count; i++)
            {
                var level = i + 1;
                MeshRenderer renderer = null;
                if (i < target.GeneratedRenderers.Count)
                {
                    renderer = target.GeneratedRenderers[i];
                }

                if (renderer == null)
                {
                    var child = new GameObject(GetLevelName(level));
                    Undo.RegisterCreatedObjectUndo(child, "Create Maze LOD Renderer");
                    child.transform.SetParent(target.transform, false);
                    child.layer = target.SourceRenderer.gameObject.layer;
                    var sourceFlags = GameObjectUtility.GetStaticEditorFlags(target.SourceRenderer.gameObject);
                    GameObjectUtility.SetStaticEditorFlags(child, sourceFlags & ~StaticEditorFlags.BatchingStatic);
                    child.AddComponent<MeshFilter>();
                    renderer = child.AddComponent<MeshRenderer>();
                }

                renderer.gameObject.name = GetLevelName(level);
                CopyRendererSettings(target.SourceRenderer, renderer);
                renderer.GetComponent<MeshFilter>().sharedMesh = meshes[i];
                renderers.Add(renderer);
            }

            for (var i = target.GeneratedRenderers.Count - 1; i >= meshes.Count; i--)
            {
                var renderer = target.GeneratedRenderers[i];
                if (renderer != null)
                {
                    Undo.DestroyObjectImmediate(renderer.gameObject);
                }
            }

            var lods = new LOD[settings.LevelCount];
            for (var level = 0; level < lods.Length; level++)
            {
                var renderer = level == 0 ? target.SourceRenderer : renderers[level - 1];
                lods[level] = new LOD(settings.GetTransitionHeight(level), new Renderer[] { renderer })
                {
                    fadeTransitionWidth = settings.crossFade ? settings.fadeTransitionWidth : 0f
                };
            }

            group.animateCrossFading = false;
            group.fadeMode = settings.crossFade ? LODFadeMode.CrossFade : LODFadeMode.None;
            group.SetLODs(lods);
            group.RecalculateBounds();
            EditorUtility.SetDirty(group);

            target.Configure(
                target.Controller,
                target.SourceMeshFilter,
                target.SourceRenderer,
                group,
                meshes,
                renderers,
                sourceHash,
                GetSettingsHash(settings),
                settings.LevelCount);
            EditorUtility.SetDirty(target);
            return renderers;
        }

        private static void CopyRendererSettings(MeshRenderer source, MeshRenderer target)
        {
            Undo.RecordObject(target, "Configure Maze LOD Renderer");
            target.enabled = true;
            target.sharedMaterials = source.sharedMaterials;
            target.shadowCastingMode = source.shadowCastingMode;
            target.receiveShadows = source.receiveShadows;
            target.lightProbeUsage = source.lightProbeUsage;
            target.reflectionProbeUsage = source.reflectionProbeUsage;
            target.probeAnchor = source.probeAnchor;
            target.renderingLayerMask = source.renderingLayerMask;
            target.rendererPriority = source.rendererPriority;
            target.motionVectorGenerationMode = source.motionVectorGenerationMode;
            target.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder;
            EditorUtility.SetDirty(target);
        }

        private static MazeLODController FindActiveSceneController(Scene scene)
        {
            var controller = scene.GetRootGameObjects()
                .Select(root => root.GetComponent<MazeLODController>())
                .FirstOrDefault(candidate => candidate != null);
            if (controller == null)
            {
                throw new InvalidOperationException($"Scene '{scene.path}' has no root MazeLODController.");
            }

            return controller;
        }

        private static MazeLODObject[] FindSceneObjects(MazeLODController controller)
        {
            var scene = controller.gameObject.scene;
            return Object.FindObjectsByType<MazeLODObject>(FindObjectsInactive.Include)
                .Where(candidate => candidate.gameObject.scene == scene && candidate.Controller == controller)
                .ToArray();
        }

        private static void ValidateBakedObject(MazeLODObject target)
        {
            if (TryGetStateWarning(target, out var warning))
            {
                throw new InvalidOperationException($"{target.name}: {warning}");
            }

            var settings = target.EffectiveSettings;
            var generatedCount = settings.LevelCount - 1;
            if (target.LODGroup == null || target.GeneratedMeshes.Count != generatedCount ||
                target.GeneratedRenderers.Count != generatedCount)
            {
                throw new InvalidOperationException($"{target.name}: generated hierarchy does not match Level Count.");
            }

            var lods = target.LODGroup.GetLODs();
            if (lods.Length != settings.LevelCount)
            {
                throw new InvalidOperationException($"{target.name}: LODGroup level count is {lods.Length}, expected {settings.LevelCount}.");
            }

            var expectedFadeMode = settings.crossFade ? LODFadeMode.CrossFade : LODFadeMode.None;
            if (target.LODGroup.fadeMode != expectedFadeMode || target.LODGroup.animateCrossFading)
            {
                throw new InvalidOperationException($"{target.name}: LODGroup fade mode does not match the authored settings.");
            }

            var previousTriangles = MazeLODMeshBaker.CountTriangles(target.SourceMeshFilter.sharedMesh);
            for (var level = 0; level < lods.Length; level++)
            {
                var expectedRenderer = level == 0 ? target.SourceRenderer : target.GeneratedRenderers[level - 1];
                if (lods[level].renderers.Length != 1 || lods[level].renderers[0] != expectedRenderer ||
                    !Mathf.Approximately(lods[level].screenRelativeTransitionHeight, settings.GetTransitionHeight(level)))
                {
                    throw new InvalidOperationException($"{target.name}: LOD{level} renderer or transition is incorrect.");
                }

                var expectedFadeWidth = settings.crossFade ? settings.fadeTransitionWidth : 0f;
                if (!Mathf.Approximately(lods[level].fadeTransitionWidth, expectedFadeWidth))
                {
                    throw new InvalidOperationException($"{target.name}: LOD{level} fade width is incorrect.");
                }

                if (level == 0)
                {
                    continue;
                }

                var mesh = target.GeneratedMeshes[level - 1];
                var renderer = target.GeneratedRenderers[level - 1];
                if (mesh == null || renderer == null || renderer.GetComponent<MeshFilter>()?.sharedMesh != mesh ||
                    mesh.lodCount != 1 || string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(mesh)))
                {
                    throw new InvalidOperationException($"{target.name}: LOD{level} is not a standalone saved mesh renderer.");
                }

                var triangles = MazeLODMeshBaker.CountTriangles(mesh);
                if (triangles <= 0 || triangles >= previousTriangles)
                {
                    throw new InvalidOperationException(
                        $"{target.name}: LOD{level} triangle count {triangles} does not reduce level {level - 1} ({previousTriangles}).");
                }

                if ((GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.BatchingStatic) != 0)
                {
                    throw new InvalidOperationException($"{target.name}: LOD{level} is marked BatchingStatic.");
                }

                previousTriangles = triangles;
            }
        }

        private static string GetOutputFolder(MazeLODObject target)
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && target.gameObject.scene == prefabStage.scene)
            {
                return NormalizeFolder(Path.GetDirectoryName(prefabStage.assetPath));
            }

            if (EditorUtility.IsPersistent(target))
            {
                return NormalizeFolder(Path.GetDirectoryName(AssetDatabase.GetAssetPath(target)));
            }

            var scenePath = target.gameObject.scene.path;
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new InvalidOperationException("Save the scene or prefab before generating LOD assets.");
            }

            return NormalizeFolder(Path.GetDirectoryName(scenePath));
        }

        private static string NormalizeFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new InvalidOperationException("Could not resolve an LOD output folder.");
            }

            folder = folder.Replace('\\', '/');
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal) && folder != "Assets")
            {
                throw new InvalidOperationException($"LOD output must remain under Assets, not '{folder}'.");
            }

            return folder;
        }

        private static string ResolveOutputPath(
            MazeLODObject target,
            string outputFolder,
            string outputBaseName,
            int level,
            int generatedIndex)
        {
            if (generatedIndex < target.GeneratedMeshes.Count)
            {
                var ownedMesh = target.GeneratedMeshes[generatedIndex];
                var ownedPath = AssetDatabase.GetAssetPath(ownedMesh).Replace('\\', '/');
                if (!string.IsNullOrWhiteSpace(ownedPath) &&
                    string.Equals(NormalizeFolder(Path.GetDirectoryName(ownedPath)), outputFolder, StringComparison.Ordinal))
                {
                    return ownedPath;
                }
            }

            var desiredPath = $"{outputFolder}/{outputBaseName} LOD{level}.asset";
            if (AssetDatabase.LoadMainAssetAtPath(desiredPath) == null)
            {
                return desiredPath;
            }

            return AssetDatabase.GenerateUniqueAssetPath(desiredPath);
        }

        private static string GetSourceHash(Mesh mesh)
        {
            var path = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrWhiteSpace(path))
            {
                return $"memory:{mesh.GetEntityId()}:{mesh.vertexCount}:{mesh.GetIndexCount(0)}";
            }

            return AssetDatabase.GetAssetDependencyHash(path).ToString();
        }

        private static string GetSettingsHash(MazeLODSettings settings)
        {
            return Hash128.Compute(JsonUtility.ToJson(settings)).ToString();
        }

        private static string GetLevelName(int level)
        {
            return level switch
            {
                1 => "LOD1 Standard",
                2 => "LOD2 Medium",
                3 => "LOD3 Faraway",
                _ => $"LOD{level}"
            };
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var characters = value.Where(character => !invalid.Contains(character)).ToArray();
            var result = new string(characters).Trim();
            return string.IsNullOrWhiteSpace(result) ? "LOD Object" : result;
        }

        private static void EnsureEditorIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before rebuilding Maze LOD assets.");
            }
        }
    }
}
