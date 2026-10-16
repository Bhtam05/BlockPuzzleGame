using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Tự chỉnh Orthographic Size để bảng chơi luôn vừa chiều ngang màn hình dọc,
    /// bất kể tỉ lệ màn hình (9:16, 9:19.5, tablet...). Gắn vào Main Camera.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-100)]
    public class CameraFitter : MonoBehaviour
    {
        [Tooltip("Chiều rộng tối thiểu camera phải nhìn thấy (lưới 8 ô + lề).")]
        [SerializeField] private float worldWidth = 9.5f;
        [Tooltip("Chiều cao tối thiểu camera phải nhìn thấy (HUD + lưới + khay).")]
        [SerializeField] private float minHeight = 17f;

        private Camera _cam;
        private int _lastW, _lastH;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            Fit();
        }

        // Chỉ so sánh 2 số nguyên mỗi frame; chỉ tính lại khi xoay/đổi kích thước màn hình
        private void Update()
        {
            if (Screen.width != _lastW || Screen.height != _lastH) Fit();
        }

        /// <summary>Chọn size lớn hơn giữa "vừa chiều ngang" và "đủ chiều cao tối thiểu".</summary>
        private void Fit()
        {
            _lastW = Screen.width;
            _lastH = Screen.height;
            if (_lastH <= 0) return;

            float aspect = (float)_lastW / _lastH;
            _cam.orthographicSize = Mathf.Max(minHeight * 0.5f, worldWidth / (2f * aspect));
        }
    }
}
