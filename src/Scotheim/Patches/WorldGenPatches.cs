using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Scotheim.Terrain;

namespace Scotheim.Patches
{
    /// <summary>
    /// Hooks into WorldGenerator. Heights there are absolute metres; the water plane is at 30, and
    /// GetBaseHeight returns (absolute / 200). Patches use positional arguments (__0, __1, ...) so they
    /// don't depend on parameter names in the game assembly.
    /// </summary>
    static class WorldGen
    {
        internal const float WaterLevel = 30f;

        static readonly AccessTools.FieldRef<WorldGenerator, World> WorldRef = AccessTools.FieldRefAccess<WorldGenerator, World>("m_world");
        static readonly AccessTools.FieldRef<World, bool> MenuRef = AccessTools.FieldRefAccess<World, bool>("m_menu");
        static readonly AccessTools.FieldRef<World, int> SeedRef = AccessTools.FieldRefAccess<World, int>("m_seed");

        sealed class Active
        {
            public World World;
            public HighlandsTerrain Terrain;
        }

        // Two slots, replaced atomically and read from heightmap worker threads. Two because the main
        // menu's world and the real world can both be generating while a game loads.
        static volatile Active latest, previous;
        static readonly object buildLock = new object();

        /// <summary>The Highlands for this generator's world, or null to leave vanilla alone.</summary>
        internal static HighlandsTerrain For(WorldGenerator generator)
        {
            if (generator == null) return null;
            World world = WorldRef(generator);
            if (world == null) return null;

            Active hit = latest;
            if (hit != null && ReferenceEquals(hit.World, world)) return hit.Terrain;
            hit = previous;
            if (hit != null && ReferenceEquals(hit.World, world)) return hit.Terrain;

            lock (buildLock)
            {
                if (latest != null && ReferenceEquals(latest.World, world)) return latest.Terrain;
                if (previous != null && ReferenceEquals(previous.World, world)) return previous.Terrain;

                HighlandsTerrain terrain = null;
                if (!MenuRef(world) && Plugin.Instance != null && Plugin.Instance.TerrainEnabled.Value)
                    terrain = Build(generator, SeedRef(world));
                previous = latest;
                latest = new Active { World = world, Terrain = terrain };
                return terrain;
            }
        }

        static HighlandsTerrain Build(WorldGenerator generator, int seed)
        {
            // Snapshot config now: live edits mid-session would change ground under existing zones.
            var settings = Plugin.Instance.SnapshotSettings();
            Func<float, float, float> vanilla = (wx, wy) => ToAltitude(OriginalBaseHeight.Call(generator, wx, wy, false));
            var sites = HighlandsTerrain.FindSites(settings, vanilla);
            for (int i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                Plugin.Log.LogInfo(string.Format("Highlands island {0}/{1} at ({2:F0}, {3:F0}), {4:F0} m from centre, size {5:P0}, long axis {6:F0} deg; {7:P0} of the site was open water.",
                    i + 1, sites.Count, site.X, site.Y, Math.Sqrt(site.X * site.X + site.Y * site.Y), site.Scale, site.Azimuth, site.OpenWater));
                var depths = HighlandsTerrain.FootprintAltitudes(settings, vanilla, site);
                if (depths.Length > 0)
                {
                    Func<float, float> pct = p => depths[Math.Min(depths.Length - 1, (int)(p * depths.Length))];
                    int land = 0;
                    foreach (var d in depths) if (d > HighlandsTerrain.VanillaShore) land++;
                    Plugin.Log.LogInfo(string.Format("  site vanilla base altitude (m): min {0:F0}, p10 {1:F0}, p50 {2:F0}, p90 {3:F0}, max {4:F0}; {5:P0} already land or shore.",
                        depths[0], pct(0.1f), pct(0.5f), pct(0.9f), depths[depths.Length - 1], land / (float)depths.Length));
                }
                if (site.OpenWater < 0.9f)
                    Plugin.Log.LogWarning("  Less than 90% of this site was open water. Vanilla land and shallows there are left as they are, " +
                        "so this island will be smaller or broken up.");
            }
            Plugin.Log.LogInfo(string.Format("Placed {0} of {1} Highland islands. Seed {2}, signature {3}.",
                sites.Count, settings.LandmassCount, seed, Plugin.Signature(settings)));
            if (sites.Count < settings.LandmassCount)
                Plugin.Log.LogWarning("Not enough open water for every island: try a smaller Length/Width, a lower MinScale, or a wider distance band.");
            if (!ExpandWorld.Present)
                Plugin.Log.LogWarning("Expand World Data not found: the Highlands use vanilla Meadows, Black Forest and Mountain.");
            return new HighlandsTerrain(settings, seed, sites);
        }

        internal static float ToAltitude(float baseHeight) { return baseHeight * 200f - WaterLevel; }
        internal static float FromAltitude(float altitude) { return (altitude + WaterLevel) / 200f; }
    }

    /// <summary>Unpatched GetBaseHeight: vanilla's base altitude, before the landmass and glens.</summary>
    [HarmonyPatch]
    static class OriginalBaseHeight
    {
        // NoInlining: if the JIT inlines this stub into a caller before Harmony swaps its body, the
        // caller keeps the throwing stub. Seen under Mono in the test harness.
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(WorldGenerator), "GetBaseHeight")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static float Call(WorldGenerator instance, float wx, float wy, bool menuTerrain)
        {
            throw new NotImplementedException("Replaced by Harmony reverse patch.");
        }
    }

    /// <summary>
    /// Stage A: raise the landmass and carve its glens in the base height. Biome selection, rivers
    /// and vanilla's biome height functions all read this, so they see the island consistently.
    /// </summary>
    [HarmonyPatch(typeof(WorldGenerator), "GetBaseHeight")]
    static class RaiseLandmass
    {
        static void Postfix(WorldGenerator __instance, float __0, float __1, bool __2, ref float __result)
        {
            if (__2) return; // menu terrain
            var terrain = WorldGen.For(__instance);
            if (terrain == null) return;
            float vanilla = WorldGen.ToAltitude(__result);
            float land = terrain.LandBase(__0, __1, vanilla);
            if (land == vanilla) return;
            __result = WorldGen.FromAltitude(terrain.CarveBase(__0, __1, land));
        }
    }

    /// <summary>
    /// Assigns the Highland biomes. Runs after vanilla's (or Expand World Data's) own choice and
    /// overrides it only on the landmass, above the ocean threshold.
    /// </summary>
    [HarmonyPatch(typeof(WorldGenerator), "GetBiome", new[] { typeof(float), typeof(float), typeof(float), typeof(bool) })]
    static class AssignBiome
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix(WorldGenerator __instance, float __0, float __1, bool __3, ref Heightmap.Biome __result)
        {
            // waterAlwaysOcean: callers asking "is this under water" get Ocean, as in vanilla.
            if (__3 && __result == Heightmap.Biome.Ocean) return;
            var terrain = WorldGen.For(__instance);
            if (terrain == null || terrain.LandWeight(__0, __1) <= 0.5f) return;

            // Patched base height, so glens and the island are included; vanilla's decides whether
            // this was already land (which keeps its vanilla biome).
            float baseAltitude = WorldGen.ToAltitude(__instance.GetBaseHeightPatched(__0, __1));
            float vanillaAltitude = WorldGen.ToAltitude(OriginalBaseHeight.Call(__instance, __0, __1, false));
            var highland = terrain.Classify(__0, __1, baseAltitude, vanillaAltitude);
            if (highland != HighlandBiome.None)
                __result = ExpandWorld.ToGame(highland);
        }
    }

    /// <summary>Stage B: per-biome reshaping of the final height on the landmass.</summary>
    [HarmonyPatch(typeof(WorldGenerator), "GetBiomeHeight")]
    static class ShapeBiomeHeight
    {
        // Expand World Data's prefix swaps a custom biome for its "terrain" biome before the height
        // functions run, and Harmony shares that argument between patches. Record the real biome first.
        [HarmonyPriority(Priority.First)]
        static void Prefix(Heightmap.Biome __0, out Heightmap.Biome __state)
        {
            __state = __0;
        }

        static void Postfix(WorldGenerator __instance, Heightmap.Biome __state, float __1, float __2, ref float __result)
        {
            bool custom;
            var highland = ExpandWorld.FromGame(__state, out custom);
            if (highland == HighlandBiome.None) return;
            var terrain = WorldGen.For(__instance);
            if (terrain == null) return;

            float x = __1, y = __2;
            // Without custom biomes, Meadows/Black Forest/Mountain also exist on the mainland; only
            // reshape them on the island.
            if (!custom && terrain.LandWeight(x, y) <= 0.5f) return;

            float preGlen = terrain.LandBase(x, y, WorldGen.ToAltitude(OriginalBaseHeight.Call(__instance, x, y, false)));
            float h = __result - WorldGen.WaterLevel;
            switch (highland)
            {
                case HighlandBiome.Munros:
                    // Replaces vanilla's relief; lochs are carved in the same pass.
                    h = terrain.ShapeMountain(x, y, preGlen);
                    break;
                case HighlandBiome.Moor:
                    h = terrain.ApplyLochs(x, y, terrain.ShapeMoorland(x, y, h), preGlen);
                    break;
                case HighlandBiome.Forest:
                    h = terrain.ApplyLochs(x, y, terrain.ShapeForest(x, y, h), preGlen);
                    break;
            }
            __result = h + WorldGen.WaterLevel;
        }
    }

    static class WorldGeneratorExtensions
    {
        static readonly Func<WorldGenerator, float, float, bool, float> getBaseHeight =
            AccessTools.MethodDelegate<Func<WorldGenerator, float, float, bool, float>>(
                AccessTools.Method(typeof(WorldGenerator), "GetBaseHeight"));

        /// <summary>The game's GetBaseHeight, with all patches (Scotheim's included) applied.</summary>
        internal static float GetBaseHeightPatched(this WorldGenerator generator, float x, float y)
        {
            return getBaseHeight(generator, x, y, false);
        }
    }
}
