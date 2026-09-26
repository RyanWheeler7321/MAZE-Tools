#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Maze.Editor
{
    internal static class MazeFilterProfileEditorGui
    {
        private static readonly Dictionary<string, bool> LayerExpanded = new();

        public static void DrawProfile(
            SerializedObject profileObject,
            MazeFilterProfile profile,
            SerializedProperty targetsProperty = null,
            SerializedProperty objectSilhouetteRootsProperty = null,
            SerializedProperty objectSilhouetteProxyRenderersProperty = null)
        {
            profileObject.Update();
            EditorGUILayout.LabelField("Key Parameters", EditorStyles.boldLabel);
            Draw(profileObject, "sampling", "Sampling", "Reduces neighborhood and echo samples. 1 preserves the authored effect settings exactly.");
            var targetModeProperty = profileObject.FindProperty("targetMode");
            if (targetModeProperty != null && (MazeFilterTargetMode)targetModeProperty.enumValueIndex == MazeFilterTargetMode.ObjectSilhouette)
            {
                Draw(profileObject, "maskFidelity", "Mask Fidelity", "Reduces object-mask resolution. 1 preserves the authored mask resolution exactly.");
            }
            EditorGUILayout.Space(8f);
            Draw(profileObject, "filterEnabled", "Filter On");
            Draw(profileObject, "applyOrder", "Apply Order");
            Draw(profileObject, "stackOrder", "Stack Order");
            Draw(profileObject, "renderInSceneView", "Show In Scene View");
            EditorGUILayout.Space(8f);
            DrawTarget(profileObject, targetsProperty, objectSilhouetteRootsProperty, objectSilhouetteProxyRenderersProperty);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Effects", EditorStyles.boldLabel);
            DrawEffects(profileObject, profile);
            profileObject.ApplyModifiedProperties();
        }

        private static void DrawTarget(
            SerializedObject profileObject,
            SerializedProperty targetsProperty,
            SerializedProperty objectSilhouetteRootsProperty,
            SerializedProperty objectSilhouetteProxyRenderersProperty)
        {
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            var modeProperty = profileObject.FindProperty("targetMode");
            if (modeProperty == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(modeProperty, new GUIContent("Target Mode"));
            var mode = (MazeFilterTargetMode)modeProperty.enumValueIndex;
            switch (mode)
            {
                case MazeFilterTargetMode.Targets:
                    if (targetsProperty != null)
                    {
                        EditorGUILayout.PropertyField(targetsProperty, new GUIContent("Targets"), true);
                    }
                    break;
                case MazeFilterTargetMode.ScreenEdge:
                    Draw(profileObject, "screenEdgeWidthPixels", "Edge Width");
                    Draw(profileObject, "screenEdgeSoftnessPixels", "Edge Softness");
                    break;
                case MazeFilterTargetMode.DepthBand:
                    Draw(profileObject, "depthStart", "Depth Start");
                    Draw(profileObject, "depthEnd", "Depth End");
                    Draw(profileObject, "depthSoftness", "Depth Softness");
                    Draw(profileObject, "skyAmount", "Sky Amount");
                    break;
                case MazeFilterTargetMode.ScreenRegion:
                    Draw(profileObject, "regionShape", "Shape");
                    Draw(profileObject, "regionCenter", "Center");
                    Draw(profileObject, "regionSize", "Size");
                    Draw(profileObject, "regionRotationDegrees", "Rotation");
                    Draw(profileObject, "regionSoftness", "Softness");
                    break;
                case MazeFilterTargetMode.ObjectSilhouette:
                    if (objectSilhouetteRootsProperty != null)
                    {
                        EditorGUILayout.PropertyField(objectSilhouetteRootsProperty, new GUIContent("Object Roots"), true);
                    }
                    var areaProperty = profileObject.FindProperty("objectSilhouetteMode");
                    Draw(profileObject, "objectSilhouetteMode", "Mask Area");
                    var occlusionProperty = profileObject.FindProperty("objectOcclusion");
                    Draw(profileObject, "objectOcclusion", "Occlusion");
                    if (occlusionProperty != null && (MazeFilterObjectOcclusion)occlusionProperty.enumValueIndex == MazeFilterObjectOcclusion.VisibleOnly)
                    {
                        Draw(profileObject, "objectDepthBias", "Depth Bias");
                    }

                    var sourceProperty = profileObject.FindProperty("objectMaskSource");
                    Draw(profileObject, "objectMaskSource", "Mask Source");
                    if (sourceProperty != null
                        && (MazeFilterObjectMaskSource)sourceProperty.enumValueIndex == MazeFilterObjectMaskSource.ProxyRenderers
                        && objectSilhouetteProxyRenderersProperty != null)
                    {
                        EditorGUILayout.PropertyField(objectSilhouetteProxyRenderersProperty, new GUIContent("Proxy Renderers"), true);
                    }

                    Draw(profileObject, "objectMaskResolutionScale", "Mask Resolution");
                    var area = areaProperty != null ? (MazeFilterObjectSilhouetteMode)areaProperty.enumValueIndex : MazeFilterObjectSilhouetteMode.Filled;
                    if (area != MazeFilterObjectSilhouetteMode.Filled)
                    {
                        Draw(profileObject, "objectExtensionPixels", "Outside Size");
                        Draw(profileObject, "objectSoftnessPixels", "Outside Smooth");
                        Draw(profileObject, "objectEdgeCurve", "Edge Curve");
                    }
                    break;
            }

            Draw(profileObject, "maskStrength", "Mask Strength");
            Draw(profileObject, "maskContrast", "Mask Contrast");
            Draw(profileObject, "maskBias", "Mask Bias");
            Draw(profileObject, "invertMask", "Invert Mask");
        }

        private static void DrawEffects(SerializedObject profileObject, MazeFilterProfile profile)
        {
            var layers = profileObject.FindProperty("layers");
            if (layers == null)
            {
                return;
            }

            var count = Mathf.Min(layers.arraySize, MazeFilterProfile.MaxLayers);
            for (var i = 0; i < count; i++)
            {
                DrawEffect(profileObject, layers.GetArrayElementAtIndex(i), i);
            }
        }

        private static void DrawEffect(SerializedObject profileObject, SerializedProperty layer, int index)
        {
            var name = layer.FindPropertyRelative("name");
            var enabled = layer.FindPropertyRelative("enabled");
            var effect = layer.FindPropertyRelative("effect");
            var title = !string.IsNullOrWhiteSpace(name?.stringValue) ? name.stringValue : ObjectNames.NicifyVariableName(((MazeFilterEffectType)effect.intValue).ToString());
            var key = profileObject.targetObject.GetEntityId() + ":" + index;
            var expanded = LayerExpanded.TryGetValue(key, out var value) && value;

            EditorGUILayout.Space(3f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var newEnabled = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(20f));
                    if (newEnabled != enabled.boolValue)
                    {
                        enabled.boolValue = newEnabled;
                    }

                    var rowText = enabled.boolValue ? $"{title}  ON" : title;
                    var rowStyle = enabled.boolValue ? EditorStyles.boldLabel : EditorStyles.label;
                    if (GUILayout.Button(rowText, rowStyle, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight)))
                    {
                        expanded = !expanded;
                    }
                }

                LayerExpanded[key] = expanded;
                if (!expanded)
                {
                    return;
                }

                EditorGUILayout.Space(4f);
                using (new EditorGUI.IndentLevelScope())
                {
                    Draw(layer, "intensity", "Amount");
                    EditorGUILayout.Space(4f);
                    DrawEffectSpecific(layer, (MazeFilterEffectType)effect.intValue);
                }
            }
        }

        private static void DrawEffectSpecific(SerializedProperty layer, MazeFilterEffectType effect)
        {
            switch (effect)
            {
                case MazeFilterEffectType.Pixelize:
                    Draw(layer, "cellSizePixels", "Cell Size");
                    Draw(layer, "pixelShape", "Shape");
                    Draw(layer, "jitter", "Variation");
                    Draw(layer, "sizeVariation", "Tile Size Amount");
                    Draw(layer, "tileSizeMinMultiplier", "Min Tile Size");
                    Draw(layer, "tileSizeMaxMultiplier", "Max Tile Size");
                    Draw(layer, "shapeAspect", "Shape Aspect");
                    Draw(layer, "gridEdgeWidth", "Shape Softness");
                    Draw(layer, "gridPushPixels", "Distortion");
                    Draw(layer, "motionRange", "Motion Range");
                    Draw(layer, "timeSpeed", "Motion Speed");
                    Draw(layer, "colorShift", "Color Drift");
                    Draw(layer, "rowShear", "Tile Shear");
                    Draw(layer, "rotationDegrees", "Rotation Angle");
                    Draw(layer, "seed", "Seed");
                    break;
                case MazeFilterEffectType.Blur:
                    Draw(layer, "blurRadiusPixels", "Blur Spread");
                    Draw(layer, "blurSamples", "Blur Samples");
                    break;
                case MazeFilterEffectType.GridWarp:
                    Draw(layer, "cellSizePixels", "Grid Size");
                    Draw(layer, "gridEdgeWidth", "Edge Width");
                    Draw(layer, "gridPushPixels", "Pixel Push");
                    Draw(layer, "jitter", "Random Direction");
                    Draw(layer, "seed", "Seed");
                    Draw(layer, "timeSpeed", "Motion Speed");
                    break;
                case MazeFilterEffectType.PaletteCrush:
                    Draw(layer, "colorSteps", "Color Steps");
                    Draw(layer, "contrast", "Contrast");
                    break;
                case MazeFilterEffectType.OrderedDither:
                    Draw(layer, "colorSteps", "Color Steps");
                    Draw(layer, "softness", "Dither Strength");
                    break;
                case MazeFilterEffectType.InkClamp:
                    Draw(layer, "contrast", "Ink Weight");
                    Draw(layer, "softness", "Ink Softness");
                    break;
                case MazeFilterEffectType.PosterBands:
                    Draw(layer, "colorSteps", "Band Count");
                    break;
                case MazeFilterEffectType.ChromaticSlip:
                    Draw(layer, "slipPalette", "Slip Colors");
                    Draw(layer, "gridPushPixels", "Offset");
                    Draw(layer, "angleDegrees", "Direction");
                    Draw(layer, "contrast", "Color Strength");
                    Draw(layer, "softness", "Source Hold");
                    break;
                case MazeFilterEffectType.EdgeBurn:
                    Draw(layer, "blurRadiusPixels", "Edge Reach");
                    Draw(layer, "contrast", "Edge Strength");
                    Draw(layer, "softness", "Edge Threshold");
                    break;
                case MazeFilterEffectType.HalftoneShade:
                    Draw(layer, "cellSizePixels", "Dot Size");
                    Draw(layer, "halftoneShape", "Shape");
                    Draw(layer, "toneIsolation", "Areas");
                    Draw(layer, "halftoneAreaThreshold", "Area Threshold");
                    Draw(layer, "halftoneAreaSoftness", "Area Transition");
                    Draw(layer, "rowShear", "Row Shear");
                    Draw(layer, "rotationDegrees", "Rotation Angle");
                    Draw(layer, "shapeAspect", "Shape Aspect");
                    Draw(layer, "contrast", "Print Strength");
                    Draw(layer, "softness", "Dot Edge Softness");
                    Draw(layer, "colorMode", "Color Mode");
                    Draw(layer, "primaryColor", "Primary Color");
                    Draw(layer, "secondaryColor", "Secondary Color");
                    Draw(layer, "gradientAngleDegrees", "Gradient Angle");
                    Draw(layer, "gradientOffset", "Gradient Offset");
                    break;
                case MazeFilterEffectType.SignalTear:
                    Draw(layer, "cellSizePixels", "Band Height");
                    Draw(layer, "gridPushPixels", "Tear Offset");
                    Draw(layer, "jitter", "Tear Chance");
                    Draw(layer, "seed", "Seed");
                    Draw(layer, "timeSpeed", "Motion Speed");
                    break;
                case MazeFilterEffectType.HeatRipple:
                    Draw(layer, "cellSizePixels", "Wave Size");
                    Draw(layer, "gridPushPixels", "Ripple Push");
                    Draw(layer, "seed", "Seed");
                    Draw(layer, "timeSpeed", "Motion Speed");
                    break;
                case MazeFilterEffectType.GhostEcho:
                    Draw(layer, "blurRadiusPixels", "Echo Spacing");
                    Draw(layer, "blurSamples", "Echo Count");
                    Draw(layer, "angleDegrees", "Direction");
                    break;
                case MazeFilterEffectType.PaperGrainWash:
                    Draw(layer, "jitter", "Grain Strength");
                    Draw(layer, "contrast", "Wash Contrast");
                    Draw(layer, "colorShift", "Color Hold");
                    Draw(layer, "seed", "Seed");
                    Draw(layer, "timeSpeed", "Motion Speed");
                    break;
                case MazeFilterEffectType.Kuwahara:
                    Draw(layer, "blurRadiusPixels", "Brush Size");
                    break;
                case MazeFilterEffectType.BlurSlope:
                    Draw(layer, "blurRadiusPixels", "Slope Spread");
                    Draw(layer, "blurSamples", "Slope Samples");
                    Draw(layer, "angleDegrees", "Fallback Direction");
                    break;
                case MazeFilterEffectType.SobelSketch:
                    Draw(layer, "blurRadiusPixels", "Line Reach");
                    Draw(layer, "contrast", "Line Strength");
                    Draw(layer, "softness", "Line Threshold");
                    break;
                case MazeFilterEffectType.WatercolorBleed:
                    Draw(layer, "blurRadiusPixels", "Bleed Spread");
                    Draw(layer, "blurSamples", "Bleed Samples");
                    Draw(layer, "colorSteps", "Color Pools");
                    Draw(layer, "cellSizePixels", "Paper Scale");
                    Draw(layer, "jitter", "Paper Texture");
                    Draw(layer, "colorShift", "Color Drift");
                    Draw(layer, "seed", "Seed");
                    break;
                case MazeFilterEffectType.ClarityHighPass:
                    Draw(layer, "blendMode", "Blend Mode");
                    Draw(layer, "blurRadiusPixels", "Blur Radius");
                    Draw(layer, "contrast", "Detail Boost");
                    Draw(layer, "highPassThreshold", "Visible Detail Min");
                    Draw(layer, "highPassSoftness", "Min Smoothness");
                    Draw(layer, "highPassLumaOnly", "Luma Only");
                    break;
                case MazeFilterEffectType.ColorFill:
                    Draw(layer, "blendMode", "Blend Mode");
                    Draw(layer, "gradientMode", "Gradient");
                    Draw(layer, "primaryColor", "Color A");
                    Draw(layer, "secondaryColor", "Color B");
                    Draw(layer, "gradientAngleDegrees", "Gradient Angle");
                    Draw(layer, "gradientOffset", "Gradient Offset");
                    break;
            }
        }

        private static void Draw(SerializedObject profileObject, string propertyName, string label, bool includeChildren = false)
        {
            var property = profileObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren);
            }
        }

        private static void Draw(SerializedObject profileObject, string propertyName, string label, string tooltip)
        {
            var property = profileObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip));
            }
        }

        private static void Draw(SerializedProperty parent, string propertyName, string label)
        {
            var property = parent.FindPropertyRelative(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
            }
        }
    }

    [CustomEditor(typeof(MazeFilterController))]
    public sealed class MazeFilterControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty profileProperty;
        private SerializedProperty targetsProperty;
        private SerializedProperty objectSilhouetteRootsProperty;
        private SerializedProperty objectSilhouetteProxyRenderersProperty;
        private SerializedObject profileSerializedObject;
        private MazeFilterProfile activeProfile;

        private void OnEnable()
        {
            profileProperty = serializedObject.FindProperty("profile");
            targetsProperty = serializedObject.FindProperty("targets");
            objectSilhouetteRootsProperty = serializedObject.FindProperty("objectSilhouetteRoots");
            objectSilhouetteProxyRenderersProperty = serializedObject.FindProperty("objectSilhouetteProxyRenderers");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(profileProperty);
            DrawProfileButtons();
            serializedObject.ApplyModifiedProperties();

            var controller = (MazeFilterController)target;
            var profile = controller.Profile;
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Filter Profile is required.", MessageType.Warning);
                return;
            }

            EnsureProfileSerializedObject(profile);
            EditorGUILayout.Space(8f);
            MazeFilterProfileEditorGui.DrawProfile(profileSerializedObject, profile, targetsProperty, objectSilhouetteRootsProperty, objectSilhouetteProxyRenderersProperty);
            serializedObject.ApplyModifiedProperties();
        }


        private void DrawProfileButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New", GUILayout.MinHeight(22f)))
                {
                    CreateAndAssignProfile(false);
                }

                using (new EditorGUI.DisabledScope(profileProperty.objectReferenceValue == null))
                {
                    if (GUILayout.Button("Clone", GUILayout.MinHeight(22f)))
                    {
                        CreateAndAssignProfile(true);
                    }
                }
            }
        }

        private void CreateAndAssignProfile(bool cloneCurrent)
        {
            var controller = (MazeFilterController)target;
            var current = profileProperty.objectReferenceValue as MazeFilterProfile;
            var profile = cloneCurrent && current != null
                ? Instantiate(current)
                : CreateInstance<MazeFilterProfile>();
            profile.EnsureEffectRows();

            var folder = ResolveProjectSetupFolder(controller, current);
            EnsureAssetFolder(folder);
            var assetName = cloneCurrent && current != null ? $"{current.name}_Clone.asset" : "Maze_Filter.asset";
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}");
            profile.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(profile, path);
            Undo.RegisterCreatedObjectUndo(profile, cloneCurrent ? "Clone Filter Profile" : "Create Filter Profile");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.RecordObject(controller, "Assign Filter Profile");
            serializedObject.Update();
            profileProperty.objectReferenceValue = profile;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
            EditorGUIUtility.PingObject(profile);
            EnsureProfileSerializedObject(profile);
        }

        private static string ResolveProjectSetupFolder(MazeFilterController controller, MazeFilterProfile current)
        {
            var scenePath = controller != null ? controller.gameObject.scene.path : string.Empty;
            if (TryGetProjectRoot(scenePath, out var sceneRoot))
            {
                return sceneRoot + "/Setup";
            }

            var currentPath = current != null ? AssetDatabase.GetAssetPath(current) : string.Empty;
            if (TryGetProjectRoot(currentPath, out var assetRoot))
            {
                return assetRoot + "/Setup";
            }

            return "Assets/MazeTools/Profiles";
        }

        private static bool TryGetProjectRoot(string assetPath, out string projectRoot)
        {
            projectRoot = string.Empty;
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", System.StringComparison.Ordinal))
            {
                return false;
            }

            var slash = assetPath.IndexOf('/', "Assets/".Length);
            var folder = slash >= 0 ? assetPath[..slash] : assetPath;
            projectRoot = folder;
            return true;
        }

        private static void EnsureAssetFolder(string folder)
        {
            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private void EnsureProfileSerializedObject(MazeFilterProfile profile)
        {
            if (activeProfile == profile && profileSerializedObject != null)
            {
                return;
            }

            activeProfile = profile;
            profileSerializedObject = new SerializedObject(profile);
        }
    }

    [CustomEditor(typeof(MazeFilterProfile))]
    public sealed class MazeFilterProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            MazeFilterProfileEditorGui.DrawProfile(serializedObject, (MazeFilterProfile)target);
        }
    }
}
#endif
