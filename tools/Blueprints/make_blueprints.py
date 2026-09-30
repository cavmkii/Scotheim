#!/usr/bin/env python3
"""Generates Scotheim's location blueprints (PlanBuild .blueprint format, read by Expand World Data).

    python3 tools/Blueprints/make_blueprints.py      # writes src/Scotheim/Data/*.blueprint

Row format (EWD 1.73, data/Blueprints.cs): name;unused;posX;posY;posZ;rotX;rotY;rotZ;rotW;info;scaleX;scaleY;scaleZ;data;chance
Y is up. EWD rolls `chance` per object each time a location is placed, which is how the ruins vary.
The centre piece (piece_bpcenterpoint at the origin) fixes the location's ground point and isn't spawned.

Piece sizes are assumed from their names (stone_wall_2x1_ruin is 2 m wide and 1 m tall, and so on),
with pieces placed by their base; check the first in-game screenshots before trusting the geometry.
"""
import math
import random
from pathlib import Path

OUT = Path(__file__).resolve().parents[2] / "src/Scotheim/Data"


def row(name, x, y, z, yaw=0.0, scale=(1, 1, 1), chance=1.0):
    half = math.radians(yaw) / 2
    return "%s;;%.3f;%.3f;%.3f;0;%.5f;0;%.5f;;%.3f;%.3f;%.3f;;%.2f" % (
        name, x, y, z, math.sin(half), math.cos(half), scale[0], scale[1], scale[2], chance)


def ring(name, radius, width, y, chance, gap_deg=None, gap_width=0.0):
    """Wall pieces around a circle, each turned along the tangent. A gap leaves a doorway."""
    n = max(3, round(2 * math.pi * radius / width))
    rows = []
    for i in range(n):
        theta = 2 * math.pi * i / n
        if gap_deg is not None:
            d = (math.degrees(theta) - gap_deg + 180) % 360 - 180
            if abs(d) * math.pi / 180 * radius < gap_width / 2:
                continue
        x, z = radius * math.cos(theta), radius * math.sin(theta)
        yaw = -math.degrees(theta) - 90  # local X along the tangent
        rows.append(row(name, x, y, z, yaw, chance=chance))
    return rows


def write(name, description, rows):
    text = "\n".join([
        "#Name:" + name,
        "#Creator:Scotheim",
        "#Description:" + description,
        "#Category:Scotheim",
        "#Center:piece_bpcenterpoint",
        "#Pieces",
        row("piece_bpcenterpoint", 0, 0, 0),
    ] + rows) + "\n"
    (OUT / (name + ".blueprint")).write_text(text, encoding="utf-8", newline="\n")
    print("%-26s %4d objects" % (name, len(rows)))


rng = random.Random(1286)

# Broch: a double-skinned drystone tower (after Mousa, Shetland), ruined from the top down.
# Outer wall r = 6.5 m, inner r = 4.5 m, doorway facing east.
broch = []
for layer, chance in enumerate([1.0, 1.0, 0.9, 0.75, 0.55, 0.35]):
    broch += ring("stone_wall_2x1_ruin", 6.5, 2.0, layer, chance, gap_deg=0, gap_width=2.5 if layer < 2 else 0)
for layer, chance in enumerate([1.0, 0.9, 0.6]):
    broch += ring("stone_wall_1x1_ruin", 4.5, 1.0, layer, chance, gap_deg=0, gap_width=1.5 if layer < 2 else 0)
broch += [
    row("Scot_HacksilverCache", -1.5, 0.2, 1.0, chance=0.8),
    row("Scot_HacksilverCache", 1.0, 0.2, -2.0, chance=0.5),
    row("Scot_Spawner_Redcap", 0.0, 0.3, 0.0),
    row("Scot_Spawner_Redcap", -2.5, 0.3, -1.0, chance=0.6),
]
write("scotheim_broch", "A ruined broch held by redcaps.", broch)

# Crannog: a round house on a timber platform over a loch, on log piles. Placed at water level.
crannog = []
for gx in range(-2, 3):
    for gz in range(-2, 3):
        x, z = gx * 2.0, gz * 2.0
        if x * x + z * z <= 4.6 ** 2:
            crannog.append(row("wood_floor", x, 0.8, z, chance=0.95))
for i in range(10):
    theta = 2 * math.pi * i / 10
    crannog.append(row("wood_pole_log", 4.3 * math.cos(theta), -1.5, 4.3 * math.sin(theta)))
crannog += ring("woodwall", 3.0, 2.0, 0.9, 0.75, gap_deg=90, gap_width=2.0)
crannog += [
    row("Scot_HacksilverCache", 0.5, 1.0, -0.5, chance=0.6),
    row("Scot_Spawner_BeanNighe", -1.0, 1.1, 0.5, chance=0.7),
    row("Scot_Spawner_EachUisge", 6.0, 0.0, 0.0, chance=0.35),
]
write("scotheim_crannog", "A loch dwelling on timber piles, haunted.", crannog)

# Shieling: a small drystone summer hut on the hill grazing, roofless.
shieling = []
for layer, chance in enumerate([1.0, 0.7]):
    for x in (-1.0, 1.0):
        shieling.append(row("stone_wall_2x1_ruin", x, layer, -1.75, 0, chance=chance))
        if not (layer == 0 and x == 1.0):  # doorway
            shieling.append(row("stone_wall_2x1_ruin", x, layer, 1.75, 0, chance=chance))
    for z in (-0.75, 0.75):
        shieling.append(row("stone_wall_2x1_ruin", -2.25, layer, z, 90, chance=chance))
        shieling.append(row("stone_wall_2x1_ruin", 2.25, layer, z, 90, chance=chance))
shieling += [row("Scot_WildBere", 3.5 + 0.8 * i, 0, -1.0 + 0.9 * (i % 2), chance=0.6) for i in range(3)]  # old croft rig
shieling += [
    row("Scot_HacksilverCache", 0.5, 0.2, 0.0, chance=0.3),
    row("Scot_Spawner_Redcap", -0.5, 0.3, 0.0, chance=0.3),
]
write("scotheim_shieling", "A roofless summer hut on the hill grazing.", shieling)

# Stone circle: twelve standing stones, some fallen away, around a cairngorm find.
circle = []
for i in range(12):
    theta = 2 * math.pi * i / 12
    s = rng.uniform(0.7, 1.1)
    circle.append(row("Scot_StandingStone", 8 * math.cos(theta), 0, 8 * math.sin(theta), rng.uniform(0, 360),
                      scale=(s, s, s), chance=0.85))
circle.append(row("Scot_CairngormPickable", 0, 0.1, 0, chance=0.5))
write("scotheim_stonecircle", "A ring of standing stones.", circle)

# Symbol stones: one Pictish symbol stone each; each variant has its own text (see Content/Places.cs).
for n in range(1, 6):
    write("scotheim_symbolstone%d" % n, "A Pictish symbol stone.", [])
    path = OUT / ("scotheim_symbolstone%d.blueprint" % n)
    path.write_text(path.read_text(encoding="utf-8").rstrip("\n") + "\n" + row("Scot_SymbolStone%d" % n, 0, 0, 0) + "\n",
                    encoding="utf-8", newline="\n")
