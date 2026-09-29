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
  [AttributeUsage(AttributeTargets.Class)] public class BepInPlugin : Attribute { public BepInPlugin(string a, string b, string c) { } }
  public class BaseUnityPlugin : UnityEngine.MonoBehaviour { public Configuration.ConfigFile Config = new Configuration.ConfigFile(); protected Logging.ManualLogSource Logger = new Logging.ManualLogSource(); } }
public class World { public bool m_menu; public int m_seed; }
public class Heightmap { [Flags] public enum Biome { None = 0, Meadows = 1, Swamp = 2, Mountain = 4, BlackForest = 8, Plains = 16, AshLands = 32, DeepNorth = 64, Ocean = 256, Mistlands = 512 } }
public class WorldGenerator {
  private World m_world; public WorldGenerator(World w) { m_world = w; }
  // Fake vanilla: a smooth ramp so both upland and lowland exist.
  private float GetBaseHeight(float wx, float wy, bool menuTerrain) { return 0.35f + 0.25f * (float)Math.Sin(wx / 1500.0) * (float)Math.Cos(wy / 1700.0); }
  public float GetBiomeHeight(Heightmap.Biome biome, float wx, float wy, out UnityEngine.Color mask, bool preGeneration = false, bool riverPreDN = true) { mask = default(UnityEngine.Color); return GetBaseHeight(wx, wy, false) * 200f; }
  public float PublicBase(float x, float y, bool menu) { return GetBaseHeight(x, y, menu); } }
public class ZoneSystem { public List<ZoneVegetation> m_vegetation = new List<ZoneVegetation>(); [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] private void Start() { }
  public void CallStart() { Start(); }
  public class ZoneVegetation { public string m_name; public UnityEngine.GameObject m_prefab; public bool m_enable = true; public float m_min, m_max; public Heightmap.Biome m_biome; } }
