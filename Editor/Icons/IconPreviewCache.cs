using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OccasionKit.EditorTools
{
    /// <summary>Masked icon previews, rebuilt when a source texture or the background color changes.</summary>
    internal static class IconPreviewCache
    {
        private const int MaxEntries = 256;

        private static readonly Dictionary<string, Texture2D> Previews = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, IconLayers> Layers = new Dictionary<string, IconLayers>();

        public static Texture2D Get(AppIconVariant variant, int size, MaskShape shape) => Get(variant, size, shape, false);

        /// <summary>The full 108 dp composite (both layers, no mask).</summary>
        public static Texture2D GetFullLayer(AppIconVariant variant, int size) => Get(variant, size, MaskShape.Square, true);

        public static void Clear()
        {
            foreach (var texture in Previews.Values)
            {
                if (texture != null)
                    Object.DestroyImmediate(texture);
            }

            Previews.Clear();
            Layers.Clear();
        }

        private static Texture2D Get(AppIconVariant variant, int size, MaskShape shape, bool fullLayer)
        {
            if (variant == null || variant.foreground == null)
                return null;

            var sourceKey = SourceKey(variant);
            var key = $"{sourceKey}#{size}#{shape}#{fullLayer}";

            if (Previews.TryGetValue(key, out var cached) && cached != null)
                return cached;

            if (Previews.Count > MaxEntries)
                Clear();

            Texture2D texture = null;

            try
            {
                if (!Layers.TryGetValue(sourceKey, out var layers))
                {
                    layers = new IconLayers(variant);
                    Layers[sourceKey] = layers;
                }

                texture = (fullLayer ? layers.Composite(size) : layers.Masked(size, shape)).ToTexture();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OccasionKit] Preview of icon \"{variant.id}\" failed: {e.Message}");
            }

            Previews[key] = texture;
            return texture;
        }

        private static string SourceKey(AppIconVariant variant) =>
            $"{Hash(variant.foreground)}|{Hash(variant.background)}|{(variant.background == null ? ColorUtility.ToHtmlStringRGB(variant.backgroundColor) : "")}";

        private static string Hash(Texture2D texture)
        {
            if (texture == null)
                return "-";

            var path = AssetDatabase.GetAssetPath(texture);
            return string.IsNullOrEmpty(path) ? texture.GetInstanceID().ToString() : AssetDatabase.GetAssetDependencyHash(path).ToString();
        }
    }
}
