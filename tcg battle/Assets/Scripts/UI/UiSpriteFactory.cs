using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TcgBattle.UI
{
    /// <summary>
    /// Generates the generic UI shapes we need (rounded rects, rings, diamonds, soft shadows)
    /// as sprites at runtime, so the prototype needs no placeholder PNGs. All shapes are
    /// single-tinted whites; callers color them.
    /// </summary>
    public static class UiSpriteFactory
    {
        private static Sprite _rounded;
        private static Sprite _circle;
        private static Sprite _softShadow;
        private static Sprite _softGlow;
        private static Sprite _fadeDown;
        private static Sprite _fadeUp;
        private static Sprite _roundOutline;
        private static Sprite _ring;
        private static Sprite _diamond;

        /// <summary>Filled rounded rect with 24px corners (slicable at any size).</summary>
        public static Sprite Rounded
        {
            get
            {
                if (_rounded == null)
                    _rounded = CreateRounded(64, 24);
                return _rounded;
            }
        }

        /// <summary>Rounded-rect OUTLINE with a 5px stroke. Sliced: stroke stays crisp.</summary>
        public static Sprite RoundOutline
        {
            get
            {
                if (_roundOutline == null)
                    _roundOutline = CreateRoundedOutline(64, 24, 5f);
                return _roundOutline;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (_circle == null)
                    _circle = CreateCircle(64);
                return _circle;
            }
        }

        /// <summary>Thin circle outline stroke used as an ornamental ring.</summary>
        public static Sprite Ring
        {
            get
            {
                if (_ring == null)
                    _ring = CreateRing(96, 4f);
                return _ring;
            }
        }

        /// <summary>Filled diamond (rotated square) used for score markers / ornaments.</summary>
        public static Sprite Diamond
        {
            get
            {
                if (_diamond == null)
                    _diamond = CreateDiamond(64);
                return _diamond;
            }
        }

        /// <summary>Radial dark blob used behind cards as a cheap drop shadow.</summary>
        public static Sprite SoftShadow
        {
            get
            {
                if (_softShadow == null)
                    _softShadow = CreateSoftCircle(64);
                return _softShadow;
            }
        }

        /// <summary>
        /// White radial falloff, tintable for stage pools, divider glows and other
        /// low-contrast atmosphere. Unlike SoftShadow (black RGB), this keeps the
        /// caller's hue.
        /// </summary>
        public static Sprite SoftGlow
        {
            get
            {
                if (_softGlow == null)
                    _softGlow = CreateSoftGlow(64);
                return _softGlow;
            }
        }

        /// <summary>Vertical white fade: opaque at the top, transparent at the bottom.</summary>
        public static Sprite FadeDown
        {
            get
            {
                if (_fadeDown == null)
                    _fadeDown = CreateVerticalFade(128, true);
                return _fadeDown;
            }
        }

        /// <summary>Vertical white fade: transparent at the top, opaque at the bottom.</summary>
        public static Sprite FadeUp
        {
            get
            {
                if (_fadeUp == null)
                    _fadeUp = CreateVerticalFade(128, false);
                return _fadeUp;
            }
        }

        private static Sprite CreateRounded(int size, int corner)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float radius = corner;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x, radius, size - radius);
                    float cy = Mathf.Clamp(y, radius, size - radius);
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float a = Mathf.Clamp01((radius - d) / 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(corner, corner, corner, corner));
        }

        /// <summary>Sliceable rounded-rect outline. The stroke lives inside the sprite's
        /// border region so stretching keeps the stroke width constant.</summary>
        private static Sprite CreateRoundedOutline(int size, int corner, float stroke)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float half = size * 0.5f;
            float radius = corner;
            float edge = half - radius;         // distance from centre to the straight-edge region
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Signed distance to the outer rounded-rect edge. The earlier version
                    // compared two shapes that shared the same straight edges, so the
                    // difference cancelled and produced an entirely transparent texture.
                    float px = Mathf.Abs(x + 0.5f - half);
                    float py = Mathf.Abs(y + 0.5f - half);
                    float qx = px - edge;
                    float qy = py - edge;
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f)
                                             + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
                    float dist = outside + inside - radius; // negative inside the shape
                    float a = Mathf.Clamp01(-dist / 1.25f)
                            * Mathf.Clamp01((dist + stroke) / 1.25f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            int b = Mathf.RoundToInt(corner);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static float RoundedAlpha(float x, float y, int size, float radius)
        {
            if (radius <= 0f)
                return Mathf.Clamp01(Mathf.Min(x, y) * 0.5f) * Mathf.Clamp01(Mathf.Min(size - x, size - y) * 0.5f);
            float cx = Mathf.Clamp(x, radius, size - radius);
            float cy = Mathf.Clamp(y, radius, size - radius);
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01((radius - d) / 2f);
        }

        private static Sprite CreateCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half + 0.5f) * (x - half + 0.5f)
                                       + (y - half + 0.5f) * (y - half + 0.5f));
                    float a = Mathf.Clamp01((half - d) / 1.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Sliceable circle outline (constant stroke on a 96px sprite; corners rounded).</summary>
        private static Sprite CreateRing(int size, float stroke)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half + 0.5f) * (x - half + 0.5f)
                                       + (y - half + 0.5f) * (y - half + 0.5f));
                    float a = Mathf.Clamp01((stroke - Mathf.Abs(d - (half - stroke)) ) / 1.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            int b = Mathf.CeilToInt(stroke) + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static Sprite CreateDiamond(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float half = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01((half - (Mathf.Abs(x - half + 0.5f) + Mathf.Abs(y - half + 0.5f))) / 1.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateSoftCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, a * 0.85f));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateSoftGlow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateVerticalFade(int height, bool opaqueTop)
        {
            var tex = new Texture2D(2, height, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < height; y++)
            {
                float t = height > 1 ? (float)y / (height - 1) : 1f;
                float a = Mathf.SmoothStep(0f, 1f, opaqueTop ? t : 1f - t);
                for (int x = 0; x < 2; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);
        }
    }
}
