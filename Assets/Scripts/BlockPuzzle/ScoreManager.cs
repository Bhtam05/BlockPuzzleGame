using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Quản lý điểm số, combo và cập nhật UI (TextMeshPro).
    /// - Mỗi ô đặt xuống: pointsPerCell điểm.
    /// - Xóa n đường cùng lúc: pointsPerLine * n * n (xóa nhiều càng lợi), nhân hệ số combo.
    /// - Combo streak tăng khi lượt liên tiếp có xóa đường, reset khi lượt không xóa được.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text highScoreText;
        [SerializeField] private TMP_Text comboText;

        [Header("Cấu hình điểm")]
        [SerializeField] private int pointsPerCell = 1;
        [SerializeField] private int pointsPerLine = 10;
        [SerializeField] private float comboStep = 0.1f;

        private const string HighScoreKey = "BP_HighScore";

        public int Score { get; private set; }
        public int HighScore { get; private set; }
        public int ComboStreak { get; private set; }
        public float ComboMultiplier => 1f + ComboStreak * comboStep;

        public event Action<int> ScoreChanged;

        private int _displayedScore;
        private Tween _scoreTween;

        private void Awake()
        {
            HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
            RefreshUI(true);
        }

        /// <summary>
        /// Gọi sau mỗi lượt đặt khối. Trả về số điểm vừa nhận.
        /// </summary>
        public int RegisterTurn(int placedCells, int clearedLines)
        {
            int gained = placedCells * pointsPerCell;

            if (clearedLines > 0)
            {
                ComboStreak++;
                int lineScore = pointsPerLine * clearedLines * clearedLines;
                gained += Mathf.RoundToInt(lineScore * ComboMultiplier);
            }
            else
            {
                ComboStreak = 0;
            }

            AddScore(gained);
            UpdateComboUI();
            return gained;
        }

        private void AddScore(int amount)
        {
            Score += amount;

            if (Score > HighScore)
            {
                HighScore = Score;
                PlayerPrefs.SetInt(HighScoreKey, HighScore);
            }

            ScoreChanged?.Invoke(Score);
            RefreshUI(false);
        }

        /// <summary>Reset điểm khi chơi lại (giữ nguyên high score).</summary>
        public void ResetScore()
        {
            Score = 0;
            ComboStreak = 0;
            RefreshUI(true);
            UpdateComboUI();
        }

        public void SaveHighScore() => PlayerPrefs.Save();

        // ----------------------------------------------------------------- UI

        private void RefreshUI(bool immediate)
        {
            if (highScoreText != null) 
                highScoreText.text = $"Best Score: {HighScore}";

            if (scoreText == null) return;

            _scoreTween?.Kill();

            if (immediate)
            {
                _displayedScore = Score;
                scoreText.text = $"Score: {Score}";
                return;
            }

            // Số điểm chạy dần lên cho mượt
            _scoreTween = DOTween.To(() => _displayedScore, v =>
                {
                    _displayedScore = v;
                    scoreText.text = $"Score: {v}";
                }, Score, 0.35f).SetEase(Ease.OutQuad);

            scoreText.transform.DOKill(true);
            scoreText.transform.DOPunchScale(Vector3.one * 0.12f, 0.2f);
        }

        private void UpdateComboUI()
        {
            if (comboText != null) 
                comboText.text = ComboStreak > 0 ? $"x{ComboMultiplier.ToString("0.0")}" : "";
        }
    }
}