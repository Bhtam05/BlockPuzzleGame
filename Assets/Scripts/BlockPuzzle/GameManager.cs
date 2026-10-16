using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BlockPuzzle
{
    public enum GameState
    {
        Playing,
        Paused,
        GameOver
    }

    /// <summary>
    /// Điều phối toàn bộ game: nhận event đặt khối, gọi Grid/Score/Spawner,
    /// kiểm tra Game Over, Pause/Resume và xử lý chơi lại.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private GridManager grid;
        [SerializeField] private BlockSpawner spawner;
        [SerializeField] private ScoreManager score;

        [Header("Game Over UI")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TMP_Text finalScoreText;
        [SerializeField] private TMP_Text finalBestText;
        [SerializeField] private float gameOverDelay = 0.6f;

        [Header("Pause UI")]
        [SerializeField] private GameObject pausePanel;

        public GameState State { get; private set; } = GameState.Playing;
        public bool IsPlaying => State == GameState.Playing;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable() => BlockPiece.Placed += HandlePiecePlaced;
        private void OnDisable() => BlockPiece.Placed -= HandlePiecePlaced;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start() => StartGame();

        /// <summary>Bắt đầu / chơi lại ván mới.</summary>
        public void StartGame()
        {
            State = GameState.Playing;
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);

            grid.ResetGrid();
            score.ResetScore();
            spawner.ClearAll();
            spawner.SpawnSet();
        }

        /// <summary>Gắn vào nút Replay trên UI.</summary>
        public void Restart() => StartGame();

        /// <summary>Gắn vào nút cài đặt (bánh răng): tạm dừng và hiện panel Pause.</summary>
        public void Pause()
        {
            if (State != GameState.Playing) return;

            State = GameState.Paused;
            if (pausePanel != null) pausePanel.SetActive(true);
        }

        /// <summary>Gắn vào nút Tiếp tục trong panel Pause.</summary>
        public void Resume()
        {
            if (State != GameState.Paused) return;

            State = GameState.Playing;
            if (pausePanel != null) pausePanel.SetActive(false);
        }

        /// <summary>
        /// Xử lý khi người chơi thả khối hợp lệ:
        /// đặt vào bảng -> xóa đường đầy -> tính điểm -> sinh khối mới nếu cần -> kiểm tra Game Over.
        /// </summary>
        private void HandlePiecePlaced(BlockPiece piece, Vector2Int origin)
        {
            if (!IsPlaying) return;

            BlockShapeData shape = piece.Shape;
            grid.PlaceShape(shape.NormalizedCells, origin, shape.color);

            int lines = grid.ClearFullLines();
            score.RegisterTurn(shape.CellCount, lines);

            spawner.Release(piece);
            if (spawner.IsEmpty) spawner.SpawnSet();

            CheckGameOver();
        }

        /// <summary>
        /// Game Over khi KHÔNG khối nào còn trong khay đặt vừa được vào bảng.
        /// </summary>
        private void CheckGameOver()
        {
            var pieces = spawner.Pieces;
            for (int i = 0; i < pieces.Count; i++)
            {
                BlockPiece p = pieces[i];
                if (p == null) continue; // khối đã dùng

                if (grid.CanPlaceAnywhere(p.Shape)) return; // còn nước đi
            }

            GameOver();
        }

        private void GameOver()
        {
            if (State == GameState.GameOver) return;

            State = GameState.GameOver;
            Debug.Log($"GAME OVER - điểm: {score.Score}");
            if (gameOverPanel == null)
                Debug.LogWarning("Chưa gán Game Over Panel trong GameManager!", this);

            score.SaveHighScore();

            // Chờ animation xóa hàng/cột chạy xong rồi mới hiện panel
            DOVirtual.DelayedCall(gameOverDelay, () =>
            {
                if (State != GameState.GameOver) return; // người chơi đã restart
                if (finalScoreText != null) finalScoreText.text = score.Score.ToString();
                if (finalBestText != null) finalBestText.text = score.HighScore.ToString();
                if (gameOverPanel != null) gameOverPanel.SetActive(true);
            });
        }
    }
}
