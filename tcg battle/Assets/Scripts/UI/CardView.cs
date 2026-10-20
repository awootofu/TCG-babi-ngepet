using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using TcgBattle.Gameplay;

namespace TcgBattle.UI
{
    public enum CardDisplayMode { Active, Bench, Hand, Hidden }

    /// <summary>
    /// Reusable card visual. Shows the artist's full-card artwork (which already has the
    /// name, attack, flavor and frame baked in) plus lightweight live overlays:
    /// HP bar + text, element icon, attack-energy cost, KO/selected states.
    /// Handles its own click events. Built entirely in code - no prefab needed.
    /// </summary>
    public class CardView : MonoBehaviour, IPointerClickHandler
    {
        public CardInstance Card { get; private set; }
        public CardDisplayMode Mode { get; private set; } = CardDisplayMode.Active;
        public bool IsSelected { get; private set; }
        public event Action<CardView> Clicked;

        private RectTransform _root;
        private Image _backing;
        private Image _frame;          // gold ring on actives, faint ivory on the bench
        private Image _selectedFrame;  // bright ring for selection feedback
        private Image _art;
        private Image _dim;
        private Image _elementChip;
        private Image _elementIcon;
        private Image _hpBg;
        private Image _hpFill;
        private Image _costChip;
        private Image _costRing;
        private Image _supportChip;
        private TMP_Text _hpText;
        private TMP_Text _costText;
        private TMP_Text _supportText;
        private float _shownHpPct = 1f;
        private bool _hpTweening;

        // current global layout scale (1 = the 1080x1920 reference composition)
        public static float GlobalScale = 1f;

        private List<ScaledItem> _scaled;   // captured lazily on first ApplyScale

        public RectTransform Root => _root;

        // palette (kept in sync with BattleUI)
        private static readonly Color ColBacking = Hex(0x1E1730);
        private static readonly Color ColBackingSupport = Hex(0x2A2148);
        private static readonly Color ColFrameGold = new Color(0xD9 / 255f, 0xA8 / 255f, 0x43 / 255f, 0.95f);
        private static readonly Color ColFrameIvory = new Color(0xF2 / 255f, 0xEA / 255f, 0xDA / 255f, 0.32f);
        private static readonly Color ColHpFull = Hex(0x6FA867);
        private static readonly Color ColHpMid = Hex(0xD9A843);
        private static readonly Color ColHpLow = Hex(0xC0504E);
        private static readonly Color ColIvory = Hex(0xF2EADA);
        private static readonly Color ColInk = Hex(0x241B36);

        public static CardView Create(Transform parent, Vector2 size, string name = "Card")
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            var view = go.AddComponent<CardView>();
            view.EnsureBuilt();
            return view;
        }

        public void EnsureBuilt()
        {
            if (_backing != null)
                return;
            _root = (RectTransform)transform;

            _selectedFrame = MakeImage("SelectedFrame", UiSpriteFactory.RoundOutline,
                new Color(1f, 0.86f, 0.45f, 1f));
            _selectedFrame.type = Image.Type.Sliced;
            Stretch(_selectedFrame.rectTransform, -11, -11, -11, -11);
            _selectedFrame.gameObject.SetActive(false);

            _frame = MakeImage("Frame", UiSpriteFactory.RoundOutline, ColFrameIvory);
            _frame.type = Image.Type.Sliced;
            Stretch(_frame.rectTransform, 1, 1, 1, 1);

            var shadow = MakeImage("Shadow", UiSpriteFactory.SoftShadow, new Color(0f, 0f, 0f, 0.5f));
            Stretch(shadow.rectTransform, -26, -26, -26, -26); // symmetric halo around the card

            _backing = MakeImage("Backing", UiSpriteFactory.Rounded, ColBacking);
            _backing.type = Image.Type.Sliced;
            Stretch(_backing.rectTransform, 0, 0, 0, 0);
            _backing.raycastTarget = true; // the click target of the whole card

            _art = MakeImage("Art", null, Color.white);
            _art.preserveAspect = true;
            Stretch(_art.rectTransform, 6, 6, 6, 48);
            _art.raycastTarget = true;

            // ---- info strip (bottom): element icon + live HP ----
            var strip = new GameObject("InfoStrip", typeof(RectTransform));
            var stripRt = (RectTransform)strip.transform;
            strip.transform.SetParent(transform, false);
            stripRt.anchorMin = new Vector2(0f, 0f);
            stripRt.anchorMax = new Vector2(1f, 0f);
            stripRt.pivot = new Vector2(0.5f, 0f);
            stripRt.offsetMin = new Vector2(6f, 6f);
            stripRt.offsetMax = new Vector2(-6f, 46f);

            _elementChip = MakeImage("ElementChip", UiSpriteFactory.Circle, ColIvory);
            _elementChip.transform.SetParent(strip.transform, false);
            var chipRt = _elementChip.rectTransform;
            chipRt.sizeDelta = new Vector2(34, 34);
            chipRt.anchorMin = new Vector2(0, 0.5f);
            chipRt.anchorMax = new Vector2(0, 0.5f);
            chipRt.anchoredPosition = new Vector2(23, 0);

            _elementIcon = MakeImage("ElementIcon", null, Color.white);
            var elRt = _elementIcon.rectTransform;
            elRt.SetParent(strip.transform, false);
            elRt.sizeDelta = new Vector2(27, 27);
            elRt.anchorMin = new Vector2(0, 0.5f);
            elRt.anchorMax = new Vector2(0, 0.5f);
            elRt.anchoredPosition = new Vector2(23, 0);

            _hpBg = MakeImage("HpBg", UiSpriteFactory.Rounded, new Color(0f, 0f, 0f, 0.55f));
            _hpBg.type = Image.Type.Sliced;
            var bgRt = _hpBg.rectTransform;
            bgRt.SetParent(strip.transform, false);
            bgRt.anchorMin = new Vector2(0, 0.5f);
            bgRt.anchorMax = new Vector2(1, 0.5f);
            // bar is 30 units tall, vertically centred on the strip line
            bgRt.offsetMin = new Vector2(47, -15f);
            bgRt.offsetMax = new Vector2(-4, 15f);

            _hpFill = MakeImage("HpFill", UiSpriteFactory.Rounded, ColHpFull);
            _hpFill.type = Image.Type.Sliced;
            var fillRt = _hpFill.rectTransform;
            fillRt.SetParent(bgRt.transform, false);
            Stretch(fillRt, 3, 3, 3, 3);
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
            _hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _hpFill.fillAmount = 1f;

            _hpText = MakeText("HpText", bgRt, 22, Color.white, TextAlignmentOptions.Center, bold: true);
            Stretch(_hpText.rectTransform, 2, 0, 2, 0);

            // ---- attack energy cost chip (top-left) ----
            _costChip = MakeImage("CostChip", UiSpriteFactory.Circle, ColHpMid);
            var costRt = _costChip.rectTransform;
            costRt.anchorMin = new Vector2(0, 1);
            costRt.anchorMax = new Vector2(0, 1);
            costRt.anchoredPosition = new Vector2(19, -19);
            costRt.sizeDelta = new Vector2(38, 38);
            _costRing = MakeImage("CostRing", UiSpriteFactory.Ring, new Color(0f, 0f, 0f, 0.45f));
            var ringRt = _costRing.rectTransform;
            ringRt.SetParent(costRt.transform, false);
            Stretch(ringRt, -2, -2, -2, -2);

            _costText = MakeText("CostText", costRt, 21, Hex(0x3A2506), TextAlignmentOptions.Center, bold: true);
            Stretch(_costText.rectTransform, 0, 0, 0, 0);

            // ---- support tag (top-right) ----
            _supportChip = MakeImage("SupportChip", UiSpriteFactory.Rounded, new Color(0.14f, 0.11f, 0.21f, 0.92f));
            _supportChip.type = Image.Type.Sliced;
            var supRt = _supportChip.rectTransform;
            supRt.anchorMin = new Vector2(1, 1);
            supRt.anchorMax = new Vector2(1, 1);
            supRt.anchoredPosition = new Vector2(-37, -15);
            supRt.sizeDelta = new Vector2(64, 26);
            _supportText = MakeText("SupportText", supRt, 15, ColIvory, TextAlignmentOptions.Center, bold: true);
            _supportText.text = "PASIF";
            Stretch(_supportText.rectTransform, 0, 0, 0, 0);

            _dim = MakeImage("Dim", null, new Color(0f, 0f, 0f, 0f));
            Stretch(_dim.rectTransform, 0, 0, 0, 0);
            _dim.raycastTarget = false;
        }

        public void Bind(CardInstance card, CardDisplayMode mode)
        {
            Card = card;
            Mode = mode;
            gameObject.SetActive(card != null);
            if (card == null)
                return;
            EnsureBuilt();

            bool isBattle = card.IsBattle;
            bool isBoard = mode == CardDisplayMode.Active || mode == CardDisplayMode.Bench;
            bool showStrip = isBoard && isBattle;

            _art.sprite = ArtLibrary.GetCardArt(card.ArtResource);
            _art.gameObject.SetActive(true);

            _elementIcon.sprite = ArtLibrary.GetElement(card.Element);
            _elementIcon.gameObject.SetActive(showStrip);
            _elementChip.gameObject.SetActive(showStrip);

            _costChip.gameObject.SetActive(isBattle && mode != CardDisplayMode.Hidden);
            if (isBattle)
                _costText.text = card.AttackCost.ToString();

            _supportChip.gameObject.SetActive(card.IsSupport);
            _backing.color = card.IsSupport ? ColBackingSupport : ColBacking;

            // mode frame: bright gold for the focal active, faint ivory on the bench/hand
            _frame.gameObject.SetActive(isBoard);
            _frame.color = mode == CardDisplayMode.Active ? ColFrameGold : ColFrameIvory;

            // Info strip: shown only for battle cards on the board.
            var strip = _hpBg.transform.parent.gameObject;
            strip.SetActive(showStrip);
            if (showStrip)
            {
                _hpText.fontSize = (mode == CardDisplayMode.Active ? 24f : 20f) * GlobalScale;
                SetHpDisplay(card.CurrentHp, card.MaxHp);
            }

            SetDimmed(false);
            SetSelected(false);
        }

        /// <summary>
        /// Scales every internal rect (and font) of this card to the canvas height.
        /// Captured lazily on first use; the HP text font is excluded because Bind sets
        /// it per display mode.
        /// </summary>
        public void ApplyScale(float k)
        {
            if (_root == null)
                return;
            if (_scaled == null)
            {
                _scaled = new List<ScaledItem>();
                UiRescaler.CaptureTree(_root, _scaled);
                foreach (var it in _scaled)
                    if (it.RT == _hpText.rectTransform)
                        it.Text = null; // Bind owns this font size
            }
            foreach (var it in _scaled)
                it.Apply(k);
        }

        /// <summary>HP bar + text, tinted by remaining ratio (green - amber - red).</summary>
        public void SetHpDisplay(int current, int max)
        {
            if (_hpFill == null || max <= 0)
                return;
            float pct = Mathf.Clamp01((float)current / max);
            if (!_hpTweening)
            {
                _hpFill.fillAmount = pct;
                _shownHpPct = pct;
            }
            _hpText.text = $"{current}/{max}";
            _hpFill.color = HpColor(pct);
        }

        /// <summary>Smooth HP bar tween used when damage lands.</summary>
        public void TweenHpTo(int current, int max, float duration = 0.4f)
        {
            if (_hpFill == null || max <= 0 || gameObject == null || !gameObject.activeInHierarchy)
                return;
            StartCoroutine(TweenHpRoutine(current, max, duration));
        }

        private System.Collections.IEnumerator TweenHpRoutine(int current, int max, float duration)
        {
            _hpTweening = true;
            float from = _shownHpPct;
            float to = Mathf.Clamp01((float)current / max);
            float t = 0f;
            while (t < duration)
            {
                if (_hpFill == null)
                    break;
                t += Time.unscaledDeltaTime;
                float pct = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
                _hpFill.fillAmount = pct;
                _hpFill.color = HpColor(pct);
                yield return null;
            }
            if (_hpFill != null)
            {
                _hpFill.fillAmount = to;
                _shownHpPct = to;
            }
            _hpTweening = false;
        }

        private static Color HpColor(float pct)
        {
            return pct > 0.5f
                ? Color.Lerp(ColHpMid, ColHpFull, (pct - 0.5f) / 0.5f)
                : Color.Lerp(ColHpLow, ColHpMid, pct / 0.5f);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (_selectedFrame != null)
                _selectedFrame.gameObject.SetActive(selected);
        }

        public void SetDimmed(bool dimmed)
        {
            if (_dim != null)
                _dim.color = new Color(0f, 0f, 0f, dimmed ? 0.55f : 0f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this);
        }

        private Image MakeImage(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private TMP_Text MakeText(string name, Transform parent, float fontSize, Color color,
            TextAlignmentOptions align, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = TmpFontService.Get();
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            if (bold)
                tmp.fontStyle = FontStyles.Bold;
            return tmp;
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
            return new Color32((byte)(rgba >> 16 & 0xFF), (byte)(rgba >> 8 & 0xFF), (byte)(rgba & 0xFF), 0xFF);
        }
    }
}
