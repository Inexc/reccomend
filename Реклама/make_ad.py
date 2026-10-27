# -*- coding: utf-8 -*-
"""
MovieBook — вертикальный рекламный ролик 1080x1920, 30 fps, 30 секунд.
Скрипт только читает файлы приложения (постеры и Data/*.json) и создаёт
готовые кадры/видео в папке "Реклама". Код приложения не изменяется.
"""
import json
import math
import os
import random
import subprocess
import sys
from functools import lru_cache

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps

# ----------------------------------------------------------------------------
# Пути и константы
# ----------------------------------------------------------------------------
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ASSETS = os.path.join(ROOT, "Assets")
DATA = os.path.join(ROOT, "Data")
OUT = os.path.join(HERE, "output")
PREVIEW = os.path.join(HERE, "frames")
os.makedirs(OUT, exist_ok=True)
os.makedirs(PREVIEW, exist_ok=True)

W, H = 1080, 1920
FPS = 30
DURATION = 30.0

# Палитра приложения (Styles/DarkTheme.xaml)
BG = (22, 17, 20)
BG_DEEP = (11, 9, 10)
BG_SOFT = (28, 22, 25)
CARD = (34, 26, 30)
CARD_HI = (42, 32, 38)
SIDEBAR = (20, 16, 19)
BORDER = (51, 42, 44)
ACCENT = (193, 102, 107)      # #C1666B
ACCENT_DARK = (140, 64, 70)
GOLD = (212, 180, 131)        # #D4B483
TEXT = (242, 233, 228)        # #F2E9E4
MUTED = (166, 150, 143)       # #A6968F
FAINT = (120, 105, 100)
BLUE = (91, 141, 239)
WHITE = (255, 255, 255)
BLACK = (0, 0, 0)

FONT_DIR = r"C:\Windows\Fonts"


@lru_cache(maxsize=256)
def font(name, size):
    return ImageFont.truetype(os.path.join(FONT_DIR, name), size)


def F_BLACK(s): return font("seguibl.ttf", s)
def F_BOLD(s): return font("segoeuib.ttf", s)
def F_SEMI(s): return font("seguisb.ttf", s)
def F_REG(s): return font("segoeui.ttf", s)
def F_LIGHT(s): return font("segoeuil.ttf", s)
def F_SYM(s): return font("seguisym.ttf", s)
def F_SERIF(s): return font("georgiab.ttf", s)


def clamp(v, lo=0.0, hi=1.0):
    return max(lo, min(hi, v))


def lerp(a, b, t):
    return a + (b - a) * t


def ease_out_cubic(t):
    t = clamp(t)
    return 1 - (1 - t) ** 3


def ease_in_cubic(t):
    t = clamp(t)
    return t ** 3


def ease_in_out(t):
    t = clamp(t)
    return t * t * (3 - 2 * t)


def ease_out_back(t, s=1.7):
    t = clamp(t) - 1
    return 1 + t * t * ((s + 1) * t + s)


def smoothstep(a, b, t):
    if a == b:
        return 1.0 if t >= b else 0.0
    x = clamp((t - a) / (b - a))
    return x * x * (3 - 2 * x)


def fade_window(t, start, end, fade_in=0.25, fade_out=0.25):
    if t < start or t > end:
        return 0.0
    a = smoothstep(start, start + fade_in, t) if fade_in > 0 else 1.0
    b = 1.0 - smoothstep(end - fade_out, end, t) if fade_out > 0 else 1.0
    return min(a, b)


# ----------------------------------------------------------------------------
# Градиенты, маски, тени
# ----------------------------------------------------------------------------
@lru_cache(maxsize=128)
def vgrad(w, h, stops):
    """stops: ((pos, (r,g,b,a)), ...)"""
    stops = tuple(stops)
    arr = np.zeros((h, w, 4), dtype=np.float32)
    ys = np.linspace(0, 1, h, dtype=np.float32)
    for i in range(len(stops) - 1):
        p0, c0 = stops[i]
        p1, c1 = stops[i + 1]
        m = (ys >= p0) & (ys <= p1)
        if not m.any():
            continue
        k = (ys[m] - p0) / max(1e-6, p1 - p0)
        for ch in range(4):
            arr[m, :, ch] = c0[ch] + (c1[ch] - c0[ch]) * k[:, None]
    arr[: int(h * stops[0][0])] = stops[0][1]
    arr[int(h * stops[-1][0]):] = stops[-1][1]
    return Image.fromarray(arr.astype(np.uint8), "RGBA")


@lru_cache(maxsize=128)
def rounded_mask(w, h, r):
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, w - 1, h - 1), radius=r, fill=255)
    return m


def paste_rounded(base, img, xy, r=18, shadow=False, shadow_blur=26, shadow_offset=(0, 14), shadow_alpha=150):
    x, y = int(xy[0]), int(xy[1])
    img = img.convert("RGBA")
    if img.size != (base.width, base.height):
        pass
    mask = rounded_mask(img.width, img.height, r)
    if shadow:
        sil = Image.new("RGBA", img.size, (0, 0, 0, 0))
        sil.paste((0, 0, 0, shadow_alpha), (0, 0), mask)
        sil = sil.filter(ImageFilter.GaussianBlur(shadow_blur))
        base.alpha_composite(sil, (x + shadow_offset[0], y + shadow_offset[1]))
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    layer.paste(img, (0, 0), mask)
    base.alpha_composite(layer, (x, y))


def fit_cover(img, w, h):
    return ImageOps.fit(img.convert("RGB"), (w, h), Image.LANCZOS, centering=(0.5, 0.45))


def rounded_img(img, r):
    img = img.convert("RGBA")
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    out.paste(img, (0, 0), rounded_mask(img.width, img.height, r))
    return out


def radial_glow(w, h, color, cx=0.5, cy=0.5, radius=0.55, strength=255):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    nx = (xx / max(1, w - 1) - cx) / radius
    ny = (yy / max(1, h - 1) - cy) / radius
    d = np.sqrt(nx * nx + ny * ny)
    a = np.clip(1 - d, 0, 1) ** 2 * strength
    arr = np.zeros((h, w, 4), dtype=np.uint8)
    arr[..., 0] = color[0]
    arr[..., 1] = color[1]
    arr[..., 2] = color[2]
    arr[..., 3] = a.astype(np.uint8)
    return Image.fromarray(arr, "RGBA")


def add_text(draw, xy, text, fnt, fill, anchor="la", tracking=0):
    """Текст с трекингом. anchor: la / ma / ra (по горизонтали, верх)."""
    if tracking == 0:
        draw.text(xy, text, font=fnt, fill=fill, anchor=anchor)
        return
    widths = [draw.textlength(ch, font=fnt) for ch in text]
    total = sum(widths) + tracking * max(0, len(text) - 1)
    x, y = xy
    if anchor[0] == "m":
        x -= total / 2
    elif anchor[0] == "r":
        x -= total
    for ch, w in zip(text, widths):
        draw.text((x, y), ch, font=fnt, fill=fill, anchor="la")
        x += w + tracking


def text_width(draw, text, fnt, tracking=0):
    if tracking == 0:
        return draw.textlength(text, font=fnt)
    return sum(draw.textlength(ch, font=fnt) for ch in text) + tracking * max(0, len(text) - 1)


def wrap_text(draw, text, fnt, max_w):
    words = text.split()
    lines, cur = [], ""
    for w in words:
        test = (cur + " " + w).strip()
        if draw.textlength(test, font=fnt) <= max_w or not cur:
            cur = test
        else:
            lines.append(cur)
            cur = w
    if cur:
        lines.append(cur)
    return lines


def draw_para(draw, xy, text, fnt, fill, max_w, line_gap=6):
    x, y = xy
    for line in wrap_text(draw, text, fnt, max_w):
        draw.text((x, y), line, font=fnt, fill=fill, anchor="la")
        y += fnt.size + line_gap
    return y


# ----------------------------------------------------------------------------
# Данные и постеры
# ----------------------------------------------------------------------------
def load_json(name):
    with open(os.path.join(DATA, name), encoding="utf-8") as f:
        return json.load(f)


MOVIES = load_json("movies.json")
BOOKS = load_json("books.json")
for m in MOVIES:
    m["kind"] = "movie"
for b in BOOKS:
    b["kind"] = "book"
ALL = MOVIES + BOOKS
BY_ID = {x["id"]: x for x in ALL}


@lru_cache(maxsize=64)
def poster(item_id, w=600, h=900):
    item = BY_ID[item_id]
    path = os.path.join(ROOT, item["poster"].replace("/", os.sep))
    img = Image.open(path).convert("RGB")
    return fit_cover(img, w, h)


# ----------------------------------------------------------------------------
# Фоновые слои
# ----------------------------------------------------------------------------
def make_base():
    base = vgrad(W, H, (
        (0.0, (18, 13, 16, 255)),
        (0.45, (12, 9, 11, 255)),
        (1.0, (7, 6, 7, 255)),
    ))
    base.alpha_composite(radial_glow(W, H, ACCENT, 0.78, 0.12, 0.55, 38))
    base.alpha_composite(radial_glow(W, H, GOLD, 0.18, 0.88, 0.55, 24))
    return base


BASE = make_base()


def make_vignette():
    arr = np.zeros((H, W, 4), dtype=np.uint8)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    nx = (xx / (W - 1) - 0.5) * 1.05
    ny = (yy / (H - 1) - 0.5) * 0.95
    d = np.sqrt(nx * nx + ny * ny) / 0.72
    a = np.clip((d - 0.45) / 0.55, 0, 1) ** 1.6 * 210
    arr[..., 3] = a.astype(np.uint8)
    return Image.fromarray(arr, "RGBA")


VIGNETTE = make_vignette()


def make_grain(seed=3, count=6):
    rng = np.random.default_rng(seed)
    out = []
    for _ in range(count):
        n = rng.normal(0, 1, (H, W)).astype(np.float32)
        n = (n * 16 + 128).clip(0, 255).astype(np.uint8)
        rgb = np.stack([n, n, n], axis=-1)
        alpha = np.full((H, W, 1), 13, dtype=np.uint8)
        out.append(Image.fromarray(np.concatenate([rgb, alpha], axis=-1), "RGBA"))
    return out


GRAIN = make_grain()


# ----------------------------------------------------------------------------
# Постерная стена
# ----------------------------------------------------------------------------
def make_poster_wall(seed=11, w=2500, h=3000):
    rng = random.Random(seed)
    wall = Image.new("RGBA", (w, h), (10, 8, 9, 255))
    ids = ["m1", "m2", "m4", "m5", "m7", "m8", "m9", "m10",
           "b1", "b2", "b5", "b6", "b4", "b10"]
    cols, rows = 6, 7
    cw, ch = 380, 560
    for r in range(rows):
        for c in range(cols):
            item = rng.choice(ids)
            p = poster(item, cw, ch).convert("RGBA")
            ang = rng.uniform(-9, 9)
            p = p.rotate(ang, resample=Image.BICUBIC, expand=True)
            x = int(c * (cw - 18) + rng.randint(-30, 30) - 40)
            y = int(r * (ch - 35) + rng.randint(-30, 30) - 60)
            wall.alpha_composite(p, (x, y))
    # притемняем и добавляем цветовой сдвиг
    dark = Image.new("RGBA", wall.size, (8, 6, 7, 150))
    wall.alpha_composite(dark)
    wall.alpha_composite(radial_glow(w, h, ACCENT, 0.55, 0.42, 0.55, 55))
    return wall


POSTER_WALL = make_poster_wall()


# ----------------------------------------------------------------------------
# Иконки
# ----------------------------------------------------------------------------
def icon_film(d, x, y, s, color):
    x, y, s = int(x), int(y), int(s)
    d.rounded_rectangle((x, y, x + s, y + s * 0.78), radius=s * 0.09, outline=color, width=max(2, s // 14))
    for i in range(3):
        cx = x + s * (0.18 + i * 0.3)
        d.ellipse((cx - s * 0.055, y + s * 0.12, cx + s * 0.055, y + s * 0.23), fill=color)
        d.ellipse((cx - s * 0.055, y + s * 0.55, cx + s * 0.055, y + s * 0.66), fill=color)


def icon_book(d, x, y, s, color):
    x, y, s = int(x), int(y), int(s)
    d.rounded_rectangle((x + s * 0.12, y + s * 0.1, x + s * 0.5, y + s * 0.9), radius=s * 0.06, outline=color, width=max(2, s // 14))
    d.rounded_rectangle((x + s * 0.5, y + s * 0.1, x + s * 0.88, y + s * 0.9), radius=s * 0.06, outline=color, width=max(2, s // 14))
    d.line((x + s * 0.5, y + s * 0.12, x + s * 0.5, y + s * 0.9), fill=color, width=max(2, s // 16))


def icon_search(d, x, y, s, color):
    x, y, s = int(x), int(y), int(s)
    w = max(2, s // 10)
    d.ellipse((x + s * 0.08, y + s * 0.08, x + s * 0.68, y + s * 0.68), outline=color, width=w)
    d.line((x + s * 0.6, y + s * 0.6, x + s * 0.92, y + s * 0.92), fill=color, width=w)


def icon_heart(d, x, y, s, color, fill=None):
    x, y, s = int(x), int(y), int(s)
    # аппроксимация сердца из двух кругов и треугольника
    r = s * 0.27
    cy = y + s * 0.33
    d.ellipse((x + s * 0.05, cy - r, x + s * 0.05 + 2 * r, cy + r), fill=fill or color)
    d.ellipse((x + s * 0.95 - 2 * r, cy - r, x + s * 0.95, cy + r), fill=fill or color)
    d.polygon([(x + s * 0.03, cy + r * 0.15), (x + s * 0.97, cy + r * 0.15), (x + s * 0.5, y + s * 0.95)], fill=fill or color)


def icon_home(d, x, y, s, color):
    x, y, s = int(x), int(y), int(s)
    w = max(2, s // 12)
    d.line((x + s * 0.1, y + s * 0.48, x + s * 0.5, y + s * 0.12), fill=color, width=w)
    d.line((x + s * 0.5, y + s * 0.12, x + s * 0.9, y + s * 0.48), fill=color, width=w)
    d.line((x + s * 0.18, y + s * 0.42, x + s * 0.18, y + s * 0.9), fill=color, width=w)
    d.line((x + s * 0.82, y + s * 0.42, x + s * 0.82, y + s * 0.9), fill=color, width=w)
    d.line((x + s * 0.18, y + s * 0.9, x + s * 0.82, y + s * 0.9), fill=color, width=w)


def icon_gear(d, x, y, s, color):
    x, y, s = int(x), int(y), int(s)
    w = max(2, s // 12)
    d.ellipse((x + s * 0.32, y + s * 0.32, x + s * 0.68, y + s * 0.68), outline=color, width=w)
    for i in range(8):
        a = math.radians(i * 45)
        x1 = x + s * 0.5 + math.cos(a) * s * 0.32
        y1 = y + s * 0.5 + math.sin(a) * s * 0.32
        x2 = x + s * 0.5 + math.cos(a) * s * 0.44
        y2 = y + s * 0.5 + math.sin(a) * s * 0.44
        d.line((x1, y1, x2, y2), fill=color, width=w)


def icon_sparkle(d, x, y, s, color):
    x, y, s = int(x), int(y), int(s)
    cx, cy = x + s / 2, y + s / 2
    d.polygon([(cx, y), (cx + s * 0.12, cy - s * 0.12), (x + s, cy),
               (cx + s * 0.12, cy + s * 0.12), (cx, y + s),
               (cx - s * 0.12, cy + s * 0.12), (x, cy),
               (cx - s * 0.12, cy - s * 0.12)], fill=color)


def icon_link(d, x, y, s, color):
    x, y, s = int(x), int(y), int(s)
    w = max(2, s // 13)
    d.rounded_rectangle((x + s * 0.08, y + s * 0.34, x + s * 0.55, y + s * 0.64), radius=s * 0.15, outline=color, width=w)
    d.rounded_rectangle((x + s * 0.45, y + s * 0.36, x + s * 0.92, y + s * 0.66), radius=s * 0.15, outline=color, width=w)
    d.line((x + s * 0.3, y + s * 0.5, x + s * 0.7, y + s * 0.5), fill=color, width=w)


def icon_play(d, x, y, s, color):
    x, y, s = int(x), int(y), int(s)
    d.polygon([(x + s * 0.28, y + s * 0.14), (x + s * 0.28, y + s * 0.86), (x + s * 0.86, y + s * 0.5)], fill=color)


def draw_star(d, x, y, s, color):
    x, y, s = int(x), int(y), int(s)
    cx, cy = x + s / 2, y + s / 2
    pts = []
    for i in range(10):
        a = math.radians(-90 + i * 36)
        r = s * 0.5 if i % 2 == 0 else s * 0.22
        pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    d.polygon(pts, fill=color)


def rating_text(d, x, y, rating, fnt, color, star_color=None, star_s=None):
    star_s = star_s or int(fnt.size * 0.78)
    draw_star(d, x, y + int(fnt.size * 0.12), star_s, star_color or color)
    d.text((x + star_s + 6, y), f"{rating}", font=fnt, fill=color, anchor="la")
    return x + star_s + 6 + d.textlength(f"{rating}", font=fnt)


# ----------------------------------------------------------------------------
# UI: общие элементы
# ----------------------------------------------------------------------------
UI_W, UI_H = 1800, 1092
UI_R = 26


def ui_canvas():
    img = Image.new("RGBA", (UI_W, UI_H), (*BG, 255))
    d = ImageDraw.Draw(img)
    return img, d


def ui_sidebar(img, d, active="home"):
    d.rectangle((0, 0, 260, UI_H), fill=(*SIDEBAR, 255))
    # логотип
    logo = vgrad(54, 54, ((0, (*ACCENT, 255)), (1, (156, 82, 90, 255))))
    img.alpha_composite(rounded_img(logo, 14), (30, 34))
    icon_film(d, 43, 47, 28, (255, 255, 255, 255))
    d.text((30, 102), "Рекомендации", font=F_SEMI(23), fill=(*TEXT, 255), anchor="la")
    # навигация
    items = [("home", "Главная"), ("movies", "Фильмы"), ("books", "Книги"),
             ("favorites", "Избранное"), ("settings", "Настройки")]
    y = 176
    for key, label in items:
        if key == active:
            d.rounded_rectangle((18, y - 9, 242, y + 45), radius=10, fill=(*CARD, 255))
            d.rounded_rectangle((18, y + 4, 21, y + 32), radius=2, fill=(*WHITE, 255))
        col = (*TEXT, 255) if key == active else (*MUTED, 255)
        if key == "home":
            icon_home(d, 34, y + 2, 28, col)
        elif key == "movies":
            icon_film(d, 34, y + 4, 28, col)
        elif key == "books":
            icon_book(d, 34, y + 2, 28, col)
        elif key == "favorites":
            icon_heart(d, 34, y + 2, 28, col)
        else:
            icon_gear(d, 34, y + 2, 28, col)
        d.text((82, y + 4), label, font=F_SEMI(21), fill=col, anchor="la")
        y += 58
    d.text((30, UI_H - 78), "Хорошие истории", font=F_REG(18), fill=(*FAINT, 255), anchor="la")
    d.text((30, UI_H - 52), "всегда рядом", font=F_REG(18), fill=(*FAINT, 255), anchor="la")


def ui_search_bar(img, d, x, y, w=760, h=56, text="Поиск фильмов и книг..."):
    d.rounded_rectangle((x, y, x + w, y + h), radius=10, fill=(*CARD, 255), outline=(*BORDER, 255), width=2)
    icon_search(d, x + 18, y + 14, 28, (*FAINT, 255))
    d.text((x + 58, y + 15), text, font=F_REG(21), fill=(*FAINT, 255), anchor="la")


def ui_badge(d, x, y, text, fg=GOLD, bg=(28, 22, 25, 255), border=BORDER):
    f = F_SEMI(17)
    tw = d.textlength(text, font=f)
    d.rounded_rectangle((x, y, x + tw + 28, y + 30), radius=7, fill=bg, outline=(*border, 255), width=1)
    icon_sparkle(d, x + 9, y + 7, 15, (*fg, 255))
    d.text((x + 30, y + 6), text, font=f, fill=(*fg, 255), anchor="la")


def ui_button(d, x, y, w, h, text, primary=True, icon=None):
    if primary:
        d.rounded_rectangle((x, y, x + w, y + h), radius=8, fill=(*ACCENT, 255))
        col = (*WHITE, 255)
    else:
        d.rounded_rectangle((x, y, x + w, y + h), radius=8, fill=(*CARD, 255), outline=(*BORDER, 255), width=2)
        col = (*TEXT, 255)
    if icon == "play":
        icon_play(d, x + 18, y + h * 0.24, h * 0.52, col)
        tx = x + 46
    elif icon == "heart":
        icon_heart(d, x + 18, y + h * 0.24, h * 0.52, col)
        tx = x + 46
    else:
        tx = x + w / 2
    d.text((tx, y + h / 2), text, font=F_SEMI(int(h * 0.42)), fill=col, anchor="lm" if icon else "mm")


def ui_poster_card(img, d, item, x, y, w=300, h=320, title_size=19, show_meta=True):
    p = poster(item["id"], w * 2, int(h * 2)).resize((w, h), Image.LANCZOS)
    card = p.convert("RGBA")
    # градиент снизу
    gr = vgrad(w, h, ((0.0, (0, 0, 0, 0)), (0.62, (0, 0, 0, 20)), (1.0, (0, 0, 0, 190))))
    card.alpha_composite(gr)
    # тип
    kind = "ФИЛЬМ" if item["kind"] == "movie" else "КНИГА"
    d2 = ImageDraw.Draw(card)
    # сердце
    d2.ellipse((w - 44, 12, w - 12, 44), fill=(0, 0, 0, 130))
    icon_heart(d2, w - 36, 20, 18, (*WHITE, 255))
    img.alpha_composite(rounded_img(card, 14), (x, y))
    # подпись под постером (как в приложении)
    title = item["title"]
    lines = wrap_text(d, title, F_SEMI(title_size), w - 4)
    ty = y + h + 8
    for ln in lines[:2]:
        d.text((x + 2, ty), ln, font=F_SEMI(title_size), fill=(*TEXT, 255), anchor="la")
        ty += title_size + 3
    if show_meta:
        d.text((x + 2, ty + 1), item["author"], font=F_REG(14), fill=(*MUTED, 255), anchor="la")
        rating_text(d, x + 2, ty + 22, item["rating"], F_SEMI(14), (*GOLD, 255))
        d.text((x + 66, ty + 22), f"·  {item['year']}", font=F_REG(14), fill=(*MUTED, 255), anchor="la")


def ui_hero(img, d, item, x, y, w, h, heart_filled=False, heart_glow=0):
    # фон
    bgp = poster(item["id"], w, h).filter(ImageFilter.GaussianBlur(14))
    bgp = ImageEnhance_dark(bgp, 0.35)
    img.alpha_composite(rounded_img(bgp, 18), (x, y))
    d.rounded_rectangle((x, y, x + w, y + h), radius=18, fill=(*CARD, 235), outline=(*BORDER, 255), width=2)
    # постер
    pw, ph = 220, 310
    px, py = x + w - pw - 34, y + (h - ph) // 2
    p = poster(item["id"], pw * 2, ph * 2).resize((pw, ph), Image.LANCZOS)
    img.alpha_composite(rounded_img(p, 12), (px, py))
    d.rounded_rectangle((px, py, px + pw, py + ph), radius=12, outline=(*BORDER, 255), width=2)
    # сердце на постере
    hx, hy = px + pw - 52, py + ph - 52
    if heart_glow > 0:
        glow = radial_glow(120, 120, ACCENT, 0.5, 0.5, 0.5, int(180 * heart_glow))
        img.alpha_composite(glow, (hx - 30, hy - 30))
    d.ellipse((hx, hy, hx + 44, hy + 44), fill=(0, 0, 0, 165),
              outline=(*ACCENT, 255) if heart_filled else (*BORDER, 255), width=2)
    icon_heart(d, hx + 11, hy + 11, 22, (*ACCENT, 255) if heart_filled else (*WHITE, 255))
    # левая часть
    tx = x + 34
    ui_badge(d, tx, y + 28, "РЕКОМЕНДУЕМ")
    d.text((tx, y + 76), item["title"], font=F_BLACK(52), fill=(*TEXT, 255), anchor="la")
    meta_f = F_SEMI(19)
    mx = tx
    d.text((mx, y + 142), ", ".join(item["genre"]), font=meta_f, fill=(*GOLD, 255), anchor="la")
    mx += d.textlength(", ".join(item["genre"]), font=meta_f) + 22
    d.text((mx, y + 142), "·", font=meta_f, fill=(*MUTED, 255), anchor="la")
    mx += 22
    mx = rating_text(d, mx, y + 142, item["rating"], meta_f, (*GOLD, 255))
    d.text((mx + 18, y + 142), f"·   {item['year']}", font=meta_f, fill=(*MUTED, 255), anchor="la")
    d.text((tx, y + 172), item["author"], font=F_REG(18), fill=(*MUTED, 255), anchor="la")
    draw_para(d, (tx, y + 210), item["description"], F_REG(18), (*TEXT, 210), 620, 5)
    ui_button(d, tx, y + h - 62, 210, 42, "Смотреть фильм" if item["kind"] == "movie" else "Читать книгу", True, "play")
    ui_button(d, tx + 226, y + h - 62, 190, 42, "В избранное", False, "heart")


def ImageEnhance_dark(img, factor):
    from PIL import ImageEnhance
    return ImageEnhance.Brightness(img).enhance(factor)


# ----------------------------------------------------------------------------
# UI: Главная
# ----------------------------------------------------------------------------
def build_home(liked=False):
    img, d = ui_canvas()
    ui_sidebar(img, d, "home")
    cx = 300
    ui_search_bar(img, d, cx, 42, 760, 54)
    hero_item = BY_ID["m1"]
    ui_hero(img, d, hero_item, cx, 122, 1460, 356, heart_filled=liked, heart_glow=0.55 if liked else 0)
    d.text((cx, 510), "Рекомендуем для вас", font=F_BOLD(30), fill=(*TEXT, 255), anchor="la")
    d.text((cx, 548), "На основе ваших предпочтений" if not liked else "Вкус обновлён: фантастика, драма, Нолан",
           font=F_REG(17), fill=(*MUTED, 255), anchor="la")
    if liked:
        d.text((cx + 1160, 520), "ОБНОВЛЕНО", font=F_SEMI(14), fill=(*GOLD, 255), anchor="la")
    ids = ["b1", "m2", "b5", "m4"] if liked else ["m2", "m5", "m9", "m8"]
    x = cx
    for i in ids:
        ui_poster_card(img, d, BY_ID[i], x, 590, 300, 400)
        x += 336
    return img.convert("RGB")


HOME_BEFORE = build_home(False)
HOME_AFTER = build_home(True)


# ----------------------------------------------------------------------------
# UI: Детали
# ----------------------------------------------------------------------------
def build_detail():
    img, d = ui_canvas()
    ui_sidebar(img, d, "movies")
    cx = 300
    # назад
    d.rounded_rectangle((cx, 38, cx + 120, 78), radius=8, fill=(*CARD, 255), outline=(*BORDER, 255), width=2)
    d.text((cx + 18, 48), "←  Назад", font=F_SEMI(18), fill=(*TEXT, 255), anchor="la")
    d.text((cx + 150, 50), "КАТАЛОГ  /  ИНТЕРСТЕЛЛАР", font=F_SEMI(14), fill=(*FAINT, 255), anchor="la")
    # hero
    item = BY_ID["m1"]
    ui_hero(img, d, item, cx, 100, 1460, 380, heart_filled=True, heart_glow=0.4)
    # похожие
    d.text((cx, 520), "Похожие произведения", font=F_BOLD(30), fill=(*TEXT, 255), anchor="la")
    d.text((cx, 558), "Подобраны по жанру, автору и вашему вкусу", font=F_REG(17), fill=(*MUTED, 255), anchor="la")
    x = cx
    for i in ["m2", "m4", "m5", "b1"]:
        ui_poster_card(img, d, BY_ID[i], x, 602, 300, 400)
        x += 336
    return img.convert("RGB")


DETAIL = build_detail()


# ----------------------------------------------------------------------------
# UI: Избранное
# ----------------------------------------------------------------------------
def build_favorites():
    img, d = ui_canvas()
    ui_sidebar(img, d, "favorites")
    cx = 300
    d.text((cx, 40), "Избранное", font=F_BLACK(42), fill=(*TEXT, 255), anchor="la")
    d.text((cx, 94), "Ваша личная коллекция сохранённых фильмов и книг", font=F_REG(19), fill=(*MUTED, 255), anchor="la")
    # статистика
    d.rounded_rectangle((cx, 140, cx + 1460, 232), radius=12, fill=(*CARD, 255), outline=(*BORDER, 255), width=2)
    stats = [("ВСЕГО", "12", GOLD), ("ФИЛЬМЫ", "7", ACCENT), ("КНИГИ", "5", BLUE)]
    x = cx + 22
    for label, val, col in stats:
        d.rounded_rectangle((x, 154, x + 170, 218), radius=9, fill=(*BG, 255), outline=(*BORDER, 255), width=1)
        d.text((x + 16, 162), label, font=F_SEMI(12), fill=(*MUTED, 255), anchor="la")
        d.text((x + 16, 180), val, font=F_BLACK(28), fill=(*col, 255), anchor="la")
        x += 186
    d.line((cx + 610, 156, cx + 610, 216), fill=(*BORDER, 255), width=2)
    d.text((cx + 640, 158), "ВАШ ВКУС", font=F_SEMI(13), fill=(*MUTED, 255), anchor="la")
    d.text((cx + 640, 182), "Фантастика  ·  Драма  ·  Фэнтези", font=F_SEMI(21), fill=(*GOLD, 255), anchor="la")
    # сетка 3x2
    ids = ["m1", "m2", "b1", "b5", "m7", "b6"]
    x0, y0 = cx + 60, 260
    for idx, i in enumerate(ids):
        r, c = divmod(idx, 3)
        ui_poster_card(img, d, BY_ID[i], x0 + c * 420, y0 + r * 380, 260, 340)
    return img.convert("RGB")


FAVORITES = build_favorites()


# ----------------------------------------------------------------------------
# UI: Поиск
# ----------------------------------------------------------------------------
def build_search(query="фантастика", typed=10, show_results=True):
    img, d = ui_canvas()
    ui_sidebar(img, d, "home")
    # затемнённая главная на фоне
    bg = HOME_BEFORE.copy().filter(ImageFilter.GaussianBlur(6))
    bg = ImageEnhance_dark(bg, 0.45).convert("RGBA")
    img.alpha_composite(bg, (0, 0))
    # попап
    px, py, pw, ph = 380, 96, 1040, 820
    d.rounded_rectangle((px, py, px + pw, py + ph), radius=18, fill=(*CARD, 255), outline=(*BORDER, 255), width=2)
    # строка поиска
    d.rounded_rectangle((px + 30, py + 30, px + pw - 30, py + 100), radius=10, fill=(*BG, 255), outline=(*ACCENT, 255), width=2)
    icon_search(d, px + 48, py + 48, 30, (*MUTED, 255))
    shown = query[:typed]
    d.text((px + 92, py + 48), shown, font=F_REG(26), fill=(*TEXT, 255), anchor="la")
    if typed < len(query) or True:
        tw = d.textlength(shown, font=F_REG(26))
        d.line((px + 94 + tw, py + 50, px + 94 + tw, py + 84), fill=(*ACCENT, 255), width=2)
    if not show_results:
        d.text((px + 34, py + 150), "Начните вводить название, жанр или автора",
               font=F_REG(24), fill=(*MUTED, 220), anchor="la")
        d.text((px + 34, py + 192), "Поиск работает по фильмам, книгам, авторам и жанрам",
               font=F_REG(18), fill=(*FAINT, 220), anchor="la")
        return img.convert("RGB")
    d.text((px + pw - 190, py + 52), "Найдено: 5", font=F_REG(18), fill=(*MUTED, 255), anchor="la")
    # фильмы
    y = py + 130
    d.text((px + 34, y), "ФИЛЬМЫ", font=F_SEMI(15), fill=(*MUTED, 255), anchor="la")
    y += 30
    movie_ids = ["m1", "m2", "m5"]
    for i in movie_ids:
        item = BY_ID[i]
        p = poster(i, 90, 120).resize((56, 76), Image.LANCZOS)
        img.alpha_composite(rounded_img(p, 6), (px + 34, y))
        d.text((px + 106, y + 4), item["title"], font=F_SEMI(22), fill=(*TEXT, 255), anchor="la")
        gx = px + 106
        d.text((gx, y + 34), ", ".join(item["genre"]), font=F_REG(15), fill=(*MUTED, 255), anchor="la")
        gx += d.textlength(", ".join(item["genre"]), font=F_REG(15)) + 18
        d.text((gx, y + 34), "·", font=F_REG(15), fill=(*MUTED, 255), anchor="la")
        gx += 18
        rating_text(d, gx, y + 34, item["rating"], F_SEMI(15), (*GOLD, 255), star_s=12)
        d.text((gx + 52, y + 34), f"·  {item['year']}", font=F_REG(15), fill=(*MUTED, 255), anchor="la")
        d.text((px + pw - 100, y + 16), "ФИЛЬМ", font=F_SEMI(13), fill=(*ACCENT, 255), anchor="la")
        y += 88
    # разделитель
    d.line((px + 30, y + 6, px + pw - 30, y + 6), fill=(*BORDER, 255), width=2)
    y += 24
    d.text((px + 34, y), "КНИГИ", font=F_SEMI(15), fill=(*MUTED, 255), anchor="la")
    y += 30
    for i in ["b1", "b5"]:
        item = BY_ID[i]
        p = poster(i, 90, 120).resize((56, 76), Image.LANCZOS)
        img.alpha_composite(rounded_img(p, 6), (px + 34, y))
        d.text((px + 106, y + 4), item["title"], font=F_SEMI(22), fill=(*TEXT, 255), anchor="la")
        d.text((px + 106, y + 34), f"{item['author']}  ·  ★ {item['rating']}  ·  {item['year']}",
               font=F_REG(15), fill=(*MUTED, 255), anchor="la")
        d.text((px + pw - 100, y + 16), "КНИГА", font=F_SEMI(13), fill=(*BLUE, 255), anchor="la")
        y += 88
    return img.convert("RGB")


SEARCH_TYPED = build_search("фантастика", 10, True)
SEARCH_EMPTY = build_search("фантастика", 0, False)


print("UI built", flush=True)
