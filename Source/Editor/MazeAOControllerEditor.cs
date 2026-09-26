#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Maze.Editor
{
    [CustomEditor(typeof(MazeAOController))]
    public sealed class MazeAOControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty profileProperty;
        private SerializedObject profileSerializedObject;
        private MazeAOProfile activeProfile;

        private void OnEnable()
        {
            profileProperty = serializedObject.FindProperty("profile");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(profileProperty);
            serializedObject.ApplyModifiedProperties();

            var controller = (MazeAOController)target;
            var profile = controller.Profile;
            if (profile == null)
            {
                EditorGUILayout.HelpBox("AO Profile is required.", MessageType.Warning);
                return;
            }

            EnsureProfileSerializedObject(profile);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("AO Controls", EditorStyles.boldLabel);
            DrawProfileFields(profileSerializedObject);
        }

        private void EnsureProfileSerializedObject(MazeAOProfile profile)
        {
            if (activeProfile == profile && profileSerializedObject != null)
            {
                return;
            }

            activeProfile = profile;
            profileSerializedObject = new SerializedObject(profile);
        }

        private static void DrawProfileFields(SerializedObject profileObject)
        {
            profileObject.Update();
            var iterator = profileObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script")
                {
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            profileObject.ApplyModifiedProperties();
        }
    }
}
#endif
