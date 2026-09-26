#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Maze.Editor
{
    internal enum MazeTextureCompareMode
    {
        SideBySide,
        Wipe,
        Difference
    }

    internal enum MazeTextureChannelMode
    {
        RGB,
        Red,
        Green,
        Blue,
        Alpha,
        Luminance
    }

    internal enum MazeTextureViewMode
    {
        Single,
        Tiles3x3,
        Seams
    }

    [Serializable]
    internal sealed class MazeTextureModifySettings
    {
        public bool tileMode = true;
        public float exposure;
        public float lightness;
        public float contrast;
        public float saturation;
        public float vibrance;
        public float temperature;
        public float tint;
        public float hue;
        public float shadows;
        public float highlights;
        public float blackPoint;
        public float whitePoint;
        public float blur;
        public float sharpen;
        public float sharpenRadius = 1f;
        public float clarity;
        public float fade;
        public float posterize;
        public bool invert;

        public bool IsNeutral =>
            ApproximatelyZero(exposure) && ApproximatelyZero(lightness) && ApproximatelyZero(contrast) &&
            ApproximatelyZero(saturation) && ApproximatelyZero(vibrance) && ApproximatelyZero(temperature) &&
            ApproximatelyZero(tint) && ApproximatelyZero(hue) && ApproximatelyZero(shadows) &&
            ApproximatelyZero(highlights) && ApproximatelyZero(blackPoint) && ApproximatelyZero(whitePoint) &&
            ApproximatelyZero(blur) && ApproximatelyZero(sharpen) && ApproximatelyZero(clarity) &&
            ApproximatelyZero(fade) && ApproximatelyZero(posterize) && !invert;

        public void Reset()
        {
            exposure = 0f;
            lightness = 0f;
            contrast = 0f;
            saturation = 0f;
            vibrance = 0f;
            temperature = 0f;
            tint = 0f;
            hue = 0f;
            shadows = 0f;
            highlights = 0f;
            blackPoint = 0f;
            whitePoint = 0f;
            blur = 0f;
            sharpen = 0f;
            sharpenRadius = 1f;
            clarity = 0f;
            fade = 0f;
            posterize = 0f;
            invert = false;
        }

        public string ToCompactString()
        {
            return $"tile={tileMode},exp={exposure:0.###},light={lightness:0.###},contrast={contrast:0.###},sat={saturation:0.###},vibrance={vibrance:0.###},temp={temperature:0.###},tint={tint:0.###},hue={hue:0.###},shadows={shadows:0.###},highlights={highlights:0.###},black={blackPoint:0.###},white={whitePoint:0.###},blur={blur:0.###},sharpen={sharpen:0.###},radius={sharpenRadius:0.###},clarity={clarity:0.###},fade={fade:0.###},posterize={posterize:0.###},invert={invert}";
        }

        private static bool ApproximatelyZero(float value) => Mathf.Abs(value) < 0.0001f;
    }

    internal sealed class MazeTextureModifyProcessor : IDisposable
    {
        private const string ShaderFileName = "MazeTextureModify.shader";
        private const string ShaderName = "Hidden/MAZE/TextureModify";
        private const int PreviewMaxDimension = 1024;

        private Material material;
        private Texture2D sourceTexture;
        private RenderTexture previewTexture;
        private RenderTexture currentViewTexture;
        private RenderTexture previewViewTexture;
        private RenderTexture compareViewTexture;
        private bool sourceIsSrgb;
        private float medianLightness = 0.5f;

        internal Texture2D SourceTexture => sourceTexture;
        internal RenderTexture PreviewTexture => previewTexture;
        internal int Width => sourceTexture != null ? sourceTexture.width : 0;
        internal int Height => sourceTexture != null ? sourceTexture.height : 0;
        internal float MedianLightness => medianLightness;

        internal bool Load(string assetPath, bool isSrgb, out string error)
        {
            error = string.Empty;
            DisposeTextures();
            EnsureMaterial();
            if (material == null)
            {
                error = "The MAZE texture processing shader could not be loaded.";
                return false;
            }

            try
            {
                var fullPath = MazeTextureModifyHistory.AssetPathToFullPath(assetPath);
                var bytes = File.ReadAllBytes(fullPath);
                sourceIsSrgb = isSrgb;
                sourceTexture = Decode(bytes, isSrgb);
                if (sourceTexture == null)
                {
                    error = "Unity could not decode this texture source.";
                    return false;
                }

                sourceTexture.name = Path.GetFileNameWithoutExtension(assetPath) + " (MAZE Source)";
                sourceTexture.wrapMode = TextureWrapMode.Repeat;
                sourceTexture.filterMode = FilterMode.Bilinear;
                medianLightness = CalculateMedianLightness(sourceTexture, isSrgb);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        internal RenderTexture RenderPreview(MazeTextureModifySettings settings)
        {
            if (sourceTexture == null)
            {
                return null;
            }

            var scale = Mathf.Min(1f, PreviewMaxDimension / (float)Mathf.Max(sourceTexture.width, sourceTexture.height));
            var width = Mathf.Max(1, Mathf.RoundToInt(sourceTexture.width * scale));
            var height = Mathf.Max(1, Mathf.RoundToInt(sourceTexture.height * scale));
            EnsurePersistentTexture(ref previewTexture, width, height, "MAZE Texture Preview");
            RenderPipeline(sourceTexture, previewTexture, settings);
            return previewTexture;
        }

        internal void RenderViews(MazeTextureChannelMode channel, MazeTextureCompareMode compareMode, float wipe, MazeTextureBackgroundMode background, Texture currentOverride = null)
        {
            if (sourceTexture == null || previewTexture == null)
            {
                return;
            }

            var previousActive = RenderTexture.active;
            try
            {
                var width = previewTexture.width;
                var height = previewTexture.height;
                EnsurePersistentTexture(ref currentViewTexture, width, height, "MAZE Current View");
                EnsurePersistentTexture(ref previewViewTexture, width, height, "MAZE Preview View");

                SetCommonMaterialValues(null);
                material.SetFloat("_Channel", (float)channel);
                material.SetFloat("_Background", (float)background);
                BlitLinearToSrgb(currentOverride != null ? currentOverride : sourceTexture, currentViewTexture, material, 3);
                BlitLinearToSrgb(previewTexture, previewViewTexture, material, 3);

                if (compareMode != MazeTextureCompareMode.SideBySide)
                {
                    EnsurePersistentTexture(ref compareViewTexture, width, height, "MAZE Compare View");
                    material.SetTexture("_CompareTex", previewViewTexture);
                    material.SetFloat("_Wipe", wipe);
                    BlitLinearToSrgb(currentViewTexture, compareViewTexture, material, compareMode == MazeTextureCompareMode.Difference ? 4 : 5);
                }
            }
            finally
            {
                RenderTexture.active = previousActive;
            }
        }

        internal Texture CurrentView => currentViewTexture != null ? currentViewTexture : sourceTexture;
        internal Texture PreviewView => previewViewTexture != null ? previewViewTexture : previewTexture;
        internal Texture CompareView => compareViewTexture;

        internal byte[] EncodeFullResolution(MazeTextureModifySettings settings, string extension)
        {
            if (sourceTexture == null)
            {
                throw new InvalidOperationException("No texture is loaded.");
            }

            var output = CreatePersistentTexture(sourceTexture.width, sourceTexture.height, "MAZE Full Texture Output");
            Texture2D readable = null;
            var previousActive = RenderTexture.active;
            try
            {
                RenderPipeline(sourceTexture, output, settings);
                RenderTexture.active = output;
                readable = new Texture2D(output.width, output.height, TextureFormat.RGBA32, false, false)
                {
                    name = "MAZE Texture Encode Buffer"
                };
                readable.ReadPixels(new Rect(0f, 0f, output.width, output.height), 0, 0, false);
                readable.Apply(false, false);
                return extension.ToLowerInvariant() switch
                {
                    ".png" => readable.EncodeToPNG(),
                    ".jpg" or ".jpeg" => readable.EncodeToJPG(100),
                    _ => throw new NotSupportedException($"The {extension} source format is not supported by MODIFY.")
                };
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (readable != null)
                {
                    UnityEngine.Object.DestroyImmediate(readable);
                }
                ReleaseRenderTexture(ref output);
            }
        }

        internal static Texture2D Decode(byte[] bytes, bool isSrgb)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, !isSrgb)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            if (!ImageConversion.LoadImage(texture, bytes, false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }
            texture.wrapMode = TextureWrapMode.Repeat;
            return texture;
        }

        internal static string ComputeSha256(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return string.Concat(hash.ComputeHash(bytes).Select(value => value.ToString("x2")));
        }

        public void Dispose()
        {
            DisposeTextures();
            if (material != null)
            {
                UnityEngine.Object.DestroyImmediate(material);
                material = null;
            }
        }

        private void EnsureMaterial()
        {
            if (material != null)
            {
                return;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                foreach (var guid in AssetDatabase.FindAssets("MazeTextureModify t:Shader"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.Equals(Path.GetFileName(path), ShaderFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                        break;
                    }
                }
            }
            if (shader != null)
            {
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
        }

        private void RenderPipeline(Texture source, RenderTexture destination, MazeTextureModifySettings settings)
        {
            EnsureMaterial();
            if (material == null)
            {
                throw new InvalidOperationException("The MAZE texture processing shader is unavailable.");
            }

            var previousActive = RenderTexture.active;
            var width = destination.width;
            var height = destination.height;
            RenderTexture color = null;
            RenderTexture working = null;
            try
            {
                color = GetTemporaryLinear(width, height);
                SetCommonMaterialValues(settings);
                Graphics.Blit(source, color, material, 0);
                working = color;

                if (settings.blur > 0.001f)
                {
                    working = RunBlur(working, settings.blur * 24f, settings.tileMode, width, height, working == color ? null : working);
                }

                if (settings.sharpen > 0.001f)
                {
                    working = RunDetail(working, settings.sharpenRadius, settings.sharpen * 2f, settings.tileMode, width, height, working == color ? null : working);
                }

                if (settings.clarity > 0.001f)
                {
                    working = RunDetail(working, 4f + settings.clarity * 16f, settings.clarity * 1.5f, settings.tileMode, width, height, working == color ? null : working);
                }

                BlitLinearToSrgb(working, destination, null, -1);
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (working != null && working != color)
                {
                    RenderTexture.ReleaseTemporary(working);
                }
                if (color != null)
                {
                    RenderTexture.ReleaseTemporary(color);
                }
            }
        }

        private RenderTexture RunBlur(Texture source, float radius, bool tileMode, int width, int height, RenderTexture releaseAfter)
        {
            var horizontal = GetTemporaryLinear(width, height);
            var vertical = GetTemporaryLinear(width, height);
            SetTileMode(tileMode);
            material.SetFloat("_Radius", Mathf.Max(0f, radius));
            material.SetVector("_Direction", Vector2.right);
            Graphics.Blit(source, horizontal, material, 1);
            material.SetVector("_Direction", Vector2.up);
            Graphics.Blit(horizontal, vertical, material, 1);
            RenderTexture.ReleaseTemporary(horizontal);
            if (releaseAfter != null)
            {
                RenderTexture.ReleaseTemporary(releaseAfter);
            }
            return vertical;
        }

        private RenderTexture RunDetail(Texture source, float radius, float amount, bool tileMode, int width, int height, RenderTexture releaseAfter)
        {
            var blurred = RunBlur(source, radius, tileMode, width, height, null);
            var result = GetTemporaryLinear(width, height);
            SetTileMode(tileMode);
            material.SetTexture("_BlurTex", blurred);
            material.SetFloat("_Amount", amount);
            Graphics.Blit(source, result, material, 2);
            RenderTexture.ReleaseTemporary(blurred);
            if (releaseAfter != null)
            {
                RenderTexture.ReleaseTemporary(releaseAfter);
            }
            return result;
        }

        private void SetCommonMaterialValues(MazeTextureModifySettings settings)
        {
            if (settings == null)
            {
                material.SetFloat("_TileMode", 1f);
                return;
            }

            SetTileMode(settings.tileMode);
            material.SetFloat("_Neutral", settings.IsNeutral ? 1f : 0f);
            material.SetFloat("_MedianLightness", medianLightness);
            material.SetFloat("_Exposure", settings.exposure);
            material.SetFloat("_Lightness", settings.lightness);
            material.SetFloat("_Contrast", settings.contrast);
            material.SetFloat("_Saturation", settings.saturation);
            material.SetFloat("_Vibrance", settings.vibrance);
            material.SetFloat("_Temperature", settings.temperature);
            material.SetFloat("_Tint", settings.tint);
            material.SetFloat("_Hue", settings.hue);
            material.SetFloat("_Shadows", settings.shadows);
            material.SetFloat("_Highlights", settings.highlights);
            material.SetFloat("_BlackPoint", settings.blackPoint);
            material.SetFloat("_WhitePoint", settings.whitePoint);
            material.SetFloat("_Fade", settings.fade);
            material.SetFloat("_Posterize", settings.posterize);
            material.SetFloat("_Invert", settings.invert ? 1f : 0f);
        }

        private void SetTileMode(bool enabled)
        {
            material.SetFloat("_TileMode", enabled ? 1f : 0f);
        }

        private static RenderTexture GetTemporaryLinear(int width, int height)
        {
            var texture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        private static void EnsurePersistentTexture(ref RenderTexture texture, int width, int height, string name)
        {
            if (texture != null && texture.width == width && texture.height == height && texture.IsCreated())
            {
                return;
            }

            ReleaseRenderTexture(ref texture);
            texture = CreatePersistentTexture(width, height, name);
        }

        private static RenderTexture CreatePersistentTexture(int width, int height, string name)
        {
            var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false
            };
            texture.Create();
            return texture;
        }

        private static void BlitLinearToSrgb(Texture source, RenderTexture destination, Material blitMaterial, int pass)
        {
            var previous = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = true;
                if (blitMaterial == null)
                {
                    Graphics.Blit(source, destination);
                }
                else
                {
                    Graphics.Blit(source, destination, blitMaterial, pass);
                }
            }
            finally
            {
                GL.sRGBWrite = previous;
            }
        }

        private static float CalculateMedianLightness(Texture2D texture, bool isSrgb)
        {
            const int sampleAxis = 32;
            var samples = new float[sampleAxis * sampleAxis];
            var index = 0;
            for (var y = 0; y < sampleAxis; y++)
            {
                for (var x = 0; x < sampleAxis; x++)
                {
                    var color = texture.GetPixelBilinear((x + 0.5f) / sampleAxis, (y + 0.5f) / sampleAxis);
                    var linear = isSrgb ? color.linear : color;
                    samples[index++] = LinearToOklabLightness(new Vector3(linear.r, linear.g, linear.b));
                }
            }
            Array.Sort(samples);
            return Mathf.Clamp(samples[samples.Length / 2], 0.08f, 0.92f);
        }

        private static float LinearToOklabLightness(Vector3 color)
        {
            var l = 0.4122214708f * color.x + 0.5363325363f * color.y + 0.0514459929f * color.z;
            var m = 0.2119034982f * color.x + 0.6806995451f * color.y + 0.1073969566f * color.z;
            var s = 0.0883024619f * color.x + 0.2817188376f * color.y + 0.6299787005f * color.z;
            return 0.2104542553f * SignedCubeRoot(l) + 0.793617785f * SignedCubeRoot(m) - 0.0040720468f * SignedCubeRoot(s);
        }

        private static float SignedCubeRoot(float value)
        {
            return Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), 1f / 3f);
        }

        private void DisposeTextures()
        {
            if (sourceTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(sourceTexture);
                sourceTexture = null;
            }
            ReleaseRenderTexture(ref previewTexture);
            ReleaseRenderTexture(ref currentViewTexture);
            ReleaseRenderTexture(ref previewViewTexture);
            ReleaseRenderTexture(ref compareViewTexture);
        }

        private static void ReleaseRenderTexture(ref RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }
            if (RenderTexture.active == texture)
            {
                RenderTexture.active = null;
            }
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            texture = null;
        }
    }
}
#endif
