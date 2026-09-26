using System;

namespace Maze
{
    public enum MazeTransitionResult
    {
        None = 0,
        Completed = 1,
        Cancelled = 2,
        Replaced = 3,
    }

    public sealed class MazeTransitionHandle
    {
        public bool IsActive { get; private set; }
        public bool IsCovered { get; private set; }
        public bool IsComplete { get; private set; }
        public bool IsFinished => Result != MazeTransitionResult.None;
        public MazeTransitionResult Result { get; private set; }
        public string Label { get; }
        public long OperationId { get; }

        public event Action Covered;
        public event Action Complete;
        public event Action Cancelled;
        public event Action<MazeTransitionResult> Finished;

        private readonly Action<long> releaseRequest;
        private readonly Action<long> cancelRequest;

        internal MazeTransitionHandle(long operationId, string label, Action<long> releaseRequest = null, Action<long> cancelRequest = null)
        {
            OperationId = operationId;
            Label = string.IsNullOrWhiteSpace(label) ? "transition" : label;
            this.releaseRequest = releaseRequest;
            this.cancelRequest = cancelRequest;
            IsActive = true;
        }

        public void Release()
        {
            if (!IsFinished)
            {
                releaseRequest?.Invoke(OperationId);
            }
        }

        public void Cancel()
        {
            if (!IsFinished)
            {
                cancelRequest?.Invoke(OperationId);
            }
        }

        internal void MarkCovered()
        {
            if (IsCovered)
            {
                return;
            }

            IsCovered = true;
            Covered?.Invoke();
        }

        internal void MarkFinished(MazeTransitionResult result)
        {
            if (result == MazeTransitionResult.None || IsFinished)
            {
                return;
            }

            IsActive = false;
            Result = result;
            IsComplete = result == MazeTransitionResult.Completed;
            if (result == MazeTransitionResult.Completed)
            {
                Complete?.Invoke();
            }
            else if (result == MazeTransitionResult.Cancelled)
            {
                Cancelled?.Invoke();
            }
            Finished?.Invoke(result);
        }
    }
}
