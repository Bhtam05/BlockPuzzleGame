using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BlockPuzzle
{
    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        GameOver
    }

    /// <summary>
    /// Điều phối toàn bộ game: nhận event đặt khối, gọi Grid/Score/Spawner,
    /// kiểm tra Game Over, Pause/Resume, chơi lại, lưu/khôi phục ván và nút Back của Android.
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

        [Header("Menu chính")]
        [Tooltip("Màn hình khởi đầu (Bắt đầu / Cài đặt). Để trống thì vào thẳng ván chơi.")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Hiệu ứng")]
        [SerializeField] private ClearEffects effects;

        [Header("Lưu ván")]
        [Tooltip("Thoát app giữa chừng thì lần sau vào chơi tiếp ván cũ.")]
        [SerializeField] private bool saveProgress = true;

        private Tween _gameOverCall;

        public GameState State { get; private set; } = GameState.Menu;
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

        private void Start()
        {
            if (menuPanel != null) ShowMenu();
            else if (!TryRestoreGame()) StartGame();
        }

        private void Update()
        {
            if (!BackPressed()) return;

            // Nút Back của Android (cũng là phím Esc trên máy tính)
            switch (State)
            {
                case GameState.Menu:
                    if (settingsPanel != null && settingsPanel.activeSelf) CloseSettings();
                    else Application.Quit();
                    break;
                case GameState.Playing: Pause(); break;
                case GameState.Paused: Resume(); break;
                case GameState.GameOver: Application.Quit(); break;
            }
        }

        private static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
            return false;
        }

        /// <summary>
        /// Trên điện thoại: thoát app hoặc có cuộc gọi đến thì tự Pause, lưu ván và điểm cao
        /// (Android có thể đóng app bất kỳ lúc nào khi đang ở nền).
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            score.SaveHighScore();
            SaveProgress();
            PauseSilently(); // chỉ có tác dụng khi đang Playing
        }

        private void OnApplicationQuit() => SaveProgress();

        // ----------------------------------------------------------------- Menu chính

        /// <summary>Hiện màn hình khởi đầu. Ván đang chơi dở (nếu có) đã được lưu để Bắt đầu chơi tiếp.</summary>
        private void ShowMenu()
        {
            _gameOverCall?.Kill();
            BlockPiece.CancelActiveDrag();

            State = GameState.Menu;
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (menuPanel != null) menuPanel.SetActive(true);
        }

        /// <summary>Gắn vào nút Bắt đầu: chơi tiếp ván đã lưu, không có thì mở ván mới.</summary>
        public void PlayGame()
        {
            if (State != GameState.Menu) return;

            AudioManager.Play(Sfx.Click);
            if (menuPanel != null) menuPanel.SetActive(false);
            if (!TryRestoreGame()) StartGame();
        }

        /// <summary>Gắn vào nút Trang chủ (trong Tạm dừng và Game Over): lưu ván rồi về menu.</summary>
        public void GoHome()
        {
            if (menuPanel == null) return;

            AudioManager.Play(Sfx.Click);
            SaveProgress(); // trước khi đổi State
            score.SaveHighScore();
            ShowMenu();
        }

        /// <summary>Gắn vào nút Cài đặt ở menu.</summary>
        public void OpenSettings()
        {
            if (State != GameState.Menu || settingsPanel == null) return;

            AudioManager.Play(Sfx.Click);
            settingsPanel.SetActive(true);
        }

        /// <summary>Gắn vào nút Quay lại trong màn Cài đặt.</summary>
        public void CloseSettings()
        {
            if (settingsPanel == null) return;

            AudioManager.Play(Sfx.Click);
            settingsPanel.SetActive(false);
        }

        // ----------------------------------------------------------------- Bắt đầu / khôi phục

        /// <summary>Bắt đầu / chơi lại ván mới.</summary>
        public void StartGame()
        {
            State = GameState.Playing;
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (menuPanel != null) menuPanel.SetActive(false);

            _gameOverCall?.Kill();
            GameSave.Clear();
            if (effects != null) effects.StopAll();
            grid.ResetGrid();
            score.ResetScore();
            spawner.ClearAll();
            spawner.SpawnSet();
        }

        /// <summary>Dựng lại ván đã lưu. Trả về false nếu không có ván nào hoặc dữ liệu không hợp lệ.</summary>
        private bool TryRestoreGame()
        {
            if (!saveProgress) return false;
            if (!GameSave.TryLoad(out GameSaveData data)) return false;

            if (data.width != grid.Width || data.height != grid.Height || data.cells.Length != grid.Width * grid.Height)
            {
                GameSave.Clear();
                return false;
            }

            State = GameState.Playing;
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);

            if (effects != null) effects.StopAll();
            spawner.ClearAll();
            if (!spawner.RestoreTray(data.tray))
            {
                GameSave.Clear();
                return false; // StartGame sẽ dọn bảng và sinh khối mới
            }

            grid.ImportCells(data.cells);
            score.ImportState(data.score, data.streak, data.movesWithoutClear);
            CheckGameOver(); // phòng trường hợp ván lưu đã hết nước đi
            return true;
        }

        private void SaveProgress()
        {
            // Chỉ lưu khi đang có ván thật: ở menu bảng còn trống, lưu sẽ đè mất ván cũ
            if (!saveProgress || (State != GameState.Playing && State != GameState.Paused)) return;
            if (grid == null || spawner == null || score == null) return;
            GameSave.Write(grid, spawner, score);
        }

        /// <summary>Gắn vào nút Replay trên UI.</summary>
        public void Restart()
        {
            AudioManager.Play(Sfx.Click);
            StartGame();
        }

        // ----------------------------------------------------------------- Pause

        /// <summary>Gắn vào nút cài đặt (bánh răng): tạm dừng và hiện panel Pause (có các công tắc cài đặt).</summary>
        public void Pause()
        {
            if (State != GameState.Playing) return;
            AudioManager.Play(Sfx.Click);
            PauseSilently();
        }

        private void PauseSilently()
        {
            if (State != GameState.Playing) return;

            State = GameState.Paused;
            BlockPiece.CancelActiveDrag();
            if (pausePanel != null) pausePanel.SetActive(true);
        }

        /// <summary>Gắn vào nút Tiếp tục trong panel Pause.</summary>
        public void Resume()
        {
            if (State != GameState.Paused) return;

            AudioManager.Play(Sfx.Click);
            State = GameState.Playing;
            if (pausePanel != null) pausePanel.SetActive(false);
        }

        // ----------------------------------------------------------------- Lượt chơi

        /// <summary>
        /// Xử lý khi người chơi thả khối hợp lệ:
        /// đặt vào bảng -> xóa đường đầy -> tính điểm -> sinh khối mới nếu cần -> lưu ván -> kiểm tra Game Over.
        /// </summary>
        private void HandlePiecePlaced(BlockPiece piece, Vector2Int origin)
        {
            if (!IsPlaying) return;

            BlockShapeData shape = piece.Shape;
            grid.PlaceShape(shape.NormalizedCells, origin, shape.color);

            int lines = grid.ClearFullLines();
            int gained = score.RegisterTurn(shape.CellCount, lines);

            // Âm thanh + rung
            AudioManager.Play(Sfx.Place, Random.Range(0.95f, 1.05f));
            Haptics.Tap();
            if (lines > 0)
            {
                AudioManager.Play(Sfx.Clear, 1f + 0.12f * Mathf.Min(lines - 1, 3));
                if (score.ComboStreak >= 2)
                    AudioManager.Play(Sfx.Combo, 1f + 0.06f * Mathf.Min(score.ComboStreak - 2, 8));
                Haptics.Clear(lines);
            }

            // Hiệu ứng: mảnh gỗ, tia sáng, chữ điểm, rung camera (chỉ khi có xóa đường)
            if (lines > 0 && effects != null) effects.PlayClear(grid.LastClear, gained, score.ComboStreak);

            spawner.Release(piece);
            if (spawner.IsEmpty) spawner.SpawnSet();

            CheckGameOver();
            SaveProgress(); // không lưu nếu vừa Game Over
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
            GameSave.Clear(); // hết ván thì lần sau vào chơi ván mới

            // Chờ animation xóa hàng/cột chạy xong rồi mới hiện panel
            _gameOverCall?.Kill();
            _gameOverCall = DOVirtual.DelayedCall(gameOverDelay, () =>
            {
                if (State != GameState.GameOver) return; // người chơi đã restart
                AudioManager.Play(Sfx.GameOver);
                Haptics.Heavy();
                if (finalScoreText != null) finalScoreText.text = score.Score.ToString();
                if (finalBestText != null) finalBestText.text = score.HighScore.ToString();
                if (gameOverPanel != null) gameOverPanel.SetActive(true);
            });
        }
    }
}
