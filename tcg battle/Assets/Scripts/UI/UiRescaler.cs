using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace TcgBattle.UI
{
    /// <summary>
    /// One captured transform: its reference-scale size, offsets and anchored position,
    /// plus font metrics when it carries TMP text. Apply(k) rebuilds everything at a
    /// uniform scale factor so the whole composition stays proportional as the canvas
    /// size changes (Canvas Scaler match 0.5 makes the canvas rect aspect-dependent).
    /// </summary>
    public class ScaledItem
    {
        public RectTransform RT;
        public Vector2 Size;
        public Vector2 OffMin, OffMax;
        public Vector2 Pos;
        public TMP_Text Text;
        public float FontSize;
        public float FontMin, FontMax;   // used only when auto-sizing is enabled
        public float CharSpacing;
        /// <summary>False for roots whose position is owned by a layout/animation system.</summary>
        public bool ApplyPosition = true;

        public static ScaledItem Capture(RectTransform rt)
        {
            var item = new ScaledItem
            {
                RT = rt,
                Size = rt.sizeDelta,
                OffMin = rt.offsetMin,
                OffMax = rt.offsetMax,
                Pos = rt.anchoredPosition
            };
            var tmp = rt.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                item.Text = tmp;
                item.FontSize = tmp.fontSize;
                item.CharSpacing = tmp.characterSpacing;
                if (tmp.enableAutoSizing)
                {
                    item.FontMin = tmp.fontSizeMin;
                    item.FontMax = tmp.fontSizeMax;
                }
            }
            return item;
        }

        public void Apply(float k)
        {
            if (RT == null)
                return;
            float ax = RT.anchorMax.x - RT.anchorMin.x;
            float ay = RT.anchorMax.y - RT.anchorMin.y;
            bool axStretch = ax >= 0.999f;
            bool ayStretch = ay >= 0.999f;

            var sd = RT.sizeDelta;
            if (!axStretch)
                sd = new Vector2(Size.x * k, sd.y);
            if (!ayStretch)
                sd = new Vector2(sd.x, Size.y * k);
            RT.sizeDelta = sd;

            var om = RT.offsetMin;
            var oM = RT.offsetMax;
            if (axStretch)
            {
                om.x = OffMin.x * k;
                oM.x = OffMax.x * k;
            }
            if (ayStretch)
            {
                om.y = OffMin.y * k;
                oM.y = OffMax.y * k;
            }
            RT.offsetMin = om;
            RT.offsetMax = oM;

            if (ApplyPosition)
            {
                if (!axStretch && !ayStretch)
                    RT.anchoredPosition = Pos * k;
                else if (!axStretch)
                    RT.anchoredPosition = new Vector2(Pos.x * k, RT.anchoredPosition.y);
                else if (!ayStretch)
                    RT.anchoredPosition = new Vector2(RT.anchoredPosition.x, Pos.y * k);
            }

            if (Text != null)
            {
                if (Text.enableAutoSizing)
                {
                    Text.fontSizeMin = FontMin * k;
                    Text.fontSizeMax = FontMax * k;
                }
                else
                {
                    Text.fontSize = FontSize * k;
                }
                Text.characterSpacing = CharSpacing * k;
            }
        }
    }

    public static class UiRescaler
    {
        /// <summary>Capture a whole rect tree (including inactive children).</summary>
        public static void CaptureTree(RectTransform root, List<ScaledItem> into)
        {
            into.Add(ScaledItem.Capture(root));
            int n = root.childCount;
            for (int i = 0; i < n; i++)
            {
                var child = root.GetChild(i) as RectTransform;
                if (child != null)
                    CaptureTree(child, into);
            }
        }

        /// <summary>
        /// Same as CaptureTree but skips subtrees whose owner has a CardView - those are
        /// scaled by CardView.ApplyScale, because CardView rebinds its fonts per mode.
        /// </summary>
        public static void CaptureTreeExcludingCards(RectTransform root, List<ScaledItem> into)
        {
            if (root.GetComponent<CardView>() != null)
                return;
            into.Add(ScaledItem.Capture(root));
            int n = root.childCount;
            for (int i = 0; i < n; i++)
            {
                var child = root.GetChild(i) as RectTransform;
                if (child != null)
                    CaptureTreeExcludingCards(child, into);
            }
        }
    }
}
