using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BlockPuzzle
{
    /// <summary>
    /// Sinh 3 khối ngẫu nhiên vào 3 slot ở khay phía dưới.
    /// Khi dùng hết cả 3 khối thì GameManager gọi SpawnSet() để sinh bộ mới.
    /// </summary>
    public class BlockSpawner : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [SerializeField] private BlockPiece piecePrefab;
        [Tooltip("Các vị trí đặt khối trong khay (thường 3 điểm).")]
        [SerializeField] private Transform[] slots;
        [Tooltip("Để trống sẽ dùng bộ shape mặc định tạo bằng code.")]
        [SerializeField] private List<BlockShapeData> shapes = new List<BlockShapeData>();
        [Tooltip("Số lần thử random để đảm bảo bộ khối có ít nhất 1 khối đặt được.")]
        [SerializeField] private int fairnessTries = 30;
        [SerializeField] private Camera cam;

        private BlockPiece[] _pieces;
        private int _remaining;
        private BlockShapeData[] _picked;

        /// <summary>Danh sách khối đang có (phần tử đã dùng sẽ là null).</summary>
        public IReadOnlyList<BlockPiece> Pieces => _pieces;
        public bool IsEmpty => _remaining == 0;

        private void Awake()
        {
            if (cam == null) cam = Camera.main; // chỉ gọi 1 lần
            if (shapes == null || shapes.Count == 0) shapes = BlockShapeLibrary.CreateDefaults();

            _pieces = new BlockPiece[slots.Length];
            _picked = new BlockShapeData[slots.Length];
        }

        /// <summary>Sinh bộ khối mới cho tất cả slot.</summary>
        public void SpawnSet()
        {
            PickShapes();

            for (int i = 0; i < slots.Length; i++)
            {
                BlockPiece piece = Instantiate(piecePrefab, slots[i].position, Quaternion.identity, transform);
                piece.Init(_picked[i], grid, cam, slots[i].position);
                _pieces[i] = piece;
            }
            _remaining = slots.Length;
        }

        /// <summary>
        /// Random bộ khối. Thử tối đa fairnessTries lần để có ít nhất 1 khối còn đặt được,
        /// tránh game over vô lý ngay từ lúc sinh khối.
        /// </summary>
        private void PickShapes()
        {
            for (int attempt = 0; attempt < fairnessTries; attempt++)
            {
                bool anyFits = false;
                for (int i = 0; i < _picked.Length; i++)
                {
                    _picked[i] = shapes[Random.Range(0, shapes.Count)];
                    if (!anyFits && grid.CanPlaceAnywhere(_picked[i])) anyFits = true;
                }
                if (anyFits) return;
            }
            // Hết lượt thử vẫn không có khối nào vừa -> bảng thật sự bí, để GameManager xử lý Game Over
        }

        /// <summary>Gỡ khối đã đặt vào bảng.</summary>
        public void Release(BlockPiece piece)
        {
            int index = Array.IndexOf(_pieces, piece);
            if (index < 0) return;

            _pieces[index] = null;
            _remaining--;
            Destroy(piece.gameObject);
        }

        /// <summary>Xóa toàn bộ khối trong khay (khi chơi lại).</summary>
        public void ClearAll()
        {
            for (int i = 0; i < _pieces.Length; i++)
            {
                if (_pieces[i] != null) Destroy(_pieces[i].gameObject);
                _pieces[i] = null;
            }
            _remaining = 0;
        }
    }
}
