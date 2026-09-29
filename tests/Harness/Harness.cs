using System; using System.Linq; using System.Threading.Tasks; using Scotheim; using Scotheim.Terrain; using Scotheim.Patches;
static class Harness {
  static int fails = 0;
  static void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
  static int Main() {
    var plugin = new Plugin();
    typeof(Plugin).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(plugin, null);
    var world = new World { m_seed = 12345 }; var wg = new WorldGenerator(world);
    var hl = new HighlandsTerrain(new HighlandsSettings(), 12345);
    int changed = 0, total = 0; double maxErr = 0;
    for (float x = -3000; x < 3000; x += 37) for (float y = -3000; y < 3000; y += 41) {
      total++;
      float raw = OriginalBaseHeight.Call(wg, x, y, false);
      float patched = wg.PublicBase(x, y, false);
      float expect = (hl.CarveBase(x, y, raw * 200f - 30f) + 30f) / 200f;
      maxErr = Math.Max(maxErr, Math.Abs(patched - expect));
      if (Math.Abs(patched - raw) > 1e-6) changed++;
    }
    Check(changed > 0 && changed < total, $"GetBaseHeight postfix carves some points ({changed}/{total}) and reverse patch returns vanilla");
    Check(maxErr < 1e-6, $"postfix matches HighlandsTerrain.CarveBase (max err {maxErr:E1})");
    Check(wg.PublicBase(100, 100, true) == OriginalBaseHeight.Call(wg, 100, 100, true), "menuTerrain=true left alone");
    UnityEngine.Color c;
    float mx = 1234, my = 567, mraw = OriginalBaseHeight.Call(wg, mx, my, false) * 200f - 30f;
    float m = wg.GetBiomeHeight(Heightmap.Biome.Mountain, mx, my, out c);
    Check(Math.Abs(m - (hl.ShapeMountain(mx, my, mraw) + 30f)) < 1e-3, $"Mountain height replaced ({m:F2})");
    float pl = wg.GetBiomeHeight(Heightmap.Biome.Plains, mx, my, out c);
    Check(Math.Abs(pl - wg.PublicBase(mx, my, false) * 200f) < 1e-3, "Plains untouched by stage B");
    var menuWg = new WorldGenerator(new World { m_menu = true, m_seed = 12345 });
    Check(menuWg.PublicBase(mx, my, false) == OriginalBaseHeight.Call(menuWg, mx, my, false), "menu world untouched");
    var wg2 = new WorldGenerator(world); // switch back to real world
    var seq = Enumerable.Range(0, 20000).Select(i => wg2.GetBiomeHeight(Heightmap.Biome.Meadows, i * 3.1f - 3000, i * 1.7f - 2000, out c)).ToArray();
    var world2 = new World { m_seed = 12345 }; var par = new float[seq.Length];
    Parallel.For(0, par.Length, i => { UnityEngine.Color cc; par[i] = new WorldGenerator(i % 2 == 0 ? world : world2).GetBiomeHeight(Heightmap.Biome.Meadows, i * 3.1f - 3000, i * 1.7f - 2000, out cc); });
    Check(seq.SequenceEqual(par), "parallel evaluation (incl. world switching) is deterministic");
    var zs = new ZoneSystem();
    Func<string, Heightmap.Biome, float, ZoneSystem.ZoneVegetation> V = (n, b, max) => new ZoneSystem.ZoneVegetation { m_prefab = new UnityEngine.GameObject { name = n }, m_biome = b, m_min = 1, m_max = max };
    zs.m_vegetation.Add(V("Beech1", Heightmap.Biome.Meadows, 4)); zs.m_vegetation.Add(V("Beech1", Heightmap.Biome.Plains, 4));
    zs.m_vegetation.Add(V("Pinetree_01", Heightmap.Biome.BlackForest | Heightmap.Biome.Mountain, 2));
    zs.CallStart(); zs.CallStart();
    Check(zs.m_vegetation[0].m_max == 1f && zs.m_vegetation[1].m_max == 4f && Math.Abs(zs.m_vegetation[2].m_max - 3.6f) < 1e-5, "vegetation scaled by biome, not compounded on re-entry");
    Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED"); return fails;
  } }
