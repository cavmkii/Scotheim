using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Scotheim.Content
{
    /// <summary>
    /// Munro bagging. Summit cairns (location scotheim_cairn) stand on high Munro ground; touching one adds
    /// it to the character's tally for this world. Bagging <see cref="ToCompleat"/> makes the character a
    /// "compleatist", which gives a lasting effect (re-applied whenever it's missing).
    ///
    /// The tally lives in the player's custom data, which the game saves with the character.
    /// </summary>
    static class Bagging
    {
        internal const int ToCompleat = 12; // expand_locations_scotheim.yaml asks for 24
        const string CountKey = "scotheim_cairns"; // global key "scotheim_cairns <n>", set by the server
        static float nextCount;
        static bool warnedNoAltar, warnedNoStone, reportedAltars;
        static StatusEffect compleatist;
        static float nextCheck;

        internal static void Add()
        {
            // A copy of the vanilla stone pile (the build piece for storing stone): a heap of stones at its own
            // size, so it doesn't depend on EWD applying a blueprint scale. WearNTear and Piece are removed so it
            // can't be damaged or taken apart with the hammer (WearNTear first, since it needs Piece).
            var cairn = PrefabManager.Instance.CreateClonedPrefab("Scot_SummitCairn", "stone_pile");
            if (cairn == null)
            {
                Plugin.Log.LogWarning("Munro bagging skipped: stone_pile not found.");
                return;
            }
            foreach (var name in new[] { "WearNTear", "Piece" })
            {
                var component = cairn.GetComponent(name);
                if (component != null) UnityEngine.Object.DestroyImmediate(component);
            }
            // The game's RuneStone supplies hover and use (it implements Hoverable and Interactable for this
            // game version); CairnPatches below replaces what it shows and does when a SummitCairn is present.
            var runeType = System.Type.GetType("RuneStone, assembly_valheim");
            if (runeType == null)
            {
                Plugin.Log.LogWarning("Munro bagging skipped: the game's RuneStone component wasn't found.");
                return;
            }
            cairn.AddComponent(runeType);
            cairn.AddComponent<SummitCairn>();
            PrefabManager.Instance.AddPrefab(cairn);

            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = "Scot_Compleatist";
            se.m_name = "$se_scot_compleatist";
            se.m_tooltip = "$se_scot_compleatist_tooltip";
            var icon = PrefabManager.Cache.GetPrefab<StatusEffect>("SetEffect_FenringArmor");
            if (icon != null) se.m_icon = icon.m_icon;
            se.m_skillLevel = Skills.SkillType.Run; se.m_skillLevelModifier = 15f;
            se.m_runStaminaDrainModifier = -0.15f;
            se.m_jumpStaminaUseModifier = -0.15f;
            GameFields.TrySet(se, -0.5f, "m_fallDamageModifier");
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, false));
            compleatist = se;
        }

        static string Key => "scotheim_munros_" + Patches.WorldGen.CurrentSeed;

        static Dictionary<string, string> Data(Player player) =>
            GameFields.Get(player, "m_customData") as Dictionary<string, string>;

        internal static HashSet<string> Bagged(Player player)
        {
            var data = Data(player);
            string list;
            var set = new HashSet<string>();
            if (data != null && data.TryGetValue(Key, out list))
                foreach (var id in list.Split(',')) if (id.Length > 0) set.Add(id);
            return set;
        }

        internal static int Bag(Player player, string id)
        {
            var data = Data(player);
            var set = Bagged(player);
            if (data == null || !set.Add(id)) return -1;
            data[Key] = string.Join(",", new List<string>(set).ToArray());
            return set.Count;
        }

        /// <summary>
        /// Hills to bag for Compleatist in this world: <see cref="ToCompleat"/>, or every cairn if the world has fewer.
        /// How many EWD places depends on the terrain (16, 24 and 10 in the first three test worlds), so the server
        /// counts them and shares the number as a global key, which reaches clients on a dedicated server too.
        /// </summary>
        internal static int Target
        {
            get
            {
                var count = PublishedCount();
                return count > 0 ? Math.Min(ToCompleat, count) : ToCompleat;
            }
        }

        static object Instance(string typeName)
        {
            var type = Type.GetType(typeName + ", assembly_valheim");
            if (type == null) return null;
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var property = type.GetProperty("instance", flags);
            if (property != null) return property.GetValue(null, null);
            var field = type.GetField("m_instance", flags) ?? type.GetField("instance", flags);
            return field != null ? field.GetValue(null) : null;
        }

        static int PublishedCount()
        {
            var zones = Instance("ZoneSystem");
            if (zones == null) return 0;
            var method = zones.GetType().GetMethod("GetGlobalKey", new[] { typeof(string), typeof(string).MakeByRefType() });
            if (method == null) return 0;
            var args = new object[] { CountKey, null };
            int count;
            return (bool)method.Invoke(zones, args) && int.TryParse(args[1] as string, out count) ? count : 0;
        }

        static int summitsPlacedFor = int.MinValue;
        const float SummitMinAltitude = 55f;   // m above sea; the Munros start around 32-50 m
        const float SummitSeedSpacing = 240f;  // climbs start from each dome centre and a grid this far around it
        static readonly float[] ClimbSteps = { 48f, 16f, 6f }; // coarse first, so the climb steps over crags on the flanks
        const float SummitSpacing = 150f;      // tops closer than this are the same hill

        /// <summary>
        /// Server only, once per world: puts a summit cairn on top of each Munro. The terrain knows where every
        /// dome is (WorldGen.CurrentSummits); each is moved to the highest ground nearby and, if that's Munro
        /// ground high enough, registered as a scotheim_cairn location there, the way the game registers any
        /// location. It spawns when that ground is first generated, so explored areas keep what they have.
        /// A zone that already holds a location (one per zone) or is already generated is skipped.
        /// </summary>
        static void PlaceSummitCairns(object zones, System.Collections.IDictionary instances)
        {
            var summits = Patches.WorldGen.CurrentSummits;
            if (summits == null || summitsPlacedFor == Patches.WorldGen.CurrentSeed) return;
            summitsPlacedFor = Patches.WorldGen.CurrentSeed;
            var generator = Instance("WorldGenerator");
            object cairn = null;
            var locations = GameFields.Get(zones, "m_locations") as System.Collections.IList;
            if (locations != null)
                foreach (var location in locations)
                    if (LocationName(location) == "scotheim_cairn") { cairn = location; break; }
            if (generator == null || cairn == null)
            {
                Plugin.Log.LogWarning("Summit cairns not placed on the tops: " + (cairn == null ? "the scotheim_cairn location" : "WorldGenerator") + " wasn't found.");
                return;
            }
            var getHeight = generator.GetType().GetMethod("GetHeight", new[] { typeof(float), typeof(float) });
            if (getHeight == null)
            {
                Plugin.Log.LogWarning("Summit cairns not placed on the tops: WorldGenerator.GetHeight(float, float) wasn't found.");
                return;
            }
            var height = (Func<float, float, float>)Delegate.CreateDelegate(typeof(Func<float, float, float>), generator, getHeight);
            var water = GameFields.Get(zones, "m_waterLevel") as float? ?? 30f;

            // Find every real top by walking uphill from many starts: each dome centre and a grid around it. A top found
            // only near a dome's centre missed broad summits whose high point lies away from it, and hills with no dome
            // centre at all (vanilla high ground taken into the Munros).
            var found = new List<Vector3>();
            int starts = 0;
            for (int i = 0; i + 1 < summits.Length; i += 2)
                for (float oz = -SummitSeedSpacing; oz <= SummitSeedSpacing; oz += SummitSeedSpacing)
                    for (float ox = -SummitSeedSpacing; ox <= SummitSeedSpacing; ox += SummitSeedSpacing)
                    {
                        float x = summits[i] + ox, z = summits[i + 1] + oz;
                        if (height(x, z) - water < SummitMinAltitude - 20f) continue; // not hill ground
                        starts++;
                        found.Add(Climb(height, x, z));
                    }
            found.Sort((p, q) => q.y.CompareTo(p.y)); // highest first, so each hill keeps its highest point

            int placed = 0, low = 0, taken = 0;
            var tops = new List<Vector3>();
            foreach (var top in found)
            {
                bool near = false;
                foreach (var other in tops)
                {
                    float dx = other.x - top.x, dz = other.z - top.z;
                    if (dx * dx + dz * dz < SummitSpacing * SummitSpacing) { near = true; break; }
                }
                if (near) continue;
                tops.Add(top); // claimed even if it can't hold a cairn, so a lower point of the same hill doesn't get one
                var biome = GameFields.Call(generator, "GetBiome", top);
                bool custom;
                if (top.y - water < SummitMinAltitude || !(biome is Heightmap.Biome) ||
                    Patches.ExpandWorld.FromGame((Heightmap.Biome)biome, out custom) != Scotheim.Terrain.HighlandBiome.Munros) { low++; continue; }
                var zone = GameFields.Call(zones, "GetZone", top);
                if (zone == null || instances.Contains(zone) || (GameFields.Call(zones, "IsZoneGenerated", zone) as bool? ?? true)) { taken++; continue; }
                GameFields.Call(zones, "RegisterLocation", cairn, top, false);
                placed++;
            }
            Plugin.Log.LogInfo("Summit cairns: " + placed + " placed on Munro tops (climbed from " + starts + " starts to " + tops.Count +
                " separate tops); " + taken + " tops were already generated or held another location, " + low + " weren't high Munro ground.");
        }

        /// <summary>Walks uphill from (x, z) to where no neighbouring point is higher, and returns that top.</summary>
        static Vector3 Climb(Func<float, float, float> height, float x, float z)
        {
            float h = height(x, z);
            foreach (var step in ClimbSteps)
            {
                for (int moves = 0; moves < 60; moves++)
                {
                    float bestX = x, bestZ = z, bestH = h;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            float nx = x + dx * step, nz = z + dz * step, nh = height(nx, nz);
                            if (nh > bestH) { bestH = nh; bestX = nx; bestZ = nz; }
                        }
                    if (bestH <= h) break;
                    x = bestX; z = bestZ; h = bestH;
                }
            }
            return new Vector3(x, h, z);
        }

        static string LocationName(object location)
        {
            var prefab = location != null ? GameFields.Get(location, "m_prefab") : null;
            var name = prefab != null ? prefab.GetType().GetProperty("Name")?.GetValue(prefab, null) as string : null;
            return name ?? (location != null ? GameFields.Get(location, "m_prefabName") as string : null);
        }

        // Server only: count this world's cairns and publish the number if it changed (genloc can add more).
        static void CountCairns()
        {
            var net = Instance("ZNet");
            var zones = Instance("ZoneSystem");
            if (net == null || zones == null || !(GameFields.Call(net, "IsServer") as bool? ?? false)) return;
            var instances = GameFields.Get(zones, "m_locationInstances") as System.Collections.IDictionary;
            if (instances == null || instances.Count == 0) return;
            PlaceSummitCairns(zones, instances);
            int count = 0, altars = 0, stones = 0;
            foreach (var instance in instances.Values)
            {
                var name = LocationName(GameFields.Get(instance, "m_location"));
                if (name == "scotheim_cairn") count++;
                else if (name == GreyMan.Location)
                {
                    altars++;
                    if (!reportedAltars) Report("Grey Man altar " + altars, instance);
                }
                else if (name == GreyMan.StoneLocation)
                {
                    stones++;
                    if (!reportedAltars) Report("Grey Man's stone (hang his trophy here for his power)", instance);
                }
            }
            if (stones == 0 && instances.Count > 0 && !warnedNoStone)
            {
                warnedNoStone = true;
                Plugin.Log.LogWarning("The Grey Man's stone wasn't placed in this world, so his power can't be taken. Lower minAltitude for " +
                    GreyMan.StoneLocation + " in expand_locations_scotheim.yaml and run genloc, or use a new world.");
            }
            if (instances.Count > 0) reportedAltars = true;
            if (altars == 0 && !warnedNoAltar)
            {
                warnedNoAltar = true;
                Plugin.Log.LogWarning("No Grey Man altar was placed in this world, so he can't be summoned. Lower minAltitude for " +
                    GreyMan.Location + " in expand_locations_scotheim.yaml and run genloc, or use a new world.");
            }
            if (count == 0 || count == PublishedCount()) return;
            GameFields.Call(zones, "SetGlobalKey", CountKey + " " + count);
            Plugin.Log.LogInfo("Summit cairns in this world: " + count + ". Compleatist needs " + Math.Min(ToCompleat, count) + ".");
        }

        static void Report(string what, object instance)
        {
            var at = GameFields.Get(instance, "m_position") as Vector3?;
            Plugin.Log.LogInfo(what + (at.HasValue
                ? " at x " + Mathf.RoundToInt(at.Value.x) + ", z " + Mathf.RoundToInt(at.Value.z) + ", " + Mathf.RoundToInt(at.Value.y) +
                  " m up (devcommands: goto " + Mathf.RoundToInt(at.Value.x) + " " + Mathf.RoundToInt(at.Value.z) + ")"
                : " (position unknown)") + ".");
        }

        /// <summary>Called every frame from the plugin: gives compleatists their effect if it's missing.</summary>
        internal static void Tick()
        {
            if (compleatist == null || Time.time < nextCheck) return;
            nextCheck = Time.time + 5f;
            if (Time.time >= nextCount)
            {
                nextCount = Time.time + 60f;
                CountCairns();
            }
            var player = Player.m_localPlayer;
            var data = player != null ? Data(player) : null;
            if (data == null) return;
            // Once earned it stays, even if genloc later adds cairns and raises the target.
            if (!data.ContainsKey(Key + "_compleat"))
            {
                if (Bagged(player).Count < Target) return;
                data[Key + "_compleat"] = "1";
            }
            var seman = player.GetSEMan();
            var active = GameFields.Get(seman, "m_statusEffects") as System.Collections.IList;
            if (active != null)
                foreach (var effect in active)
                    if (effect is StatusEffect && ((StatusEffect)effect).name.StartsWith(compleatist.name)) return;
            GameFields.Call(seman, "AddStatusEffect", compleatist, false);
        }
    }

    /// <summary>Marks a summit cairn; the cairn's RuneStone calls these through <see cref="CairnPatches"/>.</summary>
    public class SummitCairn : MonoBehaviour
    {
        string Id
        {
            get
            {
                var p = transform.position;
                return Mathf.RoundToInt(p.x) + ":" + Mathf.RoundToInt(p.z);
            }
        }

        internal string HoverText()
        {
            var player = Player.m_localPlayer;
            var done = player != null && Bagging.Bagged(player).Contains(Id);
            var text = "Summit cairn\n" + (done ? "You've bagged this one." : "[<color=yellow><b>$KEY_Use</b></color>] Add a stone");
            return global::Localization.instance.Localize(text); // Localization is in assembly_guiutils
        }

        internal bool Interact(Humanoid user, bool hold)
        {
            if (hold) return false;
            var player = user as Player;
            if (player == null || player != Player.m_localPlayer) return false;
            int count = Bagging.Bag(player, Id);
            if (count < 0)
            {
                player.Message(MessageHud.MessageType.Center, "You've already left a stone here.");
                return true;
            }
            var target = Bagging.Target;
            var message = count >= target
                ? (count == target ? "Compleatist! You've bagged " + count + " hills." : "Munros bagged: " + count)
                : "Munros bagged: " + count + " of " + target;
            player.Message(MessageHud.MessageType.Center, message);
            return true;
        }
    }

    /// <summary>
    /// Hover text and use for summit cairns, taken over from the game's RuneStone. Found by name so the
    /// game's exact member signatures don't have to be known here; a missing one is logged and skipped.
    /// </summary>
    static class CairnPatches
    {
        // A null target would make PatchAll throw and skip every later patch, so check first.
        static bool Report(MethodBase method, string name)
        {
            if (method == null) Plugin.Log.LogWarning("Summit cairns can't be used: " + name + " wasn't found.");
            else Plugin.Log.LogInfo("Summit cairns: patched " + name + ".");
            return method != null;
        }

        [HarmonyPatch]
        static class Hover
        {
            static MethodBase Target() => AccessTools.Method(AccessTools.TypeByName("RuneStone"), "GetHoverText");
            static bool Prepare() => Report(Target(), "RuneStone.GetHoverText");
            static MethodBase TargetMethod() => Target();

            static bool Prefix(Component __instance, ref string __result)
            {
                var cairn = __instance.GetComponent<SummitCairn>();
                if (cairn == null) return true;
                __result = cairn.HoverText();
                return false;
            }
        }

        [HarmonyPatch]
        static class Use
        {
            static MethodBase Target() => AccessTools.Method(AccessTools.TypeByName("RuneStone"), "Interact");
            static bool Prepare() => Report(Target(), "RuneStone.Interact");
            static MethodBase TargetMethod() => Target();

            static bool Prefix(Component __instance, Humanoid __0, bool __1, ref bool __result)
            {
                var cairn = __instance.GetComponent<SummitCairn>();
                if (cairn == null) return true;
                __result = cairn.Interact(__0, __1);
                return false;
            }
        }
    }
}
