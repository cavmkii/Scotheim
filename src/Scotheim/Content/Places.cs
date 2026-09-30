using System;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace Scotheim.Content
{
    /// <summary>
    /// Prefabs used by the location blueprints (expand_locations_scotheim.yaml, Data/*.blueprint):
    /// creature spawners for Highland creatures, an unbreakable standing stone, and Pictish symbol stones
    /// that show a text when read. Component fields are set by name (GameFields), since they couldn't be
    /// checked against the game assembly here; a miss is logged and only skips that detail.
    /// </summary>
    static class Places
    {
        // Spawner prefab -> (vanilla spawner to copy, Highland creature to spawn).
        static readonly string[][] Spawners =
        {
            new[] { "Scot_Spawner_Redcap", "Spawner_Goblin", "Scot_Redcap" },
            new[] { "Scot_Spawner_BeanNighe", "Spawner_Wraith", "Scot_BeanNighe" },
            new[] { "Scot_Spawner_EachUisge", "Spawner_Troll", "Scot_EachUisge" },
        };

        // What each Pictish symbol shows and hints at. Nobody knows what the symbols meant; the texts say so.
        static readonly string[][] SymbolStones =
        {
            new[] { "Crescent and V-rod", "A crescent, struck through by a broken rod. Below it, a tower of dry stone, and small red figures at its door." },
            new[] { "Double disc and Z-rod", "Two discs joined, crossed by a zigzag rod. A house stands on the water, and a woman washes at the ford beside it." },
            new[] { "The Pictish beast", "The long-snouted swimming beast, its tail curled behind. The loch horse, perhaps. Do not mount it." },
            new[] { "Mirror and comb", "A mirror and a comb. A grey one walks the highest hill; bring what the giants carry in their chests." },
            new[] { "Serpent and Z-rod", "A serpent pierced by a rod. Climb every hill, and leave a stone on each top." },
        };

        internal static void Add(Dictionary<string, GameObject> creatures)
        {
            foreach (var spawner in Spawners)
            {
                GameObject creature;
                if (!creatures.TryGetValue(spawner[2], out creature))
                {
                    Plugin.Log.LogWarning("Skipped " + spawner[0] + ": " + spawner[2] + " wasn't added.");
                    continue;
                }
                var clone = PrefabManager.Instance.CreateClonedPrefab(spawner[0], spawner[1]);
                var component = clone != null ? clone.GetComponent("CreatureSpawner") : null;
                if (component == null)
                {
                    Plugin.Log.LogWarning("Skipped " + spawner[0] + ": " + spawner[1] + " or its CreatureSpawner not found.");
                    continue;
                }
                GameFields.TrySetObject(component, creature, "m_creaturePrefab");
                PrefabManager.Instance.AddPrefab(clone);
            }

            var standing = Stone("Scot_StandingStone");
            if (standing != null) PrefabManager.Instance.AddPrefab(standing);

            for (int i = 0; i < SymbolStones.Length; i++)
            {
                var stone = Stone("Scot_SymbolStone" + (i + 1));
                if (stone == null) continue;
                var runeType = Type.GetType("RuneStone, assembly_valheim");
                var rune = runeType != null ? stone.AddComponent(runeType) : null;
                if (rune == null)
                {
                    Plugin.Log.LogWarning("Symbol stones can't be read: the game's RuneStone component wasn't found.");
                }
                else
                {
                    var text = SymbolStones[i][1] + "\n\n(What the Pictish symbols meant is unknown. This reading is a guess.)";
                    GameFields.TrySet(rune, "Pictish symbol stone", "m_name");
                    GameFields.TrySet(rune, SymbolStones[i][0], "m_topic");
                    GameFields.TrySet(rune, text, "m_text");
                }
                PrefabManager.Instance.AddPrefab(stone);
            }
        }

        // A heath rock pillar that can't be broken: its Destructible component is removed.
        static GameObject Stone(string name)
        {
            var stone = PrefabManager.Instance.CreateClonedPrefab(name, "HeathRockPillar");
            if (stone == null)
            {
                Plugin.Log.LogWarning("Skipped " + name + ": HeathRockPillar not found.");
                return null;
            }
            var destructible = stone.GetComponent("Destructible");
            if (destructible != null) UnityEngine.Object.DestroyImmediate(destructible);
            return stone;
        }
    }
}
