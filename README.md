# Scotheim

A BepInEx mod for Valheim that adds Scottish Highlands islands: new land raised out of open ocean (four islands by default), with three biomes of their own and Mistlands-tier creatures from Highland wildlife and folklore. Vanilla land isn't touched.

![whole world, synthetic](docs/preview-world.png)

![before/after, synthetic world](docs/preview.png)

*A synthetic, vanilla-like world (6 km across, north up) before and after. This is not a real seed; see [Status](#status).*

![three seeds](docs/preview-seeds.png)

## The three biomes

| Biome | Where | Terrain | Vegetation | Weather |
|---|---|---|---|---|
| **Highland Moor** | The open lowland shelf (median slope ~4°) | Rolling relief, hummocks, lochans | Heath shrubs, birch copses, erratic boulders, cloudberries, rare standing stones | Mostly heath-clear and mist, some drizzle |
| **Caledonian Forest** | A fringe around the hills, glens through them, a few patches on the moor | Drumlins aligned NE–SW, hummocky moraine | Open Scots pine with birch, blaeberry and raspberry; no spruce; rare ruined shielings | Forest mist, some rain |
| **Munros** | The hill massifs, above ~32 m of base height | Rounded domes, NE-facing corries, soft-capped summits | Crags and scree; a few stunted pines and birches below ~70 m | Freezing snow and storms, with misty breaks |

Each biome borrows a vanilla height function and ground texture. The moor uses Meadows' grass, plus meadow grass, heath flowers and bracken as ground clutter. EWD's `nature` setting only affects farming, bees and footsteps: the Moor farms like the Plains (barley, flax), the forest like the Black Forest. Vegetation comes entirely from `expand_vegetation_scotheim.yaml`, so no vanilla plants leak in. There are no ores yet.

The landscape between them:

- **Glens** are U-shaped troughs trending NE–SW, the Great Glen / Caledonian grain at about 040°. Each glen widens with its depth so walls stay near 35°.
- **Ribbon lochs** form where glen floors drop below sea level.
- **Sea lochs** form where glens reach the coast.
- The island is an ellipse along the grain, 4 × 2.3 km by default, with a ragged coast. Most of it is a flat lowland shelf (~14 m) of open moor; a few separate NE–SW hill massifs rise out of it, wooded on their lower slopes. It comes to about 4–5 km² of new land: roughly two-thirds moor, a fifth Munros and the rest forest (set by `MassifThreshold`, `MunroMinHeight` and `ForestCover`).

## Creatures and items

Every creature is a [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/) clone of a vanilla one: new name, health, size, drops and breeding, but the vanilla model, animations and attacks. They're stand-ins until there are proper models. Where they spawn, and how hard they hit, is set in `expand_spawns_scotheim.yaml`. Its `damage` field replaces the random 0.75–1 damage factor the game gives each creature, so `2` hits about 2.3× as hard as the vanilla average.

| Creature | Base | Where / when | Health (base) | Damage | Drops |
|---|---|---|---|---|---|
| Blackface sheep | Boar | Moor, flocks of 2–5 | 70 (10) | vanilla | Wool, raw mutton |
| Lamb | Boar piglet | Bred from sheep; grows into a sheep | 20 | vanilla | — |
| Red deer | Deer | Moor and forest | 90 (10) | — | Deer meat, deer hide |
| Highland cow | Lox, ¾ size | Moor, rare herds of 2–3 | 1400 (1000) | 1.5 | Highland beef, Highland hide |
| Highland calf | Lox calf | Bred from cattle; grows into a cow | 300 | vanilla | vanilla |
| Pine marten | Hare, 0.8× | Forest | 25 (20) | — | Pine marten pelt |
| Hill wolf | Wolf | Munros; packs at night, pairs by day | 220 (80) | 2.5 | vanilla wolf drops |
| Cù-sìth | Wolf, 1.45× | Moor at night, or in mist by day; alone | 650 | 3.5 | Sìth pelt, wolf fangs |
| Cat-sìth | Ulv | Forest at night | 450 | 1.3 | Sìth pelt |
| Bean-nighe | Wraith, 0.85× | Loch and lochan shores at night | 450 (100) | 1.8 | Washer's shroud, chain |
| Each-uisge | Abomination, 0.9× | Loch shores, rare | 2200 (800) | 1.8 | Kelpie mane |
| Redcap | Goblin, 0.8× | Forest at night, groups of 2–4 | 220 (70) | 2.0 | vanilla goblin drops |
| Fuath | Troll | Forest, rare | 1500 (600) | 1.6 | vanilla troll drops |
| Hill giant | Stone golem, 1.25× | Munros above 90 m | 2600 (800) | 1.5 | Giant's heartstone, crystal |

For scale, the Mistlands' Seeker has 175 health and the Seeker Soldier 1000. The vanilla figures in brackets are from memory, not the game files. The boss Am Fear Liath Mòr (the Grey Man of Ben Macdui) comes later.

Items, also Jötunn clones. Each keeps its base item's model and icon for now:

| Item | Source | Use |
|---|---|---|
| Wool | Sheep | Tartan cloth |
| Raw / roast mutton | Sheep; cooking station | Food: 55 health, 18 stamina, 4 regen, 30 min |
| Raw / roast Highland beef | Highland cow; cooking station | Food: 65 health, 22 stamina, 5 regen, 30 min |
| Blaeberries | Blaeberry bushes in the Caledonian Forest | Food: 15 health, 45 stamina, 15 min; dye for tartan |
| Tartan cloth | Workbench: 4 wool + 2 blaeberries | Nothing yet (armour set planned) |
| Highland hide, pine marten pelt, Sìth pelt, kelpie mane, washer's shroud, giant's heartstone | Drops | Nothing yet (gear planned) |

The food values are my estimate of Mistlands-tier food, not copied from the game. Valheim's "blueberries" already look like bilberries (*Vaccinium myrtillus*), which is what a blaeberry is, so the blaeberry bush is a copy of the blueberry bush that yields the Scots-named item.

Sheep eat blaeberries, blueberries, cloudberries and raspberries, and can be tamed like boars. Being boars underneath, they also charge when provoked.

## How it fits into the world

- **Placement.** Sites are chosen from the world seed, largest island first: each takes the most open stretch of water 4.5–8.5 km from the centre that keeps clear of the islands already placed and of the Ashlands and Deep North. An island may turn up to 30° from the grain to fit. Sizes shrink evenly from 100% to `MinScale` (65%). Every peer computes the same sites. `Count` sets how many; with `AutoPlace` off, a single island goes at X/Y.
- **Additive.** The island only rises out of water deeper than a few metres. Vanilla land and the water just off its beaches keep their vanilla biome and terrain. If a vanilla islet sits inside the footprint, the Highlands wrap around it.
- **Biomes.** Custom biomes come from [Expand World Data](https://thunderstore.io/c/valheim/p/JereKuusela/Expand_World_Data/), a soft dependency. Scotheim writes `expand_biomes_scotheim.yaml`, `expand_vegetation_scotheim.yaml`, `expand_clutter_scotheim.yaml` and `expand_spawns_scotheim.yaml` into `BepInEx/config/expand_world/`. The sources are in `src/Scotheim/Data/`. Scotheim finds the biomes by their identifiers (`highland_moor`, `caledonian_forest`, `munros`). Without EWD, the island uses vanilla Meadows, Black Forest and Mountain, with their vanilla vegetation and creatures.
- **Your edits stick.** A data file is replaced only when it's missing, or when it's an unedited copy of what an earlier Scotheim wrote (compared by fingerprint, ignoring line endings). If you've edited one, it's kept, and the new default is written beside it as `*.yaml.new`. EWD doesn't load `.new` files; merge from them by hand.
- **Spawns need EWD's spawn data.** Set `Spawn data = true` in `expand_world_data.cfg`; otherwise EWD ignores `expand_spawns*.yaml` and nothing spawns on the Highlands. **Fix EWD's own dump when you do:** its `expand_spawns.yaml` writes the four `Fimbulvinter - …` entries with no `biome`, and EWD 1.73 reads a missing biome as *every* biome (`DataManager.ToBiomes`), so meteors, Jotuns and Elakingar start spawning everywhere. Add `biome: None` to those four entries. EWD 1.73's `Drop data` setting does nothing on its own: that version has no drop files. Drops are set on the Jötunn clones instead.

## Status

The terrain and biomes have run in the game. The creatures and items haven't yet.

What has been verified:

- **Terrain math.** Run offline by `tools/Preview`, on the same source files the plugin compiles, over a synthetic world. The images and numbers here come from that.
- **Harmony patches.** Run for real (HarmonyX 2.10 under Mono) in `tests/Harness`, against stub Valheim types and a stub of EWD's `BiomeManager`, including EWD's swap of a custom biome for its terrain biome. Checks:
  - the island is raised, and vanilla land and far ocean stay untouched;
  - the right biome is assigned with and without EWD;
  - IDs are picked up when EWD syncs names;
  - heights are reshaped even after EWD's swap;
  - parallel evaluation is deterministic;
  - the YAML file is written.
- **Names and signatures.** Game and EWD names and signatures were checked against Expand World Data 1.73's source, not against the game assembly.
- **Data files.** Prefab names, weather names and terrain colours were checked against EWD 1.73's dump of vanilla data from Valheim 1.0.16 (in `reference/ewd-1.73/`). Every vegetation and biome field was checked against EWD's YAML classes.
- **Creatures and items** (`tests/Data/check_data.py`, `tests/Content/run.sh`):
  - spawn keys are checked against EWD 1.73's spawn class, and biome and weather names against the biome file and EWD's dump;
  - base creatures, items, crafting stations and their components (Tameable, Procreation, Growup) are checked against Jötunn's lists generated from Valheim 1.0.7 (`reference/jotunn-valheim-1.0.7/`);
  - the Jötunn calls compile against the real Jotunn.dll 2.30.2.

Still unverified:

- that it builds against the real game. In particular, five game fields the content code sets were written from memory: `Character.m_health`, `Pickable.m_itemPrefab`, `Procreation.m_offspring`, `Growup.m_grownPrefab` and the `m_food*` fields on items. A wrong name shows up as a compile error;
- creature balance, and spawn rates in play;
- how deep real Valheim ocean is around the chosen site. This decides how fully the island rises; the log reports the fraction of open ocean;
- how the vegetation densities look in game;
- performance.

## Known limitations

- **New worlds only.** Every player and the server need the mod with identical settings; the log prints a `signature` to compare. EWD syncs its YAML from the server.
- **No ores yet**, and the folklore materials have no recipes yet.
- **Stand-in creatures.** Clones look, move and attack exactly like their base, and a scaled creature's ragdoll drops back to normal size when it dies. Clones also carry the base's trophy drop.
- **Clients and server must match.** Jötunn enforces this: everyone needs Scotheim with the same minor version.
- **Map forest dots on the moor.** EWD shades a custom biome's map with its terrain biome (Meadows for the moor), so the map draws forest dots there even where there are no trees.
- **One water plane.** Valheim has a single sea level, so every loch sits at sea level.
- **Munros snow.** Mountain ground texture is snow-covered everywhere, not only on the tops.
- **Steep ground.** 0.2–1.7% of Highland land is steeper than 40° in the previews, mostly where the island meets vanilla islets.
- **Editing biome YAML.** Restart the world afterwards so biome IDs and terrain agree.

## Build and install

1. Install BepInEx 5 for Valheim, plus [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/) (required) and Expand World Data, on the server and every client. In `expand_world_data.cfg`, set `Spawn data = true`.
2. `dotnet build src/Scotheim -c Release -p:ValheimDir="<path to Valheim>"` (restores Jötunn's compile DLL from NuGet)
3. Copy `src/Scotheim/bin/Release/netstandard2.1/Scotheim.dll` to `<Valheim>/BepInEx/plugins/`.
4. Start the game once to generate `BepInEx/config/cavmkii.scotheim.cfg` and the EWD biome file, then create a new world. The log reports where the Highlands landed.

## Previewing changes

```sh
mcs -out:preview.exe tools/Preview/Preview.cs src/Scotheim/Terrain/*.cs
mono preview.exe out 12345 6000 700      # dir, seed, window (m), pixels; centred on the landmass
python3 tools/Preview/render.py out      # -> out/preview.png (needs numpy, Pillow)
```

The preview uses the defaults in `HighlandsSettings`, so edit those to try values.

Tests:

- Patches: `CSC=<Roslyn csc.exe> HARMONY_DIR=<HarmonyX + MonoMod dlls> tests/Harness/run.sh`
- Data names: `python3 tests/Data/check_data.py`
- Content against Jötunn: `CSC=<csc.exe> JOTUNN_DLL=<Jotunn.dll> UNITY_DIR=<UnityEngine.Modules/lib/net45> tests/Content/run.sh`
