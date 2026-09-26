using UnityEngine;

namespace Maze
{
    public abstract class MazeTransitionProfile : ScriptableObject
    {
        [Header("Runtime")]
        [SerializeField] private MazeTransitionBackend backend = MazeTransitionBackend.Overlay;
        [SerializeField] private bool blockInput = true;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private int sortingOrder = 32700;
        [SerializeField] private string diagnosticsLabel = "transition";

        public MazeTransitionBackend Backend => backend;
        public bool BlockInput => blockInput;
        public bool UseUnscaledTime => useUnscaledTime;
        public int SortingOrder => sortingOrder;
        public string DiagnosticsLabel => string.IsNullOrWhiteSpace(diagnosticsLabel) ? name : diagnosticsLabel;
        public abstract string TransitionId { get; }

        protected void SetBackendForRuntime(MazeTransitionBackend nextBackend)
        {
            backend = nextBackend;
        }
    }
}
