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

        /// <summary>
        /// Writes Scotheim's biome, vegetation and clutter definitions into EWD's config folder, each only if
        /// it's missing, so edits stick. EWD reads every expand_biomes*.yaml / expand_vegetation*.yaml / expand_clutter*.yaml
        /// there, so these sit alongside its own files and are synced from the server.
        /// </summary>
        internal static void WriteDefaultFiles()
        {
            if (!Present) return;
            var dir = Path.Combine(Paths.ConfigPath, "expand_world");
            foreach (var name in DataFiles)
            {
                try
                {
                    var path = Path.Combine(dir, name);
                    if (File.Exists(path)) continue;
                    Directory.CreateDirectory(dir);
                    using (var stream = typeof(ExpandWorld).Assembly.GetManifestResourceStream("Scotheim.Data." + name))
                    using (var file = File.Create(path))
                        stream.CopyTo(file);
                    Plugin.Log.LogInfo("Wrote " + path);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("Couldn't write " + name + ": " + e.Message);
                }
            }
        }

        static readonly string[] DataFiles = { FileName, "expand_vegetation_scotheim.yaml", "expand_clutter_scotheim.yaml" };
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
