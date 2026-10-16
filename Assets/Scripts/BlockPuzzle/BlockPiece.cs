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
        [SerializeField] private float colliderPadding = 0.6f;
        [SerializeField] private int traySortingOrder = 10;
        [SerializeField] private int dragSortingOrder = 100;

        public BlockShapeData Shape { get; private set; }

        private BoxCollider2D _collider;
        private Camera _cam;
        private GridManager _grid;
        private Vector3 _homePosition;
        private Vector2 _pivot;
        private bool _dragging;

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
                sr.color = shape.color;
                sr.sortingOrder = traySortingOrder;
                _renderers.Add(sr);
            }

            // Vùng chạm rộng hơn khối một chút cho dễ bắt
            _collider.size = new Vector2(size.x * grid.CellSize + colliderPadding,
                                         size.y * grid.CellSize + colliderPadding);

            transform.position = homePosition;
            transform.localScale = Vector3.zero;
            transform.DOScale(trayScale, 0.25f).SetEase(Ease.OutBack);
        }

        // ----------------------------------------------------------------- Input

        public void OnPointerDown(PointerEventData e)
        {
            if (_dragging || s_active != null) return;
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

            s_active = this;
            _dragging = true;

            transform.DOKill();
            SetSorting(dragSortingOrder);
            transform.DOScale(1f, 0.1f).SetEase(Ease.OutQuad);
            transform.position = PointerToWorld(e) + (Vector3)liftOffset;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging) return;
            transform.position = PointerToWorld(e) + (Vector3)liftOffset;
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

        private void SetSorting(int order)
        {
            for (int i = 0; i < _renderers.Count; i++) _renderers[i].sortingOrder = order;
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
