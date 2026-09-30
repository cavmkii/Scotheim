using System;
using System.Collections;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace Scotheim.Content
{
    /// <summary>
    /// The Sluagh raid: the host of the restless dead, said to fly in from the west. Once Am Fear Liath Mòr is dead,
    /// it can come to bases in the Highlands at night.
    ///
    /// It's added straight to the game's RandEventSystem as a copy of a vanilla raid (which keeps that raid's music
    /// and timing), not through EWD's event data: with "Event data = true", EWD 1.73 fails to patch
    /// RandEventSystem.Awake on this game version, and the exception stops the rest of EWD's startup (its biome
    /// names are never preloaded, so every Highland vegetation entry is rejected).
    /// Game members are reached by name; a miss is logged and the raid is skipped.
    /// </summary>
    static class Sluagh
    {
        const string Name = "scotheim_sluagh";
        static readonly string[] Templates = { "army_seekers", "army_goblin", "army_bonemass" };
        static object system;   // the RandEventSystem the raid was last added to
        static float next;

        /// <summary>Called every frame: adds the raid to each new RandEventSystem (one per world load).</summary>
        internal static void Tick()
        {
            if (Time.time < next) return;
            next = Time.time + 5f;
            var current = Instance("RandEventSystem");
            if (current == null || ReferenceEquals(current, system)) return;
            system = current;
            try { Add(current); }
            catch (Exception e) { Plugin.Log.LogError("Couldn't add the Sluagh raid: " + e); }
        }

        static void Add(object events)
        {
            var list = GameFields.Get(events, "m_events") as IList;
            var sluagh = PrefabManager.Instance.GetPrefab("Scot_Sluagh");
            if (list == null || sluagh == null)
            {
                Plugin.Log.LogWarning("Sluagh raid skipped: the event list or Scot_Sluagh wasn't found.");
                return;
            }
            object template = null;
            foreach (var wanted in Templates)
            {
                foreach (var e in list)
                {
                    var name = GameFields.Get(e, "m_name") as string;
                    if (name == Name) return; // already there
                    if (template == null && name == wanted) template = e;
                }
                if (template != null) break;
            }
            if (template == null)
            {
                Plugin.Log.LogWarning("Sluagh raid skipped: none of " + string.Join(", ", Templates) + " was found to copy.");
                return;
            }

            var raid = GameFields.Call(template, "Clone");
            var spawns = raid != null ? GameFields.Get(raid, "m_spawn") as IList : null;
            if (spawns == null || spawns.Count == 0)
            {
                Plugin.Log.LogWarning("Sluagh raid skipped: couldn't copy a vanilla raid.");
                return;
            }
            var highlands = Highlands();
            GameFields.TrySet(raid, Name, "m_name");
            GameFields.TrySet(raid, true, "m_enabled");
            GameFields.TrySet(raid, 120f, "m_duration");
            GameFields.TrySet(raid, "The Sluagh are coming out of the west!", "m_startMessage");
            GameFields.TrySet(raid, "The host of the dead has passed on.", "m_endMessage");
            GameFields.TrySet(raid, "Misty", "m_forceEnvironment");
            if (highlands != Heightmap.Biome.None) GameFields.TrySet(raid, (int)highlands, "m_biome");
            GameFields.TrySetObject(raid, new List<string> { GreyMan.DefeatKey }, "m_requiredGlobalKeys");
            GameFields.TrySetObject(raid, new List<string>(), "m_notRequiredGlobalKeys");

            // One spawn line: the Sluagh, at night, hunting the player.
            var spawn = GameFields.Call(spawns[0], "Clone") ?? spawns[0];
            GameFields.TrySetObject(spawn, sluagh, "m_prefab");
            GameFields.TrySet(spawn, "Sluagh", "m_name");
            GameFields.TrySet(spawn, true, "m_enabled");
            GameFields.TrySet(spawn, 8, "m_maxSpawned");
            GameFields.TrySet(spawn, 6f, "m_spawnInterval");
            GameFields.TrySet(spawn, 100f, "m_spawnChance");
            GameFields.TrySet(spawn, false, "m_spawnAtDay");
            GameFields.TrySet(spawn, true, "m_spawnAtNight");
            GameFields.TrySet(spawn, 2, "m_minLevel");
            GameFields.TrySet(spawn, 3, "m_maxLevel");
            GameFields.TrySet(spawn, 2, "m_groupSizeMin");
            GameFields.TrySet(spawn, 3, "m_groupSizeMax");
            GameFields.TrySet(spawn, true, "m_huntPlayer");
            GameFields.TrySet(spawn, "", "m_requiredGlobalKey");
            if (highlands != Heightmap.Biome.None) GameFields.TrySet(spawn, (int)highlands, "m_biome");
            spawns.Clear();
            spawns.Add(spawn);

            list.Add(raid);
            Plugin.Log.LogInfo("Sluagh raid added (copied from " + GameFields.Get(template, "m_name") + ").");
        }

        // The three Highland biomes as one flags value, or None when EWD isn't supplying them.
        static Heightmap.Biome Highlands()
        {
            int flags = 0;
            foreach (var b in new[] { Scotheim.Terrain.HighlandBiome.Moor, Scotheim.Terrain.HighlandBiome.Forest, Scotheim.Terrain.HighlandBiome.Munros })
                flags |= (int)Patches.ExpandWorld.ToGame(b);
            return (Heightmap.Biome)flags;
        }

        static object Instance(string typeName)
        {
            var type = Type.GetType(typeName + ", assembly_valheim");
            if (type == null) return null;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;
            var property = type.GetProperty("instance", flags);
            if (property != null) return property.GetValue(null, null);
            var field = type.GetField("m_instance", flags) ?? type.GetField("instance", flags);
            return field != null ? field.GetValue(null) : null;
        }
    }
}
