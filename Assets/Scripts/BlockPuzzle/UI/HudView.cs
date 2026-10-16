using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Thanh tiến độ trên HUD: hiển thị số hàng/cột đã xóa theo mốc (vd 2/25).
    /// Điểm, điểm cao và combo do ScoreManager tự cập nhật.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] private ScoreManager score;
        [Tooltip("RectTransform của thanh lấp đầy, neo trái, kéo anchorMax.x để đổi độ dài.")]
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private int linesPerLevel = 25;
        [Tooltip("Độ dài tối thiểu (0-1) để thanh luôn thấy được khi mới bắt đầu.")]
        [SerializeField, Range(0f, 0.3f)] private float minFill = 0.08f;

        private Tween _tween;

        private void OnEnable()
        {
            if (score == null) return;
            score.LinesChanged += OnLinesChanged;
            Refresh(score.TotalLines, false);
        }

        private void OnDisable()
        {
            if (score != null) score.LinesChanged -= OnLinesChanged;
            _tween?.Kill();
        }

        private void OnLinesChanged(int totalLines) => Refresh(totalLines, true);

        /// <summary>Cập nhật thanh và nhãn. Qua mỗi mốc linesPerLevel thanh quay về đầu.</summary>
        private void Refresh(int totalLines, bool animate)
        {
            int current = totalLines % linesPerLevel;
            float ratio = Mathf.Lerp(minFill, 1f, current / (float)linesPerLevel);
            Vector2 target = new Vector2(ratio, 1f);

            if (progressLabel != null) progressLabel.text = current + "/" + linesPerLevel;
            if (progressFill == null) return;

            _tween?.Kill();
            if (animate) _tween = progressFill.DOAnchorMax(target, 0.35f).SetEase(Ease.OutQuad);
            else progressFill.anchorMax = target;
        }
    }
}
