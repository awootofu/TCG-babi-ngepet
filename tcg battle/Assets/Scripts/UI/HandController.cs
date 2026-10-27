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
        public float cardWidth = 176f;
        public float cardHeight = 236f;
        public float maxSpread = 1450f;         // total row width before cards overlap harder
        public float rotationPerCard = 2.4f;    // degrees at the edges
        public float liftPerCard = 5f;          // parabolic arc
        public float selectedLift = 30f;
        public float selectedScale = 1.05f;
        public float hoverLift = 14f;      // subtle lift while hovered (selected still wins)
        public float hoverScale = 1.03f;
        public float eligibleLift = 10f;   // "legal pick" hint while choosing a starting active
        public float eligibleScale = 1.02f;

        private readonly List<CardView> _views = new List<CardView>();
        private readonly Dictionary<RectTransform, Coroutine> _tweens = new Dictionary<RectTransform, Coroutine>();
        private RectTransform _rect;
        private Action<CardView> _onClick;
        private bool _hasLaidOut;
        private CardView _hovered;         // hand view currently under the pointer, if any
        private const float HoverTweenTime = 0.12f;
        private CardInspectPreview _inspectPreview;   // hover inspection overlay (built by BattleUI)

        // drag-and-drop state (hand views only; board cards never get CardDragHandler)
        private CardView _dragging;
        private Vector2 _dragGrabOffset;
        private float _lastDragEventTime;
        private float _lastDragEndedTime = -10f;
        private bool _dragEnabled = true;
        private Action<CardView, Vector2> _dragStarted;
        private Action<CardView, Vector2> _dragMoved;
        private Func<CardView, Vector2, bool> _drop;
        private Action _dragFinished;

        // reference-scale card sizing (canvas height 1080); cardWidth/cardHeight hold the
        // currently applied (scaled) values used for fan layout
        private Vector2 _baseCard;
        private float _baseSpread, _baseLift, _baseSelLift, _baseHoverLift, _baseEligibleLift;
        private float _viewportWidth;      // 0 = use maxSpread unchanged

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
            _baseEligibleLift = eligibleLift;
        }

        /// <summary>Apply a uniform layout scale (canvas height / 1080) to the fan.</summary>
        public void SetScale(float k)
        {
            Ensure();
            cardWidth = _baseCard.x * k;
            cardHeight = _baseCard.y * k;
            maxSpread = _baseSpread * k;
            liftPerCard = _baseLift * k;
            selectedLift = _baseSelLift * k;
            hoverLift = _baseHoverLift * k;
            eligibleLift = _baseEligibleLift * k;
            Layout(_hasLaidOut);
        }

        /// <summary>
        /// Clamp the fan to the usable middle of a wider/narrower landscape window.
        /// The side insets keep the right-rail stamina badge and the left info rail clear.
        /// </summary>
        public void SetViewportWidth(float width)
        {
            Ensure();
            width = Mathf.Max(0f, width);
            if (Mathf.Abs(width - _viewportWidth) < 0.5f)
                return;
            _viewportWidth = width;
            Layout(_hasLaidOut);
        }

        public void SetClickHandler(Action<CardView> onClick)
        {
            _onClick = onClick;
        }

        /// <summary>Wires the hover inspection overlay (built by BattleUI).</summary>
        public void SetInspectPreview(CardInspectPreview preview)
        {
            _inspectPreview = preview;
        }

        /// <summary>Used by BattleUI to drop the preview when the battle ends.</summary>
        public void HideInspectPreview()
        {
            if (_inspectPreview != null)
                _inspectPreview.Hide();
        }

        /// <summary>
        /// BattleUI supplies the authoritative drop decision and the slot-highlight
        /// callbacks. HandController only owns the visual drag/return behaviour.
        /// </summary>
        public void SetDragHandlers(Action<CardView, Vector2> started,
            Action<CardView, Vector2> moved, Func<CardView, Vector2, bool> drop,
            Action finished)
        {
            _dragStarted = started;
            _dragMoved = moved;
            _drop = drop;
            _dragFinished = finished;
        }

        /// <summary>
        /// Drag is legal only during setup/player turn. Disabling it also cancels an
        /// in-flight drag so a turn change or restart can never strand a lifted card.
        /// </summary>
        public void SetDragEnabled(bool enabled)
        {
            if (_dragEnabled == enabled)
                return;
            _dragEnabled = enabled;
            foreach (var v in _views)
            {
                if (v == null)
                    continue;
                var drag = v.GetComponent<CardDragHandler>();
                if (drag != null)
                    drag.enabled = enabled;
            }
            if (!enabled && _dragging != null)
                CancelDrag();
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
                if (ReferenceEquals(_dragging, _views[i]))
                {
                    _dragging = null;
                    _dragFinished?.Invoke();
                }
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
                var drag = view.gameObject.AddComponent<CardDragHandler>();
                drag.Bind(view);
                drag.Started += OnViewDragStarted;
                drag.Moved += OnViewDragged;
                drag.Ended += OnViewDragEnded;
                drag.enabled = _dragEnabled;
                _views.Add(view);
            }

            // Rebind (order may have changed).
            for (int i = 0; i < _views.Count; i++)
            {
                _views[i].Bind(hand[i], CardDisplayMode.Hand);
                _views[i].SetSelected(false);
                _views[i].Root.SetAsLastSibling();

                var drag = _views[i].GetComponent<CardDragHandler>();
                if (drag == null)
                {
                    drag = _views[i].gameObject.AddComponent<CardDragHandler>();
                    drag.Bind(_views[i]);
                    drag.Started += OnViewDragStarted;
                    drag.Moved += OnViewDragged;
                    drag.Ended += OnViewDragEnded;
                }
                drag.enabled = _dragEnabled;
            }

            // A rebind during an in-flight drag must not put the lifted card back
            // under its neighbours.
            if (_dragging != null)
                _dragging.Root.SetAsLastSibling();

            // Bind reset per-card state; if the pointer is still on the same view, re-assert
            // hover (visuals + raise) before the layout below retargets the fan.
            if (_hovered != null)
            {
                _hovered.SetHovered(true);
                _hovered.Root.SetAsLastSibling();
            }

            // Inspection preview: if the card it shows just left the hand, drop it.
            if (_inspectPreview != null && _inspectPreview.CurrentCard != null &&
                !hand.Contains(_inspectPreview.CurrentCard))
                _inspectPreview.Hide();

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
            if (_dragging != null)
                return; // inspection is suspended for the whole drag gesture

            if (entered)
            {
                if (_hovered == view)
                    return;
                if (_hovered != null && _hovered != view)
                    _hovered.SetHovered(false); // that view's exit event may arrive late or never
                _hovered = view;
                view.Root.SetAsLastSibling();   // hovered card wins the overlap fight
                if (_inspectPreview != null && view.Card != null)
                    _inspectPreview.Show(view.Card, view.Root);
            }
            else
            {
                if (_hovered != view)
                    return;                     // stale exit after Sync already cleared it
                _hovered = null;
                RestoreFanOrder();
                if (_inspectPreview != null && view.Card != null)
                    _inspectPreview.HideIfCard(view.Card);
            }
            Layout(true, HoverTweenTime);
        }

        public bool IsDragging => _dragging != null;

        /// <summary>
        /// Suppresses the synthetic click that can follow a drag release in the same
        /// frame. The gameplay action must be invoked exactly once.
        /// </summary>
        public bool ShouldIgnoreClick => Time.unscaledTime - _lastDragEndedTime < 0.2f;

        private void Update()
        {
            // A release outside the window may never reach OnEndDrag. The short grace
            // period gives the EventSystem time to deliver a normal drop first, so a
            // valid release is never converted into a cancel.
            if (_dragging != null && !Input.GetMouseButton(0) &&
                Time.unscaledTime - _lastDragEventTime > 0.2f)
                CancelDrag();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && _dragging != null)
                CancelDrag();
        }

        private void OnViewDragStarted(CardView view, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (!_dragEnabled || _dragging != null || view == null ||
                view.Mode != CardDisplayMode.Hand || !_views.Contains(view))
                return;

            _dragging = view;
            _lastDragEventTime = Time.unscaledTime;
            StopTween(view.Root);
            if (_hovered == view)
                _hovered = null;
            if (_inspectPreview != null)
                _inspectPreview.Hide();

            // Keep the fan order for every other card, then raise the dragged one.
            RestoreFanOrder();
            view.Root.SetAsLastSibling();
            view.SetHovered(false);
            view.Root.localRotation = Quaternion.identity;
            view.Root.localScale = Vector3.one * 1.07f;
            view.Root.anchoredPosition += new Vector2(0f, 8f);

            _dragGrabOffset = Vector2.zero;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rect, eventData.position, eventData.pressEventCamera, out var local))
                _dragGrabOffset = view.Root.anchoredPosition - local;

            _dragStarted?.Invoke(view, eventData.position);
            _dragMoved?.Invoke(view, eventData.position);
        }

        private void OnViewDragged(CardView view, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_dragging != view)
                return;
            _lastDragEventTime = Time.unscaledTime;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rect, eventData.position, eventData.pressEventCamera, out var local))
                view.Root.anchoredPosition = local + _dragGrabOffset;
            view.Root.SetAsLastSibling();
            _dragMoved?.Invoke(view, eventData.position);
        }

        private void OnViewDragEnded(CardView view, UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_dragging != view)
                return;

            _dragging = null;
            _lastDragEndedTime = Time.unscaledTime;
            _dragFinished?.Invoke();

            bool accepted = _drop != null && _drop(view, eventData.position);
            if (!accepted || (_views.Contains(view) && view.Card != null))
            {
                RestoreFanOrder();
                Layout(true);
            }
        }

        private void CancelDrag()
        {
            if (_dragging == null)
                return;
            var view = _dragging;
            _dragging = null;
            _lastDragEndedTime = Time.unscaledTime;
            _dragFinished?.Invoke();
            if (view != null && _views.Contains(view))
            {
                RestoreFanOrder();
                Layout(true);
            }
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

            float spread = _viewportWidth > 0f ? Mathf.Min(maxSpread, _viewportWidth) : maxSpread;
            float step = n > 1 ? Mathf.Min(cardWidth * 0.80f, spread / (n - 1)) : 0f;
            float totalWidth = step * (n - 1);
            float rotStep = n > 1 ? rotationPerCard : 0f;

            for (int i = 0; i < n; i++)
            {
                var v = _views[i];
                if (ReferenceEquals(v, _dragging))
                    continue; // the drag gesture owns this card's pose
                var rt = v.Root;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                float t = n > 1 ? (float)i / (n - 1) - 0.5f : 0f;   // -0.5..+0.5
                bool sel = v.IsSelected;
                // Selection owns the big lift/scale; hover only adds its smaller pose on
                // cards that are NOT selected; setup eligibility adds a light, persistent
                // lift so legal starting picks read at a glance without a second "selected".
                bool hov = !sel && ReferenceEquals(v, _hovered);
                bool elig = !sel && v.IsEligible; // hover adds to the pick pose, never drops it
                float lift = (sel ? selectedLift : 0f) + (hov ? hoverLift : 0f) + (elig ? eligibleLift : 0f);
                var pos = new Vector2(-totalWidth / 2f + i * step,
                    Mathf.Abs(t) * liftPerCard * 2f + lift);
                float rot = -t * rotStep * 2f * (sel ? 0.25f : 1f); // selected card straightens
                float scale = sel ? selectedScale : hov ? hoverScale : elig ? eligibleScale : 1f;

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
