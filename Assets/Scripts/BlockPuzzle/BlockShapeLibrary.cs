using System.Collections.Generic;
using UnityEngine;

namespace BlockPuzzle
{
    /// <summary>
    /// Bộ shape mặc định tạo bằng code, dùng khi chưa tạo asset BlockShapeData nào.
    /// </summary>
    public static class BlockShapeLibrary
    {
        // Sprite ô gạch vẽ gần trắng để tô màu, nên đây là màu vàng gỗ của ảnh mẫu
        private static readonly Color Gold = new Color(0.97f, 0.79f, 0.54f);

        public static List<BlockShapeData> CreateDefaults()
        {
            var list = new List<BlockShapeData>();

            // Hình chữ nhật (w x h): 1x1, I-shape, 2x2, 3x3...
            int[,] rects =
            {
                { 1, 1 }, { 1, 2 }, { 2, 1 }, { 1, 3 }, { 3, 1 }, { 1, 4 }, { 4, 1 },
                { 2, 2 }, { 2, 3 }, { 3, 2 }, { 3, 3 }
            };
            for (int i = 0; i < rects.GetLength(0); i++)
                list.Add(BlockShapeData.Create(Rect(rects[i, 0], rects[i, 1]), Gold));

            // L nhỏ (3 ô) - 4 hướng
            Add(list, "##", "#.");
            Add(list, "##", ".#");
            Add(list, "#.", "##");
            Add(list, ".#", "##");

            // L cao (4 ô) - 4 hướng
            Add(list, "#.", "#.", "##");
            Add(list, ".#", ".#", "##");
            Add(list, "##", "#.", "#.");
            Add(list, "##", ".#", ".#");

            // L lớn (5 ô) - 4 hướng
            Add(list, "#..", "#..", "###");
            Add(list, "..#", "..#", "###");
            Add(list, "###", "#..", "#..");
            Add(list, "###", "..#", "..#");

            // Z / S
            Add(list, "##.", ".##");
            Add(list, ".##", "##.");

            return list;
        }

        private static void Add(List<BlockShapeData> list, params string[] rows)
        {
            list.Add(BlockShapeData.Create(rows, Gold));
        }

        private static string[] Rect(int w, int h)
        {
            var rows = new string[h];
            for (int i = 0; i < h; i++) rows[i] = new string('#', w);
            return rows;
        }
    }
}
