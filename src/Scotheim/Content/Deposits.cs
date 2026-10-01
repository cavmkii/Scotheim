using System.Collections;
using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Scotheim.Content
{
    /// <summary>
    /// Bog iron deposits: the Black Forest's copper boulder, retinted rusty orange and mined for bog ore instead of
    /// copper, so they're as easy to spot across the moor as copper is in the forest (a mud heap was tried first and
    /// was too easy to miss). Bog iron forms as limonite in wet, peaty ground; where they sit is set in
    /// expand_vegetation_scotheim.yaml. A deposit is two prefabs: the whole rock (Destructible), which breaks into the
    /// _frac version (MineRock5) that holds the drop table. Both are copied; fields are set by name.
    /// </summary>
    static class Deposits
    {
        internal static void Add(Dictionary<string, CustomItem> added)
        {
            CustomItem ore;
            if (!added.TryGetValue("Scot_BogIronOre", out ore))
            {
                Plugin.Log.LogWarning("Bog iron deposits skipped: Scot_BogIronOre wasn't added.");
                return;
            }
            var frac = PrefabManager.Instance.CreateClonedPrefab("Scot_BogIronDeposit_frac", "rock4_copper_frac");
            var whole = PrefabManager.Instance.CreateClonedPrefab("Scot_BogIronDeposit", "rock4_copper");
            var rock = frac != null ? frac.GetComponent("MineRock5") : null;
            var destructible = whole != null ? whole.GetComponent("Destructible") : null;
            if (rock == null || destructible == null)
            {
                Plugin.Log.LogWarning("Bog iron deposits skipped: rock4_copper, rock4_copper_frac or their MineRock5/Destructible wasn't found.");
                return;
            }
            var table = GameFields.Get(rock, "m_dropItems");
            var drops = table != null ? GameFields.Get(table, "m_drops") as IList : null;
            if (drops == null || drops.Count == 0)
            {
                Plugin.Log.LogWarning("Bog iron deposits skipped: rock4_copper_frac has no drop table.");
                return;
            }
            // Copper ore becomes bog ore; the stone the rock also gives stays; anything else is dropped from the table.
            int swapped = 0;
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var drop = drops[i];
                var item = GameFields.Get(drop, "m_item") as GameObject;
                if (item != null && item.name == "CopperOre")
                {
                    GameFields.TrySetObject(drop, ore.ItemPrefab, "m_item");
                    drops[i] = drop; // DropData is a struct: write the changed copy back
                    swapped++;
                }
                else if (item == null || item.name != "Stone") drops.RemoveAt(i);
            }
            if (swapped == 0)
            {
                Plugin.Log.LogWarning("Bog iron deposits skipped: rock4_copper_frac doesn't drop copper ore to replace.");
                return;
            }
            GameFields.TrySet(whole.GetComponent("HoverText"), "$piece_scot_bogirondeposit", "m_text");
            GameFields.TrySet(rock, "$piece_scot_bogirondeposit", "m_name");
            GameFields.TrySetObject(destructible, frac, "m_spawnWhenDestroyed");
            Reskin.Prop("Scot_BogIronDeposit", whole);
            Reskin.Prop("Scot_BogIronDeposit_frac", frac);
            PrefabManager.Instance.AddPrefab(frac);
            PrefabManager.Instance.AddPrefab(whole);
        }
    }
}
