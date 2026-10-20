using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgBattle.Gameplay;

namespace TcgBattle.UI
{
    /// <summary>
    /// Lays out the player's hand as a slightly fanned, overlapping row of card views
    /// and routes clicks back to the battle UI. Layout changes tween smoothly; a selected
    /// card lifts, straightens and scales up a touch for focus. Hovering adds a smaller
    /// lift through the SAME layout tween (no second tween system), warms the card frame
    /// and raises the card above its neighbors until the pointer leaves.
    /// </summary>
    public class HandController : MonoBehaviour
    {
        [Header("Hand layout")]
        public float cardWidth = 148f;
        public float cardHeight = 207f;
        public float maxSpread = 780f;          // total row width before cards overlap harder
        public float rotationPerCard = 3.5f;    // degrees at the edges
        public float liftPerCard = 9f;          // parabolic arc
        public float selectedLift = 44f;
        public float selectedScale = 1.06f;
        public float hoverLift = 18f;      // subtle lift while hovered (selected still wins)
        public float hoverScale = 1.03f;

        private readonly List<CardView> _views = new List<CardView>();
        private readonly Dictionary<RectTransform, Coroutine> _tweens = new Dictionary<RectTransform, Coroutine>();
        private RectTransform _rect;
        private Action<CardView> _onClick;
        private bool _hasLaidOut;
        private CardView _hovered;         // hand view currently under the pointer, if any
        private const float HoverTweenTime = 0.12f;

        // reference-scale card sizing (canvas height 1920); cardWidth/cardHeight hold the
        // currently applied (scaled) values used for fan layout
        private Vector2 _baseCard;
        private float _baseSpread, _baseLift, _baseSelLift, _baseHoverLift;

        public IReadOnlyList<CardView> Views => _views;

        private void Ensure()
        {
            if (_rect != null)
                return;
            _rect = (RectTransform)transform;
            _baseCard = new Vector2(cardWidth, cardHeight);
            _baseSpread = maxSpread;
            _baseLift = liftPerCard;
            _baseSelLift = selectedLift;
            _baseHoverLift = hoverLift;
        }

        /// <summary>Apply a uniform layout scale (canvas height / 1920) to the fan.</summary>
        public void SetScale(float k)
        {
            Ensure();
            cardWidth = _baseCard.x * k;
            cardHeight = _baseCard.y * k;
            maxSpread = _baseSpread * k;
            liftPerCard = _baseLift * k;
            selectedLift = _baseSelLift * k;
            hoverLift = _baseHoverLift * k;
            Layout(_hasLaidOut);
        }

        public void SetClickHandler(Action<CardView> onClick)
        {
            _onClick = onClick;
        }

        /// <summary>Sync views with the hand list; keeps existing views alive when possible.</summary>
        public void Sync(List<CardInstance> hand)
        {
            Ensure();

            // Remove views for cards no longer in hand.
            for (int i = _views.Count - 1; i >= 0; i--)
            {
                if (hand.Contains(_views[i].Card))
                    continue;
                StopTween(_views[i].Root);
                Destroy(_views[i].gameObject);
                _views.RemoveAt(i);
            }

            // A hovered view that left the hand also leaves hover state.
            if (_hovered != null && !_views.Contains(_hovered))
            {
                _hovered = null;
                RestoreFanOrder();
            }

            // Append views for new cards (always created at reference size; the current
            // scale is applied to their internals by the rescale pass).
            while (_views.Count < hand.Count)
            {
                var card = hand[_views.Count];
                var view = CardView.Create(transform, _baseCard, $"Hand {_views.Count}");
                view.Bind(card, CardDisplayMode.Hand);
                view.Clicked += c => _onClick?.Invoke(c);
                view.Hovered += OnViewHover;
                _views.Add(view);
            }

            // Rebind (order may have changed).
            for (int i = 0; i < _views.Count; i++)
            {
                _views[i].Bind(hand[i], CardDisplayMode.Hand);
                _views[i].SetSelected(false);
                _views[i].Root.SetAsLastSibling();
            }

            // Bind reset per-card state; if the pointer is still on the same view, re-assert
            // hover (visuals + raise) before the layout below retargets the fan.
            if (_hovered != null)
            {
                _hovered.SetHovered(true);
                _hovered.Root.SetAsLastSibling();
            }

            // apply the current layout scale to every card's internal rects
            foreach (var v in _views)
                v.ApplyScale(CardView.GlobalScale);

            Layout(_hasLaidOut);
            _hasLaidOut = true;
        }

        public void SetSelected(CardView view)
        {
            foreach (var v in _views)
                v.SetSelected(v == view);
        }

        public CardView FindView(CardInstance card)
        {
            foreach (var v in _views)
                if (v.Card == card)
                    return v;
            return null;
        }

        /// <summary>
        /// CardView hover callback. Folds hover into the SAME layout tween (retarget via
        /// Layout) - never a competing position/rotation tween. Raise is instant; exit
        /// restores hand order immediately so the fan never stays reordered.
        /// </summary>
        private void OnViewHover(CardView view, bool entered)
        {
            if (view == null || view.Mode != CardDisplayMode.Hand || !_views.Contains(view))
                return; // bench/active cards handle their own hover feedback later

            if (entered)
            {
                if (_hovered == view)
                    return;
                if (_hovered != null && _hovered != view)
                    _hovered.SetHovered(false); // that view's exit event may arrive late or never
                _hovered = view;
                view.Root.SetAsLastSibling();   // hovered card wins the overlap fight
            }
            else
            {
                if (_hovered != view)
                    return;                     // stale exit after Sync already cleared it
                _hovered = null;
                RestoreFanOrder();
            }
            Layout(true, HoverTweenTime);
        }

        /// <summary>Re-asserts hand order in the sibling stack (SetAsLastSibling in order).</summary>
        private void RestoreFanOrder()
        {
            foreach (var v in _views)
                v.Root.SetAsLastSibling();
        }

        /// <summary>Fanned row: center anchored, slight rotation and vertical arc.</summary>
        public void Layout()
        {
            Layout(_hasLaidOut);
        }

        private void Layout(bool animate, float tweenTime = 0.18f)
        {
            Ensure();
            int n = _views.Count;
            if (n == 0)
                return;

            float step = n > 1 ? Mathf.Min(cardWidth * 0.56f, maxSpread / (n - 1)) : 0f;
            float totalWidth = step * (n - 1);
            float rotStep = n > 1 ? rotationPerCard : 0f;

            for (int i = 0; i < n; i++)
            {
                var v = _views[i];
                var rt = v.Root;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                float t = n > 1 ? (float)i / (n - 1) - 0.5f : 0f;   // -0.5..+0.5
                bool sel = v.IsSelected;
                // Selection owns the big lift/scale; hover only adds its smaller pose on
                // cards that are NOT selected, so the two states stay visually distinct.
                bool hov = !sel && ReferenceEquals(v, _hovered);
                float lift = (sel ? selectedLift : 0f) + (hov ? hoverLift : 0f);
                var pos = new Vector2(-totalWidth / 2f + i * step,
                    Mathf.Abs(t) * liftPerCard * 2f + lift);
                float rot = -t * rotStep * 2f * (sel ? 0.25f : 1f); // selected card straightens
                float scale = sel ? selectedScale : hov ? hoverScale : 1f;

                bool jump = !animate ||
                            (rt.anchoredPosition - pos).sqrMagnitude < 1.5f &&
                            Mathf.Abs(NormDeg(rt.localRotation.eulerAngles.z) - NormDeg(rot)) < 0.5f &&
                            Mathf.Abs(rt.localScale.x - scale) < 0.01f;
                if (jump)
                {
                    StopTween(rt);
                    rt.anchoredPosition = pos;
                    rt.localRotation = Quaternion.Euler(0f, 0f, rot);
                    rt.localScale = new Vector3(scale, scale, 1f);
                }
                else
                {
                    StopTween(rt);
                    _tweens[rt] = StartCoroutine(TweenView(rt, pos, rot, scale, tweenTime));
                }
            }
        }

        private IEnumerator TweenView(RectTransform rt, Vector2 pos, float rot, float scale, float duration)
        {
            Vector2 fromPos = rt.anchoredPosition;
            float fromRot = NormDeg(rt.localRotation.eulerAngles.z);
            float fromScale = rt.localScale.x;
            float t = 0f;
            while (t < duration)
            {
                if (rt == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                rt.anchoredPosition = Vector2.LerpUnclamped(fromPos, pos, k);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(fromRot, rot, k));
                float s = Mathf.Lerp(fromScale, scale, k);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (rt != null)
            {
                rt.anchoredPosition = pos;
                rt.localRotation = Quaternion.Euler(0f, 0f, rot);
                rt.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void StopTween(RectTransform rt)
        {
            if (rt != null && _tweens.TryGetValue(rt, out var c) && c != null)
                StopCoroutine(c);
            _tweens.Remove(rt);
        }

        private static float NormDeg(float deg)
        {
            deg %= 360f;
            if (deg > 180f)
                deg -= 360f;
            if (deg < -180f)
                deg += 360f;
            return deg;
        }
    }
}
