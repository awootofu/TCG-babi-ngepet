using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TcgBattle.Gameplay;

namespace TcgBattle.UI
{
    /// <summary>
    /// Fixed, large card inspection on the right side of the battlefield. The supplied
    /// PNG already contains the complete printed card face (border, artwork, name,
    /// printed HP/ATK, effect and flavour text), so the artwork is the entire preview:
    /// no extra panel, status row or duplicated text. It never moves with the pointer
    /// and never intercepts input. Shared by hand cards and face-up board cards.
    /// </summary>
    public class CardInspectPreview : MonoBehaviour
    {
        private const float CardAspect = 1260f / 1760f;

        private RectTransform _rt;
        private CanvasGroup _group;
        private Image _art;
        private float _panelWidth = 380f;
        private CardInstance _shownCard;
        private Coroutine _fade;

        /// <summary>The card whose data is currently previewed, or null.</summary>
        public CardInstance CurrentCard => _shownCard;

        private void Awake()
        {
            EnsureBuilt();
        }

        public void EnsureBuilt()
        {
            if (_group != null)
                return;
            _rt = (RectTransform)transform;

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false; // the preview must never intercept input
            _group.interactable = false;
            _group.alpha = 0f;

            _art = MakeImage("CardFace", null, Color.white);
            _art.preserveAspect = true;
            Stretch(_art.rectTransform, 0, 0, 0, 0);
            ApplyPanelWidth();
        }

        /// <summary>
        /// The surrounding layout gives the card as much width as it can spare without
        /// covering the action row. The image keeps the card aspect, so the entire
        /// preview is always the complete printed face.
        /// </summary>
        public void SetPanelWidth(float width)
        {
            width = Mathf.Clamp(width, 260f, 380f);
            if (Mathf.Abs(width - _panelWidth) < 0.5f)
                return;
            _panelWidth = width;
            if (_rt != null)
                ApplyPanelWidth();
        }

        private void ApplyPanelWidth()
        {
            _rt.sizeDelta = new Vector2(_panelWidth, _panelWidth / CardAspect);
        }

        /// <summary>
        /// Show the preview for a card. Content is rebound without restarting the fade
        /// when another card follows (rapid sweeps restage seamlessly).
        /// </summary>
        public void Show(CardInstance card, RectTransform cardRect)
        {
            if (card == null || card.FaceDown)
            {
                Hide();
                return;
            }

            _shownCard = card;
            _art.sprite = ArtLibrary.GetCardArt(card.ArtResource);

            if (_fade != null)
                StopCoroutine(_fade);
            float aFrom = _group.alpha;
            float sFrom = _rt.localScale.x;
            if (aFrom < 0.99f || !Mathf.Approximately(sFrom, 1f))
            {
                float dur = Mathf.Clamp(0.05f + 0.07f * (1f - aFrom), 0.05f, 0.12f);
                _fade = StartCoroutine(FadeRoutine(aFrom, 1f, sFrom, 1f, dur));
            }
        }

        /// <summary>Hide only if the given card is still the one being previewed.</summary>
        public void HideIfCard(CardInstance card)
        {
            if (card != null && _shownCard == card)
                Hide();
        }

        public void Hide()
        {
            if (_shownCard == null && _group.alpha <= 0.01f)
                return;
            _shownCard = null;
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(_group.alpha, 0f, _rt.localScale.x, 0.94f, 0.10f));
        }

        private IEnumerator FadeRoutine(float aFrom, float aTo, float sFrom, float sTo, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                if (_group == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                _group.alpha = Mathf.Lerp(aFrom, aTo, k);
                float s = Mathf.Lerp(sFrom, sTo, k);
                _rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (_group != null)
            {
                _group.alpha = aTo;
                _rt.localScale = new Vector3(sTo, sTo, 1f);
            }
            _fade = null;
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

        private static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }
    }
}
