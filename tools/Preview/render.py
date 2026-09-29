"""Render Preview.cs output as shaded relief, before vs after. Needs numpy and Pillow."""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

out = Path(sys.argv[1] if len(sys.argv) > 1 else "preview_out")
px, size, _, _ = open(out / "meta.txt").read().split()
px, size = int(px), float(size)
cell = size / px

BIOME_TINT = {1: (150, 170, 95), 8: (70, 100, 60), 4: (150, 145, 140)}


def shade(h):
    gy, gx = np.gradient(h * 2.0, cell)  # 2x vertical exaggeration
    # Light from the NW, 45 degrees up.
    lx, ly, lz = -0.5, 0.5, 0.7071
    n = np.sqrt(gx * gx + gy * gy + 1.0)
    # Rows run north to south, so +row is -north.
    return np.clip((-gx * lx + gy * ly + lz) / n, 0, 1)


def render(name):
    h = np.fromfile(out / f"{name}.f32", dtype=np.float32).reshape(px, px)
    b = np.fromfile(out / f"{name}.biome", dtype=np.uint8).reshape(px, px)
    s = shade(h)[..., None]
    tint = np.zeros((px, px, 3), np.float32)
    for k, c in BIOME_TINT.items():
        tint[b == k] = c
    snow = np.clip((h - 110) / 60, 0, 1)[..., None]
    land = tint * (1 - snow) + np.array([235, 235, 240]) * snow
    img = land * (0.35 + 0.75 * s)
    water = h < 0
    depth = np.clip(-h / 15, 0, 1)
    img[water] = (np.array([70, 110, 150]) * (1 - depth[water, None]) + np.array([25, 45, 80]) * depth[water, None])
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)), h


before, hb = render("before")
after, ha = render("after")
pad = 40
canvas = Image.new("RGB", (px * 2 + pad * 3, px + pad * 2), (245, 245, 240))
canvas.paste(before, (pad, pad))
canvas.paste(after, (px + pad * 2, pad))
d = ImageDraw.Draw(canvas)
d.text((pad, 12), f"before (synthetic vanilla-like)   {size/1000:.1f} km across, N up", fill=(0, 0, 0))
d.text((px + pad * 2, 12), "after (Scotheim)   grey=Mountain  dark green=Black Forest  olive=Meadows", fill=(0, 0, 0))
canvas.save(out / "preview.png")

for name, h in (("before", hb), ("after", ha)):
    land = h[h > 0]
    print(f"{name}: max {h.max():.0f} m, p99 {np.percentile(land, 99):.0f} m, water {100*(h<0).mean():.1f}%")
print("wrote", out / "preview.png")
