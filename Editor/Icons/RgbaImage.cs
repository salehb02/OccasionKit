using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OccasionKit.EditorTools
{
    internal enum MaskShape
    {
        Square = 0,
        Circle = 1,
        Squircle = 2,
        RoundedSquare = 3,
        Teardrop = 4,
    }

    /// <summary>A CPU image with straight alpha, for building icon resources and previews. Row 0 is the bottom.</summary>
    internal sealed class RgbaImage
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Color[] Pixels;

        public RgbaImage(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Color[width * height];
        }

        private RgbaImage(int width, int height, Color[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public static RgbaImage Solid(int size, Color color)
        {
            var image = new RgbaImage(size, size);

            for (var i = 0; i < image.Pixels.Length; i++)
                image.Pixels[i] = color;

            return image;
        }

        /// <summary>Reads the source file at full resolution when it's a PNG or JPG; otherwise reads the imported texture.</summary>
        public static RgbaImage FromTexture(Texture2D texture)
        {
            if (texture == null)
                return null;

            var path = AssetDatabase.GetAssetPath(texture);
            var extension = Path.GetExtension(path).ToLowerInvariant();

            if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
            {
                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                try
                {
                    if (decoded.LoadImage(File.ReadAllBytes(path), false))
                        return new RgbaImage(decoded.width, decoded.height, decoded.GetPixels());
                }
                finally
                {
                    Object.DestroyImmediate(decoded);
                }
            }

            return ReadBack(texture);
        }

        private static RgbaImage ReadBack(Texture2D texture)
        {
            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);

            try
            {
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                copy.Apply();
                return new RgbaImage(copy.width, copy.height, copy.GetPixels());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(copy);
            }
        }

        /// <summary>Pads a non-square image with transparency so the whole image fits.</summary>
        public RgbaImage FitSquare()
        {
            if (Width == Height)
                return this;

            var size = Math.Max(Width, Height);
            var result = new RgbaImage(size, size);
            Blit(result, (size - Width) / 2, (size - Height) / 2);
            return result;
        }

        /// <summary>Crops a non-square image to its center square.</summary>
        public RgbaImage CoverSquare()
        {
            if (Width == Height)
                return this;

            var size = Math.Min(Width, Height);
            return Crop((Width - size) / 2, (Height - size) / 2, size, size);
        }

        public RgbaImage CropCenter(int width, int height) => Crop((Width - width) / 2, (Height - height) / 2, width, height);

        private RgbaImage Crop(int x0, int y0, int width, int height)
        {
            var result = new RgbaImage(width, height);

            for (var y = 0; y < height; y++)
            {
                var sy = y + y0;

                if (sy < 0 || sy >= Height)
                    continue;

                for (var x = 0; x < width; x++)
                {
                    var sx = x + x0;

                    if (sx >= 0 && sx < Width)
                        result.Pixels[y * width + x] = Pixels[sy * Width + sx];
                }
            }

            return result;
        }

        private void Blit(RgbaImage target, int x0, int y0)
        {
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                    target.Pixels[(y + y0) * target.Width + x + x0] = Pixels[y * Width + x];
            }
        }

        /// <summary>Area-averaged downscale (bilinear upscale) in premultiplied alpha, so edges don't get dark halos.</summary>
        public RgbaImage Resize(int width, int height)
        {
            if (width == Width && height == Height)
                return new RgbaImage(width, height, (Color[])Pixels.Clone());

            var source = new Vector4[Pixels.Length];

            for (var i = 0; i < Pixels.Length; i++)
            {
                var c = Pixels[i];
                source[i] = new Vector4(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
            }

            var horizontal = new Vector4[width * Height];

            for (var y = 0; y < Height; y++)
                Resample(source, Width, y * Width, 1, horizontal, width, y * width, 1);

            var vertical = new Vector4[width * height];

            for (var x = 0; x < width; x++)
                Resample(horizontal, Height, x, width, vertical, height, x, width);

            var result = new RgbaImage(width, height);

            for (var i = 0; i < vertical.Length; i++)
            {
                var v = vertical[i];
                result.Pixels[i] = v.w > 1e-5f ? new Color(v.x / v.w, v.y / v.w, v.z / v.w, Mathf.Clamp01(v.w)) : Color.clear;
            }

            return result;
        }

        private static void Resample(Vector4[] src, int srcLength, int srcOffset, int srcStride, Vector4[] dst, int dstLength, int dstOffset, int dstStride)
        {
            var scale = (float)srcLength / dstLength;

            for (var i = 0; i < dstLength; i++)
            {
                Vector4 value;

                if (dstLength <= srcLength)
                {
                    var start = i * scale;
                    var end = start + scale;
                    var last = Math.Min(srcLength, (int)Math.Ceiling(end));
                    value = Vector4.zero;

                    for (var s = (int)start; s < last; s++)
                    {
                        var weight = Math.Min(end, s + 1) - Math.Max(start, s);

                        if (weight > 0)
                            value += src[srcOffset + s * srcStride] * weight;
                    }

                    value /= scale;
                }
                else
                {
                    var position = (i + 0.5f) * scale - 0.5f;
                    var s0 = Mathf.Clamp(Mathf.FloorToInt(position), 0, srcLength - 1);
                    var s1 = Math.Min(s0 + 1, srcLength - 1);
                    value = Vector4.Lerp(src[srcOffset + s0 * srcStride], src[srcOffset + s1 * srcStride], Mathf.Clamp01(position - s0));
                }

                dst[dstOffset + i * dstStride] = value;
            }
        }

        /// <summary>Alpha-blends <paramref name="top"/> (same size) over this image.</summary>
        public void DrawOver(RgbaImage top)
        {
            if (top.Width != Width || top.Height != Height)
                throw new ArgumentException("Layers must be the same size.");

            for (var i = 0; i < Pixels.Length; i++)
            {
                var s = top.Pixels[i];
                var d = Pixels[i];
                var a = s.a + d.a * (1 - s.a);

                Pixels[i] = a <= 1e-5f
                    ? Color.clear
                    : new Color(
                        (s.r * s.a + d.r * d.a * (1 - s.a)) / a,
                        (s.g * s.a + d.g * d.a * (1 - s.a)) / a,
                        (s.b * s.a + d.b * d.a * (1 - s.a)) / a,
                        a);
            }
        }

        /// <summary>Cuts the image to a launcher mask shape with anti-aliased edges.</summary>
        public void Mask(MaskShape shape)
        {
            if (shape == MaskShape.Square)
                return;

            const int samples = 4;

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var inside = 0;

                    for (var sy = 0; sy < samples; sy++)
                    {
                        for (var sx = 0; sx < samples; sx++)
                        {
                            var u = (x + (sx + 0.5f) / samples) / Width * 2 - 1;
                            var v = (y + (sy + 0.5f) / samples) / Height * 2 - 1;

                            if (IsInside(shape, u, v))
                                inside++;
                        }
                    }

                    var index = y * Width + x;
                    var c = Pixels[index];
                    c.a *= inside / (float)(samples * samples);
                    Pixels[index] = c;
                }
            }
        }

        /// <param name="u">-1 (left) to 1 (right).</param>
        /// <param name="v">-1 (bottom) to 1 (top).</param>
        public static bool IsInside(MaskShape shape, float u, float v)
        {
            switch (shape)
            {
                case MaskShape.Circle:
                    return u * u + v * v <= 1;
                case MaskShape.Squircle:
                    return u * u * u * u + v * v * v * v <= 1;
                case MaskShape.RoundedSquare:
                    return InsideRounded(u, v, 0.36f);
                case MaskShape.Teardrop:
                    return InsideRounded(u, v, u > 0 && v < 0 ? 0.3f : 1f);
                default:
                    return true;
            }
        }

        private static bool InsideRounded(float u, float v, float radius)
        {
            var ax = Math.Abs(u);
            var ay = Math.Abs(v);

            if (ax > 1 || ay > 1)
                return false;

            var corner = 1 - radius;

            if (ax <= corner || ay <= corner)
                return true;

            var dx = ax - corner;
            var dy = ay - corner;
            return dx * dx + dy * dy <= radius * radius;
        }

        /// <param name="alpha">False writes an RGB file, for icons that must be opaque (iOS).</param>
        public byte[] EncodePng(bool alpha = true)
        {
            var texture = new Texture2D(Width, Height, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);

            try
            {
                texture.SetPixels(Pixels);
                texture.Apply(false);
                return texture.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        public Texture2D ToTexture()
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            texture.SetPixels(Pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
