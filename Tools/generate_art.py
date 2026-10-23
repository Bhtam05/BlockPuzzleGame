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


def inner_shadow(img, mask, depth=6, strength=120, dy=3, color=(0, 0, 0)):
    """Bóng lõm ở mép trên-trái bên trong mask (làm ô trống/khoảng lõm trông như khắc vào gỗ)."""
    w, h = img.size
    inv = Image.eval(mask, lambda v: 255 - v)
    inv = inv.filter(ImageFilter.GaussianBlur(depth))
    off = Image.new("L", (w, h), 255)
    off.paste(inv, (0, dy))
    a = (np.asarray(off, float) / 255.0) * (np.asarray(mask, float) / 255.0) * strength
    layer = np.zeros((h, w, 4), np.uint8)
    layer[..., 0], layer[..., 1], layer[..., 2] = color
    layer[..., 3] = np.clip(a, 0, 255).astype(np.uint8)
    return over(img, Image.fromarray(layer, "RGBA"))


def rim_light(img, mask, strength=110, width=2, dy=-1, color=(255, 255, 255)):
    """Viền sáng mảnh ở mép trên bên trong mask (ánh sáng từ trên chiếu xuống cạnh gờ)."""
    w, h = img.size
    inv = Image.eval(mask, lambda v: 255 - v).filter(ImageFilter.GaussianBlur(width))
    off = Image.new("L", (w, h), 255)
    off.paste(inv, (0, -dy))
    a = (np.asarray(off, float) / 255.0) * (np.asarray(mask, float) / 255.0) * strength
    layer = np.zeros((h, w, 4), np.uint8)
    layer[..., 0], layer[..., 1], layer[..., 2] = color
    layer[..., 3] = np.clip(a, 0, 255).astype(np.uint8)
    return over(img, Image.fromarray(layer, "RGBA"))


# ----------------------------------------------------------------------------- gạch
def grain_arr(w, h, fine=14, band=9, wavy=6):
    """Vân gỗ ngang kiểu ván gỗ thật: sợi mảnh dày đặc + dải sáng/tối + vài đường vân uốn lượn."""
    fine_l = noise_arr(w, h, 120, 1) * fine                      # sợi mảnh, mỗi hàng một giá trị
    band_l = noise_arr(w, h, 160, 9) * band                      # dải sáng tối chậm
    yy, xx = np.mgrid[0:h, 0:w]
    ph = rng.uniform(0, 6.28)
    wave = np.sin((yy + np.sin(xx / w * 5 + ph) * 3.5) / 3.1 + ph)   # đường vân hơi uốn
    wave_l = np.where(wave > 0.78, -1.0, 0.0) * wavy                 # chỉ vài đường tối mảnh
    return fine_l + band_l + wave_l


def make_cell():
    """Ô gạch gỗ sáng như ván gỗ phong: viền nâu mảnh, mặt có vân ngang rõ, vát cạnh sáng trên / tối dưới.
    Vẽ bằng xám/trắng để SpriteRenderer.color tô màu (tông gỗ, xanh ngọc...)."""
    W = 128
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    outline_mask = rr_mask(W, W, 11, 2)
    img = over(img, fill(vgrad(W, W, (118, 118, 118), (96, 96, 96)), outline_mask))   # viền nâu mảnh sau khi tô màu
    face_mask = rr_mask(W, W, 8, 6)
    face = vgrad(W, W, (255, 255, 255), (236, 236, 236)) + grain_arr(W, W)[..., None]
    img = over(img, fill(face, face_mask))
    img = rim_light(img, face_mask, strength=170, width=2, dy=-2)                       # cạnh trên sáng
    img = inner_shadow(img, face_mask, depth=4, strength=95, dy=-3)                     # cạnh dưới tối
    # cạnh trái sáng nhẹ, cạnh phải tối nhẹ để có cảm giác vát đều
    edge = np.zeros((W, W, 4), np.uint8)
    xx = np.arange(W)[None, :].repeat(W, 0)
    left = np.clip(1 - (xx - 6) / 5.0, 0, 1)
    right = np.clip(1 - (W - 7 - xx) / 5.0, 0, 1)
    m = np.asarray(face_mask, float) / 255
    edge[..., :3] = 255
    edge[..., 3] = (left * m * 90).astype(np.uint8)
    img = over(img, Image.fromarray(edge, "RGBA"))
    edge2 = np.zeros((W, W, 4), np.uint8)
    edge2[..., 3] = (right * m * 70).astype(np.uint8)
    img = over(img, Image.fromarray(edge2, "RGBA"))
    save("cell_block", img, ppu=128)


def make_empty_cell():
    """Ô trống: gỗ tối có vân ngang, mép trên tối (hơi lõm), mép dưới có gờ sáng mảnh."""
    W = 128
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    outer = rr_mask(W, W, 11, 2)
    img = over(img, fill(vgrad(W, W, (30, 17, 10), (84, 52, 32)), outer))
    inner_mask = rr_mask(W, W, 8, 5)
    inner = vgrad(W, W, (52, 31, 19), (66, 40, 25)) + grain_arr(W, W, 7, 4, 3)[..., None]
    img = over(img, fill(inner, inner_mask))
    img = inner_shadow(img, inner_mask, depth=5, strength=120, dy=4)
    save("cell_empty", img, ppu=128)


# ----------------------------------------------------------------------------- bảng
def make_board_frame():
    S = 256
    outer = wood_arr(S, S, (226, 146, 68), (172, 100, 44), 0.12)
    img = plate(S, S, 52, ((240, 168, 84), (164, 92, 40)), None, 8, outline=(88, 46, 20),
                face_arr=outer, glossy=False)
    # đường chạm khắc vàng nhạt chạy quanh khung
    inlay = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(inlay).rounded_rectangle([20, 20, S - 21, S - 21], radius=36,
                                            outline=(255, 214, 140, 150), width=2)
    img = over(img, inlay)
    # khoảng lõm chứa các ô: gờ sáng rồi nền nâu tối có bóng đổ vào trong
    img = over(img, solid(S, S, 24, 36, vgrad(S, S, (96, 54, 24), (246, 178, 96))))
    pit_mask = rr_mask(S, S, 22, 40)
    img = over(img, fill(vgrad(S, S, (44, 26, 15), (60, 36, 22)), pit_mask))
    img = inner_shadow(img, pit_mask, depth=10, strength=170, dy=6)
    # ánh sáng từ trên chiếu lên mép khung
    img = rim_light(img, rr_mask(S, S, 48, 4), strength=70, width=3, dy=-2, color=(255, 236, 190))
    save("board_frame", img, ppu=128, border=(64, 64, 64, 64))


# ----------------------------------------------------------------------------- HUD
def make_hud():
    # Khung điểm cao: bảng xanh lá viền vàng nâu
    green = vgrad(256, 128, (46, 132, 74), (30, 98, 54)) + (noise_arr(256, 128, 6, 6) * 5)[..., None]
    p = plate(256, 128, 34, ((228, 160, 60), (160, 96, 28)), None, 9, outline=(86, 48, 14),
              face_arr=green, glossy=False)
    p = over(p, gloss(Image.new("RGBA", (256, 128), (0, 0, 0, 0)), rr_mask(256, 128, 25, 12), 38, 0.5))
    p = inner_shadow(p, rr_mask(256, 128, 25, 12), depth=6, strength=110, dy=4)
    save("hud_panel_best", p, border=(44, 44, 44, 44))

    # Khung điểm hiện tại: gỗ tối, viền sáng phát sáng nhẹ
    wd = wood_arr(256, 128, (112, 66, 36), (78, 44, 24), 0.10)
    q = plate(256, 128, 34, ((255, 190, 214), (212, 140, 190)), None, 7, outline=(255, 214, 236),
              face_arr=wd, glossy=False)
    glow = Image.new("RGBA", (256, 128), (0, 0, 0, 0))
    ImageDraw.Draw(glow).rounded_rectangle([2, 2, 253, 125], radius=34, outline=(255, 170, 220, 255), width=6)
    glow = glow.filter(ImageFilter.GaussianBlur(5))
    q = inner_shadow(q, rr_mask(256, 128, 27, 9), depth=6, strength=120, dy=4)
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

    # Hạt bụi sáng mềm cho không khí nền (tròn, mờ dần ra rìa)
    N = 32
    yy, xx = np.mgrid[0:N, 0:N]
    r = np.sqrt(((xx - (N - 1) / 2) / (N / 2)) ** 2 + ((yy - (N - 1) / 2) / (N / 2)) ** 2)
    a = np.clip(1 - r, 0, 1) ** 1.8
    arr = np.zeros((N, N, 4), np.uint8)
    arr[..., 0], arr[..., 1], arr[..., 2] = 255, 236, 190
    arr[..., 3] = (a * 255).astype(np.uint8)
    save("fx_mote", Image.fromarray(arr, "RGBA"), ppu=128)


# ----------------------------------------------------------------------------- nền
def make_background():
    """Nền ván gỗ ghép dọc, có khe giữa các ván, ánh sáng ấm ở giữa-trên và tối dần ở rìa."""
    W, H = 512, 1024
    arr = wood_arr(W, H, (206, 130, 64), (176, 102, 46), 0.11)
    # chia thành 4 ván, mỗi ván lệch tông một chút
    plank_w = W // 4
    for i in range(4):
        shade = 1 + rng.uniform(-0.05, 0.05)
        arr[:, i * plank_w:(i + 1) * plank_w] *= shade
    for i in range(1, 4):                      # khe tối + gờ sáng bên cạnh
        x = i * plank_w
        arr[:, x - 1:x + 1] *= 0.55
        arr[:, x + 1:x + 3] *= 1.07
    yy, xx = np.mgrid[0:H, 0:W]
    d = np.sqrt(((xx - W / 2) / (W / 2)) ** 2 + ((yy - H * 0.38) / (H * 0.62)) ** 2)
    arr *= (1.10 - 0.34 * np.clip(d - 0.25, 0, 1))[..., None]     # sáng giữa-trên, tối dần ra rìa
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    save("bg_wood", img, ppu=100)


# ----------------------------------------------------------------------------- chất liệu khối mới
def _bigdraw(W):
    big = Image.new("RGB", (W * SS, W * SS), (0, 0, 0))
    return big, ImageDraw.Draw(big, "RGBA")


def _shrink(big, W):
    return big.convert("RGBA").resize((W, W), Image.LANCZOS)


def _finish_cell(face, W, outline_col, outline_arr_top=None, bevel=True, r_out=11, r_face=8, inset_face=6):
    """Ghép viền + mặt (face là ảnh RGBA đã vẽ) vào khung ô bo góc chuẩn 128px, thêm vát cạnh."""
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    img = over(img, fill(vgrad(W, W, outline_col[0], outline_col[1]), rr_mask(W, W, r_out, 2)))
    face_mask = rr_mask(W, W, r_face, inset_face)
    f = face.copy()
    a = np.asarray(f.split()[3], float) * np.asarray(face_mask, float) / 255.0
    f.putalpha(Image.fromarray(a.astype(np.uint8)))
    img = over(img, f)
    if bevel:
        img = rim_light(img, face_mask, strength=120, width=2, dy=-2)
        img = inner_shadow(img, face_mask, depth=4, strength=80, dy=-3)
    return img


def make_cell_grass():
    """Ô cỏ: thảm cỏ xanh có ngọn cỏ dày, vài bông hoa nhỏ, viền xanh đậm."""
    W = 128
    base = fill(vgrad(W, W, (150, 214, 88), (96, 170, 58)) + noise_arr(W, W, 10, 10)[..., None] * 10,
                Image.new("L", (W, W), 255))
    big = base.convert("RGB").resize((W * SS, W * SS), Image.LANCZOS)
    d = ImageDraw.Draw(big, "RGBA")
    r = np.random.default_rng(21)
    for _ in range(260):   # ngọn cỏ: nét ngắn hơi nghiêng, mảnh sáng + mảnh tối
        x = r.uniform(4, W - 4); y = r.uniform(8, W - 2)
        L = r.uniform(7, 15); lean = r.uniform(-3.5, 3.5)
        tone = r.random()
        col = (210, 245, 120, 190) if tone > 0.55 else (52, 120, 40, 170)
        d.line([(x * SS, y * SS), ((x + lean) * SS, (y - L) * SS)], fill=col, width=int(r.uniform(1.4, 2.4) * SS))
    for (fx, fy, c) in [(30, 40, (255, 250, 235)), (92, 76, (255, 220, 90)), (62, 102, (255, 250, 235)), (100, 28, (255, 190, 210))]:
        for k in range(5):   # bông hoa 5 cánh
            ang = k * 1.2566
            d.ellipse([(fx + math.cos(ang) * 4 - 3) * SS, (fy + math.sin(ang) * 4 - 3) * SS,
                       (fx + math.cos(ang) * 4 + 3) * SS, (fy + math.sin(ang) * 4 + 3) * SS], fill=c + (255,))
        d.ellipse([(fx - 2.4) * SS, (fy - 2.4) * SS, (fx + 2.4) * SS, (fy + 2.4) * SS], fill=(240, 160, 40, 255))
    img = _finish_cell(_shrink(big, W), W, ((60, 110, 40), (38, 86, 34)))
    save("cell_grass", img, ppu=128)


def make_cell_gem():
    """Ô đá quý: viên ngọc cắt mặt xanh lam, có mặt sáng/tối và đốm lấp lánh."""
    W = 128
    big, d = _bigdraw(W)
    S = SS
    d.rectangle([0, 0, W * S, W * S], fill=(70, 160, 235, 255))
    c = W / 2
    inner = 30   # hình vuông giữa
    def P(pts, col): d.polygon([(x * S, y * S) for x, y in pts], fill=col)
    # bốn mặt nghiêng từ viền vào khung giữa
    P([(0, 0), (W, 0), (W - inner, inner), (inner, inner)], (190, 236, 255, 255))     # trên: sáng nhất
    P([(0, 0), (inner, inner), (inner, W - inner), (0, W)], (110, 196, 248, 255))     # trái
    P([(W, 0), (W, W), (W - inner, W - inner), (W - inner, inner)], (36, 96, 190, 255))  # phải: tối
    P([(0, W), (W, W), (W - inner, W - inner), (inner, W - inner)], (26, 70, 156, 255))  # dưới: tối nhất
    P([(inner, inner), (W - inner, inner), (W - inner, W - inner), (inner, W - inner)], (84, 178, 246, 255))  # mặt giữa
    # mặt nhỏ trong mặt giữa tạo cảm giác cắt kim cương
    P([(inner, inner), (W - inner, inner), (c, c)], (170, 228, 255, 255))
    P([(inner, inner), (c, c), (inner, W - inner)], (120, 205, 250, 255))
    P([(W - inner, inner), (W - inner, W - inner), (c, c)], (52, 128, 214, 255))
    P([(inner, W - inner), (c, c), (W - inner, W - inner)], (40, 108, 198, 255))
    for a, b in [((0, 0), (inner, inner)), ((W, 0), (W - inner, inner)), ((0, W), (inner, W - inner)), ((W, W), (W - inner, W - inner))]:
        d.line([(a[0] * S, a[1] * S), (b[0] * S, b[1] * S)], fill=(255, 255, 255, 90), width=2 * S)
    face = _shrink(big, W)
    img = _finish_cell(face, W, ((24, 44, 120), (16, 30, 96)), r_face=9)
    # lấp lánh trắng
    spark = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ds = ImageDraw.Draw(spark, "RGBA")
    for (sx, sy, sz) in [(34, 30, 9), (92, 92, 6)]:
        ds.polygon([(sx, sy - sz), (sx + sz * 0.28, sy - sz * 0.28), (sx + sz, sy), (sx + sz * 0.28, sy + sz * 0.28),
                    (sx, sy + sz), (sx - sz * 0.28, sy + sz * 0.28), (sx - sz, sy), (sx - sz * 0.28, sy - sz * 0.28)],
                   fill=(255, 255, 255, 240))
    img = over(img, spark.filter(ImageFilter.GaussianBlur(0.6)))
    save("cell_gem", img, ppu=128)


def make_cell_melon():
    """Ô dưa hấu: ruột đỏ hồng, hạt đen, vòng vỏ trắng xanh nhạt rồi vỏ xanh sọc."""
    W = 128
    big, d = _bigdraw(W)
    S = SS
    d.rectangle([0, 0, W * S, W * S], fill=(255, 84, 100, 255))
    # ruột: gradient tâm sáng hơn
    for k in range(18, 0, -1):
        t = k / 18.0
        col = (int(255 - 20 * t), int(150 - 70 * t), int(150 - 55 * t), 255)
        m = 6 + (1 - t) * 26
        d.rounded_rectangle([m * S, m * S, (W - m) * S, (W - m) * S], radius=int((10 + (1 - t) * 6) * S), fill=col)
    # vòng vỏ trắng + xanh nhạt + sọc xanh đậm
    ring = Image.new("RGBA", (W * S, W * S), (0, 0, 0, 0))
    dr = ImageDraw.Draw(ring, "RGBA")
    dr.rectangle([0, 0, W * S, W * S], fill=(46, 140, 70, 255))                                   # vỏ ngoài
    r2 = np.random.default_rng(5)
    for xx in range(-W, W * 2, 18):                                                               # sọc xiên
        dr.polygon([(xx * S, 0), ((xx + 7) * S, 0), ((xx + 7 - 40) * S, W * S), ((xx - 40) * S, W * S)], fill=(26, 98, 50, 120))
    dr.rounded_rectangle([7 * S, 7 * S, (W - 7) * S, (W - 7) * S], radius=12 * S, fill=(214, 240, 178, 255))   # vỏ trắng
    dr.rounded_rectangle([12 * S, 12 * S, (W - 12) * S, (W - 12) * S], radius=10 * S, fill=(255, 255, 255, 0))
    hole = Image.new("L", (W * S, W * S), 255)
    ImageDraw.Draw(hole).rounded_rectangle([12 * S, 12 * S, (W - 12) * S, (W - 12) * S], radius=10 * S, fill=0)
    ring.putalpha(hole)
    big.paste(ring.convert("RGB"), (0, 0), ring.split()[3])
    d = ImageDraw.Draw(big, "RGBA")
    for (hx, hy, ang) in [(40, 44, 20), (82, 38, -25), (60, 66, 5), (36, 88, -15), (90, 84, 25), (62, 100, 0)]:
        ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
        pts = []
        for tt in np.linspace(0, 2 * math.pi, 18, endpoint=False):
            lx, ly = math.cos(tt) * 4.2, math.sin(tt) * 7.0 * (0.6 + 0.4 * (math.sin(tt) < 0))
            pts.append(((hx + lx * ca - ly * sa) * S, (hy + lx * sa + ly * ca) * S))
        d.polygon(pts, fill=(36, 20, 24, 255))
        d.ellipse([(hx - 1.6) * S, (hy - 3.5) * S, (hx - 0.2) * S, (hy - 1.8) * S], fill=(255, 255, 255, 110))
    img = _finish_cell(_shrink(big, W), W, ((22, 78, 40), (16, 60, 34)), bevel=True)
    save("cell_melon", img, ppu=128)


def make_cell_wool():
    """Ô vải len: các hàng mũi đan chữ V, sợi bông xù, màu hồng đào."""
    W = 128
    big, d = _bigdraw(W)
    S = SS
    d.rectangle([0, 0, W * S, W * S], fill=(232, 150, 190, 255))
    r = np.random.default_rng(33)
    cw, ch = 16, 14
    for row in range(-1, W // ch + 2):
        for col in range(-1, W // cw + 2):
            x0 = col * cw + (cw // 2 if row % 2 else 0)
            y0 = row * ch
            tone = 1.0 + r.uniform(-0.05, 0.05)
            hi = (int(min(255, 250 * tone)), int(min(255, 196 * tone)), int(min(255, 222 * tone)), 255)
            lo = (196, 100, 150, 255)
            # hai nét chéo tạo chữ V, nét sáng + bóng tối bên dưới
            for (ax, ay, bx, by) in [(x0, y0, x0 + cw / 2, y0 + ch), (x0 + cw, y0, x0 + cw / 2, y0 + ch)]:
                d.line([(ax * S, (ay + 2) * S), (bx * S, (by + 2) * S)], fill=lo, width=int(5.2 * S))
                d.line([(ax * S, ay * S), (bx * S, by * S)], fill=hi, width=int(4.4 * S))
                d.line([(ax * S, (ay - 1) * S), (bx * S, (by - 1) * S)], fill=(255, 226, 238, 150), width=int(1.4 * S))
    face = _shrink(big, W)
    # sợi bông li ti
    arr = np.asarray(face, float)
    arr[..., :3] += (r.normal(0, 7, (W, W))[..., None])
    face = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")
    img = _finish_cell(face, W, ((170, 78, 128), (140, 60, 108)), r_out=12, r_face=10, inset_face=5)
    # đường may viền ngoài (nét đứt sáng)
    st = Image.new("RGBA", (W * SS, W * SS), (0, 0, 0, 0))
    ds = ImageDraw.Draw(st, "RGBA")
    m = 9 * SS
    n = 15
    for k in range(n):
        t0, t1 = k / n, (k + 0.55) / n
        for (p0, p1) in [((m + t0 * (W * SS - 2 * m), m), (m + t1 * (W * SS - 2 * m), m)),
                         ((m + t0 * (W * SS - 2 * m), W * SS - m), (m + t1 * (W * SS - 2 * m), W * SS - m)),
                         ((m, m + t0 * (W * SS - 2 * m)), (m, m + t1 * (W * SS - 2 * m))),
                         ((W * SS - m, m + t0 * (W * SS - 2 * m)), (W * SS - m, m + t1 * (W * SS - 2 * m)))]:
            ds.line([p0, p1], fill=(255, 245, 250, 200), width=int(2.2 * SS))
    img = over(img, _shrink(st, W))
    save("cell_wool", img, ppu=128)


def make_glow():
    """Quầng sáng vàng bo vuông phủ lên ô thuộc hàng/cột sắp đầy khi đang đưa khối vào."""
    W = 128
    big = Image.new("L", (W * SS, W * SS), 0)
    ImageDraw.Draw(big).rounded_rectangle([12 * SS, 12 * SS, (W - 12) * SS, (W - 12) * SS], radius=14 * SS, fill=255)
    core = big.resize((W, W), Image.LANCZOS)
    halo = np.asarray(core.filter(ImageFilter.GaussianBlur(9)), float) / 255
    inner = np.asarray(core, float) / 255
    a = np.clip(halo * 0.9 + inner * 0.25, 0, 1)
    arr = np.zeros((W, W, 4), np.uint8)
    arr[..., 0], arr[..., 1], arr[..., 2] = 255, 226, 120
    arr[..., 3] = (a * 255).astype(np.uint8)
    save("fx_cell_glow", Image.fromarray(arr, "RGBA"), ppu=128)


# ----------------------------------------------------------------------------- menu rừng nhỏ
def _leaf_poly(cx, cy, length, width, ang):
    """Đa giác chiếc lá (nhọn hai đầu), ang tính bằng độ, trả về danh sách điểm."""
    pts = []
    ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    for t in np.linspace(0, 1, 14):
        x = t * length
        y = math.sin(t * math.pi) * width * (1 - 0.25 * t)
        pts.append((x, y))
    for t in np.linspace(1, 0, 14):
        x = t * length
        y = -math.sin(t * math.pi) * width * (1 - 0.25 * t)
        pts.append((x, y))
    return [(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in pts]


def _draw_leaf(d, cx, cy, length, width, ang, col, vein=True, S=1):
    col = tuple(int(c) for c in col)
    pts = _leaf_poly(cx, cy, length, width, ang)
    d.polygon([(x * S, y * S) for x, y in pts], fill=col)
    if vein:
        ex, ey = cx + math.cos(math.radians(ang)) * length * 0.92, cy + math.sin(math.radians(ang)) * length * 0.92
        d.line([(cx * S, cy * S), (ex * S, ey * S)], fill=tuple(min(255, c + 40) for c in col[:3]) + (255,), width=max(1, int(S * 1.2)))


def _draw_flower(d, x, y, r, petal, center, S=1):
    petal = tuple(int(c) for c in petal)
    center = tuple(int(c) for c in center)
    for k in range(5):
        ang = k * 1.2566 - 1.57
        px, py = x + math.cos(ang) * r * 0.9, y + math.sin(ang) * r * 0.9
        d.ellipse([(px - r * 0.75) * S, (py - r * 0.75) * S, (px + r * 0.75) * S, (py + r * 0.75) * S], fill=petal)
    d.ellipse([(x - r * 0.55) * S, (y - r * 0.55) * S, (x + r * 0.55) * S, (y + r * 0.55) * S], fill=center)


def make_menu_bg():
    """Nền rừng hoàng hôn: trời tím-hồng, trăng mờ, các lớp đồi cây xa dần, sương cuối cùng ở dưới."""
    W, H = 720, 1280
    sky = np.zeros((H, W, 3), float)
    stops = [(0.0, (30, 28, 70)), (0.35, (84, 56, 110)), (0.58, (190, 100, 110)), (0.72, (244, 170, 110)), (1.0, (60, 70, 60))]
    ys = np.linspace(0, 1, H)
    for c in range(3):
        sky[..., c] = np.interp(ys, [s[0] for s in stops], [s[1][c] for s in stops])[:, None]
    img = Image.fromarray(sky.astype(np.uint8), "RGB").convert("RGBA")
    r = np.random.default_rng(77)
    # sao
    d = ImageDraw.Draw(img, "RGBA")
    for _ in range(90):
        x, y = r.uniform(0, W), r.uniform(0, H * 0.42)
        a = int(r.uniform(90, 230) * (1 - y / (H * 0.45)))
        s = r.uniform(0.8, 2.0)
        d.ellipse([x - s, y - s, x + s, y + s], fill=(255, 245, 220, max(40, a)))
    # trăng + quầng
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse([430, 170, 640, 380], fill=(255, 224, 170, 90))
    img = over(img, glow.filter(ImageFilter.GaussianBlur(46)))
    d = ImageDraw.Draw(img, "RGBA")
    d.ellipse([480, 220, 590, 330], fill=(255, 242, 214, 255))
    d.ellipse([505, 238, 540, 272], fill=(240, 222, 190, 120))
    d.ellipse([538, 280, 562, 304], fill=(240, 222, 190, 100))

    def hills(base, amp, col, seed, trees, tcol, tscale):
        rr = np.random.default_rng(seed)
        layer = Image.new("RGBA", (W * 2, H * 2), (0, 0, 0, 0))
        dl = ImageDraw.Draw(layer, "RGBA")
        pts = [(0, H * 2)]
        ph = rr.uniform(0, 6)
        for x in range(0, W * 2 + 8, 8):
            y = H * 2 * base - (math.sin(x / 2 / 140 + ph) * 0.6 + math.sin(x / 2 / 61 + ph * 2) * 0.4) * amp * 2
            pts.append((x, y))
        pts.append((W * 2, H * 2))
        dl.polygon(pts, fill=col)
        for _ in range(trees):   # cây thông
            tx = rr.uniform(0, W)
            idx = int(tx * 2 / 8) + 1
            ty = pts[min(idx, len(pts) - 2)][1] / 2 + 6
            hgt = rr.uniform(70, 130) * tscale
            wid = hgt * 0.38
            for k in range(4):
                top = ty - hgt + k * hgt * 0.22
                bot = ty - hgt * 0.2 + k * hgt * 0.1
                bw = wid * (0.45 + k * 0.22)
                dl.polygon([(tx * 2, top * 2), ((tx - bw) * 2, bot * 2), ((tx + bw) * 2, bot * 2)], fill=tcol)
            dl.rectangle([(tx - 2) * 2, (ty - hgt * 0.2) * 2, (tx + 2) * 2, (ty + 4) * 2], fill=tcol)
        return layer.resize((W, H), Image.LANCZOS)

    img = over(img, hills(0.66, 40, (96, 70, 110, 255), 1, 26, (88, 64, 104, 255), 0.7))
    mist = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(mist).rectangle([0, int(H * 0.60), W, int(H * 0.74)], fill=(255, 190, 150, 70))
    img = over(img, mist.filter(ImageFilter.GaussianBlur(40)))
    img = over(img, hills(0.74, 44, (52, 62, 84, 255), 2, 30, (44, 56, 80, 255), 0.95))
    img = over(img, hills(0.83, 36, (28, 52, 56, 255), 3, 22, (22, 44, 50, 255), 1.3))
    img = over(img, hills(0.93, 24, (18, 38, 38, 255), 4, 8, (14, 32, 34, 255), 1.7))
    # tối dần ở rìa trên cho nổi bật chữ
    vig = np.zeros((H, W, 4), np.uint8)
    yy, xx = np.mgrid[0:H, 0:W]
    dist = np.sqrt(((xx - W / 2) / (W * 0.75)) ** 2 + ((yy - H * 0.45) / (H * 0.75)) ** 2)
    vig[..., 3] = (np.clip(dist - 0.45, 0, 1) ** 1.5 * 150).astype(np.uint8)
    img = over(img, Image.fromarray(vig, "RGBA"))
    save("menu_bg", img, ppu=100)


def make_menu_vine(name, seed, flip):
    """Dây leo rũ từ góc trên xuống, có lá và vài bông hoa. Sprite 300x760."""
    W, H = 300, 760
    S = 3
    r = np.random.default_rng(seed)
    big = Image.new("RGBA", (W * S, H * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(big, "RGBA")
    # đường dây chính: đi xuống uốn lượn
    pts = []
    for t in np.linspace(0, 1, 120):
        x = 40 + math.sin(t * 6.2 + seed) * 28 + t * 70 + math.sin(t * 15) * 4
        y = -10 + t * (H - 80)
        pts.append((x, y))
    d.line([(x * S, y * S) for x, y in pts], fill=(58, 100, 44, 255), width=int(7 * S), joint="curve")
    d.line([(x * S, y * S) for x, y in pts], fill=(96, 150, 62, 255), width=int(3.4 * S), joint="curve")
    # dây phụ
    for st in (0.22, 0.45, 0.66):
        i = int(st * len(pts))
        px, py = pts[i]
        side = 1 if r.random() > 0.5 else -1
        sub = []
        for t in np.linspace(0, 1, 40):
            sub.append((px + side * t * 70 + math.sin(t * 5) * 8, py + t * 120 + math.sin(t * 3 + st) * 6))
        d.line([(x * S, y * S) for x, y in sub], fill=(70, 118, 50, 255), width=int(4 * S), joint="curve")
        for k in range(3, 40, 6):
            lx, ly = sub[k]
            _draw_leaf(d, lx, ly, 26, 9, r.uniform(-30, 30) + (0 if side > 0 else 180) + 40, (112, 176, 70, 255), S=S)
    for i in range(4, len(pts) - 2, 5):
        lx, ly = pts[i]
        base = r.choice([-1, 1])
        ang = (35 if base > 0 else 145) + r.uniform(-25, 25)
        tone = r.choice([(104, 170, 66, 255), (136, 196, 82, 255), (78, 138, 56, 255)])
        _draw_leaf(d, lx, ly, r.uniform(30, 48), r.uniform(10, 16), ang, tone, S=S)
    for st, col in [(0.18, (255, 214, 120)), (0.38, (255, 170, 190)), (0.58, (255, 250, 235)), (0.80, (255, 190, 120))]:
        fx, fy = pts[int(st * len(pts))]
        _draw_flower(d, fx + r.uniform(-8, 8), fy, 10 + r.uniform(0, 3), col + (255,), (240, 150, 50, 255), S=S)
    img = big.resize((W, H), Image.LANCZOS)
    if flip:
        img = img.transpose(Image.FLIP_LEFT_RIGHT)
    # bóng nhẹ phía sau để lá nổi trên nền
    sh = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    a = np.asarray(img.split()[3], float) * 0.35
    sh.putalpha(Image.fromarray(a.astype(np.uint8)))
    sh = sh.filter(ImageFilter.GaussianBlur(3))
    save(name, over(sh, img), ppu=100)


def make_menu_bush():
    """Bụi cây + hoa dọc mép dưới màn hình, 720x300 (gấu thò đầu ra từ sau bụi)."""
    W, H = 720, 300
    S = 2
    r = np.random.default_rng(88)
    big = Image.new("RGBA", (W * S, H * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(big, "RGBA")
    for layer, (col, ytop) in enumerate([((34, 74, 44, 255), 110), ((52, 104, 52, 255), 150), ((78, 140, 64, 255), 190)]):
        for _ in range(70):
            x = r.uniform(-20, W + 20)
            y = ytop + r.uniform(-20, 40)
            rad = r.uniform(26, 48)
            d.ellipse([(x - rad) * S, (y - rad * 0.8) * S, (x + rad) * S, (y + rad * 0.8) * S], fill=col)
        d.rectangle([0, (ytop + 30) * S, W * S, H * S], fill=col)
    for _ in range(46):
        x = r.uniform(0, W); y = r.uniform(100, 250)
        _draw_leaf(d, x, y, r.uniform(26, 44), r.uniform(8, 12), r.uniform(200, 340),
                   r.choice([(120, 190, 78, 255), (98, 164, 66, 255), (150, 208, 96, 255)]), S=S)
    for _ in range(16):
        x = r.uniform(10, W - 10); y = r.uniform(110, 230)
        _draw_flower(d, x, y, r.uniform(7, 11),
                     r.choice([(255, 244, 230, 255), (255, 200, 120, 255), (255, 160, 184, 255), (255, 226, 110, 255)]),
                     (240, 150, 50, 255), S=S)
    save("menu_bush", big.resize((W, H), Image.LANCZOS), ppu=100)


def _sign(name, w, h, ropes, nails=True):
    """Bảng gỗ treo bằng hai sợi dây (ropes = chiều cao dây phía trên bảng)."""
    pad = 6
    total_h = h + ropes
    img = Image.new("RGBA", (w, total_h), (0, 0, 0, 0))
    plank = plate(w, h, 26, ((150, 98, 56), (104, 64, 36)), ((214, 160, 98), (178, 124, 70)), 8, (60, 34, 20),
                  face_arr=wood_arr(w, h, (216, 164, 102), (176, 122, 68), 0.1))
    # vài đường ghép ván
    seam = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ds = ImageDraw.Draw(seam)
    for yy in (h // 3, 2 * h // 3):
        ds.line([(30, yy), (w - 30, yy)], fill=(80, 46, 26, 70), width=3)
    seam.putalpha(Image.eval(seam.split()[3], lambda v: v))
    plank = over(plank, seam)
    img.alpha_composite(plank, (0, ropes))
    d = ImageDraw.Draw(img, "RGBA")
    for rx in (int(w * 0.2), int(w * 0.8)):
        d.line([(rx, 0), (rx, ropes + 20)], fill=(120, 84, 48, 255), width=9)
        d.line([(rx - 2, 0), (rx - 2, ropes + 20)], fill=(176, 130, 80, 255), width=3)
        for k in range(0, ropes, 14):
            d.line([(rx - 5, k), (rx + 5, k + 7)], fill=(86, 56, 30, 200), width=2)
        if nails:
            d.ellipse([rx - 11, ropes + 12, rx + 11, ropes + 34], fill=(70, 70, 76, 255))
            d.ellipse([rx - 6, ropes + 16, rx + 2, ropes + 24], fill=(180, 180, 190, 255))
    save(name, img, ppu=100)
    return img


def make_menu_signs():
    _sign("menu_sign", 760, 280, 70)
    _sign("menu_plank", 480, 170, 90)
    # lá lẻ để làm lá rơi
    W = 64
    S = 4
    big = Image.new("RGBA", (W * S, W * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(big, "RGBA")
    _draw_leaf(d, 8, 32, 48, 17, 0, (150, 206, 96, 255), S=S)
    _draw_leaf(d, 8, 32, 48, 8, 0, (110, 168, 68, 255), vein=False, S=S)
    d.line([(8 * S, 32 * S), (54 * S, 32 * S)], fill=(196, 236, 150, 255), width=2 * S)
    save("menu_leaf", big.resize((W, W), Image.LANCZOS), ppu=100)


def make_forest_menu():
    make_menu_bg()
    make_menu_vine("menu_vine_left", 3, False)
    make_menu_vine("menu_vine_right", 9, True)
    make_menu_bush()
    make_menu_signs()


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

    gold = tint(block, (0.97, 0.79, 0.54)).resize((88, 88), Image.LANCZOS)
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
    make_cell_grass()
    make_cell_gem()
    make_cell_melon()
    make_cell_wool()
    make_glow()
    make_forest_menu()
    with open(os.path.join(OUT, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump({"items": manifest}, f, indent=2)
    make_preview()
    print("OK:", len(manifest), "sprites ->", OUT)
