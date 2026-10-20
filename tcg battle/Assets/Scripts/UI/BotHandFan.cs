using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgBattle.Gameplay;

namespace TcgBattle.UI
{
    /// <summary>
    /// Compact, deliberately unreadable opponent hand: a fan of generic face-down card
    /// backs whose count mirrors BattleManager.Bot.Hand. The fan never binds a CardView,
    /// artwork, name, stat or tooltip, so hidden information cannot leak through hover or
    /// inspection. Draws animate a back out of the deck stack; plays send a generic back
    /// toward the committed destination. Purely cosmetic - it never affects battle timing.
    /// </summary>
    public class BotHandFan : MonoBehaviour
    {
        private const float FanOpacity = 0.32f;
        private const float DrawTime = 0.26f;
        private const float PlayTime = 0.32f;

        private RectTransform _root;
        private RectTransform _deckAnchor;
        private Func<CardInstance, RectTransform> _destination;
        private readonly List<CardInstance> _cards = new List<CardInstance>();
        private readonly List<RectTransform> _backs = new List<RectTransform>();
        private bool _hasSynced;
        private float _k = 1f;

        private readonly Vector2 _baseCard = new Vector2(123f, 165f);
        private const float BaseStep = 40f;
        private const float BaseMaxRow = 420f;
        private const float BaseArc = 5f;
        private const float BaseRotation = 8f;

        public int VisibleCount => _backs.Count;

        public void Initialize(RectTransform root, RectTransform deckAnchor,
            Func<CardInstance, RectTransform> destination)
        {
            _root = root;
            _deckAnchor = deckAnchor;
            _destination = destination;

            var group = gameObject.GetComponent<CanvasGroup>();
            if (group == null)
                group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = FanOpacity;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        /// <summary>Start a new match without animating the previous hand out.</summary>
        public void ResetVisuals()
        {
            StopAllCoroutines();
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            _cards.Clear();
            _backs.Clear();
            _hasSynced = false;
        }

        public void SetScale(float k)
        {
            _k = Mathf.Clamp(k, 0.3f, 2f);
            Layout();
        }

        /// <summary>
        /// Synchronise the visible backs with the real bot hand. Called after board slots
        /// have rebound, so a card that was played can resolve its committed destination.
        /// </summary>
        public void Sync(List<CardInstance> hand)
        {
            if (_root == null)
                return;
            if (hand == null)
                hand = new List<CardInstance>();

            var oldCards = new List<CardInstance>(_cards);
            var oldPositions = new List<Vector2>();
            var oldRotations = new List<Quaternion>();
            for (int i = 0; i < _backs.Count; i++)
            {
                oldPositions.Add(_backs[i].anchoredPosition);
                oldRotations.Add(_backs[i].localRotation);
            }

            if (_hasSynced)
            {
                for (int i = 0; i < oldCards.Count; i++)
                {
                    if (hand.Contains(oldCards[i]))
                        continue;
                    var target = _destination != null ? _destination(oldCards[i]) : null;
                    SpawnPlayGhost(oldCards[i], i < oldPositions.Count ? oldPositions[i] : Vector2.zero,
                        i < oldRotations.Count ? oldRotations[i] : Quaternion.identity, target);
                }
            }

            _cards.Clear();
            _cards.AddRange(hand);

            while (_backs.Count < _cards.Count)
                _backs.Add(CreateBack(transform, "BotHandBack" + _backs.Count, _baseCard * _k));
            while (_backs.Count > _cards.Count)
            {
                int last = _backs.Count - 1;
                if (_backs[last] != null)
                    Destroy(_backs[last].gameObject);
                _backs.RemoveAt(last);
            }

            var additions = new HashSet<CardInstance>();
            if (_hasSynced)
            {
                for (int i = 0; i < _cards.Count; i++)
                    if (!oldCards.Contains(_cards[i]))
                        additions.Add(_cards[i]);
            }

            Layout(additions);
            _hasSynced = true;
        }

        private void Layout(HashSet<CardInstance> animateIn = null)
        {
            if (_root == null)
                return;
            int n = _backs.Count;
            if (n == 0)
                return;

            float cardW = _baseCard.x * _k;
            float cardH = _baseCard.y * _k;
            float maxRow = BaseMaxRow * _k;
            float step = n > 1
                ? Mathf.Min(BaseStep * _k, Mathf.Max(0f, maxRow - cardW) / (n - 1))
                : 0f;
            float total = step * (n - 1);
            Vector2 fromDeck = _deckAnchor != null
                ? (Vector2)_root.InverseTransformPoint(_deckAnchor.position)
                : Vector2.zero;

            for (int i = 0; i < n; i++)
            {
                var back = _backs[i];
                if (back == null)
                    continue;
                back.sizeDelta = new Vector2(cardW, cardH);
                float t = n > 1 ? (float)i / (n - 1) - 0.5f : 0f;
                // The opponent holds the fan facing away from the camera: keep the
                // horizontal order/spacing, mirror the arc vertically and turn every
                // generic back upside down. No card face is ever bound here.
                var finalPos = new Vector2(-total * 0.5f + i * step, -Mathf.Abs(t) * BaseArc * _k);
                float rot = 180f + t * BaseRotation;
                back.SetAsLastSibling();

                bool animate = animateIn != null && i < _cards.Count && animateIn.Contains(_cards[i]);
                if (animate)
                {
                    back.anchoredPosition = fromDeck;
                    back.localRotation = Quaternion.identity;
                    back.localScale = Vector3.one;
                    StartCoroutine(DrawIn(back, finalPos, rot));
                }
                else
                {
                    back.anchoredPosition = finalPos;
                    back.localRotation = Quaternion.Euler(0f, 0f, rot);
                    back.localScale = Vector3.one;
                }
            }
        }

        private IEnumerator DrawIn(RectTransform back, Vector2 finalPos, float finalRot)
        {
            Vector2 from = back.anchoredPosition;
            float t = 0f;
            while (t < DrawTime)
            {
                if (back == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / DrawTime));
                back.anchoredPosition = Vector2.LerpUnclamped(from, finalPos, k);
                back.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, finalRot, k));
                yield return null;
            }
            if (back != null)
            {
                back.anchoredPosition = finalPos;
                back.localRotation = Quaternion.Euler(0f, 0f, finalRot);
            }
        }

        private void SpawnPlayGhost(CardInstance card, Vector2 fromPos, Quaternion fromRot,
            RectTransform target)
        {
            var ghost = CreateBack(_root, "BotHandPlayGhost", _baseCard * _k);
            ghost.anchoredPosition = fromPos;
            ghost.localRotation = fromRot;
            ghost.SetAsLastSibling();
            var fade = ghost.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 1f;
            fade.blocksRaycasts = false;
            fade.interactable = false;

            Vector2 to = fromPos;
            if (target != null)
                to = _root.InverseTransformPoint(target.position);
            StartCoroutine(PlayOut(ghost, fromPos, to, fade));
        }

        private IEnumerator PlayOut(RectTransform ghost, Vector2 from, Vector2 to, CanvasGroup fade)
        {
            float t = 0f;
            while (t < PlayTime)
            {
                if (ghost == null)
                    yield break;
                ghost.SetAsLastSibling();
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / PlayTime));
                ghost.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
                ghost.localScale = Vector3.one * Mathf.Lerp(1f, 0.84f, k);
                if (fade != null)
                    fade.alpha = Mathf.Clamp01(1f - Mathf.InverseLerp(0.55f, 1f, k));
                yield return null;
            }
            if (ghost != null)
                Destroy(ghost.gameObject);
        }

        /// <summary>Two static mini backs make the draw origin readable without a counter.</summary>
        public void BuildDeckStack(RectTransform deckRoot)
        {
            if (deckRoot == null)
                return;

            var group = deckRoot.gameObject.GetComponent<CanvasGroup>();
            if (group == null)
                group = deckRoot.gameObject.AddComponent<CanvasGroup>();
            group.alpha = FanOpacity;
            group.interactable = false;
            group.blocksRaycasts = false;

            var under = CreateBack(deckRoot, "DeckUnder", new Vector2(70f, 94f));
            under.anchoredPosition = new Vector2(-6f, 6f);
            under.localRotation = Quaternion.Euler(0f, 0f, -4f);
            var top = CreateBack(deckRoot, "DeckTop", new Vector2(70f, 94f));
            top.anchoredPosition = Vector2.zero;
            top.localRotation = Quaternion.Euler(0f, 0f, 3f);
        }

        private RectTransform CreateBack(Transform parent, string name, Vector2 size)
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
