using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Hạt bụi sáng trôi chậm ở nền (không khí ấm của căn phòng gỗ). Nằm giữa nền và bảng chơi.
    /// Tất cả hạt tạo sẵn từ đầu và cập nhật trong 1 vòng lặp, không cấp phát lúc chơi.
    /// Dùng ít hạt, mờ và chậm để không làm rối mắt khi đang chơi.
    /// </summary>
    public class AmbientDust : MonoBehaviour
    {
        [SerializeField] private Sprite moteSprite;
        [SerializeField] private Camera cam;
        [SerializeField, Range(0, 60)] private int count = 22;
        [SerializeField] private Vector2 size = new Vector2(0.12f, 0.4f);
        [SerializeField] private Vector2 riseSpeed = new Vector2(0.12f, 0.35f);
        [SerializeField] private float sway = 0.35f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.5f;
        [SerializeField] private int sortingOrder = -50;

        private SpriteRenderer[] _sr;
        private float[] _x, _y, _speed, _phase, _freq, _scale, _alpha;
        private Color _tint = new Color(1f, 0.93f, 0.75f, 1f);

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
            if (moteSprite == null || cam == null)
            {
                enabled = false;
                return;
            }

            _sr = new SpriteRenderer[count];
            _x = new float[count]; _y = new float[count]; _speed = new float[count];
            _phase = new float[count]; _freq = new float[count]; _scale = new float[count]; _alpha = new float[count];

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Mote");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = moteSprite;
                sr.sortingOrder = sortingOrder;
                _sr[i] = sr;

                Respawn(i, true);
            }
        }

        private void Respawn(int i, bool anywhere)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;

            _x[i] = Random.Range(-halfW, halfW);
            _y[i] = anywhere ? Random.Range(-halfH, halfH) : -halfH - 0.5f;
            _speed[i] = Random.Range(riseSpeed.x, riseSpeed.y);
            _phase[i] = Random.Range(0f, 6.283f);
            _freq[i] = Random.Range(0.4f, 1.1f);
            _scale[i] = Random.Range(size.x, size.y);
            _alpha[i] = Random.Range(0.35f, 1f) * maxAlpha;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float time = Time.time;
            float halfH = cam.orthographicSize;
            Vector3 camPos = cam.transform.position;

            for (int i = 0; i < _sr.Length; i++)
            {
                _y[i] += _speed[i] * dt;
                if (_y[i] > halfH + 0.5f) Respawn(i, false);

                float px = _x[i] + Mathf.Sin(time * _freq[i] + _phase[i]) * sway;

                // Mờ dần ở hai đầu (vào/ra màn hình) và nhấp nháy nhẹ
                float edge = Mathf.Clamp01((halfH - Mathf.Abs(_y[i])) / 1.5f);
                float twinkle = 0.7f + 0.3f * Mathf.Sin(time * 1.7f + _phase[i] * 2f);
                Color c = _tint;
                c.a = _alpha[i] * edge * twinkle;

                _sr[i].color = c;
                _sr[i].transform.position = new Vector3(camPos.x + px, camPos.y + _y[i], 0f);
                _sr[i].transform.localScale = Vector3.one * _scale[i];
            }
        }
    }
}
