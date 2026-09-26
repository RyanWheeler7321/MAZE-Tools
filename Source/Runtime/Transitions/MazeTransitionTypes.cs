using System;
using UnityEngine;

namespace Maze
{
    public enum MazeTransitionBackend
    {
        Overlay = 0,
        CameraMesh = 1,
        GpuFullscreen = 2,
    }

    public enum MazeTransitionDirection
    {
        In = 0,
        Out = 1,
    }

    public enum MazeTransitionOrigin
    {
        Center = 0,
        Point = 1,
        Left = 2,
        Right = 3,
        Top = 4,
        Bottom = 5,
        TopLeft = 6,
        TopRight = 7,
        BottomLeft = 8,
        BottomRight = 9,
        Edges = 10,
        Corners = 11,
        Random = 12,
    }

    public enum MazeTransitionOrder
    {
        Distance = 0,
        RowScan = 1,
        ColumnScan = 2,
        ReverseRowScan = 3,
        ReverseColumnScan = 4,
        RandomSeeded = 5,
    }

    public enum MazeTransitionSpreadDirection
    {
        FromOrigin = 0,
        LeftToRight = 1,
        RightToLeft = 2,
        BottomToTop = 3,
        TopToBottom = 4,
        TopLeftToBottomRight = 5,
        TopRightToBottomLeft = 6,
        BottomLeftToTopRight = 7,
        BottomRightToTopLeft = 8,
        CenterOut = 9,
        EdgesIn = 10,
        CornersIn = 11,
        RandomSide = 12,
    }

    public enum MazeSquareTransitionPatternKind
    {
        TessellateWave = 0,
        RandomTessellate = 1,
        InkGridLock = 2,
        CrosshairSweep = 3,
        VenetianRuneBlinds = 4,
        CardFlipSquares = 5,
        CornerBracketCollapse = 6,
        CreepingBlueprintLines = 7,
        ShutterApertureRects = 8,
        CheckeredDesync = 9,
        PageCutSlits = 10,
        SignalCorruptionBlocks = 11,
        BlockfallLock = 12,
        CircuitTraceFill = 13,
        GatefoldGrid = 14,
        ZoomLattice = 15,
        DominoColumns = 16,
        CourierBlocks = 17,
        SquaredCircuitDecay = 18,
        VideowallSnap = 19,
    }

    public enum MazeVectorTransitionPatternKind
    {
        VectorSigilSnap = 0,
        BlackPageTurn = 1,
        RuneGuillotine = 2,
        BoxIris = 3,
        GridWireframeCollapse = 4,
        DiagonalDraftingWipe = 5,
        DoorLattice = 6,
        PulseFrameCrush = 7,
        GlyphSlotMachine = 8,
        HardCutAfterimage = 9,
    }

    public enum MazeVectorTransitionElementKind
    {
        FillRect = 0,
        StrokeRect = 1,
        CornerAccents = 2,
        DashedOutline = 3,
    }

    public enum MazeTransitionEase
    {
        Linear = 0,
        Smooth = 1,
        EaseIn = 2,
        EaseOut = 3,
    }

    public enum MazeTransitionState
    {
        Hidden = 0,
        PlayingIn = 1,
        Covered = 2,
        PlayingOut = 3,
    }

    public enum MazeTransitionFlickerWeighting
    {
        Auto = 0,
        Even = 1,
        Center = 2,
        Edges = 3,
    }

    public struct MazeTransitionContext
    {
        public Camera Camera;
        public Transform Parent;
        public Vector2 ScreenPoint;
        public bool HasScreenPoint;
        public bool ClearOnCovered;
        public bool PlayOutAfterCovered;
        public bool HasRandomSeed;
        public int RandomSeed;
        public bool Flicker;
        public float FlickerDensity;
        public MazeTransitionFlickerWeighting FlickerWeighting;
        public bool Additive;
        public int SortingOrderOffset;
        public int AdditiveMaxActive;
        public string Label;
        public Action OnCovered;
        public Action OnComplete;

        public static MazeTransitionContext Default(string label = null)
        {
            return new MazeTransitionContext
            {
                Label = string.IsNullOrWhiteSpace(label) ? "transition" : label,
            };
        }

        public static MazeTransitionContext FromScreenPoint(Vector2 screenPoint, string label = null)
        {
            return new MazeTransitionContext
            {
                ScreenPoint = screenPoint,
                HasScreenPoint = true,
                Label = string.IsNullOrWhiteSpace(label) ? "transition" : label,
            };
        }
    }

    public readonly struct MazeTransitionTileFrame
    {
        public readonly Rect Rect;
        public readonly Color Color;
        public readonly bool Visible;

        public MazeTransitionTileFrame(Rect rect, Color color, bool visible)
        {
            Rect = rect;
            Color = color;
            Visible = visible;
        }
    }

    public readonly struct MazeTransitionVectorFrame
    {
        public readonly Rect Rect;
        public readonly Color FillColor;
        public readonly Color StrokeColor;
        public readonly float StrokeThickness;
        public readonly bool Visible;
        public readonly MazeVectorTransitionElementKind Kind;
        public readonly float CornerLength;
        public readonly float DashLength;
        public readonly float DashGap;
        public readonly float Progress;

        public MazeTransitionVectorFrame(
            Rect rect,
            Color fillColor,
            Color strokeColor,
            float strokeThickness,
            bool visible,
            MazeVectorTransitionElementKind kind,
            float cornerLength,
            float dashLength,
            float dashGap,
            float progress = 1f)
        {
            Rect = rect;
            FillColor = fillColor;
            StrokeColor = strokeColor;
            StrokeThickness = strokeThickness;
            Visible = visible;
            Kind = kind;
            CornerLength = cornerLength;
            DashLength = dashLength;
            DashGap = dashGap;
            Progress = progress;
        }
    }
}
