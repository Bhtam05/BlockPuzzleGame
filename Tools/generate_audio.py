#!/usr/bin/env python3
"""Sinh toàn bộ âm thanh game bằng code (numpy + wave), không cần file nhạc bên ngoài.

Chạy:  python3 Tools/generate_audio.py
Kết quả: Assets/Resources/Audio/*.wav (mono, 16-bit, 22050 Hz). Unity tự import, AudioManager đọc qua Resources.
Muốn thay âm thanh thật: ghi đè file cùng tên (sfx_place, sfx_pick, sfx_clear, sfx_combo, sfx_gameover, sfx_click, bgm_main).
"""
import os
import wave

import numpy as np

SR = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "Audio")
rng = np.random.default_rng(7)


def t_axis(sec):
    return np.arange(int(SR * sec)) / SR


def env_exp(n, decay, attack=0.004):
    """Bao hình: lên nhanh, tắt theo hàm mũ."""
    t = np.arange(n) / SR
    e = np.exp(-t * decay)
    a = max(1, int(SR * attack))
    e[:a] *= np.linspace(0, 1, a)
    return e


def tone(freq, sec, decay=8.0, harm=(1.0, 0.4, 0.15), attack=0.004):
    """Nốt kiểu marimba / gõ gỗ: vài hài âm tắt nhanh."""
    t = t_axis(sec)
    s = np.zeros_like(t)
    for i, h in enumerate(harm, start=1):
        s += h * np.sin(2 * np.pi * freq * i * t)
    return s * env_exp(len(t), decay, attack)


def lowpass(x, k=0.2):
    y = np.zeros_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc += k * (v - acc)
        y[i] = acc
    return y


def normalize(x, peak=0.8):
    m = np.max(np.abs(x))
    return x if m == 0 else x / m * peak


def fade_edges(x, ms=6):
    n = int(SR * ms / 1000)
    x = x.copy()
    x[:n] *= np.linspace(0, 1, n)
    x[-n:] *= np.linspace(1, 0, n)
    return x


def write(name, x):
    os.makedirs(OUT, exist_ok=True)
    data = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print("%-14s %5.2fs %6.1f KB" % (name, len(x) / SR, len(data) * 2 / 1024))


def pad(x, sec):
    out = np.zeros(int(SR * sec))
    out[: len(x)] += x[: len(out)]
    return out


def mix_at(buf, x, at):
    i = int(SR * at)
    end = min(len(buf), i + len(x))
    buf[i:end] += x[: end - i]


# ---------------------------------------------------------------- SFX

def sfx_place():
    """Tiếng gõ gỗ trầm khi đặt khối."""
    n = int(SR * 0.16)
    thump = tone(150, 0.16, decay=28, harm=(1.0, 0.5, 0.2))
    noise = lowpass(rng.standard_normal(n), 0.25) * env_exp(n, 70)
    click = tone(900, 0.16, decay=90, harm=(1.0,)) * 0.3
    return fade_edges(normalize(thump + noise * 0.9 + click, 0.85))


def sfx_pick():
    """Tiếng tick nhẹ khi nhấc khối."""
    return fade_edges(normalize(tone(620, 0.07, decay=55, harm=(1.0, 0.3)), 0.5))


def sfx_click():
    """Tiếng bấm nút."""
    x = tone(480, 0.09, decay=45, harm=(1.0, 0.5)) + tone(960, 0.09, decay=60, harm=(1.0,)) * 0.4
    return fade_edges(normalize(x, 0.6))


def sfx_clear():
    """Xóa đường: chuỗi nốt marimba đi lên (C5 E5 G5 C6)."""
    buf = np.zeros(int(SR * 0.9))
    for i, f in enumerate([523.25, 659.25, 783.99, 1046.5]):
        mix_at(buf, tone(f, 0.5, decay=7, harm=(1.0, 0.35, 0.12)) * (0.8 + 0.05 * i), i * 0.07)
    shimmer = tone(2093, 0.4, decay=10, harm=(1.0,)) * 0.15
    mix_at(buf, shimmer, 0.2)
    return fade_edges(normalize(buf, 0.85), 10)


def sfx_combo():
    """Combo: hai nốt cao lấp lánh."""
    buf = np.zeros(int(SR * 0.6))
    mix_at(buf, tone(1318.5, 0.4, decay=9, harm=(1.0, 0.3)), 0.0)
    mix_at(buf, tone(1760.0, 0.45, decay=8, harm=(1.0, 0.3)), 0.09)
    mix_at(buf, tone(2637.0, 0.3, decay=14, harm=(1.0,)) * 0.3, 0.12)
    return fade_edges(normalize(buf, 0.7), 10)


def sfx_gameover():
    """Game over: bốn nốt đi xuống, trầm dần."""
    buf = np.zeros(int(SR * 1.5))
    for i, f in enumerate([392.0, 349.23, 311.13, 261.63]):
        mix_at(buf, tone(f, 0.7, decay=4.5, harm=(1.0, 0.5, 0.2)) , i * 0.22)
    mix_at(buf, tone(130.81, 1.0, decay=3.0, harm=(1.0, 0.4)) * 0.7, 0.66)
    return fade_edges(normalize(buf, 0.85), 12)


def sfx_levelup():
    """Đổi chất liệu khối: arpeggio đi lên sáng và lấp lánh (C5 G5 C6 E6)."""
    buf = np.zeros(int(SR * 1.1))
    for i, f in enumerate([523.25, 783.99, 1046.5, 1318.5]):
        mix_at(buf, tone(f, 0.55, decay=6, harm=(1.0, 0.4, 0.15)) * (0.75 + 0.06 * i), i * 0.09)
    mix_at(buf, tone(2093, 0.5, decay=7, harm=(1.0,)) * 0.2, 0.3)
    mix_at(buf, tone(2637, 0.4, decay=9, harm=(1.0,)) * 0.15, 0.4)
    return fade_edges(normalize(buf, 0.8), 10)


# ---------------------------------------------------------------- Nhạc nền

def bgm_main():
    """Vòng nhạc êm 24 giây (80 BPM, 8 ô nhịp, C - Am - F - G lặp hai lần), nốt gảy marimba + bass.
    Đuôi nốt được cuộn về đầu bản nên lặp vòng không bị ngắt."""
    bpm = 80
    beat = 60.0 / bpm
    bar = 4 * beat
    bars = 8
    total = bars * bar
    buf = np.zeros(int(SR * total))

    # (bass, [nốt hợp âm]) cho mỗi ô nhịp
    chords = [
        (130.81, [261.63, 329.63, 392.00]),   # C
        (110.00, [220.00, 261.63, 329.63]),   # Am
        (87.31, [174.61, 220.00, 261.63]),    # F
        (98.00, [196.00, 246.94, 293.66]),    # G
    ]
    pent = [523.25, 587.33, 659.25, 783.99, 880.00, 1046.5]  # C pentatonic cao cho giai điệu
    melody_seed = np.random.default_rng(11)

    def add_wrapped(x, at):
        i = int(SR * at)
        for k in range(len(x)):
            buf[(i + k) % len(buf)] += x[k]

    for b in range(bars):
        bass, notes = chords[b % 4]
        t0 = b * bar
        add_wrapped(tone(bass, 1.6, decay=2.2, harm=(1.0, 0.3)) * 0.55, t0)
        add_wrapped(tone(bass, 1.6, decay=2.2, harm=(1.0, 0.3)) * 0.35, t0 + 2 * beat)
        # arpeggio nốt đôi
        for j in range(8):
            f = notes[[0, 1, 2, 1][j % 4]] * (2 if j % 4 != 0 else 1)
            add_wrapped(tone(f, 0.7, decay=5, harm=(1.0, 0.3, 0.1)) * 0.28, t0 + j * beat / 2)
        # giai điệu thưa: 2-3 nốt mỗi ô nhịp
        for j in melody_seed.choice(np.arange(0, 8), size=3, replace=False):
            f = pent[melody_seed.integers(0, len(pent))]
            add_wrapped(tone(f, 0.9, decay=4.5, harm=(1.0, 0.25)) * 0.22, t0 + j * beat / 2)

    return normalize(buf, 0.6)


if __name__ == "__main__":
    write("sfx_place", sfx_place())
    write("sfx_pick", sfx_pick())
    write("sfx_click", sfx_click())
    write("sfx_clear", sfx_clear())
    write("sfx_combo", sfx_combo())
    write("sfx_gameover", sfx_gameover())
    write("sfx_levelup", sfx_levelup())
    write("bgm_main", bgm_main())
