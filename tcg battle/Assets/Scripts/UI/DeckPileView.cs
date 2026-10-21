using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TcgBattle.UI
{
    /// <summary>
    /// Small, clearly visible deck pile for either side: three offset card backs plus
    /// a "DEK n" counter that stays next to the pile and updates on every draw.
    /// Purely cosmetic; BattleManager remains the source of truth for the count.
    /// </summary>
    public class DeckPileView : MonoBehaviour
    {
        private RectTransform _stack;
        private TMP_Text _countText;
        private int _lastCount = -1;

        public void Build()
        {
            var root = (RectTransform)transform;

            var stackGo = new GameObject("Stack", typeof(RectTransform));
            _stack = (RectTransform)stackGo.transform;
            _stack.SetParent(root, false);
            _stack.anchorMin = _stack.anchorMax = new Vector2(0.5f, 0.5f);
            _stack.pivot = new Vector2(0.5f, 0.5f);
            _stack.sizeDelta = new Vector2(74f, 100f);
            _stack.anchoredPosition = Vector2.zero;

            var stackGroup = stackGo.AddComponent<CanvasGroup>();
            stackGroup.alpha = 0.88f;
            stackGroup.interactable = false;
            stackGroup.blocksRaycasts = false;

            var cardSize = new Vector2(70f, 94f);
            var under = CardBackVisual.Create(_stack, "DeckUnder", cardSize);
            under.anchoredPosition = new Vector2(-7f, 7f);
            under.localRotation = Quaternion.Euler(0f, 0f, -5f);

            var middle = CardBackVisual.Create(_stack, "DeckMiddle", cardSize);
            middle.anchoredPosition = new Vector2(-3.5f, 3.5f);
            middle.localRotation = Quaternion.Euler(0f, 0f, -2f);

            var top = CardBackVisual.Create(_stack, "DeckTop", cardSize);
            top.anchoredPosition = Vector2.zero;
            top.localRotation = Quaternion.Euler(0f, 0f, 3f);

            BuildCounter(root);
            SetCount(0);
        }

        public void SetCount(int count)
        {
            count = Mathf.Max(0, count);
            if (_countText != null)
                _countText.text = $"DEK {count}";
            if (_stack != null)
                _stack.gameObject.SetActive(count > 0);

            // A tiny pulse on a draw makes the decrement legible without delaying play.
            if (_lastCount >= 0 && count < _lastCount && _countText != null)
                StartCoroutine(UiFx.Punch(_countText.rectTransform, 1.16f, 0.2f));

            _lastCount = count;
        }

        private void BuildCounter(RectTransform root)
        {
            var chipGo = new GameObject("CountChip", typeof(RectTransform), typeof(Image));
            var chipRt = (RectTransform)chipGo.transform;
            chipRt.SetParent(root, false);
            chipRt.anchorMin = chipRt.anchorMax = new Vector2(0.5f, 0.5f);
            chipRt.pivot = new Vector2(0.5f, 0.5f);
            chipRt.sizeDelta = new Vector2(86f, 25f);
            chipRt.anchoredPosition = new Vector2(0f, -61f);

            var bg = chipGo.GetComponent<Image>();
            bg.sprite = UiSpriteFactory.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.08f, 0.05f, 0.13f, 0.84f);
            bg.raycastTarget = false;

            var ring = new GameObject("Outline", typeof(RectTransform), typeof(Image));
            var ringRt = (RectTransform)ring.transform;
            ringRt.SetParent(chipRt, false);
            Stretch(ringRt, 0f, 0f, 0f, 0f);
            var ringImage = ring.GetComponent<Image>();
            ringImage.sprite = UiSpriteFactory.RoundOutline;
            ringImage.type = Image.Type.Sliced;
            ringImage.color = new Color(0.85f, 0.66f, 0.26f, 0.58f);
            ringImage.raycastTarget = false;

            var textGo = new GameObject("Count", typeof(RectTransform));
            var textRt = (RectTransform)textGo.transform;
            textRt.SetParent(chipRt, false);
            Stretch(textRt, 3f, 1f, 3f, 2f);
            _countText = textGo.AddComponent<TextMeshProUGUI>();
            _countText.font = TmpFontService.Get();
            _countText.fontSize = 15f;
            _countText.fontStyle = FontStyles.Bold;
            _countText.characterSpacing = 0.7f;
            _countText.color = new Color(0.95f, 0.76f, 0.31f, 1f);
            _countText.alignment = TextAlignmentOptions.Center;
            _countText.raycastTarget = false;
            _countText.text = "DEK 0";
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
