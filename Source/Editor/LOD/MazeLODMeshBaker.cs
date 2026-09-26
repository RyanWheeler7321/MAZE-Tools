using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Maze.Editor
{
    internal readonly struct MazeLODLevelStats
    {
        public MazeLODLevelStats(int level, int sourceLevel, int targetTriangles, int triangles, int vertices)
        {
            Level = level;
            SourceLevel = sourceLevel;
            TargetTriangles = targetTriangles;
            Triangles = triangles;
            Vertices = vertices;
        }

        public int Level { get; }
        public int SourceLevel { get; }
        public int TargetTriangles { get; }
        public int Triangles { get; }
        public int Vertices { get; }
    }

    internal sealed class MazeLODGeneratedLevel
    {
        public MazeLODGeneratedLevel(Mesh mesh, MazeLODLevelStats stats)
        {
            Mesh = mesh;
            Stats = stats;
        }

        public Mesh Mesh { get; }
        public MazeLODLevelStats Stats { get; }
    }

    internal static class MazeLODMeshBaker
    {
        private const int MinimumGeneratorTriangleCount = 256;

        public static IReadOnlyList<MazeLODGeneratedLevel> GenerateCompactLevels(
            Mesh source,
            IReadOnlyList<int> targetTriangleCounts,
            string outputName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (targetTriangleCounts == null || targetTriangleCounts.Count == 0)
            {
                return Array.Empty<MazeLODGeneratedLevel>();
            }

            ValidateSource(source);
            ValidateTargets(source, targetTriangleCounts);
            var temporary = Object.Instantiate(source);
            temporary.name = source.name + " LOD Generation";
            try
            {
                MeshLodUtility.GenerateMeshLods(temporary);
                if (temporary.lodCount <= targetTriangleCounts.Count)
                {
                    throw new InvalidOperationException(
                        $"Requested {targetTriangleCounts.Count + 1} total LOD levels, but Unity generated only {temporary.lodCount}.");
                }

                var selectedSourceLevels = SelectSourceLevels(temporary, targetTriangleCounts);
                using var readable = Mesh.AcquireReadOnlyMeshData(temporary);
                var sourceData = readable[0];
                var descriptors = temporary.GetVertexAttributes();
                var results = new List<MazeLODGeneratedLevel>(targetTriangleCounts.Count);
                try
                {
                    for (var generatedIndex = 0; generatedIndex < targetTriangleCounts.Count; generatedIndex++)
                    {
                        var level = generatedIndex + 1;
                        var sourceLevel = selectedSourceLevels[generatedIndex];
                        var mesh = CompactLevel(
                            temporary,
                            sourceData,
                            descriptors,
                            sourceLevel,
                            $"{outputName} {GetRoleName(level)}");
                        results.Add(new MazeLODGeneratedLevel(
                            mesh,
                            new MazeLODLevelStats(
                                level,
                                sourceLevel,
                                targetTriangleCounts[generatedIndex],
                                CountTriangles(mesh),
                                mesh.vertexCount)));
                    }

                    return results;
                }
                catch
                {
                    for (var i = 0; i < results.Count; i++)
                    {
                        Object.DestroyImmediate(results[i].Mesh);
                    }

                    throw;
                }
            }
            finally
            {
                Object.DestroyImmediate(temporary);
            }
        }

        public static int CountTriangles(Mesh mesh)
        {
            if (mesh == null)
            {
                return 0;
            }

            var triangles = 0;
            for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                if (mesh.GetTopology(subMesh) == MeshTopology.Triangles)
                {
                    triangles += (int)mesh.GetIndexCount(subMesh) / 3;
                }
            }

            return triangles;
        }

        private static void ValidateSource(Mesh source)
        {
            if (source.blendShapeCount > 0)
            {
                throw new InvalidOperationException("Maze LOD v1 does not support meshes with blend shapes.");
            }

            var triangles = 0;
            for (var subMesh = 0; subMesh < source.subMeshCount; subMesh++)
            {
                if (source.GetTopology(subMesh) != MeshTopology.Triangles)
                {
                    throw new InvalidOperationException($"Submesh {subMesh} is not triangle topology.");
                }

                triangles += (int)source.GetIndexCount(subMesh) / 3;
            }

            if (triangles < MinimumGeneratorTriangleCount)
            {
                throw new InvalidOperationException(
                    $"Maze LOD skips automatic generation below {MinimumGeneratorTriangleCount} triangles; source has {triangles}.");
            }
        }

        private static void ValidateTargets(Mesh source, IReadOnlyList<int> targetTriangleCounts)
        {
            var previous = CountTriangles(source);
            for (var i = 0; i < targetTriangleCounts.Count; i++)
            {
                var target = targetTriangleCounts[i];
                if (target < 1 || target >= previous)
                {
                    throw new InvalidOperationException(
                        $"LOD{i + 1} triangle target must be positive and lower than the preceding level ({previous}).");
                }

                previous = target;
            }
        }

        private static int[] SelectSourceLevels(Mesh generatedMesh, IReadOnlyList<int> targetTriangleCounts)
        {
            var availableLevels = generatedMesh.lodCount - 1;
            var selected = new int[targetTriangleCounts.Count];
            var previousSourceLevel = 0;
            for (var targetIndex = 0; targetIndex < targetTriangleCounts.Count; targetIndex++)
            {
                var remainingTargets = targetTriangleCounts.Count - targetIndex - 1;
                var firstCandidate = previousSourceLevel + 1;
                var lastCandidate = availableLevels - remainingTargets;
                var bestLevel = firstCandidate;
                var bestDifference = long.MaxValue;
                for (var candidate = firstCandidate; candidate <= lastCandidate; candidate++)
                {
                    var difference = Math.Abs((long)CountLodTriangles(generatedMesh, candidate) - targetTriangleCounts[targetIndex]);
                    if (difference >= bestDifference)
                    {
                        continue;
                    }

                    bestDifference = difference;
                    bestLevel = candidate;
                }

                selected[targetIndex] = bestLevel;
                previousSourceLevel = bestLevel;
            }

            return selected;
        }

        private static int CountLodTriangles(Mesh mesh, int level)
        {
            var triangles = 0;
            for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                triangles += checked((int)mesh.GetLod(subMesh, level).indexCount) / 3;
            }

            return triangles;
        }

        private static string GetRoleName(int level)
        {
            return level switch
            {
                1 => "LOD1 Standard",
                2 => "LOD2 Medium",
                3 => "LOD3 Faraway",
                _ => $"LOD{level}"
            };
        }

        private static Mesh CompactLevel(
            Mesh generatedSource,
            Mesh.MeshData sourceData,
            VertexAttributeDescriptor[] descriptors,
            int level,
            string name)
        {
            var remap = new Dictionary<int, int>();
            var originalVertices = new List<int>();
            var submeshIndices = new List<int>[sourceData.subMeshCount];
            var totalIndexCount = 0;

            for (var subMesh = 0; subMesh < sourceData.subMeshCount; subMesh++)
            {
                var descriptor = sourceData.GetSubMesh(subMesh);
                var range = generatedSource.GetLod(subMesh, level);
                var indices = new List<int>((int)range.indexCount);
                var absoluteStart = checked(descriptor.indexStart + (int)range.indexStart);
                var rangeIndexCount = checked((int)range.indexCount);
                for (var indexOffset = 0; indexOffset < rangeIndexCount; indexOffset++)
                {
                    var rawIndex = ReadIndex(sourceData, checked(absoluteStart + indexOffset));
                    var sourceVertex = checked(rawIndex + descriptor.baseVertex);
                    if (sourceVertex < 0 || sourceVertex >= sourceData.vertexCount)
                    {
                        throw new InvalidOperationException(
                            $"LOD{level} submesh {subMesh} references vertex {sourceVertex} outside 0..{sourceData.vertexCount - 1}.");
                    }

                    if (!remap.TryGetValue(sourceVertex, out var compactVertex))
                    {
                        compactVertex = originalVertices.Count;
                        remap.Add(sourceVertex, compactVertex);
                        originalVertices.Add(sourceVertex);
                    }

                    indices.Add(compactVertex);
                }

                submeshIndices[subMesh] = indices;
                totalIndexCount += indices.Count;
            }

            if (originalVertices.Count == 0 || totalIndexCount == 0)
            {
                throw new InvalidOperationException($"LOD{level} contains no geometry.");
            }

            var output = new Mesh { name = name };
            var writable = Mesh.AllocateWritableMeshData(1);
            try
            {
                var targetData = writable[0];
                targetData.SetVertexBufferParams(originalVertices.Count, descriptors);
                CopyVertexStreams(sourceData, targetData, originalVertices);

                var indexFormat = originalVertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
                targetData.SetIndexBufferParams(totalIndexCount, indexFormat);
                WriteIndices(targetData, indexFormat, submeshIndices);

                targetData.subMeshCount = sourceData.subMeshCount;
                var indexStart = 0;
                for (var subMesh = 0; subMesh < sourceData.subMeshCount; subMesh++)
                {
                    var sourceSubmesh = sourceData.GetSubMesh(subMesh);
                    var indices = submeshIndices[subMesh];
                    var targetSubmesh = new SubMeshDescriptor(indexStart, indices.Count, MeshTopology.Triangles)
                    {
                        baseVertex = 0,
                        firstVertex = 0,
                        vertexCount = originalVertices.Count,
                        bounds = sourceSubmesh.bounds
                    };
                    targetData.SetSubMesh(subMesh, targetSubmesh, MeshUpdateFlags.DontRecalculateBounds);
                    indexStart += indices.Count;
                }

                Mesh.ApplyAndDisposeWritableMeshData(
                    writable,
                    output,
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                writable = default;
                output.bounds = generatedSource.bounds;
                if (output.lodCount != 1)
                {
                    throw new InvalidOperationException($"Compacted {name} retained Mesh LOD data unexpectedly.");
                }

                return output;
            }
            catch
            {
                if (writable.Length > 0)
                {
                    writable.Dispose();
                }

                Object.DestroyImmediate(output);
                throw;
            }
        }

        private static int ReadIndex(Mesh.MeshData meshData, int index)
        {
            if (meshData.indexFormat == IndexFormat.UInt16)
            {
                return meshData.GetIndexData<ushort>()[index];
            }

            return checked((int)meshData.GetIndexData<uint>()[index]);
        }

        private static void CopyVertexStreams(
            Mesh.MeshData source,
            Mesh.MeshData target,
            IReadOnlyList<int> originalVertices)
        {
            for (var stream = 0; stream < source.vertexBufferCount; stream++)
            {
                var stride = source.GetVertexBufferStride(stream);
                var sourceBytes = source.GetVertexData<byte>(stream);
                var targetBytes = target.GetVertexData<byte>(stream);
                for (var compactVertex = 0; compactVertex < originalVertices.Count; compactVertex++)
                {
                    NativeArray<byte>.Copy(
                        sourceBytes,
                        originalVertices[compactVertex] * stride,
                        targetBytes,
                        compactVertex * stride,
                        stride);
                }
            }
        }

        private static void WriteIndices(
            Mesh.MeshData target,
            IndexFormat indexFormat,
            IReadOnlyList<List<int>> submeshIndices)
        {
            var cursor = 0;
            if (indexFormat == IndexFormat.UInt16)
            {
                var targetIndices = target.GetIndexData<ushort>();
                for (var subMesh = 0; subMesh < submeshIndices.Count; subMesh++)
                {
                    var indices = submeshIndices[subMesh];
                    for (var i = 0; i < indices.Count; i++)
                    {
                        targetIndices[cursor++] = checked((ushort)indices[i]);
                    }
                }

                return;
            }

            var targetIndices32 = target.GetIndexData<uint>();
            for (var subMesh = 0; subMesh < submeshIndices.Count; subMesh++)
            {
                var indices = submeshIndices[subMesh];
                for (var i = 0; i < indices.Count; i++)
                {
                    targetIndices32[cursor++] = checked((uint)indices[i]);
                }
            }
        }
    }
}
