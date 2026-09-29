#!/usr/bin/env python3
"""Cross-checks Scotheim's EWD data files and Jötunn content against reference dumps of the game.

    python3 tests/Data/check_data.py        (needs PyYAML)

Checks names, not balance: YAML keys against EWD 1.73's classes, biome and weather names against
the biome file and EWD's vanilla dump, prefab and item names against Jötunn's generated lists for
Valheim 1.0.7, and that every Scot_* name used somewhere is defined in Content/.
"""
import re
import sys
from pathlib import Path

import yaml

root = Path(__file__).resolve().parents[2]
data = root / "src/Scotheim/Data"
content = (root / "src/Scotheim/Content/HighlandContent.cs").read_text(encoding="utf-8")
localization = (root / "src/Scotheim/Content/Localization.cs").read_text(encoding="utf-8")
gear_src = (root / "src/Scotheim/Content/Gear.cs").read_text(encoding="utf-8")
ref = root / "reference"

fails = 0


def check(ok, what):
    global fails
    print(("PASS " if ok else "FAIL ") + what)
    if not ok:
        fails += 1


def lines(path):
    return {l.strip() for l in path.read_text(encoding="utf-8").splitlines() if l.strip()}


# EWD 1.73 ExpandWorldData/spawn/SpawnData.cs (release commit 2168284). maxSpawned, spawnInterval and
# spawnDistance are serialized with DefaultValuesHandling.Preserve, i.e. EWD writes them always; give them.
SPAWN_FIELDS = set("""prefab enabled name drops biome biomeArea spawnChance maxSpawned spawnInterval maxLevel
minLevel minAltitude maxAltitude spawnAtDay spawnAtNight requiredGlobalKey requiredEnvironments spawnDistance
spawnRadiusMin spawnRadiusMax groupSizeMin groupSizeMax groupRadius minTilt maxTilt inForest outsideForest
canSpawnCloseToPlayer insidePlayerBase inLava outsideLava minOceanDepth maxOceanDepth huntPlayer groundOffset
groundOffsetRandom levelUpMinCenterDistance overrideLevelupChance minDistance maxDistance objects data faction
fields""".split())
PRESERVED = {"maxSpawned", "spawnInterval", "spawnDistance"}

vanilla_items = lines(ref / "jotunn-valheim-1.0.7/items.txt")
vanilla_prefabs = lines(ref / "jotunn-valheim-1.0.7/prefabs.txt")
characters = {}
for l in (ref / "jotunn-valheim-1.0.7/characters.txt").read_text(encoding="utf-8").splitlines():
    name, _, comps = l.partition(": ")
    characters[name] = set(comps.split(", "))

biomes = {b["biome"] for b in yaml.safe_load((data / "expand_biomes_scotheim.yaml").read_text(encoding="utf-8"))}
environments = {e["name"] for e in yaml.safe_load((ref / "ewd-1.73/expand_environments.yaml").read_text(encoding="utf-8"))}
spawns = yaml.safe_load((data / "expand_spawns_scotheim.yaml").read_text(encoding="utf-8"))
vegetation = yaml.safe_load((data / "expand_vegetation_scotheim.yaml").read_text(encoding="utf-8"))

items = dict(re.findall(r'Name = "(Scot_\w+)", Base = "(\w+)"', content.split("ItemSpec[] Items")[1].split("static void AddItems")[0]))
creatures = dict(re.findall(r'Name = "(Scot_\w+)", Base = "(\w+)"', content.split("CreatureSpec[] Creatures")[1]))
gear = {n: (look, donor) for n, look, donor in re.findall(r'Name = "(Scot_\w+)", Look = "(\w+)", Donor = "(\w+)"', gear_src)}
prefabs = {"Scot_BlaeberryBush"} if 'CreateClonedPrefab("Scot_BlaeberryBush", "BlueberryBush")' in content else set()

# --- spawns
bad_keys = sorted({k for s in spawns for k in s} - SPAWN_FIELDS)
check(not bad_keys, "spawn keys are EWD 1.73 spawn fields" + (": unknown " + ", ".join(bad_keys) if bad_keys else ""))
missing = [s["name"] for s in spawns if not PRESERVED <= set(s)]
check(not missing, "every spawn sets maxSpawned, spawnInterval and spawnDistance" + (": " + ", ".join(missing) if missing else ""))
names = [s["name"] for s in spawns]
check(len(names) == len(set(names)), "spawn names are unique")
used_biomes = {b.strip() for s in spawns for b in s["biome"].split(",")}
check(used_biomes <= biomes, "spawn biomes are Scotheim biomes (" + ", ".join(sorted(used_biomes)) + ")")
used_envs = {e.strip() for s in spawns if "requiredEnvironments" in s for e in s["requiredEnvironments"].split(",")}
check(used_envs <= environments, "required environments exist in vanilla (" + ", ".join(sorted(used_envs)) + ")")
unknown = sorted({s["prefab"] for s in spawns} - set(creatures))
check(not unknown, "every spawned prefab is a Scotheim creature" + (": " + ", ".join(unknown) if unknown else ""))
bad_fields = sorted({k for s in spawns for k in (s.get("fields") or {})} - {"damage"})
check(not bad_fields, "spawn fields only use damage" + (": " + ", ".join(bad_fields) if bad_fields else ""))
never = sorted(set(creatures) - {s["prefab"] for s in spawns} - {"Scot_Lamb", "Scot_HighlandCalf"})
check(not never, "every adult creature has a spawn" + (": missing " + ", ".join(never) if never else ""))

# --- vegetation
veg_custom = sorted({v["prefab"] for v in vegetation if v["prefab"].startswith("Scot_")} - prefabs)
check(not veg_custom, "vegetation Scot_ prefabs are defined in Content" + (": " + ", ".join(veg_custom) if veg_custom else ""))
veg_vanilla = sorted({v["prefab"] for v in vegetation if not v["prefab"].startswith("Scot_")} - vanilla_prefabs)
check(not veg_vanilla, "vegetation vanilla prefabs exist" + (": " + ", ".join(veg_vanilla) if veg_vanilla else ""))

# --- content
bad = sorted(b for b in items.values() if b not in vanilla_items)
check(not bad, "item bases are vanilla items" + (": " + ", ".join(bad) if bad else ""))
bad = sorted(b for b in creatures.values() if b not in characters)
check(not bad, "creature bases are vanilla characters" + (": " + ", ".join(bad) if bad else ""))
for adult, young in re.findall(r'Relink\(added, "(\w+)", "(\w+)"\)', content):
    a, y = characters.get(creatures.get(adult), set()), characters.get(creatures.get(young), set())
    check({"Procreation", "Tameable"} <= a and "Growup" in y,
          "%s (%s) breeds and %s (%s) grows up" % (adult, creatures.get(adult), young, creatures.get(young)))
dropped = set(re.findall(r'Drop\("(\w+)"', content)) | set(re.findall(r'RequirementConfig\("(\w+)"', content))
eaten = set(re.findall(r'"(\w+)"', "".join(re.findall(r"Food = new\[\] \{([^}]*)\}", content))))
bad = sorted(n for n in dropped | eaten if n not in items and n not in vanilla_items)
check(not bad, "drops, recipes and food name real items" + (": " + ", ".join(bad) if bad else ""))
stations = set(re.findall(r'"(piece_\w+)"', content))
check(stations <= vanilla_prefabs, "crafting stations exist (" + ", ".join(sorted(stations)) + ")")
# --- gear
bad = sorted({v for pair in gear.values() for v in pair} - vanilla_items)
check(len(gear) == 26 and not bad, "%d gear pieces; looks and stat donors are vanilla items" % len(gear) + (": " + ", ".join(bad) if bad else ""))
needed = set(re.findall(r'Req\("(\w+)"', gear_src))
bad = sorted(n for n in needed if n not in items and n not in vanilla_items)
check(not bad, "gear recipes name real items (" + ", ".join(sorted(needed)) + ")" + (": missing " + ", ".join(bad) if bad else ""))
stations = {re.search(r'const string Forge = "(\w+)"', gear_src).group(1)} | set(re.findall(r'Station = "(\w+)"', gear_src))
check(stations <= vanilla_prefabs, "gear crafting stations exist (" + ", ".join(sorted(stations)) + ")")
sets_src = (root / "src/Scotheim/Content/Sets.cs").read_text(encoding="utf-8")
set_ids = set(re.findall(r'Id = "(\w+)"', sets_src))
by_set = {}
capes_in_sets = [n for n, sid in re.findall(r'Name = "(Scot_\w+)"[^\n]*Set = "(\w+)"', gear_src) if gear[n][0].startswith("Cape")]
check(not capes_in_sets, "capes stand outside the sets" + (": " + ", ".join(capes_in_sets) if capes_in_sets else ""))
for n, sid in re.findall(r'Name = "(Scot_\w+)"[^\n]*Set = "(\w+)"', gear_src):
    by_set.setdefault(sid, []).append(n)
check(set(by_set) == set_ids, "every set has pieces and a bonus (" + ", ".join(sorted(set_ids)) + ")")
for sid, names in sorted(by_set.items()):
    check(any(l.startswith("Helm") for l in (gear[n][0] for n in names)) and
          any(l.endswith("Legs") for l in (gear[n][0] for n in names)) and
          any(l.endswith(("Chest", "Cuirass")) for l in (gear[n][0] for n in names)),
          "set %s covers head, chest and legs (%s)" % (sid, ", ".join(names)))
icons = set(re.findall(r'Icon = "(\w+)"', sets_src))
effects = {l.split("|")[1] for l in (root / "reference/jotunn-valheim-1.0.7/status-effects.txt").read_text(encoding="utf-8").splitlines() if "|" in l} if (root / "reference/jotunn-valheim-1.0.7/status-effects.txt").exists() else set()
check(icons <= effects, "set bonus icons come from vanilla status effects (" + ", ".join(sorted(icons)) + ")")
for kind, defined in (("item", items), ("creature", creatures), ("gear piece", gear)):
    missing = sorted(n for n in defined if '{ "%s", ' % n not in localization)
    check(not missing, "every %s has English text" % kind + (": " + ", ".join(missing) if missing else ""))

print("ALL PASS" if fails == 0 else "%d FAILED" % fails)
sys.exit(1 if fails else 0)
