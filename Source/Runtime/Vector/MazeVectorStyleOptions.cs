using System;

namespace Maze
{
    public enum MazeVectorStrokeCap
    {
        Butt,
        Square,
        Round
    }

    public enum MazeVectorStrokeJoin
    {
        Bevel,
        Round,
        Miter
    }

    public enum MazeVectorCornerMode
    {
        Rounded,
        Sharp,
        Cut,
        Bracket,
        Stepped
    }

    public enum MazeVectorShadingMode
    {
        Flat = 0,
        VerticalGradient = 1,
        Rim = 2,
        Inset = 3,
        Raised = 4,
        HorizontalGradient = 5,
        DiagonalGradient = 6,
        InverseDiagonalGradient = 7,
        AmorphousGradient = 8
    }

    public enum MazeVectorStrokePersonality
    {
        Plain,
        ThinBright,
        ThickAccent,
        SplitGapped,
        Hooked,
        Ticked,
        Stepped,
        Doubled,
        UnevenAccent
    }

    public enum MazeVectorStrokeGradientMode
    {
        None,
        AlongPath,
        Spatial
    }

    [Serializable]
    public struct MazeVectorStrokeGradientStop
    {
        public float position;
        public UnityEngine.Color color;

        public MazeVectorStrokeGradientStop(float position, UnityEngine.Color color)
        {
            this.position = position;
            this.color = color;
        }
    }

    [Flags]
    public enum MazeVectorDirtyFlags
    {
        None = 0,
        Geometry = 1 << 0,
        Style = 1 << 1,
        Progress = 1 << 2,
        State = 1 << 3,
        Visibility = 1 << 4,
        Transform = 1 << 5,
        Render = 1 << 6,
        All = Geometry | Style | Progress | State | Visibility | Transform | Render
    }

    public enum MazeVectorRole
    {
        ButtonPlate,
        ButtonIcon,
        HintBar,
        PanelFrame,
        LabelBacker,
        GalleryCard,
        Accent,
        Warning
    }

    public enum MazeVectorState
    {
        Idle,
        Selected,
        Pressed,
        Disabled,
        Warning
    }

    [Serializable]
    public struct MazeVectorStyleKey
    {
        public MazeVectorRole role;
        public MazeVectorState state;
        public MazeVectorStyle style;
    }
}
