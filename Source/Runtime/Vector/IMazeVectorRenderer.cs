using UnityEngine;

namespace Maze
{
    public interface IMazeVectorRenderer
    {
        float Progress { get; }
        void SetState(MazeVectorState state);
        void SetProgress(float progress);
        void SetVisible(bool visible);
        void SetSelected(bool selected);
        void MarkGeometryDirty();
    }

    public interface IMazeVectorShapeRenderer
    {
        MazeVectorShape Shape { get; }
        void SetShape(MazeVectorShape shape);
    }

    public interface IMazeVectorStyleRenderer
    {
        MazeVectorStyle Style { get; }
        void SetStyle(MazeVectorStyle style);
    }

    public interface IMazeVectorPaintRenderer
    {
        MazeVectorPaint Paint { get; }
        void SetPaint(MazeVectorPaint paint);
    }

    public interface IMazeVectorOutputRenderer
    {
        float OutputIntensity { get; }
        void SetOutputIntensity(float intensity);
    }

    // Generated vector UI that rebuilds its preview in edit mode.
    public interface IMazeVectorPreviewOwner
    {
        void RefreshVectorPreview();
    }

    public interface IMazeVectorImmediateRefresh
    {
        void RefreshVectorNow();
    }
}
