"""Upscale keyed small Skyline icons 2x with hq2x, keeping alpha.

Input files are named <name>.<w>.<h>.png; outputs are <name>.<2w>.<2h>.png.
hqx works on RGB only, so the color and the alpha mask are upscaled separately:
the color over a neutral fill, the mask as a black/white image. The fill is then
un-blended out of the edge pixels using the upscaled mask as the coverage.
"""
import glob
import os
import re
import sys

import hqx
from PIL import Image

src_dir = sys.argv[1]
out_dir = sys.argv[2]
os.makedirs(out_dir, exist_ok=True)

FILL = (128, 128, 128)
NAME_RE = re.compile(r"^(.*)\.(\d+)\.(\d+)\.png$")

count = 0
for path in sorted(glob.glob(os.path.join(src_dir, "*.png"))):
    m = NAME_RE.match(os.path.basename(path))
    if not m:
        continue
    name, w, h = m.group(1), int(m.group(2)), int(m.group(3))
    img = Image.open(path).convert("RGBA")
    if img.size != (w, h):
        continue
    alpha = img.getchannel("A")

    # Color over a neutral fill, so edge blends are easy to undo.
    color = Image.new("RGB", img.size, FILL)
    color.paste(img.convert("RGB"), mask=alpha)
    color2 = hqx.hq2x(color).convert("RGB")

    # Alpha mask as an RGB image; hq2x smooths its diagonals the same way.
    mask_rgb = Image.merge("RGB", (alpha, alpha, alpha))
    mask2 = hqx.hq2x(mask_rgb).convert("L")

    out = Image.new("RGBA", color2.size, (0, 0, 0, 0))
    cp = color2.load()
    mp = mask2.load()
    op = out.load()
    w2, h2 = color2.size
    for y in range(h2):
        for x in range(w2):
            a = mp[x, y]
            if a == 0:
                continue
            r, g, b = cp[x, y]
            if a < 255:
                # Un-blend: c = a*glyph + (1-a)*fill  =>  glyph = (c - (1-a)*fill) / a
                f = a / 255.0
                r = int(round(min(255, max(0, (r - (1 - f) * FILL[0]) / f))))
                g = int(round(min(255, max(0, (g - (1 - f) * FILL[1]) / f))))
                b = int(round(min(255, max(0, (b - (1 - f) * FILL[2]) / f))))
            op[x, y] = (r, g, b, a)
    out.save(os.path.join(out_dir, f"{name}.{w2}.{h2}.png"))
    count += 1

print(f"hq2x: wrote {count} icons to {out_dir}")
