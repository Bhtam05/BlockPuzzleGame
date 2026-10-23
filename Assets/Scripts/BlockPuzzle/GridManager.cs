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
        [SerializeField] private float placeFlash = 0.28f;
        [SerializeField, Range(0f, 1f)] private float previewAlpha = 0.45f;

        [Header("Đổi chất liệu")]
        [Tooltip("Khối đã đặt đổi chất liệu sau chừng này giây (chờ animation đặt/xóa xong).")]
        [SerializeField] private float skinSwapDelay = 0.3f;
        [Tooltip("Thêm bao nhiêu giây trễ cho mỗi 1 unit xa tâm gợn sóng.")]
        [SerializeField] private float skinRippleSpeed = 0.045f;

        [Header("Gần xong (hàng/cột sắp đầy sáng lên khi đang kéo khối)")]
        [SerializeField] private Sprite hintSprite;
        [SerializeField] private Color hintColor = new Color(1f, 0.9f, 0.5f, 1f);
        [SerializeField, Range(0f, 1f)] private float hintMinAlpha = 0.25f;
        [SerializeField, Range(0f, 1f)] private float hintMaxAlpha = 0.8f;
        [SerializeField] private float hintPulse = 0.45f;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;

        /// <summary>Giây chờ trước khi khối đổi chất liệu (khay dùng cùng độ trễ để khớp nhịp).</summary>
        public float SkinSwapDelay => skinSwapDelay;

        /// <summary>Chất liệu khối hiện tại (null = dùng sprite của prefab).</summary>
        public BlockSkin Skin => _skin;

        /// <summary>Sprite của chất liệu hiện tại, null nếu chưa có (giữ sprite của prefab).</summary>
        public Sprite SkinSprite => _skin != null ? _skin.sprite : null;

        /// <summary>Màu vẽ ô: gỗ tô theo màu shape, chất liệu khác giữ nguyên màu sprite (trắng).</summary>
        public Color SkinColor(Color shapeColor) => (_skin == null || _skin.tintByShape) ? shapeColor : Color.white;

        /// <summary>Tâm hình học của cả bảng (World Position). Dùng để đặt khung/nền bảng.</summary>
        public Vector3 BoardCenter
        {
            get
            {
                Transform origin = gridOrigin != null ? gridOrigin : transform;
                return origin.position + new Vector3((width - 1) * cellSize * 0.5f,
                                                     (height - 1) * cellSize * 0.5f, 0f);
            }
        }

        /// <summary>Thông tin lần xóa gần nhất (hàng, cột, vị trí và màu từng ô). Được tái sử dụng, chỉ đọc ngay sau ClearFullLines.</summary>
        public ClearInfo LastClear { get; } = new ClearInfo();

        private int[,] _grid;
        private SpriteRenderer[,] _blocks;
        private Color[,] _cellColor; // màu GỐC của shape ở từng ô (để lưu ván và đổi lại chất liệu gỗ đúng màu)
        private BlockSkin _skin;
        private bool[,] _sim;        // tính hàng/cột sắp đầy
        private bool[] _hintRow;
        private bool[] _hintCol;
        private readonly List<SpriteRenderer> _hintPool = new List<SpriteRenderer>();
        private int _hintActive;
        private float _hintT;
        private Tween _hintTween;
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
            _cellColor = new Color[width, height];
            _sim = new bool[width, height];
            _hintRow = new bool[height];
            _hintCol = new bool[width];
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
                ApplySkinTo(sr, color);
                sr.sortingOrder = 1;
                _blocks[x, y] = sr;
                _cellColor[x, y] = color;

                sr.transform.DOScale(cellSize, placeDuration).SetEase(Ease.OutBack);

                // Chớp sáng khi vừa đặt xuống rồi về màu gạch (SetTarget để DOKill của ô cũng dừng tween này)
                Color target = SkinColor(color);
                Color flash = Color.Lerp(target, Color.white, 0.75f);
                sr.color = flash;
                DOTween.To(() => 0f, t => sr.color = Color.Lerp(flash, target, t), 1f, placeFlash)
                    .SetEase(Ease.OutQuad).SetTarget(sr.transform);
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
            LastClear.Reset();

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

            // Ghi lại hàng/cột bị xóa để dựng hiệu ứng
            for (int y = 0; y < height; y++) if (_rowFull[y]) LastClear.Rows.Add(y);
            for (int x = 0; x < width; x++) if (_colFull[x]) LastClear.Cols.Add(x);

            // Bước 2: xóa các ô thuộc hàng/cột đầy
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!_rowFull[y] && !_colFull[x]) continue;

                    // Ghi vị trí + màu ô trước khi ClearCell giải phóng sprite
                    LastClear.CellPositions.Add(GridToWorld(x, y));
                    LastClear.CellColors.Add(_blocks[x, y] != null ? FxColorOf(x, y) : Color.white);
                    ClearCell(x, y);
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
            HidePreviewCells();
            color = SkinColor(color);
            color.a = previewAlpha;
            Sprite sprite = SkinSprite;

            for (int i = 0; i < cells.Length; i++)
            {
                SpriteRenderer sr = GetPreview(i);
                sr.gameObject.SetActive(true);
                if (sprite != null) sr.sprite = sprite;
                sr.transform.position = GridToWorld(origin.x + cells[i].x, origin.y + cells[i].y);
                sr.color = color;
            }
            _previewActive = cells.Length;

            RefreshHints(cells, origin);
        }

        public void HidePreview()
        {
            HidePreviewCells();
            ClearHints();
        }

        private void HidePreviewCells()
        {
            for (int i = 0; i < _previewActive; i++) _previewPool[i].gameObject.SetActive(false);
            _previewActive = 0;
        }

        // ----------------------------------------------------------------- Gần xong

        /// <summary>
        /// Nếu đặt khối ở vị trí này sẽ làm đầy hàng/cột nào thì cho cả hàng/cột đó sáng nhấp nháy,
        /// để người chơi thấy trước "sắp ăn". Chỉ cần xét hàng/cột mà khối chạm vào.
        /// </summary>
        private void RefreshHints(Vector2Int[] cells, Vector2Int origin)
        {
            if (hintSprite == null) return;

            for (int i = 0; i < cells.Length; i++)
            {
                int x = origin.x + cells[i].x, y = origin.y + cells[i].y;
                _sim[x, y] = true;
                _hintRow[y] = true;
                _hintCol[x] = true;
            }

            // Hàng/cột bị chạm chỉ giữ lại nếu mọi ô đều đã có gạch hoặc nằm trong khối đang đưa vào
            for (int y = 0; y < height; y++)
            {
                if (!_hintRow[y]) continue;
                for (int x = 0; x < width; x++)
                    if (_grid[x, y] == 0 && !_sim[x, y]) { _hintRow[y] = false; break; }
            }
            for (int x = 0; x < width; x++)
            {
                if (!_hintCol[x]) continue;
                for (int y = 0; y < height; y++)
                    if (_grid[x, y] == 0 && !_sim[x, y]) { _hintCol[x] = false; break; }
            }

            for (int i = 0; i < cells.Length; i++) _sim[origin.x + cells[i].x, origin.y + cells[i].y] = false;

            // Hiện quầng sáng, mỗi ô giao nhau chỉ một lần
            int count = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (_hintRow[y] || _hintCol[x])
                    {
                        SpriteRenderer sr = GetHint(count++);
                        sr.gameObject.SetActive(true);
                        sr.transform.position = GridToWorld(x, y);
                    }
                }
            }

            // Trả lại cờ tạm
            for (int y = 0; y < height; y++) _hintRow[y] = false;
            for (int x = 0; x < width; x++) _hintCol[x] = false;

            for (int i = count; i < _hintActive; i++) _hintPool[i].gameObject.SetActive(false);
            _hintActive = count;

            if (_hintActive == 0) { KillHintTween(); return; }

            ApplyHintAlpha();
            if (_hintTween == null || !_hintTween.IsActive())
            {
                _hintTween = DOTween.To(() => _hintT, v => { _hintT = v; ApplyHintAlpha(); }, 1f, hintPulse)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetTarget(this);
            }
        }

        private void ApplyHintAlpha()
        {
            Color c = hintColor;
            c.a = Mathf.Lerp(hintMinAlpha, hintMaxAlpha, _hintT);
            for (int i = 0; i < _hintActive; i++) _hintPool[i].color = c;
        }

        private void ClearHints()
        {
            for (int i = 0; i < _hintActive; i++) _hintPool[i].gameObject.SetActive(false);
            _hintActive = 0;
            KillHintTween();
        }

        private void KillHintTween()
        {
            if (_hintTween != null) _hintTween.Kill();
            _hintTween = null;
            _hintT = 0f;
        }

        private SpriteRenderer GetHint(int index)
        {
            while (_hintPool.Count <= index)
            {
                var go = new GameObject("Hint");
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * cellSize;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = hintSprite;
                sr.sortingOrder = 3; // trên khối và bóng xem trước, dưới khối đang kéo
                go.SetActive(false);
                _hintPool.Add(sr);
            }
            return _hintPool[index];
        }

        // ----------------------------------------------------------------- Chất liệu khối

        /// <summary>Gán sprite + màu của chất liệu hiện tại cho một ô.</summary>
        private void ApplySkinTo(SpriteRenderer sr, Color shapeColor)
        {
            Sprite sprite = SkinSprite;
            if (sprite != null) sr.sprite = sprite;
            sr.color = SkinColor(shapeColor);
        }

        /// <summary>Màu mảnh vỡ khi xóa ô: chất liệu không tô màu dùng màu hiệu ứng riêng, gỗ dùng màu shape.</summary>
        private Color FxColorOf(int x, int y)
        {
            if (_skin != null && !_skin.tintByShape) return _skin.fxColor;
            return _cellColor[x, y];
        }

        /// <summary>
        /// Đổi chất liệu cho mọi khối trên bảng. animate = true: mỗi ô đổi sau skinSwapDelay + khoảng cách tới
        /// <paramref name="rippleFrom"/> * skinRippleSpeed nên tạo thành một gợn sóng lan ra, ô nào đổi thì nảy lên và chớp sáng.
        /// </summary>
        public void SetSkin(BlockSkin skin, bool animate, Vector3 rippleFrom)
        {
            _skin = skin;
            if (_blocks == null) return; // chưa Awake xong

            Sprite sprite = SkinSprite;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    SpriteRenderer sr = _blocks[x, y];
                    if (sr == null) continue;

                    Color target = SkinColor(_cellColor[x, y]);
                    if (!animate)
                    {
                        if (sprite != null) sr.sprite = sprite;
                        sr.color = target;
                        continue;
                    }

                    float delay = skinSwapDelay + Vector3.Distance(GridToWorld(x, y), rippleFrom) * skinRippleSpeed;
                    int cx = x, cy = y;
                    DOVirtual.DelayedCall(delay, () =>
                    {
                        if (_blocks[cx, cy] != sr) return; // ô đã bị xóa / dọn trong lúc chờ
                        Transform tr = sr.transform;
                        if (sprite != null) sr.sprite = sprite;
                        Color flash = Color.Lerp(target, Color.white, 0.8f);
                        sr.color = flash;
                        tr.localScale = Vector3.one * (cellSize * 0.55f);
                        tr.DOScale(cellSize, 0.35f).SetEase(Ease.OutBack).SetTarget(tr);
                        DOTween.To(() => 0f, t => sr.color = Color.Lerp(flash, target, t), 1f, 0.4f)
                            .SetEase(Ease.OutQuad).SetTarget(tr);
                    }).SetTarget(sr.transform);
                }
            }
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

        // ----------------------------------------------------------------- Lưu / khôi phục ván

        /// <summary>Số ô đang có gạch.</summary>
        public int FilledCount
        {
            get
            {
                int n = 0;
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        if (_grid[x, y] != 0) n++;
                return n;
            }
        }

        /// <summary>Chép trạng thái bảng vào mảng phẳng (chỉ số y * width + x, true = có gạch). Dùng cho SpawnPlanner.</summary>
        public void CopyOccupancy(bool[] buffer)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    buffer[y * width + x] = _grid[x, y] != 0;
        }

        /// <summary>Tỉ lệ bảng đã lấp đầy, 0..1 (dùng chỉnh độ khó khi sinh khối).</summary>
        public float FillRatio => (float)FilledCount / (width * height);

        /// <summary>
        /// Ghi trạng thái bảng vào mảng phẳng (chỉ số x * height + y): 0 = trống, khác 0 = màu ô đã nén.
        /// Màu luôn có alpha 255 nên giá trị khác 0.
        /// </summary>
        public void ExportCells(int[] buffer)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int i = x * height + y;
                    if (_grid[x, y] == 0 || _blocks[x, y] == null) { buffer[i] = 0; continue; }

                    Color32 c = _cellColor[x, y]; // màu gốc của shape (không phụ thuộc chất liệu đang hiện)
                    buffer[i] = (c.r << 24) | (c.g << 16) | (c.b << 8) | 255;
                }
            }
        }

        /// <summary>Dựng lại bảng từ mảng do ExportCells ghi ra (không chạy animation).</summary>
        public void ImportCells(int[] data)
        {
            ResetGrid();
            if (data == null || data.Length != width * height) return;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int v = data[x * height + y];
                    if (v == 0) continue;

                    _grid[x, y] = 1;

                    SpriteRenderer sr = GetBlock();
                    sr.transform.DOKill();
                    sr.transform.position = GridToWorld(x, y);
                    sr.transform.localScale = Vector3.one * cellSize;
                    Color32 c32 = new Color32((byte)((v >> 24) & 0xFF), (byte)((v >> 16) & 0xFF),
                                              (byte)((v >> 8) & 0xFF), 255);
                    ApplySkinTo(sr, c32);
                    sr.sortingOrder = 1;
                    _blocks[x, y] = sr;
                    _cellColor[x, y] = c32;
                }
            }
        }

        // ----------------------------------------------------------------- Reset & Pool

        /// <summary>Xóa sạch bảng (dùng khi chơi lại).</summary>
        public void ResetGrid()
        {
            HidePreview();
            DOTween.Kill(this);
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
