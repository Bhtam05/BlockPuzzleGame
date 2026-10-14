using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Quản lý bảng chơi: mảng int[,] (0 = trống, 1 = có gạch), kiểm tra đặt khối,
    /// xóa hàng/cột đầy và quy đổi tọa độ Grid &lt;-&gt; World.
    /// Quy ước: ô (0,0) nằm góc dưới-trái. Sprite prefab nên có kích thước 1 unit (scale = cellSize).
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField, Range(4, 12)] private int width = 8;
        [SerializeField, Range(4, 12)] private int height = 8;
        [SerializeField] private float cellSize = 1f;
        [Tooltip("Vị trí TÂM của ô (0,0). Nếu để trống sẽ dùng chính transform này.")]
        [SerializeField] private Transform gridOrigin;

        [Header("Prefab")]
        [SerializeField] private SpriteRenderer emptyCellPrefab;
        [SerializeField] private SpriteRenderer blockPrefab;

        [Header("FX")]
        [SerializeField] private float placeDuration = 0.12f;
        [SerializeField] private float clearDuration = 0.25f;
        [SerializeField, Range(0f, 1f)] private float previewAlpha = 0.45f;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;

        private int[,] _grid;
        private SpriteRenderer[,] _blocks;
        private bool[] _rowFull;
        private bool[] _colFull;

        // Object pool để không Instantiate/Destroy liên tục
        private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
        private readonly List<SpriteRenderer> _previewPool = new List<SpriteRenderer>();
        private int _previewActive;

        private void Awake()
        {
            if (gridOrigin == null) gridOrigin = transform;

            _grid = new int[width, height];
            _blocks = new SpriteRenderer[width, height];
            _rowFull = new bool[height];
            _colFull = new bool[width];

            BuildBackground();
        }

        /// <summary>Sinh các ô nền rỗng của bảng.</summary>
        private void BuildBackground()
        {
            if (emptyCellPrefab == null) return;

            var bgRoot = new GameObject("GridBackground").transform;
            bgRoot.SetParent(transform, false);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var cell = Instantiate(emptyCellPrefab, GridToWorld(x, y), Quaternion.identity, bgRoot);
                    cell.transform.localScale = Vector3.one * cellSize;
                    cell.sortingOrder = 0;
                }
            }
        }

        // ----------------------------------------------------------------- Toạ độ

        /// <summary>Tọa độ ô -> World Position (tâm ô).</summary>
        public Vector3 GridToWorld(int x, int y)
        {
            return gridOrigin.position + new Vector3(x * cellSize, y * cellSize, 0f);
        }

        /// <summary>World Position -> tọa độ ô gần nhất (có thể nằm ngoài bảng).</summary>
        public Vector2Int WorldToGrid(Vector3 world)
        {
            Vector3 local = world - gridOrigin.position;
            return new Vector2Int(
                Mathf.RoundToInt(local.x / cellSize),
                Mathf.RoundToInt(local.y / cellSize));
        }

        public bool IsInside(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

        // ----------------------------------------------------------------- Kiểm tra đặt khối

        /// <summary>Kiểm tra khối (cells đã chuẩn hóa) đặt tại origin có hợp lệ không.</summary>
        public bool CanPlace(Vector2Int[] cells, Vector2Int origin)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                int x = origin.x + cells[i].x;
                int y = origin.y + cells[i].y;
                if (!IsInside(x, y) || _grid[x, y] != 0) return false;
            }
            return true;
        }

        /// <summary>Khối này còn đặt được ở bất kỳ vị trí nào trên bảng không? (dùng cho Game Over)</summary>
        public bool CanPlaceAnywhere(BlockShapeData shape)
        {
            var cells = shape.NormalizedCells;
            Vector2Int size = shape.Size;

            for (int y = 0; y <= height - size.y; y++)
            {
                for (int x = 0; x <= width - size.x; x++)
                {
                    if (CanPlace(cells, new Vector2Int(x, y))) return true;
                }
            }
            return false;
        }

        // ----------------------------------------------------------------- Đặt khối

        /// <summary>Đặt khối vào bảng (giả định đã kiểm tra CanPlace).</summary>
        public void PlaceShape(Vector2Int[] cells, Vector2Int origin, Color color)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                int x = origin.x + cells[i].x;
                int y = origin.y + cells[i].y;

                _grid[x, y] = 1;

                SpriteRenderer sr = GetBlock();
                sr.transform.position = GridToWorld(x, y);
                sr.transform.localScale = Vector3.zero;
                sr.color = color;
                sr.sortingOrder = 1;
                _blocks[x, y] = sr;

                sr.transform.DOScale(cellSize, placeDuration).SetEase(Ease.OutBack);
            }
        }

        // ----------------------------------------------------------------- Xóa hàng / cột

        /// <summary>
        /// Xóa tất cả hàng ngang/cột dọc đã đầy. Trả về tổng số hàng + cột bị xóa (dùng tính combo).
        /// Ô giao nhau giữa hàng và cột chỉ bị xóa 1 lần.
        /// </summary>
        public int ClearFullLines()
        {
            int lines = 0;

            // Bước 1: đánh dấu trước, chưa xóa gì để các đường giao nhau vẫn tính đúng
            for (int y = 0; y < height; y++)
            {
                bool full = true;
                for (int x = 0; x < width; x++)
                {
                    if (_grid[x, y] == 0) { full = false; break; }
                }
                _rowFull[y] = full;
                if (full) lines++;
            }

            for (int x = 0; x < width; x++)
            {
                bool full = true;
                for (int y = 0; y < height; y++)
                {
                    if (_grid[x, y] == 0) { full = false; break; }
                }
                _colFull[x] = full;
                if (full) lines++;
            }

            if (lines == 0) return 0;

            // Bước 2: xóa các ô thuộc hàng/cột đầy
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (_rowFull[y] || _colFull[x]) ClearCell(x, y);
                }
            }

            return lines;
        }

        private void ClearCell(int x, int y)
        {
            _grid[x, y] = 0;

            SpriteRenderer sr = _blocks[x, y];
            _blocks[x, y] = null;
            if (sr == null) return;

            sr.transform.DOKill();
            sr.transform.DOScale(0f, clearDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() => ReleaseBlock(sr));
        }

        // ----------------------------------------------------------------- Preview

        /// <summary>Hiện bóng mờ báo vị trí khối sẽ rơi vào.</summary>
        public void ShowPreview(Vector2Int[] cells, Vector2Int origin, Color color)
        {
            HidePreview();
            color.a = previewAlpha;

            for (int i = 0; i < cells.Length; i++)
            {
                SpriteRenderer sr = GetPreview(i);
                sr.gameObject.SetActive(true);
                sr.transform.position = GridToWorld(origin.x + cells[i].x, origin.y + cells[i].y);
                sr.color = color;
            }
            _previewActive = cells.Length;
        }

        public void HidePreview()
        {
            for (int i = 0; i < _previewActive; i++) _previewPool[i].gameObject.SetActive(false);
            _previewActive = 0;
        }

        private SpriteRenderer GetPreview(int index)
        {
            while (_previewPool.Count <= index)
            {
                var sr = Instantiate(blockPrefab, transform);
                sr.transform.localScale = Vector3.one * cellSize;
                sr.sortingOrder = 2;
                sr.gameObject.SetActive(false);
                _previewPool.Add(sr);
            }
            return _previewPool[index];
        }

        // ----------------------------------------------------------------- Reset & Pool

        /// <summary>Xóa sạch bảng (dùng khi chơi lại).</summary>
        public void ResetGrid()
        {
            HidePreview();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    _grid[x, y] = 0;
                    if (_blocks[x, y] != null)
                    {
                        _blocks[x, y].transform.DOKill();
                        ReleaseBlock(_blocks[x, y]);
                        _blocks[x, y] = null;
                    }
                }
            }
        }

        private SpriteRenderer GetBlock()
        {
            SpriteRenderer sr = _pool.Count > 0 ? _pool.Pop() : Instantiate(blockPrefab, transform);
            sr.gameObject.SetActive(true);
            return sr;
        }

        private void ReleaseBlock(SpriteRenderer sr)
        {
            sr.gameObject.SetActive(false);
            _pool.Push(sr);
        }
    }
}
