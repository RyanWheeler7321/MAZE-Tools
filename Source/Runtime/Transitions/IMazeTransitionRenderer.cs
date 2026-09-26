using UnityEngine;

namespace Maze
{
    internal interface IMazeTransitionRenderer
    {
        void Build(MazeSquareTransitionProfile profile, Rect screenRect, Transform parent, Camera camera);
        void SetSortingOrder(int sortingOrder);
        void SetTile(int index, MazeTransitionTileFrame frame);
        void Upload();
        void SetVisible(bool visible);
        void Release();
    }
}
