# Block Puzzle (Unity 6, 2D mobile)

Game xếp khối kiểu Block Puzzle cho điện thoại (màn dọc, mục tiêu Android): kéo 3 khối vào bảng 8x8, lấp đầy hàng hoặc cột để xóa và ghi điểm, hết chỗ đặt thì thua. Giao diện theme gỗ ấm. Toàn bộ sprite và âm thanh được sinh bằng code (Python), nên có thể thay bằng art và âm thanh thật bất cứ lúc nào mà không đụng tới code game.

> **Tình trạng:** toàn bộ code được viết mà chưa chạy trong Unity ở phía người viết (chỉ kiểm tra cú pháp và mô phỏng logic combo bằng Python). Phần lõi game, giao diện và công cụ dựng UI đã được chạy thử; hiệu ứng, âm thanh, lưu ván, cài đặt, đồ họa mới và bản build Android **chưa được xác nhận trên máy thật**. Lần đầu mở project hãy xem Console, có thể còn lỗi nhỏ.

## Tính năng

| Nhóm | Nội dung |
| --- | --- |
| Lối chơi | Bảng 8x8, kéo thả 3 khối, bóng mờ báo vị trí rơi, xóa hàng/cột (nhiều đường cùng lúc), Game Over, chơi lại |
| Điểm | Điểm đặt khối, điểm xóa đường theo cấp số nhân, combo, điểm cao lưu trên máy |
| Độ khó | Bảng càng đầy càng ra nhiều khối nhỏ; bộ 3 khối không bao giờ giống nhau và luôn có ít nhất một khối đặt được |
| Giao diện | Nền ván gỗ, khung bảng chạm khắc, ô gạch gỗ phong có vân, HUD (điểm cao, điểm, combo), popup Tạm dừng / Game Over, mascot, tiếng Việt có dấu |
| Hiệu ứng | Chớp sáng khi đặt khối, mảnh gỗ vỡ, vệt sáng quét, chữ điểm bay lên, chữ COMBO, rung màn hình, hạt bụi sáng ở nền |
| Âm thanh | Tiếng nhấc/đặt khối, xóa đường, combo, bấm nút, Game Over, nhạc nền (sinh bằng code) |
| Cài đặt | Công tắc Âm thanh, Nhạc nền, Rung, Rung màn hình (lưu PlayerPrefs) |
| Di động | Camera tự co theo tỉ lệ màn hình, Safe Area (tai thỏ), 60 FPS, chặn tắt màn hình, tự Pause khi ra nền, rung Android, nút Back |
| Lưu ván | Thoát app rồi vào lại vẫn chơi tiếp ván cũ (bảng, khay, điểm, combo) |

## Yêu cầu

- Unity 6 (đang dùng 6000.3), template 2D (URP 2D).
- **TextMeshPro**: Window > TextMeshPro > Import TMP Essential Resources.
- **DOTween** (import xong chạy Tools > Demigiant > DOTween Utility Panel > Setup). Chỉ dùng phần core.
- Scene phải có sẵn **EventSystem** (tool dựng UI không tạo vì không biết project dùng Input System nào).
- Python 3 (không bắt buộc): `numpy` + `Pillow` để sinh lại sprite, `numpy` để sinh lại âm thanh.

## Cài nhanh

1. Copy thư mục `Assets/` của repo vào project Unity (ghi đè file trùng tên). Nếu trước đó có script `CameraFitter.cs`, `HudView.cs` hoặc file thanh tiến độ tự tạo thì xóa đi.
2. Mở scene có sẵn các object `GridRoot` (GridManager), `Spawner` (BlockSpawner), `ScoreManager`, `GameManager`.
3. Chạy **Tools > Block Puzzle > Build UI (All)**. Tool sẽ tự:
   - cấu hình import cho sprite (PPU, viền 9-slice) theo `manifest.json`,
   - tạo/gán prefab ô gạch và khối kéo,
   - dựng camera, nền gỗ, hạt bụi nền, khung bảng, 3 ô khay,
   - tạo TMP Font Asset tiếng Việt từ `Assets/Fonts/DejaVuSans-Bold.ttf`,
   - dựng HUD, popup Game Over, popup Tạm dừng (kèm 4 công tắc cài đặt), hiệu ứng xóa đường và rung camera,
   - gán sẵn mọi tham chiếu cho `GridManager`, `BlockSpawner`, `ScoreManager`, `GameManager`.
4. Ctrl+S lưu scene rồi bấm Play. Chạy lại tool bao nhiêu lần cũng được (Ctrl+Z để hoàn tác). Mỗi lần đổi sprite hoặc cập nhật code UI đều nên chạy lại.

## Cách chơi và tính điểm

- Mỗi lượt kéo một trong 3 khối vào bảng. Dùng hết 3 khối thì có bộ mới. Không khối nào còn đặt vừa thì Game Over.
- **Đặt khối:** 1 điểm mỗi ô (không nhân combo).
- **Xóa n đường** (hàng + cột) cùng lúc: `10 * n * n` điểm, nhân hệ số combo. Ô giao nhau giữa hàng và cột chỉ bị xóa một lần.
- **Combo:** chuỗi tăng 1 sau mỗi lượt có xóa. Lượt không xóa được **chưa mất chuỗi**, chỉ reset khi có 3 lượt đặt liên tiếp không xóa.
- **Hệ số** = `min(3.0, 1 + chuỗi * 0.1)`, áp trước khi tăng chuỗi nên lần xóa đầu là x1.0. Số trên HUD là hệ số sẽ áp cho lần xóa kế tiếp.
- Chữ `COMBO xN!` hiện từ chuỗi 2. Điểm cao lưu bằng PlayerPrefs, khóa `BP_HighScore`.
- Các số (điểm/ô, điểm/đường, bước, trần, số lượt chờ) chỉnh trong Inspector của `ScoreManager`.

## Cấu trúc

```
Assets/
  Art/Generated/      sprite PNG + manifest.json (PPU, viền 9-slice)
  Fonts/              DejaVuSans-Bold.ttf (đủ dấu tiếng Việt)
  Resources/Audio/    sfx_*.wav + bgm_main.wav
  Editor/             BlockPuzzleUIBuilder.cs, BlockPuzzleAndroidSetup.cs (menu Tools > Block Puzzle)
  Scripts/BlockPuzzle/
    GridManager, BlockPiece, BlockSpawner, ScoreManager, GameManager, AppSettings
    AudioManager, Haptics, GameSettings, GameSave
    BlockShapeData, BlockShapeLibrary, ClearInfo
    UI/               CameraFitter, BackgroundFitter, BoardFrame, SafeArea, PopupAnimator,
                      ClearEffects, ScreenShake, AmbientDust, SettingToggle
Tools/
  generate_art.py     vẽ lại toàn bộ sprite (Pillow + numpy)
  generate_audio.py   tổng hợp lại âm thanh và nhạc nền (numpy)
  art_preview.png     ảnh xem thử bố cục
```

| Script | Vai trò |
| --- | --- |
| `GridManager` | Mảng `int[,]`, kiểm tra đặt khối, xóa hàng/cột, preview, đổi tọa độ Grid/World, xuất/nhập bảng để lưu ván |
| `BlockPiece` | Kéo thả bằng EventSystems, nâng khối lên trên ngón tay, snap, tween trả về khay, hủy kéo khi Pause |
| `BlockSpawner` | Sinh 3 khối, độ khó thích ứng, kiểm tra công bằng, dựng lại khay từ bản lưu |
| `ScoreManager` | Điểm, combo, điểm cao |
| `GameManager` | Điều phối, trạng thái Playing/Paused/GameOver, Pause/Resume/Restart, lưu/khôi phục ván, nút Back |
| `AudioManager`, `Haptics`, `GameSettings` | Âm thanh, rung Android, công tắc cài đặt |
| `GameSave` | Lưu ván đang chơi (JSON trong PlayerPrefs) |
| `ClearEffects`, `ScreenShake`, `AmbientDust` | Hạt (pool dựng sẵn), chữ điểm, rung camera, bụi nền |
| `BlockPuzzleUIBuilder` | Dựng toàn bộ scene bằng một menu |

Quy ước: ô (0,0) ở góc dưới-trái; sprite 128 PPU = 1 unit; màu ô lấy từ `SpriteRenderer.color` (sprite `cell_block` vẽ gần trắng, `BlockShapeLibrary` tô màu gỗ).

## Luồng một lượt đặt khối

`BlockPiece` (thả hợp lệ) -> event `Placed` -> `GameManager.HandlePiecePlaced` -> `GridManager.PlaceShape` + `ClearFullLines` -> `ScoreManager.RegisterTurn` -> âm thanh/rung/`ClearEffects` -> `BlockSpawner.Release/SpawnSet` -> `CheckGameOver` -> `GameSave.Write`.

## Tùy chỉnh

- **Đổi hình sprite:** sửa `Tools/generate_art.py` rồi `python3 Tools/generate_art.py`, hoặc ghi đè PNG cùng tên trong `Assets/Art/Generated`, sau đó chạy lại Build UI.
- **Đổi màu khối:** thuộc tính màu trong `BlockShapeLibrary`.
- **Đổi âm thanh:** ghi đè file `.wav` cùng tên trong `Resources/Audio` (hoặc `.ogg`/`.mp3` cùng tên, xóa wav cũ). Sinh lại bằng `python3 Tools/generate_audio.py`.
- **Đổi chữ/font:** thuộc tính `Txt...` đầu `BlockPuzzleUIBuilder.cs` (có bản tiếng Việt và tiếng Anh). Muốn đổi font: thay file `.ttf`, xóa `DejaVuSans-Bold SDF.asset`, chạy lại Build UI.
- **Bố cục khay/bảng:** hằng `GridCenterY`, `SlotY`, `SlotSpacing` trong builder.
- **Lưới khác 8x8:** đổi `Width/Height` trong `GridManager` (khung bảng tự co, chạy lại Build UI để căn). Bản lưu ván cũ sẽ tự bị bỏ.
- **Độ khó:** `BlockSpawner` có `Crowded From` (0.5), `Crowded Max` (0.85), `Small Boost`, `Big Penalty`, công tắc `Adaptive Difficulty`.
- **Hiệu ứng:** object `Effects` (số hạt, tốc độ, màu, `Popup Font Size`), `ScreenShake`, `AmbientDust` (`Count = 0` để tắt), `Place Flash` trong `GridManager`.
- **Lưu ván:** ô `Save Progress` trong `GameManager`.

## Build lên Android

1. Unity Hub > Installs > Add modules > **Android Build Support** (tick OpenJDK, Android SDK & NDK).
2. File > Build Profiles > Android > **Switch Platform**.
3. Chạy **Tools > Block Puzzle > Configure Android Build**: khóa dọc, IL2CPP + ARM64, thêm scene vào Build, báo việc còn phải làm tay.
4. Player Settings > Other Settings > Identification: đặt **Package Name** riêng (vd `com.tenban.blockpuzzle`).
5. Trên điện thoại bật **Gỡ lỗi USB** (Tùy chọn nhà phát triển), cắm cáp, chọn máy ở Run Device rồi **Build And Run**.

Quyền `VIBRATE` do Unity tự thêm khi build. Thoát app giữa chừng thì game tự Pause, lưu điểm cao và lưu ván. Nút Back: đang chơi mở Tạm dừng, đang Tạm dừng thì tiếp tục, ở Game Over thì thoát game.

## Việc chưa làm và hướng phát triển

Chưa làm: ô đặc biệt (ô số, ô ngọc; chưa định nghĩa cơ chế), quảng cáo, cửa hàng, thành tựu, bảng xếp hạng, đa ngôn ngữ, icon app và chuẩn bị phát hành (keystore, Google Play). Thanh tiến độ `0/25` của game tham khảo được chủ động bỏ để khác game gốc.

Gợi ý bước tiếp theo:
1. Build và chơi thử trên Android thật, sửa lỗi nếu có (compile, cỡ chữ điểm bay lên, độ mượt kéo thả).
2. Cân bằng độ khó và hệ số combo sau khi chơi thử.
3. Thay âm thanh bằng bản chuyên nghiệp, thêm icon app.
4. Làm ô đặc biệt sau khi chốt cơ chế.
5. Chuẩn bị phát hành.

## Giấy phép tài nguyên

Font DejaVu theo giấy phép trong `Assets/Fonts/LICENSE-DejaVu.txt`. Sprite và âm thanh do script trong `Tools/` sinh ra.
