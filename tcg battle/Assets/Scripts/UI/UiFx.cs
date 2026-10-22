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
    /// Hover / press / disabled feedback for buttons.
    /// Owns the background image color and the TMP label color (crossfaded per state),
    /// plus the press-shrink animation. BattleUI.MakeButton sets the Button's built-in
    /// transition to None so nothing else drives the same graphics.
    /// States: disabled mutes background+label and ignores input; pressed dims slightly
    /// (alongside the shrink); hovered shifts the background toward a warm gold accent —
    /// chosen so the shift is visible on the ivory, gold, and ink-purple button colors alike.
    /// </summary>
    public class ButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float pressScale = 0.955f;
        [SerializeField] private float hoverAccent = 0.22f; // 0 = no shift, 1 = full accent
        [SerializeField] private float pressDim = 0.88f;   // pressed brightness multiplier (rgb)
        [SerializeField] private float colorFadeTime = 0.08f;

        private static readonly Color HoverAccent = Hex(0xC08A2E); // warm gold-ochre
        private static readonly Color MutedBg = Hex(0x5C5470);     // muted purple, keeps the palette
        private static readonly Color MutedLabel = Hex(0x9A92AC);

        private Image _bg;
        private TMPro.TMP_Text _label;
        private RectTransform _rt;
        private Vector3 _base = Vector3.one;
        private Button _btn;
        private Coroutine _scaleAnim;
        private Coroutine _colorAnim;

        private Color _bgBase = Color.white;
        private Color _labelBase = Color.white;
        private bool _hovering;
        private bool _pressed;
        private bool _lastInteractable = true;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _base = _rt.localScale;
            _btn = GetComponent<Button>();
            _bg = GetComponent<Image>();
            _label = _rt.GetComponentInChildren<TMPro.TMP_Text>();
            if (_bg != null) _bgBase = _bg.color;
            if (_label != null) _labelBase = _label.color;
        }

        private void OnEnable()
        {
            // Pointer state and press-scale cannot be trusted across deactivation: a
            // click handler can hide the overlay before PointerUp arrives, and that
            // deactivation also stops the scale coroutine mid-shrink. Snap both back.
            _hovering = false;
            _pressed = false;
            if (_scaleAnim != null)
            {
                StopCoroutine(_scaleAnim);
                _scaleAnim = null;
            }
            if (_rt != null)
                _rt.localScale = _base;
            Refresh(instant: true);
        }

        private void Update()
        {
            if (_btn == null || _btn.interactable == _lastInteractable)
                return;
            _lastInteractable = _btn.interactable;
            if (!_btn.interactable)
            {
                _pressed = false; // a dead button never stays shrunk
                AnimateScale(1f, 0.08f);
            }
            Refresh(instant: false);
        }

        private bool Interactable => _btn == null || _btn.interactable;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovering = true;
            Refresh(instant: false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovering = false;
            Refresh(instant: false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable)
                return;
            _pressed = true;
            Refresh(instant: false);
            AnimateScale(pressScale, 0.05f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!Interactable)
                return;
            _pressed = false;
            Refresh(instant: false);
            AnimateScale(1f, 0.1f);
        }

        private void Refresh(bool instant)
        {
            if (_bg == null && _label == null)
                return;

            var bgTarget = _bgBase;
            var labelTarget = _labelBase;
            if (!Interactable)
            {
                bgTarget = LerpRgb(_bgBase, MutedBg, 0.40f);
                bgTarget.a = _bgBase.a * 0.55f;
                labelTarget = LerpRgb(_labelBase, MutedLabel, 0.50f);
                labelTarget.a = _labelBase.a * 0.42f;
            }
            else
            {
                float accent = _pressed ? hoverAccent * 0.6f : (_hovering ? hoverAccent : 0f);
                bgTarget = LerpRgb(_bgBase, HoverAccent, accent);
                if (_pressed)
                    bgTarget = new Color(bgTarget.r * pressDim, bgTarget.g * pressDim, bgTarget.b * pressDim, bgTarget.a);
            }

            CrossFadeColors(bgTarget, labelTarget, instant ? 0f : colorFadeTime);
        }

        private static Color LerpRgb(Color from, Color to, float t)
        {
            return new Color(
                Mathf.Lerp(from.r, to.r, t),
                Mathf.Lerp(from.g, to.g, t),
                Mathf.Lerp(from.b, to.b, t),
                from.a);
        }

        private void CrossFadeColors(Color bgTarget, Color labelTarget, float duration)
        {
            if (_colorAnim != null)
                StopCoroutine(_colorAnim);
            if (duration <= 0f)
            {
                ApplyColors(bgTarget, labelTarget);
                _colorAnim = null;
                return;
            }
            _colorAnim = StartCoroutine(ColorAnim(bgTarget, labelTarget, duration));
        }

        private void ApplyColors(Color bg, Color label)
        {
            if (_bg != null)
                _bg.color = bg;
            if (_label != null)
                _label.color = label;
        }

        private IEnumerator ColorAnim(Color bgTarget, Color labelTarget, float duration)
        {
            Color bgFrom = _bg != null ? _bg.color : Color.white;
            Color labelFrom = _label != null ? _label.color : Color.white;
            float t = 0f;
            while (t < duration)
            {
                if (_bg == null && _label == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                ApplyColors(
                    _bg != null ? Color.Lerp(bgFrom, bgTarget, k) : bgTarget,
                    _label != null ? Color.Lerp(labelFrom, labelTarget, k) : labelTarget);
                yield return null;
            }
            ApplyColors(bgTarget, labelTarget);
            _colorAnim = null;
        }

        private void AnimateScale(float target, float duration)
        {
            if (_scaleAnim != null)
                StopCoroutine(_scaleAnim);
            _scaleAnim = StartCoroutine(ScaleAnim(target, duration));
        }

        private IEnumerator ScaleAnim(float target, float duration)
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

        private static Color Hex(int rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                1f);
        }
    }
}
