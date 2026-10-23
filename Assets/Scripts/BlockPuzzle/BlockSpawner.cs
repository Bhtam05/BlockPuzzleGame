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
    /// Bộ 3 khối luôn giải được (mô phỏng đặt cả 3 theo một thứ tự nào đó, có tính xóa đường), và khi bảng
    /// càng đầy thì càng hay có ít nhất một khối làm xóa được đường (SpawnPlanner).
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

        [Header("Bộ khối hợp lý")]
        [Tooltip("Bật: bộ 3 khối luôn đặt hết được (theo một thứ tự nào đó) nếu tồn tại bộ như vậy. Tắt: chỉ cần có ít nhất 1 khối đặt được.")]
        [SerializeField] private bool guaranteeSolvable = true;
        [Tooltip("Xác suất bộ khối có ít nhất một khối làm xóa được đường khi bảng còn thoáng.")]
        [SerializeField, Range(0f, 1f)] private float lineHelpMin = 0.15f;
        [Tooltip("Xác suất đó khi bảng đã gần đầy (từ Crowded Max trở lên).")]
        [SerializeField, Range(0f, 1f)] private float lineHelpMax = 0.7f;

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

        // Chọn bộ khối hợp lý (tạo sẵn một lần, không cấp phát lúc chơi)
        private SpawnPlanner _planner;
        private bool[] _occupancy;
        private int[] _bestIdx;
        private int[] _fallbackIdx;

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
            _bestIdx = new int[slots.Length];
            _fallbackIdx = new int[slots.Length];
            for (int i = 0; i < _pieceShape.Length; i++) _pieceShape[i] = -1;

            _planner = new SpawnPlanner(grid.Width, grid.Height);
            _occupancy = new bool[grid.Width * grid.Height];
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
        /// Chọn bộ khối. Thử tối đa fairnessTries lần random (theo trọng số độ khó), mỗi lần kiểm tra bằng mô phỏng:
        /// 1) không ra 3 khối giống hệt nhau, 2) bộ khối phải giải được (đặt hết cả 3 theo một thứ tự nào đó),
        /// 3) đôi khi (nhiều hơn khi bảng gần đầy) bắt buộc có một khối làm xóa được đường.
        /// Không tìm được bộ giải được thì lùi về bộ có ít nhất một khối còn đặt được, cuối cùng mới chịu bộ bí
        /// (Game Over thật, GameManager xử lý).
        /// </summary>
        private void PickShapes()
        {
            float fill = grid.FillRatio;
            BuildWeights(adaptiveDifficulty ? fill : 0f);

            bool usePlanner = guaranteeSolvable && slots.Length == 3;
            bool wantLine = false;
            if (usePlanner)
            {
                grid.CopyOccupancy(_occupancy);
                _planner.Load(_occupancy);

                float help = Mathf.Lerp(lineHelpMin, lineHelpMax,
                    Mathf.InverseLerp(0.2f, Mathf.Max(0.21f, crowdedMax), fill));
                wantLine = Random.value < help;
            }

            bool haveSolvable = false;   // _bestIdx: bộ giải được đầu tiên (dùng khi không tìm được bộ có khối xóa đường)
            bool haveFallback = false;   // _fallbackIdx: bộ có ít nhất 1 khối đặt được

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

                if (allSame || !anyFits) continue;

                if (!usePlanner) return; // chế độ cũ: chỉ cần có ít nhất 1 khối đặt được

                if (!haveFallback)
                {
                    System.Array.Copy(_pickedIdx, _fallbackIdx, _pickedIdx.Length);
                    haveFallback = true;
                }

                BlockShapeData a = shapes[_pickedIdx[0]], b = shapes[_pickedIdx[1]], c = shapes[_pickedIdx[2]];
                if (!_planner.IsSolvable(a, b, c)) continue;

                if (!wantLine) return; // bộ giải được là đủ

                if (_planner.CanClearLine(a) || _planner.CanClearLine(b) || _planner.CanClearLine(c)) return;

                if (!haveSolvable)
                {
                    System.Array.Copy(_pickedIdx, _bestIdx, _pickedIdx.Length);
                    haveSolvable = true;
                }
            }

            // Hết lượt thử mà chưa chốt: dùng bộ giải được nếu có, không thì bộ còn đặt được, không thì để bộ cuối cùng
            if (haveSolvable) System.Array.Copy(_bestIdx, _pickedIdx, _pickedIdx.Length);
            else if (haveFallback) System.Array.Copy(_fallbackIdx, _pickedIdx, _pickedIdx.Length);
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

        // ----------------------------------------------------------------- Đổi chất liệu

        /// <summary>Đổi chất liệu cho các khối còn trong khay (gọi khi bảng đổi chất liệu).</summary>
        public void RefreshSkin(float baseDelay)
        {
            for (int i = 0; i < _pieces.Length; i++)
                if (_pieces[i] != null) _pieces[i].RefreshSkin(baseDelay);
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
