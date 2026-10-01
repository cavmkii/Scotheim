using System.Collections;
using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Scotheim.Content
{
    /// <summary>
    /// Bog iron deposits: the Swamp's muddy scrap pile, retinted rusty orange, mined for bog ore instead of scrap.
    /// Bog iron forms as limonite in wet, peaty ground, so the moor's low, boggy ground is where these sit
    /// (expand_vegetation_scotheim.yaml). A mud pile is two prefabs: the whole heap (Destructible), which breaks into
    /// the _frac version (MineRock5) that holds the drop table. Both are copied; fields are set by name.
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
            var frac = PrefabManager.Instance.CreateClonedPrefab("Scot_BogIronDeposit_frac", "mudpile_frac");
            var whole = PrefabManager.Instance.CreateClonedPrefab("Scot_BogIronDeposit", "mudpile");
            var rock = frac != null ? frac.GetComponent("MineRock5") : null;
            var destructible = whole != null ? whole.GetComponent("Destructible") : null;
            if (rock == null || destructible == null)
            {
                Plugin.Log.LogWarning("Bog iron deposits skipped: mudpile, mudpile_frac or their MineRock5/Destructible wasn't found.");
                return;
            }
            var table = GameFields.Get(rock, "m_dropItems");
            var drops = table != null ? GameFields.Get(table, "m_drops") as IList : null;
            if (drops == null || drops.Count == 0)
            {
                Plugin.Log.LogWarning("Bog iron deposits skipped: mudpile_frac has no drop table.");
                return;
            }
            // Scrap becomes bog ore; anything else the pile drops is dropped from the table.
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var drop = drops[i];
                var item = GameFields.Get(drop, "m_item") as GameObject;
                if (item != null && item.name == "IronScrap")
                {
                    GameFields.TrySetObject(drop, ore.ItemPrefab, "m_item");
                    drops[i] = drop; // DropData is a struct: write the changed copy back
                }
                else drops.RemoveAt(i);
            }
            if (drops.Count == 0)
            {
                Plugin.Log.LogWarning("Bog iron deposits skipped: mudpile_frac doesn't drop scrap iron to replace.");
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
