# Scotheim

A Valheim mod that raises the Scottish Highlands out of the sea: four islands of open moor, Caledonian pinewood and rounded hills, with their own wildlife, folklore, crafts and a hill-walking challenge. It's late-game content, pitched alongside the Mistlands.

This page is the player's guide. It explains how things work without giving away what you'll find. Everything is spelled out in the [full reference](docs/DEVELOPMENT.md), which **contains spoilers**.

![The Highlands on a synthetic world map](docs/preview-world.png)

*Where the islands sit on a synthetic, vanilla-like world (not a real seed).*

## Contents

- [Installing](#installing)
- [Finding the Highlands](#finding-the-highlands)
- [The land](#the-land)
- [Surviving the Highlands](#surviving-the-highlands)
- [Animals and husbandry](#animals-and-husbandry)
- [Gathering](#gathering)
- [Processing and crafting](#processing-and-crafting)
- [Places to explore](#places-to-explore)
- [Munro bagging](#munro-bagging)
- [Things best discovered](#things-best-discovered)
- [Settings](#settings)
- [Troubleshooting](#troubleshooting)
- [Known limitations](#known-limitations)
- [Where the ideas come from](#where-the-ideas-come-from)
- [For developers](#for-developers)

## Installing

**You need:**

- BepInEx 5 for Valheim
- [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/), the modding library
- [Expand World Data](https://thunderstore.io/c/valheim/p/JereKuusela/Expand_World_Data/) (tested with 1.73). It supplies the custom biomes. Without it the islands still appear, but as plain Meadows, Black Forest and Mountain with vanilla plants and creatures.
- Scotheim.dll

**With a mod manager** (Vortex, r2modman): install BepInEx, Jötunn and Expand World Data through it, then add Scotheim (a zip of `Scotheim.dll`).

**By hand:** put the DLLs in `Valheim/BepInEx/plugins/`.

**First launch.** Start the game once, quit, and start it again before making a world. On that first start Scotheim switches on the Expand World Data setting its creatures need, and Expand World Data only picks it up on the next start.

**Use a new world.** The islands are built when the world is generated. In an existing world, only ground nobody has explored yet would change, and it wouldn't line up with what's already there.

**Updating.** Replace the DLL. Terrain changes in an update only show in new worlds. Nothing in the config needs touching.

**Multiplayer.** The server and every player need the same Scotheim build. Expand World Data shares its data from the server, so players don't have to copy it.

## Finding the Highlands

There are four islands, the largest about 4 km long, 4.5–8.5 km out from the centre of the world, in the same ring as the Mistlands. They're placed in the most open stretches of sea in that ring, clear of the Ashlands and the Deep North, and each runs roughly north-east to south-west. Take a boat.

The game log (`BepInEx/LogOutput.log`) gives each island's position when the world loads, as a line like `Highlands island 1/4 at (x, y)`, if you'd rather not search.

## The land

The islands have three biomes of their own.

**Highland Moor.** The open lowland that makes up most of each island: rolling ground under heather, with lochans in the hollows and a couple of large moor lochs per island. Birch copses, boulders and the odd standing stone. Mist and drizzle more often than not.

**Caledonian Forest.** Open Scots pine and birch around the foot of the hills and up the glens, with heather in the clearings and berries underfoot. Damp and misty.

**Munros.** The hills: rounded summits with corries on their north-east sides. Pinewood on the lower slopes gives way to heather and scrub, then to bare heath and grass on the tops. Cold, often snowing, often in cloud.

Between them:

- **Glens** run north-east to south-west through the hills, following the grain of the real Highlands.
- **Lochs** lie in the deeper glens and run out to the sea as sea lochs.
- **Loch shores** have a flat strand at the water's edge. A lot of the moor's useful things grow or form there, so lochs are worth finding.

## Surviving the Highlands

- **Cold.** The Munros freeze. Bring frost resistance, or make your own (see the still, below).
- **Midges.** On the moor and in the forest, on calm days around dawn and dusk, midges bite: your stamina comes back more slowly. Wind or a fire's smoke keeps them off, and so does a salve made from a plant that grows by the water.
- **The night.** The Highlands are dangerous at roughly Mistlands level, and more so after dark. Much of what hunts there comes from Gaelic folklore. Loch shores at night deserve particular care.

## Animals and husbandry

- **Blackface sheep** graze the moor in small flocks. They're harmless: hit one and it runs. Feed them berries to tame them. A tamed sheep that's fed sheds wool every so often, and tamed sheep breed lambs.
- **Highland cattle** graze in small herds. They never start a fight, but they defend themselves, and they're big.
- **Red deer** and **pine martens** are there to hunt.

The grazing animals leave each other alone, tamed or not.

## Gathering

| Resource | Where to look |
|---|---|
| **Bog iron ore** | Orange nodules along loch and lochan shores, and rusty deposits on those shores (mine them with a pickaxe). Rare away from the water. |
| **Peat** | Turves on the open moor, and black peat hags (break them for several at once) |
| **Bog oak** | Black branches lying on the open moor |
| **Rowan wood** | Branches in the Caledonian Forest |
| **Bere** | Wild patches on the moor. Bere is the old barley of the north, and like barley its grain is its own seed. |
| **Blaeberries** | Bushes in the forest and on the hills |
| **Bog myrtle** | Clumps by loch and bog edges on the low moor |
| **Rarer things** | Some materials turn up only high in the hills, in old ruins, or on particular creatures. |

## Processing and crafting

- **Bog iron.** Roast the ore into **sinter** in the smelter, then run the sinter down to **bog iron** bars in the blast furnace.
- **Peat.** Cut peat is wet. Build a **peat stack** (hammer, near a workbench) and stack peat in it to dry. It needs no fuel, but it's slow.
- **Uisge-beatha.** Mash bere with dried peat at the cauldron to make a whisky wash, then ferment it. A dram keeps the cold out, and it does a bit more besides.
- **Moss bandage.** Dried peat and wool at the workbench. It gives a quick heal now and a little more over a few seconds. It shares the healing meads' cooldown, so it can't be stacked with them.
- **Bog myrtle salve.** Brewed at the cauldron. Keeps the midges off for a good while.
- **Tartan cloth.** Wool, dyed with blaeberries, at the workbench.
- **Highland food.** Haggis, cranachan and bannock, made at the cauldron.
- **Bere** can be planted with the cultivator on the open moor.

**Gear.** There are four armour sets, each with a set bonus suited to a different way of fighting, plus capes and weapons for every skill. A few legendaries need rare materials. They're made at the black forge and the galdr table, and the recipes use only what the Highlands provide.

## Places to explore

Some ruins hold loot and some hold trouble. The Highlands have:

- **Brochs.** Ruined Iron Age drystone towers.
- **Crannogs.** Timber dwellings built out over the lochs.
- **Shielings.** Roofless huts from the summer grazing.
- **Stone circles** on the moor.
- **Pictish symbol stones.** They glow like vanilla runestones. Read them: they know things about the Highlands, and some of them mark places on your map.
- **Summit cairns** on top of the Munros (see below).

Places only appear in ground generated after the mod was installed.

## Munro bagging

In Scotland a "Munro" is a mountain over 3,000 feet, and "bagging" is climbing them to tick them off. Here, each Munro has a small cairn of stones on its top. Use it to add your stone and bag the hill.

Each character keeps their own tally for each world, and each hill counts once. Bag twelve, or every cairn in a world that has fewer, and you become a **Compleatist**, a title that comes with something useful for anyone who spends time on the hills.

## Things best discovered

Something walks the high tops. The symbol stones say more than this page will. If you want everything spelled out, the [full reference](docs/DEVELOPMENT.md) has it.

## Settings

You don't need to edit any config file.

- Scotheim switches on Expand World Data's **Spawn data** (the Highland creatures need it) and keeps its **Event data** off, because that setting breaks Expand World Data 1.73 on the current game. Both are written to `expand_world_data.cfg`, and the log says when one was changed.
- With Spawn data on, Expand World Data writes the game's own spawns to `expand_spawns.yaml`. Its version 1.73 leaves four Fimbulvinter entries without a biome, which would make them spawn everywhere. Scotheim fixes those four entries whenever the file is written and leaves the rest alone.
- `BepInEx/config/cavmkii.scotheim.cfg` lists every terrain setting, but they're ignored unless you set `CustomTerrain = true`. That's for tinkering. Players sharing a world must then use identical settings.
- Scotheim writes its own Expand World Data files into `BepInEx/config/expand_world/`. If you edit one, your edit is kept, and a later update writes its new version beside it as `*.yaml.new` for you to merge by hand.

## Troubleshooting

| Problem | What to do |
|---|---|
| No Highland creatures | Restart the game once (see First launch), then use a new world. |
| Meteors or Jotuns everywhere | Look in the log for `Gave 4 Fimbulvinter spawns … "biome: None"`. If it isn't there, open `BepInEx/config/expand_world/expand_spawns.yaml` and add `biome: None` to the four `Fimbulvinter - …` entries. |
| The islands are there but look like vanilla Meadows, Black Forest and Mountain | Expand World Data is missing or failed to start. Check the log for its errors, and that its `Event data` setting is off. |
| No ruins or cairns where you're standing | That ground was generated before Scotheim was installed. Use a new world, or Expand World Data's `genloc` command for unexplored ground. |
| Floating or buried trees and rocks in multiplayer | Someone has a different Scotheim build. Each log prints `Terrain signature: …` at startup, and these must match. |
| Something else | Send `BepInEx/LogOutput.log`, with your Steam ID and character names removed. Scotheim logs what it did, and any game detail it couldn't apply shows as a `Game field check … skipped` line. |

## Known limitations

- **Stand-in looks.** Creatures and gear are recoloured copies of vanilla ones, with vanilla models, animations and attacks, until proper models exist. A sheep is a pale boar, and a Highland cow is a ginger lox.
- **Map.** The game's map draws forest dots on the moor, because Expand World Data shades it like the Meadows.
- **Snow.** The Munros' ground texture is snowy all the way down, not just on the tops.
- **Coasts.** Where an island's site already had vanilla islets near its coast, those are left as they were, so some coasts are ragged.
- **One sea level.** Valheim has a single water level, so every loch and lochan sits at sea level.

## Where the ideas come from

The mod leans on real Highland geography, history and folklore. Some background, none of it spoilers:

- **The land.** Glens follow the north-east grain of the Great Glen fault. Corries face north-east, as most Scottish corries do. Moor lochs and lochans fill hollows in the blanket bog.
- **Bog iron.** Medieval Highland iron was mostly smelted from bog ore (limonite), which forms in wet, peaty ground and was roasted before smelting.
- **Peat.** Peat was cut and stacked to dry in the wind, and it smokes the malt for whisky. Dried sphagnum moss was a real wound dressing, gathered in Scotland by the ton during the First World War.
- **Bog myrtle and midges.** The Highland midge bites in still, damp air at dawn and dusk. Bog myrtle has long been rubbed on to keep it off, and it's still in commercial repellents.
- **Bere** is a six-row barley still grown in Orkney and the Western Isles.
- **Munros** are named after Sir Hugh Munro, whose 1891 list started the habit of bagging them. Someone who has climbed them all is a Compleatist.
- **The places.** Brochs are Iron Age towers like Mousa in Shetland. Crannogs are loch dwellings like those excavated on Loch Tay. Shielings were summer huts on the hill grazing. The stone circles are modelled on Callanish and Clava. The symbol stones follow Pictish Class I stones.

## For developers

How to build, test and preview the terrain, how each system works, and the full content reference with spoilers: [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).
