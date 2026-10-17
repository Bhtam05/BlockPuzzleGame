#!/usr/bin/env python3
"""
Vẽ toàn bộ sprite theme gỗ cho game Block Puzzle bằng code (Pillow + numpy).

Chạy:  python3 Tools/generate_art.py [thư_mục_xuất]
Mặc định xuất vào Assets/Art/Generated, kèm manifest.json (PPU + viền 9-slice)
để Editor tool trong Unity tự cấu hình import, và Tools/art_preview.png để xem nhanh.

Ô gạch được vẽ gần trắng (xám sáng) để Unity tô màu bằng SpriteRenderer.color
(vàng, xanh ngọc, ...). Mọi thứ khác vẽ sẵn màu.
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join("Assets", "Art", "Generated")
SS = 4  # supersampling cho cạnh mịn
rng = np.random.default_rng(7)
manifest = []


# ----------------------------------------------------------------------------- helpers
def rr_mask(w, h, r, inset=0):
    """Mask bo góc, vẽ ở độ phân giải gấp SS rồi thu nhỏ để khử răng cưa."""
    r = max(1, min(r, (min(w, h) - 2 * inset) // 2))
    m = Image.new("L", (w * SS, h * SS), 0)
    ImageDraw.Draw(m).rounded_rectangle(
        [inset * SS, inset * SS, (w - inset) * SS - 1, (h - inset) * SS - 1],
        radius=r * SS, fill=255)
    return m.resize((w, h), Image.LANCZOS)


def vgrad(w, h, top, bottom):
    t = np.linspace(0, 1, h)[:, None, None]
    arr = np.array(top, float) * (1 - t) + np.array(bottom, float) * t
    return np.repeat(arr, w, axis=1)


def noise_arr(w, h, sx, sy):
    """Nhiễu mượt, kéo giãn theo (sx, sy): sx > sy cho vân ngang như vân gỗ."""
    small = rng.random((max(2, h // sy), max(2, w // sx)))
    im = Image.fromarray((small * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
    return (np.asarray(im, float) / 255 - 0.5) * 2


def wood_arr(w, h, c1, c2, streak=0.09):
    base = vgrad(w, h, c1, c2)
    v = (noise_arr(w, h, 48, 3) * 0.6 + noise_arr(w, h, 200, 14) * 0.4) * streak * 255
    v += rng.normal(0, 1.4, (h, w))
    return base + v[..., None]


def fill(arr, mask):
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    img.putalpha(mask)
    return img


def solid(w, h, r, inset, arr):
    return fill(arr, rr_mask(w, h, r, inset))


def over(a, b):
    return Image.alpha_composite(a, b)


def gloss(img, mask, strength=70, frac=0.55):
    w, h = img.size
    t = np.linspace(0, 1, h)[:, None]
    a = np.clip(1 - t / frac, 0, 1) ** 1.6 * strength
    a = np.repeat(a, w, axis=1) * (np.asarray(mask, float) / 255.0)
    layer = np.zeros((h, w, 4), np.uint8)
    layer[..., :3] = 255
    layer[..., 3] = a.astype(np.uint8)
    return over(img, Image.fromarray(layer, "RGBA"))


def plate(w, h, r, rim, face, rim_w, outline=None, face_arr=None, glossy=True, noise=0):
    """Tấm bo góc: viền ngoài (outline) + gờ (rim) + mặt (face) + bóng nhẹ."""
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ol = 0
    if outline:
        ol = 3
        img = over(img, solid(w, h, r, 0, vgrad(w, h, outline, outline)))
    img = over(img, solid(w, h, r - ol, ol, vgrad(w, h, rim[0], rim[1])))
    fa = face_arr if face_arr is not None else vgrad(w, h, face[0], face[1])
    if noise:
        fa = fa + (noise_arr(w, h, 24, 24) * noise)[..., None]
    fi = ol + rim_w
    fmask = rr_mask(w, h, max(4, r - fi), fi)
    img = over(img, fill(fa, fmask))
    if glossy:
        img = gloss(img, fmask)
    return img


def button(w, h, r, rim, face, outline, lip_col, lip=10):
    """Nút có đáy 3D: một tấm tối hơn nằm dưới tấm chính."""
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    lip_img = plate(w, h - lip + 4, r, (lip_col, lip_col), (lip_col, lip_col), 4, outline, glossy=False)
    canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    canvas.paste(lip_img, (0, lip - 4))
    top = plate(w, h - lip, r, rim, face, 7, outline)
    canvas.alpha_composite(top, (0, 0))
    return over(img, canvas)


def save(name, img, ppu=100, border=(0, 0, 0, 0)):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name + ".png"))
    manifest.append({"name": name, "ppu": ppu,
                     "border": list(border)})  # Unity: left, bottom, right, top


def nine(img, border, size):
    """Co giãn 9-slice (chỉ dùng cho preview)."""
    l, b, r, t = border
    w, h = img.size
    W, H = size
    out = Image.new("RGBA", size, (0, 0, 0, 0))
    xs = [(0, l, 0, l), (l, w - r, l, W - r), (w - r, w, W - r, W)]
    ys = [(0, t, 0, t), (t, h - b, t, H - b), (h - b, h, H - b, H)]
    for sy0, sy1, dy0, dy1 in ys:
        for sx0, sx1, dx0, dx1 in xs:
            if sx1 <= sx0 or sy1 <= sy0 or dx1 <= dx0 or dy1 <= dy0:
                continue
            piece = img.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.LANCZOS)
            out.alpha_composite(piece, (dx0, dy0))
    return out


# ----------------------------------------------------------------------------- gạch
def make_cell():
    W = 128
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    img = over(img, solid(W, W, 24, 3, vgrad(W, W, (205, 205, 205), (140, 140, 140))))   # viền tối
    face_mask = rr_mask(W, W, 18, 9)
    face = vgrad(W, W, (255, 255, 255), (226, 226, 226)) + \
        (noise_arr(W, W, 22, 22) * 7)[..., None]                                          # vân đá nhẹ
    img = over(img, fill(face, face_mask))
    # vệt sáng chéo như mặt gạch bóng
    sh = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ImageDraw.Draw(sh).polygon([(20, 18), (70, 18), (18, 70)], fill=(255, 255, 255, 80))
    sh = sh.filter(ImageFilter.GaussianBlur(6))
    sh.putalpha(Image.fromarray((np.asarray(sh.split()[3], float) *
                                 np.asarray(face_mask, float) / 255).astype(np.uint8)))
    img = over(img, sh)
    # gờ sáng dưới đáy mặt gạch
    edge = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ImageDraw.Draw(edge).rounded_rectangle([9, 9, W - 10, W - 10], radius=18, outline=(255, 255, 255, 90), width=2)
    img = over(img, edge)
    save("cell_block", img, ppu=128)


def make_empty_cell():
    W = 128
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    img = over(img, solid(W, W, 14, 3, vgrad(W, W, (38, 22, 13), (78, 48, 30))))
    inner = vgrad(W, W, (56, 33, 20), (66, 40, 25)) + (noise_arr(W, W, 30, 30) * 3)[..., None]
    img = over(img, fill(inner, rr_mask(W, W, 11, 8)))
    save("cell_empty", img, ppu=128)


# ----------------------------------------------------------------------------- bảng
def make_board_frame():
    S = 256
    outer = wood_arr(S, S, (222, 142, 66), (178, 104, 46))
    img = plate(S, S, 52, ((236, 162, 80), (170, 98, 42)), None, 8, outline=(92, 50, 22),
                face_arr=outer, glossy=False)
    # khoảng lõm chứa các ô: gờ sáng rồi nền nâu tối
    img = over(img, solid(S, S, 24, 36, vgrad(S, S, (110, 62, 28), (240, 170, 90))))
    img = over(img, solid(S, S, 22, 40, vgrad(S, S, (44, 26, 15), (60, 36, 22))))
    save("board_frame", img, ppu=128, border=(64, 64, 64, 64))


# ----------------------------------------------------------------------------- HUD
def make_hud():
    # Khung điểm cao: bảng xanh lá viền vàng nâu
    green = vgrad(256, 128, (46, 132, 74), (30, 98, 54)) + (noise_arr(256, 128, 6, 6) * 5)[..., None]
    p = plate(256, 128, 34, ((228, 160, 60), (160, 96, 28)), None, 9, outline=(86, 48, 14),
              face_arr=green, glossy=False)
    p = over(p, gloss(Image.new("RGBA", (256, 128), (0, 0, 0, 0)), rr_mask(256, 128, 25, 12), 38, 0.5))
    save("hud_panel_best", p, border=(44, 44, 44, 44))

    # Khung điểm hiện tại: gỗ tối, viền sáng phát sáng nhẹ
    wd = wood_arr(256, 128, (112, 66, 36), (78, 44, 24), 0.10)
    q = plate(256, 128, 34, ((255, 190, 214), (212, 140, 190)), None, 7, outline=(255, 214, 236),
              face_arr=wd, glossy=False)
    glow = Image.new("RGBA", (256, 128), (0, 0, 0, 0))
    ImageDraw.Draw(glow).rounded_rectangle([2, 2, 253, 125], radius=34, outline=(255, 170, 220, 255), width=6)
    glow = glow.filter(ImageFilter.GaussianBlur(5))
    q = over(glow, q)
    save("hud_panel_score", q, border=(44, 44, 44, 44))

    # Nút tròn vuông chứa icon cài đặt
    btn = plate(128, 128, 30, ((240, 176, 96), (170, 98, 44)), ((226, 150, 70), (190, 110, 48)), 8,
                outline=(88, 46, 18))
    save("btn_square", btn, border=(36, 36, 36, 36))

    # Icon bánh răng
    S = 96 * SS
    g = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(g)
    c = S / 2
    col = (110, 56, 20, 255)
    d.ellipse([c - 30 * SS, c - 30 * SS, c + 30 * SS, c + 30 * SS], fill=col)
    for i in range(8):
        a = math.radians(i * 45)
        ca, sa = math.cos(a), math.sin(a)
        pts = []
        for dx, dy in [(-9, 26), (9, 26), (8, 42), (-8, 42)]:
            pts.append((c + (dx * ca - dy * sa) * SS, c + (dx * sa + dy * ca) * SS))
        d.polygon(pts, fill=col)
    d.ellipse([c - 14 * SS, c - 14 * SS, c + 14 * SS, c + 14 * SS], fill=(0, 0, 0, 0))
    save("icon_gear", g.resize((96, 96), Image.LANCZOS))

    # Icon vương miện
    S = 4
    cw = Image.new("RGBA", (96 * S, 64 * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(cw)
    pts = [(4, 56), (10, 14), (30, 36), (48, 4), (66, 36), (86, 14), (92, 56)]
    d.polygon([(x * S, y * S) for x, y in pts], fill=(120, 52, 36, 255))
    d.rounded_rectangle([4 * S, 50 * S, 92 * S, 62 * S], radius=5 * S, fill=(120, 52, 36, 255))
    for x, y in [(10, 14), (48, 4), (86, 14)]:
        d.ellipse([(x - 5) * S, (y - 5) * S, (x + 5) * S, (y + 5) * S], fill=(120, 52, 36, 255))
    save("icon_crown", cw.resize((96, 64), Image.LANCZOS))


# ----------------------------------------------------------------------------- popup
def make_popup():
    cream = vgrad(512, 512, (252, 232, 190), (236, 204, 150)) + (noise_arr(512, 512, 18, 18) * 5)[..., None]
    pop = plate(512, 512, 72, ((196, 120, 52), (140, 78, 30)), None, 18, outline=(78, 42, 16),
                face_arr=cream, glossy=False)
    inner = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    ImageDraw.Draw(inner).rounded_rectangle([24, 24, 487, 487], radius=54, outline=(170, 110, 50, 120), width=3)
    pop = over(pop, inner)
    save("popup_panel", pop, border=(96, 96, 96, 96))

    rib = plate(512, 120, 40, ((246, 120, 90), (190, 56, 44)), ((236, 86, 66), (196, 52, 40)), 7,
                outline=(110, 24, 18))
    save("title_ribbon", rib, border=(56, 56, 56, 56))

    save("btn_gold", button(320, 120, 40, ((255, 232, 130), (226, 150, 24)),
                            ((255, 214, 84), (242, 164, 28)), (122, 64, 10), (168, 98, 12)),
         border=(48, 52, 48, 48))
    save("btn_green", button(320, 120, 40, ((170, 236, 130), (60, 150, 52)),
                             ((120, 214, 90), (52, 160, 60)), (28, 86, 30), (30, 108, 38)),
         border=(48, 52, 48, 48))


# ----------------------------------------------------------------------------- mascot
def _ell(d, cx, cy, rx, ry, fill, outline=None, ow=3):
    """Elip có viền, toạ độ tính theo pixel đích (nhân SS bên trong)."""
    if outline:
        d.ellipse([(cx - rx - ow) * SS, (cy - ry - ow) * SS, (cx + rx + ow) * SS, (cy + ry + ow) * SS],
                  fill=outline)
    d.ellipse([(cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS], fill=fill)


def make_mascot():
    """Gấu con tự thiết kế, thò đầu và hai chân lên mép khung điểm."""
    W, H = 192, 150
    img = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img, "RGBA")
    ol = (74, 40, 20, 255)
    fur = (190, 122, 68, 255)
    light = (246, 218, 180, 255)
    dark = (52, 30, 20, 255)

    for cx in (46, 146):                                   # tai
        _ell(d, cx, 40, 24, 24, fur, ol)
        _ell(d, cx, 42, 13, 13, (236, 170, 134, 255))
    _ell(d, 96, 82, 70, 56, fur, ol, 4)                    # đầu
    _ell(d, 96, 100, 33, 24, light)                        # mõm
    for cx in (68, 124):                                   # mắt
        _ell(d, cx, 76, 9, 11, dark)
        _ell(d, cx + 3, 72, 3.5, 3.5, (255, 255, 255, 255))
    for cx in (50, 142):                                   # má hồng
        _ell(d, cx, 98, 11, 7, (255, 140, 130, 150))
    _ell(d, 96, 90, 9, 6, dark)                            # mũi
    for x0 in (86, 96):                                    # miệng cười
        d.arc([x0 * SS, 93 * SS, (x0 + 10) * SS, 105 * SS], 0, 180, fill=dark, width=3 * SS)
    for cx in (46, 146):                                   # hai chân bám mép
        _ell(d, cx, 136, 24, 16, fur, ol)
        for k in (-8, 0, 8):
            d.line([((cx + k) * SS, 130 * SS), ((cx + k) * SS, 138 * SS)], fill=ol, width=2 * SS)
    save("mascot", img.resize((W, H), Image.LANCZOS))


# ----------------------------------------------------------------------------- hiệu ứng
def make_fx():
    """Mảnh gỗ vỡ, tia lấp lánh, vệt sáng quét. Vẽ gần trắng/vàng nhạt để Unity tô màu."""
    # Mảnh gỗ: đa giác méo có mặt sáng và mặt tối
    S = 64
    big = Image.new("RGBA", (S * SS, S * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(big, "RGBA")

    def poly(pts, col):
        d.polygon([(x * SS, y * SS) for x, y in pts], fill=col)

    poly([(9, 20), (34, 8), (57, 24), (48, 53), (15, 50)], (150, 150, 150, 255))   # viền tối
    poly([(13, 22), (34, 12), (52, 26), (45, 48), (18, 46)], (222, 222, 222, 255))  # mặt chính
    poly([(13, 22), (34, 12), (52, 26), (32, 31)], (255, 255, 255, 255))            # mặt sáng
    poly([(32, 31), (52, 26), (45, 48), (18, 46)], (205, 205, 205, 255))            # mặt tối
    save("fx_chip", big.resize((S, S), Image.LANCZOS), ppu=128)

    # Tia lấp lánh 4 cánh + quầng sáng
    N = 64
    yy, xx = np.mgrid[0:N, 0:N]
    u = (xx - (N - 1) / 2) / (N / 2)
    v = (yy - (N - 1) / 2) / (N / 2)
    r = np.sqrt(u * u + v * v)
    glow = np.exp(-(r / 0.28) ** 2)
    ray_h = np.exp(-np.abs(v) * 22) * np.clip(1 - np.abs(u), 0, 1) ** 2
    ray_v = np.exp(-np.abs(u) * 22) * np.clip(1 - np.abs(v), 0, 1) ** 2
    a = np.clip(glow * 0.9 + np.maximum(ray_h, ray_v), 0, 1)
    arr = np.zeros((N, N, 4), np.uint8)
    arr[..., 0], arr[..., 1], arr[..., 2] = 255, 240, 190
    arr[..., 3] = (a * 255).astype(np.uint8)
    save("fx_spark", Image.fromarray(arr, "RGBA"), ppu=64)

    # Vệt sáng quét: đuôi mờ dần về bên trái, đầu sáng gắt ở bên phải
    W, H = 256, 64
    yy, xx = np.mgrid[0:H, 0:W]
    uu = xx / (W - 1)
    vv = (yy - (H - 1) / 2) / (H / 2)
    head = np.cos(np.clip((uu - 0.82) / 0.18, 0, 1) * np.pi / 2) ** 2          # đầu tắt mượt, không cụt
    along = np.where(uu < 0.82, (uu / 0.82) ** 2.2, head)
    window = np.clip(1 - vv ** 2, 0, 1) ** 2                                   # triệt viền trên/dưới
    across = (np.exp(-(vv / 0.28) ** 2) * 0.9 + np.exp(-(vv / 0.7) ** 2) * 0.35) * window
    a = np.clip(along * across, 0, 1)
    arr = np.zeros((H, W, 4), np.uint8)
    arr[..., 0], arr[..., 1], arr[..., 2] = 255, 240, 175
    arr[..., 3] = (a * 255).astype(np.uint8)
    save("fx_streak", Image.fromarray(arr, "RGBA"), ppu=128)


# ----------------------------------------------------------------------------- nền
def make_background():
    W, H = 512, 1024
    arr = wood_arr(W, H, (204, 128, 62), (178, 104, 48), 0.11)
    yy, xx = np.mgrid[0:H, 0:W]
    d = np.sqrt(((xx - W / 2) / (W / 2)) ** 2 + ((yy - H / 2) / (H / 2)) ** 2)
    arr *= (1 - 0.20 * np.clip(d - 0.5, 0, 1))[..., None]  # tối dần ở rìa
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    save("bg_wood", img, ppu=100)


# ----------------------------------------------------------------------------- preview
def make_preview():
    def load(n):
        return Image.open(os.path.join(OUT, n + ".png")).convert("RGBA")

    bg = load("bg_wood").resize((900, 1600), Image.LANCZOS)
    cv = bg.copy()
    # bảng 8x8
    frame = nine(load("board_frame"), (64, 64, 64, 64), (780, 780))
    cv.alpha_composite(frame, (60, 380))
    empty = load("cell_empty").resize((88, 88), Image.LANCZOS)
    block = load("cell_block")

    def tint(img, rgb):
        a = np.asarray(img, float).copy()
        a[..., 0] *= rgb[0]; a[..., 1] *= rgb[1]; a[..., 2] *= rgb[2]
        return Image.fromarray(a.astype(np.uint8), "RGBA")

    gold = tint(block, (1.0, 0.86, 0.50)).resize((88, 88), Image.LANCZOS)
    jade = tint(block, (0.52, 0.86, 0.68)).resize((88, 88), Image.LANCZOS)
    ox, oy = 60 + 38, 380 + 38
    layout = {(0, 0): "g", (1, 0): "g", (0, 1): "j", (1, 1): "j", (2, 0): "g", (3, 0): "g", (4, 3): "j"}
    for gx in range(8):
        for gy in range(8):
            cv.alpha_composite(empty, (ox + gx * 88 - 0, oy + gy * 88))
    for (gx, gy), k in layout.items():
        cv.alpha_composite(gold if k == "g" else jade, (ox + gx * 88, oy + (7 - gy) * 88))
    # HUD
    pb = nine(load("hud_panel_best"), (44, 44, 44, 44), (400, 130))
    ps = nine(load("hud_panel_score"), (44, 44, 44, 44), (400, 130))
    cv.alpha_composite(pb, (40, 110)); cv.alpha_composite(ps, (460, 110))
    cv.alpha_composite(load("icon_crown").resize((72, 48)), (170, 66))
    cv.alpha_composite(load("mascot").resize((144, 113)), (588, 16))
    gb = load("btn_square").resize((100, 100)); cv.alpha_composite(gb, (780, 20))
    cv.alpha_composite(load("icon_gear").resize((64, 64)), (798, 38))
    # popup mẫu
    pop = nine(load("popup_panel"), (96, 96, 96, 96), (560, 420))
    rib = nine(load("title_ribbon"), (56, 56, 56, 56), (440, 100))
    cv.alpha_composite(pop, (170, 1100)); cv.alpha_composite(rib, (230, 1060))
    cv.alpha_composite(nine(load("btn_gold"), (48, 52, 48, 48), (300, 110)), (300, 1360))
    cv.alpha_composite(nine(load("btn_green"), (48, 52, 48, 48), (200, 90)), (60, 1380))
    # Ảnh xem thử nằm cạnh script (không nằm trong Assets để Unity khỏi import)
    cv.convert("RGB").save(os.path.join(os.path.dirname(os.path.abspath(__file__)), "art_preview.png"))


if __name__ == "__main__":
    make_cell()
    make_empty_cell()
    make_board_frame()
    make_hud()
    make_popup()
    make_mascot()
    make_background()
    make_fx()
    with open(os.path.join(OUT, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump({"items": manifest}, f, indent=2)
    make_preview()
    print("OK:", len(manifest), "sprites ->", OUT)
