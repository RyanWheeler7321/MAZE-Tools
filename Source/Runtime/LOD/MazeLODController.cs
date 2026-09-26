using UnityEngine;

namespace Maze
{
    [DisallowMultipleComponent]
    public sealed class MazeLODController : MonoBehaviour
    {
        [SerializeField] private MazeLODSettings defaultSettings = default;

        public MazeLODSettings DefaultSettings => defaultSettings.levelCount > 0
            ? defaultSettings
            : MazeLODSettings.Default;

        private void Reset()
        {
            name = "LOD";
            defaultSettings = MazeLODSettings.Default;
        }

        internal void EnsureDefaults()
        {
            if (defaultSettings.levelCount <= 0)
            {
                defaultSettings = MazeLODSettings.Default;
            }
        }
    }
}
