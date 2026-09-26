using UnityEngine;

namespace Maze
{
    [CreateAssetMenu(menuName = "MAZE/Vector/Preset", fileName = "MazeVectorPreset")]
    public sealed class MazeVectorPreset : ScriptableObject
    {
        [SerializeField] private MazeVectorRenderMode renderMode = MazeVectorRenderMode.Overlay;
        [SerializeField] private MazeVectorShape shape = new();
        [SerializeField] private MazeVectorStyle style = new();

        public MazeVectorRenderMode RenderMode => renderMode;
        public MazeVectorShape Shape => shape;
        public MazeVectorStyle Style => style;
    }
}
