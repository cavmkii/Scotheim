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
    /// (model, icon, animations and attacks), with its numbers copied from a Mistlands item (the
    /// "donor") and scaled. Balance therefore follows the game's own Mistlands tier instead of
    /// figures typed in here.
    /// </summary>
    static class Gear
    {
        sealed class GearSpec
        {
            public string Name, Look, Donor;
            public float Damage = 1f, Armor = 1f, Weight = 1f, Durability = 1f;
            public string Set; // id of the armour set this piece belongs to (see Sets)
            public Action<ItemDrop.ItemData.SharedData> Tweak;
            public RequirementConfig[] Recipe;
            public string Station = Forge;
        }

        static RequirementConfig Req(string item, int amount, int perLevel) => new RequirementConfig(item, amount, perLevel);

        const string Forge = "blackforge";

        static readonly GearSpec[] Items =
        {
            // --- weapons
            new GearSpec { Name = "Scot_Claymore", Look = "THSwordKrom", Donor = "THSwordKrom",
                Recipe = new[] { Req("BlackMetal", 20, 10), Req("Iron", 10, 5), Req("Scot_HighlandHide", 3, 1), Req("Scot_TartanCloth", 2, 1) } },
            new GearSpec { Name = "Scot_Dirk", Look = "KnifeBlackMetal", Donor = "KnifeSkollAndHati",
                Recipe = new[] { Req("BlackMetal", 8, 4), Req("YggdrasilWood", 4, 2), Req("Scot_MartenPelt", 2, 1) } },
            new GearSpec { Name = "Scot_LochaberAxe", Look = "AtgeirBlackmetal", Donor = "AtgeirHimminAfl",
                // A plain steel poleaxe: the donor's lightning becomes slash.
                Tweak = s => { s.m_damages.m_slash += s.m_damages.m_lightning; s.m_damages.m_lightning = 0f;
                               s.m_damagesPerLevel.m_slash += s.m_damagesPerLevel.m_lightning; s.m_damagesPerLevel.m_lightning = 0f; },
                Recipe = new[] { Req("YggdrasilWood", 10, 5), Req("BlackMetal", 20, 10), Req("Iron", 10, 5), Req("Scot_HighlandHide", 2, 1) } },
            new GearSpec { Name = "Scot_Targe", Look = "ShieldBanded", Donor = "ShieldCarapace",
                Recipe = new[] { Req("FineWood", 10, 5), Req("Iron", 4, 2), Req("Scot_HighlandHide", 4, 2), Req("Scot_TartanCloth", 1, 1) } },


            // --- legendary
            // Robert the Bruce's axe at Bannockburn (1314): one blow killed Henry de Bohun and broke the shaft
            // (Barbour, The Brus, 1370s). A plain steel axe: the donor's poison becomes slash.
            new GearSpec { Name = "Scot_BruceAxe", Look = "AxeJotunBane", Donor = "AxeJotunBane", Damage = 1.3f,
                Tweak = s => { s.m_damages.m_slash += s.m_damages.m_poison; s.m_damages.m_poison = 0f;
                               s.m_damagesPerLevel.m_slash += s.m_damagesPerLevel.m_poison; s.m_damagesPerLevel.m_poison = 0f; },
                Recipe = new[] { Req("Scot_GiantHeartstone", 2, 1), Req("BlackMetal", 20, 10), Req("YggdrasilWood", 6, 3), Req("Scot_HighlandHide", 2, 1) } },
            // The claidheamh soluis of J. F. Campbell's Popular Tales of the West Highlands (1860-62), usually won
            // from a giant: the donor's frost becomes spirit.
            new GearSpec { Name = "Scot_ClaidheamhSoluis", Look = "SwordMistwalker", Donor = "SwordMistwalker", Damage = 1.2f,
                Tweak = s => { s.m_damages.m_spirit += s.m_damages.m_frost; s.m_damages.m_frost = 0f;
                               s.m_damagesPerLevel.m_spirit += s.m_damagesPerLevel.m_frost; s.m_damagesPerLevel.m_frost = 0f; },
                Recipe = new[] { Req("Scot_GiantHeartstone", 3, 1), Req("Scot_SithPelt", 4, 2), Req("BlackMetal", 20, 10), Req("Eitr", 10, 5) } },
            // After the 1.63 m two-hander at the Wallace Monument, Stirling (its attribution is traditional).
            new GearSpec { Name = "Scot_WallaceSword", Look = "THSwordKrom", Donor = "THSwordKrom", Damage = 1.3f, Weight = 1.5f, Durability = 1.5f,
                Tweak = s => s.m_movementModifier -= 0.05f,
                Recipe = new[] { Req("Scot_GiantHeartstone", 2, 1), Req("BlackMetal", 30, 15), Req("Iron", 15, 5), Req("Scot_TartanCloth", 4, 2), Req("Scot_HighlandHide", 4, 2) } },

            // --- Pictish weapons: after the Aberlemno stone's bareheaded spearmen with small shields.
            new GearSpec { Name = "Scot_PictishSpear", Look = "SpearBronze", Donor = "SpearCarapace",
                Recipe = new[] { Req("YggdrasilWood", 8, 4), Req("BlackMetal", 10, 5), Req("Silver", 2, 1) } },
            new GearSpec { Name = "Scot_PictishShield", Look = "ShieldBronzeBuckler", Donor = "ShieldCarapaceBuckler",
                Recipe = new[] { Req("FineWood", 8, 4), Req("Scot_HighlandHide", 3, 1), Req("Silver", 2, 1) } },

            // --- Pictish set: light and quick. Only the silver chain has evidence behind it.
            new GearSpec { Name = "Scot_PictishChain", Look = "HelmetDverger", Donor = "HelmetCarapace", Armor = 0.6f, Weight = 0.5f, Set = "pictish",
                Recipe = new[] { Req("Silver", 20, 10), Req("Scot_GiantHeartstone", 1, 0) } },
            new GearSpec { Name = "Scot_PictishJerkin", Look = "ArmorLeatherChest", Donor = "ArmorCarapaceChest", Armor = 0.7f, Weight = 0.5f, Set = "pictish",
                Tweak = s => s.m_movementModifier = 0f,
                Recipe = new[] { Req("Scot_HighlandHide", 6, 3), Req("ScaleHide", 6, 3), Req("Silver", 4, 2) } },
            new GearSpec { Name = "Scot_PictishTrews", Look = "ArmorLeatherLegs", Donor = "ArmorCarapaceLegs", Armor = 0.7f, Weight = 0.5f, Set = "pictish",
                Tweak = s => s.m_movementModifier = 0f,
                Recipe = new[] { Req("Scot_Wool", 10, 5), Req("Scot_HighlandHide", 4, 2), Req("Silver", 2, 1) } },
            new GearSpec { Name = "Scot_PictishCloak", Look = "CapeDeerHide", Donor = "CapeFeather", Set = "pictish",
                Recipe = new[] { Req("Scot_MartenPelt", 6, 3), Req("Scot_HighlandHide", 2, 1), Req("Silver", 2, 1) } },

            // --- Clansman set ("Braveheart"): the film's look, not the 1290s. Light, for two-handers.
            new GearSpec { Name = "Scot_BlueBonnet", Look = "HelmetLeather", Donor = "HelmetCarapace", Armor = 0.8f, Weight = 0.5f, Set = "clansman",
                Recipe = new[] { Req("Scot_Wool", 6, 3), Req("Scot_Blaeberries", 4, 2), Req("Scot_HighlandHide", 1, 1) } },
            new GearSpec { Name = "Scot_Leine", Look = "ArmorRagsChest", Donor = "ArmorCarapaceChest", Armor = 0.85f, Weight = 0.6f, Set = "clansman",
                Tweak = s => s.m_movementModifier = 0f,
                Recipe = new[] { Req("LinenThread", 10, 5), Req("Scot_HighlandHide", 4, 2), Req("Iron", 4, 2) } },
            new GearSpec { Name = "Scot_Kilt", Look = "ArmorRagsLegs", Donor = "ArmorCarapaceLegs", Armor = 0.85f, Weight = 0.6f, Set = "clansman",
                Tweak = s => s.m_movementModifier = 0f,
                Recipe = new[] { Req("Scot_TartanCloth", 6, 3), Req("Scot_HighlandHide", 2, 1) } },
            new GearSpec { Name = "Scot_BeltedPlaid", Look = "CapeTrollHide", Donor = "CapeFeather", Set = "clansman",
                Recipe = new[] { Req("Scot_TartanCloth", 8, 4), Req("Scot_Wool", 4, 2) } },

            // --- Man-at-arms set: Wars of Independence steel. Heavy; either chest piece counts.
            new GearSpec { Name = "Scot_Knapskull", Look = "HelmetPadded", Donor = "HelmetCarapace", Set = "manatarms",
                Recipe = new[] { Req("Iron", 10, 5), Req("BlackMetal", 6, 3), Req("Scot_MartenPelt", 2, 1) } },
            new GearSpec { Name = "Scot_Acton", Look = "ArmorPaddedCuirass", Donor = "ArmorCarapaceChest", Armor = 0.8f, Weight = 0.6f, Set = "manatarms",
                // Quilted and light: less armour than the brigandine, no movement penalty.
                Tweak = s => s.m_movementModifier = 0f,
                Recipe = new[] { Req("Scot_TartanCloth", 6, 3), Req("Scot_Wool", 10, 5), Req("Scot_HighlandHide", 4, 2) } },
            new GearSpec { Name = "Scot_Brigandine", Look = "ArmorIronChest", Donor = "ArmorCarapaceChest", Armor = 1.15f, Weight = 1.3f, Set = "manatarms",
                Recipe = new[] { Req("BlackMetal", 20, 10), Req("Iron", 10, 5), Req("Scot_TartanCloth", 4, 2), Req("Scot_HighlandHide", 4, 2) } },
            new GearSpec { Name = "Scot_Chausses", Look = "ArmorIronLegs", Donor = "ArmorCarapaceLegs", Armor = 1.15f, Weight = 1.3f, Set = "manatarms",
                Recipe = new[] { Req("BlackMetal", 14, 7), Req("Iron", 10, 5), Req("Scot_HighlandHide", 2, 1) } },
            new GearSpec { Name = "Scot_SaltireCape", Look = "CapeLinen", Donor = "CapeFeather", Set = "manatarms",
                Recipe = new[] { Req("LinenThread", 10, 5), Req("JuteBlue", 4, 2), Req("Scot_TartanCloth", 2, 1) } },

            // --- Sìth set: faerie glamour, for eitr magic. Made at the galdr table.
            new GearSpec { Name = "Scot_SithCrown", Look = "HelmetMidsummerCrown", Donor = "HelmetMage", Set = "sith", Station = "piece_magetable",
                Recipe = new[] { Req("Scot_SithPelt", 2, 1), Req("Scot_Blaeberries", 6, 3), Req("Eitr", 8, 4) } },
            new GearSpec { Name = "Scot_SithRobe", Look = "ArmorMageChest", Donor = "ArmorMageChest", Set = "sith", Station = "piece_magetable",
                Recipe = new[] { Req("Scot_SithPelt", 4, 2), Req("Scot_KelpieMane", 4, 2), Req("JuteBlue", 6, 3), Req("Eitr", 15, 5) } },
            new GearSpec { Name = "Scot_SithLeggings", Look = "ArmorMageLegs", Donor = "ArmorMageLegs", Set = "sith", Station = "piece_magetable",
                Recipe = new[] { Req("Scot_SithPelt", 3, 1), Req("Scot_KelpieMane", 2, 1), Req("JuteBlue", 4, 2), Req("Eitr", 10, 5) } },
            // The Bratach Sìth of the MacLeods, kept at Dunvegan; said to bring victory when unfurled.
            // Keeps the feather cape's look and its slow fall; adds resistance to pierce.
            new GearSpec { Name = "Scot_FairyFlag", Look = "CapeFeather", Donor = "CapeFeather", Set = "sith", Station = "piece_magetable",
                Tweak = s => s.m_damageModifiers.Add(new HitData.DamageModPair { m_type = HitData.DamageType.Pierce, m_modifier = HitData.DamageModifier.Resistant }),
                Recipe = new[] { Req("Scot_WashersShroud", 3, 1), Req("Scot_SithPelt", 2, 1), Req("LinenThread", 10, 5), Req("JuteRed", 4, 2) } },
        };

        internal static void Add()
        {
            var bonuses = Sets.AddBonuses();
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
                    StatusEffect bonus;
                    if (spec.Set != null && bonuses.TryGetValue(spec.Set, out bonus))
                    {
                        s.m_setName = "scot_" + spec.Set;
                        s.m_setSize = Sets.Size;
                        s.m_setStatusEffect = bonus;
                    }
                    ItemManager.Instance.AddItem(item);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("Couldn't add " + spec.Name + ": " + e);
                }
            }
        }

        // Numbers only. Attacks, animations and item type stay the look's, so a knife still swings like a knife.
        static void CopyStats(ItemDrop.ItemData.SharedData from, ItemDrop.ItemData.SharedData to, GearSpec spec)
        {
            to.m_damages = from.m_damages; // a struct, so this copies
            to.m_damages.Modify(spec.Damage);
            to.m_damagesPerLevel = from.m_damagesPerLevel;
            to.m_damagesPerLevel.Modify(spec.Damage);
            to.m_blockPower = from.m_blockPower;
            to.m_blockPowerPerLevel = from.m_blockPowerPerLevel;
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
