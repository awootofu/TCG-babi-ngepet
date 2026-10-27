using UnityEngine;
using UnityEngine.UI;

namespace TcgBattle.UI
{
    /// <summary>
    /// Shared UI-only card back used by the opponent hand, both deck piles and draw
    /// ghosts. The design is a restrained batik/kawung medallion in the project's ink,
    /// gold and ivory palette. It is deliberately generic: no card identity, name,
    /// stats or element, so hidden information can never leak through it.
    /// </summary>
    public static class CardBackVisual
    {
        public static RectTransform Create(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;

            float s = size.x / 123f;
            float w = size.x;
            float h = size.y;

            var shadow = MakeImage(rt, "Shadow", UiSpriteFactory.SoftShadow,
                UiStyle.WithAlpha(Color.black, 0.48f));
            Stretch(shadow.rectTransform, -8f, -8f, -8f, -8f);

            // Deep ink base, a slightly lifted field and a violet glow read as a
            // lacquered "wayang" card back rather than a flat placeholder rectangle.
            var backing = MakeImage(rt, "Backing", UiSpriteFactory.Rounded, UiStyle.PanelInset);
            backing.type = Image.Type.Sliced;
            Stretch(backing.rectTransform, 0f, 0f, 0f, 0f);

            var field = MakeImage(rt, "Field", UiSpriteFactory.Rounded, UiStyle.CardBacking);
            field.type = Image.Type.Sliced;
            Stretch(field.rectTransform, 3f, 3f, 3f, 3f);

            var violetGlow = MakeImage(rt, "VioletGlow", UiSpriteFactory.SoftGlow,
                UiStyle.WithAlpha(UiStyle.Dark, 0.10f));
            Center(violetGlow.rectTransform, new Vector2(w * 0.86f, h * 0.72f),
                new Vector2(0f, h * 0.08f));

            var goldGlow = MakeImage(rt, "GoldGlow", UiSpriteFactory.SoftGlow,
                UiStyle.WithAlpha(UiStyle.Gold, 0.08f));
            Center(goldGlow.rectTransform, new Vector2(w * 0.72f, w * 0.72f), Vector2.zero);

            var frame = MakeImage(rt, "Frame", UiSpriteFactory.RoundOutline,
                UiStyle.WithAlpha(UiStyle.Gold, 0.58f));
            frame.type = Image.Type.Sliced;
            Stretch(frame.rectTransform, 1.5f, 1.5f, 1.5f, 1.5f);

            var inner = MakeImage(rt, "InnerLine", UiSpriteFactory.RoundOutline,
                UiStyle.WithAlpha(UiStyle.Ivory, 0.12f));
            inner.type = Image.Type.Sliced;
            Stretch(inner.rectTransform, 6f, 6f, 6f, 6f);

            // Thin vertical spine, echoed by the batik diamond bands above and below.
            var spine = MakeImage(rt, "Spine", null, UiStyle.WithAlpha(UiStyle.Gold, 0.14f));
            Center(spine.rectTransform, new Vector2(1.5f * s, h * 0.62f), Vector2.zero);

            AddBatikBand(rt, size, s, 1f);
            AddBatikBand(rt, size, s, -1f);

            // Central kawung medallion: circular gold halo, layered diamonds and a
            // small four-lobed motif. All shapes are generic ornaments.
            var halo = MakeImage(rt, "MedallionGlow", UiSpriteFactory.SoftGlow,
                UiStyle.WithAlpha(UiStyle.Gold, 0.13f));
            Center(halo.rectTransform, new Vector2(w * 0.88f, w * 0.88f), Vector2.zero);

            var ring = MakeImage(rt, "MedallionRing", UiSpriteFactory.Ring,
                UiStyle.WithAlpha(UiStyle.Gold, 0.26f));
            Center(ring.rectTransform, new Vector2(w * 0.60f, w * 0.60f), Vector2.zero);

            var outer = MakeImage(rt, "MedallionOuter", UiSpriteFactory.Diamond,
                UiStyle.WithAlpha(UiStyle.Gold, 0.50f));
            Center(outer.rectTransform, new Vector2(w * 0.40f, w * 0.40f), Vector2.zero);

            var mid = MakeImage(rt, "MedallionMid", UiSpriteFactory.Diamond,
                UiStyle.WithAlpha(UiStyle.PanelInset, 0.97f));
            Center(mid.rectTransform, new Vector2(w * 0.31f, w * 0.31f), Vector2.zero);

            var core = MakeImage(rt, "MedallionCore", UiSpriteFactory.Diamond,
                UiStyle.WithAlpha(UiStyle.Gold, 0.86f));
            Center(core.rectTransform, new Vector2(w * 0.13f, w * 0.13f), Vector2.zero);

            var coreRing = MakeImage(rt, "MedallionCoreRing", UiSpriteFactory.Ring,
                UiStyle.WithAlpha(UiStyle.Ivory, 0.24f));
            Center(coreRing.rectTransform, new Vector2(w * 0.19f, w * 0.19f), Vector2.zero);

            AddDiamond(rt, "KawungTL", new Vector2(-w * 0.15f, w * 0.15f), w * 0.055f,
                UiStyle.WithAlpha(UiStyle.Gold, 0.42f));
            AddDiamond(rt, "KawungTR", new Vector2(w * 0.15f, w * 0.15f), w * 0.055f,
                UiStyle.WithAlpha(UiStyle.Gold, 0.42f));
            AddDiamond(rt, "KawungBL", new Vector2(-w * 0.15f, -w * 0.15f), w * 0.055f,
                UiStyle.WithAlpha(UiStyle.Gold, 0.42f));
            AddDiamond(rt, "KawungBR", new Vector2(w * 0.15f, -w * 0.15f), w * 0.055f,
                UiStyle.WithAlpha(UiStyle.Gold, 0.42f));

            float dx = w * 0.33f;
            float dy = h * 0.36f;
            AddDiamond(rt, "CornerTL", new Vector2(-dx, dy), w * 0.065f,
                UiStyle.WithAlpha(UiStyle.Gold, 0.55f));
            AddDiamond(rt, "CornerTR", new Vector2(dx, dy), w * 0.065f,
                UiStyle.WithAlpha(UiStyle.Gold, 0.55f));
            AddDiamond(rt, "CornerBL", new Vector2(-dx, -dy), w * 0.065f,
                UiStyle.WithAlpha(UiStyle.Gold, 0.55f));
            AddDiamond(rt, "CornerBR", new Vector2(dx, -dy), w * 0.065f,
                UiStyle.WithAlpha(UiStyle.Gold, 0.55f));

            // A small "up" mark at the top (the opponent fan is rotated 180 degrees).
            AddDiamond(rt, "TopMark", new Vector2(0f, h * 0.445f), w * 0.045f,
                UiStyle.WithAlpha(UiStyle.Ivory, 0.40f));
            AddDiamond(rt, "BottomMark", new Vector2(0f, -h * 0.445f), w * 0.035f,
                UiStyle.WithAlpha(UiStyle.Gold, 0.30f));

            return rt;
        }

        private static void AddBatikBand(RectTransform parent, Vector2 size, float s, float side)
        {
            float y = size.y * 0.395f * side;
            for (int i = -2; i <= 2; i++)
            {
                float x = size.x * 0.15f * i;
                AddDiamond(parent, "Band" + side + "_" + i, new Vector2(x, y),
                    size.x * 0.045f, UiStyle.WithAlpha(UiStyle.Gold, 0.30f));
            }

            var line = MakeImage(parent, "BandLine" + side, null,
                UiStyle.WithAlpha(UiStyle.Gold, 0.16f));
            Center(line.rectTransform, new Vector2(size.x * 0.56f, 1.2f * s),
                new Vector2(0f, y));
        }

        private static void AddDiamond(RectTransform parent, string name, Vector2 pos,
            float diameter, Color color)
        {
            var d = MakeImage(parent, name, UiSpriteFactory.Diamond, color);
            Center(d.rectTransform, new Vector2(diameter, diameter), pos);
        }

        private static void Center(RectTransform rt, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        private static Image MakeImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }
    }
}
