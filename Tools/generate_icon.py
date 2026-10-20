#!/usr/bin/env python3
"""
Vẽ icon app (không chữ): bảng gỗ 3x3 có vài khối gỗ xếp thành chữ L và một khối xanh ngọc, trên nền ván gỗ.

Chạy:  python3 Tools/generate_art.py   (cần có sprite trước)
       python3 Tools/generate_icon.py
Kết quả trong Assets/Art/Icon:
  icon_1024.png               icon vuông đầy đủ (Play Store, icon cũ)
  icon_adaptive_background.png  lớp nền Android adaptive icon (1024x1024, kín)
  icon_adaptive_foreground.png  lớp trước, nền trong suốt, nội dung nằm trong vùng an toàn (đường kính ~62%)
Và Tools/icon_preview.png xem thử trên mặt nạ tròn / bo góc.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "..")
ART = os.path.join(ROOT, "Assets", "Art", "Generated")
OUT = os.path.join(ROOT, "Assets", "Art", "Icon")
sys.path.insert(0, HERE)
import generate_art as ga  # noqa: E402  (dùng lại hàm vẽ gỗ, nine-slice)

S = 1024


def load(name):
    return Image.open(os.path.join(ART, name + ".png")).convert("RGBA")


def tint(img, rgb):
    a = np.asarray(img, float).copy()
    a[..., 0] *= rgb[0]
    a[..., 1] *= rgb[1]
    a[..., 2] *= rgb[2]
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")


def background():
    """Ván gỗ ấm, sáng ở góc trên-trái, tối dần ra rìa."""
    arr = ga.wood_arr(S, S, (222, 144, 72), (176, 100, 46), 0.12)
    yy, xx = np.mgrid[0:S, 0:S]
    d = np.sqrt(((xx - S * 0.38) / (S * 0.75)) ** 2 + ((yy - S * 0.30) / (S * 0.75)) ** 2)
    arr *= (1.12 - 0.42 * np.clip(d, 0, 1.2))[..., None]
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB").convert("RGBA")


def mark(side):
    """Bảng 3x3 + khối gỗ, trả về ảnh RGBA vuông cạnh `side` (nền trong suốt, có đổ bóng)."""
    frame_src = load("board_frame")
    frame = ga.nine(frame_src, (64, 64, 64, 64), (side, side))
    pad = int(side * 0.115)                # độ dày khung + lề trong
    cell_area = side - 2 * pad
    cell = cell_area // 3
    gap = max(2, cell // 22)

    empty = load("cell_empty")
    wood = tint(load("cell_block"), (0.97, 0.79, 0.54))
    jade = tint(load("cell_block"), (0.50, 0.86, 0.66))

    # 1 = gỗ, 2 = ngọc, 0 = trống (hàng 0 ở trên)
    layout = [[1, 0, 2],
              [1, 0, 0],
              [1, 1, 1]]

    board = frame.copy()
    ox = (side - 3 * cell) // 2
    oy = ox
    shadow = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    for r in range(3):
        for c in range(3):
            x = ox + c * cell
            y = oy + r * cell
            size = cell - gap
            if layout[r][c] == 0:
                board.alpha_composite(empty.resize((size, size), Image.LANCZOS), (x, y))
            else:
                sd = Image.new("RGBA", (size, size), (0, 0, 0, 140))
                shadow.alpha_composite(sd, (x + int(cell * 0.04), y + int(cell * 0.08)))
    shadow = shadow.filter(ImageFilter.GaussianBlur(cell * 0.07))
    board = Image.alpha_composite(board, shadow)
    for r in range(3):
        for c in range(3):
            v = layout[r][c]
            if v == 0:
                continue
            size = cell - gap
            spr = (wood if v == 1 else jade).resize((size, size), Image.LANCZOS)
            board.alpha_composite(spr, (ox + c * cell, oy + r * cell))

    # đổ bóng cả bảng xuống nền
    out = Image.new("RGBA", (side + 80, side + 80), (0, 0, 0, 0))
    sh = Image.new("RGBA", out.size, (0, 0, 0, 0))
    mask = frame.split()[3]
    blk = Image.new("RGBA", frame.size, (30, 12, 2, 150))
    blk.putalpha(mask.point(lambda v: int(v * 0.6)))
    sh.alpha_composite(blk, (40, 56))
    sh = sh.filter(ImageFilter.GaussianBlur(side * 0.025))
    out = Image.alpha_composite(out, sh)
    out.alpha_composite(board, (40, 40))
    return out


def centered(layer, canvas_size, target_side):
    """Đặt layer vào giữa canvas trong suốt, co sao cho cạnh bảng ≈ target_side."""
    scale = target_side / (layer.width - 80)
    lw = int(layer.width * scale)
    lh = int(layer.height * scale)
    resized = layer.resize((lw, lh), Image.LANCZOS)
    canvas = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    canvas.alpha_composite(resized, ((canvas_size - lw) // 2, (canvas_size - lh) // 2))
    return canvas


def main():
    os.makedirs(OUT, exist_ok=True)
    big = mark(900)                                   # vẽ lớn rồi co lại cho mịn

    bg = background()
    # Lớp trước: cạnh bảng ~ 460px, vừa trong vòng tròn an toàn (đường kính ~ 626px) dù cạnh chéo ~ 608
    fg = centered(big, S, 460)
    # Icon đầy đủ: bảng to hơn vì không bị mặt nạ cắt (Play Store tự bo góc)
    full = Image.alpha_composite(bg, centered(big, S, 680))

    bg.convert("RGB").save(os.path.join(OUT, "icon_adaptive_background.png"))
    fg.save(os.path.join(OUT, "icon_adaptive_foreground.png"))
    full.convert("RGB").save(os.path.join(OUT, "icon_1024.png"))

    # Xem thử: icon đầy đủ bo góc, icon adaptive trên mặt nạ tròn và vuông bo
    adaptive = Image.alpha_composite(bg, fg)
    def masked(img, shape):
        m = Image.new("L", (S, S), 0)
        d = ImageDraw.Draw(m)
        if shape == "circle":
            d.ellipse([0, 0, S - 1, S - 1], fill=255)
        else:
            d.rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.22), fill=255)
        out = img.copy()
        out.putalpha(m)
        return out
    sheet = Image.new("RGBA", (3 * 340 + 80, 380), (245, 245, 245, 255))
    for i, (img, shape) in enumerate([(full, "squircle"), (adaptive, "circle"), (adaptive, "squircle")]):
        t = masked(img, shape).resize((340, 340), Image.LANCZOS)
        sheet.alpha_composite(t, (20 + i * 360, 20))
    sheet.convert("RGB").save(os.path.join(HERE, "icon_preview.png"))
    print("OK ->", OUT)


if __name__ == "__main__":
    main()
