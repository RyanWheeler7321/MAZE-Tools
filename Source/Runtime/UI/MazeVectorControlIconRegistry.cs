using System;
using UnityEngine;

namespace Maze
{
    [Serializable]
    public struct MazeVectorControlIconEntry
    {
        public string key;
        public string assetName;
    }

    [CreateAssetMenu(menuName = "MAZE/Vector/Control Icon Registry", fileName = "MazeVectorControlIconRegistry")]
    public sealed class MazeVectorControlIconRegistry : ScriptableObject
    {
        [SerializeField] private MazeVectorControlIconEntry[] entries = Array.Empty<MazeVectorControlIconEntry>();

        public MazeVectorControlIconEntry[] Entries => entries;

        public void SetEntries(MazeVectorControlIconEntry[] nextEntries)
        {
            entries = nextEntries ?? Array.Empty<MazeVectorControlIconEntry>();
        }
    }
}
