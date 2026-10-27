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
    /// A read-only copy of the pixels currently shown by a CardView. Form-change FX use
    /// this to keep the old card on screen while the authoritative state has already
    /// moved to the new card. It contains no CardInstance and never enters gameplay.
    /// </summary>
    public struct CardVisualSnapshot
    {
        public Sprite Art;
        public Sprite ElementIcon;
        public string HpText;
        public float HpFill;
        public Color HpColor;
        public float HpFontSize;
        public string CostText;
        public string SupportText;
        public Color BackingColor;
        public Color FrameColor;
        public Color DimColor;
        public Color SupportChipColor;
        public Color SupportTextColor;
        public Color SupportRingColor;
        public Color StageRingColor;
        public Color BottomAccentColor;
        public Color ElementRingColor;
        public bool ShowArt;
        public bool ShowStrip;
        public bool ShowCost;
        public bool ShowElement;
        public bool ShowSupport;
        public bool ShowFrame;
        public bool ShowStageRing;
        public bool ShowBottomAccent;
    }

    /// <summary>
    /// Reusable card visual. Shows the artist's full-card artwork (which already has the
    /// name, attack, flavor and frame baked in) plus lightweight live overlays:
    /// HP bar + text, element icon, attack-energy cost, KO/selected states.
    /// Handles its own click events. Built entirely in code - no prefab needed.
    /// </summary>
    public class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public CardInstance Card { get; private set; }
        public CardDisplayMode Mode { get; private set; } = CardDisplayMode.Active;
        public bool IsSelected { get; private set; }
        public bool IsEligible { get; private set; }
        public event Action<CardView> Clicked;

        /// <summary>Fires when the pointer enters/leaves this card's raycast area (bubbled from children).</summary>
        public event Action<CardView, bool> Hovered;

        private RectTransform _root;
        private Image _backing;
        private Image _frame;          // gold ring on actives, faint ivory on the bench/hand
        private Image _selectedFrame;  // bright ring for selection feedback
        private Image _eligibleFrame;  // soft ring for "legal pick" cards (setup)
        private Image _targetFrame;    // action-target ring (attack / retreat affordance)
        private Image _stageRing;      // second inner ring for evolution-stage cards
        private Image _bottomAccent;   // element/support color line in the UI backing
        private Image _art;
        private Image _dim;
        private Image _elementChip;
        private Image _elementRing;
        private Image _elementIcon;
        private Image _hpBg;
        private Image _hpFill;
        private Image _costChip;
        private Image _costRing;
        private Image _supportChip;
        private Image _supportRing;
        private TMP_Text _hpText;
        private TMP_Text _costText;
        private TMP_Text _supportText;
        private float _shownHpPct = 1f;
        private bool _hpTweening;
        private Coroutine _hpTween;
        private bool _hovered;
        private Color _frameDefault = Color.clear;   // hover-only outline, transparent at rest

        // current global layout scale (1 = the 1080x1920 reference composition)
        public static float GlobalScale = 1f;

        private List<ScaledItem> _scaled;   // captured lazily on first ApplyScale

        public RectTransform Root => _root;

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

            // shadow first: it must sit behind the frame/target rings, not over them
            var shadow = MakeImage("Shadow", UiSpriteFactory.SoftShadow, UiStyle.CardShadow);
            Stretch(shadow.rectTransform, -26, -26, -26, -26); // symmetric halo around the card

            _selectedFrame = MakeImage("SelectedFrame", UiSpriteFactory.RoundOutline,
                UiStyle.SelectedFrame);
            _selectedFrame.type = Image.Type.Sliced;
            Stretch(_selectedFrame.rectTransform, -6, -6, -6, -6);
            _selectedFrame.gameObject.SetActive(false);

            _eligibleFrame = MakeImage("EligibleFrame", UiSpriteFactory.RoundOutline,
                UiStyle.EligibleFrame);
            _eligibleFrame.type = Image.Type.Sliced;
            Stretch(_eligibleFrame.rectTransform, -5, -5, -5, -5);
            _eligibleFrame.gameObject.SetActive(false);

            _targetFrame = MakeImage("TargetFrame", UiSpriteFactory.RoundOutline,
                UiStyle.TargetHarmful);
            _targetFrame.type = Image.Type.Sliced;
            Stretch(_targetFrame.rectTransform, -8, -8, -8, -8);
            _targetFrame.gameObject.SetActive(false);

            // The supplied card PNG already carries its own printed border. This ring is
            // only a hover outline just outside the art (invisible at rest); the heavier
            // selection/eligible/target rings are the interaction states.
            _frame = MakeImage("HoverOutline", UiSpriteFactory.RoundOutline, Color.clear);
            _frame.type = Image.Type.Sliced;
            Stretch(_frame.rectTransform, -2, -2, -2, -2);

            _backing = MakeImage("Backing", UiSpriteFactory.Rounded, UiStyle.CardBacking);
            _backing.type = Image.Type.Sliced;
            Stretch(_backing.rectTransform, 0, 0, 0, 0);
            _backing.raycastTarget = true; // the click target of the whole card

            // Evolution-stage halo outside the printed frame, never over the art.
            _stageRing = MakeImage("StageRing", UiSpriteFactory.RoundOutline, UiStyle.StageRing1);
            _stageRing.type = Image.Type.Sliced;
            Stretch(_stageRing.rectTransform, -4, -4, -4, -4);
            _stageRing.gameObject.SetActive(false);

            // The card PNG is the complete face (border, name, printed stats, effect text).
            // Render it across the whole slot; only preserveAspect may leave a few pixels
            // of dark backing at the sides because slot and card aspects differ slightly.
            _art = MakeImage("Art", null, Color.white);
            _art.preserveAspect = true;
            Stretch(_art.rectTransform, 0, 0, 0, 0);
            _art.raycastTarget = true;

            // Kept for snapshot compatibility with form-change FX; the printed artwork
            // already carries the element/support colour language.
            _bottomAccent = MakeImage("BottomAccent", null, UiStyle.Gold);
            var accentRt = _bottomAccent.rectTransform;
            accentRt.anchorMin = new Vector2(0f, 0f);
            accentRt.anchorMax = new Vector2(1f, 0f);
            accentRt.pivot = new Vector2(0.5f, 0f);
            accentRt.offsetMin = new Vector2(8f, 3f);
            accentRt.offsetMax = new Vector2(-8f, 5f);
            _bottomAccent.gameObject.SetActive(false);

            // ---- info strip (bottom): element icon + live HP ----
            var strip = new GameObject("InfoStrip", typeof(RectTransform));
            var stripRt = (RectTransform)strip.transform;
            strip.transform.SetParent(transform, false);
            stripRt.anchorMin = new Vector2(0f, 0f);
            stripRt.anchorMax = new Vector2(1f, 0f);
            stripRt.pivot = new Vector2(0.5f, 0f);
            stripRt.offsetMin = new Vector2(5f, 3f);
            stripRt.offsetMax = new Vector2(-5f, 27f);

            var stripBacking = MakeImage("StripBacking", UiSpriteFactory.Rounded, UiStyle.StripBacking);
            stripBacking.type = Image.Type.Sliced;
            stripBacking.rectTransform.SetParent(strip.transform, false);
            Stretch(stripBacking.rectTransform, 0, 0, 0, 0);

            _elementChip = MakeImage("ElementChip", UiSpriteFactory.Circle, UiStyle.Ivory);
            _elementChip.transform.SetParent(strip.transform, false);
            var chipRt = _elementChip.rectTransform;
            chipRt.sizeDelta = new Vector2(20, 20);
            chipRt.anchorMin = new Vector2(0, 0.5f);
            chipRt.anchorMax = new Vector2(0, 0.5f);
            chipRt.anchoredPosition = new Vector2(15, 0);

            _elementRing = MakeImage("ElementRing", UiSpriteFactory.Ring, UiStyle.Dark);
            var elRingRt = _elementRing.rectTransform;
            elRingRt.SetParent(_elementChip.rectTransform, false);
            Stretch(elRingRt, -2, -2, -2, -2);

            _elementIcon = MakeImage("ElementIcon", null, Color.white);
            var elRt = _elementIcon.rectTransform;
            elRt.SetParent(strip.transform, false);
            elRt.sizeDelta = new Vector2(17, 17);
            elRt.anchorMin = new Vector2(0, 0.5f);
            elRt.anchorMax = new Vector2(0, 0.5f);
            elRt.anchoredPosition = new Vector2(15, 0);

            _hpBg = MakeImage("HpBg", UiSpriteFactory.Rounded, UiStyle.HpTrack);
            _hpBg.type = Image.Type.Sliced;
            var bgRt = _hpBg.rectTransform;
            bgRt.SetParent(strip.transform, false);
            bgRt.anchorMin = new Vector2(0, 0.5f);
            bgRt.anchorMax = new Vector2(1, 0.5f);
            // slim bar, vertically centred on the strip line, clear of the tiny element orb
            bgRt.offsetMin = new Vector2(29, -8f);
            bgRt.offsetMax = new Vector2(-4, 8f);

            _hpFill = MakeImage("HpFill", UiSpriteFactory.Rounded, UiStyle.HpFull);
            _hpFill.type = Image.Type.Sliced;
            var fillRt = _hpFill.rectTransform;
            fillRt.SetParent(bgRt.transform, false);
            Stretch(fillRt, 3, 3, 3, 3);
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
            _hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _hpFill.fillAmount = 1f;

            var hpOutline = MakeImage("HpOutline", UiSpriteFactory.RoundOutline, UiStyle.HpOutline);
            hpOutline.type = Image.Type.Sliced;
            hpOutline.rectTransform.SetParent(bgRt.transform, false);
            Stretch(hpOutline.rectTransform, 0, 0, 0, 0);

            _hpText = MakeText("HpText", bgRt, 13, UiStyle.HpText, TextAlignmentOptions.Center, bold: true);
            Stretch(_hpText.rectTransform, 2, 0, 2, 0);

            // ---- attack energy cost chip, over the printed empty cost orb (bottom-left) ----
            _costChip = MakeImage("CostChip", UiSpriteFactory.Circle, UiStyle.Gold);
            var costRt = _costChip.rectTransform;
            costRt.anchorMin = new Vector2(0.13f, 0.30f);
            costRt.anchorMax = new Vector2(0.13f, 0.30f);
            costRt.anchoredPosition = Vector2.zero;
            costRt.sizeDelta = new Vector2(26, 26);
            _costRing = MakeImage("CostRing", UiSpriteFactory.Ring, new Color(0f, 0f, 0f, 0.45f));
            var ringRt = _costRing.rectTransform;
            ringRt.SetParent(costRt.transform, false);
            Stretch(ringRt, -2, -2, -2, -2);

            _costText = MakeText("CostText", costRt, UiStyle.CardCost, UiStyle.InkOnGold,
                TextAlignmentOptions.Center, bold: true);
            Stretch(_costText.rectTransform, 0, 0, 0, 0);

            // ---- support tag: small footer, flush with the printed bottom border ----
            _supportChip = MakeImage("SupportChip", UiSpriteFactory.Rounded, UiStyle.SupportChip);
            _supportChip.type = Image.Type.Sliced;
            var supRt = _supportChip.rectTransform;
            supRt.anchorMin = new Vector2(0.5f, 0f);
            supRt.anchorMax = new Vector2(0.5f, 0f);
            supRt.pivot = new Vector2(0.5f, 0f);
            supRt.anchoredPosition = new Vector2(0f, 4f);
            supRt.sizeDelta = new Vector2(74f, 18f);

            _supportRing = MakeImage("SupportRing", UiSpriteFactory.RoundOutline,
                UiStyle.SupportChipRing);
            _supportRing.type = Image.Type.Sliced;
            _supportRing.rectTransform.SetParent(supRt, false);
            Stretch(_supportRing.rectTransform, -1, -1, -1, -1);

            _supportText = MakeText("SupportText", supRt, UiStyle.CardChip, UiStyle.Ivory,
                TextAlignmentOptions.Center, bold: true);
            _supportText.text = "PASIF";
            Stretch(_supportText.rectTransform, 0, 0, 0, 0);

            _dim = MakeImage("Dim", null, new Color(0f, 0f, 0f, 0f));
            Stretch(_dim.rectTransform, 0, 0, 0, 0);
            _dim.raycastTarget = false;
        }

        public void Bind(CardInstance card, CardDisplayMode mode)
        {
            // A view being rebound to a different card must not keep tweening the old
            // card's HP (the KO/promotion path rebinds in the same frame as the damage).
            if (!ReferenceEquals(Card, card))
                StopHpTween();

            bool cardChanged = !ReferenceEquals(Card, card);
            Card = card;
            Mode = mode;
            gameObject.SetActive(card != null);
            if (card == null)
                return;

            // Board views are laid out by their slot and only ever leave that centre
            // for cosmetic lunges/shakes. A promoted/retreated card is a different
            // physical card, so start it from the slot centre instead of inheriting
            // a stale motion offset from the card it replaces.
            if (cardChanged && (mode == CardDisplayMode.Active || mode == CardDisplayMode.Bench) && _root != null)
            {
                _root.anchoredPosition = Vector2.zero;
                _root.localRotation = Quaternion.identity;
                _root.localScale = Vector3.one;
            }
            EnsureBuilt();

            bool isBattle = card.IsBattle;
            bool isBoard = mode == CardDisplayMode.Active || mode == CardDisplayMode.Bench;
            bool showStrip = isBoard && isBattle;
            int stage = isBattle ? UiStyle.EvolutionStage(card.CardId) : 0;

            _art.sprite = ArtLibrary.GetCardArt(card.ArtResource);
            _art.gameObject.SetActive(true);

            _elementIcon.sprite = ArtLibrary.GetElement(card.Element);
            _elementIcon.gameObject.SetActive(showStrip);
            _elementChip.gameObject.SetActive(showStrip);
            _elementRing.color = UiStyle.WithAlpha(UiStyle.ElementAccent(card.Element), 0.70f);

            _costChip.gameObject.SetActive(isBattle && mode != CardDisplayMode.Hidden);
            if (isBattle)
                _costText.text = card.AttackCost.ToString();

            _supportChip.gameObject.SetActive(card.IsSupport && mode != CardDisplayMode.Hidden);
            if (card.IsSupport)
            {
                _supportText.text = UiStyle.TypeLabel(card);
                _supportChip.color = UiStyle.SupportChipColor(card);
                _supportRing.color = UiStyle.SupportRingColor(card);
                _supportText.color = UiStyle.SupportTextColor(card);
            }
            _backing.color = card.IsSupport ? UiStyle.CardBackingSupport : UiStyle.CardBacking;

            // The printed card face owns its border/color. Keep the old frame image as the
            // hover-only outer outline (clear at rest) and the stage halo outside the art.
            _frame.gameObject.SetActive(mode != CardDisplayMode.Hidden);
            _frameDefault = Color.clear;
            _frame.color = _frameDefault;

            _stageRing.gameObject.SetActive(stage > 0 && mode != CardDisplayMode.Hidden);
            _stageRing.color = UiStyle.StageRingColor(stage);

            // Decorative in-card accent is redundant with the printed batik border.
            _bottomAccent.gameObject.SetActive(false);

            // Info strip: shown only for battle cards on the board.
            var strip = _hpBg.transform.parent.gameObject;
            strip.SetActive(showStrip);
            if (showStrip)
            {
                _hpText.fontSize = (mode == CardDisplayMode.Active
                    ? UiStyle.CardHpActive
                    : UiStyle.CardHpBench) * GlobalScale;
                SetHpDisplay(card.CurrentHp, card.MaxHp);
            }

            SetDimmed(false);
            SetHovered(false);
            SetSelected(false);
            SetEligible(false);
            SetTarget(false, false);
        }

        /// <summary>
        /// Copy the currently rendered card face. Must be called before the view is
        /// rebound, so an evolution/transformation can animate away from the old face.
        /// </summary>
        public CardVisualSnapshot CaptureVisual()
        {
            EnsureBuilt();
            return new CardVisualSnapshot
            {
                Art = _art.sprite,
                ElementIcon = _elementIcon.sprite,
                HpText = _hpText.text,
                HpFill = _hpFill.fillAmount,
                HpColor = _hpFill.color,
                HpFontSize = _hpText.fontSize,
                CostText = _costText.text,
                SupportText = _supportText.text,
                BackingColor = _backing.color,
                FrameColor = _frame.color,
                DimColor = _dim.color,
                SupportChipColor = _supportChip.color,
                SupportTextColor = _supportText.color,
                SupportRingColor = _supportRing.color,
                StageRingColor = _stageRing.color,
                BottomAccentColor = _bottomAccent.color,
                ElementRingColor = _elementRing.color,
                ShowArt = _art.gameObject.activeSelf,
                ShowStrip = _hpBg.transform.parent.gameObject.activeSelf,
                ShowCost = _costChip.gameObject.activeSelf,
                ShowElement = _elementChip.gameObject.activeSelf,
                ShowSupport = _supportChip.gameObject.activeSelf,
                ShowFrame = _frame.gameObject.activeSelf,
                ShowStageRing = _stageRing.gameObject.activeSelf,
                ShowBottomAccent = _bottomAccent.gameObject.activeSelf,
            };
        }

        /// <summary>
        /// Apply a captured face to a throwaway CardView. The root transform is left
        /// alone, which lets a form-change ghost swap faces at the flash peak without
        /// resetting its spin/lift/scale.
        /// </summary>
        public void BindVisual(CardVisualSnapshot visual, CardDisplayMode mode)
        {
            Card = null;
            Mode = mode;
            gameObject.SetActive(true);
            EnsureBuilt();

            _art.sprite = visual.Art;
            _art.gameObject.SetActive(visual.ShowArt);
            _elementIcon.sprite = visual.ElementIcon;
            _elementIcon.gameObject.SetActive(visual.ShowElement);
            _elementChip.gameObject.SetActive(visual.ShowElement);
            _costChip.gameObject.SetActive(visual.ShowCost);
            _costText.text = visual.CostText;
            _supportChip.gameObject.SetActive(visual.ShowSupport);
            _supportChip.color = visual.SupportChipColor;
            _supportRing.color = visual.SupportRingColor;
            _supportText.text = visual.SupportText;
            _supportText.color = visual.SupportTextColor;
            _backing.color = visual.BackingColor;
            _frame.gameObject.SetActive(visual.ShowFrame);
            _frame.color = visual.FrameColor;
            _frameDefault = visual.FrameColor;
            _stageRing.gameObject.SetActive(visual.ShowStageRing);
            _stageRing.color = visual.StageRingColor;
            _bottomAccent.gameObject.SetActive(visual.ShowBottomAccent);
            _bottomAccent.color = visual.BottomAccentColor;
            _elementRing.color = visual.ElementRingColor;
            _dim.color = visual.DimColor;

            var strip = _hpBg.transform.parent.gameObject;
            strip.SetActive(visual.ShowStrip);
            _hpFill.fillAmount = visual.HpFill;
            _hpFill.color = visual.HpColor;
            _hpText.text = visual.HpText;
            _hpText.fontSize = visual.HpFontSize;

            SetSelected(false);
            SetEligible(false);
            SetTarget(false, false);
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
                // Layout systems (HandController fan, slot tweens, attack lunge) own the
                // root's position; the rescale pass may only resize it.
                if (_scaled.Count > 0)
                    _scaled[0].ApplyPosition = false;
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
            // While a damage tween is running it owns both the fill and the number, so
            // state refreshes can't snap the bar ahead of the impact.
            if (_hpTweening)
                return;
            float pct = Mathf.Clamp01((float)current / max);
            _hpFill.fillAmount = pct;
            _shownHpPct = pct;
            _hpText.text = UiStyle.HpLabel(current, max, true);
            _hpFill.color = UiStyle.HpColor(pct);
        }

        /// <summary>
        /// Smooth HP bar tween used when damage lands. A delay holds the pre-damage bar
        /// until the attack's impact frame; the number counts down with the bar.
        /// </summary>
        public void TweenHpTo(int current, int max, float duration = 0.4f, float delay = 0f)
        {
            if (_hpFill == null || max <= 0 || gameObject == null || !gameObject.activeInHierarchy)
                return;
            if (_hpTween != null)
                StopCoroutine(_hpTween);
            _hpTweening = true; // synchronously, before the manager's same-frame refresh
            _hpTween = StartCoroutine(TweenHpRoutine(current, max, duration, delay));
        }

        private void StopHpTween()
        {
            if (_hpTween != null)
                StopCoroutine(_hpTween);
            _hpTween = null;
            _hpTweening = false;
        }

        private System.Collections.IEnumerator TweenHpRoutine(int current, int max, float duration, float delay)
        {
            float from = _shownHpPct;
            int fromHp = Mathf.RoundToInt(from * max);
            float to = Mathf.Clamp01((float)current / max);
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < duration)
            {
                if (_hpFill == null)
                    break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                float pct = Mathf.Lerp(from, to, k);
                _hpFill.fillAmount = pct;
                _hpFill.color = UiStyle.HpColor(pct);
                _hpText.text = UiStyle.HpLabel(
                    Mathf.RoundToInt(Mathf.Lerp(fromHp, current, k)), max, true);
                yield return null;
            }
            if (_hpFill != null)
            {
                _hpFill.fillAmount = to;
                _hpFill.color = UiStyle.HpColor(to);
                _hpText.text = UiStyle.HpLabel(current, max, true);
                _shownHpPct = to;
            }
            _hpTweening = false;
            _hpTween = null;
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (_selectedFrame != null)
                _selectedFrame.gameObject.SetActive(selected);
        }

        /// <summary>Soft ring marking a card that is a legal pick right now (setup).</summary>
        public void SetEligible(bool eligible)
        {
            IsEligible = eligible;
            if (_eligibleFrame != null)
                _eligibleFrame.gameObject.SetActive(eligible);
        }

        /// <summary>
        /// Action-target affordance: harmful = a valid enemy to attack, helpful = a valid
        /// bench card to retreat to. Purely visual - clicks always keep their normal rules.
        /// </summary>
        public void SetTarget(bool targeted, bool harmful)
        {
            if (_targetFrame == null)
                return;
            _targetFrame.gameObject.SetActive(targeted);
            if (targeted)
                _targetFrame.color = harmful
                    ? UiStyle.TargetHarmful
                    : UiStyle.TargetHelpful;
        }

        /// <summary>Whether the card is currently hover-highlighted.</summary>
        public bool IsHovered => _hovered;

        /// <summary>
        /// Hover highlight: warms the mode frame in place. Deliberately no new child
        /// objects and no motion here, so the rescale capture stays valid and the
        /// fan layout (HandController) remains the single owner of card movement.
        /// </summary>
        public void SetHovered(bool hovered)
        {
            _hovered = hovered;
            if (_frame != null)
                _frame.color = hovered ? UiStyle.HoverFrame : _frameDefault;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Card == null || !gameObject.activeInHierarchy)
                return;
            SetHovered(true);
            Hovered?.Invoke(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Process even for a dying view: listeners may need to clear their state.
            SetHovered(false);
            Hovered?.Invoke(this, false);
        }

        public void SetDimmed(bool dimmed, float alpha = 0.55f)
        {
            if (_dim != null)
                _dim.color = new Color(0f, 0f, 0f, dimmed ? alpha : 0f);
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
    }
}
