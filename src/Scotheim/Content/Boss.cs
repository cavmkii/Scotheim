using System;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Scotheim.Content
{
    /// <summary>
    /// Am Fear Liath Mòr, the Big Grey Man of Ben Macdui: a very tall grey figure on the high Cairngorms, known
    /// mostly from the panic and dread climbers reported (Norman Collie's account of 1925 is the famous one).
    ///
    /// He's summoned at an altar on a high Munro (location scotheim_greyman) by offering giant's heartstones,
    /// fights as a boss (health bar, boss music, a world key when he dies), and anyone near him feels the dread:
    /// slower stamina regen and a heavier step. Symbol stone 4 marks the altar on the map when read.
    ///
    /// Boss fields on Character and OfferingBowl are set by name (GameFields); a miss is logged and only skips
    /// that detail.
    /// </summary>
    static class GreyMan
    {
        internal const string Boss = "Scot_GreyMan";
        internal const string Altar = "Scot_GreyManAltar";
        internal const string Location = "scotheim_greyman";
        internal const string StoneLocation = "scotheim_greymanstone";
        internal const string DefeatKey = "defeated_scot_greyman";
        internal const int Heartstones = 3;
        internal static StatusEffect Dread;
        static GameObject bossPrefab;
        static ItemDrop heartstone, trophy;
        static bool altarBuilt;

        // Jötunn announces creatures before items, so the altar is built by whichever of these runs second.
        internal static void ItemsReady(System.Collections.Generic.Dictionary<string, CustomItem> added)
        {
            CustomItem item;
            if (added.TryGetValue("Scot_GiantHeartstone", out item)) heartstone = item.ItemDrop;
            else Plugin.Log.LogWarning("The Grey Man can't be summoned: Scot_GiantHeartstone wasn't added.");
            if (added.TryGetValue("Scot_TrophyGreyMan", out item)) trophy = item.ItemDrop;
            else Plugin.Log.LogWarning("The Grey Man's power can't be taken: Scot_TrophyGreyMan wasn't added.");
            TryAddAltar();
        }

        /// <summary>Called once the boss creature exists: makes it a boss and builds its altar and dread.</summary>
        internal static void Add(GameObject boss)
        {
            var character = boss.GetComponent<Character>();
            GameFields.TrySet(character, true, "m_boss");
            GameFields.TrySet(character, "boss_queen", "m_bossEvent"); // borrows the Queen's boss music
            GameFields.TrySet(character, DefeatKey, "m_defeatSetGlobalKey");
            GameFields.TrySet(character, "Boss", "m_faction");
            boss.AddComponent<GreyManDread>();

            AddDread();
            bossPrefab = boss;
            TryAddAltar();
        }

        static void TryAddAltar()
        {
            if (altarBuilt || bossPrefab == null || heartstone == null) return;
            altarBuilt = true;
            AddAltar(bossPrefab);
            if (trophy != null) AddStone(AddPower());
        }

        // The guardian power: the Grey Man's long, sure stride on the high tops. Same length and cooldown as vanilla's.
        static StatusEffect AddPower()
        {
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = "GP_Scot_GreyMan";
            se.m_name = "$se_scot_greymanpower";
            se.m_tooltip = "$se_scot_greymanpower_tooltip";
            se.m_ttl = 300f;
            GameFields.TrySet(se, 1200f, "m_cooldown");
            var icons = trophy.m_itemData.m_shared.m_icons;
            if (icons != null && icons.Length > 0) se.m_icon = icons[0];
            se.m_runStaminaDrainModifier = -0.5f;
            se.m_jumpStaminaUseModifier = -0.5f;
            GameFields.TrySet(se, -0.75f, "m_fallDamageModifier");
            GameFields.TrySet(se, -0.5f, "m_noiseModifier");
            se.m_mods = new System.Collections.Generic.List<HitData.DamageModPair>
            {
                // Frost resistance is what stops freezing (as frost resistance mead does).
                new HitData.DamageModPair { m_type = HitData.DamageType.Frost, m_modifier = HitData.DamageModifier.Resistant },
            };
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, false));
            return se;
        }

        // The stone where his trophy hangs to give the power: a copy of the Queen's boss stone from the start
        // temple (vanilla spawns these as standalone objects), placed on a Munro summit (location scotheim_greymanstone).
        static void AddStone(StatusEffect power)
        {
            var stone = PrefabManager.Instance.CreateClonedPrefab("Scot_GreyManStone", "BossStone_TheQueen");
            if (stone == null)
            {
                Plugin.Log.LogWarning("The Grey Man's power can't be taken: BossStone_TheQueen wasn't found.");
                return;
            }
            var standType = Type.GetType("ItemStand, assembly_valheim");
            var stand = standType != null ? stone.GetComponentInChildren(standType, true) : null;
            if (stand == null)
            {
                Plugin.Log.LogWarning("The Grey Man's power can't be taken: BossStone_TheQueen has no ItemStand.");
                return;
            }
            GameFields.TrySetObject(stand, new System.Collections.Generic.List<ItemDrop> { trophy }, "m_supportedItems");
            GameFields.TrySetObject(stand, power, "m_guardianPower");

            var runeType = Type.GetType("RuneStone, assembly_valheim");
            var rune = runeType != null ? stone.GetComponentInChildren(runeType, true) : null;
            if (rune != null)
            {
                var random = GameFields.Get(rune, "m_randomTexts") as System.Collections.IList;
                if (random != null) random.Clear();
                GameFields.TrySet(rune, "$piece_scot_greymanstone", "m_name");
                GameFields.TrySet(rune, "Am Fear Liath Mòr", "m_topic");
                GameFields.TrySet(rune, "Hang the grey one's head here, and walk the high tops as he did: sure-footed, unheard, " +
                    "and never cold.", "m_text");
            }
            PrefabManager.Instance.AddPrefab(stone);
        }

        static void AddDread()
        {
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = "Scot_Dread";
            se.m_name = "$se_scot_dread";
            se.m_tooltip = "$se_scot_dread_tooltip";
            se.m_ttl = 3f; // refreshed while you're near him
            var icon = PrefabManager.Cache.GetPrefab<StatusEffect>("Cold");
            if (icon != null) se.m_icon = icon.m_icon;
            se.m_staminaRegenMultiplier = 0.6f;
            se.m_speedModifier = -0.1f;
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, false));
            Dread = se;
        }

        static void AddAltar(GameObject boss)
        {
            // The Deep North boss room's offering altar is a standalone prefab with a working OfferingBowl.
            var altar = PrefabManager.Instance.CreateClonedPrefab("Scot_GreyManAltar", "offeraltar_FrozenKing_bossroom");
            if (altar == null)
            {
                Plugin.Log.LogWarning("The Grey Man can't be summoned: offeraltar_FrozenKing_bossroom wasn't found.");
                return;
            }
            var bowlType = Type.GetType("OfferingBowl, assembly_valheim");
            var bowl = bowlType != null ? altar.GetComponentInChildren(bowlType, true) : null; // may sit on a child
            if (bowl == null)
            {
                Plugin.Log.LogWarning("The Grey Man can't be summoned: offeraltar_FrozenKing_bossroom has no OfferingBowl.");
                return;
            }
            // ConditionalObject shows or hides parts by world state in the boss room; out here it could hide the altar.
            var conditional = altar.GetComponent("ConditionalObject");
            if (conditional != null) UnityEngine.Object.DestroyImmediate(conditional);
            GameFields.TrySet(bowl, "$piece_scot_greymanaltar", "m_name");
            GameFields.TrySet(bowl, "$piece_scot_greymanaltar_use", "m_useItemText");
            GameFields.TrySetObject(bowl, heartstone, "m_bossItem");
            GameFields.TrySet(bowl, Heartstones, "m_bossItems");
            GameFields.TrySetObject(bowl, boss, "m_bossPrefab");
            GameFields.TrySet(bowl, false, "m_useItemStands");
            PrefabManager.Instance.AddPrefab(altar);
        }
    }

    /// <summary>Gives the local player the dread while the Grey Man is alive and near. Runs on every client.</summary>
    public class GreyManDread : MonoBehaviour
    {
        const float Range = 35f;
        float next;

        void Update()
        {
            if (Time.time < next || GreyMan.Dread == null) return;
            next = Time.time + 1f;
            var player = Player.m_localPlayer;
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > Range) return;
            GameFields.Call(player.GetSEMan(), "AddStatusEffect", GreyMan.Dread, true);
        }
    }
}
