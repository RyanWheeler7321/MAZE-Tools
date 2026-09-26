using System;
using UnityEngine;

namespace Maze
{
    [Serializable]
    public sealed class MazeVectorIconPart
    {
        public string name = "Part";
        public MazeVectorShape shape = MazeVectorShape.Rect(new Vector2(64f, 64f));
        public Vector2 size = new(100f, 100f);
        public Vector2 position = Vector2.zero;
        public MazeVectorRole role = MazeVectorRole.ButtonIcon;
        public bool useStyleOverride;
        public MazeVectorStyle styleOverride;
        public MazeVectorPaint paint;
    }

    [Serializable]
    public sealed class MazeVectorIconDefinition
    {
        public string id = "icon";
        public Vector2 size = new(106f, 106f);
        public MazeVectorIconPart[] parts = Array.Empty<MazeVectorIconPart>();

        public static MazeVectorIconDefinition FromJson(string json)
        {
            return string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<MazeVectorIconDefinition>(json);
        }
    }

    public static class MazeVectorIconLibrary
    {
        public static MazeVectorIconDefinition Book()
        {
            return new MazeVectorIconDefinition
            {
                id = "book",
                parts = new[]
                {
                    Part("Book Left", ClosedPolygon(new Vector2(-0.72f, -0.7f), new Vector2(-0.72f, 0.62f), new Vector2(-0.08f, 0.38f), new Vector2(-0.08f, -0.78f))),
                    Part("Book Right", ClosedPolygon(new Vector2(0.72f, -0.7f), new Vector2(0.72f, 0.62f), new Vector2(0.08f, 0.38f), new Vector2(0.08f, -0.78f))),
                    Part("Book Spine", MazeVectorShape.Line(new Vector2(0f, -0.72f), new Vector2(0f, 0.44f)))
                }
            };
        }

        public static MazeVectorIconDefinition Loop()
        {
            return new MazeVectorIconDefinition
            {
                id = "loop",
                parts = new[]
                {
                    Part("Loop Left", MazeVectorShape.Circle(30f, 40), new Vector2(62f, 62f), new Vector2(-23f, 0f)),
                    Part("Loop Right", MazeVectorShape.Circle(30f, 40), new Vector2(62f, 62f), new Vector2(23f, 0f)),
                    Part("Loop Slash", MazeVectorShape.Line(new Vector2(-0.58f, -0.48f), new Vector2(0.58f, 0.48f)))
                }
            };
        }

        public static MazeVectorIconDefinition OptionsGrid()
        {
            return new MazeVectorIconDefinition
            {
                id = "options_grid",
                parts = new[]
                {
                    Part("Option Cells", new MazeVectorShape { kind = MazeVectorShapeKind.GridCells, size = new Vector2(86f, 86f), gridColumns = 2, gridRows = 2, gridGap = 12f }),
                    Part("Option Accent", new MazeVectorShape { kind = MazeVectorShapeKind.CornerAccents, size = new Vector2(98f, 98f), cornerLength = 18f })
                }
            };
        }

        public static MazeVectorIconDefinition ExitArrow()
        {
            return new MazeVectorIconDefinition
            {
                id = "exit_arrow",
                parts = new[]
                {
                    Part("Exit Door", ClosedPolygon(new Vector2(-0.45f, -0.72f), new Vector2(-0.45f, 0.72f), new Vector2(0.18f, 0.54f), new Vector2(0.18f, -0.54f))),
                    Part("Exit Arrow Shaft", MazeVectorShape.Line(new Vector2(-0.05f, 0f), new Vector2(0.72f, 0f))),
                    Part("Exit Arrow Head A", MazeVectorShape.Line(new Vector2(0.46f, 0.25f), new Vector2(0.72f, 0f))),
                    Part("Exit Arrow Head B", MazeVectorShape.Line(new Vector2(0.46f, -0.25f), new Vector2(0.72f, 0f)))
                }
            };
        }

        public static MazeVectorIconDefinition Diamond()
        {
            return new MazeVectorIconDefinition
            {
                id = "diamond",
                parts = new[]
                {
                    Part("Diamond", new MazeVectorShape { kind = MazeVectorShapeKind.Polygon, points = { new Vector2(0f, 0.84f), new Vector2(0.84f, 0f), new Vector2(0f, -0.84f), new Vector2(-0.84f, 0f) }, closed = true })
                }
            };
        }

        public static MazeVectorIconDefinition Sword()
        {
            return new MazeVectorIconDefinition
            {
                id = "sword",
                parts = new[]
                {
                    Part("Blade", MazeVectorShape.Line(new Vector2(-0.46f, -0.62f), new Vector2(0.56f, 0.58f)), new Vector2(106f, 106f), Vector2.zero),
                    Part("Guard", MazeVectorShape.Line(new Vector2(-0.5f, -0.08f), new Vector2(0.02f, -0.48f)), new Vector2(106f, 106f), Vector2.zero),
                    Part("Handle", MazeVectorShape.Line(new Vector2(-0.62f, -0.76f), new Vector2(-0.34f, -0.48f)), new Vector2(106f, 106f), Vector2.zero)
                }
            };
        }

        public static MazeVectorIconDefinition Shield()
        {
            return new MazeVectorIconDefinition
            {
                id = "shield",
                parts = new[]
                {
                    Part("Shield", new MazeVectorShape { kind = MazeVectorShapeKind.Polygon, points = { new Vector2(-0.58f, 0.55f), new Vector2(0f, 0.72f), new Vector2(0.58f, 0.55f), new Vector2(0.48f, -0.28f), new Vector2(0f, -0.76f), new Vector2(-0.48f, -0.28f) }, closed = true }),
                    Part("Shield Split", MazeVectorShape.Line(new Vector2(0f, 0.54f), new Vector2(0f, -0.5f)))
                }
            };
        }

        public static MazeVectorIconDefinition Potion()
        {
            return new MazeVectorIconDefinition
            {
                id = "potion",
                parts = new[]
                {
                    Part("Bottle", new MazeVectorShape { kind = MazeVectorShapeKind.Polygon, points = { new Vector2(-0.22f, 0.5f), new Vector2(0.22f, 0.5f), new Vector2(0.2f, 0.12f), new Vector2(0.46f, -0.18f), new Vector2(0.34f, -0.72f), new Vector2(-0.34f, -0.72f), new Vector2(-0.46f, -0.18f), new Vector2(-0.2f, 0.12f) }, closed = true }),
                    Part("Cork", OutlineRect(new Vector2(38f, 24f)), new Vector2(38f, 24f), new Vector2(0f, 32f)),
                    Part("Liquid", MazeVectorShape.Line(new Vector2(-0.28f, -0.3f), new Vector2(0.28f, -0.3f)))
                }
            };
        }

        public static MazeVectorIconDefinition Crystal()
        {
            return new MazeVectorIconDefinition
            {
                id = "crystal",
                parts = new[]
                {
                    Part("Crystal", new MazeVectorShape { kind = MazeVectorShapeKind.Polygon, points = { new Vector2(0f, 0.78f), new Vector2(0.48f, 0.18f), new Vector2(0.28f, -0.72f), new Vector2(-0.28f, -0.72f), new Vector2(-0.48f, 0.18f) }, closed = true }),
                    Part("Facet", MazeVectorShape.Line(new Vector2(0f, 0.72f), new Vector2(0f, -0.68f), new Vector2(0.38f, 0.14f)))
                }
            };
        }

        public static MazeVectorIconDefinition Leaf()
        {
            return new MazeVectorIconDefinition
            {
                id = "leaf",
                parts = new[]
                {
                    Part("Leaf Edge", ClosedPolygon(new Vector2(-0.56f, -0.18f), new Vector2(-0.1f, 0.52f), new Vector2(0.62f, 0.48f), new Vector2(0.24f, -0.24f))),
                    Part("Leaf Stem", MazeVectorShape.Line(new Vector2(-0.62f, -0.62f), new Vector2(0.32f, 0.28f)))
                }
            };
        }

        public static MazeVectorIconDefinition Key()
        {
            return new MazeVectorIconDefinition
            {
                id = "key",
                parts = new[]
                {
                    Part("Key Ring", MazeVectorShape.Circle(22f, 28), new Vector2(58f, 58f), new Vector2(-22f, 20f)),
                    Part("Key Stem", MazeVectorShape.Line(new Vector2(-0.02f, 0.12f), new Vector2(0.62f, -0.52f))),
                    Part("Key Teeth", MazeVectorShape.Line(new Vector2(0.34f, -0.22f), new Vector2(0.56f, -0.22f), new Vector2(0.56f, -0.44f)))
                }
            };
        }

        public static MazeVectorIconDefinition Heart()
        {
            return new MazeVectorIconDefinition
            {
                id = "heart",
                parts = new[]
                {
                    Part("Heart", ClosedPolygon(new Vector2(0f, -0.68f), new Vector2(-0.62f, -0.08f), new Vector2(-0.46f, 0.48f), new Vector2(0f, 0.36f), new Vector2(0.46f, 0.48f), new Vector2(0.62f, -0.08f)))
                }
            };
        }

        public static MazeVectorIconDefinition GameplayCrosshair()
        {
            return new MazeVectorIconDefinition
            {
                id = "gameplay_crosshair",
                parts = new[]
                {
                    Part("Outer Ring", MazeVectorShape.Circle(30f, 36), new Vector2(74f, 74f), Vector2.zero),
                    Part("Horizontal Left", MazeVectorShape.Line(new Vector2(-0.82f, 0f), new Vector2(-0.28f, 0f))),
                    Part("Horizontal Right", MazeVectorShape.Line(new Vector2(0.28f, 0f), new Vector2(0.82f, 0f))),
                    Part("Vertical Bottom", MazeVectorShape.Line(new Vector2(0f, -0.82f), new Vector2(0f, -0.28f))),
                    Part("Vertical Top", MazeVectorShape.Line(new Vector2(0f, 0.28f), new Vector2(0f, 0.82f))),
                    Part("Center", new MazeVectorShape { kind = MazeVectorShapeKind.Polygon, points = { new Vector2(0f, 0.16f), new Vector2(0.16f, 0f), new Vector2(0f, -0.16f), new Vector2(-0.16f, 0f) }, closed = true })
                }
            };
        }

        public static MazeVectorIconDefinition GraphicsDisplay()
        {
            return new MazeVectorIconDefinition
            {
                id = "graphics_display",
                parts = new[]
                {
                    Part("Screen", OutlineRect(new Vector2(78f, 50f)), new Vector2(78f, 50f), new Vector2(0f, 2f)),
                    Part("Stand Neck", MazeVectorShape.Line(new Vector2(0f, -0.52f), new Vector2(0f, -0.24f))),
                    Part("Stand Foot", MazeVectorShape.Line(new Vector2(-0.24f, -0.68f), new Vector2(0.24f, -0.68f))),
                    Part("Scan", MazeVectorShape.Line(new Vector2(-0.52f, 0.22f), new Vector2(0.52f, 0.22f))),
                    Part("Pixel", new MazeVectorShape { kind = MazeVectorShapeKind.GridCells, size = new Vector2(34f, 24f), gridColumns = 2, gridRows = 2, gridGap = 5f })
                }
            };
        }

        public static MazeVectorIconDefinition SoundSpeaker()
        {
            return new MazeVectorIconDefinition
            {
                id = "sound_speaker",
                parts = new[]
                {
                    Part("Speaker", ClosedPolygon(new Vector2(-0.72f, -0.24f), new Vector2(-0.42f, -0.24f), new Vector2(-0.08f, -0.56f), new Vector2(-0.08f, 0.56f), new Vector2(-0.42f, 0.24f), new Vector2(-0.72f, 0.24f))),
                    Part("Wave A", MazeVectorShape.Line(new Vector2(0.16f, -0.28f), new Vector2(0.34f, 0f), new Vector2(0.16f, 0.28f))),
                    Part("Wave B", MazeVectorShape.Line(new Vector2(0.38f, -0.48f), new Vector2(0.68f, 0f), new Vector2(0.38f, 0.48f)))
                }
            };
        }

        public static MazeVectorIconDefinition ControlsKeys()
        {
            return new MazeVectorIconDefinition
            {
                id = "controls_keys",
                parts = new[]
                {
                    Part("Key Left", OutlineRect(new Vector2(26f, 26f)), new Vector2(26f, 26f), new Vector2(-24f, 4f)),
                    Part("Key Right", OutlineRect(new Vector2(26f, 26f)), new Vector2(26f, 26f), new Vector2(24f, 4f)),
                    Part("Key Bottom", OutlineRect(new Vector2(26f, 26f)), new Vector2(26f, 26f), new Vector2(0f, -24f)),
                    Part("Left Mark", MazeVectorShape.Line(new Vector2(-0.52f, 0.08f), new Vector2(-0.38f, 0.08f))),
                    Part("Right Mark", MazeVectorShape.Line(new Vector2(0.38f, 0.08f), new Vector2(0.52f, 0.08f))),
                    Part("Bottom Mark", MazeVectorShape.Line(new Vector2(-0.04f, -0.46f), new Vector2(0.12f, -0.46f)))
                }
            };
        }

        private static MazeVectorShape OutlineRect(Vector2 size)
        {
            return new MazeVectorShape { kind = MazeVectorShapeKind.OutlineBand, size = size };
        }

        private static MazeVectorShape ClosedPolygon(params Vector2[] points)
        {
            var shape = new MazeVectorShape { kind = MazeVectorShapeKind.Polygon, closed = true };
            if (points != null)
            {
                shape.points.AddRange(points);
            }
            return shape;
        }

        private static MazeVectorIconPart Part(string name, MazeVectorShape shape)
        {
            return Part(name, shape, new Vector2(106f, 106f), Vector2.zero);
        }

        private static MazeVectorIconPart Part(string name, MazeVectorShape shape, Vector2 size, Vector2 position)
        {
            return new MazeVectorIconPart { name = name, shape = shape, size = size, position = position, role = MazeVectorRole.ButtonIcon };
        }
    }
}
