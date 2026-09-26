using System.Collections.Generic;
using UnityEngine;

namespace Maze
{
    public enum MazeFilterEffectType
    {
        [InspectorName("Prism Pixelation")] Pixelize = 0,
        Blur = 1,
        [InspectorName("Grid Warp")] GridWarp = 2,
        [InspectorName("Palette Crush")] PaletteCrush = 3,
        [InspectorName("Ordered Dither")] OrderedDither = 4,
        [InspectorName("Ink Clamp")] InkClamp = 5,
        [InspectorName("Poster Bands")] PosterBands = 6,
        [InspectorName("Chromatic Slip")] ChromaticSlip = 7,
        [InspectorName("Edge Burn")] EdgeBurn = 8,
        [InspectorName("Halftone Shade")] HalftoneShade = 9,
        [InspectorName("Signal Tear")] SignalTear = 10,
        [InspectorName("Heat Ripple")] HeatRipple = 11,
        [InspectorName("Ghost Echo")] GhostEcho = 12,
        [InspectorName("Paper Grain Wash")] PaperGrainWash = 13,
        Kuwahara = 14,
        [InspectorName("Blur Slope")] BlurSlope = 15,
        [InspectorName("Sobel Sketch")] SobelSketch = 16,
        [InspectorName("Watercolor Bleed")] WatercolorBleed = 17,
        [InspectorName("Clarity High Pass")] ClarityHighPass = 18,
        [InspectorName("Color Fill")] ColorFill = 19,
    }

    public enum MazeFilterPixelShape
    {
        Square = 0,
        Triangle = 1,
        [InspectorName("Round Cells")] Circle = 2,
        [InspectorName("Thin Diamond")] Diamond = 3,
        Hex = 4,
        Brick = 5,
        [InspectorName("Dirty Rects")] DirtyRects = 6,
        Tendril = 7,
        Blob = 8,
        [InspectorName("Square Triangles")] SquareTriangle = 9,
    }

    public enum MazeFilterHalftoneShape
    {
        Circle = 0,
        Square = 1,
        Triangle = 2,
        [InspectorName("Thin Diamond")] Diamond = 3,
        Crosshatch = 4,
    }

    public enum MazeFilterColorMode
    {
        [InspectorName("Source Darken")] SourceDarken = 0,
        [InspectorName("Solid Ink")] SolidInk = 1,
        [InspectorName("Gradient Ink")] GradientInk = 2,
        [InspectorName("Two Tone")] TwoTone = 3,
        [InspectorName("Tint Source")] TintSource = 4,
    }

    public enum MazeFilterBlendMode
    {
        Normal = 0,
        Add = 1,
        Multiply = 2,
        Screen = 3,
        Overlay = 4,
        [InspectorName("Soft Light")] SoftLight = 5,
        Darken = 6,
        Lighten = 7,
        [InspectorName("Linear Light")] LinearLight = 8,
    }

    public enum MazeFilterGradientMode
    {
        Solid = 0,
        Linear = 1,
        Radial = 2,
    }

    public enum MazeFilterToneIsolation
    {
        Darker = 0,
        Lighter = 1,
        [InspectorName("Low Contrast")] LowContrast = 2,
        Midtones = 3,
        Full = 4,
    }

    public enum MazeFilterSlipPalette
    {
        [InspectorName("Red / Cyan")] RedCyan = 0,
        [InspectorName("Magenta / Green")] MagentaGreen = 1,
        [InspectorName("Blue / Yellow")] BlueYellow = 2,
        [InspectorName("Warm / Cool")] WarmCool = 3,
        [InspectorName("Purple / Gold")] PurpleGold = 4,
        [InspectorName("RGB Spread")] RgbSpread = 5,
    }

    public enum MazeFilterTargetMode
    {
        [InspectorName("Full Screen")] FullScreen = 0,
        Targets = 1,
        [InspectorName("Screen Edge")] ScreenEdge = 2,
        [InspectorName("Depth Band")] DepthBand = 3,
        [InspectorName("Screen Region")] ScreenRegion = 4,
        [InspectorName("Object Silhouette")] ObjectSilhouette = 5,
    }

    public enum MazeFilterRegionShape
    {
        Rectangle = 0,
        Circle = 1,
        [InspectorName("Horizontal Strip")] HorizontalStrip = 2,
        [InspectorName("Vertical Strip")] VerticalStrip = 3,
        Diamond = 4,
        Ring = 5,
        [InspectorName("Diagonal Strip")] DiagonalStrip = 6,
    }

    public enum MazeFilterApplyOrder
    {
        [InspectorName("Final After Post FX")] FinalAfterPost = 0,
        [InspectorName("After Scene FX Before Post")] AfterSceneFXBeforePost = 1,
        [InspectorName("Early Before Scene FX")] EarlyBeforeSceneFX = 2,
    }

    public enum MazeFilterObjectSilhouetteMode
    {
        [InspectorName("Object Fill")] Filled = 0,
        [InspectorName("Outside Ring")] OutsideRing = 1,
        [InspectorName("Inside And Outside")] InsideAndOutside = 2,
    }

    public enum MazeFilterObjectOcclusion
    {
        [InspectorName("Visible Only")] VisibleOnly = 0,
        [InspectorName("Ignore Depth")] IgnoreDepth = 1,
    }

    public enum MazeFilterObjectMaskSource
    {
        [InspectorName("Exact Renderers")] ExactRenderers = 0,
        [InspectorName("Proxy Renderers")] ProxyRenderers = 1,
    }

    [System.Serializable]
    public sealed class MazeFilterLayer
    {
        public string name = "Filter Layer";
        public bool enabled;
        public MazeFilterEffectType effect = MazeFilterEffectType.Pixelize;
        [Range(0f, 1f)] public float intensity = 1f;
        [HideInInspector] [Range(0f, 1f)] public float sourceBlend = 1f;

        [Header("Modes")]
        public MazeFilterPixelShape pixelShape = MazeFilterPixelShape.Square;
        public MazeFilterHalftoneShape halftoneShape = MazeFilterHalftoneShape.Circle;
        public MazeFilterToneIsolation toneIsolation = MazeFilterToneIsolation.Darker;
        public MazeFilterSlipPalette slipPalette = MazeFilterSlipPalette.RedCyan;
        public MazeFilterColorMode colorMode = MazeFilterColorMode.SourceDarken;
        public MazeFilterBlendMode blendMode = MazeFilterBlendMode.Overlay;
        public MazeFilterGradientMode gradientMode = MazeFilterGradientMode.Solid;

        [Header("Shape")]
        [Range(2f, 256f)] public float cellSizePixels = 18f;
        [Range(0f, 1f)] public float jitter = 0.15f;
        [Range(0f, 10f)] public float sizeVariation;
        [Range(1f, 8f)] public float tileSizeMinMultiplier = 1f;
        [Range(1f, 8f)] public float tileSizeMaxMultiplier = 8f;
        [Range(0.15f, 2.5f)] public float shapeAspect = 1f;
        [Range(-2f, 2f)] public float rowShear;
        public float seed = 1f;
        [Range(0f, 8f)] public float timeSpeed;

        [Header("Samples")]
        [Range(0f, 32f)] public float blurRadiusPixels = 3f;
        [Range(1, 12)] public int blurSamples = 4;

        [Header("Pattern Warp")]
        [Range(0.01f, 0.5f)] public float gridEdgeWidth = 0.14f;
        [Range(-32f, 32f)] public float gridPushPixels = 4f;
        [Range(-1f, 1f)] public float motionRange;

        [Header("Tone")]
        [Range(2f, 32f)] public float colorSteps = 6f;
        [Range(0f, 8f)] public float contrast = 1f;
        [Range(0f, 1f)] public float softness = 0.25f;
        [Range(0f, 1f)] public float halftoneAreaThreshold = 0.5f;
        [Range(0.001f, 1f)] public float halftoneAreaSoftness = 0.5f;
        [Range(0f, 0.1f)] public float highPassThreshold;
        [Range(0.001f, 0.1f)] public float highPassSoftness = 0.02f;
        public bool highPassLumaOnly = true;
        [Range(-1f, 1f)] public float colorShift;
        [Range(0f, 360f)] public float angleDegrees;
        [Range(0f, 360f)] public float rotationDegrees;
        public Color primaryColor = new(0.02f, 0.015f, 0.04f, 1f);
        public Color secondaryColor = new(0.7f, 0.1f, 1f, 1f);
        [Range(0f, 360f)] public float gradientAngleDegrees = 90f;
        [Range(-1f, 1f)] public float gradientOffset;
    }

    [CreateAssetMenu(fileName = "MAZE_FilterProfile", menuName = "MAZE/Rendering/Filter Profile")]
    public sealed class MazeFilterProfile : ScriptableObject
    {
        public const int MaxLayers = 24; // Keep in sync with MAZE_FILTER_MAX_LAYERS in Maze_Filter.shader.
        private const int DefaultLayerCount = 20;
        private const int CurrentDataVersion = 7;
        public const int MaxTargets = 8;

        [SerializeField, HideInInspector] private int dataVersion;

        [Header("Key Parameters")]
        [Range(0.25f, 1f)] public float sampling = 1f;
        [Range(0.25f, 1f)] public float maskFidelity = 1f;

        [Header("Core")]
        public bool filterEnabled = true;
        public MazeFilterApplyOrder applyOrder = MazeFilterApplyOrder.FinalAfterPost;
        [Range(-20, 20)] public int stackOrder;
        public bool renderInSceneView = true;

        [Header("Target")]
        public MazeFilterTargetMode targetMode = MazeFilterTargetMode.FullScreen;

        [Header("Depth Band")]
        [Min(0.01f)] public float depthStart = 4f;
        [Min(0.01f)] public float depthEnd = 24f;
        [Min(0.01f)] public float depthSoftness = 3f;
        [Range(0f, 1f)] public float skyAmount = 1f;

        [Header("Screen Edge")]
        [Range(0f, 384f)] public float screenEdgeWidthPixels = 96f;
        [Range(1f, 384f)] public float screenEdgeSoftnessPixels = 64f;

        [Header("Screen Region")]
        public MazeFilterRegionShape regionShape = MazeFilterRegionShape.Rectangle;
        public Vector2 regionCenter = new(0.5f, 0.5f);
        public Vector2 regionSize = new(0.5f, 0.5f);
        [Range(0f, 0.25f)] public float regionSoftness = 0.05f;
        [Range(0f, 360f)] public float regionRotationDegrees;

        [Header("Object Silhouette")]
        public MazeFilterObjectSilhouetteMode objectSilhouetteMode = MazeFilterObjectSilhouetteMode.Filled;
        public MazeFilterObjectOcclusion objectOcclusion = MazeFilterObjectOcclusion.VisibleOnly;
        public MazeFilterObjectMaskSource objectMaskSource = MazeFilterObjectMaskSource.ExactRenderers;
        [Range(0.02f, 1f)] public float objectMaskResolutionScale = 0.5f;
        [Range(0f, 0.1f)] public float objectDepthBias = 0.03f;
        [Range(0f, 96f)] public float objectExtensionPixels = 12f;
        [Range(0.25f, 96f)] public float objectSoftnessPixels = 8f;
        [Range(0.2f, 4f)] public float objectEdgeCurve = 1f;

        [Header("Mask Controls")]
        [Range(0f, 2f)] public float maskStrength = 1f;
        [Range(0.1f, 8f)] public float maskContrast = 1f;
        [Range(-1f, 1f)] public float maskBias;
        public bool invertMask;

        [Header("Effects")]
        public List<MazeFilterLayer> layers = new();

        public bool RendersFilters => filterEnabled && ActiveLayerCount > 0;
        public bool RequiresDepth => RendersFilters && targetMode == MazeFilterTargetMode.DepthBand;
        public bool RequiresTargets => RendersFilters && targetMode == MazeFilterTargetMode.Targets;
        public bool RequiresObjectSilhouette => RendersFilters && targetMode == MazeFilterTargetMode.ObjectSilhouette;

        private void OnEnable()
        {
            EnsureEffectRows();
        }

        private void OnValidate()
        {
            EnsureEffectRows();
        }

        public int ActiveLayerCount
        {
            get
            {
                if (layers == null)
                {
                    return 0;
                }

                var count = 0;
                var limit = Mathf.Min(layers.Count, MaxLayers);
                for (var i = 0; i < limit; i++)
                {
                    if (LayerRenders(layers[i]))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void EnsureEffectRows()
        {
            EnsureProfileDefaults();
            layers ??= new List<MazeFilterLayer>();
            EnsureLayer(0, "Prism Pixelation", MazeFilterEffectType.Pixelize);
            EnsureLayer(1, "Blur", MazeFilterEffectType.Blur);
            EnsureLayer(2, "Grid Warp", MazeFilterEffectType.GridWarp);
            EnsureLayer(3, "Palette Crush", MazeFilterEffectType.PaletteCrush);
            EnsureLayer(4, "Ordered Dither", MazeFilterEffectType.OrderedDither);
            EnsureLayer(5, "Ink Clamp", MazeFilterEffectType.InkClamp);
            EnsureLayer(6, "Poster Bands", MazeFilterEffectType.PosterBands);
            EnsureLayer(7, "Chromatic Slip", MazeFilterEffectType.ChromaticSlip);
            EnsureLayer(8, "Edge Burn", MazeFilterEffectType.EdgeBurn);
            EnsureLayer(9, "Halftone Shade", MazeFilterEffectType.HalftoneShade);
            EnsureLayer(10, "Signal Tear", MazeFilterEffectType.SignalTear);
            EnsureLayer(11, "Heat Ripple", MazeFilterEffectType.HeatRipple);
            EnsureLayer(12, "Ghost Echo", MazeFilterEffectType.GhostEcho);
            EnsureLayer(13, "Paper Grain Wash", MazeFilterEffectType.PaperGrainWash);
            EnsureLayer(14, "Kuwahara", MazeFilterEffectType.Kuwahara);
            EnsureLayer(15, "Blur Slope", MazeFilterEffectType.BlurSlope);
            EnsureLayer(16, "Sobel Sketch", MazeFilterEffectType.SobelSketch);
            EnsureLayer(17, "Watercolor Bleed", MazeFilterEffectType.WatercolorBleed);
            EnsureLayer(18, "Clarity High Pass", MazeFilterEffectType.ClarityHighPass);
            EnsureLayer(19, "Color Fill", MazeFilterEffectType.ColorFill);
            while (layers.Count > DefaultLayerCount)
            {
                layers.RemoveAt(layers.Count - 1);
            }

            for (var i = 0; i < layers.Count; i++)
            {
                RepairLayerDefaults(layers[i]);
            }
        }

        private void EnsureProfileDefaults()
        {
            var previousVersion = dataVersion;
            if (dataVersion >= CurrentDataVersion)
            {
                return;
            }

            if (layers != null)
            {
                foreach (var layer in layers)
                {
                    if (layer == null)
                    {
                        continue;
                    }

                    if (previousVersion < 2 && layer.pixelShape != MazeFilterPixelShape.Diamond && Mathf.Abs(layer.shapeAspect - 0.55f) <= 0.001f)
                    {
                        layer.shapeAspect = 1f;
                    }

                    if (previousVersion < 3)
                    {
                        layer.tileSizeMinMultiplier = 1f;
                        layer.tileSizeMaxMultiplier = 8f;
                        layer.halftoneAreaThreshold = 0.5f;
                        layer.halftoneAreaSoftness = 0.5f;
                    }

                    if (previousVersion < 4)
                    {
                        layer.blendMode = layer.effect == MazeFilterEffectType.ClarityHighPass ? MazeFilterBlendMode.LinearLight : MazeFilterBlendMode.Overlay;
                        layer.gradientMode = MazeFilterGradientMode.Solid;
                        layer.highPassThreshold = 0f;
                        layer.highPassSoftness = 0.02f;
                        layer.highPassLumaOnly = true;
                    }

                    if (previousVersion < 5 && layer.effect == MazeFilterEffectType.ClarityHighPass)
                    {
                        layer.blendMode = MazeFilterBlendMode.LinearLight;
                        if (layer.contrast <= 1.01f)
                        {
                            layer.contrast = 3f;
                        }

                        layer.highPassThreshold = layer.highPassThreshold > 0.05f ? 0f : Mathf.Clamp(layer.highPassThreshold, 0f, 0.1f);
                        layer.highPassSoftness = layer.highPassSoftness > 0.08f ? 0.02f : Mathf.Clamp(layer.highPassSoftness, 0.001f, 0.1f);
                    }
                }
            }

            if (previousVersion < 7)
            {
                sampling = 1f;
                maskFidelity = 1f;
                objectDepthBias = 0.03f;
                objectMaskSource = MazeFilterObjectMaskSource.ExactRenderers;
            }

            sampling = Mathf.Clamp(sampling, 0.25f, 1f);
            maskFidelity = Mathf.Clamp(maskFidelity, 0.25f, 1f);
            objectDepthBias = Mathf.Clamp(objectDepthBias, 0f, 0.1f);

            if (objectMaskResolutionScale <= 0f)
            {
                objectMaskResolutionScale = 0.5f;
            }

            objectMaskResolutionScale = Mathf.Clamp(objectMaskResolutionScale, 0.02f, 1f);

            if (objectExtensionPixels < 0f)
            {
                objectExtensionPixels = 12f;
            }

            if (objectSoftnessPixels <= 0f)
            {
                objectSoftnessPixels = 8f;
            }

            if (objectEdgeCurve <= 0f)
            {
                objectEdgeCurve = 1f;
            }

            if (maskStrength <= 0f)
            {
                maskStrength = 1f;
            }

            if (maskContrast <= 0f)
            {
                maskContrast = 1f;
            }

            dataVersion = CurrentDataVersion;
        }

        private void EnsureLayer(int index, string layerName, MazeFilterEffectType effect)
        {
            var created = false;
            while (layers.Count <= index)
            {
                layers.Add(new MazeFilterLayer());
                created = true;
            }

            var layer = layers[index];
            if (layer == null)
            {
                layer = new MazeFilterLayer();
                created = true;
            }

            layer.name = layerName;
            layer.effect = effect;
            if (created)
            {
                ApplyDefaults(layer, index, effect);
            }
            else
            {
                RepairLayerDefaults(layer);
            }

            layers[index] = layer;
        }

        private static void RepairLayerDefaults(MazeFilterLayer layer)
        {
            if (layer == null)
            {
                return;
            }

            if (layer.shapeAspect <= 0.01f)
            {
                layer.shapeAspect = 1f;
            }

            if (float.IsNaN(layer.sizeVariation) || float.IsInfinity(layer.sizeVariation))
            {
                layer.sizeVariation = 0f;
            }
            else
            {
                layer.sizeVariation = Mathf.Clamp(layer.sizeVariation, 0f, 10f);
            }

            if (float.IsNaN(layer.tileSizeMinMultiplier) || float.IsInfinity(layer.tileSizeMinMultiplier) || layer.tileSizeMinMultiplier <= 0f)
            {
                layer.tileSizeMinMultiplier = 1f;
            }

            if (float.IsNaN(layer.tileSizeMaxMultiplier) || float.IsInfinity(layer.tileSizeMaxMultiplier) || layer.tileSizeMaxMultiplier <= 0f)
            {
                layer.tileSizeMaxMultiplier = 8f;
            }

            layer.tileSizeMinMultiplier = Mathf.Clamp(layer.tileSizeMinMultiplier, 1f, 8f);
            layer.tileSizeMaxMultiplier = Mathf.Clamp(layer.tileSizeMaxMultiplier, layer.tileSizeMinMultiplier, 8f);

            if (float.IsNaN(layer.motionRange) || float.IsInfinity(layer.motionRange))
            {
                layer.motionRange = 0f;
            }

            if (float.IsNaN(layer.halftoneAreaThreshold) || float.IsInfinity(layer.halftoneAreaThreshold))
            {
                layer.halftoneAreaThreshold = 0.5f;
            }
            else
            {
                layer.halftoneAreaThreshold = Mathf.Clamp01(layer.halftoneAreaThreshold);
            }

            if (float.IsNaN(layer.halftoneAreaSoftness) || float.IsInfinity(layer.halftoneAreaSoftness) || layer.halftoneAreaSoftness <= 0f)
            {
                layer.halftoneAreaSoftness = 0.5f;
            }
            else
            {
                layer.halftoneAreaSoftness = Mathf.Clamp(layer.halftoneAreaSoftness, 0.001f, 1f);
            }

            if (float.IsNaN(layer.highPassThreshold) || float.IsInfinity(layer.highPassThreshold))
            {
                layer.highPassThreshold = 0f;
            }
            else
            {
                layer.highPassThreshold = Mathf.Clamp(layer.highPassThreshold, 0f, 0.1f);
            }

            if (float.IsNaN(layer.highPassSoftness) || float.IsInfinity(layer.highPassSoftness) || layer.highPassSoftness <= 0f)
            {
                layer.highPassSoftness = 0.02f;
            }
            else
            {
                layer.highPassSoftness = Mathf.Clamp(layer.highPassSoftness, 0.001f, 0.1f);
            }

            if (layer.primaryColor.a <= 0f && layer.primaryColor.r <= 0f && layer.primaryColor.g <= 0f && layer.primaryColor.b <= 0f)
            {
                layer.primaryColor = new Color(0.02f, 0.015f, 0.04f, 1f);
            }

            if (layer.secondaryColor.a <= 0f && layer.secondaryColor.r <= 0f && layer.secondaryColor.g <= 0f && layer.secondaryColor.b <= 0f)
            {
                layer.secondaryColor = new Color(0.7f, 0.1f, 1f, 1f);
            }
        }

        private static void ApplyDefaults(MazeFilterLayer layer, int index, MazeFilterEffectType effect)
        {
            layer.enabled = false;
            layer.intensity = 1f;
            layer.sourceBlend = 1f;
            layer.seed = index + 1;
            layer.cellSizePixels = effect switch
            {
                MazeFilterEffectType.Pixelize => 48f,
                MazeFilterEffectType.OrderedDither => 4f,
                MazeFilterEffectType.HalftoneShade => 12f,
                MazeFilterEffectType.SignalTear => 18f,
                _ => 18f,
            };
            layer.jitter = effect switch
            {
                MazeFilterEffectType.Pixelize => 0.22f,
                MazeFilterEffectType.PaperGrainWash => 0.2f,
                MazeFilterEffectType.SignalTear => 0.3f,
                _ => 0.15f,
            };
            layer.timeSpeed = effect is MazeFilterEffectType.HeatRipple or MazeFilterEffectType.SignalTear ? 1f : 0f;
            layer.sizeVariation = 0f;
            layer.tileSizeMinMultiplier = 1f;
            layer.tileSizeMaxMultiplier = 8f;
            layer.shapeAspect = 1f;
            layer.motionRange = 0f;
            layer.rowShear = 0f;
            layer.blurRadiusPixels = effect switch
            {
                MazeFilterEffectType.Kuwahara => 3f,
                MazeFilterEffectType.BlurSlope => 5f,
                MazeFilterEffectType.WatercolorBleed => 4f,
                MazeFilterEffectType.ClarityHighPass => 3f,
                MazeFilterEffectType.GhostEcho => 7f,
                _ => 3f,
            };
            layer.blurSamples = effect switch
            {
                MazeFilterEffectType.Kuwahara => 2,
                MazeFilterEffectType.BlurSlope => 5,
                MazeFilterEffectType.GhostEcho => 4,
                _ => 4,
            };
            layer.gridEdgeWidth = 0.14f;
            if (effect == MazeFilterEffectType.Pixelize)
            {
                layer.gridEdgeWidth = 0.08f;
            }

            layer.gridPushPixels = effect switch
            {
                MazeFilterEffectType.Pixelize => 0f,
                MazeFilterEffectType.ChromaticSlip => 3f,
                MazeFilterEffectType.SignalTear => 10f,
                MazeFilterEffectType.HeatRipple => 5f,
                MazeFilterEffectType.GhostEcho => 8f,
                _ => 4f,
            };
            layer.colorSteps = effect switch
            {
                MazeFilterEffectType.PaletteCrush => 6f,
                MazeFilterEffectType.PosterBands => 5f,
                MazeFilterEffectType.OrderedDither => 4f,
                _ => 6f,
            };
            layer.contrast = effect switch
            {
                MazeFilterEffectType.HalftoneShade => 1.5f,
                MazeFilterEffectType.InkClamp => 1.4f,
                MazeFilterEffectType.ChromaticSlip => 1f,
                MazeFilterEffectType.ClarityHighPass => 3f,
                _ => 1f,
            };
            layer.softness = effect switch
            {
                MazeFilterEffectType.ChromaticSlip => 0.45f,
                MazeFilterEffectType.EdgeBurn => 0.25f,
                MazeFilterEffectType.InkClamp => 0.15f,
                MazeFilterEffectType.SobelSketch => 0.3f,
                _ => 0.25f,
            };
            layer.halftoneAreaThreshold = 0.5f;
            layer.halftoneAreaSoftness = 0.5f;
            layer.highPassThreshold = 0f;
            layer.highPassSoftness = 0.02f;
            layer.highPassLumaOnly = true;
            layer.colorShift = effect switch
            {
                MazeFilterEffectType.Pixelize => 0.22f,
                MazeFilterEffectType.WatercolorBleed => 0.12f,
                _ => 0f,
            };
            layer.pixelShape = MazeFilterPixelShape.Square;
            layer.halftoneShape = MazeFilterHalftoneShape.Circle;
            layer.toneIsolation = MazeFilterToneIsolation.Darker;
            layer.slipPalette = MazeFilterSlipPalette.WarmCool;
            layer.colorMode = MazeFilterColorMode.SourceDarken;
            layer.blendMode = effect == MazeFilterEffectType.ClarityHighPass ? MazeFilterBlendMode.LinearLight : effect == MazeFilterEffectType.ColorFill ? MazeFilterBlendMode.Multiply : MazeFilterBlendMode.Overlay;
            layer.gradientMode = MazeFilterGradientMode.Solid;
            layer.primaryColor = new Color(0.02f, 0.015f, 0.04f, 1f);
            layer.secondaryColor = new Color(0.7f, 0.1f, 1f, 1f);
            layer.gradientAngleDegrees = 90f;
            layer.gradientOffset = 0f;
            layer.rotationDegrees = 0f;
            layer.angleDegrees = effect switch
            {
                MazeFilterEffectType.GhostEcho => 35f,
                MazeFilterEffectType.BlurSlope => 0f,
                _ => 0f,
            };
        }

        private static bool LayerRenders(MazeFilterLayer layer)
        {
            return layer != null && layer.enabled && layer.intensity > 0.001f && layer.sourceBlend > 0.001f;
        }
    }
}
