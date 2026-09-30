using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Scotheim.Content
{
    /// <summary>
    /// Crofting and the still: bere planted with the cultivator (a copy of the barley sapling), and
    /// uisge-beatha brewed like vanilla mead (wash at the cauldron, then the fermenter).
    ///
    /// Whisky keeps the frost-resistance mead's protection from cold, which is what makes the Munros'
    /// freezing weather survivable, and adds some stamina regen.
    /// </summary>
    static class Croft
    {
        internal static void Add(Dictionary<string, CustomItem> added)
        {
            AddBereSapling(added);
            AddWhisky(added);
        }

        static void AddBereSapling(Dictionary<string, CustomItem> added)
        {
            var sapling = new CustomPiece("sapling_scot_bere", "sapling_barley", new PieceConfig
            {
                Name = "$piece_scot_bere",
                Description = "$piece_scot_bere_desc",
                PieceTable = PieceTables.Cultivator,
                Requirements = new[] { new RequirementConfig("Scot_Bere", 1, 0, true) },
            });
            if (sapling.PiecePrefab == null)
            {
                Plugin.Log.LogWarning("Skipped planting bere: sapling_barley not found.");
                return;
            }
            var plant = sapling.PiecePrefab.GetComponent("Plant");
            var grown = PrefabManager.Instance.GetPrefab("Scot_Pickable_Bere");
            if (plant == null || grown == null)
            {
                Plugin.Log.LogWarning("Skipped planting bere: the sapling's Plant or the bere plant is missing.");
                return;
            }
            GameFields.TrySetObject(plant, new[] { grown }, "m_grownPrefabs");
            PieceManager.Instance.AddPiece(sapling);
        }

        static void AddWhisky(Dictionary<string, CustomItem> added)
        {
            ItemManager.Instance.AddItemConversion(new CustomItemConversion(new FermenterConversionConfig
            {
                FromItem = "Scot_WhiskyWash", ToItem = "Scot_UisgeBeatha", ProducedItems = 6,
            }));

            CustomItem whisky;
            if (!added.TryGetValue("Scot_UisgeBeatha", out whisky)) return;
            var frost = PrefabManager.Cache.GetPrefab<StatusEffect>("Potion_frostresist") as SE_Stats;
            if (frost == null)
            {
                Plugin.Log.LogWarning("Uisge-beatha keeps the vanilla frost-resistance effect: Potion_frostresist not found.");
                return;
            }
            var dram = Object.Instantiate(frost);
            dram.name = "Scot_UisgeBeathaEffect";
            dram.m_name = "$se_scot_uisgebeatha";
            dram.m_tooltip = "$se_scot_uisgebeatha_tooltip";
            dram.m_staminaRegenMultiplier = 1.15f;
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(dram, false));
            GameFields.TrySetObject(whisky.ItemDrop.m_itemData.m_shared, dram, "m_consumeStatusEffect");
        }
    }
}
