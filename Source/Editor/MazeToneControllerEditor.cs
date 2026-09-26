#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Maze.Editor
{
    [CustomEditor(typeof(MazeToneController))]
    public sealed class MazeToneControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty profileProperty;
        private SerializedObject profileSerializedObject;
        private MazeToneProfile activeProfile;

        private void OnEnable()
        {
            profileProperty = serializedObject.FindProperty("profile");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(profileProperty);
            serializedObject.ApplyModifiedProperties();

            var controller = (MazeToneController)target;
            var profile = controller.Profile;
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Tone Profile is required.", MessageType.Warning);
                return;
            }

            EnsureProfileSerializedObject(profile);
            EditorGUILayout.Space(8f);
            DrawProfileFields(profileSerializedObject, profile);
        }

        private void EnsureProfileSerializedObject(MazeToneProfile profile)
        {
            if (activeProfile == profile && profileSerializedObject != null)
            {
                return;
            }

            activeProfile = profile;
            profileSerializedObject = new SerializedObject(profile);
        }

        private static void DrawProfileFields(SerializedObject profileObject, MazeToneProfile profile)
        {
            profileObject.Update();
            EditorGUILayout.LabelField("Tone", EditorStyles.boldLabel);
            Draw(profileObject, "toneEnabled", "Tone On");
            var modeProperty = Draw(profileObject, "mode", "Mode");
            var qualityProperty = Draw(profileObject, "quality", "Quality");
            Draw(profileObject, "renderInSceneView", "Show In Scene View");
            Draw(profileObject, "exposureEV", "Exposure EV");
            Draw(profileObject, "strength", "Strength");
            if (qualityProperty != null && (MazeToneQualityTier)qualityProperty.enumValueIndex == MazeToneQualityTier.Dithered)
            {
                Draw(profileObject, "ditherStrength", "Dither Strength");
            }

            var mode = modeProperty != null ? (MazeToneMode)modeProperty.enumValueIndex : profile.mode;
            switch (mode)
            {
                case MazeToneMode.Neutral:
                    DrawNeutral(profileObject);
                    break;
                case MazeToneMode.ACES:
                    DrawAces(profileObject);
                    break;
                case MazeToneMode.ReinhardExtended:
                    DrawReinhard(profileObject);
                    break;
                case MazeToneMode.GT:
                    DrawGt(profileObject);
                    break;
            }

            profileObject.ApplyModifiedProperties();
        }

        private static void DrawNeutral(SerializedObject profileObject)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Neutral", EditorStyles.boldLabel);
            Draw(profileObject, "neutralContrast", "Contrast");
            Draw(profileObject, "neutralWhitePoint", "White Point");
            Draw(profileObject, "neutralWhiteClip", "White Clip");
        }

        private static void DrawAces(SerializedObject profileObject)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("ACES", EditorStyles.boldLabel);
            Draw(profileObject, "acesContrast", "Contrast");
            Draw(profileObject, "acesSaturation", "Saturation");
        }

        private static void DrawReinhard(SerializedObject profileObject)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Reinhard Extended", EditorStyles.boldLabel);
            Draw(profileObject, "reinhardWhitePoint", "White Point");
        }

        private static void DrawGt(SerializedObject profileObject)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("GT", EditorStyles.boldLabel);
            Draw(profileObject, "gtMaxBrightness", "Max Brightness");
            Draw(profileObject, "gtContrast", "Contrast");
            Draw(profileObject, "gtLinearStart", "Linear Start");
            Draw(profileObject, "gtLinearLength", "Linear Length");
            Draw(profileObject, "gtBlackTightness", "Black Tightness");
        }

        private static SerializedProperty Draw(SerializedObject profileObject, string propertyName, string label)
        {
            var property = profileObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label));
            }

            return property;
        }
    }
}
#endif
