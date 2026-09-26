using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Maze
{
    internal static class MazeDepthSource
    {
        public const string OpaqueDepthSourceName = "cameraDepthTexture_after_opaques";

        public static bool TryGetOpaqueDepth(UniversalResourceData resourceData, out TextureHandle depth)
        {
            depth = resourceData.cameraDepthTexture;
            return depth.IsValid();
        }
    }
}
