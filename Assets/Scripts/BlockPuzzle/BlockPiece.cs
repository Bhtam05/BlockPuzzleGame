using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BlockPuzzle
{
    /// <summary>
    /// Khối gạch kéo thả. Gắn vào Prefab (cần BoxCollider2D).
    /// Yêu cầu scene: EventSystem + Physics2DRaycaster trên Main Camera.
    /// Khi thả hợp lệ sẽ bắn event Placed, GameManager xử lý việc đặt vào bảng.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class BlockPiece : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        /// <summary>(piece, ô gốc dưới-trái trên grid)</summary>
        public static event Action<BlockPiece, Vector2Int> Placed;

        // Chỉ cho kéo 1 khối tại 1 thời điểm (chống multi-touch)
        private static BlockPiece s_active;

        [SerializeField] private SpriteRenderer cellPrefab;
        [SerializeField] private float trayScale = 0.6f;
        [Tooltip("Khối nổi lên trên ngón tay để không bị che.")]
        [SerializeField] private Vector2 liftOffset = new Vector2(0f, 1.8f);
        [Tooltip("Ngón tay kéo lên bao nhiêu thì khối đi lên gấp chừng đó lần (1 = đi cùng tốc độ). Giúp khối lên tới hàng trên cùng của bảng khi tay mới tới giữa màn hình.")]
        [SerializeField, Range(1f, 2.5f)] private float verticalGain = 1.5f;
        [Tooltip("Tương tự theo chiều ngang: tay kéo sang trái/phải bao nhiêu thì khối đi gấp chừng đó lần (1 = cùng tốc độ).")]
        [SerializeField, Range(1f, 2.5f)] private float horizontalGain = 1.4f;
        [Tooltip("Khối được dịch thêm tối đa bao nhiêu unit sang hai bên nhờ horizontalGain.")]
        [SerializeField] private float maxExtraShift = 2.5f;
        [Tooltip("Khối được nâng thêm tối đa bao nhiêu unit so với lift cơ bản nhờ verticalGain (giữ khối không bay quá xa tay).")]
        [SerializeField] private float maxExtraLift = 4f;
        [Header("Vùng chạm (đơn vị World, tính theo kích thước khối trong khay)")]
        [Tooltip("Đệm thêm quanh khối khi còn nằm trong khay.")]
        [SerializeField] private float hitPadding = 0.7f;
        [Tooltip("Vùng chạm tối thiểu mỗi chiều, để khối nhỏ (1x1, 2x1) vẫn dễ bắt bằng ngón tay (3.0 = bằng khoảng cách 2 khối trong khay).")]
        [SerializeField] private float minHitSize = 2.6f;
        [Tooltip("Vùng chạm tối đa mỗi chiều, giữ dưới khoảng cách giữa 2 ô khay để không lấn sang khối bên cạnh.")]
        [SerializeField] private float maxHitSize = 2.95f;
        [SerializeField] private int traySortingOrder = 10;
        [SerializeField] private int dragSortingOrder = 100;

        public BlockShapeData Shape { get; private set; }

        private BoxCollider2D _collider;
        private Camera _cam;
        private GridManager _grid;
        private Vector3 _homePosition;
        private Vector2 _pivot;
        private bool _dragging;
        private float _startPointerX; // vị trí (world) của ngón tay lúc bắt đầu kéo
        private float _startPointerY;

        private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
        private Vector2Int _lastOrigin = new Vector2Int(int.MinValue, int.MinValue);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_active = null;
            Placed = null;
        }

        /// <summary>Khởi tạo khối: dựng các ô con, đặt vị trí khay và chạy animation xuất hiện.</summary>
        public void Init(BlockShapeData shape, GridManager grid, Camera cam, Vector3 homePosition)
        {
            Shape = shape;
            _grid = grid;
            _cam = cam;
            _homePosition = homePosition;

            _collider = GetComponent<BoxCollider2D>();

            Vector2Int size = shape.Size;
            // Tâm bounding box (tính theo đơn vị ô). Gốc transform = tâm bounding box
            _pivot = new Vector2((size.x - 1) / 2f, (size.y - 1) / 2f);

            foreach (Vector2Int c in shape.NormalizedCells)
            {
                SpriteRenderer sr = Instantiate(cellPrefab, transform);
                sr.transform.localPosition = ((Vector2)c - _pivot) * grid.CellSize;
                sr.transform.localScale = Vector3.one * grid.CellSize;
                if (grid.SkinSprite != null) sr.sprite = grid.SkinSprite;
                sr.color = grid.SkinColor(shape.color);
                sr.sortingOrder = traySortingOrder;
                _renderers.Add(sr);
            }

            // Vùng chạm tính theo kích thước thật trên màn hình (khối trong khay bị scale nhỏ lại trayScale),
            // có đệm và có mức tối thiểu để khối nhỏ vẫn dễ bắt, nhưng không vượt khoảng cách giữa các ô khay.
            float scale = Mathf.Max(0.01f, trayScale);
            float worldW = Mathf.Clamp(size.x * grid.CellSize * scale + hitPadding, minHitSize, Mathf.Max(minHitSize, maxHitSize));
            float worldH = Mathf.Clamp(size.y * grid.CellSize * scale + hitPadding, minHitSize, Mathf.Max(minHitSize, maxHitSize));
            _collider.size = new Vector2(worldW / scale, worldH / scale);

            transform.position = homePosition;
            transform.localScale = Vector3.zero;
            transform.DOScale(trayScale, 0.25f).SetEase(Ease.OutBack);
        }

        /// <summary>
        /// Hủy thao tác kéo đang diễn ra (khối bay về khay). Gọi khi Pause hoặc app ra nền,
        /// vì lúc đó Android có thể không gửi sự kiện nhả tay nên khối sẽ kẹt và chặn mọi lần kéo sau.
        /// </summary>
        public static void CancelActiveDrag()
        {
            BlockPiece p = s_active;
            if (p == null) return;

            p._dragging = false;
            s_active = null;
            p._grid.HidePreview();
            p._lastOrigin = new Vector2Int(int.MinValue, int.MinValue);
            p.ReturnToTray();
        }

        // ----------------------------------------------------------------- Input

        public void OnPointerDown(PointerEventData e)
        {
            if (_dragging || s_active != null) return;
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

            s_active = this;
            _dragging = true;
            AudioManager.Play(Sfx.Pick);

            transform.DOKill(); // cũng dừng animation đổi chất liệu đang chờ (OnKill sẽ áp chất liệu mới ngay)
            SetSorting(dragSortingOrder);
            transform.DOScale(1f, 0.1f).SetEase(Ease.OutQuad);
            Vector3 start = PointerToWorld(e);
            _startPointerX = start.x;
            _startPointerY = start.y;
            transform.position = DragPosition(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging) return;
            transform.position = DragPosition(e);
            UpdatePreview();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_dragging) return;

            _dragging = false;
            s_active = null;
            _grid.HidePreview();
            _lastOrigin = new Vector2Int(int.MinValue, int.MinValue);

            // Đang Pause/Game Over mà vẫn thả khối (kéo bằng ngón khác) thì cho bay về khay, không để kẹt
            bool canDrop = GameManager.Instance != null && GameManager.Instance.IsPlaying;

            Vector2Int origin = GetOriginCell();
            if (canDrop && _grid.CanPlace(Shape.NormalizedCells, origin))
            {
                Placed?.Invoke(this, origin);
            }
            else
            {
                ReturnToTray();
            }
        }

        // ----------------------------------------------------------------- Snap & Preview

        /// <summary>
        /// Tính ô grid gốc (dưới-trái) mà khối đang lơ lửng phía trên.
        /// Khi kéo, scale = 1 nên vị trí ô (0,0) của khối = tâm - pivot * cellSize.
        /// </summary>
        private Vector2Int GetOriginCell()
        {
            Vector3 bottomLeftCell = transform.position - (Vector3)(_pivot * _grid.CellSize);
            return _grid.WorldToGrid(bottomLeftCell);
        }

        private void UpdatePreview()
        {
            Vector2Int origin = GetOriginCell();
            if (origin == _lastOrigin) return; // chưa đổi ô -> khỏi tính lại
            _lastOrigin = origin;

            if (_grid.CanPlace(Shape.NormalizedCells, origin))
                _grid.ShowPreview(Shape.NormalizedCells, origin, Shape.color);
            else
                _grid.HidePreview();
        }

        // ----------------------------------------------------------------- Tween

        /// <summary>Thả sai chỗ: bay về vị trí ban đầu trong khay.</summary>
        private void ReturnToTray()
        {
            _collider.enabled = false;
            transform.DOKill();
            transform.DOScale(trayScale, 0.25f);
            transform.DOMove(_homePosition, 0.25f)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                {
                    _collider.enabled = true;
                    SetSorting(traySortingOrder);
                });
        }

        // ----------------------------------------------------------------- Đổi chất liệu

        /// <summary>
        /// Đổi chất liệu theo bảng. Mỗi ô con đổi lần lượt sau baseDelay (cùng nhịp với gợn sóng trên bảng) và nảy lên.
        /// Nếu bị ngắt giữa chừng (người chơi nhấc khối lên) thì áp chất liệu mới ngay.
        /// </summary>
        public void RefreshSkin(float baseDelay)
        {
            Sequence seq = DOTween.Sequence().SetTarget(transform);
            for (int i = 0; i < _renderers.Count; i++)
            {
                SpriteRenderer sr = _renderers[i];
                seq.InsertCallback(baseDelay + i * 0.05f, () =>
                {
                    if (sr == null) return;
                    ApplySkinTo(sr);
                    Transform tr = sr.transform;
                    tr.localScale = Vector3.one * (_grid.CellSize * 0.55f);
                    tr.DOScale(_grid.CellSize, 0.3f).SetEase(Ease.OutBack).SetTarget(transform);
                });
            }
            bool finished = false;
            seq.OnComplete(() => finished = true);
            seq.OnKill(() => { if (!finished) ApplySkinNow(); });
        }

        private void ApplySkinTo(SpriteRenderer sr)
        {
            if (_grid.SkinSprite != null) sr.sprite = _grid.SkinSprite;
            sr.color = _grid.SkinColor(Shape.color);
        }

        /// <summary>Áp chất liệu hiện tại cho mọi ô con và trả scale về chuẩn (dùng khi animation đổi bị ngắt).</summary>
        private void ApplySkinNow()
        {
            if (_grid == null || Shape == null) return;
            for (int i = 0; i < _renderers.Count; i++)
            {
                SpriteRenderer sr = _renderers[i];
                if (sr == null) continue;
                ApplySkinTo(sr);
                sr.transform.localScale = Vector3.one * _grid.CellSize;
            }
        }

        private void SetSorting(int order)
        {
            for (int i = 0; i < _renderers.Count; i++) _renderers[i].sortingOrder = order;
        }

        /// <summary>
        /// Vị trí khối khi kéo = ngón tay + lift cơ bản + phần nâng/dịch thêm tăng dần khi tay đi xa điểm bắt đầu.
        /// Nhờ vậy tay chỉ cần tới khoảng giữa màn hình mà khối đã lên được hàng trên cùng của bảng.
        /// </summary>
        private Vector3 DragPosition(PointerEventData e)
        {
            Vector3 p = PointerToWorld(e);
            float rise = Mathf.Max(0f, p.y - _startPointerY);
            float extra = Mathf.Min(maxExtraLift, rise * (verticalGain - 1f));

            // Ngang: dịch thêm theo hướng tay đang đi, tối đa maxExtraShift
            float dx = p.x - _startPointerX;
            float shift = Mathf.Clamp(dx * (horizontalGain - 1f), -maxExtraShift, maxExtraShift);

            return p + (Vector3)liftOffset + new Vector3(shift, extra, 0f);
        }

        private Vector3 PointerToWorld(PointerEventData e)
        {
            Vector3 w = _cam.ScreenToWorldPoint(new Vector3(e.position.x, e.position.y, -_cam.transform.position.z));
            w.z = 0f;
            return w;
        }

        private void OnDestroy()
        {
            transform.DOKill();
            if (s_active == this) s_active = null;
        }
    }
}
