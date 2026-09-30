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
prefab_components = {}
for l in (ref / "jotunn-valheim-1.0.7/prefab-components.txt").read_text(encoding="utf-8").splitlines():
    name, _, comps = l.partition(": ")
    prefab_components[name] = set(comps.split(", "))
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
pickables = re.findall(r'AddPickable\(added, "(Scot_\w+)", "(\w+)", "(Scot_\w+)"\)', content)
prefabs = {p for p, _, _ in pickables}

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
never = sorted(set(creatures) - {s["prefab"] for s in spawns} - {"Scot_Lamb", "Scot_HighlandCalf", "Scot_GreyMan"})  # the boss is summoned
check(not never, "every adult creature has a spawn" + (": missing " + ", ".join(never) if never else ""))

# --- vegetation
veg_custom = sorted({v["prefab"] for v in vegetation if v["prefab"].startswith("Scot_")} - prefabs)
check(not veg_custom, "vegetation Scot_ prefabs are defined in Content" + (": " + ", ".join(veg_custom) if veg_custom else ""))
veg_vanilla = sorted({v["prefab"] for v in vegetation if not v["prefab"].startswith("Scot_")} - vanilla_prefabs)
check(not veg_vanilla, "vegetation vanilla prefabs exist" + (": " + ", ".join(veg_vanilla) if veg_vanilla else ""))

bad = sorted({b for _, b, _ in pickables} - vanilla_prefabs) + sorted({i for _, _, i in pickables} - set(items))
check(not bad, "%d pickables copy real prefabs and yield Scotheim items" % len(pickables) + (": " + ", ".join(bad) if bad else ""))
unplaced = sorted(p for p in prefabs - {v["prefab"] for v in vegetation} if not p.startswith("Scot_Pickable_"))  # crops are planted
check(not unplaced, "every pickable is placed in the vegetation file" + (": " + ", ".join(unplaced) if unplaced else ""))

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
check(len(gear) == 32 and not bad, "%d gear pieces; looks and stat donors are vanilla items" % len(gear) + (": " + ", ".join(bad) if bad else ""))
needed = set(re.findall(r'Req\("(\w+)"', gear_src))
# Only what the Highlands yield: Scotheim items, plus vanilla items Highland trees, rocks and creatures drop
# (hill wolves: wolf fang and pelt; fuath: troll hide; hill giants: crystal; bean-nighe: chain; red deer: deer hide).
HIGHLAND_VANILLA = {"Wood", "FineWood", "RoundLog", "Resin", "Stone", "Flint", "DeerHide", "WolfPelt", "WolfFang",
                    "TrollHide", "Crystal", "Chain", "Coins", "Raspberry"}
needed |= set(re.findall(r'RequirementConfig\("(\w+)"', content))
foreign = sorted(n for n in needed if not n.startswith("Scot_") and n not in HIGHLAND_VANILLA)
check(not foreign, "all recipes use only Highland materials" + (": foreign " + ", ".join(foreign) if foreign else ""))
bad = sorted(n for n in needed if n not in items and n not in vanilla_items)
check(not bad, "gear recipes name real items (" + ", ".join(sorted(needed)) + ")" + (": missing " + ", ".join(bad) if bad else ""))
stations = set(re.findall(r'const string \w+ = "((?:piece_|blackforge)\w*)"', gear_src)) | set(re.findall(r'Station = "(\w+)"', gear_src))
check(stations <= vanilla_prefabs, "gear crafting stations exist (" + ", ".join(sorted(stations)) + ")")
sets_src = (root / "src/Scotheim/Content/Sets.cs").read_text(encoding="utf-8")
set_ids = set(re.findall(r'Id = "(\w+)"', sets_src))
by_set = {}
capes_in_sets = [n for n, sid in re.findall(r'Name = "(Scot_\w+)"[^\n]*Set = "(\w+)"', gear_src) if gear[n][0].startswith("Cape")]
check(not capes_in_sets, "capes stand outside the sets" + (": " + ", ".join(capes_in_sets) if capes_in_sets else ""))
for n, sid in re.findall(r'Name = "(Scot_\w+)"[^\n]*Set = "(\w+)"', gear_src):
    by_set.setdefault(sid, []).append(n)
worn = set(re.findall(r'(?:EquipEffect|OnHit) = "(\w+)"', gear_src))
check(worn <= set_ids and set(by_set) == set_ids - worn, "every set has pieces and a bonus; every worn and on-hit effect exists (" + ", ".join(sorted(set_ids)) + ")")
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

# Jötunn's clone constructor overwrites CreatureConfig.Name with the prefab name, so the name must be set after.
check("character.m_name = Localization.CreatureName(spec.Name);" in content,
      "creature names are set after cloning (Jötunn resets them to the prefab name)")

# --- reskins: every recoloured name is a real Scotheim creature, item or gear piece
reskin = (root / "src/Scotheim/Content/Reskin.cs").read_text(encoding="utf-8")
names = set(re.findall(r'\{ "(Scot_\w+)", (?:Tint|TartanLook)', reskin))
unknown = sorted(names - set(creatures) - set(items) - set(gear))
check(not unknown, "%d reskins name real Scotheim things" % len(names) + (": unknown " + ", ".join(unknown) if unknown else ""))

# --- places: EWD location keys (1.73 location/LocationData.cs) and blueprint contents
LOCATION_FIELDS = set("""prefab enabled dungeon biome biomeArea altBiome quantity minDistance maxDistance minAltitude maxAltitude
prioritized centerFirst unique randomSeed pregenerate group groups groupMax awayFrom closeTo minDistanceFromSimilar
maxDistanceFromSimilar discoverLabel iconAlways iconPlaced randomRotation randomCardinal slopeRotation snapToWater
minTerrainDelta maxTerrainDelta inForest forestTresholdMin forestTresholdMax offset groundOffset data objectData objectSwap
dungeonObjectData dungeonObjectSwap locationObjectData locationObjectSwap objects commands exteriorRadius clearArea
randomDamage noBuild noBuildDungeon levelArea levelRadius levelBorder paint paintRadius paintBorder scaleMin scaleMax
scaleUniform minVegetation maxVegetation surroundCheckVegetation surroundCheckDistance surroundCheckLayers
surroundBetterThanAverage relaxableunique relaxable relaxableamount""".split())
locations = yaml.safe_load((data / "expand_locations_scotheim.yaml").read_text(encoding="utf-8"))
bad_keys = sorted({k for l in locations for k in l} - LOCATION_FIELDS)
check(not bad_keys, "location keys are EWD 1.73 location fields" + (": unknown " + ", ".join(bad_keys) if bad_keys else ""))
used = {b.strip() for l in locations for b in l["biome"].split(",")}
check(used <= biomes, "location biomes are Scotheim biomes")
places_src = (root / "src/Scotheim/Content/Places.cs").read_text(encoding="utf-8")
spawners = re.findall(r'new\[\] \{ "(Scot_Spawner_\w+)", "(\w+)", "(Scot_\w+)" \}', places_src)
bad = [s for s in spawners if s[1] not in vanilla_prefabs or s[2] not in creatures or "CreatureSpawner" not in prefab_components.get(s[1], set())]
check(spawners and not bad, "%d spawners copy vanilla CreatureSpawners and spawn Scotheim creatures" % len(spawners) + (": " + str(bad) if bad else ""))
stones = {"Scot_StandingStone"} | {"Scot_SymbolStone%d" % (i + 1) for i in range(places_src.count('new[] { "') - len(spawners))}
clones_src = "".join((root / "src/Scotheim/Content" / f).read_text(encoding="utf-8") for f in ("Bagging.cs", "Boss.cs"))
cairns = set(re.findall(r'CreateClonedPrefab\("(Scot_\w+)", "(\w+)"\)', clones_src))
check(len(cairns) == 2 and all(base in vanilla_prefabs for _, base in cairns), "summit cairn and boss altar copy vanilla prefabs: " + ", ".join(sorted(b for _, b in cairns)))
check("OfferingBowl" in prefab_components.get("offeraltar_FrozenKing_bossroom", set()), "the boss altar's base has an OfferingBowl")
own = set(prefabs) | {s[0] for s in spawners} | stones | {c for c, _ in cairns} | {"piece_bpcenterpoint"}
blueprint_names = set()
missing = set()
for f in sorted(data.glob("*.blueprint")):
    blueprint_names.add(f.stem)
    for line in f.read_text(encoding="utf-8").splitlines():
        if not line or line.startswith("#"):
            continue
        fields = line.split(";")
        if len(fields) != 15:
            missing.add(f.name + ": malformed row")
        elif fields[0] not in vanilla_prefabs and fields[0] not in own:
            missing.add(fields[0])
check(not missing, "blueprints use real prefabs" + (": " + ", ".join(sorted(missing)) if missing else ""))
unplaced = sorted({l["prefab"] for l in locations} - blueprint_names)
check(not unplaced, "every location has a blueprint" + (": " + ", ".join(unplaced) if unplaced else ""))
ew = (root / "src/Scotheim/Patches/ExpandWorld.cs").read_text(encoding="utf-8")
unwritten = sorted(n + ".blueprint" for n in blueprint_names if n + ".blueprint" not in ew)
check(not unwritten, "every blueprint is written on startup" + (": " + ", ".join(unwritten) if unwritten else ""))

# --- data file upgrades: every version a release wrote must be listed in ExpandWorld.Shipped, or players
# holding that version get the new default only as a .new file.
import hashlib, subprocess
ew = (root / "src/Scotheim/Patches/ExpandWorld.cs").read_text(encoding="utf-8")
shipped = {k: set(v.split('", "')) for k, v in re.findall(r'\{ (?:FileName|"(\w+\.(?:yaml|blueprint))"), new\[\] \{ "([^}]*)" \} \}', ew) if k}
shipped["expand_biomes_scotheim.yaml"] = set(re.search(r'\{ FileName, new\[\] \{ "([^}]*)" \} \}', ew).group(1).split('", "'))
def fingerprint(text):
    return hashlib.sha256(text.replace("\r", "").encode("utf-8")).hexdigest()[:16]
try:
    for f in sorted(list(data.glob("expand_*.yaml")) + list(data.glob("*.blueprint"))):
        rel = "src/Scotheim/Data/" + f.name
        current = fingerprint(f.read_text(encoding="utf-8"))
        commits = subprocess.run(["git", "log", "--format=%H", "--", rel], cwd=root, capture_output=True, text=True).stdout.split()
        past = {fingerprint(subprocess.run(["git", "show", c + ":" + rel], cwd=root, capture_output=True, text=True).stdout) for c in commits}
        missing = sorted(past - {current} - shipped.get(f.name, set()))
        check(not missing, "every released version of %s is upgradable" % f.name + (": missing " + ", ".join(missing) if missing else ""))
except FileNotFoundError:
    print("SKIP data file upgrade check (no git)")

print("ALL PASS" if fails == 0 else "%d FAILED" % fails)
sys.exit(1 if fails else 0)
