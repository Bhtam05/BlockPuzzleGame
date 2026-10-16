# 🧩 Block Puzzle Game (Unity)

Một tựa game giải đố khối màu 2D (Block Puzzle) được phát triển bằng **Unity**, tối ưu hóa cho màn hình dọc (Portrait) trên thiết bị di động. Dự án kết hợp lối chơi giải đố cổ điển với hệ thống tính điểm, chuỗi combo mượt mà và giao diện phong cách gỗ ấm cúng được tự động hóa bằng Editor Script.

---

## ✨ Tính năng chính

1. **Lưới trò chơi & Cơ chế đặt khối (8x8 Grid & Placer):**
   - Bảng lưới chuẩn 8x8 tối ưu cho thao tác kéo thả (Drag & Drop) cảm ứng.
   - Kiểm tra va chạm và tính toán khớp vị trí (Snap) chính xác từng ô.

2. **Khay chứa chờ (3-Piece Spawner):**
   - Sinh ngẫu nhiên bộ 3 khối gạch đa dạng hình dáng ở mỗi lượt chơi.
   - Tự động làm mới bộ 3 khối mới ngay khi người chơi sử dụng hết.

3. **Hệ thống Điểm số & Combo (Score & Combo Manager):**
   - Tính điểm dựa trên số ô đặt xuống và số hàng/cột được xóa đồng thời (xóa nhiều thưởng lớn theo cấp số nhân).
   - Hệ thống **Combo Streak** tích lũy liên tiếp qua các lượt xóa bảng, gia tăng điểm thưởng kết hợp hiệu ứng chạy số mượt mà sử dụng **DOTween**.
   - Lưu trữ điểm cao cá nhân (`High Score`) bằng `PlayerPrefs`.

4. **Giao diện Gỗ ấm & Tự động hóa qua Editor Script:**
   - Giao diện thiết kế theo phong cách tối giản, sang trọng với tông màu gỗ ấm cúng.
   - Tích hợp **Custom Editor Tool** (`Tools > Block Puzzle > Build UI (All)`) giúp tự động dựng toàn bộ khung nền, HUD (điểm số, vương miện điểm cao, thanh tiến độ, hệ số combo, nút cài đặt) và các popup vào Scene chỉ với 1 cú click chuột.
   - Xử lý sẵn các popup trạng thái **Game Over** và **Pause**, liên kết sự kiện nút bấm mượt mà.

5. **Xử lý tình huống ngoại lệ & An toàn:**
   - Tự động kiểm tra điều kiện thua (`Game Over Check`) khi không còn khối nào trong khay có thể đặt vừa lên bảng.
   - Vá lỗi an toàn: Tự động đưa khối gạch về khay an toàn nếu người chơi kích hoạt trạng thái Pause khi đang thao tác kéo thả.

---

## 📂 Cấu trúc Dự án

```text
Assets/
│
├── Art/                  # Tài nguyên đồ họa, Sprite phong cách gỗ
├── Scenes/               # Các màn chơi (SampleScene)
├── Scripts/
│   └── BlockPuzzle/
│       ├── Core/         # GameManager, GridManager, BlockSpawner
│       ├── Gameplay/     # BlockPiece, BlockShapeData, CellBlock
│       ├── Managers/     # ScoreManager
│       └── UI/           # CameraFitter, UI/Popup controllers
└── TextMeshPro/          # Thư viện hiển thị văn bản chất lượng cao
```

---

## 🚀 Hướng dẫn Cài đặt & Chạy Dự án

1. **Clone Repository:**

   ```bash
   git clone https://github.com/Bhtam05/BlockPuzzleGame.git
   ```

2. **Mở dự án trong Unity:**
   - Khởi động **Unity Hub** và chọn **Add project from disk**, trỏ tới thư mục vừa clone.
   - Khuyên dùng phiên bản Unity: **6.3 LTS** (hoặc tương đương).

3. **Thiết lập giao diện tự động (nếu cần làm mới Scene):**
   - Mở `SampleScene` trong thư mục `Assets/Scenes`.
   - Trên thanh menu Unity, chọn: **Tools > Block Puzzle > Build UI (All)** để hệ thống tự động thiết lập Canvas, bố cục màn hình và các panel.

4. **Chạy thử (Play):**
   - Chuyển tab sang **Simulator** (hoặc Game View), chọn tỷ lệ màn hình dọc di động và bấm nút **Play** để trải nghiệm.

---

## 📌 Trạng thái Phát triển & Hướng phát triển tiếp theo

- **Đã hoàn thành:** Logic cốt lõi, khay 3 khối, hệ thống điểm/combo, giao diện gỗ tự động, quản lý trạng thái Game Over và tích hợp Source Control (Git).
- **Đang lên kế hoạch:**
  - Thêm hiệu ứng hạt (Particle System) khi phá hủy hàng/cột.
  - Hiệu ứng chữ điểm bay lên (`Floating Text`) sinh động.
  - Tích hợp âm thanh (SFX/BGM) cho các thao tác kéo thả và ghi điểm.
