using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Khung gỗ bao quanh bảng chơi. Sprite dùng 9-slice (Draw Mode = Sliced) nên tự co giãn
    /// theo kích thước lưới trong GridManager, đổi lưới 8x8 sang 9x9 không cần vẽ lại.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoardFrame : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [Tooltip("Khoảng đệm từ mép ngoài các ô đến mép ngoài khung (đơn vị World).")]
        [SerializeField] private float padding = 0.42f;

        private void Start()
        {
            var sr = GetComponent<SpriteRenderer>();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(grid.Width * grid.CellSize + padding * 2f,
                                  grid.Height * grid.CellSize + padding * 2f);
            transform.position = grid.BoardCenter;
        }
    }
}
