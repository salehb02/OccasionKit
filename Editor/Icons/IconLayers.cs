using UnityEngine;

namespace OccasionKit.EditorTools
{
    /// <summary>
    /// An icon's source layers, loaded once, and everything made from them. Adaptive icon geometry: each layer is
    /// 108 dp; launchers show the center 72 dp through their mask; art inside the center 66 dp circle is never cut.
    /// </summary>
    internal sealed class IconLayers
    {
        public const int LayerDp = 108;
        public const int ViewportDp = 72;
        public const int SafeZoneDp = 66;
        public const int LegacyDp = 48;
        public const int RecommendedSourceSize = 432;

        private readonly AppIconVariant _variant;
        private readonly RgbaImage _foreground;
        private readonly RgbaImage _background;
        private readonly RgbaImage _monochrome;
        private readonly RgbaImage _legacy;

        public IconLayers(AppIconVariant variant)
        {
            _variant = variant;
            _foreground = RgbaImage.FromTexture(variant.foreground)?.FitSquare();
            _background = RgbaImage.FromTexture(variant.background)?.CoverSquare();
            _monochrome = RgbaImage.FromTexture(variant.monochrome)?.FitSquare();
            _legacy = RgbaImage.FromTexture(variant.legacy)?.FitSquare();
        }

        public bool HasBackgroundTexture { get => _background != null; }
        public bool HasMonochrome { get => _monochrome != null; }

        public RgbaImage Foreground(int size) => _foreground != null ? _foreground.Resize(size, size) : new RgbaImage(size, size);

        public RgbaImage Background(int size)
        {
            if (_background != null)
                return _background.Resize(size, size);

            var color = _variant.backgroundColor;
            color.a = 1;
            return RgbaImage.Solid(size, color);
        }

        public RgbaImage Monochrome(int size) => _monochrome != null ? _monochrome.Resize(size, size) : new RgbaImage(size, size);

        /// <summary>Both layers, full 108 dp.</summary>
        public RgbaImage Composite(int size)
        {
            var image = Background(size);
            image.DrawOver(Foreground(size));
            return image;
        }

        /// <summary>The center 72 dp a launcher shows, before its mask.</summary>
        public RgbaImage Viewport(int size)
        {
            var layerSize = Mathf.RoundToInt(size * (float)LayerDp / ViewportDp);
            return Composite(layerSize).CropCenter(size, size);
        }

        public RgbaImage Masked(int size, MaskShape shape)
        {
            var image = Viewport(size);
            image.Mask(shape);
            return image;
        }

        public RgbaImage Legacy(int size, bool round)
        {
            var image = _legacy != null ? _legacy.Resize(size, size) : Viewport(size);

            if (round)
                image.Mask(MaskShape.Circle);

            return image;
        }
    }
}
