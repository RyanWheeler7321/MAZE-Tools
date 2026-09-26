using UnityEditor;
using UnityEngine;

namespace Maze.Editor
{
    internal static class MazeLODInspectorUtility
    {
        public static void DrawSettings(SerializedProperty settings)
        {
            var levelCount = settings.FindPropertyRelative("levelCount");
            EditorGUILayout.PropertyField(levelCount, new GUIContent("Level Count"));
            if (levelCount.intValue >= 2)
            {
                EditorGUILayout.PropertyField(
                    settings.FindPropertyRelative("standardTriangleTarget"),
                    new GUIContent("Standard Target"));
            }

            if (levelCount.intValue >= 3)
            {
                EditorGUILayout.PropertyField(
                    settings.FindPropertyRelative("mediumTriangleTarget"),
                    new GUIContent("Medium Target"));
            }

            if (levelCount.intValue >= 4)
            {
                EditorGUILayout.PropertyField(
                    settings.FindPropertyRelative("farawayTriangleTarget"),
                    new GUIContent("Faraway Target"));
            }

            EditorGUILayout.PropertyField(
                settings.FindPropertyRelative("firstTransitionHeight"),
                new GUIContent("First Transition"));
            EditorGUILayout.PropertyField(
                settings.FindPropertyRelative("transitionScale"),
                new GUIContent("Transition Scale"));

            var crossFade = settings.FindPropertyRelative("crossFade");
            EditorGUILayout.PropertyField(crossFade, new GUIContent("Crossfade"));
            if (crossFade.boolValue)
            {
                EditorGUILayout.PropertyField(
                    settings.FindPropertyRelative("fadeTransitionWidth"),
                    new GUIContent("Fade Width"));
            }

            var cull = settings.FindPropertyRelative("cull");
            EditorGUILayout.PropertyField(cull, new GUIContent("Cull"));
            if (cull.boolValue)
            {
                EditorGUILayout.PropertyField(
                    settings.FindPropertyRelative("cullScreenHeight"),
                    new GUIContent("Cull Screen Height"));
            }
        }
    }
}
