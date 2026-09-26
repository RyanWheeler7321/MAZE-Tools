using System.Collections.Generic;
using UnityEngine;

namespace Maze
{
    public static class MazeVectorControlIconAssets
    {
        private const string ResourceRoot = "MazeVector/Controls/";
        private const string RegistryPath = "MazeVector/Controls/ControlIconRegistry";

        private static readonly Dictionary<string, MazeVectorAsset> Cache = new();
        private static Dictionary<string, string> semanticAssetNames;

        public static bool TryGetSemantic(string semanticKey, out MazeVectorAsset asset)
        {
            var normalized = MazeControlGlyphResolver.Normalize(semanticKey);
            var assetNames = SemanticAssetNames();
            assetNames.TryGetValue(normalized, out var assetName);
            return TryLoad(assetName, out asset);
        }

        public static bool TryGetKey(string keyText, out MazeVectorAsset asset)
        {
            return TryGetSemantic(keyText, out asset);
        }

        public static bool TryGetMouse(bool right, out MazeVectorAsset asset)
        {
            return TryGetSemantic(right ? "mouse_right" : "mouse_left", out asset);
        }

        public static bool TryGetDPad(int direction, out MazeVectorAsset asset)
        {
            var semanticKey = direction switch
            {
                1 => "dpad_right",
                2 => "dpad_down",
                3 => "dpad_left",
                4 => "dpad_up",
                _ => "dpad_neutral"
            };
            return TryGetSemantic(semanticKey, out asset);
        }

        public static bool TryGetGamepadButton(string text, out MazeVectorAsset asset)
        {
            var semanticKey = MazeControlGlyphResolver.Normalize(text) switch
            {
                "y" or "north" => "gamepad_north",
                "b" or "east" => "gamepad_east",
                "a" or "south" => "gamepad_south",
                "x" or "west" => "gamepad_west",
                "menu" or "start" => "gamepad_menu",
                "back" or "select" => "gamepad_back",
                _ => "gamepad_neutral"
            };
            return TryGetSemantic(semanticKey, out asset);
        }

        public static void ReloadRegistry()
        {
            semanticAssetNames = null;
            Cache.Clear();
        }

        private static Dictionary<string, string> SemanticAssetNames()
        {
            if (semanticAssetNames != null)
            {
                return semanticAssetNames;
            }
            semanticAssetNames = new Dictionary<string, string>();
            var registry = Resources.Load<MazeVectorControlIconRegistry>(RegistryPath);
            var entries = registry != null ? registry.Entries : null;
            for (var i = 0; entries != null && i < entries.Length; i++)
            {
                var key = MazeControlGlyphResolver.Normalize(entries[i].key);
                var assetName = entries[i].assetName;
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(assetName))
                {
                    continue;
                }
                semanticAssetNames[key] = assetName;
            }
            return semanticAssetNames;
        }

        private static bool TryLoad(string assetName, out MazeVectorAsset asset)
        {
            asset = null;
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return false;
            }
            if (Cache.TryGetValue(assetName, out asset))
            {
                return asset != null;
            }
            asset = Resources.Load<MazeVectorAsset>(ResourceRoot + assetName);
            Cache[assetName] = asset;
            return asset != null;
        }
    }
}
