using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle
{
    /// <summary>
    /// Màn hình menu "rừng nhỏ lúc hoàng hôn": bảng tên và bảng kỷ lục gỗ treo đung đưa, dây leo hai bên rung nhẹ,
    /// đom đóm bay lập lòe, lá rơi chậm. Gắn vào MenuPanel; hiệu ứng chạy lại mỗi lần panel được bật.
    /// Toàn bộ chuyển động dùng DOTween (không có Update), đom đóm/lá dựng một lần rồi tái sử dụng.
    /// </summary>
    public class MenuScreen : MonoBehaviour
    {
        [Header("Bảng treo")]
        [SerializeField] private RectTransform titleSign;
        [SerializeField] private RectTransform bestPlank;
        [SerializeField] private RectTransform mascot;
        [SerializeField] private TMP_Text bestValue;

        [Header("Nút")]
        [SerializeField] private RectTransform startButton;
        [SerializeField] private RectTransform settingsButton;
        [SerializeField] private TMP_Text startLabel;
        [SerializeField] private string startText = "BẮT ĐẦU";
        [SerializeField] private string continueText = "TIẾP TỤC";

        [Header("Trang trí")]
        [SerializeField] private RectTransform vineLeft;
        [SerializeField] private RectTransform vineRight;
        [Tooltip("Lớp chứa đom đóm và lá (nằm sau dây leo, trước nền).")]
        [SerializeField] private RectTransform particleLayer;
        [SerializeField] private Sprite fireflySprite;
        [SerializeField] private Sprite leafSprite;
        [SerializeField, Range(0, 40)] private int fireflyCount = 16;
        [SerializeField, Range(0, 20)] private int leafCount = 8;

        private readonly List<RectTransform> _fireflies = new List<RectTransform>();
        private readonly List<RectTransform> _leaves = new List<RectTransform>();
        private bool _built;
        private Vector2 _titleHome, _plankHome;

        private void Awake()
        {
            if (titleSign != null) _titleHome = titleSign.anchoredPosition;
            if (bestPlank != null) _plankHome = bestPlank.anchoredPosition;
        }

        private void OnEnable()
        {
            RefreshTexts();
            BuildParticlesOnce();
            PlayIntro();
            StartAmbient();
        }

        private void OnDisable()
        {
            DOTween.Kill(this);
            // Đom đóm và lá có tween riêng (đệ quy) gắn trực tiếp vào từng đối tượng
            for (int i = 0; i < _fireflies.Count; i++) KillParticle(_fireflies[i]);
            for (int i = 0; i < _leaves.Count; i++) KillParticle(_leaves[i]);
        }

        private static void KillParticle(RectTransform rt)
        {
            if (rt == null) return;
            rt.DOKill();
            var img = rt.GetComponent<Image>();
            if (img != null) img.DOKill();
        }

        /// <summary>Cập nhật kỷ lục và nhãn nút Bắt đầu / Tiếp tục theo dữ liệu đã lưu.</summary>
        private void RefreshTexts()
        {
            if (bestValue != null)
                bestValue.text = PlayerPrefs.GetInt(ScoreManager.HighScoreKey, 0).ToString();
            if (startLabel != null)
                startLabel.text = GameSave.Exists ? continueText : startText;
        }

        // ----------------------------------------------------------------- Xuất hiện

        private void PlayIntro()
        {
            Drop(titleSign, _titleHome, 0f);
            Drop(bestPlank, _plankHome, 0.15f);
            Pop(startButton, 0.4f, true);
            Pop(settingsButton, 0.5f, false);
        }

        /// <summary>Bảng thả từ trên xuống rồi nảy nhẹ như treo vào dây.</summary>
        private void Drop(RectTransform rt, Vector2 home, float delay)
        {
            if (rt == null) return;

            rt.anchoredPosition = home + Vector2.up * 700f;
            rt.DOAnchorPos(home, 0.85f).SetDelay(delay).SetEase(Ease.OutBounce).SetTarget(this);
        }

        private void Pop(RectTransform rt, float delay, bool breathe)
        {
            if (rt == null) return;

            rt.localScale = Vector3.zero;
            Sequence seq = DOTween.Sequence().SetTarget(this);
            seq.AppendInterval(delay);
            seq.Append(rt.DOScale(1f, 0.4f).SetEase(Ease.OutBack));
            // Tween lặp vô hạn không được nằm trong Sequence, nên khởi động riêng sau khi nút hiện xong
            if (breathe)
                seq.AppendCallback(() => rt.DOScale(1.04f, 0.9f).SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo).SetTarget(this));
        }

        // ----------------------------------------------------------------- Chuyển động nền

        private void StartAmbient()
        {
            Sway(titleSign, 1.3f, 2.8f, 0f);
            Sway(bestPlank, 1.8f, 2.3f, 0.4f);
            Sway(vineLeft, 2.2f, 3.4f, 0f);
            Sway(vineRight, 2.2f, 3.9f, 0.7f);

            if (mascot != null)
            {
                Vector2 p = mascot.anchoredPosition;
                mascot.DOAnchorPosY(p.y + 8f, 1.1f).SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo).SetTarget(this);
            }

            for (int i = 0; i < _fireflies.Count; i++) WanderFirefly(_fireflies[i], true);
            for (int i = 0; i < _leaves.Count; i++) FallLeaf(_leaves[i], true);
        }

        /// <summary>Đung đưa quanh trục treo (pivot ở đỉnh) ±angle độ.</summary>
        private void Sway(RectTransform rt, float angle, float period, float phase)
        {
            if (rt == null) return;

            rt.localRotation = Quaternion.Euler(0f, 0f, -angle);
            rt.DOLocalRotate(new Vector3(0f, 0f, angle), period).SetDelay(phase).SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo).SetTarget(this);
        }

        // ----------------------------------------------------------------- Đom đóm và lá rơi

        private void BuildParticlesOnce()
        {
            if (_built || particleLayer == null) return;
            _built = true;

            for (int i = 0; i < fireflyCount; i++)
            {
                float size = Random.Range(34f, 64f);
                RectTransform rt = NewSprite("Firefly", fireflySprite, size, new Color(1f, 0.94f, 0.55f, 0f));
                _fireflies.Add(rt);
            }

            for (int i = 0; i < leafCount; i++)
            {
                float size = Random.Range(50f, 84f);
                RectTransform rt = NewSprite("Leaf", leafSprite, size, Color.white);
                _leaves.Add(rt);
            }
        }

        private RectTransform NewSprite(string name, Sprite sprite, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(particleLayer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private Vector2 RandomPoint(float marginX, float marginY)
        {
            Rect r = particleLayer.rect;
            return new Vector2(Random.Range(r.xMin + marginX, r.xMax - marginX),
                               Random.Range(r.yMin + marginY, r.yMax - marginY));
        }

        /// <summary>Đom đóm bay lang thang từ điểm này sang điểm kia gần đó, sáng tối lập lòe.</summary>
        private void WanderFirefly(RectTransform rt, bool first)
        {
            if (rt == null) return;

            if (first)
            {
                rt.anchoredPosition = RandomPoint(60f, 200f);
                var img = rt.GetComponent<Image>();
                img.DOKill();
                Color c = img.color;
                c.a = 0f;
                img.color = c;
                img.DOFade(Random.Range(0.55f, 1f), Random.Range(0.7f, 1.5f)).SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo).SetDelay(Random.Range(0f, 1.2f));
            }

            Vector2 from = rt.anchoredPosition;
            Rect r = particleLayer.rect;
            Vector2 to = from + new Vector2(Random.Range(-220f, 220f), Random.Range(-160f, 160f));
            to.x = Mathf.Clamp(to.x, r.xMin + 40f, r.xMax - 40f);
            to.y = Mathf.Clamp(to.y, r.yMin + 200f, r.yMax - 140f);

            rt.DOAnchorPos(to, Random.Range(3f, 6f)).SetEase(Ease.InOutSine)
                .OnComplete(() => WanderFirefly(rt, false));
        }

        /// <summary>Lá rơi chậm từ trên xuống, đung đưa ngang và xoay; rơi hết thì quay lại trên cùng.</summary>
        private void FallLeaf(RectTransform rt, bool first)
        {
            if (rt == null) return;

            Rect r = particleLayer.rect;
            float x = Random.Range(r.xMin + 80f, r.xMax - 80f);
            float topY = r.yMax + 60f;
            float bottomY = r.yMin + 120f;
            float startY = first ? Random.Range(bottomY, topY) : topY;

            rt.DOKill(); // dọn tween của lượt rơi trước (đung đưa và xoay chạy lặp vô hạn)
            rt.anchoredPosition = new Vector2(x, startY);
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            float duration = Random.Range(9f, 15f) * (startY - bottomY) / (topY - bottomY);
            rt.DOAnchorPosY(bottomY, Mathf.Max(0.5f, duration)).SetEase(Ease.Linear)
                .OnComplete(() => FallLeaf(rt, false));
            rt.DOAnchorPosX(x + Random.Range(60f, 130f) * (Random.value < 0.5f ? -1f : 1f), Random.Range(2.5f, 4f))
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
            rt.DOLocalRotate(new Vector3(0f, 0f, Random.Range(120f, 240f) * (Random.value < 0.5f ? -1f : 1f)),
                    Random.Range(3f, 5f), RotateMode.LocalAxisAdd)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }
    }
}
