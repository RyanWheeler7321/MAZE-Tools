using UnityEngine;
using UnityEngine.UI;

namespace Maze
{
    public static class MazeVectorCanvasUtility
    {
        public static RenderMode ConfigureCanvas(Canvas canvas, MazeVectorRenderMode vectorMode, Camera camera, int sortingOrder, float planeDistance = 2f)
        {
            if (canvas == null)
            {
                return RenderMode.ScreenSpaceOverlay;
            }

            var wantsCamera = vectorMode == MazeVectorRenderMode.CameraUI;

            if (vectorMode == MazeVectorRenderMode.World)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
            }
            else if (wantsCamera && camera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = Mathf.Max(0.01f, planeDistance);
                camera.enabled = true;
            }
            else
            {
                if (wantsCamera)
                {
                    MazeDiagnosticsLog.WarnOnce("maze.vector.canvas.camera_missing:" + canvas.GetEntityId(), "Maze.Vector", "camera_missing", "vector canvas requested camera rendering but no camera was available; falling back to overlay", MazeDiagnosticsLog.JoinData(
                        MazeDiagnosticsLog.JsonString("canvas", canvas.name),
                        MazeDiagnosticsLog.JsonString("mode", vectorMode.ToString())));
                }
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }

            canvas.sortingOrder = sortingOrder;
            return canvas.renderMode;
        }

        public static void EnsureScreenCanvasBasics(Canvas canvas, Vector2 referenceResolution)
        {
            if (canvas == null)
            {
                return;
            }

            var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        public static void EnsurePaintChannels(Graphic graphic)
        {
            var targetCanvas = graphic != null ? graphic.canvas : null;
            if (targetCanvas != null)
            {
                targetCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            }
        }

        public static void EnsureOutputIntensityChannels(Graphic graphic)
        {
            var targetCanvas = graphic != null ? graphic.canvas : null;
            if (targetCanvas != null)
            {
                targetCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord2;
            }
        }
    }
}
