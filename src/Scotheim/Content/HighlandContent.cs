using System;
using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace Scotheim
{
    // Clients and server must agree on the cloned prefabs, as they must on the terrain settings.
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public partial class Plugin
    {
        partial void RegisterContent() => Content.HighlandContent.Register();

        void Update()
        {
            Content.Bagging.Tick();
            Content.Midges.Tick();
        }
    }
}

namespace Scotheim.Content
{
    /// <summary>
    /// Highland creatures and items, cloned from vanilla prefabs with Jötunn. The clones only
    /// change names, stats, drops and breeding; models, animations and attacks are the vanilla
    /// base's. Where they spawn is set in expand_spawns_scotheim.yaml (Expand World Data), and how
    /// hard they hit there, since the spawn system is what sets a creature's damage factor.
    /// </summary>
    static class HighlandContent
    {
        internal static void Register()
        {
            Localization.Register();
            PrefabManager.OnVanillaPrefabsAvailable += AddItems;
            CreatureManager.OnVanillaCreaturesAvailable += AddCreatures;
        }

        // ---------------------------------------------------------------- items

        struct Food
        {
            public float Health, Stamina, Regen, Duration;
            public Food(float health, float stamina, float regen, float duration)
            { Health = health; Stamina = stamina; Regen = regen; Duration = duration; }
        }

        sealed class ItemSpec
        {
            public string Name, Base;
            public Food? Food;
            public RequirementConfig[] Recipe;
            public int Amount = 1; // items made per craft
            public string Station;
        }

        // Base prefabs lend the mesh and icon. Food values are set on the clone; Mistlands-tier
        // cooked meats sit around 50-60 health, 18-20 stamina, 4-5 regen, 30 minutes.
        static readonly ItemSpec[] Items =
        {
            new ItemSpec { Name = "Scot_Wool", Base = "LinenThread" },
            new ItemSpec { Name = "Scot_Mutton", Base = "RawMeat" },
            new ItemSpec { Name = "Scot_CookedMutton", Base = "CookedMeat", Food = new Food(55, 18, 4, 1800) },
            new ItemSpec { Name = "Scot_Beef", Base = "LoxMeat" },
            new ItemSpec { Name = "Scot_CookedBeef", Base = "CookedLoxMeat", Food = new Food(65, 22, 5, 1800) },
            new ItemSpec { Name = "Scot_Blaeberries", Base = "Blueberries", Food = new Food(15, 45, 1, 900) },
            new ItemSpec { Name = "Scot_HighlandHide", Base = "LoxPelt" },
            new ItemSpec { Name = "Scot_MartenPelt", Base = "DeerHide" },
            new ItemSpec { Name = "Scot_SithPelt", Base = "WolfPelt" },
            new ItemSpec { Name = "Scot_KelpieMane", Base = "JuteBlue" },
            new ItemSpec { Name = "Scot_WashersShroud", Base = "LinenThread" },
            new ItemSpec { Name = "Scot_GiantHeartstone", Base = "Crystal" },
            new ItemSpec { Name = "Scot_TrophyGreyMan", Base = "TrophyMorgen" },
            // Highland raw materials: gear is made from these and the drops above, nothing from other biomes.
            new ItemSpec { Name = "Scot_BogIronOre", Base = "IronOre" },
            new ItemSpec { Name = "Scot_BogIron", Base = "Iron" },
            // Hacksilver is scrap (like the Swamp's scrap iron); the smelter turns it into Pictish silver bars.
            new ItemSpec { Name = "Scot_Hacksilver", Base = "IronScrap" },
            new ItemSpec { Name = "Scot_PictishSilver", Base = "Silver" },
            new ItemSpec { Name = "Scot_BogOak", Base = "RoundLog" },
            new ItemSpec { Name = "Scot_Cairngorm", Base = "Crystal" },
            new ItemSpec { Name = "Scot_RowanWood", Base = "FineWood" },
            // Crofting and the still. Bere is the old Scottish barley; like vanilla barley it is its own seed.
            new ItemSpec { Name = "Scot_Bere", Base = "Barley" },
            new ItemSpec { Name = "Scot_Peat", Base = "Coal" },
            new ItemSpec
            {
                Name = "Scot_WhiskyWash", Base = "MeadBaseFrostResist", Station = "piece_cauldron",
                Recipe = new[] { new RequirementConfig("Scot_Bere", 10), new RequirementConfig("Scot_Peat", 2) },
            },
            new ItemSpec { Name = "Scot_UisgeBeatha", Base = "MeadFrostResist" },
            // Bog myrtle keeps midges off (Content/Midges.cs): gathered by lochs on the moor, made into a salve.
            new ItemSpec { Name = "Scot_BogMyrtle", Base = "Thistle" },
            new ItemSpec
            {
                Name = "Scot_MyrtleSalve", Base = "MeadPoisonResist", Station = "piece_cauldron", Amount = 3,
                Recipe = new[] { new RequirementConfig("Scot_BogMyrtle", 6), new RequirementConfig("Scot_Mutton", 1) },
            },
            // Foods (estimates for late-game food, not copied from the game).
            new ItemSpec
            {
                Name = "Scot_Haggis", Base = "Sausages", Food = new Food(85, 30, 6, 2400), Station = "piece_cauldron", Amount = 2,
                Recipe = new[] { new RequirementConfig("Scot_Mutton", 2), new RequirementConfig("Scot_Bere", 3) },
            },
            new ItemSpec
            {
                Name = "Scot_Cranachan", Base = "Salad", Food = new Food(35, 85, 4, 2400), Station = "piece_cauldron", Amount = 3,
                Recipe = new[] { new RequirementConfig("Raspberry", 4), new RequirementConfig("Scot_Bere", 2), new RequirementConfig("Scot_UisgeBeatha", 1) },
            },
            new ItemSpec
            {
                Name = "Scot_Bannock", Base = "Bread", Food = new Food(50, 50, 4, 1800), Station = "piece_cauldron", Amount = 2,
                Recipe = new[] { new RequirementConfig("Scot_Bere", 4) },
            },
            new ItemSpec
            {
                Name = "Scot_TartanCloth", Base = "JuteRed", Station = "piece_workbench", Amount = 2,
                Recipe = new[] { new RequirementConfig("Scot_Wool", 4), new RequirementConfig("Scot_Blaeberries", 2) },
            },
        };

        static void AddItems()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddItems;
            var added = new Dictionary<string, CustomItem>();
            foreach (var spec in Items)
            {
                try
                {
                    var config = new ItemConfig { Name = Localization.ItemName(spec.Name), Description = Localization.ItemDescription(spec.Name) };
                    if (spec.Recipe != null)
                    {
                        config.CraftingStation = spec.Station;
                        config.Requirements = spec.Recipe;
                        config.Amount = spec.Amount;
                    }
                    var item = new CustomItem(spec.Name, spec.Base, config);
                    if (item.ItemDrop == null)
                    {
                        Plugin.Log.LogWarning("Skipped item " + spec.Name + ": base prefab " + spec.Base + " not found.");
                        continue;
                    }
                    if (spec.Food.HasValue)
                    {
                        var shared = item.ItemDrop.m_itemData.m_shared;
                        shared.m_food = spec.Food.Value.Health;
                        shared.m_foodStamina = spec.Food.Value.Stamina;
                        shared.m_foodRegen = spec.Food.Value.Regen;
                        shared.m_foodBurnTime = spec.Food.Value.Duration;
                    }
                    Reskin.Item(spec.Name, item.ItemPrefab, item.ItemDrop.m_itemData.m_shared);
                    if (ItemManager.Instance.AddItem(item)) added[spec.Name] = item;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("Couldn't add item " + spec.Name + ": " + e);
                }
            }

            foreach (var station in new[] { "piece_cookingstation", "piece_cookingstation_iron" })
            {
                AddCooking(station, "Scot_Mutton", "Scot_CookedMutton", 25f);
                AddCooking(station, "Scot_Beef", "Scot_CookedBeef", 35f);
            }

            // Bog ore smelts like any iron ore, with charcoal from Highland wood.
            ItemManager.Instance.AddItemConversion(new CustomItemConversion(new SmelterConversionConfig
            {
                Station = "smelter", FromItem = "Scot_BogIronOre", ToItem = "Scot_BogIron",
            }));
            // Hacksilver melts down into bars, as the Picts recast Roman plate into their chains.
            ItemManager.Instance.AddItemConversion(new CustomItemConversion(new SmelterConversionConfig
            {
                Station = "smelter", FromItem = "Scot_Hacksilver", ToItem = "Scot_PictishSilver",
            }));

            Gear.Add();

            // Things to pick up in the Highlands, placed by expand_vegetation_scotheim.yaml.
            AddPickable(added, "Scot_BlaeberryBush", "BlueberryBush", "Scot_Blaeberries");
            AddPickable(added, "Scot_BogMyrtlePickable", "Pickable_Thistle", "Scot_BogMyrtle");
            AddPickable(added, "Scot_BogIronNodule", "Pickable_Stone", "Scot_BogIronOre");
            AddPickable(added, "Scot_BogOakPickable", "Pickable_Branch", "Scot_BogOak");
            AddPickable(added, "Scot_CairngormPickable", "Pickable_Flint", "Scot_Cairngorm");
            AddPickable(added, "Scot_RowanBranch", "Pickable_Branch", "Scot_RowanWood");
            AddPickable(added, "Scot_HacksilverCache", "Pickable_Stone", "Scot_Hacksilver");
            AddPickable(added, "Scot_PeatTurf", "Pickable_Stone", "Scot_Peat");
            AddPickable(added, "Scot_WildBere", "Pickable_Barley_Wild", "Scot_Bere");
            AddPickable(added, "Scot_Pickable_Bere", "Pickable_Barley", "Scot_Bere");
            Croft.Add(added);
            Midges.Add(added);
        }

        static void AddCooking(string station, string from, string to, float time)
        {
            ItemManager.Instance.AddItemConversion(new CustomItemConversion(new CookingConversionConfig
            {
                Station = station, FromItem = from, ToItem = to, CookTime = time,
            }));
        }

        // A copy of a vanilla pickable that yields a Highland item instead. Valheim's "blueberries" are
        // drawn like bilberries (Vaccinium myrtillus), which is what a blaeberry is, so the blaeberry bush
        // is a copy of the blueberry bush.
        static void AddPickable(Dictionary<string, CustomItem> added, string name, string basePrefab, string itemName)
        {
            CustomItem item;
            if (!added.TryGetValue(itemName, out item))
            {
                Plugin.Log.LogWarning("Skipped " + name + ": item " + itemName + " wasn't added.");
                return;
            }
            var clone = PrefabManager.Instance.CreateClonedPrefab(name, basePrefab);
            var pickable = clone != null ? clone.GetComponent<Pickable>() : null;
            if (pickable == null)
            {
                Plugin.Log.LogWarning("Skipped " + name + ": " + basePrefab + " or its Pickable not found.");
                return;
            }
            pickable.m_itemPrefab = item.ItemPrefab;
            PrefabManager.Instance.AddPrefab(clone);
        }

        // ------------------------------------------------------------ creatures

        sealed class CreatureSpec
        {
            public string Name, Base;
            public float Health, Scale = 1f;
            public DropConfig[] Drops;
            public string[] Food;
        }

        static DropConfig Drop(string item, int min, int max, float chance = 100f) =>
            new DropConfig { Item = item, MinAmount = min, MaxAmount = max, Chance = chance };

        // Vanilla health for comparison (from memory, not the game files): Deer 10, Boar 10, Lox 1000,
        // Hare 20, Wolf 80, Wraith 100, Goblin 70, Troll 600, Abomination 800, Stone Golem 800; Mistlands
        // Seeker 175, Seeker Soldier 1000. Base names, components and vanilla drops were checked against
        // Jötunn's generated character list for Valheim 1.0.7. Drops left null keep the base's table.
        static readonly CreatureSpec[] Creatures =
        {
            new CreatureSpec { Name = "Scot_RedDeer", Base = "Deer", Health = 90, Scale = 1.15f,
                Drops = new[] { Drop("DeerMeat", 2, 3), Drop("DeerHide", 2, 3) } },
            new CreatureSpec { Name = "Scot_Sheep", Base = "Boar", Health = 70,
                Drops = new[] { Drop("Scot_Wool", 2, 3), Drop("Scot_Mutton", 1, 2) },
                Food = new[] { "Scot_Blaeberries", "Blueberries", "Cloudberry", "Raspberry" } },
            new CreatureSpec { Name = "Scot_Lamb", Base = "Boar_piggy", Health = 20 },
            new CreatureSpec { Name = "Scot_HighlandCow", Base = "Lox", Health = 1400, Scale = 0.75f,
                Drops = new[] { Drop("Scot_Beef", 3, 5), Drop("Scot_HighlandHide", 2, 3) } },
            new CreatureSpec { Name = "Scot_HighlandCalf", Base = "Lox_Calf", Health = 300, Scale = 0.75f },
            new CreatureSpec { Name = "Scot_PineMarten", Base = "Hare", Health = 25, Scale = 0.8f,
                Drops = new[] { Drop("Scot_MartenPelt", 1, 1) } },
            new CreatureSpec { Name = "Scot_HillWolf", Base = "Wolf", Health = 220, Scale = 1.1f },
            new CreatureSpec { Name = "Scot_CuSith", Base = "Wolf", Health = 650, Scale = 1.45f,
                Drops = new[] { Drop("Scot_SithPelt", 1, 2), Drop("WolfFang", 1, 2) } },
            new CreatureSpec { Name = "Scot_CatSith", Base = "Ulv", Health = 450,
                Drops = new[] { Drop("Scot_SithPelt", 1, 1) } },
            new CreatureSpec { Name = "Scot_EachUisge", Base = "Abomination", Health = 2200, Scale = 0.9f,
                Drops = new[] { Drop("Scot_KelpieMane", 2, 3) } },
            new CreatureSpec { Name = "Scot_BeanNighe", Base = "Wraith", Health = 450, Scale = 0.85f,
                Drops = new[] { Drop("Scot_WashersShroud", 1, 1), Drop("Chain", 1, 1) } },
            new CreatureSpec { Name = "Scot_Fuath", Base = "Troll", Health = 1500 },
            new CreatureSpec { Name = "Scot_HillGiant", Base = "StoneGolem", Health = 2600, Scale = 1.25f,
                Drops = new[] { Drop("Scot_GiantHeartstone", 1, 1), Drop("Crystal", 8, 12) } },
            // Redcaps hoard scrap: hacksilver instead of the goblin's black metal scrap, and only rarely a bar.
            // The boss (Content/Boss.cs): summoned at the altar, never spawned. Morgen is Ashlands tier; the boss
            // sits between the Seeker Queen (Mistlands) and the Fader in health.
            new CreatureSpec { Name = "Scot_GreyMan", Base = "Morgen_NonSleeping", Health = 9000, Scale = 1.3f,
                Drops = new[] { Drop("Scot_TrophyGreyMan", 1, 1), Drop("Scot_Cairngorm", 4, 6), Drop("Scot_PictishSilver", 6, 10),
                    Drop("Scot_GiantHeartstone", 1, 2) } },
            // The Sluagh: the host of the restless dead, flying in from the west at night. Only comes in the raid
            // (expand_events_scotheim.yaml), once the Grey Man is dead.
            new CreatureSpec { Name = "Scot_Sluagh", Base = "Ghost", Health = 350, Scale = 1.2f,
                Drops = new[] { Drop("Coins", 3, 8, 50f), Drop("Scot_PictishSilver", 1, 1, 10f) } },
            new CreatureSpec { Name = "Scot_Redcap", Base = "Goblin", Health = 220, Scale = 0.8f,
                Drops = new[] { Drop("Scot_Hacksilver", 1, 3), Drop("Scot_PictishSilver", 1, 1, 10f), Drop("Coins", 10, 20, 50f) } },
        };

        static void AddCreatures()
        {
            CreatureManager.OnVanillaCreaturesAvailable -= AddCreatures;
            var added = new Dictionary<string, GameObject>();
            foreach (var spec in Creatures)
            {
                try
                {
                    var config = new CreatureConfig { Name = Localization.CreatureName(spec.Name) };
                    if (spec.Drops != null) config.DropConfigs = spec.Drops;
                    if (spec.Food != null) config.Consumables = spec.Food;
                    var creature = new CustomCreature(spec.Name, spec.Base, config);
                    var character = creature.Prefab != null ? creature.Prefab.GetComponent<Character>() : null;
                    if (character == null)
                    {
                        Plugin.Log.LogWarning("Skipped creature " + spec.Name + ": base prefab " + spec.Base + " not found.");
                        continue;
                    }
                    // Jötunn's clone constructor replaces the configured name with the prefab name, so the
                    // localised name has to be set again here (otherwise players see "Scot_CuSith").
                    character.m_name = Localization.CreatureName(spec.Name);
                    character.m_health = spec.Health;
                    if (spec.Scale != 1f) creature.Prefab.transform.localScale *= spec.Scale;
                    Reskin.Creature(spec.Name, creature.Prefab);
                    if (CreatureManager.Instance.AddCreature(creature)) added[spec.Name] = creature.Prefab;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("Couldn't add creature " + spec.Name + ": " + e);
                }
            }
            // Clones still breed into and grow up as the vanilla creature until relinked.
            Relink(added, "Scot_Sheep", "Scot_Lamb");
            Relink(added, "Scot_HighlandCow", "Scot_HighlandCalf");
            Places.Add(added);
            Bagging.Add();
            Flock.Add(added);
            GameObject boss;
            if (added.TryGetValue(GreyMan.Boss, out boss)) GreyMan.Add(boss);
        }

        static void Relink(Dictionary<string, GameObject> added, string adult, string young)
        {
            GameObject a, y;
            if (!added.TryGetValue(adult, out a) || !added.TryGetValue(young, out y)) return;
            var procreation = a.GetComponent<Procreation>();
            var growup = y.GetComponent<Growup>();
            if (procreation != null) procreation.m_offspring = y;
            if (growup != null) growup.m_grownPrefab = a;
            if (procreation == null || growup == null)
                Plugin.Log.LogWarning("Breeding for " + adult + " not linked (Procreation " + (procreation != null) + ", Growup " + (growup != null) + ").");
        }
    }
}
