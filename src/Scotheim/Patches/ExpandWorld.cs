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
        /// Writes Scotheim's biome, vegetation, clutter and spawn definitions into EWD's config folder. EWD reads
        /// every expand_biomes*.yaml / expand_vegetation*.yaml / expand_clutter*.yaml / expand_spawns*.yaml there,
        /// so these sit alongside its own files and are synced from the server.
        ///
        /// Edits stick: a file is replaced only if it's missing or byte-for-byte a version an earlier Scotheim
        /// wrote. An edited file is left alone and the new default goes next to it as *.yaml.new, which EWD ignores.
        /// </summary>
        internal static void WriteDefaultFiles()
        {
            if (!Present) return;
            foreach (var name in DataFiles)
            {
                // Blueprints go to EWD's blueprint folder (its "Blueprint folder" setting, PlanBuild by default).
                var dir = Path.Combine(Paths.ConfigPath, name.EndsWith(".blueprint") ? "PlanBuild" : "expand_world");
                try
                {
                    string shipped;
                    using (var stream = typeof(ExpandWorld).Assembly.GetManifestResourceStream("Scotheim.Data." + name))
                    using (var reader = new StreamReader(stream))
                        shipped = reader.ReadToEnd();
                    var path = Path.Combine(dir, name);
                    Directory.CreateDirectory(dir);
                    if (!File.Exists(path))
                    {
                        File.WriteAllText(path, shipped);
                        Plugin.Log.LogInfo("Wrote " + path);
                        continue;
                    }
                    var existing = Fingerprint(File.ReadAllText(path));
                    if (existing == Fingerprint(shipped)) continue;
                    string[] older;
                    if (Shipped.TryGetValue(name, out older) && Array.IndexOf(older, existing) >= 0)
                    {
                        File.WriteAllText(path, shipped);
                        Plugin.Log.LogInfo("Updated " + path + " (it was an unedited copy from an older Scotheim).");
                    }
                    else
                    {
                        File.WriteAllText(path + ".new", shipped);
                        Plugin.Log.LogWarning(path + " has local edits, so it was kept. This version's default is in " +
                            name + ".new; merge what you want from it.");
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("Couldn't write " + name + ": " + e.Message);
                }
            }
        }

        /// <summary>SHA-256 prefix of the text with CRs dropped, so a Windows checkout's line endings don't count as edits.</summary>
        internal static string Fingerprint(string text)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text.Replace("\r", "")));
                return BitConverter.ToString(hash, 0, 8).Replace("-", "").ToLowerInvariant();
            }
        }

        static readonly string[] DataFiles =
        {
            FileName, "expand_vegetation_scotheim.yaml", "expand_clutter_scotheim.yaml", "expand_spawns_scotheim.yaml",
            "expand_locations_scotheim.yaml",
            "scotheim_broch.blueprint", "scotheim_crannog.blueprint", "scotheim_shieling.blueprint", "scotheim_stonecircle.blueprint",
            "scotheim_symbolstone1.blueprint", "scotheim_symbolstone2.blueprint", "scotheim_symbolstone3.blueprint",
            "scotheim_symbolstone4.blueprint", "scotheim_symbolstone5.blueprint", "scotheim_cairn.blueprint",
        };

        // Fingerprints of every version earlier releases wrote (see git history of src/Scotheim/Data).
        static readonly Dictionary<string, string[]> Shipped = new Dictionary<string, string[]>
        {
            // tests/Data/check_data.py checks this list against git history.
            { FileName, new[] { "e7fd26f1f5d69f39", "063f4b3eae79531a", "47c982eb6dd734ee", "a6262f4eff3dc651" } },
            { "expand_vegetation_scotheim.yaml", new[] { "f52a0b849de4f93e", "08c9860a31fa88fa", "67ce30b011c4b0fa", "5701be8eddf32039", "ea39f5128c57a39a", "5074ac3eed8be0de" } },
            { "expand_clutter_scotheim.yaml", new[] { "f186b91465d1e721", "facdfd177fc3d3ed" } },
            { "expand_spawns_scotheim.yaml", new[] { "e3fe9b1655bcf793" } },
            { "expand_locations_scotheim.yaml", new[] { "ce145deb2eded126", "2a726b435d06f2f4", "2d799944054d17f9", "cc8d5b7368e22015", "dc9432fb0422aa35" } },
            { "scotheim_shieling.blueprint", new[] { "4e2b1ff402de1f0c" } },
            { "scotheim_cairn.blueprint", new[] { "4b67526b98f77391", "77cb3cbfa4b1f120" } },
        };
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
