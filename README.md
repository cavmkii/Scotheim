# Scotheim

A BepInEx mod for Valheim that adds a Scottish Highlands landmass: a new island raised out of open ocean, with three biomes of its own. Vanilla land isn't touched.

![before/after, synthetic world](docs/preview.png)

*A synthetic, vanilla-like world (6 km across, north up) before and after. This is not a real seed; see [Status](#status).*

![three seeds](docs/preview-seeds.png)

## The three biomes

| Biome | Where | Terrain | Vegetation | Weather |
|---|---|---|---|---|
| **Highland Moor** | The open lowland shelf (median slope ~4°) | Rolling relief, hummocks, lochans | Heath shrubs, birch copses, erratic boulders, cloudberries, rare standing stones | Mostly heath-clear and mist, some drizzle |
| **Caledonian Forest** | A fringe around the hills, glens through them, a few patches on the moor | Drumlins aligned NE–SW, hummocky moraine | Open Scots pine with birch, blaeberry and raspberry; no spruce; rare ruined shielings | Forest mist, some rain |
| **Munros** | The hill massifs, above ~32 m of base height | Rounded domes, NE-facing corries, soft-capped summits | Crags and scree; a few stunted pines and birches below ~70 m | Freezing snow and storms, with misty breaks |

Each biome borrows a vanilla height function and ground texture. The moor uses Meadows' grass, plus meadow grass, heath flowers and bracken as ground clutter. EWD's `nature` setting only affects farming, bees and footsteps: the Moor farms like the Plains (barley, flax), the forest like the Black Forest. Vegetation comes entirely from `expand_vegetation_scotheim.yaml`, so no vanilla plants leak in. There are no ores and no creatures yet; those are the next phase.

The landscape between them:

- **Glens** are U-shaped troughs trending NE–SW, the Great Glen / Caledonian grain at about 040°. Each glen widens with its depth so walls stay near 35°.
- **Ribbon lochs** form where glen floors drop below sea level.
- **Sea lochs** form where glens reach the coast.
- The island is an ellipse along the grain, 4 × 2.3 km by default, with a ragged coast. Most of it is a flat lowland shelf (~14 m) of open moor; a few separate NE–SW hill massifs rise out of it, wooded on their lower slopes. It comes to about 4–5 km² of new land: roughly two-thirds moor, a fifth Munros and the rest forest (set by `MassifThreshold`, `MunroMinHeight` and `ForestCover`).

## How it fits into the world

- **Placement.** The site is chosen from the world seed: the most open deep-ocean spot 5–8.5 km from the centre, clear of the Ashlands and Deep North. Every peer computes the same site. You can also set the position by hand.
- **Additive.** The island only rises out of water deeper than a few metres. Vanilla land and the water just off its beaches keep their vanilla biome and terrain. If a vanilla islet sits inside the footprint, the Highlands wrap around it.
- **Biomes.** Custom biomes come from [Expand World Data](https://thunderstore.io/c/valheim/p/JereKuusela/Expand_World_Data/), a soft dependency. On first run Scotheim writes `expand_biomes_scotheim.yaml`, `expand_vegetation_scotheim.yaml` and `expand_clutter_scotheim.yaml` into `BepInEx/config/expand_world/`, and never overwrites them afterwards, so edits stick. The sources are in `src/Scotheim/Data/`. Scotheim finds the biomes by their identifiers (`highland_moor`, `caledonian_forest`, `munros`). Without EWD, the island uses vanilla Meadows, Black Forest and Mountain, with their vanilla vegetation and creatures.

## Status

**Not yet run in the game.**

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

Still unverified:

- that it builds against the real game;
- how deep real Valheim ocean is around the chosen site. This decides how fully the island rises; the log reports the fraction of open ocean;
- how the vegetation densities look in game;
- performance.

## Known limitations

- **New worlds only.** Every player and the server need the mod with identical settings; the log prints a `signature` to compare. EWD syncs its YAML from the server.
- **No creatures or ores yet.** With custom biomes, nothing spawns on the island until the enemies and materials phase.
- **Map forest dots on the moor.** EWD shades a custom biome's map with its terrain biome (Meadows for the moor), so the map draws forest dots there even where there are no trees.
- **One water plane.** Valheim has a single sea level, so every loch sits at sea level.
- **Munros snow.** Mountain ground texture is snow-covered everywhere, not only on the tops.
- **Steep ground.** 0.2–1.7% of Highland land is steeper than 40° in the previews, mostly where the island meets vanilla islets.
- **Editing biome YAML.** Restart the world afterwards so biome IDs and terrain agree.

## Build and install

1. Install BepInEx 5 for Valheim, plus Expand World Data on the server and every client.
2. `dotnet build src/Scotheim -c Release -p:ValheimDir="<path to Valheim>"`
3. Copy `src/Scotheim/bin/Release/netstandard2.1/Scotheim.dll` to `<Valheim>/BepInEx/plugins/`.
4. Start the game once to generate `BepInEx/config/cavmkii.scotheim.cfg` and the EWD biome file, then create a new world. The log reports where the Highlands landed.

## Previewing changes

```sh
mcs -out:preview.exe tools/Preview/Preview.cs src/Scotheim/Terrain/*.cs
mono preview.exe out 12345 6000 700      # dir, seed, window (m), pixels; centred on the landmass
python3 tools/Preview/render.py out      # -> out/preview.png (needs numpy, Pillow)
```

The preview uses the defaults in `HighlandsSettings`, so edit those to try values.

Patch tests: `CSC=<Roslyn csc.exe> HARMONY_DIR=<HarmonyX + MonoMod dlls> tests/Harness/run.sh`
