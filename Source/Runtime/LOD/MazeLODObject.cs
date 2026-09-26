using System.Collections.Generic;
using UnityEngine;

namespace Maze
{
    [DisallowMultipleComponent]
    public sealed class MazeLODObject : MonoBehaviour
    {
        [SerializeField] private MazeLODController controller;
        [SerializeField] private MeshFilter sourceMeshFilter;
        [SerializeField] private MeshRenderer sourceRenderer;
        [SerializeField] private bool useControllerSettings = true;
        [SerializeField] private MazeLODSettings overrideSettings = default;

        [SerializeField, HideInInspector] private LODGroup lodGroup;
        [SerializeField, HideInInspector] private List<Mesh> generatedMeshes = new();
        [SerializeField, HideInInspector] private List<MeshRenderer> generatedRenderers = new();
        [SerializeField, HideInInspector] private string bakedSourceHash;
        [SerializeField, HideInInspector] private string bakedSettingsHash;
        [SerializeField, HideInInspector] private int bakedLevelCount;

        public MazeLODController Controller => controller;
        public MeshFilter SourceMeshFilter => sourceMeshFilter;
        public MeshRenderer SourceRenderer => sourceRenderer;
        public bool UseControllerSettings => useControllerSettings;
        public MazeLODSettings OverrideSettings => overrideSettings.levelCount > 0
            ? overrideSettings
            : MazeLODSettings.Default;
        public LODGroup LODGroup => lodGroup;
        public IReadOnlyList<Mesh> GeneratedMeshes => generatedMeshes;
        public IReadOnlyList<MeshRenderer> GeneratedRenderers => generatedRenderers;
        public string BakedSourceHash => bakedSourceHash;
        public string BakedSettingsHash => bakedSettingsHash;
        public int BakedLevelCount => bakedLevelCount;

        public MazeLODSettings EffectiveSettings => useControllerSettings && controller != null
            ? controller.DefaultSettings
            : OverrideSettings;

        private void Reset()
        {
            overrideSettings = MazeLODSettings.Default;
            FindSourceRenderer();
        }

        internal void Configure(
            MazeLODController owningController,
            MeshFilter meshFilter,
            MeshRenderer meshRenderer,
            LODGroup owningLodGroup,
            IReadOnlyList<Mesh> meshes,
            IReadOnlyList<MeshRenderer> renderers,
            string sourceHash,
            string settingsHash,
            int levelCount)
        {
            controller = owningController;
            sourceMeshFilter = meshFilter;
            sourceRenderer = meshRenderer;
            lodGroup = owningLodGroup;
            generatedMeshes.Clear();
            generatedRenderers.Clear();
            if (meshes != null)
            {
                generatedMeshes.AddRange(meshes);
            }

            if (renderers != null)
            {
                generatedRenderers.AddRange(renderers);
            }

            bakedSourceHash = sourceHash ?? string.Empty;
            bakedSettingsHash = settingsHash ?? string.Empty;
            bakedLevelCount = levelCount;
        }

        internal void SetController(MazeLODController owningController)
        {
            controller = owningController;
        }

        internal void EnsureDefaults()
        {
            if (overrideSettings.levelCount <= 0)
            {
                overrideSettings = MazeLODSettings.Default;
            }
        }

        internal bool FindSourceRenderer()
        {
            var renderers = GetComponentsInChildren<MeshRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (generatedRenderers.Contains(renderer))
                {
                    continue;
                }

                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                sourceRenderer = renderer;
                sourceMeshFilter = filter;
                return true;
            }

            return false;
        }
    }
}
