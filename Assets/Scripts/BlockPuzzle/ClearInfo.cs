using System.Collections.Generic;
using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Thông tin lần xóa gần nhất của GridManager, dùng để dựng hiệu ứng.
    /// Đối tượng được tái sử dụng (không cấp phát mới mỗi lần xóa), nên chỉ đọc ngay lúc nhận,
    /// không giữ tham chiếu lâu dài.
    /// </summary>
    public sealed class ClearInfo
    {
        /// <summary>Chỉ số các hàng ngang vừa bị xóa.</summary>
        public readonly List<int> Rows = new List<int>(8);

        /// <summary>Chỉ số các cột dọc vừa bị xóa.</summary>
        public readonly List<int> Cols = new List<int>(8);

        /// <summary>Vị trí World (tâm ô) của từng ô bị xóa, mỗi ô chỉ xuất hiện 1 lần.</summary>
        public readonly List<Vector3> CellPositions = new List<Vector3>(64);

        /// <summary>Màu của từng ô bị xóa, cùng thứ tự với CellPositions.</summary>
        public readonly List<Color> CellColors = new List<Color>(64);

        public int LineCount => Rows.Count + Cols.Count;

        public void Reset()
        {
            Rows.Clear();
            Cols.Clear();
            CellPositions.Clear();
            CellColors.Clear();
        }
    }
}
