using System;
using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Một kiểu chất liệu của khối gạch (gỗ, cỏ, đá quý, dưa hấu, vải len...).
    /// Chỉ là dữ liệu: sprite và màu hiệu ứng. Khối trên bảng và trong khay đều lấy từ đây.
    /// </summary>
    [Serializable]
    public class BlockSkin
    {
        [Tooltip("Tên hiện trên chữ bay lên khi đổi chất liệu.")]
        public string displayName = "Gỗ";
        public Sprite sprite;
        [Tooltip("Bật: tô màu sprite theo màu của shape (dùng cho gỗ, sprite vẽ gần trắng). Tắt: giữ nguyên màu sprite.")]
        public bool tintByShape;
        [Tooltip("Màu mảnh vỡ / chữ khi xóa hàng với chất liệu này (chỉ dùng khi không tô màu theo shape).")]
        public Color fxColor = Color.white;
    }
}
