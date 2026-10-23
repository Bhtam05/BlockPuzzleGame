using System;
using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>Dữ liệu một ván đang chơi dở (chuyển thành JSON lưu vào PlayerPrefs).</summary>
    [Serializable]
    public class GameSaveData
    {
        public int version = 1;
        public int width;
        public int height;
        /// <summary>width*height phần tử, chỉ số = x * height + y. 0 = ô trống, khác 0 = màu ô đã nén.</summary>
        public int[] cells;
        /// <summary>Chỉ số shape của từng slot trong khay, -1 = khối đã dùng.</summary>
        public int[] tray;
        public int score;
        public int streak;
        public int movesWithoutClear;
        /// <summary>Seed chọn thứ tự chất liệu khối của ván (0 = bản lưu cũ chưa có).</summary>
        public int themeSeed;
    }

    /// <summary>
    /// Lưu / đọc ván đang chơi để thoát app rồi vào lại vẫn chơi tiếp được.
    /// Lưu sau mỗi lượt đặt khối và khi app ra nền; xóa khi Game Over hoặc bắt đầu ván mới.
    /// </summary>
    public static class GameSave
    {
        private const string Key = "BP_SaveGame";
        private const int Version = 1;

        /// <summary>Có ván đang chơi dở không (dùng cho nhãn nút ở menu).</summary>
        public static bool Exists => PlayerPrefs.HasKey(Key);

        public static void Write(GridManager grid, BlockSpawner spawner, ScoreManager score, BlockThemeManager themes)
        {
            var data = new GameSaveData
            {
                version = Version,
                width = grid.Width,
                height = grid.Height,
                cells = new int[grid.Width * grid.Height],
                tray = new int[spawner.SlotCount]
            };

            grid.ExportCells(data.cells);
            spawner.GetTrayState(data.tray);
            score.ExportState(out data.score, out data.streak, out data.movesWithoutClear);
            data.themeSeed = themes != null ? themes.Seed : 0;

            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        /// <summary>Đọc ván đã lưu. Trả về false nếu chưa có hoặc dữ liệu hỏng/khác phiên bản.</summary>
        public static bool TryLoad(out GameSaveData data)
        {
            data = null;
            if (!PlayerPrefs.HasKey(Key)) return false;

            try
            {
                data = JsonUtility.FromJson<GameSaveData>(PlayerPrefs.GetString(Key));
            }
            catch (Exception)
            {
                data = null;
            }

            if (data == null || data.version != Version || data.cells == null || data.tray == null)
            {
                Clear();
                data = null;
                return false;
            }
            return true;
        }

        public static void Clear()
        {
            if (!PlayerPrefs.HasKey(Key)) return;
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
