using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TcgBattle.Gameplay;

namespace TcgBattle.UI
{
    /// <summary>
    /// Reusable cosmetic reveal for an already-committed evolution or transformation.
    /// A throwaway CardView ghost shows the old face, charges up with a gold halo and
    /// exactly two flat Z rotations, swaps to the new face at the flash peak, then
    /// settles back into the slot. The real board view is only alpha-hidden for the
    /// duration; its transform, sorting, raycasts and CardInstance are never touched.
    /// </summary>
    public class CardFormFx : MonoBehaviour
    {
        private static readonly Color ColGoldBright = Hex(0xF2C14E);
        private static readonly Color ColGoldSoft = new Color(0.95f, 0.76f, 0.35f, 0.95f);
        private static readonly Color ColIvory = Hex(0xF2EADA);
        private static readonly Color ColBanner = new Color(0.10f, 0.07f, 0.18f, 0.94f);

        private CardView _target;
        private CardInstance _expectedCard;
        private CardVisualSnapshot _oldVisual;
        private CardVisualSnapshot _newVisual;
        private CardDisplayMode _mode;
        private float _k;
        private string _heading;
        private string _path;
        private Action<CardFormFx> _finished;

        private RectTransform _root;
        private RectTransform _ghostRt;
        private CardView _ghost;
        private CanvasGroup _targetGroup;
        private CanvasGroup _bannerGroup;
        private Image _glow;
        private Image _outline;
        private float _targetAlpha = 1f;
        private bool _cleaned;

        public static CardFormFx Play(RectTransform layer, CardView target,
            CardVisualSnapshot oldVisual, CardVisualSnapshot newVisual,
            string heading, string path, float k, Color accent,
            Action<CardFormFx> finished = null)
        {
            if (layer == null || target == null || target.Root == null || target.Card == null)
                return null;

            var go = new GameObject("CardFormFx", typeof(RectTransform));
            var fx = go.AddComponent<CardFormFx>();
            fx.Init(layer, target, oldVisual, newVisual, heading, path, k, accent, finished);
            return fx;
        }

        private void Init(RectTransform layer, CardView target,
            CardVisualSnapshot oldVisual, CardVisualSnapshot newVisual,
            string heading, string path, float k, Color accent,
            Action<CardFormFx> finished)
        {
            _target = target;
            _expectedCard = target.Card;
            _oldVisual = oldVisual;
            _newVisual = newVisual;
            _mode = target.Mode;
            _k = Mathf.Max(0.3f, k);
            _heading = heading;
            _path = path;
            _finished = finished;

            _targetGroup = target.GetComponent<CanvasGroup>();
            if (_targetGroup == null)
                _targetGroup = target.gameObject.AddComponent<CanvasGroup>();
            _targetAlpha = _targetGroup.alpha;
            _targetGroup.alpha = 0f;

            Build(layer, accent);
            StartCoroutine(Run());
        }

        private void Build(RectTransform layer, Color accent)
        {
            _root = (RectTransform)transform;
            _root.SetParent(layer, false);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);

            Vector2 cardSize = _target.Root.rect.size;
            _root.sizeDelta = cardSize;
            var slot = _target.Root.parent as RectTransform;
            _root.anchoredPosition = layer.InverseTransformPoint((slot != null ? slot : _target.Root).position);

            _glow = MakeImage(_root, "Glow", UiSpriteFactory.SoftShadow,
                WithA(accent, 0f), cardSize + new Vector2(170f, 170f) * _k, Vector2.zero);
            _outline = MakeImage(_root, "GlowRing", UiSpriteFactory.RoundOutline,
                WithA(ColGoldSoft, 0f), cardSize + new Vector2(24f, 24f) * _k, Vector2.zero);
            _outline.type = Image.Type.Sliced;

            _ghost = CardView.Create(_root, cardSize, "FormGhost");
            _ghostRt = _ghost.Root;
            _ghostRt.anchorMin = _ghostRt.anchorMax = new Vector2(0.5f, 0.5f);
            _ghostRt.pivot = new Vector2(0.5f, 0.5f);
            _ghostRt.anchoredPosition = Vector2.zero;
            _ghostRt.sizeDelta = cardSize;
            _ghost.ApplyScale(_k);
            _ghost.BindVisual(_oldVisual, _mode);

            var ghostGroup = _ghost.gameObject.AddComponent<CanvasGroup>();
            ghostGroup.interactable = false;
            ghostGroup.blocksRaycasts = false;

            BuildBanner(cardSize);
        }

        private void BuildBanner(Vector2 cardSize)
        {
            float width = Mathf.Max(cardSize.x + 90f * _k, 330f * _k);
            float height = 78f * _k;
            float y = -cardSize.y * 0.33f;

            var go = new GameObject("FormBanner", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, y);

            var bg = go.GetComponent<Image>();
            bg.sprite = UiSpriteFactory.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = ColBanner;
            bg.raycastTarget = false;

            var border = MakeImage(rt, "Border", UiSpriteFactory.RoundOutline,
                WithA(ColGoldSoft, 0.75f), new Vector2(width, height), Vector2.zero);
            border.type = Image.Type.Sliced;

            var heading = MakeText(rt, "Heading", 25f * _k, ColGoldBright,
                TextAlignmentOptions.Center, true);
            heading.rectTransform.sizeDelta = new Vector2(width - 20f * _k, 34f * _k);
            heading.rectTransform.anchoredPosition = new Vector2(0f, 14f * _k);
            heading.text = _heading;

            var path = MakeText(rt, "Path", 14f * _k, ColIvory,
                TextAlignmentOptions.Center, false);
            path.rectTransform.sizeDelta = new Vector2(width - 16f * _k, 24f * _k);
            path.rectTransform.anchoredPosition = new Vector2(0f, -18f * _k);
            path.text = _path;
            path.enableAutoSizing = true;
            path.fontSizeMin = 9f * _k;
            path.fontSizeMax = 14f * _k;

            _bannerGroup = go.GetComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
            _bannerGroup.interactable = false;
            _bannerGroup.blocksRaycasts = false;
        }

        private IEnumerator Run()
        {
            const float charge = 0.55f;
            const float settle = 0.28f;
            const float hold = 0.20f;
            const float fadeOut = 0.18f;

            float t = 0f;
            while (t < charge)
            {
                if (!TargetValid())
                {
                    Cleanup();
                    yield break;
                }
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / charge);
                float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p / 0.35f));
                float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p / 0.30f));
                float spin = Mathf.SmoothStep(0f, 1f, p);

                _ghostRt.anchoredPosition = new Vector2(0f, 26f * _k * lift);
                _ghostRt.localScale = Vector3.one * Mathf.Lerp(1f, 1.10f, grow);
                _ghostRt.localRotation = Quaternion.Euler(0f, 0f, 720f * spin);

                float glow = Mathf.Clamp01(p / 0.25f);
                float pulse = 0.85f + 0.15f * Mathf.Sin(p * Mathf.PI * 3f);
                SetGlow(glow * 0.55f * pulse, glow * 0.85f);
                yield return null;
            }

            if (!TargetValid())
            {
                Cleanup();
                yield break;
            }

            // Exactly two full turns are complete at the flash peak. 720 degrees is
            // visually identical to zero, so the face swap cannot snap the rotation.
            _ghostRt.localRotation = Quaternion.Euler(0f, 0f, 720f);
            _ghostRt.anchoredPosition = new Vector2(0f, 26f * _k);
            _ghostRt.localScale = Vector3.one * 1.10f;
            _ghost.BindVisual(_newVisual, _mode);
            SpawnFlash();

            StartCoroutine(FadeGroup(_bannerGroup, 1f, 0.12f));

            Vector2 fromPos = _ghostRt.anchoredPosition;
            Vector3 fromScale = _ghostRt.localScale;
            t = 0f;
            while (t < settle)
            {
                if (!TargetValid())
                {
                    Cleanup();
                    yield break;
                }
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / settle);
                float e = Mathf.SmoothStep(0f, 1f, p);
                _ghostRt.anchoredPosition = Vector2.Lerp(fromPos, Vector2.zero, e);
                _ghostRt.localScale = Vector3.Lerp(fromScale, Vector3.one, e);
                _ghostRt.localRotation = Quaternion.identity;
                SetGlow(Mathf.Lerp(0.55f, 0f, e), Mathf.Lerp(0.85f, 0f, e));
                yield return null;
            }

            _ghostRt.anchoredPosition = Vector2.zero;
            _ghostRt.localScale = Vector3.one;
            _ghostRt.localRotation = Quaternion.identity;

            yield return new WaitForSecondsRealtime(hold);
            if (!TargetValid())
            {
                Cleanup();
                yield break;
            }
            yield return FadeGroup(_bannerGroup, 0f, fadeOut);
            Cleanup();
        }

        private bool TargetValid()
        {
            return !_cleaned && _target != null && _expectedCard != null &&
                   ReferenceEquals(_target.Card, _expectedCard) &&
                   !_expectedCard.IsKnockedOut && _target.gameObject.activeInHierarchy;
        }

        private void SetGlow(float softAlpha, float ringAlpha)
        {
            if (_glow != null)
                _glow.color = WithA(_glow.color, softAlpha);
            if (_outline != null)
                _outline.color = WithA(_outline.color, ringAlpha);
        }

        private void SpawnFlash()
        {
            Vector2 cardSize = _target.Root.rect.size;
            var soft = MakeImage(_root, "FormFlash", UiSpriteFactory.SoftShadow,
                new Color(1f, 0.92f, 0.62f, 0.85f),
                cardSize + new Vector2(180f, 180f) * _k, Vector2.zero);
            soft.transform.SetAsLastSibling();
            StartCoroutine(FlashRoutine(soft.rectTransform, soft, 0.55f, 1.45f, 0.24f));

            var ring = MakeImage(_root, "FormFlashRing", UiSpriteFactory.RoundOutline,
                WithA(ColGoldBright, 0.95f),
                cardSize + new Vector2(34f, 34f) * _k, Vector2.zero);
            ring.type = Image.Type.Sliced;
            ring.transform.SetAsLastSibling();
            StartCoroutine(FlashRoutine(ring.rectTransform, ring, 0.96f, 1.34f, 0.28f));
        }

        private IEnumerator FlashRoutine(RectTransform rt, Image img,
            float startScale, float endScale, float duration)
        {
            if (rt == null || img == null)
                yield break;
            Color baseColor = img.color;
            float t = 0f;
            while (t < duration)
            {
                if (rt == null || img == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                float s = Mathf.Lerp(startScale, endScale, Mathf.SmoothStep(0f, 1f, p));
                rt.localScale = new Vector3(s, s, 1f);
                img.color = WithA(baseColor, baseColor.a * (1f - p));
                yield return null;
            }
            if (rt != null)
                Destroy(rt.gameObject);
        }

        private IEnumerator FadeGroup(CanvasGroup group, float to, float duration)
        {
            if (group == null)
                yield break;
            float from = group.alpha;
            float t = 0f;
            while (t < duration)
            {
                if (group == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            if (group != null)
                group.alpha = to;
        }

        public void CancelImmediate()
        {
            if (_cleaned)
                return;
            Cleanup();
        }

        private void Cleanup()
        {
            if (_cleaned)
                return;
            _cleaned = true;
            StopAllCoroutines();

            if (_targetGroup != null)
                _targetGroup.alpha = _targetAlpha;

            var cb = _finished;
            _finished = null;
            if (gameObject != null)
            {
                // Hide immediately so a newer form FX cannot render on top of the
                // old ghost during the one-frame deferred Destroy window.
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
            cb?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (_cleaned)
                return;
            _cleaned = true;
            StopAllCoroutines();
            if (_targetGroup != null)
                _targetGroup.alpha = _targetAlpha;
        }

        private static Image MakeImage(RectTransform parent, string name, Sprite sprite,
            Color color, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TMP_Text MakeText(RectTransform parent, string name, float fontSize,
            Color color, TextAlignmentOptions align, bool bold)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
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

        private static Color WithA(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        private static Color Hex(int rgba)
        {
            return new Color32((byte)(rgba >> 16 & 0xFF), (byte)(rgba >> 8 & 0xFF), (byte)(rgba & 0xFF), 0xFF);
        }
    }
}
