using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BlockPuzzle
{
    /// <summary>
    /// Đổi chất liệu của TẤT CẢ khối khi đạt mốc điểm: gỗ (luôn đầu tiên) rồi lần lượt cỏ, đá quý,
    /// dưa hấu, vải len theo thứ tự ngẫu nhiên (xáo trộn theo seed của ván, mỗi vòng xáo lại và
    /// không bao giờ ra cùng một chất liệu hai mốc liền nhau).
    /// Seed được lưu cùng ván chơi nên thoát app vào lại vẫn đúng chất liệu đang chơi.
    /// </summary>
    public class BlockThemeManager : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [SerializeField] private BlockSpawner spawner;
        [SerializeField] private ClearEffects effects;

        [Tooltip("Phần tử 0 = gỗ (mặc định lúc bắt đầu). Các phần tử còn lại là chất liệu đặc biệt, ra theo thứ tự ngẫu nhiên.")]
        [SerializeField] private BlockSkin[] skins;
        [Tooltip("Cứ mỗi chừng này điểm thì đổi chất liệu một lần.")]
        [SerializeField, Min(50)] private int pointsPerStage = 600;

        // Mảng dùng lại, không cấp phát mỗi lần tính
        private int[] _perm;
        private int[] _prevPerm;

        /// <summary>Seed của ván hiện tại (lưu cùng ván).</summary>
        public int Seed { get; private set; }

        /// <summary>Chất liệu đang dùng (null nếu chưa cấu hình).</summary>
        public BlockSkin Current => (skins != null && _skinIndex >= 0 && _skinIndex < skins.Length) ? skins[_skinIndex] : null;

        private int _skinIndex;

        // ----------------------------------------------------------------- Vòng đời ván

        /// <summary>Ván mới: seed mới, quay về gỗ. Gọi trước khi dọn bảng và sinh khối.</summary>
        public void NewGame()
        {
            Seed = Random.Range(1, int.MaxValue);
            _skinIndex = 0;
            if (grid != null) grid.SetSkin(Current, false, Vector3.zero);
        }

        /// <summary>Khôi phục ván đã lưu: chất liệu suy ra từ seed và điểm, không chạy animation.</summary>
        public void Restore(int seed, int score)
        {
            Seed = seed != 0 ? seed : Random.Range(1, int.MaxValue); // bản lưu cũ chưa có seed
            _skinIndex = SkinForStage(StageOf(score));
            if (grid != null) grid.SetSkin(Current, false, Vector3.zero);
        }

        /// <summary>
        /// Gọi sau khi điểm thay đổi. Nếu vừa chạm mốc mới (và chất liệu khác chất liệu cũ) thì đổi hết khối
        /// trên bảng và trong khay, gợn sóng lan từ <paramref name="rippleFrom"/>. Trả về true nếu có đổi.
        /// </summary>
        public bool OnScoreChanged(int score, Vector3 rippleFrom)
        {
            if (skins == null || skins.Length < 2) return false;

            int next = SkinForStage(StageOf(score));
            if (next == _skinIndex) return false;

            _skinIndex = next;
            BlockSkin skin = Current;
            if (grid != null) grid.SetSkin(skin, true, rippleFrom);
            if (spawner != null) spawner.RefreshSkin(grid != null ? grid.SkinSwapDelay : 0.3f);
            if (effects != null) effects.PlayThemeChange(skin != null ? skin.displayName + "!" : "", skin != null ? skin.fxColor : Color.white);
            return true;
        }

        // ----------------------------------------------------------------- Chọn chất liệu theo mốc

        private int StageOf(int score) => Mathf.Max(0, score) / pointsPerStage;

        /// <summary>Mốc 0 là gỗ. Mốc n >= 1: lấy chất liệu thứ (n-1) trong dãy các vòng xáo trộn.</summary>
        private int SkinForStage(int stage)
        {
            int special = skins != null ? skins.Length - 1 : 0;
            if (stage <= 0 || special <= 0) return 0;

            int idx = stage - 1;
            int round = idx / special;
            int pos = idx % special;

            EnsureBuffers(special);
            BuildPermutation(round, special);
            return 1 + _perm[pos];
        }

        private void EnsureBuffers(int n)
        {
            if (_perm == null || _perm.Length != n)
            {
                _perm = new int[n];
                _prevPerm = new int[n];
            }
        }

        /// <summary>
        /// Xáo trộn deterministic theo (seed, vòng). Vòng sau được đổi chỗ nếu chất liệu đầu trùng chất liệu cuối của vòng trước.
        /// Tính lần lượt từ vòng 0 nên kết quả luôn giống nhau với cùng seed.
        /// </summary>
        private void BuildPermutation(int round, int n)
        {
            for (int r = 0; r <= round; r++)
            {
                for (int i = 0; i < n; i++) _perm[i] = i;

                var rng = new System.Random(unchecked(Seed * 31 + r * 7919));
                for (int i = n - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    int tmp = _perm[i]; _perm[i] = _perm[j]; _perm[j] = tmp;
                }

                if (r > 0 && n > 1 && _perm[0] == _prevPerm[n - 1])
                {
                    int tmp = _perm[0]; _perm[0] = _perm[1]; _perm[1] = tmp;
                }

                Array.Copy(_perm, _prevPerm, n);
            }
        }
    }
}
