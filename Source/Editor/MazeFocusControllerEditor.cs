#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Maze.Editor
{
    [CustomEditor(typeof(MazeFocusController))]
    public sealed class MazeFocusControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty profileProperty;
        private SerializedObject profileSerializedObject;
        private MazeFocusProfile activeProfile;

        private void OnEnable()
        {
            profileProperty = serializedObject.FindProperty("profile");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(profileProperty);
            serializedObject.ApplyModifiedProperties();

            var controller = (MazeFocusController)target;
            var profile = controller.Profile;
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Focus Profile is required.", MessageType.Warning);
                return;
            }

            EnsureProfileSerializedObject(profile);
            profileSerializedObject.Update();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Focus", EditorStyles.boldLabel);
            Draw("focusEnabled", "Focus On");

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Depth Blur", EditorStyles.boldLabel);
            Draw("depthBlurEnabled", "Depth Blur On");
            Draw("quality", "Quality");

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Close Blur Band", EditorStyles.boldLabel);
            Draw("closeBlurRadius", "Blur Radius");
            Draw("focusStart", "Ends At");
            Draw("closeTransition", "Transition To Focus");

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Focus Band", EditorStyles.boldLabel);
            Draw("focusEnd", "Ends At");

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Far Blur Band", EditorStyles.boldLabel);
            Draw("farTransition", "Transition From Focus");
            Draw("farBlurRadius", "Blur Radius");

            var focusStart = profileSerializedObject.FindProperty("focusStart");
            var focusEnd = profileSerializedObject.FindProperty("focusEnd");
            if (focusStart != null && focusEnd != null && focusEnd.floatValue <= focusStart.floatValue)
            {
                EditorGUILayout.HelpBox("Focus Band End must be farther than its start.", MessageType.Error);
            }

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Band Edges", EditorStyles.boldLabel);
            Draw("silhouetteSpread", "Silhouette Spread");
            Draw("layerSeparation", "Layer Separation");

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Sky And Clouds", EditorStyles.boldLabel);
            Draw("skyBand", "Sky Band");
            Draw("cloudBand", "Cloud Band");

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Lens", EditorStyles.boldLabel);
            Draw("lensEnabled", "Lens On");
            Draw("lensCenter", "Center");

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Distortion", EditorStyles.miniBoldLabel);
            Draw("distortionEnabled", "Distortion On");
            Draw("distortionAmount", "Amount", "Negative bends outward; positive bends inward.");
            Draw("distortionX", "X Shape");
            Draw("distortionY", "Y Shape");
            Draw("distortionFalloff", "Edge Falloff");
            Draw("cropScale", "Crop Scale");

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Chromatic Aberration", EditorStyles.miniBoldLabel);
            Draw("chromaticEnabled", "Chromatic On");
            Draw("chromaticSpreadPixels", "Edge Spread");
            Draw("chromaticStart", "Starts At");
            Draw("chromaticFalloff", "Edge Falloff");

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("View", EditorStyles.boldLabel);
            Draw("renderInSceneView", "Show In Scene View");

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            Draw("debugMode", "Debug View");

            profileSerializedObject.ApplyModifiedProperties();
        }

        private void EnsureProfileSerializedObject(MazeFocusProfile profile)
        {
            if (activeProfile == profile && profileSerializedObject != null)
            {
                return;
            }

            activeProfile = profile;
            profileSerializedObject = new SerializedObject(profile);
        }

        private void Draw(string propertyName, string label, string tooltip = "")
        {
            var property = profileSerializedObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip));
            }
        }
    }
}
#endif
