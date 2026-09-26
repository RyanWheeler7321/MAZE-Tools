using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace Maze
{
    public enum MazeRenderPassKind
    {
        Unknown = 0,
        Fullscreen = 1,
        GeometryRedraw = 2,
        Mask = 3,
        Bloom = 4,
        Debug = 5,
    }

    public sealed class MazeRenderPassRecord
    {
        public string Name;
        public MazeRenderPassKind Kind;
        public int EstimatedStages;
        public int Calls;
    }

    public sealed class MazeRenderService
    {
        private readonly List<MazeRenderPassRecord> currentFramePasses = new();
        private int recordedFrame = -1;

        public IReadOnlyList<MazeRenderPassRecord> CurrentFramePasses => currentFramePasses;

        public void RecordPass(string name, MazeRenderPassKind kind, int estimatedStages = 1)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Unnamed Render Pass";
            }

            BeginFrameIfNeeded();
            for (var i = 0; i < currentFramePasses.Count; i++)
            {
                var existing = currentFramePasses[i];
                if (!string.Equals(existing.Name, name, StringComparison.Ordinal) || existing.Kind != kind)
                {
                    continue;
                }

                existing.Calls++;
                existing.EstimatedStages += Mathf.Max(1, estimatedStages);
                return;
            }

            currentFramePasses.Add(new MazeRenderPassRecord
            {
                Name = name,
                Kind = kind,
                Calls = 1,
                EstimatedStages = Mathf.Max(1, estimatedStages),
            });
        }

        public string BuildCurrentFrameSummary()
        {
            BeginFrameIfNeeded();
            if (currentFramePasses.Count == 0)
            {
                return "no MAZE render passes recorded this frame";
            }

            var total = 0;
            for (var i = 0; i < currentFramePasses.Count; i++)
            {
                total += currentFramePasses[i].EstimatedStages;
            }

            return $"{currentFramePasses.Count} MAZE pass groups, about {total} stages";
        }

        private void BeginFrameIfNeeded()
        {
            var frame = Time.frameCount;
            if (recordedFrame == frame)
            {
                return;
            }

            recordedFrame = frame;
            currentFramePasses.Clear();
        }
    }

    public static class MazeRenderFrameServices
    {
        public static MazeRenderService Current { get; set; } = new();

        public static void RecordPass(string name, MazeRenderPassKind kind, int estimatedStages = 1)
        {
            Current?.RecordPass(name, kind, estimatedStages);
        }

        public static TextureHandle CreatePerPassActiveColorCopy(RenderGraph renderGraph, TextureHandle source, string passLabel, string copyPassName)
        {
            // Compatibility helper for exceptional passes that cannot swap cameraColor.
            // Normal MAZE fullscreen passes should render to a new cameraColor and assign resourceData.cameraColor.
            var descriptor = renderGraph.GetTextureDesc(source);
            descriptor.name = passLabel + "_Input";
            descriptor.clearBuffer = false;
            var inputTexture = renderGraph.CreateTexture(descriptor);
            renderGraph.AddBlitPass(source, inputTexture, Vector2.one, Vector2.zero, passName: copyPassName);
            return inputTexture;
        }
    }
}
