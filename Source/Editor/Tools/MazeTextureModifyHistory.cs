#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Maze.Editor
{
    [Serializable]
    internal sealed class MazeTextureHistoryManifest
    {
        public int version = 1;
        public string assetGuid;
        public string lastKnownAssetPath;
        public List<MazeTextureHistoryEntry> entries = new();
    }

    [Serializable]
    internal sealed class MazeTextureHistoryEntry
    {
        public string id;
        public string timestampUtc;
        public string label;
        public string fileName;
        public string sha256;
        public int width;
        public int height;
        public string settings;
    }

    internal readonly struct MazeTextureApplyResult
    {
        internal MazeTextureApplyResult(bool success, string message, string sourceHash)
        {
            Success = success;
            Message = message;
            SourceHash = sourceHash;
        }

        internal bool Success { get; }
        internal string Message { get; }
        internal string SourceHash { get; }
    }

    internal static class MazeTextureModifyHistory
    {
        private const string HistoryRelativePath = "Internal/Texture History";
        private const string ManifestFileName = "history.json";

        internal static string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        internal static string HistoryRoot => Path.Combine(ProjectRoot, HistoryRelativePath.Replace('/', Path.DirectorySeparatorChar));

        internal static string AssetPathToFullPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        internal static string ComputeCurrentHash(string assetPath)
        {
            return MazeTextureModifyProcessor.ComputeSha256(File.ReadAllBytes(AssetPathToFullPath(assetPath)));
        }

        internal static MazeTextureHistoryManifest LoadManifest(string assetGuid, string assetPath)
        {
            var manifestPath = GetManifestPath(assetGuid);
            if (!File.Exists(manifestPath))
            {
                return new MazeTextureHistoryManifest
                {
                    assetGuid = assetGuid,
                    lastKnownAssetPath = assetPath
                };
            }

            try
            {
                var manifest = JsonUtility.FromJson<MazeTextureHistoryManifest>(File.ReadAllText(manifestPath));
                if (manifest == null || manifest.assetGuid != assetGuid)
                {
                    throw new InvalidDataException("Texture history belongs to a different asset GUID.");
                }
                manifest.entries ??= new List<MazeTextureHistoryEntry>();
                manifest.lastKnownAssetPath = assetPath;
                return manifest;
            }
            catch (Exception exception)
            {
                Debug.LogError($"MAZE Texture Modify | history_load_failed | guid={assetGuid} | error={exception.Message}");
                throw new InvalidDataException("The texture history manifest is unreadable; it was not overwritten.", exception);
            }
        }

        internal static byte[] ReadSnapshot(string assetGuid, MazeTextureHistoryEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.fileName))
            {
                throw new ArgumentException("No history revision is selected.");
            }
            return File.ReadAllBytes(Path.Combine(GetAssetHistoryDirectory(assetGuid), entry.fileName));
        }

        internal static MazeTextureApplyResult Apply(
            string assetPath,
            string assetGuid,
            string expectedSourceHash,
            byte[] candidateBytes,
            MazeTextureModifySettings settings,
            string historyLabel)
        {
            var fullPath = AssetPathToFullPath(assetPath);
            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
            var transactionPath = fullPath + ".maze-transaction-backup";
            var candidatePath = fullPath + ".maze-candidate";
            var metaPath = fullPath + ".meta";
            byte[] originalBytes = null;
            byte[] originalMeta = null;
            var replaced = false;
            var preserveTransactionBackup = false;

            try
            {
                if (!File.Exists(fullPath))
                {
                    return new MazeTextureApplyResult(false, "The source file no longer exists.", string.Empty);
                }

                originalBytes = File.ReadAllBytes(fullPath);
                var currentHash = MazeTextureModifyProcessor.ComputeSha256(originalBytes);
                if (!string.Equals(currentHash, expectedSourceHash, StringComparison.OrdinalIgnoreCase))
                {
                    return new MazeTextureApplyResult(false, "The texture changed after MODIFY opened. Reload it before applying.", currentHash);
                }

                if (candidateBytes == null || candidateBytes.Length == 0)
                {
                    return new MazeTextureApplyResult(false, "The processed image was empty.", currentHash);
                }

                ValidateCandidate(candidateBytes, extension, assetPath);
                originalMeta = File.Exists(metaPath) ? File.ReadAllBytes(metaPath) : Array.Empty<byte>();
                var originalMetaHash = MazeTextureModifyProcessor.ComputeSha256(originalMeta);
                var originalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                var expectedWidth = originalTexture != null ? originalTexture.width : 0;
                var expectedHeight = originalTexture != null ? originalTexture.height : 0;

                Snapshot(assetPath, assetGuid, originalBytes, settings, historyLabel, expectedWidth, expectedHeight);
                File.WriteAllBytes(candidatePath, candidateBytes);
                ValidateCandidate(File.ReadAllBytes(candidatePath), extension, assetPath);

                if (File.Exists(transactionPath))
                {
                    File.Delete(transactionPath);
                }
                File.Replace(candidatePath, fullPath, transactionPath, true);
                replaced = true;

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (imported == null)
                {
                    throw new InvalidDataException("Unity did not reload the modified texture.");
                }
                if (expectedWidth > 0 && (imported.width != expectedWidth || imported.height != expectedHeight))
                {
                    throw new InvalidDataException($"Dimensions changed from {expectedWidth}x{expectedHeight} to {imported.width}x{imported.height}.");
                }
                if (!string.Equals(AssetDatabase.AssetPathToGUID(assetPath), assetGuid, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The texture GUID changed during Apply.");
                }

                var finalBytes = File.ReadAllBytes(fullPath);
                var finalHash = MazeTextureModifyProcessor.ComputeSha256(finalBytes);
                var candidateHash = MazeTextureModifyProcessor.ComputeSha256(candidateBytes);
                if (!string.Equals(finalHash, candidateHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The imported source bytes do not match the processed candidate.");
                }

                var finalMeta = File.Exists(metaPath) ? File.ReadAllBytes(metaPath) : Array.Empty<byte>();
                var finalMetaHash = MazeTextureModifyProcessor.ComputeSha256(finalMeta);
                if (!string.Equals(originalMetaHash, finalMetaHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The texture .meta file changed during Apply.");
                }

                if (File.Exists(transactionPath))
                {
                    File.Delete(transactionPath);
                }

                Debug.Log($"MAZE Texture Modify | apply | asset={assetPath} | size={imported.width}x{imported.height} | bytes={candidateBytes.Length} | tile={settings?.tileMode ?? true} | history={GetAssetHistoryDirectory(assetGuid)}");
                return new MazeTextureApplyResult(true, "Applied and added to History.", finalHash);
            }
            catch (Exception exception)
            {
                try
                {
                    if (replaced && File.Exists(transactionPath))
                    {
                        if (File.Exists(fullPath))
                        {
                            File.Delete(fullPath);
                        }
                        File.Move(transactionPath, fullPath);
                    }
                    else if (originalBytes != null)
                    {
                        File.WriteAllBytes(fullPath, originalBytes);
                    }
                    if (originalMeta != null && originalMeta.Length > 0)
                    {
                        File.WriteAllBytes(metaPath, originalMeta);
                    }
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }
                catch (Exception rollbackException)
                {
                    preserveTransactionBackup = true;
                    Debug.LogError($"MAZE Texture Modify | rollback_failed | asset={assetPath} | error={rollbackException.Message}");
                    return new MazeTextureApplyResult(false, $"Apply failed and rollback also failed: {rollbackException.Message}", expectedSourceHash);
                }

                Debug.LogError($"MAZE Texture Modify | apply_failed | asset={assetPath} | error={exception.Message} | rollback=restored");
                return new MazeTextureApplyResult(false, $"Apply failed; the original was restored. {exception.Message}", expectedSourceHash);
            }
            finally
            {
                TryDelete(candidatePath);
                if (!preserveTransactionBackup)
                {
                    TryDelete(transactionPath);
                }
            }
        }

        internal static bool ContainsEmbeddedIccProfile(string assetPath)
        {
            if (!string.Equals(Path.GetExtension(assetPath), ".png", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var bytes = File.ReadAllBytes(AssetPathToFullPath(assetPath));
            if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4e || bytes[3] != 0x47)
            {
                return false;
            }

            var offset = 8;
            while (offset + 12 <= bytes.Length)
            {
                var length = (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
                if (length < 0 || offset + 12L + length > bytes.Length)
                {
                    return false;
                }
                if (bytes[offset + 4] == (byte)'i' && bytes[offset + 5] == (byte)'C' && bytes[offset + 6] == (byte)'C' && bytes[offset + 7] == (byte)'P')
                {
                    return true;
                }
                offset += 12 + length;
            }
            return false;
        }

        private static void Snapshot(
            string assetPath,
            string assetGuid,
            byte[] bytes,
            MazeTextureModifySettings settings,
            string label,
            int width,
            int height)
        {
            var manifest = LoadManifest(assetGuid, assetPath);
            var hash = MazeTextureModifyProcessor.ComputeSha256(bytes);
            if (manifest.entries.Any(entry => string.Equals(entry.sha256, hash, StringComparison.OrdinalIgnoreCase)))
            {
                SaveManifest(manifest);
                return;
            }

            var timestamp = DateTime.UtcNow;
            var extension = Path.GetExtension(assetPath).ToLowerInvariant();
            var fileName = $"{timestamp:yyyyMMdd_HHmmss_fff}_{hash[..10]}{extension}";
            var directory = GetAssetHistoryDirectory(assetGuid);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, fileName), bytes);
            manifest.entries.Add(new MazeTextureHistoryEntry
            {
                id = Guid.NewGuid().ToString("N"),
                timestampUtc = timestamp.ToString("O"),
                label = manifest.entries.Count == 0 ? "Original" : label,
                fileName = fileName,
                sha256 = hash,
                width = width,
                height = height,
                settings = settings?.ToCompactString() ?? string.Empty
            });
            manifest.lastKnownAssetPath = assetPath;
            SaveManifest(manifest);
        }

        private static void SaveManifest(MazeTextureHistoryManifest manifest)
        {
            var directory = GetAssetHistoryDirectory(manifest.assetGuid);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, ManifestFileName);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(manifest, true));
            if (File.Exists(path))
            {
                File.Replace(temporary, path, null, true);
            }
            else
            {
                File.Move(temporary, path);
            }
        }

        private static void ValidateCandidate(byte[] bytes, string extension, string assetPath)
        {
            if (extension is not (".png" or ".jpg" or ".jpeg"))
            {
                throw new NotSupportedException($"{extension} is not supported by MODIFY.");
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            var texture = MazeTextureModifyProcessor.Decode(bytes, importer?.sRGBTexture ?? true);
            if (texture == null)
            {
                throw new InvalidDataException("The processed file could not be decoded.");
            }
            if (texture.width < 1 || texture.height < 1)
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidDataException("The processed image has invalid dimensions.");
            }
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static string GetManifestPath(string assetGuid)
        {
            return Path.Combine(GetAssetHistoryDirectory(assetGuid), ManifestFileName);
        }

        private static string GetAssetHistoryDirectory(string assetGuid)
        {
            return Path.Combine(HistoryRoot, assetGuid);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // The primary transaction result has already been reported.
            }
        }
    }
}
#endif
