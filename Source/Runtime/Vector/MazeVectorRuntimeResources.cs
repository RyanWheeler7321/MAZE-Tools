using UnityEngine;

namespace Maze
{
    // Owns the generated MazeVector materials and textures.
    public static class MazeVectorRuntimeResources
    {
        private const string UiOutputShaderResource = "MazeVectorUI";
        private static Material worldSolidMaterial;
        private static Material uiOutputMaterial;

        public static int GeneratedMaterialCount => MazeVectorPaintMaterials.GeneratedMaterialCount
            + (worldSolidMaterial != null ? 1 : 0)
            + (uiOutputMaterial != null ? 1 : 0);
        public static int GeneratedTextureCount => MazeVectorPaintMaterials.GeneratedTextureCount;

        public static Material GetUiOutputMaterial()
        {
            if (uiOutputMaterial != null)
            {
                return uiOutputMaterial;
            }

            var shader = Resources.Load<Shader>(UiOutputShaderResource) ?? Shader.Find("Hidden/MAZE/VectorUI");
            if (shader == null)
            {
                MazeDiagnosticsLog.WarnOnce(
                    "maze.vector.ui_output_shader_missing",
                    "Maze.Vector",
                    "ui_output_shader_missing",
                    "MazeVector UI output shader is missing",
                    MazeDiagnosticsLog.JsonString("resource", UiOutputShaderResource));
                return null;
            }

            uiOutputMaterial = new Material(shader)
            {
                name = "MazeVector UI Output",
                hideFlags = HideFlags.HideAndDontSave
            };
            return uiOutputMaterial;
        }

        public static Material GetWorldSolidMaterial()
        {
            if (worldSolidMaterial != null)
            {
                return worldSolidMaterial;
            }

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default") ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                return null;
            }
            worldSolidMaterial = new Material(shader)
            {
                name = "MazeVector World Solid",
                hideFlags = HideFlags.HideAndDontSave
            };
            return worldSolidMaterial;
        }

        public static void Reset()
        {
            MazeVectorPaintMaterials.ReleaseGeneratedResources();
            DestroyTransient(worldSolidMaterial);
            DestroyTransient(uiOutputMaterial);
            worldSolidMaterial = null;
            uiOutputMaterial = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForRuntime()
        {
            Reset();
        }

        internal static void DestroyTransient(Object value)
        {
            if (value == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Object.Destroy(value);
            }
            else
            {
                Object.DestroyImmediate(value);
            }
        }
    }
}
