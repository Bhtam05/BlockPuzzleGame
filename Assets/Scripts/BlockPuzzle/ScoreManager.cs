using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Quản lý điểm số, combo và cập nhật UI (TextMeshPro).
    /// - Mỗi ô đặt xuống: pointsPerCell điểm (không nhân combo).
    /// - Xóa n đường cùng lúc: pointsPerLine * n * n (xóa nhiều càng lợi), nhân hệ số combo.
    /// - Chuỗi combo (ComboStreak) tăng 1 mỗi lượt có xóa đường. Lượt không xóa chưa làm mất chuỗi:
    ///   chỉ khi có comboGraceMoves lượt đặt liên tiếp không xóa (mặc định 3, tức một bộ khối) thì chuỗi về 0.
    /// - Hệ số = 1 + chuỗi * comboStep, tối đa maxMultiplier. Hệ số hiển thị trên HUD chính là hệ số
    ///   sẽ áp cho lần xóa kế tiếp, nên lần xóa đầu tiên chưa có thưởng (x1.0).
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
        [Tooltip("Hệ số combo tối đa, để điểm không phình vô hạn.")]
        [SerializeField] private float maxMultiplier = 3f;
        [Tooltip("Số lượt đặt khối liên tiếp không xóa được thì chuỗi combo bị reset (3 = một bộ khối).")]
        [SerializeField, Min(1)] private int comboGraceMoves = 3;

        private const string HighScoreKey = "BP_HighScore";

        public int Score { get; private set; }
        public int HighScore { get; private set; }

        /// <summary>Số lần xóa đường liên tiếp (còn trong thời gian chờ). 0 = chưa có combo.</summary>
        public int ComboStreak { get; private set; }

        /// <summary>Hệ số áp cho lần xóa kế tiếp, từ x1.0 đến maxMultiplier.</summary>
        public float ComboMultiplier => Mathf.Min(maxMultiplier, 1f + ComboStreak * comboStep);

        /// <summary>Số lượt đặt còn lại trước khi mất chuỗi (0 nếu chưa có chuỗi). Dùng cho UI sau này.</summary>
        public int ComboMovesLeft => ComboStreak > 0 ? comboGraceMoves - _movesWithoutClear : 0;

        public event Action<int> ScoreChanged;

        private int _displayedScore;
        private int _movesWithoutClear; // số lượt đặt liên tiếp gần nhất không xóa được
        private Tween _scoreTween;

        private void Awake()
        {
            HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
            RefreshUI(true);
        }

        /// <summary>
        /// Gọi sau mỗi lượt đặt khối. Trả về số điểm vừa nhận.
        /// Sau khi gọi, ComboStreak là chuỗi tính cả lượt này (dùng để hiện chữ COMBO xN!).
        /// </summary>
        public int RegisterTurn(int placedCells, int clearedLines)
        {
            int gained = placedCells * pointsPerCell;

            if (clearedLines > 0)
            {
                // Dùng hệ số hiện có TRƯỚC khi tăng chuỗi: lần xóa đầu x1.0, lần liên tiếp thứ 2 x1.1...
                int lineScore = pointsPerLine * clearedLines * clearedLines;
                gained += Mathf.RoundToInt(lineScore * ComboMultiplier);

                ComboStreak++;
                _movesWithoutClear = 0;
            }
            else if (ComboStreak > 0)
            {
                // Không xóa được: chỉ mất chuỗi khi quá số lượt chờ
                _movesWithoutClear++;
                if (_movesWithoutClear >= comboGraceMoves)
                {
                    ComboStreak = 0;
                    _movesWithoutClear = 0;
                }
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
            _movesWithoutClear = 0;
            RefreshUI(true);
            UpdateComboUI();
        }

        /// <summary>Lấy trạng thái điểm để lưu ván đang chơi.</summary>
        public void ExportState(out int score, out int streak, out int movesWithoutClear)
        {
            score = Score;
            streak = ComboStreak;
            movesWithoutClear = _movesWithoutClear;
        }

        /// <summary>Khôi phục điểm và combo từ ván đã lưu (không chạy animation, không đụng high score đã lưu).</summary>
        public void ImportState(int score, int streak, int movesWithoutClear)
        {
            Score = Mathf.Max(0, score);
            ComboStreak = Mathf.Max(0, streak);
            _movesWithoutClear = Mathf.Clamp(movesWithoutClear, 0, comboGraceMoves);

            if (Score > HighScore)
            {
                HighScore = Score;
                PlayerPrefs.SetInt(HighScoreKey, HighScore);
            }

            RefreshUI(true);
            UpdateComboUI();
        }

        public void SaveHighScore() => PlayerPrefs.Save();

        // ----------------------------------------------------------------- UI

        private void RefreshUI(bool immediate)
        {
            if (highScoreText != null) highScoreText.text = HighScore.ToString();
            if (scoreText == null) return;

            _scoreTween?.Kill();

            if (immediate)
            {
                _displayedScore = Score;
                scoreText.text = Score.ToString();
                return;
            }

            // Số điểm chạy dần lên cho mượt
            _scoreTween = DOTween.To(() => _displayedScore, v =>
                {
                    _displayedScore = v;
                    scoreText.text = v.ToString();
                }, Score, 0.35f).SetEase(Ease.OutQuad);

            scoreText.transform.DOKill(true);
            scoreText.transform.DOPunchScale(Vector3.one * 0.12f, 0.2f);
        }

        private void UpdateComboUI()
        {
            if (comboText != null) comboText.text = "x" + ComboMultiplier.ToString("0.0");
        }
    }
}
