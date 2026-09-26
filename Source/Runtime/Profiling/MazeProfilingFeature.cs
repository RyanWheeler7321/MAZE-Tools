using System;
using System.Collections.Generic;

namespace Maze
{
    public interface IMazeProfilingFeatureProvider
    {
        void AddMazeProfilingFeatures(List<MazeProfilingFeatureHandle> features);
    }

    public sealed class MazeProfilingFeatureHandle
    {
        private readonly Func<string> stateGetter;
        private readonly Action<bool> enabledSetter;
        private readonly Func<object> captureState;
        private readonly Action<object> restoreState;

        public MazeProfilingFeatureHandle(
            string id,
            string label,
            string category,
            Func<string> stateGetter,
            Action<bool> enabledSetter,
            Func<object> captureState,
            Action<object> restoreState,
            string qualityVariants = null)
        {
            Id = id;
            Label = label;
            Category = category;
            this.stateGetter = stateGetter;
            this.enabledSetter = enabledSetter;
            this.captureState = captureState;
            this.restoreState = restoreState;
            QualityVariants = qualityVariants ?? string.Empty;
        }

        public string Id { get; }
        public string Label { get; }
        public string Category { get; }
        public string QualityVariants { get; }
        public string CurrentState => stateGetter != null ? stateGetter() : string.Empty;

        public object CaptureState()
        {
            return captureState != null ? captureState() : null;
        }

        public void SetEnabled(bool enabled)
        {
            enabledSetter?.Invoke(enabled);
        }

        public void RestoreState(object state)
        {
            restoreState?.Invoke(state);
        }
    }

}
