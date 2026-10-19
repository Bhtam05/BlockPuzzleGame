using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BlockPuzzle
{
    /// <summary>
    /// Sinh 3 khối ngẫu nhiên vào 3 slot ở khay phía dưới.
    /// Khi dùng hết cả 3 khối thì GameManager gọi SpawnSet() để sinh bộ mới.
    /// Độ khó thích ứng: bảng càng đầy thì khối nhỏ, dễ đặt càng hay ra, khối lớn ít ra hơn.
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

        [Header("Độ khó thích ứng")]
        [Tooltip("Bật: bảng càng đầy càng ưu tiên khối nhỏ. Tắt: random đều.")]
        [SerializeField] private bool adaptiveDifficulty = true;
        [Tooltip("Từ tỉ lệ lấp đầy này trở lên bắt đầu giảm khối lớn, tăng khối nhỏ (0..1).")]
        [SerializeField, Range(0.2f, 0.9f)] private float crowdedFrom = 0.5f;
        [Tooltip("Từ tỉ lệ này trở lên là mức hỗ trợ tối đa (0..1).")]
        [SerializeField, Range(0.3f, 1f)] private float crowdedMax = 0.85f;
        [Tooltip("Hệ số tăng khả năng ra khối nhỏ (<=3 ô) ở mức hỗ trợ tối đa.")]
        [SerializeField, Min(1f)] private float smallBoost = 3f;
        [Tooltip("Hệ số giảm khả năng ra khối lớn (>=5 ô) ở mức hỗ trợ tối đa (0..1).")]
        [SerializeField, Range(0.05f, 1f)] private float bigPenalty = 0.25f;

        private BlockPiece[] _pieces;
        private int[] _pieceShape;  // chỉ số shape của từng slot, -1 = trống / đã dùng
        private int[] _pickedIdx;   // bộ khối vừa random
        private float[] _weights;
        private int _remaining;

        /// <summary>Danh sách khối đang có (phần tử đã dùng sẽ là null).</summary>
        public IReadOnlyList<BlockPiece> Pieces => _pieces;
        public bool IsEmpty => _remaining == 0;
        public int SlotCount => slots.Length;

        private void Awake()
        {
            if (cam == null) cam = Camera.main; // chỉ gọi 1 lần
            if (shapes == null || shapes.Count == 0) shapes = BlockShapeLibrary.CreateDefaults();

            _pieces = new BlockPiece[slots.Length];
            _pieceShape = new int[slots.Length];
            _pickedIdx = new int[slots.Length];
            _weights = new float[shapes.Count];
            for (int i = 0; i < _pieceShape.Length; i++) _pieceShape[i] = -1;
        }

        /// <summary>Sinh bộ khối mới cho tất cả slot.</summary>
        public void SpawnSet()
        {
            PickShapes();

            for (int i = 0; i < slots.Length; i++) SpawnAt(i, _pickedIdx[i]);
            _remaining = slots.Length;
        }

        private void SpawnAt(int slot, int shapeIndex)
        {
            BlockPiece piece = Instantiate(piecePrefab, slots[slot].position, Quaternion.identity, transform);
            piece.Init(shapes[shapeIndex], grid, cam, slots[slot].position);
            _pieces[slot] = piece;
            _pieceShape[slot] = shapeIndex;
        }

        // ----------------------------------------------------------------- Random

        /// <summary>
        /// Random bộ khối. Thử tối đa fairnessTries lần để có ít nhất 1 khối còn đặt được
        /// và không ra 3 khối giống hệt nhau, tránh game over vô lý ngay từ lúc sinh khối.
        /// </summary>
        private void PickShapes()
        {
            float fill = adaptiveDifficulty ? grid.FillRatio : 0f;
            BuildWeights(fill);

            for (int attempt = 0; attempt < fairnessTries; attempt++)
            {
                bool anyFits = false;
                bool allSame = _pickedIdx.Length > 1;

                for (int i = 0; i < _pickedIdx.Length; i++)
                {
                    _pickedIdx[i] = PickWeighted();
                    if (i > 0 && _pickedIdx[i] != _pickedIdx[0]) allSame = false;
                    if (!anyFits && grid.CanPlaceAnywhere(shapes[_pickedIdx[i]])) anyFits = true;
                }

                if (anyFits && !allSame) return;
            }
            // Hết lượt thử vẫn không có khối nào vừa -> bảng thật sự bí, để GameManager xử lý Game Over
        }

        private void BuildWeights(float fill)
        {
            // k = 0 khi bảng còn thoáng, tăng dần đến 1 khi bảng gần đầy
            float k = adaptiveDifficulty ? Mathf.InverseLerp(crowdedFrom, Mathf.Max(crowdedFrom + 0.01f, crowdedMax), fill) : 0f;

            for (int i = 0; i < _weights.Length; i++)
            {
                int cells = shapes[i].CellCount;
                float w = 1f;
                if (cells <= 3) w = Mathf.Lerp(1f, smallBoost, k);
                else if (cells >= 5) w = Mathf.Lerp(1f, bigPenalty, k);
                _weights[i] = w;
            }
        }

        private int PickWeighted()
        {
            float total = 0f;
            for (int i = 0; i < _weights.Length; i++) total += _weights[i];

            float r = Random.value * total;
            for (int i = 0; i < _weights.Length; i++)
            {
                r -= _weights[i];
                if (r <= 0f) return i;
            }
            return _weights.Length - 1;
        }

        // ----------------------------------------------------------------- Lưu / khôi phục khay

        /// <summary>Ghi chỉ số shape của từng slot (-1 = đã dùng) vào mảng result.</summary>
        public void GetTrayState(int[] result)
        {
            for (int i = 0; i < result.Length && i < _pieceShape.Length; i++) result[i] = _pieceShape[i];
        }

        /// <summary>
        /// Dựng lại khay từ dữ liệu đã lưu. Trả về false nếu dữ liệu không hợp lệ
        /// (khác số slot, chỉ số shape sai, hoặc khay trống), khi đó khay không bị đổi.
        /// </summary>
        public bool RestoreTray(int[] state)
        {
            if (state == null || state.Length != slots.Length) return false;

            bool any = false;
            for (int i = 0; i < state.Length; i++)
            {
                if (state[i] >= shapes.Count) return false;
                if (state[i] >= 0) any = true;
            }
            if (!any) return false;

            ClearAll();
            for (int i = 0; i < state.Length; i++)
            {
                if (state[i] < 0) continue;
                SpawnAt(i, state[i]);
                _remaining++;
            }
            return true;
        }

        // ----------------------------------------------------------------- Gỡ khối

        /// <summary>Gỡ khối đã đặt vào bảng.</summary>
        public void Release(BlockPiece piece)
        {
            int index = Array.IndexOf(_pieces, piece);
            if (index < 0) return;

            _pieces[index] = null;
            _pieceShape[index] = -1;
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
                _pieceShape[i] = -1;
            }
            _remaining = 0;
        }
    }
}
