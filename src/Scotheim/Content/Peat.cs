using System.Collections;
using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Scotheim.Content
{
    /// <summary>
    /// Peat. Cut wet from the moor, then stacked in the wind to dry: the peat stack is a copy of the charcoal kiln
    /// that takes cut peat and gives dried peat, slowly and without fuel. Dried peat smokes the malt for the whisky
    /// (the wash recipe) and, as bog moss, makes a wound dressing: sphagnum from the bogs was a standard dressing,
    /// gathered in Scotland by the ton in the First World War.
    ///
    /// The bandage heals some at once and some over a few seconds, and shares the healing meads' cooldown slot so
    /// it can't be stacked with them. Fields are set by name; a miss is logged and that detail is skipped.
    /// </summary>
    static class Peat
    {
        const float SecondsPerPeat = 90f;   // the charcoal kiln is faster; drying in the wind takes time

        internal static void Add(Dictionary<string, CustomItem> added)
        {
            AddStack();
            AddBandage(added);
        }

        static void AddStack()
        {
            var stack = new CustomPiece("piece_scot_peatstack", "charcoal_kiln", new PieceConfig
            {
                Name = "$piece_scot_peatstack",
                Description = "$piece_scot_peatstack_desc",
                PieceTable = PieceTables.Hammer,
                CraftingStation = "piece_workbench",
                Requirements = new[] { new RequirementConfig("Stone", 10, 0, true), new RequirementConfig("Wood", 6, 0, true) },
            });
            var smelter = stack.PiecePrefab != null ? stack.PiecePrefab.GetComponent("Smelter") : null;
            if (smelter == null)
            {
                Plugin.Log.LogWarning("Peat stack skipped: charcoal_kiln or its Smelter wasn't found.");
                return;
            }
            // The kiln turns wood into coal; the stack only dries peat (added below as a Jötunn conversion).
            var conversions = GameFields.Get(smelter, "m_conversion") as IList;
            if (conversions != null) conversions.Clear();
            GameFields.TrySet(smelter, "$piece_scot_peatstack", "m_name");
            GameFields.TrySet(smelter, SecondsPerPeat, "m_secPerProduct");
            GameFields.TrySet(smelter, "$piece_scot_peatstack_add", "m_addOreTooltip"); // the kiln's says "Add wood"
            PieceManager.Instance.AddPiece(stack);
            ItemManager.Instance.AddItemConversion(new CustomItemConversion(new SmelterConversionConfig
            {
                Station = "piece_scot_peatstack", FromItem = "Scot_Peat", ToItem = "Scot_DriedPeat",
            }));
        }

        static void AddBandage(Dictionary<string, CustomItem> added)
        {
            CustomItem bandage;
            if (!added.TryGetValue("Scot_MossBandage", out bandage)) return;
            var minor = PrefabManager.Cache.GetPrefab<StatusEffect>("Potion_health_minor");
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = "Scot_Bandaged";
            se.m_name = "$se_scot_bandaged";
            se.m_tooltip = "$se_scot_bandaged_tooltip";
            if (minor != null)
            {
                se.m_icon = minor.m_icon;
                // Same category as the healing meads: one heal at a time.
                GameFields.TrySet(se, GameFields.Get(minor, "m_category") ?? "healthpotion", "m_category");
            }
            se.m_ttl = 60f; // the cooldown: shorter than the meads', for a smaller heal
            GameFields.TrySet(se, 30f, "m_healthUpFront");
            GameFields.TrySet(se, 30f, "m_healthOverTime");
            GameFields.TrySet(se, 10f, "m_healthOverTimeDuration");
            GameFields.TrySet(se, 1f, "m_healthOverTimeInterval");
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, false));
            GameFields.TrySetObject(bandage.ItemDrop.m_itemData.m_shared, se, "m_consumeStatusEffect");
        }
    }
}
