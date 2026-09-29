using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Scotheim.Terrain;

namespace Scotheim.Patches
{
    /// <summary>
    /// Bridge to Expand World Data, which owns custom biomes: names, weather, terrain textures,
    /// map colours and (later) spawns and vegetation. Soft dependency via reflection, so Scotheim
    /// still loads without it and falls back to vanilla Meadows / Black Forest / Mountain.
    ///
    /// EWD numbers custom biomes by their order in its YAML files, so the IDs are looked up by name,
    /// and looked up again whenever EWD reloads or receives biome data from the server.
    /// </summary>
    static class ExpandWorld
    {
        public const string Guid = "expand_world_data";
        public const string MoorId = "highland_moor";
        public const string ForestId = "caledonian_forest";
        public const string MunrosId = "munros";
        const string FileName = "expand_biomes_scotheim.yaml";

        sealed class Ids
        {
            public Heightmap.Biome Moor = Heightmap.Biome.Meadows;
            public Heightmap.Biome Forest = Heightmap.Biome.BlackForest;
            public Heightmap.Biome Munros = Heightmap.Biome.Mountain;
            public bool Custom;
        }

        static readonly Func<string, Heightmap.Biome> getBiome = BindGetBiome();
        static volatile Ids ids;
        static bool warned;

        internal static bool Present { get { return getBiome != null; } }

        static Func<string, Heightmap.Biome> BindGetBiome()
        {
            var method = AccessTools.Method("ExpandWorldData.BiomeManager:GetBiome", new[] { typeof(string) });
            if (method == null || method.ReturnType != typeof(Heightmap.Biome)) return null;
            return (Func<string, Heightmap.Biome>)Delegate.CreateDelegate(typeof(Func<string, Heightmap.Biome>), method);
        }

        static Ids Current
        {
            get
            {
                var current = ids;
                if (current == null) current = ResolveIds();
                return current;
            }
        }

        internal static void Resolve() { ResolveIds(); }

        static Ids ResolveIds()
        {
            var result = new Ids();
            if (getBiome != null)
            {
                var moor = getBiome(MoorId);
                var forest = getBiome(ForestId);
                var munros = getBiome(MunrosId);
                if (moor != Heightmap.Biome.None && forest != Heightmap.Biome.None && munros != Heightmap.Biome.None)
                {
                    result.Moor = moor;
                    result.Forest = forest;
                    result.Munros = munros;
                    result.Custom = true;
                }
                else if (!warned)
                {
                    warned = true;
                    Plugin.Log.LogWarning("Expand World Data is installed but doesn't define " + MoorId + ", " + ForestId + " and " + MunrosId +
                        " (yet). Using vanilla biomes on the Highlands until it does. Check " + FileName + " in the expand_world config folder.");
                }
            }
            ids = result;
            return result;
        }

        internal static Heightmap.Biome ToGame(HighlandBiome biome)
        {
            var current = Current;
            switch (biome)
            {
                case HighlandBiome.Moor: return current.Moor;
                case HighlandBiome.Forest: return current.Forest;
                case HighlandBiome.Munros: return current.Munros;
                default: return Heightmap.Biome.None;
            }
        }

        /// <summary>Which Highland biome a game biome is. Vanilla biomes only count when EWD isn't supplying custom ones.</summary>
        internal static HighlandBiome FromGame(Heightmap.Biome biome, out bool custom)
        {
            var current = Current;
            custom = current.Custom;
            if (biome == current.Moor) return HighlandBiome.Moor;
            if (biome == current.Forest) return HighlandBiome.Forest;
            if (biome == current.Munros) return HighlandBiome.Munros;
            return HighlandBiome.None;
        }

        /// <summary>Writes Scotheim's biome definitions for EWD, unless the file already exists (so edits stick).</summary>
        internal static void WriteDefaultBiomes()
        {
            if (!Present) return;
            try
            {
                var dir = Path.Combine(Paths.ConfigPath, "expand_world");
                var path = Path.Combine(dir, FileName);
                if (File.Exists(path)) return;
                Directory.CreateDirectory(dir);
                File.WriteAllText(path, DefaultBiomesYaml);
                Plugin.Log.LogInfo("Wrote " + path);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Couldn't write " + FileName + ": " + e.Message);
            }
        }

        // Terrain colours pick which vanilla ground textures a biome uses; all four channels matter
        // (EWD reads a missing alpha as 1). Values are vanilla's Plains, Black Forest and Mountain.
        // "nature" decides vegetation and plant rules. For now it borrows the vanilla biome, so trees
        // appear; custom vegetation and spawns come next.
        const string DefaultBiomesYaml =
@"# Scotheim's Highland biomes for Expand World Data.
# Scotheim writes this file only if it's missing, so edit freely; delete it to get the defaults back.
# Scotheim finds the biomes by their 'biome' identifiers below, so keep those.
# After editing, restart the world so biome IDs and terrain agree.

- biome: highland_moor
  name: Highland Moor
  terrain: Meadows
  nature: Meadows
  colorTerrain: 0, 0, 0, 1
  colorMap: 0.53, 0.46, 0.44, 1
  environments:
  - environment: Misty
    weight: 2
  - environment: LightRain
    weight: 2
  - environment: Rain
    weight: 1
  - environment: Heath clear
    weight: 1

- biome: caledonian_forest
  name: Caledonian Forest
  terrain: BlackForest
  nature: BlackForest
  colorTerrain: 0, 0, 1, 0
  colorMap: 0.24, 0.32, 0.2, 1
  environments:
  - environment: DeepForest Mist
    weight: 2
  - environment: Rain
    weight: 1
  - environment: LightRain
    weight: 1

- biome: munros
  name: Munros
  terrain: Mountain
  nature: Mountain
  colorTerrain: 0, 1, 0, 0
  colorMap: 0.6, 0.6, 0.58, 1
  environments:
  - environment: Snow
    weight: 2
  - environment: SnowStorm
    weight: 1
  - environment: Misty
    weight: 1
";
    }

    /// <summary>Look the biome IDs up again whenever EWD loads biome data or receives names from the server.</summary>
    [HarmonyPatch]
    static class ReResolveOnEwdReload
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            var type = AccessTools.TypeByName("ExpandWorldData.BiomeManager");
            foreach (var name in new[] { "Load", "LoadNames", "SetNames" })
            {
                foreach (var method in AccessTools.GetDeclaredMethods(type))
                    if (method.Name == name) yield return method;
            }
        }

        static bool Prepare()
        {
            return AccessTools.TypeByName("ExpandWorldData.BiomeManager") != null;
        }

        static void Postfix()
        {
            ExpandWorld.Resolve();
        }
    }
}
