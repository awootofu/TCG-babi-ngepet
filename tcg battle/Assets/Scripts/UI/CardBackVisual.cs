using UnityEngine;
using UnityEngine.UI;

namespace TcgBattle.UI
{
    /// <summary>
    /// The shared card-back artwork used by the opponent hand, deck piles and draw
    /// ghosts. It is deliberately generic: no card identity, name, stats or element.
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

            var shadow = MakeImage(rt, "Shadow", UiSpriteFactory.SoftShadow,
                new Color(0f, 0f, 0f, 0.45f));
            Stretch(shadow.rectTransform, -8f, -8f, -8f, -8f);

            var backing = MakeImage(rt, "Backing", UiSpriteFactory.Rounded,
                Hex(0x1E1730));
            backing.type = Image.Type.Sliced;
            Stretch(backing.rectTransform, 0f, 0f, 0f, 0f);

            var frame = MakeImage(rt, "Frame", UiSpriteFactory.RoundOutline,
                new Color(0.95f, 0.90f, 0.80f, 0.66f));
            frame.type = Image.Type.Sliced;
            Stretch(frame.rectTransform, 2f, 2f, 2f, 2f);

            var inner = MakeImage(rt, "InnerLine", UiSpriteFactory.RoundOutline,
                new Color(0.85f, 0.66f, 0.26f, 0.46f));
            inner.type = Image.Type.Sliced;
            Stretch(inner.rectTransform, 8f, 8f, 8f, 8f);

            // Small top-edge gilding gives the otherwise symmetric back a visible
            // "up": when the opponent fan is turned 180 degrees, this sits at the
            // bottom. It is a back ornament only - no text, art, stats or identity.
            var topMark = MakeImage(rt, "TopEdgeMark", UiSpriteFactory.Rounded,
                new Color(0.95f, 0.90f, 0.80f, 0.72f));
            topMark.type = Image.Type.Sliced;
            topMark.rectTransform.anchorMin = topMark.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            topMark.rectTransform.sizeDelta = new Vector2(size.x * 0.30f, 3f);
            topMark.rectTransform.anchoredPosition = new Vector2(0f, size.y * 0.39f);

            var diamond = MakeImage(rt, "Diamond", UiSpriteFactory.Diamond,
                new Color(0.85f, 0.66f, 0.26f, 0.74f));
            diamond.rectTransform.anchorMin = diamond.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            diamond.rectTransform.sizeDelta = new Vector2(34f, 34f) * (size.x / 123f);

            var diamondCore = MakeImage(rt, "DiamondCore", UiSpriteFactory.Diamond,
                new Color(0.12f, 0.09f, 0.19f, 0.92f));
            diamondCore.rectTransform.anchorMin = diamondCore.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            diamondCore.rectTransform.sizeDelta = new Vector2(21f, 21f) * (size.x / 123f);

            float dx = size.x * 0.32f;
            float dy = size.y * 0.34f;
            AddCornerDiamond(rt, "CornerTL", new Vector2(-dx, dy), size);
            AddCornerDiamond(rt, "CornerTR", new Vector2(dx, dy), size);
            AddCornerDiamond(rt, "CornerBL", new Vector2(-dx, -dy), size);
            AddCornerDiamond(rt, "CornerBR", new Vector2(dx, -dy), size);
            return rt;
        }

        private static void AddCornerDiamond(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            var d = MakeImage(parent, name, UiSpriteFactory.Diamond,
                new Color(0.85f, 0.66f, 0.26f, 0.55f));
            var rt = d.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(7f, 7f) * (size.x / 123f);
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

        private static Color Hex(int rgba)
        {
            return new Color32((byte)(rgba >> 16 & 0xFF), (byte)(rgba >> 8 & 0xFF),
                (byte)(rgba & 0xFF), 0xFF);
        }
    }
}
