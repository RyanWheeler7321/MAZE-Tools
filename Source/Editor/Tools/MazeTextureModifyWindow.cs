#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Maze.Editor
{
    internal enum MazeTextureBackgroundMode
    {
        Checker,
        Black,
        Gray,
        White
    }

    internal sealed class MazeTextureModifyWindow : EditorWindow
    {
        private const string MenuPath = "Assets/MODIFY";
        private const string StyleFileName = "MazeTextureModify.uss";
        private const float ControlsWidth = 330f;
        private const double PreviewFrameInterval = 1.0 / 60.0;

        [SerializeField] private string assetGuid;
        [SerializeField] private string assetPath;
        [SerializeField] private string loadedSourceHash;
        [SerializeField] private MazeTextureModifySettings settings = new();
        [SerializeField] private MazeTextureCompareMode compareMode;
        [SerializeField] private MazeTextureChannelMode channelMode;
        [SerializeField] private MazeTextureViewMode viewMode;
        [SerializeField] private MazeTextureBackgroundMode backgroundMode;
        [SerializeField] private float wipe = 0.5f;
        [SerializeField] private float zoom = 1f;
        [SerializeField] private Vector2 uvCenter = new(0.5f, 0.5f);
        [SerializeField] private string selectedHistoryId;

        private MazeTextureModifyProcessor processor;
        private Texture2D historyTexture;
        private bool renderQueued;
        private bool renderUpdateRegistered;
        private double nextRenderTime;
        private bool validAsset;
        private string operationMessage;
        private bool operationError;
        private bool pointerDragging;
        private int pointerId;
        private Vector2 previousPointerPosition;

        private Label assetLabel;
        private Label currentLabel;
        private Label statusLabel;
        private HelpBox validationBox;
        private VisualElement previewRoot;
        private VisualElement sideBySideRoot;
        private VisualElement combinedRoot;
        private VisualElement currentPane;
        private Image currentImage;
        private Image previewImage;
        private Image combinedImage;
        private Slider wipeSlider;
        private Button applyButton;
        private Button restoreButton;
        private VisualElement historyRows;
        private DropdownField compareField;
        private DropdownField channelField;
        private DropdownField viewField;
        private DropdownField backgroundField;
        private Toggle tileToggle;

        [MenuItem(MenuPath, false, 2000)]
        private static void OpenFromSelection()
        {
            if (Selection.activeObject is not Texture2D texture)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(texture);
            var window = GetWindow<MazeTextureModifyWindow>();
            window.titleContent = new GUIContent("MODIFY");
            window.minSize = new Vector2(1000f, 800f);
            window.AssignAsset(path);
            window.Show();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateOpenFromSelection()
        {
            return Selection.objects.Length == 1 && Selection.activeObject is Texture2D;
        }

        public void CreateGUI()
        {
            titleContent = new GUIContent("MODIFY");
            minSize = new Vector2(1000f, 800f);
            processor ??= new MazeTextureModifyProcessor();
            rootVisualElement.Clear();
            var style = LoadStyleSheet();
            if (style != null)
            {
                rootVisualElement.styleSheets.Add(style);
            }
            rootVisualElement.AddToClassList("maze-modify-root");
            BuildBody();
            BuildFooter();
            ResolveSerializedAsset();
        }

        private static StyleSheet LoadStyleSheet()
        {
            foreach (var guid in AssetDatabase.FindAssets("MazeTextureModify t:StyleSheet"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileName(path), StyleFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                }
            }

            return null;
        }

        private void OnDisable()
        {
            renderQueued = false;
            StopRenderUpdates();
            processor?.Dispose();
            processor = null;
            DestroyHistoryTexture();
        }

        internal void AssignAsset(string path)
        {
            assetPath = path;
            assetGuid = AssetDatabase.AssetPathToGUID(path);
            selectedHistoryId = string.Empty;
            settings ??= new MazeTextureModifySettings();
            settings.Reset();
            settings.tileMode = true;
            zoom = 1f;
            uvCenter = new Vector2(0.5f, 0.5f);
            loadedSourceHash = string.Empty;
            if (rootVisualElement.panel != null)
            {
                LoadAsset();
            }
            else
            {
                EditorApplication.delayCall += () =>
                {
                    if (this != null && rootVisualElement.panel != null)
                    {
                        LoadAsset();
                    }
                };
            }
        }

        private void ResolveSerializedAsset()
        {
            if (!string.IsNullOrWhiteSpace(assetGuid))
            {
                var resolved = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    assetPath = resolved;
                }
            }
            LoadAsset();
        }

        private void BuildBody()
        {
            var body = new VisualElement();
            body.AddToClassList("maze-modify-body");
            previewRoot = BuildPreview();
            body.Add(previewRoot);
            body.Add(BuildControls());
            rootVisualElement.Add(body);
        }

        private VisualElement BuildPreview()
        {
            var root = new VisualElement();
            root.AddToClassList("maze-modify-preview-root");

            var assetToolbar = new Toolbar();
            assetToolbar.AddToClassList("maze-modify-toolbar");
            assetLabel = new Label("No texture");
            assetLabel.AddToClassList("maze-modify-asset-label");
            assetToolbar.Add(assetLabel);
            assetToolbar.Add(CreateToolbarSpacer());
            tileToggle = new ToolbarToggle { text = "Tile Mode", value = settings?.tileMode ?? true };
            tileToggle.RegisterValueChangedCallback(evt =>
            {
                settings.tileMode = evt.newValue;
                QueueRender();
            });
            assetToolbar.Add(tileToggle);
            root.Add(assetToolbar);

            var previewToolbar = new Toolbar();
            var fitButton = new ToolbarButton(() =>
            {
                zoom = 1f;
                uvCenter = ViewCenter();
                ApplyViewUv();
            }) { text = "Fit" };
            previewToolbar.Add(fitButton);
            var actualButton = new ToolbarButton(SetActualSize) { text = "100%" };
            previewToolbar.Add(actualButton);

            compareField = CreateEnumDropdown(compareMode, value =>
            {
                compareMode = (MazeTextureCompareMode)value;
                UpdateCompareLayout();
                QueueRender();
            });
            previewToolbar.Add(compareField);
            channelField = CreateEnumDropdown(channelMode, value =>
            {
                channelMode = (MazeTextureChannelMode)value;
                QueueRender();
            });
            previewToolbar.Add(channelField);
            viewField = CreateEnumDropdown(viewMode, value =>
            {
                viewMode = (MazeTextureViewMode)value;
                uvCenter = ViewCenter();
                ApplyViewUv();
            });
            previewToolbar.Add(viewField);
            backgroundField = CreateEnumDropdown(backgroundMode, value =>
            {
                backgroundMode = (MazeTextureBackgroundMode)value;
                QueueRender();
            });
            previewToolbar.Add(backgroundField);

            wipeSlider = new Slider(0f, 1f) { value = wipe };
            wipeSlider.AddToClassList("maze-modify-wipe-slider");
            wipeSlider.RegisterValueChangedCallback(evt =>
            {
                wipe = evt.newValue;
                QueueRender();
            });
            previewToolbar.Add(wipeSlider);
            root.Add(previewToolbar);

            sideBySideRoot = new VisualElement();
            sideBySideRoot.AddToClassList("maze-modify-side-by-side");
            (currentPane, currentImage, currentLabel) = CreateImagePane("Current");
            var previewPane = CreateImagePane("Preview");
            previewImage = previewPane.image;
            sideBySideRoot.Add(currentPane);
            sideBySideRoot.Add(previewPane.pane);
            root.Add(sideBySideRoot);

            combinedRoot = new VisualElement();
            combinedRoot.AddToClassList("maze-modify-combined");
            var combinedPane = CreateImagePane("Compare");
            combinedImage = combinedPane.image;
            combinedRoot.Add(combinedPane.pane);
            root.Add(combinedRoot);

            root.RegisterCallback<WheelEvent>(OnPreviewWheel);
            root.RegisterCallback<PointerDownEvent>(OnPreviewPointerDown);
            root.RegisterCallback<PointerMoveEvent>(OnPreviewPointerMove);
            root.RegisterCallback<PointerUpEvent>(OnPreviewPointerUp);
            root.RegisterCallback<PointerCaptureOutEvent>(_ => pointerDragging = false);
            UpdateCompareLayout();
            return root;
        }

        private VisualElement BuildControls()
        {
            var deck = new VisualElement();
            deck.AddToClassList("maze-modify-control-deck");
            var grid = new VisualElement();
            grid.AddToClassList("maze-modify-control-grid");

            AddSlider(grid, "Exposure", -4f, 4f, 0f, () => settings.exposure, value => settings.exposure = value);
            AddSlider(grid, "Lightness", -1f, 1f, 0f, () => settings.lightness, value => settings.lightness = value);
            AddSlider(grid, "Contrast", -1f, 1f, 0f, () => settings.contrast, value => settings.contrast = value);
            AddSlider(grid, "Saturation", -1f, 1f, 0f, () => settings.saturation, value => settings.saturation = value);
            AddSlider(grid, "Vibrance", -1f, 1f, 0f, () => settings.vibrance, value => settings.vibrance = value);
            AddSlider(grid, "Temperature", -1f, 1f, 0f, () => settings.temperature, value => settings.temperature = value);
            AddSlider(grid, "Tint", -1f, 1f, 0f, () => settings.tint, value => settings.tint = value);
            AddSlider(grid, "Hue", -180f, 180f, 0f, () => settings.hue, value => settings.hue = value);
            AddSlider(grid, "Shadows", -1f, 1f, 0f, () => settings.shadows, value => settings.shadows = value);
            AddSlider(grid, "Highlights", -1f, 1f, 0f, () => settings.highlights, value => settings.highlights = value);
            AddSlider(grid, "Black Point", 0f, 1f, 0f, () => settings.blackPoint, value => settings.blackPoint = value);
            AddSlider(grid, "White Point", 0f, 1f, 0f, () => settings.whitePoint, value => settings.whitePoint = value);
            AddSlider(grid, "Blur", 0f, 1f, 0f, () => settings.blur, value => settings.blur = value);
            AddSlider(grid, "Sharpen", 0f, 1f, 0f, () => settings.sharpen, value => settings.sharpen = value);
            AddSlider(grid, "Sharp Radius", 0.5f, 4f, 1f, () => settings.sharpenRadius, value => settings.sharpenRadius = value);
            AddSlider(grid, "Clarity", 0f, 1f, 0f, () => settings.clarity, value => settings.clarity = value);
            AddSlider(grid, "Fade", 0f, 1f, 0f, () => settings.fade, value => settings.fade = value);
            AddSlider(grid, "Posterize", 0f, 1f, 0f, () => settings.posterize, value => settings.posterize = value);

            var invertCard = new VisualElement();
            invertCard.AddToClassList("maze-modify-control-card");
            var invert = new Toggle("Invert") { value = settings.invert };
            invert.RegisterValueChangedCallback(evt =>
            {
                settings.invert = evt.newValue;
                invertCard.EnableInClassList("maze-modify-slider-active", evt.newValue);
                QueueRender();
            });
            invertCard.EnableInClassList("maze-modify-slider-active", settings.invert);
            invertCard.Add(invert);
            grid.Add(invertCard);
            deck.Add(grid);

            var history = new Foldout { text = "History", value = false };
            history.AddToClassList("maze-modify-history");
            historyRows = new VisualElement();
            historyRows.AddToClassList("maze-modify-history-rows");
            history.Add(historyRows);
            restoreButton = new Button(RestoreSelectedHistory) { text = "Restore Selected" };
            restoreButton.SetEnabled(false);
            history.Add(restoreButton);
            deck.Add(history);

            validationBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            validationBox.style.display = DisplayStyle.None;
            deck.Add(validationBox);
            return deck;
        }

        private void BuildFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("maze-modify-footer");
            statusLabel = new Label();
            statusLabel.AddToClassList("maze-modify-status");
            footer.Add(statusLabel);
            var reset = new Button(() =>
            {
                settings.Reset();
                tileToggle?.SetValueWithoutNotify(settings.tileMode);
                RebuildControlsAndRender();
            }) { text = "Reset All" };
            footer.Add(reset);
            applyButton = new Button(Apply) { text = "Apply" };
            applyButton.AddToClassList("maze-modify-apply");
            footer.Add(applyButton);
            rootVisualElement.Add(footer);
        }

        private void LoadAsset()
        {
            operationMessage = string.Empty;
            operationError = false;
            DestroyHistoryTexture();
            selectedHistoryId = string.Empty;
            processor?.Dispose();
            processor = new MazeTextureModifyProcessor();
            currentImage.image = null;
            previewImage.image = null;
            combinedImage.image = null;
            validAsset = ValidateAsset(out var validationMessage, out var importer);
            assetLabel.text = string.IsNullOrWhiteSpace(assetPath) ? "No texture" : $"{Path.GetFileName(assetPath)}   {GetTextureSizeText()}";
            if (!validAsset)
            {
                ShowValidation(validationMessage, true);
                applyButton?.SetEnabled(false);
                return;
            }

            if (!processor.Load(assetPath, importer.sRGBTexture, out var loadError))
            {
                validAsset = false;
                ShowValidation(loadError, true);
                applyButton?.SetEnabled(false);
                return;
            }

            assetLabel.text = $"{Path.GetFileName(assetPath)}   {processor.Width} × {processor.Height}";
            loadedSourceHash = MazeTextureModifyHistory.ComputeCurrentHash(assetPath);
            ShowValidation(string.Empty, false);
            RefreshHistory();
            QueueRender();
        }

        private bool ValidateAsset(out string message, out TextureImporter importer)
        {
            importer = null;
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                message = "MODIFY works on source textures inside this MAZE project's Assets folder.";
                return false;
            }

            var extension = Path.GetExtension(assetPath).ToLowerInvariant();
            if (extension is not (".png" or ".jpg" or ".jpeg"))
            {
                message = $"{extension} is not supported yet. MODIFY currently accepts PNG and JPEG color textures.";
                return false;
            }

            importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                message = "This asset does not have a writable TextureImporter source.";
                return false;
            }
            if (!importer.sRGBTexture)
            {
                message = "This is a linear/data texture. MODIFY currently protects data, mask, HDR, and normal-map sources from color processing.";
                return false;
            }
            if (importer.textureType is TextureImporterType.NormalMap or TextureImporterType.SingleChannel or TextureImporterType.Lightmap or TextureImporterType.DirectionalLightmap)
            {
                message = $"{importer.textureType} textures are not color images and are protected from MODIFY.";
                return false;
            }

            var fullPath = MazeTextureModifyHistory.AssetPathToFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                message = "The selected texture has no source file on disk.";
                return false;
            }
            if ((File.GetAttributes(fullPath) & FileAttributes.ReadOnly) != 0)
            {
                message = "The source file is read-only.";
                return false;
            }
            if (MazeTextureModifyHistory.ContainsEmbeddedIccProfile(assetPath))
            {
                message = "This PNG contains an embedded ICC profile. MODIFY will not strip it silently.";
                return false;
            }
            return true;
        }

        private void QueueRender()
        {
            if (!validAsset || processor == null)
            {
                UpdateApplyState();
                return;
            }

            renderQueued = true;
            if (!renderUpdateRegistered)
            {
                renderUpdateRegistered = true;
                EditorApplication.update += ProcessQueuedRender;
            }
            UpdateApplyState();
        }

        private void ProcessQueuedRender()
        {
            if (this == null || processor == null || !validAsset)
            {
                StopRenderUpdates();
                return;
            }
            if (!renderQueued)
            {
                StopRenderUpdates();
                return;
            }
            if (EditorApplication.timeSinceStartup < nextRenderTime)
            {
                return;
            }

            renderQueued = false;
            RenderNow();
            nextRenderTime = EditorApplication.timeSinceStartup + PreviewFrameInterval;
            if (!renderQueued)
            {
                StopRenderUpdates();
            }
        }

        private void StopRenderUpdates()
        {
            if (!renderUpdateRegistered)
            {
                return;
            }
            EditorApplication.update -= ProcessQueuedRender;
            renderUpdateRegistered = false;
        }

        private void RenderNow()
        {
            if (this == null || processor == null || !validAsset)
            {
                return;
            }
            try
            {
                processor.RenderPreview(settings);
                processor.RenderViews(channelMode, compareMode, wipe, backgroundMode, historyTexture);
                currentImage.image = processor.CurrentView;
                previewImage.image = processor.PreviewView;
                combinedImage.image = processor.CompareView;
                UpdateCompareLayout();
                ApplyViewUv();
                UpdateApplyState();
            }
            catch (Exception exception)
            {
                operationError = true;
                operationMessage = exception.Message;
                UpdateStatus();
            }
        }

        private void Apply()
        {
            if (!validAsset || settings.IsNeutral)
            {
                return;
            }
            SetOperationState("Processing full resolution...", false, false);
            try
            {
                var bytes = processor.EncodeFullResolution(settings, Path.GetExtension(assetPath));
                var result = MazeTextureModifyHistory.Apply(assetPath, assetGuid, loadedSourceHash, bytes, settings, "Before Apply");
                if (!result.Success)
                {
                    SetOperationState(result.Message, true, true);
                    return;
                }

                loadedSourceHash = result.SourceHash;
                settings.Reset();
                selectedHistoryId = string.Empty;
                DestroyHistoryTexture();
                LoadAsset();
                SetOperationState(result.Message, false, true);
            }
            catch (Exception exception)
            {
                SetOperationState(exception.Message, true, true);
            }
        }

        private void RestoreSelectedHistory()
        {
            var manifest = MazeTextureModifyHistory.LoadManifest(assetGuid, assetPath);
            var entry = manifest.entries.FirstOrDefault(candidate => candidate.id == selectedHistoryId);
            if (entry == null)
            {
                return;
            }
            if (!EditorUtility.DisplayDialog("Restore Texture", $"Restore {FormatHistoryEntry(entry)}? The current texture will be added to History first.", "Restore", "Cancel"))
            {
                return;
            }

            try
            {
                var bytes = MazeTextureModifyHistory.ReadSnapshot(assetGuid, entry);
                var neutral = new MazeTextureModifySettings { tileMode = settings.tileMode };
                var result = MazeTextureModifyHistory.Apply(assetPath, assetGuid, loadedSourceHash, bytes, neutral, "Before Restore");
                if (!result.Success)
                {
                    SetOperationState(result.Message, true, true);
                    return;
                }
                loadedSourceHash = result.SourceHash;
                settings.Reset();
                selectedHistoryId = string.Empty;
                DestroyHistoryTexture();
                LoadAsset();
                SetOperationState("History revision restored.", false, true);
            }
            catch (Exception exception)
            {
                SetOperationState(exception.Message, true, true);
            }
        }

        private void RefreshHistory()
        {
            if (historyRows == null || string.IsNullOrWhiteSpace(assetGuid))
            {
                return;
            }
            historyRows.Clear();
            var manifest = MazeTextureModifyHistory.LoadManifest(assetGuid, assetPath);
            if (manifest.entries.Count == 0)
            {
                historyRows.Add(new Label("No applied revisions"));
                restoreButton?.SetEnabled(false);
                return;
            }

            foreach (var entry in manifest.entries.AsEnumerable().Reverse())
            {
                var captured = entry;
                var button = new Button(() => SelectHistory(captured)) { text = FormatHistoryEntry(captured) };
                button.EnableInClassList("maze-modify-history-selected", captured.id == selectedHistoryId);
                historyRows.Add(button);
            }
            restoreButton?.SetEnabled(!string.IsNullOrWhiteSpace(selectedHistoryId));
        }

        private void SelectHistory(MazeTextureHistoryEntry entry)
        {
            DestroyHistoryTexture();
            var bytes = MazeTextureModifyHistory.ReadSnapshot(assetGuid, entry);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            historyTexture = MazeTextureModifyProcessor.Decode(bytes, importer?.sRGBTexture ?? true);
            if (historyTexture == null)
            {
                SetOperationState("The selected history revision could not be decoded.", true, true);
                return;
            }
            historyTexture.name = "MAZE History Preview";
            selectedHistoryId = entry.id;
            currentLabel.text = entry.label;
            RefreshHistory();
            QueueRender();
        }

        private void UpdateApplyState()
        {
            applyButton?.SetEnabled(validAsset && settings != null && !settings.IsNeutral);
            UpdateStatus();
        }

        private void SetOperationState(string message, bool error, bool enableApply)
        {
            operationMessage = message;
            operationError = error;
            applyButton?.SetEnabled(enableApply && validAsset && !settings.IsNeutral);
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (statusLabel == null)
            {
                return;
            }
            statusLabel.text = operationMessage;
            statusLabel.EnableInClassList("maze-modify-status-error", operationError);
        }

        private void ShowValidation(string message, bool error)
        {
            if (validationBox == null)
            {
                return;
            }
            validationBox.text = message;
            validationBox.messageType = error ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning;
            validationBox.style.display = string.IsNullOrWhiteSpace(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void UpdateCompareLayout()
        {
            if (sideBySideRoot == null || combinedRoot == null)
            {
                return;
            }
            var side = compareMode == MazeTextureCompareMode.SideBySide;
            sideBySideRoot.style.display = side ? DisplayStyle.Flex : DisplayStyle.None;
            combinedRoot.style.display = side ? DisplayStyle.None : DisplayStyle.Flex;
            wipeSlider.style.display = compareMode == MazeTextureCompareMode.Wipe ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ApplyViewUv()
        {
            var baseSize = viewMode == MazeTextureViewMode.Tiles3x3 ? 3f : 1f;
            var center = viewMode == MazeTextureViewMode.Seams && zoom <= 1.001f ? Vector2.zero : uvCenter;
            var size = baseSize / Mathf.Max(0.05f, zoom);
            var uv = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
            if (currentImage != null) currentImage.uv = uv;
            if (previewImage != null) previewImage.uv = uv;
            if (combinedImage != null) combinedImage.uv = uv;
        }

        private void OnPreviewWheel(WheelEvent evt)
        {
            var multiplier = evt.delta.y > 0f ? 0.86f : 1.16f;
            zoom = Mathf.Clamp(zoom * multiplier, 0.25f, 32f);
            ApplyViewUv();
            evt.StopPropagation();
        }

        private void OnPreviewPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 && evt.button != 2)
            {
                return;
            }
            pointerDragging = true;
            pointerId = evt.pointerId;
            previousPointerPosition = evt.position;
            previewRoot.CapturePointer(pointerId);
            evt.StopPropagation();
        }

        private void OnPreviewPointerMove(PointerMoveEvent evt)
        {
            if (!pointerDragging || evt.pointerId != pointerId)
            {
                return;
            }
            var pointerPosition = new Vector2(evt.position.x, evt.position.y);
            var delta = pointerPosition - previousPointerPosition;
            previousPointerPosition = pointerPosition;
            var width = Mathf.Max(1f, previewRoot.resolvedStyle.width);
            var height = Mathf.Max(1f, previewRoot.resolvedStyle.height);
            var baseSize = viewMode == MazeTextureViewMode.Tiles3x3 ? 3f : 1f;
            var visible = baseSize / Mathf.Max(0.05f, zoom);
            uvCenter -= new Vector2(delta.x / width * visible, -delta.y / height * visible);
            ApplyViewUv();
            evt.StopPropagation();
        }

        private void OnPreviewPointerUp(PointerUpEvent evt)
        {
            if (!pointerDragging || evt.pointerId != pointerId)
            {
                return;
            }
            pointerDragging = false;
            if (previewRoot.HasPointerCapture(pointerId))
            {
                previewRoot.ReleasePointer(pointerId);
            }
            evt.StopPropagation();
        }

        private void SetActualSize()
        {
            if (processor == null || currentPane == null)
            {
                return;
            }
            var paneWidth = Mathf.Max(1f, currentPane.resolvedStyle.width);
            var paneHeight = Mathf.Max(1f, currentPane.resolvedStyle.height);
            zoom = Mathf.Max(1f, Mathf.Min(processor.Width / paneWidth, processor.Height / paneHeight));
            uvCenter = new Vector2(0.5f, 0.5f);
            ApplyViewUv();
        }

        private Vector2 ViewCenter()
        {
            return viewMode == MazeTextureViewMode.Seams ? Vector2.zero : new Vector2(0.5f, 0.5f);
        }

        private void RebuildControlsAndRender()
        {
            var savedPath = assetPath;
            var savedGuid = assetGuid;
            CreateGUI();
            assetPath = savedPath;
            assetGuid = savedGuid;
            QueueRender();
        }

        private void AddSlider(VisualElement parent, string label, float minimum, float maximum, float neutral, Func<float> getter, Action<float> setter)
        {
            var card = new VisualElement();
            card.AddToClassList("maze-modify-control-card");
            var header = new VisualElement();
            header.AddToClassList("maze-modify-control-header");
            var name = new Label(label);
            name.AddToClassList("maze-modify-slider-label");
            header.Add(name);
            var field = new FloatField { value = getter() };
            field.AddToClassList("maze-modify-number");
            header.Add(field);
            var slider = new Slider(minimum, maximum) { value = getter() };
            slider.AddToClassList("maze-modify-slider");
            var reset = new Button(() =>
            {
                setter(neutral);
                slider.SetValueWithoutNotify(neutral);
                field.SetValueWithoutNotify(neutral);
                card.EnableInClassList("maze-modify-slider-active", false);
                QueueRender();
            }) { text = "•" };
            reset.tooltip = $"Reset {label}";
            reset.AddToClassList("maze-modify-slider-reset");
            header.Add(reset);
            card.Add(header);

            slider.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                field.SetValueWithoutNotify(evt.newValue);
                card.EnableInClassList("maze-modify-slider-active", Mathf.Abs(evt.newValue - neutral) > 0.0001f);
                QueueRender();
            });
            field.RegisterValueChangedCallback(evt =>
            {
                var value = Mathf.Clamp(evt.newValue, minimum, maximum);
                setter(value);
                slider.SetValueWithoutNotify(value);
                field.SetValueWithoutNotify(value);
                card.EnableInClassList("maze-modify-slider-active", Mathf.Abs(value - neutral) > 0.0001f);
                QueueRender();
            });
            name.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.clickCount == 2)
                {
                    setter(neutral);
                    slider.SetValueWithoutNotify(neutral);
                    field.SetValueWithoutNotify(neutral);
                    card.EnableInClassList("maze-modify-slider-active", false);
                    QueueRender();
                }
            });
            card.EnableInClassList("maze-modify-slider-active", Mathf.Abs(getter() - neutral) > 0.0001f);
            card.Add(slider);
            parent.Add(card);
        }

        private static DropdownField CreateEnumDropdown<T>(T value, Action<Enum> changed) where T : Enum
        {
            var choices = Enum.GetNames(typeof(T)).Select(ObjectNames.NicifyVariableName).ToList();
            var field = new DropdownField(choices, Convert.ToInt32(value, CultureInfo.InvariantCulture));
            field.RegisterValueChangedCallback(evt =>
            {
                var index = choices.IndexOf(evt.newValue);
                changed((Enum)Enum.ToObject(typeof(T), Mathf.Max(0, index)));
            });
            field.AddToClassList("maze-modify-toolbar-dropdown");
            return field;
        }

        private static VisualElement CreateToolbarSpacer()
        {
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            return spacer;
        }

        private static (VisualElement pane, Image image, Label label) CreateImagePane(string labelText)
        {
            var pane = new VisualElement();
            pane.AddToClassList("maze-modify-image-pane");
            var label = new Label(labelText);
            label.AddToClassList("maze-modify-image-label");
            pane.Add(label);
            var image = new Image { scaleMode = ScaleMode.ScaleToFit };
            image.AddToClassList("maze-modify-image");
            pane.Add(image);
            return (pane, image, label);
        }

        private string GetTextureSizeText()
        {
            if (processor != null && processor.Width > 0)
            {
                return $"{processor.Width} × {processor.Height}";
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            return texture != null ? $"{texture.width} × {texture.height}" : string.Empty;
        }

        private static string FormatHistoryEntry(MazeTextureHistoryEntry entry)
        {
            if (DateTime.TryParse(entry.timestampUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
            {
                return $"{entry.label}   {timestamp.ToLocalTime():MMM d  h:mm:ss tt}";
            }
            return entry.label;
        }

        private void DestroyHistoryTexture()
        {
            if (historyTexture == null)
            {
                return;
            }
            DestroyImmediate(historyTexture);
            historyTexture = null;
            if (currentLabel != null) currentLabel.text = "Current";
        }

    }
}
#endif
