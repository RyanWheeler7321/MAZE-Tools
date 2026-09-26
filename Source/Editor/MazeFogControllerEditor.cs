#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Maze.Editor
{
    [CustomEditor(typeof(MazeFogController))]
    public sealed class MazeFogControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty profileProperty;
        private SerializedObject profileSerializedObject;
        private MazeFogProfile activeProfile;

        private void OnEnable()
        {
            profileProperty = serializedObject.FindProperty("profile");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(profileProperty);
            serializedObject.ApplyModifiedProperties();

            var controller = (MazeFogController)target;
            var profile = controller.Profile;
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Fog Profile is required.", MessageType.Warning);
                return;
            }

            EnsureProfileSerializedObject(profile);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Fog Controls", EditorStyles.boldLabel);
            DrawProfileFields(profileSerializedObject);
        }

        private void EnsureProfileSerializedObject(MazeFogProfile profile)
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
            DrawCoreControls(profileObject);
            DrawBandControls(profileObject);
            DrawHorizonControls(profileObject);
            DrawShapeControls(profileObject);

            profileObject.ApplyModifiedProperties();
        }

        private static void DrawCoreControls(SerializedObject profileObject)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Global Fog", EditorStyles.boldLabel);
            DrawProperty(profileObject, "fogEnabled", "Fog On");
            DrawProperty(profileObject, "fogIntensity", "Fog Amount");
            DrawProperty(profileObject, "quality");
            DrawProperty(profileObject, "style");
            DrawProperty(profileObject, "maxDistance", "Max Distance");
            DrawProperty(profileObject, "maxOpacity", "Max Fog Amount");
            DrawProperty(profileObject, "wispiness", "Wispiness");
            DrawProperty(profileObject, "wispinessGranularity", "Wispiness Granularity");
            DrawProperty(profileObject, "closeFogAmount", "Close Fog");
            DrawProperty(profileObject, "veryCloseFogAmount", "Very Close Fog");
            DrawProperty(profileObject, "closeFogGranularity", "Close Granularity");
            DrawProperty(profileObject, "skyFogAmount", "Sky Fog Amount");
            DrawProperty(profileObject, "skyBlend", "Sky Mix");
        }

        private static void DrawBandControls(SerializedObject profileObject)
        {
            var bands = profileObject.FindProperty("bands");
            if (bands == null)
            {
                return;
            }

            EditorGUILayout.Space(8f);
            EnsureBoundaryBands(bands);
            EditorGUILayout.LabelField("Distance Bands", EditorStyles.boldLabel);
            DrawProperty(profileObject, "bandTransitionDistance", "Transition");
            using (new EditorGUI.DisabledScope(bands.arraySize >= MazeFogProfile.MaxFogBands))
            {
                if (GUILayout.Button("Add Middle Band"))
                {
                    AddMiddleBand(bands);
                }
            }

            for (var i = 0; i < bands.arraySize; i++)
            {
                var band = bands.GetArrayElementAtIndex(i);
                var isGlobal = i == 0;
                var isFinal = i == bands.arraySize - 1;
                var name = band.FindPropertyRelative("name");
                var enabled = band.FindPropertyRelative("enabled");
                var density = band.FindPropertyRelative("density");
                var startDistance = band.FindPropertyRelative("startDistance");

                EditorGUILayout.Space(5f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (!isGlobal && !isFinal)
                        {
                            using (new EditorGUILayout.VerticalScope(GUILayout.Width(28f)))
                            {
                                using (new EditorGUI.DisabledScope(i <= 1))
                                {
                                    if (GUILayout.Button("^", GUILayout.Width(28f)))
                                    {
                                        bands.MoveArrayElement(i, i - 1);
                                        return;
                                    }
                                }

                                using (new EditorGUI.DisabledScope(i >= bands.arraySize - 2))
                                {
                                    if (GUILayout.Button("v", GUILayout.Width(28f)))
                                    {
                                        bands.MoveArrayElement(i, i + 1);
                                        return;
                                    }
                                }
                            }

                            EditorGUILayout.PropertyField(enabled, GUIContent.none, GUILayout.Width(18f));
                            EditorGUILayout.PropertyField(name, GUIContent.none);
                            if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                            {
                                bands.DeleteArrayElementAtIndex(i);
                                return;
                            }
                        }
                        else
                        {
                            EditorGUILayout.LabelField(isGlobal ? "Global Band" : "Final Band", EditorStyles.boldLabel);
                        }
                    }

                    if (isGlobal)
                    {
                        startDistance.floatValue = 0f;
                        enabled.boolValue = true;
                        name.stringValue = "Global Band";
                        EditorGUILayout.LabelField("Starts At", "0 (fixed)");
                    }
                    else
                    {
                        if (isFinal)
                        {
                            enabled.boolValue = true;
                            name.stringValue = "Final Band";
                        }

                        EditorGUILayout.PropertyField(startDistance, new GUIContent("Starts At"));
                    }

                    EditorGUILayout.PropertyField(density, new GUIContent(isGlobal ? "Global Density" : "Added Density"));
                    DrawRelativeProperty(band, "color", "Color");
                }
            }
        }

        private static void EnsureBoundaryBands(SerializedProperty bands)
        {
            while (bands.arraySize < 2)
            {
                bands.InsertArrayElementAtIndex(bands.arraySize);
            }
        }

        private static void AddMiddleBand(SerializedProperty bands)
        {
            EnsureBoundaryBands(bands);
            var index = bands.arraySize - 1;
            var previous = bands.GetArrayElementAtIndex(index - 1);
            var final = bands.GetArrayElementAtIndex(index);
            var previousStart = Mathf.Max(0f, previous.FindPropertyRelative("startDistance").floatValue);
            var finalStart = Mathf.Max(previousStart + 1f, final.FindPropertyRelative("startDistance").floatValue);
            bands.InsertArrayElementAtIndex(index);
            var band = bands.GetArrayElementAtIndex(index);
            SetRelativeString(band, "name", $"Band {index}");
            SetRelativeBool(band, "enabled", true);
            SetRelativeFloat(band, "density", 0.1f);
            SetRelativeFloat(band, "startDistance", Mathf.Lerp(previousStart, finalStart, 0.5f));
        }

        private static void DrawShapeControls(SerializedObject profileObject)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Ground / Sky Bias", EditorStyles.boldLabel);
            DrawProperty(profileObject, "groundSkyHeight", "Split Height");
            DrawProperty(profileObject, "groundSkyTransition", "Smooth Transition");
            DrawProperty(profileObject, "groundThickness", "Ground Thickness");
            DrawProperty(profileObject, "skyThickness", "Sky Thickness");

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Volume Height Shape", EditorStyles.boldLabel);
            DrawProperty(profileObject, "baseHeight");
            DrawProperty(profileObject, "heightThickness");
            DrawProperty(profileObject, "heightFalloff");
            DrawProperty(profileObject, "groundBias");

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Nearby Texture", EditorStyles.boldLabel);
            DrawProperty(profileObject, "nearNoiseSize", "Pattern Size");
            DrawProperty(profileObject, "nearNoiseStrength", "Amount");
            DrawProperty(profileObject, "nearNoiseContrast", "Contrast");
            DrawProperty(profileObject, "noiseScrollSpeed");

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Distant Texture", EditorStyles.boldLabel);
            DrawProperty(profileObject, "farNoiseSize", "Pattern Size");
            DrawProperty(profileObject, "farNoiseStrength", "Amount");
            DrawProperty(profileObject, "farNoiseContrast", "Contrast");
            DrawProperty(profileObject, "farNoiseStartDistance", "Starts At");
            DrawProperty(profileObject, "farNoiseFullDistance", "Full At");

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Lighting / Quality", EditorStyles.boldLabel);
            DrawProperty(profileObject, "anisotropy");
            DrawProperty(profileObject, "lightResponse");
            DrawProperty(profileObject, "ambientResponse");
            DrawProperty(profileObject, "cloudShadowStrength");
            DrawProperty(profileObject, "stepCount");
            DrawProperty(profileObject, "renderInSceneView");
        }

        private static void DrawHorizonControls(SerializedObject profileObject)
        {
            var enabled = profileObject.FindProperty("horizonHazeEnabled");
            if (enabled == null)
            {
                return;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Optional Horizon Haze", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(enabled, new GUIContent("Horizon Haze"));
            if (!enabled.boolValue)
            {
                return;
            }

            DrawProperty(profileObject, "horizonHazeStrength", "Strength");
            DrawProperty(profileObject, "horizonHazeStartDistance", "Starts At");
            DrawProperty(profileObject, "horizonHazeFullDistance", "Full At");
            DrawProperty(profileObject, "horizonHazeVerticalSize", "Vertical Size");
        }

        private static void DrawProperty(SerializedObject serializedObject, string propertyPath, string label = null)
        {
            var property = serializedObject.FindProperty(propertyPath);
            if (property != null)
            {
                if (label != null)
                {
                    EditorGUILayout.PropertyField(property, new GUIContent(label), true);
                }
                else
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }
        }

        private static void DrawRelativeProperty(SerializedProperty property, string relativePath, string label = null)
        {
            var relative = property.FindPropertyRelative(relativePath);
            if (relative != null)
            {
                if (label != null)
                {
                    EditorGUILayout.PropertyField(relative, new GUIContent(label), true);
                    return;
                }

                EditorGUILayout.PropertyField(relative, true);
            }
        }

        private static void SetRelativeFloat(SerializedProperty property, string relativePath, float value)
        {
            var relative = property.FindPropertyRelative(relativePath);
            if (relative != null)
            {
                relative.floatValue = value;
            }
        }

        private static void SetRelativeString(SerializedProperty property, string relativePath, string value)
        {
            var relative = property.FindPropertyRelative(relativePath);
            if (relative != null)
            {
                relative.stringValue = value;
            }
        }

        private static void SetRelativeBool(SerializedProperty property, string relativePath, bool value)
        {
            var relative = property.FindPropertyRelative(relativePath);
            if (relative != null)
            {
                relative.boolValue = value;
            }
        }
    }
}
#endif
