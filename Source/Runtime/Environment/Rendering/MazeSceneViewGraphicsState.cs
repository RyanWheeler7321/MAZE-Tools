namespace Maze
{
    public static class MazeSceneViewGraphicsState
    {
        public static bool Fog { get; set; } = true;
        public static bool LightFX { get; set; } = true;
        public static bool Focus { get; set; } = true;
        public static bool AO { get; set; } = true;

        public static bool SkyClouds { get; set; } = true;
        public static bool DistanceFog { get; set; } = true;
        public static bool VolumetricFog { get; set; } = true;
        public static bool LightShafts { get; set; } = true;

        public static bool ShouldRenderFog(bool isSceneViewCamera)
        {
#if UNITY_EDITOR
            return !isSceneViewCamera || Fog;
#else
            return true;
#endif
        }

        public static bool ShouldRenderFocus(bool isSceneViewCamera)
        {
#if UNITY_EDITOR
            return !isSceneViewCamera || Focus;
#else
            return true;
#endif
        }

        public static bool ShouldRenderLightFX(bool isSceneViewCamera)
        {
#if UNITY_EDITOR
            return !isSceneViewCamera || LightFX;
#else
            return true;
#endif
        }

        public static bool ShouldRenderAO(bool isSceneViewCamera)
        {
#if UNITY_EDITOR
            return !isSceneViewCamera || AO;
#else
            return true;
#endif
        }

        public static bool ShouldRenderSkyClouds(bool isSceneViewCamera)
        {
#if UNITY_EDITOR
            return !isSceneViewCamera || SkyClouds;
#else
            return true;
#endif
        }

        public static bool ShouldRenderDistanceFog(bool isSceneViewCamera)
        {
#if UNITY_EDITOR
            return !isSceneViewCamera || DistanceFog;
#else
            return true;
#endif
        }

        public static bool ShouldRenderVolumetricFog(bool isSceneViewCamera)
        {
#if UNITY_EDITOR
            return !isSceneViewCamera || VolumetricFog;
#else
            return true;
#endif
        }

        public static bool ShouldRenderLightShafts(bool isSceneViewCamera)
        {
#if UNITY_EDITOR
            return !isSceneViewCamera || LightShafts;
#else
            return true;
#endif
        }
    }
}
