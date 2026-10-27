# -*- coding: utf-8 -*-
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_ad as M
from PIL import Image

out = os.path.join(M.PREVIEW, "ui")
os.makedirs(out, exist_ok=True)
for name, img in [
    ("home_before", M.HOME_BEFORE),
    ("home_after", M.HOME_AFTER),
    ("detail", M.DETAIL),
    ("favorites", M.FAVORITES),
    ("search", M.SEARCH_TYPED),
]:
    img.save(os.path.join(out, name + ".png"))

# композиция: окно приложения на вертикальном холсте
for name, img in [("home", M.HOME_BEFORE), ("detail", M.DETAIL), ("favorites", M.FAVORITES), ("search", M.SEARCH_TYPED)]:
    frame = M.BASE.copy()
    fw = 1000
    fh = int(fw * M.UI_H / M.UI_W)
    ui = img.resize((fw, fh), Image.LANCZOS)
    M.paste_rounded(frame, ui, ((M.W - fw) // 2, 640), 24, shadow=True, shadow_blur=38, shadow_offset=(0, 18), shadow_alpha=170)
    frame.convert("RGB").save(os.path.join(out, "comp_" + name + ".png"))

# постерная стена
M.POSTER_WALL.crop((700, 400, 1780, 2320)).resize((M.W, M.H), Image.LANCZOS).convert("RGB").save(os.path.join(out, "wall.png"))
print("saved")
