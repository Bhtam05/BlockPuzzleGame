using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Dữ liệu hình dạng 1 khối gạch (ScriptableObject).
    /// cells: danh sách tọa độ ô (x phải, y lên). Không cần chuẩn hóa sẵn, code tự dời về gốc (0,0).
    /// </summary>
    [CreateAssetMenu(menuName = "BlockPuzzle/Block Shape", fileName = "Shape_New")]
    public class BlockShapeData : ScriptableObject
    {
        public Vector2Int[] cells = { Vector2Int.zero };
        public Color color = Color.white;

        [System.NonSerialized] private Vector2Int[] _normalized;
        [System.NonSerialized] private Vector2Int _size;

        /// <summary>Các ô đã dời sao cho min x = 0 và min y = 0 (cache, không cấp phát lại).</summary>
        public Vector2Int[] NormalizedCells
        {
            get { if (_normalized == null) Normalize(); return _normalized; }
        }

        /// <summary>Kích thước bounding box (số ô ngang, số ô dọc).</summary>
        public Vector2Int Size
        {
            get { if (_normalized == null) Normalize(); return _size; }
        }

        public int CellCount => NormalizedCells.Length;

        private void OnValidate() => _normalized = null;

        private void Normalize()
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var c in cells)
            {
                if (c.x < minX) minX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.x > maxX) maxX = c.x;
                if (c.y > maxY) maxY = c.y;
            }

            _normalized = new Vector2Int[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                _normalized[i] = new Vector2Int(cells[i].x - minX, cells[i].y - minY);

            _size = new Vector2Int(maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>
        /// Tạo shape lúc runtime từ mảng chuỗi ('#' = có ô). Dòng đầu tiên là hàng trên cùng.
        /// </summary>
        public static BlockShapeData Create(string[] rows, Color color)
        {
            var data = CreateInstance<BlockShapeData>();
            var list = new System.Collections.Generic.List<Vector2Int>();
            for (int r = 0; r < rows.Length; r++)
            {
                for (int x = 0; x < rows[r].Length; x++)
                {
                    if (rows[r][x] == '#') list.Add(new Vector2Int(x, rows.Length - 1 - r));
                }
            }
            data.cells = list.ToArray();
            data.color = color;
            return data;
        }
    }
}
