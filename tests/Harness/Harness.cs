using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using Scotheim;
using Scotheim.Patches;
using Scotheim.Terrain;

static class Harness
{
    static int fails = 0;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
    static float Alt(float baseHeight) { return baseHeight * 200f - 30f; }

    static int Main()
    {
        var configDir = Path.Combine(BepInEx.Paths.ConfigPath, "expand_world");
        if (Directory.Exists(configDir)) Directory.Delete(configDir, true);

        // Stand-in for EWD's own prefix that swaps a custom biome for its terrain biome.
        var h = new Harmony("harness.ewd");
        h.Patch(AccessTools.Method(typeof(WorldGenerator), "GetBiomeHeight"), prefix: new HarmonyMethod(typeof(ExpandWorldData.TerrainSwap), "Prefix"));

        var plugin = new Plugin();
        typeof(Plugin).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(plugin, null);

        var yaml = Path.Combine(configDir, "expand_biomes_scotheim.yaml");
        var vegYaml = Path.Combine(configDir, "expand_vegetation_scotheim.yaml");
        Check(File.Exists(yaml) && new[] { "biome: highland_moor", "biome: caledonian_forest", "biome: munros" }.All(File.ReadAllText(yaml).Contains)
            && File.Exists(vegYaml) && File.ReadAllText(vegYaml).Contains("prefab: Pinetree_01"),
            "biome and vegetation YAML written for Expand World Data");
        File.WriteAllText(vegYaml, "# edited");
        typeof(ExpandWorld).GetMethod("WriteDefaultFiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);
        Check(File.ReadAllText(vegYaml) == "# edited", "existing YAML is never overwritten");

        var world = new World { m_seed = 12345 };
        var wg = new WorldGenerator(world);
        var settings = new HighlandsSettings();
        float sx, sy;
        HighlandsTerrain.FindSite(settings, (x, y) => Alt(OriginalBaseHeight.Call(wg, x, y, false)), out sx, out sy);
        var hl = new HighlandsTerrain(settings, 12345, sx, sy);
        Console.WriteLine("     site " + sx.ToString("F0") + ", " + sy.ToString("F0"));

        // Sample a grid over the site and the vanilla strip.
        var points = new List<float[]>();
        for (float x = sx - 3500; x <= sx + 3500; x += 97) for (float y = sy - 3500; y <= sy + 3500; y += 89) points.Add(new[] { x, y });
        for (float y = -2000; y <= 2000; y += 211) points.Add(new[] { -7000f, y });

        int raised = 0, vanillaTouched = 0, farTouched = 0; double maxErr = 0;
        foreach (var p in points)
        {
            float raw = OriginalBaseHeight.Call(wg, p[0], p[1], false), patched = wg.PublicBase(p[0], p[1], false);
            float expect = (hl.CarveBase(p[0], p[1], hl.LandBase(p[0], p[1], Alt(raw))) + 30f) / 200f;
            maxErr = Math.Max(maxErr, Math.Abs(patched - expect));
            if (patched != raw) raised++;
            if (Alt(raw) > HighlandsTerrain.VanillaShore && patched != raw) vanillaTouched++;
            if (hl.LandWeight(p[0], p[1]) == 0f && patched != raw) farTouched++;
        }
        Check(raised > 0, "GetBaseHeight raises a landmass (" + raised + "/" + points.Count + " samples)");
        Check(maxErr < 1e-6, "postfix matches LandBase + CarveBase (max err " + maxErr.ToString("E1") + ")");
        Check(vanillaTouched == 0 && farTouched == 0, "vanilla land and open ocean away from the site untouched");
        Check(wg.PublicBase(sx, sy, true) == OriginalBaseHeight.Call(wg, sx, sy, true), "menuTerrain=true left alone");

        // Find one sample of each Highland biome.
        var found = new Dictionary<HighlandBiome, float[]>();
        foreach (var p in points)
        {
            var b = hl.Classify(p[0], p[1], Alt(wg.PublicBase(p[0], p[1], false)), Alt(OriginalBaseHeight.Call(wg, p[0], p[1], false)));
            if (b != HighlandBiome.None && !found.ContainsKey(b)) found[b] = p;
        }
        Check(found.Count == 3, "landmass has Moor, Forest and Munros (" + string.Join(",", found.Keys) + ")");

        // Without custom biomes: vanilla fallbacks on the island, vanilla strip unchanged.
        var fallback = new Dictionary<HighlandBiome, Heightmap.Biome> {
            { HighlandBiome.Moor, Heightmap.Biome.Meadows }, { HighlandBiome.Forest, Heightmap.Biome.BlackForest }, { HighlandBiome.Munros, Heightmap.Biome.Mountain } };
        Check(found.All(kv => wg.GetBiome(kv.Value[0], kv.Value[1]) == fallback[kv.Key]), "without EWD biomes: vanilla Meadows/Black Forest/Mountain on the island");
        Check(wg.GetBiome(-7000f, 0f) == Heightmap.Biome.Meadows, "vanilla land keeps its biome");

        // EWD delivers the custom biomes (as a client receiving names from the server would).
        ExpandWorldData.BiomeManager.SetNames(new Dictionary<Heightmap.Biome, string> {
            { (Heightmap.Biome)1024, "highland_moor" }, { (Heightmap.Biome)2048, "caledonian_forest" }, { (Heightmap.Biome)4096, "munros" } });
        var custom = new Dictionary<HighlandBiome, Heightmap.Biome> {
            { HighlandBiome.Moor, (Heightmap.Biome)1024 }, { HighlandBiome.Forest, (Heightmap.Biome)2048 }, { HighlandBiome.Munros, (Heightmap.Biome)4096 } };
        Check(found.All(kv => wg.GetBiome(kv.Value[0], kv.Value[1]) == custom[kv.Key]), "after EWD SetNames: custom biome IDs assigned");
        UnityEngine.Color c;

        // Heights: the patch must see the custom biome even though EWD's prefix swaps it for the terrain biome.
        foreach (var kv in found)
        {
            float x = kv.Value[0], y = kv.Value[1];
            float preGlen = hl.LandBase(x, y, Alt(OriginalBaseHeight.Call(wg, x, y, false)));
            float vanillaH = wg.PublicBase(x, y, false) * 200f - 30f; // stub's terrain function is base height
            float expect;
            switch (kv.Key)
            {
                case HighlandBiome.Munros: expect = hl.ShapeMountain(x, y, preGlen); break;
                case HighlandBiome.Moor: expect = hl.ApplyLochs(x, y, hl.ShapeMoorland(x, y, vanillaH), preGlen); break;
                default: expect = hl.ApplyLochs(x, y, hl.ShapeForest(x, y, vanillaH), preGlen); break;
            }
            float got = wg.GetBiomeHeight(custom[kv.Key], x, y, out c) - 30f;
            Check(Math.Abs(got - expect) < 1e-3, kv.Key + " height reshaped under EWD terrain swap (" + got.ToString("F1") + " m)");
        }
        float mainland = wg.GetBiomeHeight(Heightmap.Biome.Meadows, -7000f, 0f, out c);
        Check(mainland == wg.PublicBase(-7000f, 0f, false) * 200f, "vanilla Meadows on the mainland not reshaped");

        var menuWg = new WorldGenerator(new World { m_menu = true, m_seed = 12345 });
        Check(menuWg.PublicBase(sx, sy, false) == OriginalBaseHeight.Call(menuWg, sx, sy, false), "menu world untouched");

        var world2 = new World { m_seed = 12345 };
        Func<WorldGenerator, int, float> sample = (g, i) => { UnityEngine.Color cc; float x = sx - 2000 + (i % 200) * 20, y = sy - 2000 + (i / 200) * 20;
            return g.GetBiomeHeight(g.GetBiome(x, y), x, y, out cc); };
        var wgA = new WorldGenerator(world);
        var seq = Enumerable.Range(0, 20000).Select(i => sample(wgA, i)).ToArray();
        var par = new float[seq.Length];
        Parallel.For(0, par.Length, i => { par[i] = sample(new WorldGenerator(i % 2 == 0 ? world : world2), i); });
        Check(seq.SequenceEqual(par), "parallel evaluation (incl. two worlds) is deterministic");

        Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
        return fails;
    }
}
