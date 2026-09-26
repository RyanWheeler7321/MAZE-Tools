using UnityEditor;
using UnityEngine;

namespace Maze.Editor
{
    [CustomEditor(typeof(MazeLODObject))]
    public sealed class MazeLODObjectEditor : UnityEditor.Editor
    {
        private SerializedProperty controller;
        private SerializedProperty sourceMeshFilter;
        private SerializedProperty sourceRenderer;
        private SerializedProperty useControllerSettings;
        private SerializedProperty overrideSettings;

        private void OnEnable()
        {
            controller = serializedObject.FindProperty("controller");
            sourceMeshFilter = serializedObject.FindProperty("sourceMeshFilter");
            sourceRenderer = serializedObject.FindProperty("sourceRenderer");
            useControllerSettings = serializedObject.FindProperty("useControllerSettings");
            overrideSettings = serializedObject.FindProperty("overrideSettings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(controller);
            EditorGUILayout.PropertyField(sourceMeshFilter, new GUIContent("Source Mesh"));
            EditorGUILayout.PropertyField(sourceRenderer, new GUIContent("Source Renderer"));
            EditorGUILayout.PropertyField(useControllerSettings, new GUIContent("Use Scene Settings"));
            if (!useControllerSettings.boolValue)
            {
                EditorGUILayout.Space(4f);
                MazeLODInspectorUtility.DrawSettings(overrideSettings);
            }

            serializedObject.ApplyModifiedProperties();

            var lodObject = (MazeLODObject)target;
            if (MazeLODBaker.TryGetStateWarning(lodObject, out var warning))
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Generate / Rebuild LODs"))
                {
                    MazeLODBaker.Rebuild(lodObject);
                }
            }
        }
    }
}
