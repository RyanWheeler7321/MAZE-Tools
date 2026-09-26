using System;
using System.Diagnostics;
using UnityEngine.Profiling;

namespace Maze
{
    public readonly struct MazeProfileScope : IDisposable
    {
        private readonly MazeProfiler profiler;
        private readonly string sampleName;
        private readonly long startTimestamp;
        private readonly bool recordRuntimeSample;
        private readonly bool profilerSampleStarted;

        internal MazeProfileScope(MazeProfiler profiler, string sampleName, bool recordRuntimeSample, bool profilerSampleStarted)
        {
            this.profiler = profiler;
            this.sampleName = sampleName;
            this.recordRuntimeSample = recordRuntimeSample;
            this.profilerSampleStarted = profilerSampleStarted;
            startTimestamp = recordRuntimeSample ? Stopwatch.GetTimestamp() : 0L;
        }

        public void Dispose()
        {
            if (profilerSampleStarted)
            {
                Profiler.EndSample();
            }

            if (!recordRuntimeSample || profiler == null || string.IsNullOrWhiteSpace(sampleName))
            {
                return;
            }

            var elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            var elapsedMs = elapsedTicks * 1000.0 / Stopwatch.Frequency;
            profiler.RecordScopeSample(sampleName, elapsedMs);
        }
    }
}
