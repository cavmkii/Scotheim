// Minimal stand-ins for the Unity, BepInEx and Valheim types Scotheim touches. Shapes mirror the
// real game (private GetBaseHeight/m_world, flags enum values) but the bodies are fake.
using System; using System.Collections.Generic;
namespace UnityEngine { public struct Color { } public class Object { public string name; } public class GameObject : Object { } public class MonoBehaviour { } }
namespace BepInEx.Logging { public class ManualLogSource { public List<string> Lines = new List<string>(); public void LogInfo(object o){Lines.Add("I "+o);Console.WriteLine("[info] "+o);} public void LogWarning(object o){Lines.Add("W "+o);Console.WriteLine("[warn] "+o);} public void LogError(object o){Lines.Add("E "+o);Console.WriteLine("[err] "+o);} } }
namespace BepInEx.Configuration {
  public abstract class AcceptableValueBase { }
  public class AcceptableValueRange<T> : AcceptableValueBase { public AcceptableValueRange(T a, T b) { } }
  public class ConfigDescription { public ConfigDescription(string d, AcceptableValueBase v) { } }
  public class ConfigEntry<T> { public T Value; }
  public class ConfigFile { public Dictionary<string, object> All = new Dictionary<string, object>();
    public ConfigEntry<T> Bind<T>(string s, string k, T d, string desc) { var e = new ConfigEntry<T> { Value = d }; All[s + "/" + k] = e; return e; }
    public ConfigEntry<T> Bind<T>(string s, string k, T d, ConfigDescription desc) { var e = new ConfigEntry<T> { Value = d }; All[s + "/" + k] = e; return e; } } }
namespace BepInEx {
  public static class Paths { public static string ConfigPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "scotheim-harness-config"); }
  [AttributeUsage(AttributeTargets.Class)] public class BepInPlugin : Attribute { public BepInPlugin(string a, string b, string c) { } }
  [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public class BepInDependency : Attribute { public enum DependencyFlags { HardDependency = 1, SoftDependency = 2 } public BepInDependency(string guid, DependencyFlags flags) { } }
  public class BaseUnityPlugin : UnityEngine.MonoBehaviour { public Configuration.ConfigFile Config = new Configuration.ConfigFile(); protected Logging.ManualLogSource Logger = new Logging.ManualLogSource(); } }
public class World { public bool m_menu; public int m_seed; }
public class Heightmap { [Flags] public enum Biome { None = 0, Meadows = 1, Swamp = 2, Mountain = 4, BlackForest = 8, Plains = 16, AshLands = 32, DeepNorth = 64, Ocean = 256, Mistlands = 512 } }
public class WorldGenerator {
  private World m_world; public WorldGenerator(World w) { m_world = w; }
  // Fake vanilla: mostly deep ocean, with a strip of land along x < -6000 so "vanilla land" exists.
  private float GetBaseHeight(float wx, float wy, bool menuTerrain) { return wx < -6000f ? 0.3f : 0.05f + 0.02f * (float)Math.Sin(wy / 900.0); }
  public float GetBiomeHeight(Heightmap.Biome biome, float wx, float wy, out UnityEngine.Color mask, bool preGeneration = false, bool riverPreDN = true) { mask = default(UnityEngine.Color); return GetBaseHeight(wx, wy, false) * 200f; }
  // Vanilla-ish: ocean by base height, otherwise Meadows.
  public Heightmap.Biome GetBiome(float wx, float wy, float oceanLevel = 0.02f, bool waterAlwaysOcean = false) { return GetBaseHeight(wx, wy, false) * 200f - 30f < -26f ? Heightmap.Biome.Ocean : Heightmap.Biome.Meadows; }
  public float PublicBase(float x, float y, bool menu) { return GetBaseHeight(x, y, menu); } }
namespace ExpandWorldData {
  // Mirrors EWD's name lookup and the swap of a custom biome for its terrain biome before height functions run.
  public class BiomeManager {
    public static System.Collections.Generic.Dictionary<string, Heightmap.Biome> Names = new System.Collections.Generic.Dictionary<string, Heightmap.Biome>();
    public static Heightmap.Biome GetBiome(string name) { Heightmap.Biome b; return Names.TryGetValue(name.ToLowerInvariant(), out b) ? b : Heightmap.Biome.None; }
    public static void SetNames(System.Collections.Generic.Dictionary<Heightmap.Biome, string> names) { Names.Clear(); foreach (var kv in names) Names[kv.Value.ToLowerInvariant()] = kv.Key; }
  }
  public static class TerrainSwap {
    public static Heightmap.Biome Terrain(Heightmap.Biome b) { return b == (Heightmap.Biome)1024 ? Heightmap.Biome.Meadows : b == (Heightmap.Biome)2048 ? Heightmap.Biome.BlackForest : b == (Heightmap.Biome)4096 ? Heightmap.Biome.Mountain : b; }
    public static void Prefix(ref Heightmap.Biome __0) { __0 = Terrain(__0); }
  }
}
public class ZoneSystem { public List<ZoneVegetation> m_vegetation = new List<ZoneVegetation>(); [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] private void Start() { }
  public void CallStart() { Start(); }
  public class ZoneVegetation { public string m_name; public UnityEngine.GameObject m_prefab; public bool m_enable = true; public float m_min, m_max; public Heightmap.Biome m_biome; } }
