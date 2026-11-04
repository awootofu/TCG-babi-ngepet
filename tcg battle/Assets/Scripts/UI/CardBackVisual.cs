using UnityEngine;
using UnityEngine.UI;

namespace TcgBattle.UI
{
    /// <summary>
    /// Shared face-down card visual used by the opponent hand, both deck piles and
    /// draw ghosts. It is the official KISATARA "Back Side" artwork loaded from
    /// Resources, so the player and bot always show exactly the same supplied design.
    /// The image preserves its aspect ratio and never exposes card identity, stats or
    /// element information.
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

            // Plain dark seat behind the supplied art: the card-back image is 630x880
            // while the UI slots are slightly wider, so preserveAspect leaves a couple
            // of pixels at the sides. The seat keeps those pixels from showing through.
            var seat = MakeImage(rt, "Seat", UiSpriteFactory.Rounded, UiStyle.CardBacking);
            seat.type = Image.Type.Sliced;
            Stretch(seat.rectTransform, 0f, 0f, 0f, 0f);

            var art = MakeImage(rt, "Art", ArtLibrary.GetCardBack(), Color.white);
            art.preserveAspect = true;
            Stretch(art.rectTransform, 0f, 0f, 0f, 0f);

            return rt;
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
