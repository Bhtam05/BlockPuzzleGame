using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Hiệu ứng khi xóa hàng/cột: mảnh gỗ vỡ bay ra, tia sáng quét dọc hàng/cột, chữ điểm bay lên
    /// (+điểm, COMBO xN!) và rung camera khi xóa nhiều đường cùng lúc.
    /// Mọi hạt dùng chung một pool dựng sẵn từ đầu và cập nhật trong 1 vòng lặp, không Instantiate
    /// hay cấp phát tween cho từng hạt.
    /// </summary>
    public class ClearEffects : MonoBehaviour
    {
        private enum Mode { Chip, Spark, Streak }

        private sealed class Particle
        {
            public SpriteRenderer Sr;
            public Mode Mode;
            public Vector2 Vel;
            public float Spin;
            public float Age, Life, Scale;
            public Color Color;
            public Vector3 From, To; // dùng cho tia sáng
        }

        [Header("Tham chiếu")]
        [SerializeField] private GridManager grid;
        [SerializeField] private ScreenShake shake;
        [SerializeField] private TextMeshPro popupPrefab;

        [Header("Sprite")]
        [SerializeField] private Sprite chipSprite;
        [SerializeField] private Sprite sparkSprite;
        [SerializeField] private Sprite streakSprite;

        [Header("Mảnh gỗ vỡ")]
        [SerializeField] private int chipsPerCell = 3;
        [SerializeField] private Vector2 chipSpeed = new Vector2(2.5f, 6f);
        [SerializeField] private float chipGravity = 14f;
        [SerializeField] private Vector2 chipLife = new Vector2(0.55f, 0.9f);
        [SerializeField] private Vector2 chipScale = new Vector2(0.25f, 0.5f);

        [Header("Tia sáng vàng")]
        [SerializeField] private float streakDuration = 0.32f;
        [SerializeField] private float streakLength = 3.5f;
        [SerializeField] private float streakThickness = 1.2f;

        [Header("Chữ điểm bay lên")]
        [SerializeField] private float popupFontSize = 6f;
        [SerializeField] private float popupRise = 1.4f;
        [SerializeField] private float popupLife = 0.9f;
        [SerializeField] private int comboFromStreak = 2;
        [SerializeField] private Color pointsColor = new Color32(255, 214, 90, 255);
        [SerializeField] private Color comboColor = new Color32(255, 128, 52, 255);

        [Header("Rung camera")]
        [SerializeField] private int shakeFromLines = 2;
        [SerializeField] private float shakeBase = 0.08f;
        [SerializeField] private float shakeStep = 0.05f;
        [SerializeField] private float shakeMax = 0.3f;
        [SerializeField] private float shakeDuration = 0.3f;

        [Header("Pool")]
        [SerializeField] private int particlePool = 220;
        [SerializeField] private int popupPool = 6;

        private const int SortStreak = 30;
        private const int SortChip = 40;
        private const int SortSpark = 41;

        private readonly Stack<Particle> _free = new Stack<Particle>();
        private readonly List<Particle> _active = new List<Particle>();
        private readonly Stack<TextMeshPro> _freePopups = new Stack<TextMeshPro>();
        private readonly List<TextMeshPro> _busyPopups = new List<TextMeshPro>();

        private void Awake()
        {
            _active.Capacity = particlePool;
            for (int i = 0; i < particlePool; i++) _free.Push(CreateParticle());
            for (int i = 0; i < popupPool; i++) _freePopups.Push(CreatePopup());
        }

        private void OnDisable() => DOTween.Kill(this);

        // ================================================================= API

        /// <summary>
        /// Gọi ngay sau khi xóa hàng/cột và tính điểm.
        /// <paramref name="pointsGained"/>: điểm nhận được ở lượt này; <paramref name="comboStreak"/>: chuỗi combo hiện tại.
        /// </summary>
        public void PlayClear(ClearInfo info, int pointsGained, int comboStreak)
        {
            if (info == null || info.LineCount == 0 || grid == null) return;

            PlayStreaks(info);
            PlayChipsAndSparks(info);
            PlayPopups(info, pointsGained, comboStreak);

            if (shake != null && info.LineCount >= shakeFromLines)
            {
                float strength = Mathf.Min(shakeMax, shakeBase + shakeStep * (info.LineCount - shakeFromLines));
                shake.Shake(strength, shakeDuration);
            }
        }

        /// <summary>
        /// Khi bảng đổi chất liệu: chùm tia lấp lánh bay lên từ khắp bảng + chữ tên chất liệu.
        /// Chạy trễ một nhịp để khớp lúc các khối bắt đầu đổi.
        /// </summary>
        public void PlayThemeChange(string text, Color color)
        {
            if (grid == null) return;

            DOVirtual.DelayedCall(0.35f, () =>
            {
                Vector3 c = grid.BoardCenter;
                float halfW = grid.Width * grid.CellSize * 0.5f;
                float halfH = grid.Height * grid.CellSize * 0.5f;

                Color tint = Color.Lerp(color, Color.white, 0.5f);
                for (int i = 0; i < 40; i++)
                {
                    Vector3 pos = c + new Vector3(Random.Range(-halfW, halfW), Random.Range(-halfH, halfH), 0f);
                    Particle p = Spawn(Mode.Spark, sparkSprite, SortSpark, pos,
                        Random.Range(0.5f, 0.9f), Random.Range(0.6f, 1.3f), tint);
                    if (p == null) break;
                    p.Vel = new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(0.6f, 2.2f));
                    p.Spin = Random.Range(-160f, 160f);
                }

                if (!string.IsNullOrEmpty(text))
                    ShowPopup(text, c + Vector3.up * 1.6f, color, 1.5f);
            }).SetTarget(this);
        }

        /// <summary>Tắt ngay mọi hiệu ứng đang chạy (khi chơi lại).</summary>
        public void StopAll()
        {
            DOTween.Kill(this);

            for (int i = _active.Count - 1; i >= 0; i--) Release(_active[i]);
            _active.Clear();

            for (int i = _busyPopups.Count - 1; i >= 0; i--) ReleasePopup(_busyPopups[i]);
        }

        // ================================================================= Tia sáng

        /// <summary>Mỗi hàng/cột bị xóa có một vệt sáng quét từ đầu này sang đầu kia.</summary>
        private void PlayStreaks(ClearInfo info)
        {
            float half = streakLength * 0.5f;

            foreach (int row in info.Rows)
            {
                Vector3 a = grid.GridToWorld(0, row);
                Vector3 b = grid.GridToWorld(grid.Width - 1, row);
                SpawnStreak(a + Vector3.left * half, b + Vector3.right * half, 0f);
            }

            foreach (int col in info.Cols)
            {
                Vector3 a = grid.GridToWorld(col, 0);
                Vector3 b = grid.GridToWorld(col, grid.Height - 1);
                SpawnStreak(a + Vector3.down * half, b + Vector3.up * half, 90f);
            }
        }

        private void SpawnStreak(Vector3 from, Vector3 to, float angle)
        {
            Particle p = Spawn(Mode.Streak, streakSprite, SortStreak, from, streakDuration, 1f, Color.white);
            if (p == null) return;

            p.From = from;
            p.To = to;
            Transform tr = p.Sr.transform;
            tr.rotation = Quaternion.Euler(0f, 0f, angle);
            tr.localScale = new Vector3(streakLength / streakSprite.bounds.size.x, streakThickness, 1f);
        }

        // ================================================================= Mảnh gỗ + lấp lánh

        private void PlayChipsAndSparks(ClearInfo info)
        {
            for (int i = 0; i < info.CellPositions.Count; i++)
            {
                Vector3 pos = info.CellPositions[i];
                Color tint = info.CellColors[i];
                tint.a = 1f;

                for (int k = 0; k < chipsPerCell; k++)
                {
                    Vector3 start = pos + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), 0f);
                    Particle p = Spawn(Mode.Chip, chipSprite, SortChip, start,
                        Random.Range(chipLife.x, chipLife.y), Random.Range(chipScale.x, chipScale.y), tint);
                    if (p == null) return; // hết pool thì thôi, không ảnh hưởng gameplay

                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float speed = Random.Range(chipSpeed.x, chipSpeed.y);
                    p.Vel = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed + 2f); // lệch lên trên
                    p.Spin = Random.Range(-540f, 540f);
                }

                Particle spark = Spawn(Mode.Spark, sparkSprite, SortSpark, pos,
                    Random.Range(0.35f, 0.55f), Random.Range(0.7f, 1.2f), new Color(1f, 0.95f, 0.7f, 1f));
                if (spark != null)
                {
                    spark.Vel = new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.2f, 1.2f));
                    spark.Spin = Random.Range(-120f, 120f);
                }
            }
        }

        // ================================================================= Vòng lặp hạt

        private void Update()
        {
            int count = _active.Count;
            if (count == 0) return;

            float dt = Time.deltaTime;

            // Duyệt ngược để xóa hoán đổi (swap-remove) an toàn
            for (int i = count - 1; i >= 0; i--)
            {
                Particle p = _active[i];
                p.Age += dt;
                float t = p.Age / p.Life;

                if (t >= 1f)
                {
                    Release(p);
                    int last = _active.Count - 1;
                    _active[i] = _active[last];
                    _active.RemoveAt(last);
                    continue;
                }

                Transform tr = p.Sr.transform;
                Color c = p.Color;

                switch (p.Mode)
                {
                    case Mode.Chip:
                        p.Vel.y -= chipGravity * dt;
                        tr.position += new Vector3(p.Vel.x * dt, p.Vel.y * dt, 0f);
                        tr.Rotate(0f, 0f, p.Spin * dt);
                        tr.localScale = Vector3.one * (p.Scale * (1f - 0.4f * t));
                        c.a = 1f - t * t;
                        break;

                    case Mode.Spark:
                        tr.position += new Vector3(p.Vel.x * dt, p.Vel.y * dt, 0f);
                        tr.Rotate(0f, 0f, p.Spin * dt);
                        tr.localScale = Vector3.one * (p.Scale * Mathf.Sin(t * Mathf.PI)); // nở rồi co lại
                        break;

                    case Mode.Streak:
                        float eased = 1f - (1f - t) * (1f - t); // chạy nhanh lúc đầu rồi chậm dần
                        tr.position = Vector3.Lerp(p.From, p.To, eased);
                        c.a = Mathf.Sin(t * Mathf.PI);
                        break;
                }

                p.Sr.color = c;
            }
        }

        private Particle Spawn(Mode mode, Sprite sprite, int order, Vector3 pos, float life, float scale, Color color)
        {
            if (sprite == null || _free.Count == 0) return null;

            Particle p = _free.Pop();
            p.Mode = mode;
            p.Age = 0f;
            p.Life = Mathf.Max(0.01f, life);
            p.Scale = scale;
            p.Color = color;
            p.Vel = Vector2.zero;
            p.Spin = 0f;

            SpriteRenderer sr = p.Sr;
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = color;

            Transform tr = sr.transform;
            tr.position = pos;
            tr.rotation = Quaternion.identity;
            tr.localScale = Vector3.one * scale;

            sr.gameObject.SetActive(true);
            _active.Add(p);
            return p;
        }

        private void Release(Particle p)
        {
            p.Sr.gameObject.SetActive(false);
            _free.Push(p);
        }

        private Particle CreateParticle()
        {
            var go = new GameObject("FxParticle");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            go.SetActive(false);
            return new Particle { Sr = sr };
        }

        // ================================================================= Chữ điểm

        private void PlayPopups(ClearInfo info, int pointsGained, int comboStreak)
        {
            // Vị trí hiện chữ: trung bình tâm các ô vừa bị xóa
            Vector3 center = Vector3.zero;
            int n = info.CellPositions.Count;
            if (n == 0) return;
            for (int i = 0; i < n; i++) center += info.CellPositions[i];
            center /= n;

            // Xóa càng nhiều đường chữ càng to
            float size = 1f + 0.15f * Mathf.Min(info.LineCount - 1, 4);

            ShowPopup("+" + pointsGained, center, pointsColor, size);
            if (comboStreak >= comboFromStreak)
                ShowPopup("COMBO x" + comboStreak + "!", center + Vector3.up * 1.0f, comboColor, size * 1.25f);
        }

        private void ShowPopup(string text, Vector3 pos, Color color, float scale)
        {
            if (popupPrefab == null) return;

            TextMeshPro t = _freePopups.Count > 0 ? _freePopups.Pop() : CreatePopup();
            _busyPopups.Add(t);

            t.text = text;
            t.fontSize = popupFontSize;
            t.color = color; // alpha = 1

            Transform tr = t.transform;
            tr.position = pos;
            tr.localScale = Vector3.zero;
            t.gameObject.SetActive(true);

            float fade = 0.3f;
            Sequence seq = DOTween.Sequence().SetId(this);
            seq.Append(tr.DOScale(scale, 0.18f).SetEase(Ease.OutBack));
            seq.Join(tr.DOMoveY(pos.y + popupRise, popupLife).SetEase(Ease.OutQuad));
            seq.Insert(popupLife - fade, DOTween.To(() => t.alpha, a => t.alpha = a, 0f, fade));
            seq.OnComplete(() => ReleasePopup(t));
        }

        private void ReleasePopup(TextMeshPro t)
        {
            if (t == null) return;
            t.gameObject.SetActive(false);
            _busyPopups.Remove(t);
            _freePopups.Push(t);
        }

        private TextMeshPro CreatePopup()
        {
            if (popupPrefab == null) return null;
            TextMeshPro t = Instantiate(popupPrefab, transform);
            t.gameObject.SetActive(false);
            return t;
        }
    }
}
