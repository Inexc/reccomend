# -*- coding: utf-8 -*-
"""Рендер вертикального рекламного ролика MovieBook (1080x1920, 30 fps, 30 c)."""
import math
import os
import random
import subprocess
import sys

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_ad as M

W, H = M.W, M.H
FPS = M.FPS
DUR = M.DURATION


# ---------------------------------------------------------------------------
# Кэш теней и вспомогательные слои
# ---------------------------------------------------------------------------
@M.lru_cache(maxsize=32)
def shadow_layer(w, h, r, blur=34, alpha=170):
    pad = blur * 2
    layer = Image.new("RGBA", (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.rounded_rectangle((pad, pad, pad + w, pad + h), radius=r, fill=(0, 0, 0, alpha))
    return layer.filter(ImageFilter.GaussianBlur(blur))


def ui_layer(ui, focus, out_w, out_h, radius=22):
    fx, fy, fw, fh = focus
    fx = max(0, min(ui.width - 2, fx))
    fy = max(0, min(ui.height - 2, fy))
    fw = max(2, min(ui.width - fx, fw))
    fh = max(2, min(ui.height - fy, fh))
    crop = ui.crop((int(fx), int(fy), int(fx + fw), int(fy + fh)))
    crop = crop.resize((int(out_w), int(out_h)), Image.LANCZOS)
    crop = ImageEnhance.Brightness(crop).enhance(1.07)
    crop = ImageEnhance.Contrast(crop).enhance(1.04)
    layer = M.rounded_img(crop, radius)
    d = ImageDraw.Draw(layer)
    d.rounded_rectangle((0, 0, layer.width - 1, layer.height - 1), radius=radius,
                        outline=(*M.BORDER, 255), width=2)
    return layer


def paste_ui(frame, ui, focus, out=(0, 612, 1080, 655), radius=22, alpha=255, shadow=True):
    x, y, w, h = out
    layer = ui_layer(ui, focus, w, h, radius)
    if shadow:
        sh = shadow_layer(w, h, radius)
        frame.alpha_composite(sh, (int(x - sh.width // 2 + w // 2), int(y - sh.height // 2 + h // 2 + 18)))
    if alpha < 255:
        a = layer.getchannel("A").point(lambda v: int(v * alpha / 255))
        layer.putalpha(a)
    frame.alpha_composite(layer, (int(x), int(y)))


def ui_to_screen(ux, uy, focus, out=(0, 612, 1080, 655)):
    fx, fy, fw, fh = focus
    x, y, w, h = out
    return x + (ux - fx) * w / fw, y + (uy - fy) * h / fh


def interp_focus(t, keys):
    """keys: [(time, (x,y,w,h)), ...] с ease-in-out между кадрами."""
    if t <= keys[0][0]:
        return keys[0][1]
    for i in range(len(keys) - 1):
        t0, r0 = keys[i]
        t1, r1 = keys[i + 1]
        if t0 <= t <= t1:
            k = M.ease_in_out((t - t0) / max(1e-6, t1 - t0))
            return tuple(M.lerp(a, b, k) for a, b in zip(r0, r1))
    return keys[-1][1]


def add_flash(frame, alpha, color=(255, 255, 255)):
    if alpha <= 0:
        return
    overlay = Image.new("RGBA", frame.size, (*color, int(M.clamp(alpha, 0, 255))))
    frame.alpha_composite(overlay)


def add_fade(frame, alpha):
    if alpha <= 0:
        return
    overlay = Image.new("RGBA", frame.size, (0, 0, 0, int(M.clamp(alpha, 0, 255))))
    frame.alpha_composite(overlay)


def glitch(frame, amount=0, seed=0):
    if amount <= 0:
        return frame
    rng = random.Random(seed)
    rgb = frame.convert("RGB")
    arr = np.array(rgb)
    h = arr.shape[0]
    for _ in range(7):
        y0 = rng.randint(0, h - 40)
        hh = rng.randint(8, 55)
        dx = rng.randint(-amount, amount)
        arr[y0:y0 + hh] = np.roll(arr[y0:y0 + hh], dx, axis=1)
    out = Image.fromarray(arr, "RGB").convert("RGBA")
    # хроматическая аберрация
    r, g, b = out.split()[:3]
    r = ImageChops.offset(r, amount, 0)
    b = ImageChops.offset(b, -amount, 0)
    out = Image.merge("RGBA", (r, g, b, out.getchannel("A")))
    return out


# ---------------------------------------------------------------------------
# Текст
# ---------------------------------------------------------------------------
def big_text(frame, text, y, size, color, tracking=0, alpha=255, scale=1.0, x=W / 2, align="ma", shadow=True):
    f = M.F_BLACK(max(8, int(size * scale)))
    layer = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    if shadow:
        M.add_text(d, (x + 4, y + 7), text, f, (0, 0, 0, int(alpha * 0.55)), anchor=align, tracking=tracking)
    M.add_text(d, (x, y), text, f, (*color, int(alpha)), anchor=align, tracking=tracking)
    frame.alpha_composite(layer)


def caption(frame, text, y, size=44, color=M.TEXT, alpha=255, tracking=3, fnt=None, x=W / 2, align="ma"):
    f = fnt or M.F_SEMI(size)
    layer = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    M.add_text(d, (x, y), text, f, (*color, int(alpha)), anchor=align, tracking=tracking)
    frame.alpha_composite(layer)


def eyebrow(frame, text, y, color=M.GOLD, alpha=255, size=22, tracking=5, x=W / 2):
    caption(frame, text, y, size=size, color=color, alpha=alpha, tracking=tracking, fnt=M.F_SEMI(size), x=x)


# ---------------------------------------------------------------------------
# Логотип
# ---------------------------------------------------------------------------
def logo_mark(frame, cx, cy, size, alpha=255):
    mark = M.vgrad(size, size, ((0.0, (*M.ACCENT, 255)), (1.0, (150, 74, 82, 255))))
    mark = M.rounded_img(mark, int(size * 0.24))
    d = ImageDraw.Draw(mark)
    M.icon_film(d, size * 0.24, size * 0.3, size * 0.52, (255, 255, 255, 255))
    if alpha < 255:
        a = mark.getchannel("A").point(lambda v: int(v * alpha / 255))
        mark.putalpha(a)
    frame.alpha_composite(mark, (int(cx - size / 2), int(cy - size / 2)))


def logo_lockup(frame, t, y=620, alpha=255, scale=1.0, show_tag=True):
    # мягкое свечение
    glow = M.radial_glow(760, 760, M.ACCENT, 0.5, 0.5, 0.5, int(80 * alpha / 255))
    frame.alpha_composite(glow, (W // 2 - 380, y - 190))
    logo_mark(frame, W / 2, y, int(180 * scale), alpha)
    big_text(frame, "MOVIEBOOK", y + 128, int(112 * scale), M.TEXT, tracking=10, alpha=alpha, shadow=True)
    if show_tag:
        caption(frame, "ФИЛЬМЫ  +  КНИГИ", y + 268, size=int(24 * scale), color=M.GOLD, alpha=alpha, tracking=7)


# ---------------------------------------------------------------------------
# Фоновые сцены
# ---------------------------------------------------------------------------
def wall_background(frame, t, zoom=1.0, center=(1250, 1450), dark=150, blur=0, seed=0):
    cw, ch = W / zoom, H / zoom
    cx, cy = center
    x0 = int(M.clamp(cx - cw / 2, 0, M.POSTER_WALL.width - cw))
    y0 = int(M.clamp(cy - ch / 2, 0, M.POSTER_WALL.height - ch))
    crop = M.POSTER_WALL.crop((x0, y0, int(x0 + cw), int(y0 + ch))).resize((W, H), Image.LANCZOS)
    if blur:
        crop = crop.filter(ImageFilter.GaussianBlur(blur))
    frame.alpha_composite(crop)
    frame.alpha_composite(Image.new("RGBA", (W, H), (8, 6, 7, int(dark))))
    frame.alpha_composite(M.radial_glow(W, H, M.ACCENT, 0.7, 0.25, 0.5, 45))


def bokeh(frame, t, n=18, alpha=70):
    rng = random.Random(21)
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for i in range(n):
        x = rng.randint(0, W)
        y = rng.randint(0, H)
        r = rng.randint(8, 60)
        dx = math.sin(t * 0.6 + i) * 20
        dy = math.cos(t * 0.4 + i * 1.7) * 16
        col = M.GOLD if i % 3 == 0 else M.ACCENT
        d.ellipse((x + dx - r, y + dy - r, x + dx + r, y + dy + r), fill=(*col, int(alpha * 0.25)))
    layer = layer.filter(ImageFilter.GaussianBlur(12))
    frame.alpha_composite(layer)


def heart_particles(frame, t, t0, cx, cy, count=18, color=M.ACCENT, power=1.0):
    if t < t0:
        return
    age = t - t0
    if age > 1.6:
        return
    rng = random.Random(77)
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for i in range(count):
        ang = rng.uniform(-math.pi, 0.2 * math.pi)
        sp = rng.uniform(80, 360) * power
        x = cx + math.cos(ang) * sp * age
        y = cy + math.sin(ang) * sp * age + 180 * age * age
        s = rng.uniform(8, 26) * (1 - age / 1.6)
        if s <= 0:
            continue
        a = int(255 * (1 - age / 1.6))
        if i % 4 == 0:
            M.draw_star(d, x, y, s, (*M.GOLD, a))
        else:
            M.icon_heart(d, x, y, s, (*color, a))
    frame.alpha_composite(layer)


# ---------------------------------------------------------------------------
# Сцены
# ---------------------------------------------------------------------------
def scene_hook(frame, t):
    # 0.0-2.2 — постерная стена + кинетический текст
    z = 1.0 + 0.10 * t
    wall_background(frame, t, zoom=z, center=(1250 + t * 30, 1450 - t * 40), dark=118, blur=1.2)
    bokeh(frame, t, 14, 50)

    # пульсирующее сердце в центре
    pulse = 0.5 + 0.5 * math.sin(t * 8)
    glow = M.radial_glow(520, 520, M.ACCENT, 0.5, 0.5, 0.45, int(55 + 30 * pulse))
    frame.alpha_composite(glow, (W // 2 - 260, 700))

    t1 = 0.00
    if t >= t1:
        k = M.ease_out_back(M.clamp((t - t1) / 0.35))
        big_text(frame, "ОНО ЗНАЕТ", 560, 128, M.TEXT, tracking=2, alpha=255, scale=0.82 + 0.18 * k)
    t2 = 0.48
    if t >= t2:
        k = M.ease_out_back(M.clamp((t - t2) / 0.32))
        big_text(frame, "ЧТО ТЫ", 720, 128, M.TEXT, tracking=2, alpha=255, scale=0.82 + 0.18 * k)
    t3 = 0.90
    if t >= t3:
        k = M.ease_out_back(M.clamp((t - t3) / 0.32))
        big_text(frame, "ХОЧЕШЬ.", 880, 128, M.ACCENT, tracking=2, alpha=255, scale=0.82 + 0.18 * k)
    t4 = 1.38
    if t >= t4:
        a = M.smoothstep(t4, t4 + 0.25, t)
        caption(frame, "смотреть  ·  читать  ·  чувствовать", 1060, size=30, color=M.GOLD, alpha=int(255 * a), tracking=4)
    if t >= 1.55:
        a = M.smoothstep(1.55, 1.75, t)
        eyebrow(frame, "MOVIEBOOK", 1170, color=M.TEXT, alpha=int(210 * a), size=22, tracking=8)

    # вспышка/глитч на выходе
    if t > 1.72:
        glitch(frame, amount=int(8 * (1 - M.clamp((t - 1.72) / 0.35))), seed=int(t * 100))
    add_flash(frame, 90 * M.smoothstep(2.05, 2.18, t))


def scene_bridge(frame, t):
    # 2.2-4.4 — «меньше выбора — больше историй»
    frame.alpha_composite(Image.new("RGBA", (W, H), (10, 8, 9, 255)))
    # кинолента с постерами
    strip_y = 1280
    ids = ["m1", "m2", "m4", "b1", "b5", "m7", "m9", "b6"]
    shift = (t - 2.2) * 120
    for i, pid in enumerate(ids):
        x = int((i * 250 - shift) % (len(ids) * 250) - 250)
        p = M.poster(pid, 220, 320).convert("RGBA").rotate(-4, resample=Image.BICUBIC, expand=True)
        p = p.resize((220, 320), Image.LANCZOS)
        frame.alpha_composite(p, (x, strip_y))
    frame.alpha_composite(Image.new("RGBA", (W, H), (10, 8, 9, 135)))
    frame.alpha_composite(M.radial_glow(W, H, M.ACCENT, 0.5, 0.35, 0.6, 50))

    if t >= 2.25:
        k = M.ease_out_cubic(M.clamp((t - 2.25) / 0.45))
        big_text(frame, "МЕНЬШЕ", 480, 122, M.TEXT, tracking=2, alpha=int(255 * k), scale=0.92 + 0.08 * k)
        big_text(frame, "ВЫБОРА.", 620, 122, M.TEXT, tracking=2, alpha=int(255 * k), scale=0.92 + 0.08 * k)
    if t >= 2.95:
        k = M.ease_out_cubic(M.clamp((t - 2.95) / 0.45))
        big_text(frame, "БОЛЬШЕ", 800, 122, M.GOLD, tracking=2, alpha=int(255 * k), scale=0.92 + 0.08 * k)
        big_text(frame, "ИСТОРИЙ.", 940, 122, M.GOLD, tracking=2, alpha=int(255 * k), scale=0.92 + 0.08 * k)
    if t >= 3.55:
        a = M.smoothstep(3.55, 3.85, t)
        caption(frame, "MovieBook отбирает лучшее — тебе остаётся смотреть", 1090, size=27,
                color=M.MUTED, alpha=int(230 * a), tracking=1)
    add_flash(frame, 120 * M.smoothstep(4.25, 4.4, t))


def scene_logo(frame, t):
    # 4.4-7.2 — логотип и появление интерфейса
    frame.alpha_composite(Image.new("RGBA", (W, H), (10, 8, 9, 255)))
    frame.alpha_composite(M.radial_glow(W, H, M.ACCENT, 0.5, 0.35, 0.55, 70))
    frame.alpha_composite(M.radial_glow(W, H, M.GOLD, 0.5, 0.55, 0.55, 26))

    # логотип
    k = M.ease_out_back(M.clamp((t - 4.42) / 0.55), 1.9)
    a = int(255 * M.smoothstep(4.42, 4.7, t))
    logo_lockup(frame, t, y=350, alpha=a, scale=0.72 + 0.28 * k, show_tag=True)

    # интерфейс выезжает снизу
    if t >= 5.25:
        p = M.ease_out_cubic(M.clamp((t - 5.25) / 0.85))
        y = int(M.lerp(1560, 760, p))
        sc = 0.94 + 0.06 * p
        # интерфейс рисуем в отдельный слой и масштабируем
        out_w, out_h = int(1080 * sc), int(655 * sc)
        layer = ui_layer(M.HOME_BEFORE, (0, 0, 1800, 1092), out_w, out_h, 22)
        sh = shadow_layer(out_w, out_h, 22)
        frame.alpha_composite(sh, (int((W - out_w) / 2 - sh.width // 2 + out_w // 2), int(y - sh.height // 2 + out_h // 2 + 16)))
        frame.alpha_composite(layer, (int((W - out_w) / 2), y))
        # подпись
        if t >= 5.7:
            aa = M.smoothstep(5.7, 6.0, t)
            eyebrow(frame, "ЗНАЕТ, ЧТО ТЕБЕ ПОНРАВИТСЯ", 1420, color=M.GOLD, alpha=int(255 * aa), size=23, tracking=5)
            caption(frame, "твой личный рекомендатор", 1470, size=30, color=M.TEXT, alpha=int(220 * aa), tracking=1)


def scene_taste(frame, t):
    # 7.2-11.8 — лайк и обновление рекомендаций
    frame.alpha_composite(Image.new("RGBA", (W, H), (10, 8, 9, 255)))
    frame.alpha_composite(M.radial_glow(W, H, M.ACCENT, 0.5, 0.35, 0.6, 38))

    # большое сердце над интерфейсом — визуальный акцент на лайке
    if 7.25 < t < 8.45:
        a = M.smoothstep(7.25, 7.55, t) * (1 - M.smoothstep(8.2, 8.45, t))
        pulse = 1.0 + 0.06 * math.sin((t - 7.25) * 9)
        hs = int(118 * pulse)
        hx0, hy0 = W // 2 - hs // 2, 435 - hs // 2
        glow = M.radial_glow(360, 360, M.ACCENT, 0.5, 0.5, 0.5, int(80 * a))
        frame.alpha_composite(glow, (W // 2 - 180, 435 - 180))
        ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        dd = ImageDraw.Draw(ov)
        M.icon_heart(dd, hx0, hy0, hs, (*M.ACCENT, int(245 * a)))
        dd.ellipse((W // 2 - 95, 340, W // 2 + 95, 530), outline=(*M.GOLD, int(120 * a)), width=2)
        frame.alpha_composite(ov)

    full = (0, 0, 1800, 1092)
    heart_focus = (0, 0, 1800, 1092)
    focus = interp_focus(t, [
        (7.2, full), (8.55, full), (11.7, full),
    ])
    out = (0, 612, 1080, 655)

    # до/после лайка
    if t < 8.10:
        paste_ui(frame, M.HOME_BEFORE, focus, out)
    elif t < 8.22:
        k = M.smoothstep(8.10, 8.22, t)
        l1 = ui_layer(M.HOME_BEFORE, focus, out[2], out[3], 22)
        l2 = ui_layer(M.HOME_AFTER, focus, out[2], out[3], 22)
        blended = Image.blend(l1, l2, k)
        frame.alpha_composite(blended, (out[0], out[1]))
    else:
        paste_ui(frame, M.HOME_AFTER, focus, out)

    # короткая вспышка при лайке
    if 8.02 <= t <= 8.10:
        add_flash(frame, 60 * (1 - M.smoothstep(8.02, 8.10, t)), (255, 245, 235))

    # координата сердца на экране
    hx, hy = ui_to_screen(1696, 425, focus, out)

    # пульсация до нажатия
    if 7.5 < t < 8.05:
        p = 0.5 + 0.5 * math.sin((t - 7.5) * 14)
        r = 30 + 16 * p
        ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        dd = ImageDraw.Draw(ov)
        dd.ellipse((hx - r, hy - r, hx + r, hy + r), outline=(*M.ACCENT, int(200 * p)), width=4)
        frame.alpha_composite(ov)

    # тап
    if 8.02 <= t <= 8.55:
        age = t - 8.02
        ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        dd = ImageDraw.Draw(ov)
        for i in range(3):
            r = 26 + (age * 260) - i * 34
            if r > 0:
                a = int(190 * max(0, 1 - age / 0.55))
                dd.ellipse((hx - r, hy - r, hx + r, hy + r), outline=(*M.GOLD, a), width=4)
        frame.alpha_composite(ov)
        heart_particles(frame, t, 8.02, hx, hy, 22, M.ACCENT, 1.0)

    # подписи
    if t >= 7.35:
        a = int(255 * M.smoothstep(7.35, 7.65, t) * (1 - M.smoothstep(8.15, 8.35, t)))
        eyebrow(frame, "ОДИН ЛАЙК", 265, color=M.GOLD, alpha=a, size=25, tracking=6)
        caption(frame, "нажми на сердце", 315, size=27, color=M.MUTED, alpha=int(a * 0.9), tracking=1)
    if t >= 8.35:
        a = int(255 * M.smoothstep(8.35, 8.65, t))
        big_text(frame, "И АЛГОРИТМ", 330, 66, M.TEXT, tracking=1, alpha=a)
        big_text(frame, "ПОНЯЛ ТЕБЯ.", 405, 66, M.ACCENT, tracking=1, alpha=a)

    # профиль вкуса
    if t >= 8.8:
        panel_y = 1330
        a = int(255 * M.smoothstep(8.8, 9.15, t))
        ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        dd = ImageDraw.Draw(ov)
        dd.rounded_rectangle((90, panel_y, 990, panel_y + 330), radius=22, fill=(*M.CARD, 225),
                             outline=(*M.BORDER, 255), width=2)
        dd.text((125, panel_y + 22), "ТВОЙ ВКУС", font=M.F_SEMI(19), fill=(*M.GOLD, a), anchor="la")
        dd.text((865, panel_y + 22), "ОБНОВЛЁН", font=M.F_SEMI(15), fill=(*M.MUTED, a), anchor="la")
        bars = [("Фантастика", 0.86, M.ACCENT), ("Драма", 0.67, M.GOLD), ("Фэнтези", 0.42, M.BLUE)]
        for i, (label, val, col) in enumerate(bars):
            yy = panel_y + 78 + i * 76
            dd.text((125, yy), label, font=M.F_SEMI(23), fill=(*M.TEXT, a), anchor="la")
            dd.text((865, yy), f"{int(val * 100)}%", font=M.F_SEMI(21), fill=(*col, a), anchor="la")
            dd.rounded_rectangle((125, yy + 38, 865, yy + 54), radius=8, fill=(*M.BG, 255))
            k = M.ease_out_cubic(M.clamp((t - 8.95 - i * 0.12) / 0.6))
            dd.rounded_rectangle((125, yy + 38, 125 + int(740 * val * k), yy + 54), radius=8, fill=(*col, 255))
        frame.alpha_composite(ov)

    # тост
    if 8.25 < t < 9.1:
        a = int(255 * M.fade_window(t, 8.25, 9.1, 0.12, 0.25))
        aa = a / 255.0
        ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        dd = ImageDraw.Draw(ov)
        dd.rounded_rectangle((340, 520, 740, 584), radius=32, fill=(*M.CARD, int(235 * aa)), outline=(*M.ACCENT, int(220 * aa)), width=2)
        dd.text((W / 2, 552), "Добавлено в избранное", font=M.F_SEMI(24), fill=(*M.TEXT, a), anchor="mm")
        frame.alpha_composite(ov)


def scene_two_worlds(frame, t):
    # 11.8-15.6 — фильмы + книги, затем похожие
    frame.alpha_composite(Image.new("RGBA", (W, H), (10, 8, 9, 255)))
    frame.alpha_composite(M.radial_glow(W, H, M.ACCENT, 0.2, 0.25, 0.5, 40))
    frame.alpha_composite(M.radial_glow(W, H, M.BLUE, 0.8, 0.75, 0.5, 30))

    # верх — фильмы
    a1 = int(255 * M.smoothstep(11.82, 12.15, t))
    eyebrow(frame, "ФИЛЬМЫ", 170, color=M.ACCENT, alpha=a1, size=24, tracking=7, x=W / 2)
    for i, pid in enumerate(["m1", "m4", "m9"]):
        k = M.ease_out_cubic(M.clamp((t - 11.9 - i * 0.09) / 0.45))
        x = int(M.lerp(-380 + i * 340, 60 + i * 330, k))
        y = 240 + (i % 2) * 12
        p = M.poster(pid, 300, 420).convert("RGBA")
        frame.alpha_composite(M.rounded_img(p, 16), (x, y))
    # низ — книги
    a2 = int(255 * M.smoothstep(12.0, 12.3, t))
    eyebrow(frame, "КНИГИ", 1110, color=M.BLUE, alpha=a2, size=24, tracking=7, x=W / 2)
    for i, pid in enumerate(["b1", "b5", "b6"]):
        k = M.ease_out_cubic(M.clamp((t - 12.0 - i * 0.09) / 0.45))
        x = int(M.lerp(1120 - i * 330, 90 + i * 330, k))
        y = 1180 + (i % 2) * 12
        p = M.poster(pid, 300, 420).convert("RGBA")
        frame.alpha_composite(M.rounded_img(p, 16), (x, y))
    if t >= 12.9:
        aa = int(190 * M.smoothstep(12.9, 13.2, t))
        caption(frame, "фильмы + книги — в одном приложении", 1690, size=24, color=M.MUTED, alpha=aa, tracking=1)

    # центральная плашка
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dd = ImageDraw.Draw(ov)
    dd.rectangle((0, 790, W, 990), fill=(10, 8, 9, 225))
    dd.line((100, 806, 980, 806), fill=(*M.GOLD, 120), width=2)
    dd.line((100, 974, 980, 974), fill=(*M.GOLD, 120), width=2)
    frame.alpha_composite(ov)
    if t >= 12.35:
        k = M.ease_out_cubic(M.clamp((t - 12.35) / 0.4))
        big_text(frame, "ОДИН ВКУС.", 828, 64, M.TEXT, tracking=2, alpha=int(255 * k))
    if t >= 12.8:
        k = M.ease_out_cubic(M.clamp((t - 12.8) / 0.4))
        big_text(frame, "ДВА МИРА ИСТОРИЙ.", 900, 54, M.GOLD, tracking=1, alpha=int(255 * k))

    # переход к деталям
    if t >= 13.55:
        a = M.smoothstep(13.55, 13.8, t)
        frame.alpha_composite(Image.new("RGBA", (W, H), (10, 8, 9, int(255 * a))))
        focus = interp_focus(t, [(13.55, (270, 500, 1460, 886)), (15.5, (270, 540, 1460, 886))])
        paste_ui(frame, M.DETAIL, focus, (0, 612, 1080, 655), alpha=int(255 * a))
        if t >= 13.9:
            aa = int(255 * M.smoothstep(13.9, 14.2, t))
            eyebrow(frame, "ПОХОЖИЕ ИСТОРИИ", 280, color=M.GOLD, alpha=aa, size=24, tracking=6)
            big_text(frame, "В ОДИН КЛИК.", 325, 62, M.TEXT, tracking=1, alpha=aa)
            caption(frame, "жанр  ·  автор  ·  рейтинг  ·  настроение", 1380, size=27,
                    color=M.MUTED, alpha=int(aa * 0.85), tracking=1)


def scene_favorites(frame, t):
    # 15.6-19.2 — личная полка
    frame.alpha_composite(Image.new("RGBA", (W, H), (10, 8, 9, 255)))
    frame.alpha_composite(M.radial_glow(W, H, M.GOLD, 0.25, 0.3, 0.55, 35))
    focus = interp_focus(t, [
        (15.6, (0, 0, 1800, 1092)),
        (16.1, (280, 0, 1480, 898)),
        (16.8, (280, 0, 1480, 898)),
        (17.4, (300, 250, 1460, 886)),
        (19.0, (300, 250, 1460, 886)),
    ])
    paste_ui(frame, M.FAVORITES, focus, (0, 612, 1080, 655))
    # мягкое затухание у нижней кромки «окна», чтобы обрезка сетки выглядела аккуратно
    if t < 17.15:
        fade_h = 150
        grad = M.vgrad(1080, fade_h, ((0.0, (10, 8, 9, 0)), (1.0, (10, 8, 9, 255))))
        frame.alpha_composite(grad, (0, 1267 - fade_h))
    if t >= 15.75:
        a = int(255 * M.smoothstep(15.75, 16.05, t))
        eyebrow(frame, "ТВОЯ ЛИЧНАЯ ПОЛКА", 280, color=M.GOLD, alpha=a, size=24, tracking=6)
    if t >= 16.15:
        a = int(255 * M.smoothstep(16.15, 16.45, t))
        big_text(frame, "ВСЁ, ЧТО ХОЧЕШЬ", 330, 58, M.TEXT, tracking=1, alpha=a)
        big_text(frame, "СОХРАНИТЬ.", 400, 58, M.ACCENT, tracking=1, alpha=a)
    if t >= 16.6:
        aa = int(200 * M.smoothstep(16.6, 16.9, t))
        caption(frame, "твоя коллекция — всегда рядом", 1370, size=27, color=M.MUTED, alpha=aa, tracking=1)
    # сердечки
    for i, (x, y) in enumerate([(90, 700), (990, 760), (120, 1250), (950, 1300)]):
        if t > 16.4 + i * 0.18:
            age = (t - 16.4 - i * 0.18) % 2.2
            a = int(150 * max(0, 1 - age / 2.2))
            ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
            dd = ImageDraw.Draw(ov)
            M.icon_heart(dd, x, y - age * 35, 30, (*M.ACCENT, a))
            frame.alpha_composite(ov)
    # акцент на статистике
    if 16.0 < t < 16.8:
        a = int(150 * M.fade_window(t, 16.0, 16.8, 0.15, 0.25))
        x1, y1 = ui_to_screen(300, 140, focus, (0, 612, 1080, 655))
        x2, y2 = ui_to_screen(1760, 232, focus, (0, 612, 1080, 655))
        ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        dd = ImageDraw.Draw(ov)
        dd.line((x1 + 8, y1 - 6, x2 - 8, y1 - 6), fill=(*M.GOLD, a), width=3)
        dd.line((x1 + 8, y2 + 6, x2 - 8, y2 + 6), fill=(*M.GOLD, a), width=3)
        frame.alpha_composite(ov)


def scene_search(frame, t):
    # 19.2-22.4 — поиск
    frame.alpha_composite(Image.new("RGBA", (W, H), (10, 8, 9, 255)))
    frame.alpha_composite(M.radial_glow(W, H, M.BLUE, 0.7, 0.3, 0.55, 28))
    focus = interp_focus(t, [
        (19.2, (0, 0, 1800, 1092)),
        (19.45, (250, 90, 1368, 830)),
        (21.9, (250, 90, 1368, 830)),
        (22.2, (0, 0, 1800, 1092)),
    ])
    out = (0, 612, 1080, 655)

    query = "фантастика"
    n = int(M.clamp((t - 19.55) / 0.075, 0, len(query)))
    if t < 20.05:
        paste_ui(frame, M.SEARCH_EMPTY, focus, out)
        # текст запроса поверх пустого поля
        tx, ty = ui_to_screen(380 + 92, 96 + 48, focus, out)
        # координаты поля в UI: popup x=380, поле x=410..1390, текст x=472
        tx, ty = ui_to_screen(472, 144, focus, out)
        # масштаб шрифта относительно фокуса
        fsize = max(12, int(26 * out[2] / focus[2]))
        f = M.F_REG(fsize)
        d = ImageDraw.Draw(frame)
        shown = query[:n]
        d.text((tx, ty), shown, font=f, fill=(*M.TEXT, 255), anchor="la")
        tw = d.textlength(shown, font=f)
        if int(t * 3) % 2 == 0:
            d.line((tx + tw + 2, ty - 2, tx + tw + 2, ty + fsize + 8), fill=(*M.ACCENT, 255), width=2)
    else:
        paste_ui(frame, M.SEARCH_TYPED, focus, out)
        # курсор сразу после запроса
        tw_ui = M.F_REG(26).getlength(query)
        tx, ty = ui_to_screen(472 + tw_ui, 144, focus, out)
        if int(t * 3) % 2 == 0:
            fsize = max(12, int(26 * out[2] / focus[2]))
            d = ImageDraw.Draw(frame)
            d.line((tx + 3, ty - 2, tx + 3, ty + fsize + 8), fill=(*M.ACCENT, 255), width=2)
        if t > 20.05:
            add_flash(frame, 70 * (1 - M.smoothstep(20.05, 20.3, t)), (230, 240, 255))

    if t >= 19.4:
        a = int(255 * M.smoothstep(19.4, 19.7, t))
        eyebrow(frame, "ПОИСК ЗА СЕКУНДУ", 280, color=M.GOLD, alpha=a, size=24, tracking=6)
    if t >= 19.75:
        a = int(255 * M.smoothstep(19.75, 20.05, t))
        caption(frame, "фильмы  ·  книги  ·  жанры  ·  авторы", 330, size=28, color=M.TEXT, alpha=a, tracking=1)
    if 20.6 < t < 21.5:
        a = int(190 * M.fade_window(t, 20.6, 21.5, 0.15, 0.2))
        x1, y1 = ui_to_screen(380, 96, focus, out)
        x2, y2 = ui_to_screen(1420, 916, focus, out)
        ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        dd = ImageDraw.Draw(ov)
        dd.rounded_rectangle((x1 - 4, y1 - 4, x2 + 4, y2 + 4), radius=20, outline=(*M.GOLD, a), width=3)
        frame.alpha_composite(ov)
    if t >= 20.5:
        aa = int(200 * M.smoothstep(20.5, 20.8, t))
        caption(frame, "фильмы и книги — в одном поиске", 1370, size=27, color=M.MUTED, alpha=aa, tracking=1)
    # вспышка-«динь»
    if 21.0 < t < 21.3:
        add_flash(frame, 45 * (1 - M.smoothstep(21.0, 21.3, t)), (255, 245, 220))


def scene_features(frame, t):
    # 22.4-25.2 — быстрый монтаж возможностей
    frame.alpha_composite(Image.new("RGBA", (W, H), (10, 8, 9, 255)))
    z = 1.0 + 0.04 * (t - 22.4)
    wall_background(frame, t, zoom=z, center=(1300, 1500), dark=170, blur=6)
    eyebrow(frame, "MOVIEBOOK", 220, color=M.GOLD, alpha=210, size=20, tracking=8)
    caption(frame, "ВСЁ ДЛЯ ТВОЕГО ВЕЧЕРА", 258, size=22, color=M.MUTED, alpha=190, tracking=2)
    items = [
        (22.4, 23.2, "УМНЫЕ", "РЕКОМЕНДАЦИИ", "алгоритм, который понимает вкус", M.icon_sparkle, M.ACCENT),
        (23.2, 24.0, "ПОХОЖИЕ", "ИСТОРИИ", "фильмы и книги — в один клик", M.icon_link, M.GOLD),
        (24.0, 25.0, "ТЁМНАЯ", "ТЕМА", "и всё, что нужно для вечера", M.icon_gear, M.BLUE),
    ]
    for t0, t1, line1, line2, sub, icon_fn, col in items:
        if not (t0 <= t < t1):
            continue
        local = (t - t0) / (t1 - t0)
        k = M.ease_out_back(M.clamp((t - t0) / 0.28), 1.5)
        a = int(255 * M.smoothstep(t0, t0 + 0.12, t))
        # карточка
        ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        dd = ImageDraw.Draw(ov)
        dd.rounded_rectangle((80, 560, 1000, 1320), radius=34, fill=(*M.CARD, 235), outline=(*M.BORDER, 255), width=2)
        # иконка
        dd.ellipse((440, 640, 640, 840), fill=(*M.BG, 255), outline=(*col, 220), width=3)
        icon_fn(dd, 495, 695, 90, (*col, 255))
        dd.text((W / 2, 900), line1, font=M.F_BLACK(64), fill=(*M.TEXT, a), anchor="ma")
        dd.text((W / 2, 975), line2, font=M.F_BLACK(64), fill=(*col, a), anchor="ma")
        dd.text((W / 2, 1080), sub, font=M.F_REG(26), fill=(*M.MUTED, a), anchor="ma")
        dd.line((260, 1140, 820, 1140), fill=(*col, 120), width=2)
        frame.alpha_composite(ov)
        # индикатор прогресса
        for j in range(3):
            cx = W / 2 - 44 + j * 44
            active = (j == int((t - 22.4) / 0.9))
            dd2 = ImageDraw.Draw(frame)
            dd2.ellipse((cx - 5, 1500, cx + 5, 1510), fill=(*M.GOLD, 255) if active else (*M.FAINT, 255))
        # вспышка на входе
        add_flash(frame, 55 * (1 - M.smoothstep(t0, t0 + 0.18, t)), (255, 255, 255))
    # финальный райзер
    if t > 24.8:
        add_flash(frame, 150 * M.smoothstep(24.8, 25.2, t))


def scene_end(frame, t):
    # 25.2-30.0 — финальный экран
    frame.alpha_composite(Image.new("RGBA", (W, H), (10, 8, 9, 255)))
    z = 1.02 + 0.03 * (t - 25.2)
    wall_background(frame, t, zoom=z, center=(1250, 1450), dark=190, blur=20)
    bokeh(frame, t, 20, 60)
    frame.alpha_composite(M.radial_glow(W, H, M.ACCENT, 0.5, 0.32, 0.55, 55))
    frame.alpha_composite(M.radial_glow(W, H, M.GOLD, 0.5, 0.62, 0.55, 30))

    k = M.ease_out_back(M.clamp((t - 25.25) / 0.7), 1.7)
    a = int(255 * M.smoothstep(25.25, 25.55, t))
    logo_mark(frame, W / 2, 560, int(180 * (0.7 + 0.3 * k)), a)
    big_text(frame, "MOVIEBOOK", 650, 112, M.TEXT, tracking=12, alpha=a, scale=0.9 + 0.1 * k)
    caption(frame, "ФИЛЬМЫ  +  КНИГИ", 790, size=24, color=M.GOLD, alpha=a, tracking=7)

    if t >= 25.7:
        aa = int(255 * M.smoothstep(25.7, 26.0, t))
        ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        dd = ImageDraw.Draw(ov)
        dd.line((200, 880, 880, 880), fill=(*M.GOLD, 120), width=2)
        frame.alpha_composite(ov)
        big_text(frame, "ТВОЙ СЛЕДУЮЩИЙ", 940, 52, M.TEXT, tracking=1, alpha=aa)
        big_text(frame, "ЛЮБИМЫЙ ФИЛЬМ", 1005, 52, M.TEXT, tracking=1, alpha=aa)
        big_text(frame, "УЖЕ ЗДЕСЬ.", 1070, 52, M.ACCENT, tracking=1, alpha=aa)
    if t >= 26.5:
        aa = int(255 * M.smoothstep(26.5, 26.8, t))
        caption(frame, "РЕКОМЕНДАЦИИ ФИЛЬМОВ И КНИГ", 1180, size=25, color=M.GOLD, alpha=aa, tracking=5)

    # CTA
    if t >= 26.9:
        aa = int(255 * M.smoothstep(26.9, 27.2, t))
        pulse = 1.0 + 0.02 * math.sin((t - 26.9) * 5)
        bw, bh = int(540 * pulse), int(100 * pulse)
        bx, by = (W - bw) // 2, int(1300 - (bh - 100) / 2)
        ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        dd = ImageDraw.Draw(ov)
        dd.rounded_rectangle((bx, by, bx + bw, by + bh), radius=50, fill=(*M.ACCENT, 245))
        dd.text((W / 2, by + bh / 2), "ПОПРОБОВАТЬ", font=M.F_SEMI(34), fill=(*M.WHITE, aa), anchor="mm")
        frame.alpha_composite(ov)
        if t > 27.0:
            glow = M.radial_glow(700, 260, M.ACCENT, 0.5, 0.5, 0.55, int(50 + 25 * math.sin((t - 27) * 4)))
            frame.alpha_composite(glow, (W // 2 - 350, 1280))
    if t >= 27.6:
        aa = int(180 * M.smoothstep(27.6, 27.9, t))
        caption(frame, "ссылка в описании", 1450, size=22, color=M.MUTED, alpha=aa, tracking=1)

    # финальное затемнение
    add_fade(frame, 255 * M.smoothstep(29.35, 29.95, t))


# ---------------------------------------------------------------------------
# Кадр
# ---------------------------------------------------------------------------
def render_frame(t):
    frame = M.BASE.copy()
    if t < 2.2:
        scene_hook(frame, t)
    elif t < 4.4:
        scene_bridge(frame, t)
    elif t < 7.2:
        scene_logo(frame, t)
    elif t < 11.8:
        scene_taste(frame, t)
    elif t < 15.6:
        scene_two_worlds(frame, t)
    elif t < 19.2:
        scene_favorites(frame, t)
    elif t < 22.4:
        scene_search(frame, t)
    elif t < 25.2:
        scene_features(frame, t)
    else:
        scene_end(frame, t)

    # глобальный грейд и зерно
    frame.alpha_composite(M.GRAIN[int(t * FPS) % len(M.GRAIN)])
    frame.alpha_composite(M.VIGNETTE)
    return frame.convert("RGB")


def save_previews(times):
    for t in times:
        img = render_frame(t)
        path = os.path.join(M.PREVIEW, f"frame_{t:05.2f}.png")
        img.save(path)
        print("preview", path, flush=True)


def render_video(seconds=None, name="MovieBook_ad_1080x1920.mp4"):
    audio = os.path.join(M.OUT, "soundtrack.wav")
    out = os.path.join(M.OUT, name)
    total = int((seconds if seconds else DUR) * FPS)
    cmd = [
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "warning",
        "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
        "-i", audio,
        "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p",
        "-profile:v", "high", "-level", "4.1",
        "-c:a", "aac", "-b:a", "192k", "-ar", "48000",
        "-af", "loudnorm=I=-14:TP=-1.0:LRA=11",
        "-shortest", "-movflags", "+faststart",
        out,
    ]
    print("encoding", out, flush=True)
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for i in range(total):
        t = i / FPS
        frame = render_frame(t)
        proc.stdin.write(frame.tobytes())
        if i % 30 == 0:
            print(f"frame {i}/{total}  t={t:.2f}", flush=True)
    proc.stdin.close()
    proc.wait()
    if proc.returncode != 0:
        raise SystemExit(f"ffmpeg failed: {proc.returncode}")
    print("done", out, flush=True)
    # постер-кадр
    thumb = render_frame(28.0)
    thumb.save(os.path.join(M.OUT, "thumbnail.png"))
    print("thumbnail saved", flush=True)


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "preview":
        save_previews([0.2, 1.0, 1.8, 3.2, 4.8, 5.8, 6.6, 7.9, 8.2, 9.8, 12.5, 14.6, 16.4, 17.6, 20.3, 21.2, 23.0, 24.5, 26.0, 28.0])
    elif len(sys.argv) > 1 and sys.argv[1] == "test":
        render_video(seconds=2.0, name="test.mp4")
    else:
        render_video()
