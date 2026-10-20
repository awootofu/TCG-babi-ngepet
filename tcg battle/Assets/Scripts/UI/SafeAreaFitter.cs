using UnityEngine;

namespace TcgBattle.UI
{
    /// <summary>
    /// Shrinks the root rect to Screen.safeArea so notch/home-indicator insets never cover
    /// the battle UI on phones. No-op in the editor (safe area equals the full screen there).
    /// </summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        private Rect _lastSafe;
        private Vector2Int _lastScreen;
        private bool _applied;

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (_applied && Screen.safeArea == _lastSafe &&
                Screen.width == _lastScreen.x && Screen.height == _lastScreen.y)
                return;
            Apply();
        }

        private void Apply()
        {
            var rt = (RectTransform)transform;
            var sa = Screen.safeArea;
            _lastSafe = sa;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
            _applied = true;

            float w = Screen.width;
            float h = Screen.height;
            var min = sa.position;
            var max = min + sa.size;
            rt.anchorMin = new Vector2(min.x / w, min.y / h);
            rt.anchorMax = new Vector2(max.x / w, max.y / h);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
