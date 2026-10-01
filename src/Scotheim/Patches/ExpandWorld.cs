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
        /// With "Spawn data" on, EWD writes the game's own spawns to expand_spawns.yaml, and EWD 1.73 writes the four
        /// "Fimbulvinter - …" entries (an event's spawns) without a biome, which it then reads as every biome: meteors,
        /// Jotuns and Elakingar everywhere. Those entries get "biome: None" here, now and whenever the file is written
        /// (EWD reloads it when it changes). Nothing else in the file is touched, and a fixed file is left alone.
        /// </summary>
        internal static void WatchSpawnDump()
        {
            var dir = Path.Combine(Paths.ConfigPath, "expand_world");
            try
            {
                FixSpawnDump(Path.Combine(dir, SpawnDump));
                Directory.CreateDirectory(dir);
                spawnWatcher = new FileSystemWatcher(dir, SpawnDump);
                spawnWatcher.Created += (o, e) => FixSpawnDump(e.FullPath);
                spawnWatcher.Changed += (o, e) => FixSpawnDump(e.FullPath);
                spawnWatcher.EnableRaisingEvents = true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Couldn't watch " + SpawnDump + " (" + e.Message + "). If meteors fall everywhere, add " +
                    "\"biome: None\" to its Fimbulvinter entries.");
            }
        }

        const string SpawnDump = "expand_spawns.yaml";
        static FileSystemWatcher spawnWatcher;
        static readonly object spawnLock = new object();

        static void FixSpawnDump(string path)
        {
            lock (spawnLock)
            {
                string text = null;
                for (int attempt = 0; attempt < 5 && text == null; attempt++)
                {
                    try { if (!File.Exists(path)) return; text = File.ReadAllText(path); }
                    catch (IOException) { System.Threading.Thread.Sleep(200); } // EWD may still be writing it
                }
                if (text == null) return;
                var lines = new List<string>(text.Replace("\r", "").Split('\n'));
                int fixedCount = 0;
                for (int start = 0; start < lines.Count; start++)
                {
                    if (!lines[start].StartsWith("- ")) continue;
                    int end = start + 1;
                    while (end < lines.Count && !lines[end].StartsWith("- ")) end++;
                    bool fimbul = false, hasBiome = lines[start].StartsWith("- biome:");
                    for (int j = start; j < end; j++)
                    {
                        var line = lines[j].TrimStart('-', ' ');
                        if (line.StartsWith("name: Fimbulvinter")) fimbul = true;
                        if (line.StartsWith("biome:")) hasBiome = true;
                    }
                    if (!fimbul || hasBiome) continue;
                    lines.Insert(start + 1, "  biome: None");
                    fixedCount++;
                }
                if (fixedCount == 0) return;
                try
                {
                    File.WriteAllText(path, string.Join("\n", lines.ToArray()));
                    Plugin.Log.LogInfo("Gave " + fixedCount + " Fimbulvinter spawns in " + path + " \"biome: None\", so they don't spawn everywhere.");
                }
                catch (IOException e) { Plugin.Log.LogWarning("Couldn't fix " + path + " (" + e.Message + ")."); }
            }
        }

        /// <summary>
        /// EWD loads Scotheim's spawn file only with its "Spawn data" setting on, which is off by default, and its
        /// "Event data" setting breaks EWD's startup on this game version (see Content/Raid.cs). These are set here so
        /// nobody has to edit EWD's config. Reached through BepInEx's plugin list by reflection; EWD writes the change
        /// to its config file. EWD reads some settings only as it starts, which was before this ran, so a value that
        /// had to change is logged: it applies fully from the next start.
        /// </summary>
        internal static void ApplyEwdSettings()
        {
            try
            {
                var loader = Type.GetType("BepInEx.Bootstrap.Chainloader, BepInEx");
                var infos = loader != null ? loader.GetProperty("PluginInfos", BindingFlags.Static | BindingFlags.Public).GetValue(null, null) as System.Collections.IDictionary : null;
                var info = infos != null && infos.Contains(Guid) ? infos[Guid] : null;
                var plugin = info != null ? info.GetType().GetProperty("Instance").GetValue(info, null) : null;
                var config = plugin != null ? plugin.GetType().GetProperty("Config").GetValue(plugin, null) : null;
                if (config == null) return; // EWD isn't installed: the warning about that comes from elsewhere
                var changed = new List<string>();
                foreach (var setting in new[] { new KeyValuePair<string, bool>("Spawn data", true), new KeyValuePair<string, bool>("Event data", false) })
                {
                    object entry = null;
                    foreach (var key in (System.Collections.IEnumerable)config.GetType().GetProperty("Keys").GetValue(config, null))
                        if ((string)key.GetType().GetProperty("Key").GetValue(key, null) == setting.Key)
                            entry = config.GetType().GetProperty("Item").GetValue(config, new[] { key });
                    var boxed = entry != null ? entry.GetType().GetProperty("BoxedValue") : null;
                    if (boxed == null)
                    {
                        Plugin.Log.LogWarning("Couldn't find EWD's \"" + setting.Key + "\" setting. In expand_world_data.cfg, set " +
                            setting.Key + " = " + (setting.Value ? "true" : "false") + ".");
                        continue;
                    }
                    if (boxed.GetValue(entry, null) is bool && (bool)boxed.GetValue(entry, null) == setting.Value) continue;
                    boxed.SetValue(entry, setting.Value, null);
                    changed.Add(setting.Key + " = " + (setting.Value ? "true" : "false"));
                }
                if (changed.Count > 0)
                    Plugin.Log.LogWarning("Set EWD's " + string.Join(" and ", changed.ToArray()) + " for Scotheim. If this is the " +
                        "first start with Scotheim, restart the game once before making a world so EWD picks it up.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Couldn't set EWD's settings (" + e.Message + "). In expand_world_data.cfg, set " +
                    "Spawn data = true and Event data = false.");
            }
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
            RemoveRetiredFiles();
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

        // Files earlier Scotheims wrote that are no longer used, with every version shipped. An unedited copy is
        // deleted; an edited one is left alone with a warning.
        static readonly Dictionary<string, string[]> Retired = new Dictionary<string, string[]>
        {
            // EWD 1.73's "Event data" breaks its own startup on this game version; the Sluagh raid is in code now.
            { "expand_events_scotheim.yaml", new[] { "73dd3b0fe656aa0c" } },
            // Symbol stones copy the vanilla runestone location now (Content/Places.cs).
            { "scotheim_symbolstone1.blueprint", new[] { "f8d04c6497aa417e" } },
            { "scotheim_symbolstone2.blueprint", new[] { "44c167ee296a390a" } },
            { "scotheim_symbolstone3.blueprint", new[] { "d30279e6b2fe9d01" } },
            { "scotheim_symbolstone4.blueprint", new[] { "78c4921345d38de7" } },
            { "scotheim_symbolstone5.blueprint", new[] { "761413ef9151cbea" } },
            // The Grey Man's altar is part of a copied runestone location now, with its stone (Content/Places.cs).
            { "scotheim_greyman.blueprint", new[] { "22db0e188f20bc43" } },
        };

        static void RemoveRetiredFiles()
        {
            foreach (var entry in Retired)
            {
                var dir = Path.Combine(Paths.ConfigPath, entry.Key.EndsWith(".blueprint") ? "PlanBuild" : "expand_world");
                var path = Path.Combine(dir, entry.Key);
                try
                {
                    if (!File.Exists(path)) continue;
                    if (Array.IndexOf(entry.Value, Fingerprint(File.ReadAllText(path))) >= 0)
                    {
                        File.Delete(path);
                        Plugin.Log.LogInfo("Removed " + path + " (no longer used by Scotheim).");
                    }
                    else Plugin.Log.LogWarning(path + " is no longer used by Scotheim but has local edits, so it was kept.");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("Couldn't remove " + path + ": " + e.Message);
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
            "scotheim_cairn.blueprint", "scotheim_greymanstone.blueprint",
        };

        // Fingerprints of every version earlier releases wrote (see git history of src/Scotheim/Data).
        static readonly Dictionary<string, string[]> Shipped = new Dictionary<string, string[]>
        {
            // tests/Data/check_data.py checks this list against git history.
            { FileName, new[] { "e7fd26f1f5d69f39", "063f4b3eae79531a", "47c982eb6dd734ee", "a6262f4eff3dc651" } },
            { "expand_vegetation_scotheim.yaml", new[] { "f52a0b849de4f93e", "08c9860a31fa88fa", "67ce30b011c4b0fa", "5701be8eddf32039", "ea39f5128c57a39a", "5074ac3eed8be0de", "3a3641781cb81f63", "fedff3064f4fd3c7", "ecaec4640f6da63c", "1d07cbddecee2262", "35284329bcb27cd7", "ba725ab10cdb397f", "8505df72bcafbdb3", "8f97233333b23163", "cab9ff7298d6ad20" } },
            { "expand_clutter_scotheim.yaml", new[] { "f186b91465d1e721", "facdfd177fc3d3ed", "c56b433982d86238" } },
            { "expand_spawns_scotheim.yaml", new[] { "e3fe9b1655bcf793" } },
            { "expand_locations_scotheim.yaml", new[] { "ce145deb2eded126", "2a726b435d06f2f4", "2d799944054d17f9", "cc8d5b7368e22015", "dc9432fb0422aa35", "c4a1d1a5e06e3e7e", "fe12fc34ff080dbe", "39cbce773f75fe14", "8e87be14fbcf04ce", "241fbc260a58f1ef", "0b8ec0a5a7d53bae" } },
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
