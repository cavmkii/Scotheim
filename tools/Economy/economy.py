#!/usr/bin/env python3
"""Rough supply-and-demand check for Scotheim's crafting materials.

    mono preview.exe out <seed> 20000 2000        # whole world at 10 m (see tools/Preview)
    python3 tools/Economy/economy.py out           # needs numpy and PyYAML

Supply: for each Highland vegetation entry that yields a material, the expected number placed per
island, from the preview's biome and height grids. Valheim/EWD place a random count between min and max
per 64 m zone (the fraction counting as a chance of one more), then drop each attempt that fails the
biome, altitude or tilt test, so expected placements per zone = mean(min, max) x (share of the zone that
passes) x mean group size. Terrain delta is ignored, so this overestimates slightly on rough ground.

Demand: every gear recipe crafted once and upgraded to quality 4 (amount + 3 x per-level).

The preview's world is synthetic (not a real seed's vanilla terrain), so treat the numbers as orders of
magnitude, not counts.
"""
import math
import re
import sys
from pathlib import Path

import numpy as np
import yaml

root = Path(__file__).resolve().parents[2]
out = Path(sys.argv[1] if len(sys.argv) > 1 else "out")
px, size, ox, oy = (float(v) for v in (out / "meta.txt").read_text().split())
px = int(px)
cell = size / px
height = np.fromfile(out / "after.f32", dtype="<f4").reshape(px, px)[::-1]  # rows written top-down
biome = np.fromfile(out / "after.biome", dtype="u1").reshape(px, px)[::-1]
BIOMES = {"highland_moor": 101, "caledonian_forest": 108, "munros": 104}

gy, gx = np.gradient(height, cell)
tilt = np.degrees(np.arctan(np.hypot(gx, gy)))
xs = ox + (np.arange(px) + 0.5) * cell
X, Y = np.meshgrid(xs, xs)

# Islands: connected Highland ground (land or loch), found by flood fill on a 64 m grid.
highland = np.isin(biome, list(BIOMES.values()))
step = max(1, int(round(64 / cell)))
coarse = highland[::step, ::step]
labels = np.zeros(coarse.shape, dtype=int)
n = 0
for start in zip(*np.nonzero(coarse)):
    if labels[start]:
        continue
    n += 1
    stack = [start]
    labels[start] = n
    while stack:
        i, j = stack.pop()
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            a2, b2 = i + di, j + dj
            if 0 <= a2 < coarse.shape[0] and 0 <= b2 < coarse.shape[1] and coarse[a2, b2] and not labels[a2, b2]:
                labels[a2, b2] = n
                stack.append((a2, b2))
sizes = np.bincount(labels.ravel())[1:]
big = [k + 1 for k in np.argsort(sizes)[::-1] if sizes[k] >= 50][:8]
full = np.kron(labels, np.ones((step, step), dtype=int))[:px, :px]
island = np.full(full.shape, -1)
for k, lab in enumerate(big):
    island[full == lab] = k
centres = [(X[island == k].mean(), Y[island == k].mean()) for k in range(len(big))]

zone = int(round(64 / cell))
zx, zy = (np.floor((X - ox) / 64).astype(int), np.floor((Y - oy) / 64).astype(int))
zid = zx * 100000 + zy

content = (root / "src/Scotheim/Content/HighlandContent.cs").read_text(encoding="utf-8")
gear = (root / "src/Scotheim/Content/Gear.cs").read_text(encoding="utf-8")
veg = yaml.safe_load((root / "src/Scotheim/Data/expand_vegetation_scotheim.yaml").read_text(encoding="utf-8"))
yields = {p: i for p, _, i in re.findall(r'AddPickable\(added, "(\w+)", "(\w+)", "(\w+)"\)', content)}
yields["CloudberryBush"] = "Cloudberry"
yields["BlueberryBush"] = "Blueberries"
yields["RaspberryBush"] = "Raspberry"

supply = {}  # material -> per-island expected count
for entry in veg:
    item = yields.get(entry["prefab"])
    if item is None:
        continue
    codes = [BIOMES[b.strip()] for b in entry["biome"].split(",") if b.strip() in BIOMES]
    ok = np.isin(biome, codes) & (height >= entry.get("minAltitude", -1000)) & (height <= entry.get("maxAltitude", 1000))
    ok &= tilt <= entry.get("maxTilt", 90) if "maxTilt" in entry else True
    ok &= tilt >= entry.get("minTilt", 0)
    per_zone_attempts = (entry.get("min", 1) + entry.get("max", 1)) / 2
    group = (entry.get("groupSizeMin", 1) + entry.get("groupSizeMax", 1)) / 2
    # Expected placements = sum over zones of attempts x pass share x group; the pass share of a zone is
    # (passing cells in zone) / (cells per zone), so the sum is attempts x group x passing cells / cells per zone.
    cells_per_zone = (64 / cell) ** 2
    counts = [float(per_zone_attempts * group * np.count_nonzero(ok & (island == k)) / cells_per_zone)
              for k in range(len(centres))]
    per = supply.setdefault(item, [0.0] * len(centres))
    for k, c in enumerate(counts):
        per[k] += c

# Demand: all gear to quality 4, plus tartan cloth to cover it (4 wool + 2 blaeberries make 2 cloth).
demand = {}
for name, reqs in re.findall(r'Name = "(Scot_\w+)"[^\n]*(?:\n[^\n]*?)*?Recipe = new\[\] \{([^}]*)\}', gear):
    for item, amount, per in re.findall(r'Req\("(\w+)", (\d+), (\d+)\)', reqs):
        demand[item] = demand.get(item, 0) + int(amount) + 3 * int(per)
tartan = demand.get("Scot_TartanCloth", 0)
crafts = math.ceil(tartan / 2)
demand["Scot_Wool"] = demand.get("Scot_Wool", 0) + 4 * crafts
demand["Scot_Blaeberries"] = demand.get("Scot_Blaeberries", 0) + 2 * crafts
demand["Scot_BogIronOre"] = demand.pop("Scot_BogIron", 0)

print("Islands (by land area): " + ", ".join("#%d at (%.0f, %.0f)" % (k + 1, cx, cy) for k, (cx, cy) in enumerate(centres)))
print()
print("%-22s %10s %s" % ("material", "all gear", "expected on each island (pickables only)"))
for item in sorted(set(demand) | set(supply)):
    per = supply.get(item)
    print("%-22s %10s %s" % (item, demand.get(item, "-"), "  ".join("%7.0f" % v for v in per) if per else "(creature drop or crafted)"))
