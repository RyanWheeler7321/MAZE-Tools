using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Maze
{
    internal sealed class MazeFocusFrameData : ContextItem
    {
        public TextureHandle CloudMask = TextureHandle.nullHandle;
        public EntityId CloudControllerEntityId;

        public override void Reset()
        {
            CloudMask = TextureHandle.nullHandle;
            CloudControllerEntityId = EntityId.None;
        }
    }
}
