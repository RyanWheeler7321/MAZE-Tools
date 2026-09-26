using UnityEngine;

namespace Maze
{
    [DisallowMultipleComponent]
    public sealed class MazeVectorTransitionDriver : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour[] renderers = new MonoBehaviour[0];
        [SerializeField] private MazeVectorTransition transition = MazeVectorTransition.Reveal;
        [SerializeField, Min(0.01f)] private float duration = 0.2f;
        [SerializeField] private bool playOnEnable = true;

        private float startTime;
        private bool playing;

        public MazeVectorTransition Transition => transition;
        public bool IsPlaying => playing;

        private void OnEnable()
        {
            if (playOnEnable)
            {
                Play();
            }
        }

        private void Update()
        {
            if (!playing)
            {
                return;
            }

            var raw = Mathf.Clamp01((Time.unscaledTime - startTime) / Mathf.Max(0.01f, duration));
            var progress = transition == MazeVectorTransition.Pulse ? Mathf.PingPong(raw * 2f, 1f) : raw;
            ApplyProgress(progress);
            if (raw >= 1f && transition != MazeVectorTransition.Pulse)
            {
                playing = false;
            }
        }

        public void Play()
        {
            startTime = Time.unscaledTime;
            playing = true;
            ApplyProgress(0f);
        }

        public void SetProgress(float progress)
        {
            playing = false;
            ApplyProgress(Mathf.Clamp01(progress));
        }

        private void ApplyProgress(float progress)
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is IMazeVectorRenderer vectorRenderer)
                {
                    vectorRenderer.SetProgress(progress);
                }
            }
        }
    }
}
