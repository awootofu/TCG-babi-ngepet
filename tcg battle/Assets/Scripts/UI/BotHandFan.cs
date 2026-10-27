using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgBattle.Gameplay;

namespace TcgBattle.UI
{
    /// <summary>
    /// Compact, deliberately unreadable opponent hand: a fan of generic face-down card
    /// backs whose count mirrors BattleManager.Bot.Hand. The fan never binds a CardView,
    /// artwork, name, stat or tooltip, so hidden information cannot leak through hover or
    /// inspection. Draws animate a temporary back from the visible deck pile into the fan;
    /// plays send a generic back toward the committed destination. Purely cosmetic - it
    /// never affects battle timing.
    /// </summary>
    public class BotHandFan : MonoBehaviour
    {
        // Kept deliberately faint, but lifted a little so the generic backs still read
        // as dark-on-dark against the refined bot terrace.
        private const float FanOpacity = 0.40f;
        private const float DrawTime = 0.32f;
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
            FinishDrawAnimations();
            StopAllCoroutines();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
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
            {
                var back = CardBackVisual.Create(transform, "BotHandBack" + _backs.Count,
                    _baseCard * _k);
                var group = back.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 1f;
                group.interactable = false;
                group.blocksRaycasts = false;
                _backs.Add(back);
            }
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
            else
            {
                // If the UI binds after setup (or after a restart with a live hand),
                // the opening hand still gets the same deck-to-fan travel.
                for (int i = 0; i < _cards.Count; i++)
                    additions.Add(_cards[i]);
            }

            Layout();
            _hasSynced = true;
            AnimateAdditions(additions);
        }

        private void Layout()
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
                back.anchoredPosition = finalPos;
                back.localRotation = Quaternion.Euler(0f, 0f, rot);
                back.localScale = Vector3.one;
            }
        }

        private void AnimateAdditions(HashSet<CardInstance> additions)
        {
            if (!_hasSynced || additions == null || additions.Count == 0 || _deckAnchor == null)
                return;

            int order = 0;
            for (int i = 0; i < _cards.Count && i < _backs.Count; i++)
            {
                if (!additions.Contains(_cards[i]))
                    continue;
                var back = _backs[i];
                if (back == null)
                    continue;

                var group = back.GetComponent<CanvasGroup>();
                if (group == null)
                    group = back.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;

                // A tiny stagger keeps rapid consecutive draws legible, but stays far
                // below the turn pacing so gameplay is never delayed.
                float delay = Mathf.Min(order * 0.035f, 0.14f);
                CardDrawFx.Play(_root, _deckAnchor, back, _baseCard * _k, DrawTime, delay, null);
                order++;
            }
        }

        /// <summary>Snap any in-flight draw to the fan (used when the match ends).</summary>
        public void FinishDrawAnimations()
        {
            var fxs = GetComponentsInChildren<CardDrawFx>(true);
            for (int i = fxs.Length - 1; i >= 0; i--)
                if (fxs[i] != null)
                    fxs[i].Cancel();
        }

        private void SpawnPlayGhost(CardInstance card, Vector2 fromPos, Quaternion fromRot,
            RectTransform target)
        {
            var ghost = CardBackVisual.Create(_root, "BotHandPlayGhost", _baseCard * _k);
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
    }
}
