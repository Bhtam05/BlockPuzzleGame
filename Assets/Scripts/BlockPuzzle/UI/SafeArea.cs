using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Co RectTransform vào vùng an toàn (tránh tai thỏ, camera đục lỗ, thanh điều hướng).
    /// Gắn vào object cha của HUD.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform _rt;
        private Rect _lastArea;
        private Vector2Int _lastRes;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastArea || Screen.width != _lastRes.x || Screen.height != _lastRes.y)
                Apply();
        }

        private void Apply()
        {
            Rect area = Screen.safeArea;
            _lastArea = area;
            _lastRes = new Vector2Int(Screen.width, Screen.height);
            if (_lastRes.x <= 0 || _lastRes.y <= 0) return;

            Vector2 min = area.position;
            Vector2 max = area.position + area.size;
            min.x /= _lastRes.x; min.y /= _lastRes.y;
            max.x /= _lastRes.x; max.y /= _lastRes.y;

            _rt.anchorMin = min;
            _rt.anchorMax = max;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
