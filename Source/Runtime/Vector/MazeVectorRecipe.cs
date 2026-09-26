using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maze
{
    public enum MazeVectorLayer
    {
        Fill,
        Stroke,
        Highlight,
        Accent,
        Shadow,
        Interaction,
        Debug
    }

    [Serializable]
    public sealed class MazeVectorElement
    {
        public string name = "Vector Element";
        public MazeVectorShape shape = MazeVectorShape.Rect(new Vector2(80f, 80f));
        public Vector2 size = new(80f, 80f);
        public Vector2 position = Vector2.zero;
        public MazeVectorRole role = MazeVectorRole.Accent;
        public MazeVectorLayer layer = MazeVectorLayer.Stroke;
        public bool useStyleOverride;
        public MazeVectorStyle styleOverride;
        public MazeVectorPaint paint;

        public MazeVectorElement Clone()
        {
            return new MazeVectorElement
            {
                name = name,
                shape = shape != null ? shape.Clone() : null,
                size = size,
                position = position,
                role = role,
                layer = layer,
                useStyleOverride = useStyleOverride,
                styleOverride = styleOverride != null ? styleOverride.Clone() : null,
                paint = paint != null ? paint.Clone() : null
            };
        }

        public MazeVectorIconPart ToIconPart()
        {
            return new MazeVectorIconPart
            {
                name = name,
                shape = shape != null ? shape.Clone() : null,
                size = size,
                position = position,
                role = role,
                useStyleOverride = useStyleOverride,
                styleOverride = styleOverride != null ? styleOverride.Clone() : null,
                paint = paint != null ? paint.Clone() : null
            };
        }

        public static MazeVectorElement FromIconPart(MazeVectorIconPart part, MazeVectorLayer layer = MazeVectorLayer.Stroke)
        {
            if (part == null)
            {
                return null;
            }
            return new MazeVectorElement
            {
                name = part.name,
                shape = part.shape != null ? part.shape.Clone() : null,
                size = part.size,
                position = part.position,
                role = part.role,
                layer = layer,
                useStyleOverride = part.useStyleOverride,
                styleOverride = part.styleOverride != null ? part.styleOverride.Clone() : null,
                paint = part.paint != null ? part.paint.Clone() : null
            };
        }
    }

    [Serializable]
    public sealed class MazeVectorRecipe
    {
        [SerializeField] private string id = "recipe";
        [SerializeField] private Vector2 size = new(100f, 100f);
        [SerializeField] private List<MazeVectorElement> elements = new();

        public string Id { get => id; set => id = string.IsNullOrWhiteSpace(value) ? "recipe" : value; }
        public Vector2 Size { get => size; set => size = value; }
        public IReadOnlyList<MazeVectorElement> Elements => elements;
        public int Count => elements != null ? elements.Count : 0;

        public MazeVectorRecipe() { }

        public MazeVectorRecipe(string id, Vector2 size)
        {
            Id = id;
            this.size = size;
        }

        public MazeVectorRecipe Add(string name, MazeVectorShape shape, Vector2 elementSize, Vector2 position, MazeVectorStyle style, MazeVectorLayer layer = MazeVectorLayer.Stroke, MazeVectorRole role = MazeVectorRole.Accent, MazeVectorPaint paint = null)
        {
            elements ??= new List<MazeVectorElement>();
            elements.Add(new MazeVectorElement
            {
                name = name,
                shape = shape != null ? shape.Clone() : null,
                size = elementSize,
                position = position,
                role = role,
                layer = layer,
                useStyleOverride = style != null,
                styleOverride = style != null ? style.Clone() : null,
                paint = paint != null ? paint.Clone() : null
            });
            return this;
        }

        public MazeVectorRecipe Add(MazeVectorElement element)
        {
            if (element == null)
            {
                return this;
            }
            elements ??= new List<MazeVectorElement>();
            elements.Add(element.Clone());
            return this;
        }

        public MazeVectorRecipe Clone()
        {
            var clone = new MazeVectorRecipe(id, size);
            if (elements != null)
            {
                for (var i = 0; i < elements.Count; i++)
                {
                    clone.Add(elements[i]);
                }
            }
            return clone;
        }

        public MazeVectorIconDefinition ToIconDefinition()
        {
            var parts = elements == null ? Array.Empty<MazeVectorIconPart>() : new MazeVectorIconPart[elements.Count];
            for (var i = 0; i < parts.Length; i++)
            {
                parts[i] = elements[i] != null ? elements[i].ToIconPart() : null;
            }
            return new MazeVectorIconDefinition { id = id, size = size, parts = parts };
        }

        public static MazeVectorRecipe FromIconDefinition(MazeVectorIconDefinition definition, MazeVectorLayer layer = MazeVectorLayer.Stroke)
        {
            var recipe = new MazeVectorRecipe(definition != null ? definition.id : "icon", definition != null ? definition.size : Vector2.one * 100f);
            if (definition?.parts == null)
            {
                return recipe;
            }
            for (var i = 0; i < definition.parts.Length; i++)
            {
                recipe.Add(MazeVectorElement.FromIconPart(definition.parts[i], layer));
            }
            return recipe;
        }
    }

    [Serializable]
    public sealed class MazeVectorLayerSet
    {
        public bool fill = true;
        public bool stroke = true;
        public bool highlight = true;
        public bool accent = true;
        public bool shadow = true;
        public bool interaction = true;
        public bool debug;

        public static MazeVectorLayerSet All => new() { debug = true };
        public static MazeVectorLayerSet ArtOnly => new() { interaction = false, debug = false };

        public bool Allows(MazeVectorLayer layer)
        {
            return layer switch
            {
                MazeVectorLayer.Fill => fill,
                MazeVectorLayer.Stroke => stroke,
                MazeVectorLayer.Highlight => highlight,
                MazeVectorLayer.Accent => accent,
                MazeVectorLayer.Shadow => shadow,
                MazeVectorLayer.Interaction => interaction,
                MazeVectorLayer.Debug => debug,
                _ => true
            };
        }
    }
}
