# Scotheim

A BepInEx mod for Valheim that adds a Scottish Highlands landmass: a new island raised out of open ocean, with three biomes of its own. Vanilla land isn't touched.

![before/after, synthetic world](docs/preview.png)

*A synthetic, vanilla-like world (6 km across, north up) before and after. This is not a real seed; see [Status](#status).*

![three seeds](docs/preview-seeds.png)

## The three biomes

| Biome | Where | Terrain | Borrowed from vanilla (for now) |
|---|---|---|---|
| **Highland Moor** | Open ground below 50 m | Rolling relief, hummocks, lochans | Heights: Meadows. Ground texture: Plains heath. Vegetation: Meadows |
| **Caledonian Forest** | Glen floors and sheltered patches on low ground | Drumlins aligned NE–SW, hummocky moraine | Black Forest |
| **Munros** | Base height above 50 m | Rounded domes, NE-facing corries, soft-capped summits | Mountain |

The landscape between them:

- **Glens** are U-shaped troughs trending NE–SW, the Great Glen / Caledonian grain at about 040°. Each glen widens with its depth so walls stay near 35°.
- **Ribbon lochs** form where glen floors drop below sea level.
- **Sea lochs** form where glens reach the coast.
- The island is an ellipse along the grain, 4 × 2.3 km by default, with a ragged coast and NE–SW massifs rising from a ~28 m lowland. It comes to about 5 km² of new land, split roughly a third per biome.

## How it fits into the world

- **Placement.** The site is chosen from the world seed: the most open deep-ocean spot 5–8.5 km from the centre, clear of the Ashlands and Deep North. Every peer computes the same site. You can also set the position by hand.
- **Additive.** The island only rises out of water deeper than a few metres. Vanilla land and the water just off its beaches keep their vanilla biome and terrain. If a vanilla islet sits inside the footprint, the Highlands wrap around it.
- **Biomes.** Custom biomes come from [Expand World Data](https://thunderstore.io/c/valheim/p/JereKuusela/Expand_World_Data/), a soft dependency. Scotheim writes `BepInEx/config/expand_world/expand_biomes_scotheim.yaml` the first time it runs, and never overwrites it afterwards. It finds the biomes by their identifiers (`highland_moor`, `caledonian_forest`, `munros`). Without EWD, the island uses vanilla Meadows, Black Forest and Mountain.

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

Still unverified:

- that it builds against the real game;
- how deep real Valheim ocean is around the chosen site. This decides how fully the island rises; the log reports the fraction of open ocean;
- environment names (`Misty`, `LightRain`, `Heath clear`, `DeepForest Mist`, `Snow`, `SnowStorm`, …) and terrain colours in the YAML;
- performance.

## Known limitations

- **New worlds only.** Every player and the server need the mod with identical settings; the log prints a `signature` to compare. EWD syncs its YAML from the server.
- **Temporary vanilla spawns and vegetation.** `nature` borrows vanilla for now, so the Moor spawns Meadows plants (and probably Meadows creatures). Munros get Mountain vegetation, which likely includes silver. Custom vegetation, materials and enemies are the next step.
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
