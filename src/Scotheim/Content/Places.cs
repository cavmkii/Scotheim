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

        // What each Pictish symbol shows, and what its text hints at.
        static readonly string[][] SymbolStones =
        {
            new[] { "Crescent and V-rod", "A crescent, struck through by a broken rod. Below it, a tower of dry stone, and small red figures at its door." },
            new[] { "Double disc and Z-rod", "Two discs joined, crossed by a zigzag rod. A house stands on the water, and a woman washes at the ford beside it." },
            new[] { "The Pictish beast", "The long-snouted swimming beast, its tail curled behind. The loch horse, perhaps. Do not mount it." },
            new[] { "Mirror and comb", "A mirror and a comb. A grey one walks the highest hill; bring what the giants carry in their chests." },
            new[] { "Serpent and Z-rod", "A serpent pierced by a rod. Climb every hill, and leave a stone on each top." },
            new[] { "The eagle", "An eagle, wings folded, on a high stone. When the grey one falls, carry his head to the stone on the tops, and his stride is yours." },
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

            // The first symbol stones were heath pillars placed by blueprint. They're no longer placed (see
            // AddRunestones), but stay registered so worlds that already have them still load them.
            for (int i = 0; i < 5; i++)
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
                    var text = SymbolStones[i][1];
                    GameFields.TrySet(rune, "Pictish symbol stone", "m_name");
                    GameFields.TrySet(rune, SymbolStones[i][0], "m_topic");
                    GameFields.TrySet(rune, text, "m_text");
                    if (i == 3) // "Mirror and comb" points to the Grey Man: reading it marks his altar, like a vegvisir
                    {
                        GameFields.TrySet(rune, GreyMan.Location, "m_locationName");
                        GameFields.TrySet(rune, "$piece_scot_greymanaltar_pin", "m_pinName");
                        GameFields.TrySet(rune, "Boss", "m_pinType");
                    }
                }
                PrefabManager.Instance.AddPrefab(stone);
            }
        }

        /// <summary>
        /// The symbol stones, as copies of the vanilla Meadows lore runestone location: same slab and glowing runes, so
        /// they're as easy to spot as the vanilla ones, with Scotheim's texts. The copies are disabled here and
        /// placed only by expand_locations_scotheim.yaml; otherwise they'd keep the vanilla stone's Meadows placement.
        /// </summary>
        internal static void AddRunestones()
        {
            ZoneManager.OnVanillaLocationsAvailable -= AddRunestones;
            var runeType = Type.GetType("RuneStone, assembly_valheim");
            for (int i = 0; i < SymbolStones.Length; i++)
            {
                var name = "scotheim_runestone" + (i + 1);
                try
                {
                    var location = ZoneManager.Instance.CreateClonedLocation(name, "Runestone_Meadows");
                    if (location == null || location.Prefab == null)
                    {
                        Plugin.Log.LogWarning("Skipped " + name + ": Runestone_Meadows wasn't found.");
                        continue;
                    }
                    GameFields.TrySet(location.ZoneLocation, false, "m_enable");
                    GameFields.TrySet(location.ZoneLocation, 0, "m_quantity");
                    var rune = runeType != null ? location.Prefab.GetComponentInChildren(runeType, true) : null;
                    if (rune == null)
                    {
                        Plugin.Log.LogWarning("Skipped " + name + ": no RuneStone in Runestone_Meadows.");
                        continue;
                    }
                    // Vanilla lore stones pick a random text from this list, which would replace ours.
                    var random = GameFields.Get(rune, "m_randomTexts") as System.Collections.IList;
                    if (random != null) random.Clear();
                    GameFields.TrySet(rune, "Pictish symbol stone", "m_name");
                    GameFields.TrySet(rune, SymbolStones[i][0], "m_topic");
                    GameFields.TrySet(rune, SymbolStones[i][1], "m_text");
                    if (i == 3) // "Mirror and comb" points to the Grey Man: reading it marks his altar, like a vegvisir
                    {
                        GameFields.TrySet(rune, GreyMan.Location, "m_locationName");
                        GameFields.TrySet(rune, "$piece_scot_greymanaltar_pin", "m_pinName");
                        GameFields.TrySet(rune, "Boss", "m_pinType");
                    }
                    else if (i == 5) // "The eagle" marks the Grey Man's stone, where his trophy gives his power
                    {
                        GameFields.TrySet(rune, GreyMan.StoneLocation, "m_locationName");
                        GameFields.TrySet(rune, "$piece_scot_greymanstone", "m_pinName");
                        GameFields.TrySet(rune, "Boss", "m_pinType");
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("Couldn't add " + name + ": " + e);
                }
            }
            try { AddAltarLocation(runeType); }
            catch (Exception e) { Plugin.Log.LogError("Couldn't add " + GreyMan.Location + ": " + e); }
        }

        /// <summary>
        /// The Grey Man's summit: a copy of the vanilla runestone location with his altar set beside it, as vanilla
        /// boss altars have their stone. Reading the stone puts the altar on the map and hints at the offering.
        /// The altar is a networked child, so the game spawns it with the location; it's snapped to the ground.
        /// </summary>
        static void AddAltarLocation(Type runeType)
        {
            var altar = PrefabManager.Instance.GetPrefab(GreyMan.Altar);
            if (altar == null)
            {
                Plugin.Log.LogWarning("The Grey Man's summit skipped: " + GreyMan.Altar + " wasn't built.");
                return;
            }
            var location = ZoneManager.Instance.CreateClonedLocation(GreyMan.Location, "Runestone_Meadows");
            if (location == null || location.Prefab == null)
            {
                Plugin.Log.LogWarning("The Grey Man's summit skipped: Runestone_Meadows wasn't found.");
                return;
            }
            GameFields.TrySet(location.ZoneLocation, false, "m_enable");
            GameFields.TrySet(location.ZoneLocation, 0, "m_quantity");
            var rune = runeType != null ? location.Prefab.GetComponentInChildren(runeType, true) : null;
            if (rune != null)
            {
                var random = GameFields.Get(rune, "m_randomTexts") as System.Collections.IList;
                if (random != null) random.Clear();
                GameFields.TrySet(rune, "$piece_scot_greymanrunestone", "m_name");
                GameFields.TrySet(rune, "Am Fear Liath Mòr", "m_topic");
                GameFields.TrySet(rune, "The giants of the hills carry hearts of cold stone. Bring three to this summit and lay " +
                    "them down, and the footsteps that follow you in the mist will stop following.", "m_text");
                GameFields.TrySet(rune, GreyMan.Location, "m_locationName");
                GameFields.TrySet(rune, "$piece_scot_greymanaltar_pin", "m_pinName");
                GameFields.TrySet(rune, "Boss", "m_pinType");
            }
            else Plugin.Log.LogWarning("The Grey Man's summit has no readable stone: no RuneStone in Runestone_Meadows.");

            var placed = UnityEngine.Object.Instantiate(altar, location.Prefab.transform);
            placed.name = GreyMan.Altar; // the spawned object is looked up by this name
            placed.transform.localPosition = new Vector3(0f, 0f, 4f);
            placed.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // facing the stone
            var snap = Type.GetType("SnapToGround, assembly_valheim");
            if (snap != null && placed.GetComponent(snap) == null) placed.AddComponent(snap);
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
