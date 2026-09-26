#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Maze.Editor
{
    [CustomEditor(typeof(MazeLightFXController))]
    public sealed class MazeLightFXControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty profileProperty;
        private SerializedProperty lightShaftSourceProperty;
        private SerializedObject profileSerializedObject;
        private MazeLightFXProfile activeProfile;

        private void OnEnable()
        {
            profileProperty = serializedObject.FindProperty("profile");
            lightShaftSourceProperty = serializedObject.FindProperty("lightShaftSource");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(profileProperty);
            EditorGUILayout.PropertyField(lightShaftSourceProperty, new GUIContent("Shaft Light Source"));
            serializedObject.ApplyModifiedProperties();

            var controller = (MazeLightFXController)target;
            var profile = controller.Profile;
            if (profile == null)
            {
                EditorGUILayout.HelpBox("LightFX Profile is required.", MessageType.Warning);
                return;
            }

            EnsureProfileSerializedObject(profile);
            DrawProfileFields(profileSerializedObject, controller);
        }

        private void EnsureProfileSerializedObject(MazeLightFXProfile profile)
        {
            if (activeProfile == profile && profileSerializedObject != null)
            {
                return;
            }

            activeProfile = profile;
            profileSerializedObject = new SerializedObject(profile);
        }

        private static void DrawProfileFields(SerializedObject profileObject, MazeLightFXController controller)
        {
            profileObject.Update();
            var profile = profileObject.targetObject as MazeLightFXProfile;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("LightFX", EditorStyles.boldLabel);
            Draw(profileObject, "lightFXEnabled", "LightFX On");
            Draw(profileObject, "quality");
            Draw(profileObject, "renderInSceneView", "Show In Scene View");

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Light Shafts", EditorStyles.boldLabel);
            Draw(profileObject, "lightShaftsEnabled", "Shafts On");
            Draw(profileObject, "lightShaftSourceMode", "Source");
            if (profile != null && profile.lightShaftSourceMode == MazeLightFXSourceMode.ManualScreenPosition)
            {
                Draw(profileObject, "manualLightShaftViewportPosition", "Screen Position");
            }
            else if (profile != null && profile.lightShaftSourceMode == MazeLightFXSourceMode.SourceTransform && controller.LightShaftSource == null)
            {
                EditorGUILayout.HelpBox("Shaft Light Source is required for Explicit Light.", MessageType.Warning);
            }

            Draw(profileObject, "lightShaftColor", "Color");
            Draw(profileObject, "lightShaftIntensity", "Strength");
            Draw(profileObject, "lightShaftiness", "Ray Shape");
            Draw(profileObject, "lightShaftGranularity", "Ray Breakup");
            Draw(profileObject, "lightShaftNoiseScale", "Breakup Scale");
            Draw(profileObject, "lightShaftDither", "Dither Rays");
            Draw(profileObject, "lightShaftBlurRadius", "Spread");
            Draw(profileObject, "lightShaftSharpness", "Edge Focus");

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Advanced", EditorStyles.boldLabel);
            Draw(profileObject, "lightShaftDecay", "Fade Along Ray");
            Draw(profileObject, "lightShaftWeight", "Ray Step Weight");
            Draw(profileObject, "lightShaftExposure", "Brightness Push");
            Draw(profileObject, "lightShaftSamples", "Samples");
            Draw(profileObject, "lightShaftOcclusion", "Blocked By Scene");
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Source Flare", EditorStyles.boldLabel);
            Draw(profileObject, "sourceFlareEnabled", "Flare On");
            Draw(profileObject, "sourceFlareColor", "Color");
            Draw(profileObject, "sourceFlareIntensity", "Strength");
            Draw(profileObject, "haloRadius", "Halo Radius");
            Draw(profileObject, "haloIntensity", "Halo Strength");
            Draw(profileObject, "starIntensity", "Star Strength");
            Draw(profileObject, "starSpikeCount", "Star Spikes");
            Draw(profileObject, "starLength", "Star Length");
            Draw(profileObject, "starRotation", "Star Rotation");
            Draw(profileObject, "flareOcclusion", "Blocked By Scene");
            Draw(profileObject, "ghostIntensity", "Ghost Strength");
            Draw(profileObject, "ghostCount", "Ghost Count");
            Draw(profileObject, "ghostSpacing", "Ghost Spacing");
            Draw(profileObject, "chromaticOffsetPixels", "Chromatic Pixels");

            profileObject.ApplyModifiedProperties();
        }

        private static void Draw(SerializedObject serializedObject, string propertyPath, string label = null)
        {
            var property = serializedObject.FindProperty(propertyPath);
            if (property == null)
            {
                return;
            }

            if (label != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
                return;
            }

            EditorGUILayout.PropertyField(property, true);
        }
    }
}
#endif
