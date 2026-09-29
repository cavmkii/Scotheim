using System;
using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Scotheim.Content
{
    /// <summary>
    /// Highland weapons and armour. Each piece is a Jötunn clone of a vanilla item that looks right
    /// (model, icon, animations and attacks), with its numbers copied from an Ashlands item (the
    /// "donor") and scaled. Balance therefore follows the game's own Ashlands tier instead of figures
    /// typed in here; where Ashlands has no such weapon, a Mistlands donor is scaled up.
    /// Recipes use only what the Highlands yield: their own drops and raw materials (bog iron, bog oak,
    /// hacksilver, cairngorm, rowan) plus vanilla items Highland creatures and trees drop.
    /// </summary>
    static class Gear
    {
        sealed class GearSpec
        {
            public string Name, Look, Donor;
            public float Damage = 1f, Armor = 1f, Weight = 1f, Durability = 1f, Block = 1f, Force = 1f;
            public string Set; // id of the armour set this piece belongs to (see Sets)
            public string MovementFrom; // take this vanilla item's movement bonus, if it has one
            public string EquipEffect; // id of a status effect worn with the item (see Sets)
            public Action<ItemDrop.ItemData.SharedData> Tweak;
            public RequirementConfig[] Recipe;
            public string Station = Forge;
        }

        static RequirementConfig Req(string item, int amount, int perLevel) => new RequirementConfig(item, amount, perLevel);

        const string Forge = "blackforge";
        const string Galdr = "piece_magetable";

        // Folds fire, frost, lightning and poison (and spirit, when going to slash) into one damage type.
        static void AllElementalTo(ref HitData.DamageTypes d, bool spirit)
        {
            var elemental = d.m_fire + d.m_frost + d.m_lightning + d.m_poison + (spirit ? 0f : d.m_spirit);
            d.m_fire = d.m_frost = d.m_lightning = d.m_poison = 0f;
            if (spirit) d.m_spirit += elemental;
            else { d.m_spirit = 0f; d.m_slash += elemental; }
        }

        static void ToSlash(ItemDrop.ItemData.SharedData s) { AllElementalTo(ref s.m_damages, false); AllElementalTo(ref s.m_damagesPerLevel, false); }
        static void ToSpirit(ItemDrop.ItemData.SharedData s) { AllElementalTo(ref s.m_damages, true); AllElementalTo(ref s.m_damagesPerLevel, true); }

        static readonly GearSpec[] Items =
        {
            // --- Clansman weapons (Freedom: swords, axes, clubs)
            new GearSpec { Name = "Scot_Claymore", Look = "THSwordKrom", Donor = "THSwordSlayer",
                Recipe = new[] { Req("Scot_BogIron", 24, 12), Req("Scot_HighlandHide", 5, 3), Req("Scot_TartanCloth", 2, 1), Req("WolfFang", 2, 1) } },
            // After the 1.63 m two-hander at the Wallace Monument, Stirling (its attribution is traditional).
            new GearSpec { Name = "Scot_WallaceSword", Look = "THSwordKrom", Donor = "THSwordSlayer", Damage = 1.2f, Weight = 1.5f, Durability = 1.5f,
                Tweak = s => s.m_movementModifier -= 0.05f,
                Recipe = new[] { Req("Scot_BogIron", 32, 16), Req("Scot_GiantHeartstone", 2, 1), Req("Scot_Cairngorm", 1, 0), Req("Scot_TartanCloth", 4, 2), Req("Scot_HighlandHide", 5, 3) } },
            // The claidheamh soluis of J. F. Campbell's Popular Tales of the West Highlands (1860-62), usually won
            // from a giant. All the donor's elemental damage becomes spirit.
            new GearSpec { Name = "Scot_ClaidheamhSoluis", Look = "SwordMistwalker", Donor = "SwordNiedhogg", Damage = 1.1f, Tweak = ToSpirit,
                Recipe = new[] { Req("Scot_BogIron", 16, 8), Req("Scot_GiantHeartstone", 3, 1), Req("Scot_Cairngorm", 1, 0), Req("Scot_SithPelt", 4, 2) } },
            // Robert the Bruce's axe at Bannockburn (1314): one blow killed Henry de Bohun and broke the shaft
            // (Barbour, The Brus, 1370s). Plain steel: elemental damage becomes slash.
            new GearSpec { Name = "Scot_BruceAxe", Look = "AxeJotunBane", Donor = "AxeBerzerkr", Damage = 1.15f, Tweak = ToSlash,
                Recipe = new[] { Req("Scot_BogIron", 20, 10), Req("Scot_GiantHeartstone", 2, 1), Req("Scot_Cairngorm", 1, 0), Req("Scot_BogOak", 4, 2), Req("Scot_HighlandHide", 3, 1) } },
            // The long-hafted axe of the Hebridean galloglass. Ashlands has no two-handed axe, so Skullsplittur
            // (Mistlands) is scaled up, as is Demolisher for the caber.
            new GearSpec { Name = "Scot_Sparth", Look = "BattleaxeBlackmetal", Donor = "BattleaxeSkullSplittur", Damage = 1.3f, Tweak = ToSlash,
                Recipe = new[] { Req("Scot_BogIron", 18, 9), Req("Scot_BogOak", 8, 4), Req("Scot_HighlandHide", 3, 1) } },
            // Highland Games, not war. It knocks things a long way.
            new GearSpec { Name = "Scot_Caber", Look = "SledgeStagbreaker", Donor = "SledgeDemolisher", Damage = 1.3f, Force = 1.5f,
                Recipe = new[] { Req("Scot_BogOak", 30, 15), Req("Scot_BogIron", 4, 2), Req("Scot_HighlandHide", 4, 2) } },

            // --- Man-at-arms weapons (Schiltron: blocking, polearms, bows)
            // No Ashlands atgeir exists, so Himminafl is scaled up; its lightning becomes slash.
            new GearSpec { Name = "Scot_LochaberAxe", Look = "AtgeirBlackmetal", Donor = "AtgeirHimminAfl", Damage = 1.3f, Tweak = ToSlash,
                Recipe = new[] { Req("Scot_BogOak", 10, 5), Req("Scot_BogIron", 18, 9), Req("Scot_HighlandHide", 2, 1) } },
            // Ettrick Forest's archers fought at Falkirk (1298).
            new GearSpec { Name = "Scot_EttrickBow", Look = "BowHuntsman", Donor = "BowAshlands", Tweak = ToSlash,
                Recipe = new[] { Req("Scot_BogOak", 12, 6), Req("Scot_KelpieMane", 2, 1), Req("DeerHide", 4, 2) } },
            new GearSpec { Name = "Scot_Targe", Look = "ShieldBanded", Donor = "ShieldFlametal",
                Recipe = new[] { Req("Scot_BogOak", 10, 5), Req("Scot_BogIron", 8, 4), Req("Scot_HighlandHide", 4, 2), Req("Scot_TartanCloth", 1, 1) } },
            // No Ashlands buckler exists, so the Carapace buckler's block is scaled up.
            new GearSpec { Name = "Scot_PictishShield", Look = "ShieldBronzeBuckler", Donor = "ShieldCarapaceBuckler", Block = 1.3f,
                Recipe = new[] { Req("Scot_BogOak", 8, 4), Req("Scot_Hacksilver", 6, 3), Req("Scot_HighlandHide", 3, 1) } },

            // --- Pictish weapons (Woad: spears, knives, crossbows). Crossbows are carved on Pictish stones
            // (St Vigeans, Shandwick, Glenferness).
            new GearSpec { Name = "Scot_PictishSpear", Look = "SpearBronze", Donor = "SpearSplitner", Tweak = ToSlash,
                Recipe = new[] { Req("Scot_BogOak", 10, 5), Req("Scot_BogIron", 6, 3), Req("Scot_Hacksilver", 4, 2) } },
            new GearSpec { Name = "Scot_PictishCrossbow", Look = "CrossbowArbalest", Donor = "CrossbowRipper", Tweak = ToSlash,
                Recipe = new[] { Req("Scot_BogOak", 10, 5), Req("Scot_BogIron", 8, 4), Req("Scot_KelpieMane", 1, 1), Req("Scot_MartenPelt", 2, 1), Req("Scot_Hacksilver", 2, 1) } },
            // No Ashlands knife exists, so Skoll and Hati is scaled up.
            new GearSpec { Name = "Scot_Dirk", Look = "KnifeBlackMetal", Donor = "KnifeSkollAndHati", Damage = 1.4f,
                Recipe = new[] { Req("Scot_BogIron", 10, 5), Req("Scot_BogOak", 2, 1), Req("Scot_MartenPelt", 2, 1) } },

            // --- Sìth staves (Glamour: elemental and blood magic). Made at the galdr table.
            // The Cailleach Bheur, the winter hag who shaped the hills. No Ashlands frost staff exists.
            new GearSpec { Name = "Scot_CailleachStaff", Look = "StaffIceShards", Donor = "StaffIceShards", Damage = 1.4f, Station = Galdr,
                Recipe = new[] { Req("Scot_RowanWood", 15, 8), Req("Scot_SithPelt", 3, 1), Req("Scot_Cairngorm", 1, 1), Req("Crystal", 6, 3) } },
            // After the Brahan Seer's stone. A protective ward; its strength comes from Blood magic skill.
            new GearSpec { Name = "Scot_SeerStone", Look = "StaffShield", Donor = "StaffShield", Station = Galdr,
                Recipe = new[] { Req("Scot_RowanWood", 15, 8), Req("Scot_WashersShroud", 3, 1), Req("Scot_Cairngorm", 1, 1) } },

            // --- Pictish set: light and quick. Each piece also takes the matching Fenris (werewolf) piece's movement speed.
            new GearSpec { Name = "Scot_PictishChain", Look = "HelmetDverger", Donor = "HelmetAshlandsMediumHood", Armor = 0.75f, Weight = 0.5f, Set = "pictish", MovementFrom = "HelmetFenring",
                Tweak = s => s.m_movementModifier = 0f,
                Recipe = new[] { Req("Scot_Hacksilver", 16, 8), Req("Scot_GiantHeartstone", 1, 0) } },
            new GearSpec { Name = "Scot_PictishJerkin", Look = "ArmorLeatherChest", Donor = "ArmorAshlandsMediumChest", Armor = 0.75f, Weight = 0.5f, Set = "pictish", MovementFrom = "ArmorFenringChest",
                Tweak = s => s.m_movementModifier = 0f,
                Recipe = new[] { Req("Scot_HighlandHide", 10, 5), Req("Scot_Hacksilver", 4, 2), Req("Scot_MartenPelt", 2, 1) } },
            new GearSpec { Name = "Scot_PictishTrews", Look = "ArmorLeatherLegs", Donor = "ArmorAshlandsMediumlegs", Armor = 0.75f, Weight = 0.5f, Set = "pictish", MovementFrom = "ArmorFenringLegs",
                Tweak = s => s.m_movementModifier = 0f,
                Recipe = new[] { Req("Scot_Wool", 10, 5), Req("Scot_HighlandHide", 6, 3), Req("Scot_Hacksilver", 2, 1) } },

            // --- Clansman set ("Braveheart"): the film's look, not the 1290s.
            new GearSpec { Name = "Scot_BlueBonnet", Look = "HelmetLeather", Donor = "HelmetAshlandsMediumHood", Armor = 0.9f, Weight = 0.6f, Set = "clansman",
                Tweak = s => s.m_movementModifier = 0f,
                Recipe = new[] { Req("Scot_Wool", 10, 5), Req("Scot_Blaeberries", 4, 2), Req("Scot_HighlandHide", 2, 1) } },
            new GearSpec { Name = "Scot_Leine", Look = "ArmorRagsChest", Donor = "ArmorAshlandsMediumChest", Armor = 0.9f, Weight = 0.6f, Set = "clansman",
                Tweak = s => s.m_movementModifier = 0f,
                Recipe = new[] { Req("Scot_Wool", 12, 6), Req("Scot_HighlandHide", 8, 4) } },
            new GearSpec { Name = "Scot_Kilt", Look = "ArmorRagsLegs", Donor = "ArmorAshlandsMediumlegs", Armor = 0.9f, Weight = 0.6f, Set = "clansman",
                Tweak = s => s.m_movementModifier = 0f,
                Recipe = new[] { Req("Scot_TartanCloth", 8, 4), Req("Scot_HighlandHide", 6, 3) } },

            // --- Man-at-arms set: Wars of Independence steel. Either chest piece counts.
            new GearSpec { Name = "Scot_Knapskull", Look = "HelmetPadded", Donor = "HelmetFlametal", Set = "manatarms",
                Recipe = new[] { Req("Scot_BogIron", 16, 8), Req("Scot_MartenPelt", 2, 1), Req("Scot_HighlandHide", 2, 1) } },
            new GearSpec { Name = "Scot_Brigandine", Look = "ArmorIronChest", Donor = "ArmorFlametalChest", Set = "manatarms",
                Recipe = new[] { Req("Scot_BogIron", 20, 10), Req("Scot_TartanCloth", 4, 2), Req("Scot_HighlandHide", 4, 2), Req("TrollHide", 3, 1) } },
            // Quilted and light: the Ask chest's numbers, no movement penalty.
            new GearSpec { Name = "Scot_Acton", Look = "ArmorPaddedCuirass", Donor = "ArmorAshlandsMediumChest", Set = "manatarms",
                Tweak = s => s.m_movementModifier = 0f,
                Recipe = new[] { Req("Scot_TartanCloth", 10, 5), Req("Scot_Wool", 10, 5), Req("Scot_HighlandHide", 6, 3) } },
            new GearSpec { Name = "Scot_Chausses", Look = "ArmorIronLegs", Donor = "ArmorFlametalLegs", Set = "manatarms",
                Recipe = new[] { Req("Scot_BogIron", 20, 10), Req("Scot_HighlandHide", 4, 2), Req("Chain", 2, 1) } },

            // --- Sìth set: faerie glamour, for eitr magic. Made at the galdr table.
            new GearSpec { Name = "Scot_SithCrown", Look = "HelmetMidsummerCrown", Donor = "HelmetMage_Ashlands", Set = "sith", Station = Galdr,
                Recipe = new[] { Req("Scot_RowanWood", 8, 4), Req("Scot_Blaeberries", 6, 3), Req("Scot_SithPelt", 2, 1), Req("Scot_Cairngorm", 1, 0) } },
            new GearSpec { Name = "Scot_SithRobe", Look = "ArmorMageChest", Donor = "ArmorMageChest_Ashlands", Set = "sith", Station = Galdr,
                Recipe = new[] { Req("Scot_SithPelt", 4, 2), Req("Scot_KelpieMane", 4, 2), Req("Scot_WashersShroud", 2, 1), Req("Scot_Wool", 10, 5) } },
            new GearSpec { Name = "Scot_SithLeggings", Look = "ArmorMageLegs", Donor = "ArmorMageLegs_Ashlands", Set = "sith", Station = Galdr,
                Recipe = new[] { Req("Scot_SithPelt", 3, 1), Req("Scot_KelpieMane", 2, 1), Req("Scot_Wool", 8, 4) } },

            // --- Capes: Ashlands (Ash cape) numbers, each with its own twist.
            new GearSpec { Name = "Scot_PictishCloak", Look = "CapeDeerHide", Donor = "CapeAsh",
                Tweak = s => s.m_movementModifier += 0.05f,
                Recipe = new[] { Req("Scot_MartenPelt", 8, 4), Req("Scot_Hacksilver", 2, 1) } },
            new GearSpec { Name = "Scot_BeltedPlaid", Look = "CapeTrollHide", Donor = "CapeAsh", EquipEffect = "plaid",
                Recipe = new[] { Req("Scot_TartanCloth", 10, 5), Req("Scot_Wool", 4, 2) } },
            new GearSpec { Name = "Scot_SaltireCape", Look = "CapeLinen", Donor = "CapeAsh",
                Recipe = new[] { Req("Scot_Wool", 10, 5), Req("Scot_TartanCloth", 2, 1), Req("Scot_Blaeberries", 4, 2) } },
            // The Bratach Sìth of the MacLeods, kept at Dunvegan; said to bring victory when unfurled.
            // Keeps the feather cape's look, numbers and slow fall; adds resistance to pierce.
            new GearSpec { Name = "Scot_FairyFlag", Look = "CapeFeather", Donor = "CapeFeather", Station = Galdr,
                Tweak = s => s.m_damageModifiers.Add(new HitData.DamageModPair { m_type = HitData.DamageType.Pierce, m_modifier = HitData.DamageModifier.Resistant }),
                Recipe = new[] { Req("Scot_WashersShroud", 3, 1), Req("Scot_SithPelt", 2, 1), Req("Scot_Wool", 10, 5), Req("Scot_Blaeberries", 4, 2) } },
        };

        internal static void Add()
        {
            var effects = Sets.AddEffects();
            foreach (var spec in Items)
            {
                try
                {
                    var donorObject = PrefabManager.Cache.GetPrefab<GameObject>(spec.Donor);
                    var donor = donorObject != null ? donorObject.GetComponent<ItemDrop>() : null;
                    if (donor == null)
                    {
                        Plugin.Log.LogWarning("Skipped " + spec.Name + ": stat donor " + spec.Donor + " not found.");
                        continue;
                    }
                    var item = new CustomItem(spec.Name, spec.Look, new ItemConfig
                    {
                        Name = Localization.ItemName(spec.Name),
                        Description = Localization.ItemDescription(spec.Name),
                        CraftingStation = spec.Station,
                        Requirements = spec.Recipe,
                    });
                    if (item.ItemDrop == null)
                    {
                        Plugin.Log.LogWarning("Skipped " + spec.Name + ": base prefab " + spec.Look + " not found.");
                        continue;
                    }
                    var s = item.ItemDrop.m_itemData.m_shared;
                    CopyStats(donor.m_itemData.m_shared, s, spec);
                    if (spec.Tweak != null) spec.Tweak(s);
                    if (spec.MovementFrom != null) BorrowMovement(spec, s);
                    StatusEffect effect;
                    if (spec.EquipEffect != null && effects.TryGetValue(spec.EquipEffect, out effect))
                        s.m_equipStatusEffect = effect;
                    StatusEffect bonus;
                    if (spec.Set != null && effects.TryGetValue(spec.Set, out bonus))
                    {
                        s.m_setName = "scot_" + spec.Set;
                        s.m_setSize = Sets.Size;
                        s.m_setStatusEffect = bonus;
                    }
                    ItemManager.Instance.AddItem(item);
                    Plugin.Log.LogInfo(Describe(spec, s));
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("Couldn't add " + spec.Name + ": " + e);
                }
            }
        }

        // One log line per item with its final numbers, so balance can be checked against the real game.
        static string Describe(GearSpec spec, ItemDrop.ItemData.SharedData s)
        {
            var d = s.m_damages;
            var parts = new List<string>();
            Action<string, float> add = (label, v) => { if (v != 0f) parts.Add(label + " " + v.ToString("0.#")); };
            add("blunt", d.m_blunt); add("slash", d.m_slash); add("pierce", d.m_pierce); add("fire", d.m_fire);
            add("frost", d.m_frost); add("lightning", d.m_lightning); add("poison", d.m_poison); add("spirit", d.m_spirit);
            add("block", s.m_blockPower); add("armour", s.m_armor); add("knockback", s.m_attackForce);
            add("durability", s.m_maxDurability); add("weight", s.m_weight);
            if (s.m_movementModifier != 0f) parts.Add("movement " + s.m_movementModifier.ToString("+0%;-0%"));
            if (s.m_eitrRegenModifier != 0f) parts.Add("eitr regen " + s.m_eitrRegenModifier.ToString("+0%;-0%"));
            return "Stats " + spec.Name + " (from " + spec.Donor + "): " + string.Join(", ", parts.ToArray());
        }

        static void BorrowMovement(GearSpec spec, ItemDrop.ItemData.SharedData to)
        {
            var source = PrefabManager.Cache.GetPrefab<GameObject>(spec.MovementFrom);
            var item = source != null ? source.GetComponent<ItemDrop>() : null;
            if (item == null)
            {
                Plugin.Log.LogWarning(spec.Name + ": " + spec.MovementFrom + " not found; no movement bonus.");
                return;
            }
            var bonus = item.m_itemData.m_shared.m_movementModifier;
            if (bonus > 0f) to.m_movementModifier += bonus;
            Plugin.Log.LogInfo(spec.Name + ": movement " + to.m_movementModifier.ToString("+0%;-0%;0%") +
                " (" + spec.MovementFrom + " gives " + bonus.ToString("+0%;-0%;0%") + ").");
        }

        // Numbers only. Attacks, animations and item type stay the look's, so a knife still swings like a knife.
        static void CopyStats(ItemDrop.ItemData.SharedData from, ItemDrop.ItemData.SharedData to, GearSpec spec)
        {
            to.m_damages = from.m_damages; // a struct, so this copies
            to.m_damages.Modify(spec.Damage);
            to.m_damagesPerLevel = from.m_damagesPerLevel;
            to.m_damagesPerLevel.Modify(spec.Damage);
            to.m_blockPower = from.m_blockPower * spec.Block;
            to.m_blockPowerPerLevel = from.m_blockPowerPerLevel * spec.Block;
            to.m_attackForce = from.m_attackForce * spec.Force;
            to.m_eitrRegenModifier = from.m_eitrRegenModifier;
            to.m_deflectionForce = from.m_deflectionForce;
            to.m_timedBlockBonus = from.m_timedBlockBonus;
            to.m_armor = from.m_armor * spec.Armor;
            to.m_armorPerLevel = from.m_armorPerLevel * spec.Armor;
            to.m_damageModifiers = new List<HitData.DamageModPair>(from.m_damageModifiers);
            to.m_maxDurability = from.m_maxDurability * spec.Durability;
            to.m_durabilityPerLevel = from.m_durabilityPerLevel * spec.Durability;
            to.m_maxQuality = from.m_maxQuality;
            to.m_weight = from.m_weight * spec.Weight;
            to.m_movementModifier = from.m_movementModifier;
            // No partial set bonus from the look's vanilla set.
            to.m_setName = "";
            to.m_setSize = 0;
            to.m_setStatusEffect = null;
        }
    }
}
