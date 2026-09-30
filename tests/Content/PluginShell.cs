// The other half of the partial Plugin class (the real one is src/Scotheim/Plugin.cs, which needs the whole game).
namespace Scotheim { public partial class Plugin : BepInEx.BaseUnityPlugin { internal static BepInEx.Logging.ManualLogSource Log = new BepInEx.Logging.ManualLogSource(); partial void RegisterContent(); } }
namespace Scotheim.Patches { static class WorldGen { internal static volatile int CurrentSeed; } }
namespace Scotheim.Terrain { public enum HighlandBiome { None, Moor, Forest, Munros } }
namespace Scotheim.Patches { static class ExpandWorld { internal static Scotheim.Terrain.HighlandBiome FromGame(Heightmap.Biome biome, out bool custom) { custom = false; return 0; } internal static Heightmap.Biome ToGame(Scotheim.Terrain.HighlandBiome biome) => 0; } }
