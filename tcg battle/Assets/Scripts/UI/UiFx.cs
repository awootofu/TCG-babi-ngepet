using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TcgBattle.UI
{
    /// <summary>
    /// Tiny coroutine-based tween/FX helpers. All tweens are fire-and-forget and cosmetic:
    /// battle state is authoritative and updates independently of animations.
    /// Everything animates on unscaled time so the UI keeps moving even if the game is paused.
    /// </summary>
    public static class UiFx
    {
        public static IEnumerator Move(RectTransform target, Vector2 to, float duration,
            Action done = null, float overshoot = 0f)
        {
            if (target == null)
                yield break;
            Vector2 from = target.anchoredPosition;
            float t = 0f;
            if (duration <= 0f)
            {
                target.anchoredPosition = to;
                done?.Invoke();
                yield break;
            }
            while (t < duration)
            {
                if (target == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float eased = OvershootEase(k, overshoot);
                target.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
                yield return null;
            }
            if (target != null)
                target.anchoredPosition = to;
            done?.Invoke();
        }

        public static IEnumerator Fade(Graphic graphic, float toAlpha, float duration, Action done = null)
        {
            if (graphic == null)
                yield break;
            float from = graphic.color.a;
            float t = 0f;
            while (t < duration)
            {
                if (graphic == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                graphic.color = FadeColor(graphic, Mathf.Lerp(from, toAlpha, Mathf.SmoothStep(0f, 1f, t / duration)));
                yield return null;
            }
            if (graphic != null)
            {
                graphic.color = FadeColor(graphic, toAlpha);
                done?.Invoke();
            }
        }

        private static Color FadeColor(Graphic g, float a)
        {
            var c = g.color;
            c.a = a;
            return c;
        }

        public static IEnumerator TweenHpBar(Image fill, float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                if (fill == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                fill.fillAmount = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            if (fill != null)
                fill.fillAmount = to;
        }

        /// <summary>Scale punch used for score pips, energy orbs and banners.</summary>
        public static IEnumerator Punch(RectTransform target, float scaleTo, float duration)
        {
            if (target == null)
                yield break;
            Vector3 from = target.localScale;
            float t = 0f;
            while (t < duration)
            {
                if (target == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                float k = t / duration;
                float v = 1f + (scaleTo - 1f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(k));
                target.localScale = from * v;
                yield return null;
            }
            if (target != null)
                target.localScale = from;
        }

        public static IEnumerator Shake(RectTransform target, float strength, float duration)
        {
            if (target == null)
                yield break;
            Vector2 basePos = target.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                if (target == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                float decay = 1f - t / duration;
                float x = Mathf.Sin(t * 90f) * strength * decay;
                target.anchoredPosition = basePos + new Vector2(x, 0f);
                yield return null;
            }
            if (target != null)
                target.anchoredPosition = basePos;
        }

        /// <summary>
        /// One-shot impact flash: pops to startAlpha then fades to zero and destroys the object.
        /// </summary>
        public static IEnumerator FlashOut(Graphic graphic, float duration, float startAlpha)
        {
            if (graphic == null)
                yield break;
            var c = graphic.color;
            c.a = startAlpha;
            graphic.color = c;
            float t = 0f;
            while (t < duration)
            {
                if (graphic == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                var col = graphic.color;
                col.a = startAlpha * (1f - Mathf.Clamp01(t / duration));
                graphic.color = col;
                yield return null;
            }
            if (graphic != null)
                UnityEngine.Object.Destroy(graphic.gameObject);
        }

        /// <summary>0.5, 1.4, 1 (overshoot with spring feel). overshoot 0 = plain smoothstep.</summary>
        private static float OvershootEase(float t, float overshoot)
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            if (overshoot <= 0f)
                return s;
            return s + overshoot * Mathf.Sin(Mathf.PI * t) * (1f - t);
        }
    }

    /// <summary>
    /// Press feedback for buttons: slight shrink while pressed, back to normal on release.
    /// Ignored on disabled buttons so dead buttons stay still.
    /// </summary>
    public class ButtonFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float pressScale = 0.955f;

        private RectTransform _rt;
        private Vector3 _base = Vector3.one;
        private Button _btn;
        private Coroutine _anim;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _base = _rt.localScale;
            _btn = GetComponent<Button>();
        }

        private bool CanReact => _btn == null || _btn.interactable;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (CanReact)
                Animate(pressScale, 0.05f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (CanReact)
                Animate(1f, 0.1f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (CanReact)
                Animate(1f, 0.1f);
        }

        private void Animate(float target, float duration)
        {
            if (_anim != null)
                StopCoroutine(_anim);
            _anim = StartCoroutine(Anim(target, duration));
        }

        private IEnumerator Anim(float target, float duration)
        {
            Vector3 start = _rt.localScale;
            Vector3 end = _base * target;
            float t = 0f;
            while (t < duration)
            {
                if (_rt == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                _rt.localScale = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            if (_rt != null)
                _rt.localScale = end;
        }
    }
}
