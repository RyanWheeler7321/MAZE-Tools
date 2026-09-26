#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Maze.Editor
{
    [CustomEditor(typeof(MazeBloomController))]
    public sealed class MazeBloomControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty profileProperty;
        private SerializedObject profileSerializedObject;
        private MazeBloomProfile activeProfile;

        private void OnEnable()
        {
            profileProperty = serializedObject.FindProperty("profile");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(profileProperty);
            serializedObject.ApplyModifiedProperties();

            var controller = (MazeBloomController)target;
            var profile = controller.Profile;
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Bloom Profile is required.", MessageType.Warning);
                return;
            }

            EnsureProfileSerializedObject(profile);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Bloom", EditorStyles.boldLabel);
            DrawProfileFields(profileSerializedObject, profile);
        }

        private void EnsureProfileSerializedObject(MazeBloomProfile profile)
        {
            if (activeProfile == profile && profileSerializedObject != null)
            {
                return;
            }

            activeProfile = profile;
            profileSerializedObject = new SerializedObject(profile);
        }

        private static void DrawProfileFields(SerializedObject profileObject, MazeBloomProfile profile)
        {
            profileObject.Update();
            Draw(profileObject, "bloomEnabled", "Bloom On");
            Draw(profileObject, "quality", "Quality");
            Draw(profileObject, "threshold", "Threshold");
            Draw(profileObject, "softKnee", "Soft Knee");
            Draw(profileObject, "intensity", "Strength");
            Draw(profileObject, "scatter", "Spread");
            Draw(profileObject, "tint", "Tint");
            Draw(profileObject, "clamp", "Clamp");
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Bright Streaks", EditorStyles.boldLabel);
            Draw(profileObject, "streaksEnabled", "Streaks On");
            Draw(profileObject, "streakIntensity", "Strength");
            Draw(profileObject, "streakStretch", "Stretch");
            Draw(profileObject, "streakAngle", "Angle");
            Draw(profileObject, "streakChromatic", "Chromatic");
            Draw(profileObject, "renderInSceneView", "Show In Scene View");
            profileObject.ApplyModifiedProperties();
        }

        private static void Draw(SerializedObject profileObject, string propertyName, string label)
        {
            var property = profileObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label));
            }
        }
    }
}
#endif
