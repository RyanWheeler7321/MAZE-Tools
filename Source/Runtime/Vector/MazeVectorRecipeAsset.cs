using System.Collections.Generic;
using UnityEngine;

namespace Maze
{
    [CreateAssetMenu(menuName = "MAZE/Vector/Recipe", fileName = "MazeVectorRecipe")]
    public sealed class MazeVectorRecipeAsset : ScriptableObject
    {
        [SerializeField] private string recipeId = "recipe";
        [SerializeField] private Vector2 size = new(100f, 100f);
        [SerializeField] private MazeVectorProfile defaultProfile;
        [SerializeField] private MazeVectorLayerSet defaultLayers = new();
        [SerializeField] private bool defaultBatch;
        [SerializeField] private List<MazeVectorElement> elements = new();

        public string RecipeId => string.IsNullOrWhiteSpace(recipeId) ? name : recipeId;
        public Vector2 Size => size;
        public MazeVectorProfile DefaultProfile => defaultProfile;
        public MazeVectorLayerSet DefaultLayers => defaultLayers ??= new MazeVectorLayerSet();
        public bool DefaultBatch => defaultBatch;
        public IReadOnlyList<MazeVectorElement> Elements => elements;

        public MazeVectorRecipe CreateRecipe()
        {
            var recipe = new MazeVectorRecipe(RecipeId, size == Vector2.zero ? Vector2.one * 100f : size);
            if (elements == null)
            {
                return recipe;
            }

            for (var i = 0; i < elements.Count; i++)
            {
                recipe.Add(elements[i]);
            }
            return recipe;
        }

        public void SetFromRecipe(MazeVectorRecipe recipe)
        {
            recipeId = recipe != null ? recipe.Id : "recipe";
            size = recipe != null ? recipe.Size : Vector2.one * 100f;
            elements ??= new List<MazeVectorElement>();
            elements.Clear();
            if (recipe?.Elements == null)
            {
                return;
            }

            for (var i = 0; i < recipe.Elements.Count; i++)
            {
                elements.Add(recipe.Elements[i]?.Clone());
            }
        }
    }
}
