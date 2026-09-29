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

        /// <summary>The terrain transform for this generator's world, or null to leave vanilla alone.</summary>
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
                {
                    // Snapshot config now: live edits mid-session would change ground under existing zones.
                    var settings = Plugin.Instance.SnapshotSettings();
                    terrain = new HighlandsTerrain(settings, SeedRef(world));
                    Plugin.Log.LogInfo("Highlands terrain active for seed " + SeedRef(world) + ", signature " + Plugin.Signature(settings));
                }
                previous = latest;
                latest = new Active { World = world, Terrain = terrain };
                return terrain;
            }
        }

        internal static float ToAltitude(float baseHeight) { return baseHeight * 200f - WaterLevel; }
        internal static float FromAltitude(float altitude) { return (altitude + WaterLevel) / 200f; }
    }

    /// <summary>Unpatched GetBaseHeight, for the pre-glen ("raw") base altitude.</summary>
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

    /// <summary>Stage A: glens in the base height, so biome selection and vanilla biome heights follow them.</summary>
    [HarmonyPatch(typeof(WorldGenerator), "GetBaseHeight")]
    static class CarveBaseHeight
    {
        static void Postfix(WorldGenerator __instance, float __0, float __1, bool __2, ref float __result)
        {
            if (__2) return; // menu terrain
            var terrain = WorldGen.For(__instance);
            if (terrain == null) return;
            __result = WorldGen.FromAltitude(terrain.CarveBase(__0, __1, WorldGen.ToAltitude(__result)));
        }
    }

    /// <summary>Stage B: per-biome reshaping of the final height.</summary>
    [HarmonyPatch(typeof(WorldGenerator), "GetBiomeHeight")]
    static class ShapeBiomeHeight
    {
        static void Postfix(WorldGenerator __instance, Heightmap.Biome __0, float __1, float __2, ref float __result)
        {
            if (__0 != Heightmap.Biome.Mountain && __0 != Heightmap.Biome.Meadows && __0 != Heightmap.Biome.BlackForest)
                return;
            var terrain = WorldGen.For(__instance);
            if (terrain == null) return;

            float x = __1, y = __2;
            float raw = WorldGen.ToAltitude(OriginalBaseHeight.Call(__instance, x, y, false));
            float h = __result - WorldGen.WaterLevel;
            switch (__0)
            {
                case Heightmap.Biome.Mountain:
                    // Replaces vanilla's relief; lochs are carved in the same pass.
                    h = terrain.ShapeMountain(x, y, raw);
                    break;
                case Heightmap.Biome.Meadows:
                    h = terrain.ApplyLochs(x, y, terrain.ShapeMoorland(x, y, h), raw);
                    break;
                case Heightmap.Biome.BlackForest:
                    h = terrain.ApplyLochs(x, y, terrain.ShapeForest(x, y, h), raw);
                    break;
            }
            __result = h + WorldGen.WaterLevel;
        }
    }
}
