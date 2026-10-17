using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Phóng nền gỗ phủ kín toàn bộ vùng nhìn của camera (kiểu "cover").
    /// Gắn vào SpriteRenderer nền, Sorting Order rất thấp (-100).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DefaultExecutionOrder(100)]
    public class BackgroundFitter : MonoBehaviour
    {
        [Tooltip("Phóng to thêm một chút để khi camera rung không lộ mép nền.")]
        [SerializeField, Range(1f, 1.3f)] private float overscan = 1.08f;

        private SpriteRenderer _sr;
        private Camera _cam;
        private int _lastW, _lastH;
        private float _lastSize;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _cam = Camera.main; // chỉ gọi 1 lần
        }

        private void LateUpdate()
        {
            if (_cam == null || _sr.sprite == null) return;

            // Chỉ tính lại khi màn hình hoặc size camera đổi
            if (Screen.width == _lastW && Screen.height == _lastH &&
                Mathf.Approximately(_cam.orthographicSize, _lastSize)) return;

            _lastW = Screen.width;
            _lastH = Screen.height;
            _lastSize = _cam.orthographicSize;

            float viewH = _lastSize * 2f;
            float viewW = viewH * _cam.aspect;
            Vector2 spriteSize = _sr.sprite.bounds.size;
            float scale = Mathf.Max(viewW / spriteSize.x, viewH / spriteSize.y) * overscan;

            transform.localScale = Vector3.one * scale;
            Vector3 p = _cam.transform.position;
            transform.position = new Vector3(p.x, p.y, 0f);
        }
    }
}
