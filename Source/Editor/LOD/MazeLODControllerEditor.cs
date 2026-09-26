using UnityEditor;
using UnityEngine;

namespace Maze.Editor
{
    [CustomEditor(typeof(MazeLODController))]
    public sealed class MazeLODControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty defaultSettings;

        private void OnEnable()
        {
            defaultSettings = serializedObject.FindProperty("defaultSettings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            MazeLODInspectorUtility.DrawSettings(defaultSettings);
            serializedObject.ApplyModifiedProperties();

            var controller = (MazeLODController)target;
            if (!controller.DefaultSettings.TryValidate(out var error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Rebuild Scene LODs"))
                {
                    MazeLODBaker.RebuildScene(controller);
                }
            }
        }
    }
}
