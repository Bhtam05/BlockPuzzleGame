using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Mô phỏng nhanh (không đụng vào bảng thật) để BlockSpawner chọn bộ 3 khối hợp lý:
    /// - IsSolvable: bộ 3 khối có đặt hết được theo một thứ tự nào đó không (có tính cả việc xóa đường giữa các lượt).
    /// - CanClearLine: một khối có chỗ đặt để hoàn thành ít nhất một hàng/cột trên bảng hiện tại không.
    /// Mọi mảng đều tạo sẵn một lần, không cấp phát lúc chơi. Chỉ số ô = y * width + x.
    /// </summary>
    public sealed class SpawnPlanner
    {
        private readonly int _w;
        private readonly int _h;
        private readonly bool[][] _boards;      // _boards[d] = bảng sau khi đã đặt d khối
        private readonly bool[] _rowFull;
        private readonly bool[] _colFull;
        private readonly BlockShapeData[] _set = new BlockShapeData[3];

        private int _budget;
        private bool _exhausted;

        /// <summary>Số lần thử đặt tối đa cho mỗi lần kiểm tra IsSolvable (chặn tình huống quá nặng).</summary>
        public int NodeBudget { get; set; } = 4000;

        public SpawnPlanner(int width, int height)
        {
            _w = width;
            _h = height;
            _boards = new bool[4][];
            for (int i = 0; i < _boards.Length; i++) _boards[i] = new bool[width * height];
            _rowFull = new bool[height];
            _colFull = new bool[width];
        }

        /// <summary>Nạp trạng thái bảng hiện tại (true = có gạch).</summary>
        public void Load(bool[] occupancy)
        {
            System.Array.Copy(occupancy, _boards[0], _boards[0].Length);
        }

        // ----------------------------------------------------------------- Bộ 3 khối có giải được không

        /// <summary>
        /// Đúng nếu có một thứ tự đặt cả 3 khối vào bảng đã nạp. Nếu mô phỏng quá nặng (hết NodeBudget)
        /// thì coi là giải được, vì không chứng minh được là bí.
        /// </summary>
        public bool IsSolvable(BlockShapeData a, BlockShapeData b, BlockShapeData c)
        {
            _set[0] = a;
            _set[1] = b;
            _set[2] = c;
            _budget = NodeBudget;
            _exhausted = false;

            bool ok = Solve(0, 0);
            return ok || _exhausted;
        }

        private bool Solve(int depth, int usedMask)
        {
            if (depth == _set.Length) return true;

            bool[] board = _boards[depth];
            bool[] next = _boards[depth + 1];

            for (int i = 0; i < _set.Length; i++)
            {
                if ((usedMask & (1 << i)) != 0) continue;

                // Hai khối giống hệt nhau chỉ cần thử một lần
                bool duplicate = false;
                for (int j = 0; j < i; j++)
                {
                    if ((usedMask & (1 << j)) == 0 && _set[j] == _set[i]) { duplicate = true; break; }
                }
                if (duplicate) continue;

                BlockShapeData shape = _set[i];
                Vector2Int[] cells = shape.NormalizedCells;
                Vector2Int size = shape.Size;

                for (int y = 0; y <= _h - size.y; y++)
                {
                    for (int x = 0; x <= _w - size.x; x++)
                    {
                        if (!Fits(board, cells, x, y)) continue;

                        if (--_budget < 0)
                        {
                            _exhausted = true;
                            return false;
                        }

                        System.Array.Copy(board, next, board.Length);
                        Place(next, cells, x, y);
                        ClearFullLines(next);

                        if (Solve(depth + 1, usedMask | (1 << i))) return true;
                        if (_exhausted) return false;
                    }
                }
            }
            return false;
        }

        // ----------------------------------------------------------------- Khối có xóa được đường không

        /// <summary>Có chỗ đặt khối này làm đầy ít nhất một hàng hoặc cột của bảng đã nạp không.</summary>
        public bool CanClearLine(BlockShapeData shape)
        {
            bool[] board = _boards[0];
            bool[] temp = _boards[3];
            Vector2Int[] cells = shape.NormalizedCells;
            Vector2Int size = shape.Size;

            for (int y = 0; y <= _h - size.y; y++)
            {
                for (int x = 0; x <= _w - size.x; x++)
                {
                    if (!Fits(board, cells, x, y)) continue;

                    System.Array.Copy(board, temp, board.Length);
                    Place(temp, cells, x, y);
                    if (HasFullLine(temp)) return true;
                }
            }
            return false;
        }

        // ----------------------------------------------------------------- Helper

        private bool Fits(bool[] board, Vector2Int[] cells, int ox, int oy)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (board[(oy + cells[i].y) * _w + ox + cells[i].x]) return false;
            }
            return true;
        }

        private void Place(bool[] board, Vector2Int[] cells, int ox, int oy)
        {
            for (int i = 0; i < cells.Length; i++)
                board[(oy + cells[i].y) * _w + ox + cells[i].x] = true;
        }

        private bool HasFullLine(bool[] board)
        {
            for (int y = 0; y < _h; y++)
            {
                bool full = true;
                for (int x = 0; x < _w; x++)
                {
                    if (!board[y * _w + x]) { full = false; break; }
                }
                if (full) return true;
            }

            for (int x = 0; x < _w; x++)
            {
                bool full = true;
                for (int y = 0; y < _h; y++)
                {
                    if (!board[y * _w + x]) { full = false; break; }
                }
                if (full) return true;
            }
            return false;
        }

        /// <summary>Xóa các hàng/cột đầy (đánh dấu hết rồi mới xóa, để ô giao nhau tính đúng).</summary>
        private void ClearFullLines(bool[] board)
        {
            bool any = false;

            for (int y = 0; y < _h; y++)
            {
                bool full = true;
                for (int x = 0; x < _w; x++)
                {
                    if (!board[y * _w + x]) { full = false; break; }
                }
                _rowFull[y] = full;
                any |= full;
            }

            for (int x = 0; x < _w; x++)
            {
                bool full = true;
                for (int y = 0; y < _h; y++)
                {
                    if (!board[y * _w + x]) { full = false; break; }
                }
                _colFull[x] = full;
                any |= full;
            }

            if (!any) return;

            for (int y = 0; y < _h; y++)
            {
                for (int x = 0; x < _w; x++)
                {
                    if (_rowFull[y] || _colFull[x]) board[y * _w + x] = false;
                }
            }
        }
    }
}
