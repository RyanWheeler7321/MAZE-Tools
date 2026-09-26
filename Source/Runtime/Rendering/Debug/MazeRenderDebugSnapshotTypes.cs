using System;
using System.Collections.Generic;

namespace Maze
{
    public interface IMazeRenderDebugProvider
    {
        string RenderDebugProviderId { get; }
        string RenderDebugDisplayName { get; }
        bool IsRenderDebugAvailable { get; }
        void AddRenderDebugViews(List<MazeRenderDebugView> views);
        void AddRenderDebugProbeChannels(List<MazeRenderDebugProbeChannel> channels);
        object CaptureRenderDebugState();
        void RestoreRenderDebugState(object state);
        void SetRenderDebugView(string viewId);
        void ClearRenderDebugView();
        string BuildRenderDebugStateJson();
    }

    public sealed class MazeRenderDebugView
    {
        public MazeRenderDebugView(string providerId, string viewId, string displayName, string fileName, bool sampleProbes = false)
        {
            ProviderId = providerId ?? string.Empty;
            ViewId = viewId ?? string.Empty;
            DisplayName = displayName ?? viewId ?? string.Empty;
            FileName = string.IsNullOrWhiteSpace(fileName) ? (viewId ?? "debug_view") + ".png" : fileName;
            SampleProbes = sampleProbes;
        }

        public string ProviderId { get; }
        public string ViewId { get; }
        public string DisplayName { get; }
        public string FileName { get; }
        public bool SampleProbes { get; }
    }

    public sealed class MazeRenderDebugProbeChannel
    {
        public MazeRenderDebugProbeChannel(string providerId, string viewId, string channel, string meaning)
        {
            ProviderId = providerId ?? string.Empty;
            ViewId = viewId ?? string.Empty;
            Channel = channel ?? string.Empty;
            Meaning = meaning ?? string.Empty;
        }

        public string ProviderId { get; }
        public string ViewId { get; }
        public string Channel { get; }
        public string Meaning { get; }
    }
}
