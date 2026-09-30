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
        internal const string DefeatKey = "defeated_scot_greyman";
        internal const int Heartstones = 3;
        internal static StatusEffect Dread;

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
            AddAltar(boss);
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
            var bowl = altar != null ? altar.GetComponent("OfferingBowl") : null;
            var heartstone = PrefabManager.Instance.GetPrefab("Scot_GiantHeartstone");
            if (bowl == null || heartstone == null)
            {
                Plugin.Log.LogWarning("The Grey Man can't be summoned: offeraltar_FrozenKing_bossroom, its OfferingBowl or the heartstone is missing.");
                return;
            }
            // ConditionalObject shows or hides parts by world state in the boss room; out here it could hide the altar.
            var conditional = altar.GetComponent("ConditionalObject");
            if (conditional != null) UnityEngine.Object.DestroyImmediate(conditional);
            GameFields.TrySet(bowl, "$piece_scot_greymanaltar", "m_name");
            GameFields.TrySet(bowl, "$piece_scot_greymanaltar_use", "m_useItemText");
            GameFields.TrySetObject(bowl, heartstone.GetComponent<ItemDrop>(), "m_bossItem");
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
