using System;
using System.Collections;
using UnityEngine;

namespace TcgBattle.UI
{
    /// <summary>
    /// One temporary face-down card that travels from a deck pile to a hand slot.
    /// The destination is hidden while the card is in flight and fades in on arrival;
    /// the ghost lifts in a shallow arc and fades out at the same time. The effect is
    /// cosmetic, never blocks input, and destroys itself (or reveals the destination
    /// immediately if cancelled by restart/game over).
    /// </summary>
    public class CardDrawFx : MonoBehaviour
    {
        private RectTransform _layer;
        private RectTransform _deck;
        private RectTransform _destination;
        private CanvasGroup _ghostGroup;
        private CanvasGroup _destinationGroup;
        private Vector2 _cardSize;
        private float _duration;
        private float _delay;
        private Action _onArrived;
        private bool _hidDestination;

        public static CardDrawFx Play(RectTransform layer, RectTransform deck,
            RectTransform destination, Vector2 cardSize, float duration, float delay,
            Action onArrived)
        {
            if (destination == null)
            {
                onArrived?.Invoke();
                return null;
            }

            if (layer == null || deck == null)
            {
                Reveal(destination);
                onArrived?.Invoke();
                return null;
            }

            var ghost = CardBackVisual.Create(layer, "DrawCardGhost", cardSize);
            ghost.SetAsLastSibling();
            var group = ghost.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;
            group.ignoreParentGroups = true;

            var fx = ghost.gameObject.AddComponent<CardDrawFx>();
            fx._layer = layer;
            fx._deck = deck;
            fx._destination = destination;
            fx._ghostGroup = group;
            fx._cardSize = cardSize;
            fx._duration = Mathf.Max(0.01f, duration);
            fx._delay = Mathf.Max(0f, delay);
            fx._onArrived = onArrived;
            fx.HideDestination();
            fx.StartCoroutine(fx.Run());
            return fx;
        }

        /// <summary>Immediately reveal the hand slot and remove the moving card.</summary>
        public void Cancel()
        {
            if (this == null)
                return;
            if (_hidDestination)
                Reveal(_destination);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void HideDestination()
        {
            if (_destination == null)
                return;
            _destinationGroup = _destination.GetComponent<CanvasGroup>();
            if (_destinationGroup == null)
                _destinationGroup = _destination.gameObject.AddComponent<CanvasGroup>();
            _destinationGroup.alpha = 0f;
            _destinationGroup.interactable = false;
            _destinationGroup.blocksRaycasts = false;
            _hidDestination = true;
        }

        private IEnumerator Run()
        {
            if (_delay > 0f)
                yield return new WaitForSecondsRealtime(_delay);

            if (_destination == null || _deck == null || _layer == null)
            {
                Destroy(gameObject);
                yield break;
            }

            Vector2 from = _layer.InverseTransformPoint(_deck.position);
            float startRotation = NormDeg(_destination.localRotation.eulerAngles.z) + 6f;
            float lift = Mathf.Min(48f, _cardSize.y * 0.20f);
            var rt = (RectTransform)transform;
            rt.anchoredPosition = from;
            rt.localRotation = Quaternion.Euler(0f, 0f, startRotation);
            rt.localScale = Vector3.one * 0.93f;

            float t = 0f;
            while (t < _duration)
            {
                if (rt == null)
                    yield break;
                if (_destination == null)
                {
                    Destroy(gameObject);
                    yield break;
                }

                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _duration));

                // Follow the live hand slot: the fan may be tweening to a new layout
                // while the card is in flight.
                Vector2 to = _layer.InverseTransformPoint(_destination.position);
                float destRotation = NormDeg(_destination.localRotation.eulerAngles.z);
                float arc = Mathf.Sin(k * Mathf.PI) * lift;

                rt.SetAsLastSibling();
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k) + new Vector2(0f, arc);
                rt.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.LerpAngle(startRotation, destRotation, k));
                rt.localScale = Vector3.one * Mathf.Lerp(0.93f, 1f, k);

                const float fadeStart = 0.68f;
                if (k < fadeStart)
                {
                    _ghostGroup.alpha = 1f;
                    if (_destinationGroup != null)
                        _destinationGroup.alpha = 0f;
                }
                else
                {
                    float fade = Mathf.InverseLerp(fadeStart, 1f, k);
                    _ghostGroup.alpha = 1f - fade;
                    if (_destinationGroup != null)
                        _destinationGroup.alpha = fade;
                }

                yield return null;
            }

            if (_destination != null)
                Reveal(_destination);
            _onArrived?.Invoke();
            Destroy(gameObject);
        }

        private static void Reveal(RectTransform destination)
        {
            if (destination == null)
                return;
            var group = destination.GetComponent<CanvasGroup>();
            if (group == null)
                group = destination.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        private void OnDestroy()
        {
            if (_hidDestination)
                Reveal(_destination);
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
