using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Scotheim.Terrain;

namespace Scotheim
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "cavmkii.scotheim";
        public const string Name = "Scotheim";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        // Terrain settings are bound here and snapshotted per world (see WorldGenPatches.Initialize).
        readonly HighlandsSettings defaults = new HighlandsSettings();
        internal ConfigEntry<bool> TerrainEnabled;
        internal ConfigEntry<bool> VegetationEnabled;
        internal ConfigEntry<string> VegetationRules;
        readonly System.Collections.Generic.List<Action<HighlandsSettings>> readers =
            new System.Collections.Generic.List<Action<HighlandsSettings>>();

        void Awake()
        {
            Instance = this;
            Log = Logger;

            TerrainEnabled = Config.Bind("1 - General", "TerrainEnabled", true,
                "Reshape terrain. Every player and the server must use identical terrain settings, and it only " +
                "looks right on a NEW world: explored zones keep their saved objects at the old ground height.");
            VegetationEnabled = Config.Bind("1 - General", "VegetationEnabled", true,
                "Apply VegetationRules (only affects zones generated after the change).");
            VegetationRules = Config.Bind("1 - General", "VegetationRules",
                "Beech1:Meadows:0.25, Beech_small1:Meadows:0.35, Beech_small2:Meadows:0.35, Oak1:Meadows:0.5, " +
                "FirTree:BlackForest:0.6, FirTree_small:BlackForest:0.6, Pinetree_01:BlackForest:1.8",
                "Comma-separated prefab:biome:multiplier. Multiplies the min/max spawn count of matching vegetation. " +
                "Default thins broadleaf trees off the moor and swaps Black Forest spruce for Scots pine. " +
                "Unmatched rules are logged, so check the log if a prefab name is wrong for your game version.");

            BindFloat("2 - Glens", "Strength", 0f, 1f, s => s.GlenStrength, (s, v) => s.GlenStrength = v,
                "0 disables glens. Glens are cut into the base height the game uses to choose biomes, so glen floors become Meadows/Black Forest.");
            BindFloat("2 - Glens", "GrainAzimuth", 0f, 180f, s => s.GrainAzimuth, (s, v) => s.GrainAzimuth = v,
                "Trend of the main glens, degrees clockwise from north. The Great Glen and Highland Boundary Fault run about 040.");
            BindFloat("2 - Glens", "Spacing", 400f, 5000f, s => s.GlenSpacing, (s, v) => s.GlenSpacing = v, "Typical distance between parallel glens (m).");
            BindFloat("2 - Glens", "Length", 500f, 10000f, s => s.GlenLength, (s, v) => s.GlenLength = v, "How far glens run straight along the grain (m).");
            BindFloat("2 - Glens", "HalfWidth", 50f, 600f, s => s.GlenHalfWidth, (s, v) => s.GlenHalfWidth = v,
                "Nominal axis-to-shoulder distance (m). The effective width comes out ~0.6-0.8x this.");
            BindFloat("2 - Glens", "TributarySpacing", 500f, 100000f, s => s.TributarySpacing, (s, v) => s.TributarySpacing = v, "Spacing of cross-grain side glens (m). Large = few.");
            BindFloat("2 - Glens", "TributaryHalfWidth", 0f, 400f, s => s.TributaryHalfWidth, (s, v) => s.TributaryHalfWidth = v, "0 disables side glens.");
            BindFloat("2 - Glens", "FloorMin", 20f, 49f, s => s.GlenFloorMin, (s, v) => s.GlenFloorMin = v,
                "Lowest glen floor, m above sea. Keep above 20 or floors in the 2-6 km ring may turn into Swamp.");
            BindFloat("2 - Glens", "FloorMax", 20f, 49f, s => s.GlenFloorMax, (s, v) => s.GlenFloorMax = v, "Highest glen floor. Keep below 50, or floors stay Mountain biome.");
            BindFloat("2 - Glens", "ProfileExponent", 1f, 4f, s => s.GlenProfileExponent, (s, v) => s.GlenProfileExponent = v,
                "b in z ~ |x|^b for the cross-profile. 2 = parabolic U; real troughs mostly measure 1.5-2.5.");
            BindFloat("2 - Glens", "MaxRadius", 0f, 12000f, s => s.GlenMaxRadius, (s, v) => s.GlenMaxRadius = v, "Glens fade out over the last 500 m before this distance from the world centre.");
            BindFloat("2 - Glens", "LochFrequency", 0f, 1f, s => s.LochFrequency, (s, v) => s.LochFrequency = v, "Rough fraction of deep glen floor flooded as ribbon lochs.");
            BindFloat("2 - Glens", "LochDepth", 1f, 30f, s => s.LochDepth, (s, v) => s.LochDepth = v, "Loch depth below sea level (m).");

            BindFloat("3 - Mountains", "MassifLift", 0.5f, 2.5f, s => s.MassifLift, (s, v) => s.MassifLift = v, "Multiplier on base height above the 50 m Mountain threshold.");
            BindFloat("3 - Mountains", "MunroSpacing", 150f, 1500f, s => s.MunroSpacing, (s, v) => s.MunroSpacing = v, "Grid spacing of rounded summits (m).");
            BindFloat("3 - Mountains", "MunroHeight", 0f, 200f, s => s.MunroHeight, (s, v) => s.MunroHeight = v, "Maximum dome height above the massif (m).");
            BindFloat("3 - Mountains", "SummitCapStart", 60f, 400f, s => s.SummitCapStart, (s, v) => s.SummitCapStart = v, "Heights above this are compressed (m above sea).");
            BindFloat("3 - Mountains", "SummitCapRange", 0f, 300f, s => s.SummitCapRange, (s, v) => s.SummitCapRange = v, "Max extra height above SummitCapStart. 0 disables the cap.");
            BindFloat("3 - Mountains", "CragAmplitude", 0f, 30f, s => s.CragAmplitude, (s, v) => s.CragAmplitude = v, "Small ridged relief (m). Vanilla's jagged relief is replaced, not blended.");
            BindFloat("3 - Mountains", "CorrieChance", 0f, 1f, s => s.CorrieChance, (s, v) => s.CorrieChance = v, "Fraction of summits with a corrie.");
            BindFloat("3 - Mountains", "CorrieAzimuth", 0f, 360f, s => s.CorrieAzimuth, (s, v) => s.CorrieAzimuth = v, "Direction corries face. Scottish corries mostly face N-NE.");
            BindFloat("3 - Mountains", "CorrieSpread", 0f, 180f, s => s.CorrieSpread, (s, v) => s.CorrieSpread = v, "+/- random spread on corrie direction (degrees).");

            BindFloat("4 - Moorland", "RollAmplitude", 0f, 20f, s => s.MoorRollAmplitude, (s, v) => s.MoorRollAmplitude = v, "Meadows: broad rolling relief (m).");
            BindFloat("4 - Moorland", "RollScale", 50f, 2000f, s => s.MoorRollScale, (s, v) => s.MoorRollScale = v, "Wavelength of rolling relief (m).");
            BindFloat("4 - Moorland", "HummockAmplitude", 0f, 5f, s => s.HummockAmplitude, (s, v) => s.HummockAmplitude = v, "Meadows and Black Forest: small-scale hummocks (m).");
            BindFloat("4 - Moorland", "HummockScale", 10f, 200f, s => s.HummockScale, (s, v) => s.HummockScale = v, "Wavelength of hummocks (m).");
            BindFloat("4 - Moorland", "LochanFrequency", 0f, 0.5f, s => s.LochanFrequency, (s, v) => s.LochanFrequency = v,
                "Rough fraction of low moor that becomes lochans. Only possible within LochanMaxHeight of sea level: Valheim has one water plane.");
            BindFloat("4 - Moorland", "LochanMaxHeight", 0f, 30f, s => s.LochanMaxHeight, (s, v) => s.LochanMaxHeight = v, "Ground above this never becomes a lochan (m).");

            BindFloat("5 - Forest", "DrumlinAmplitude", 0f, 20f, s => s.DrumlinAmplitude, (s, v) => s.DrumlinAmplitude = v, "Black Forest: drumlin height (m).");
            BindFloat("5 - Forest", "DrumlinCoverage", 0f, 1f, s => s.DrumlinCoverage, (s, v) => s.DrumlinCoverage = v, "Rough fraction of ground covered by drumlins.");

            new Harmony(Guid).PatchAll(Assembly.GetExecutingAssembly());
            Log.LogInfo(Name + " " + Version + " loaded. Terrain signature: " + Signature(SnapshotSettings()));
        }

        void BindFloat(string section, string key, float min, float max,
            Func<HighlandsSettings, float> get, Action<HighlandsSettings, float> set, string description)
        {
            var entry = Config.Bind(section, key, get(defaults),
                new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));
            readers.Add(s => set(s, entry.Value));
        }

        internal HighlandsSettings SnapshotSettings()
        {
            var s = new HighlandsSettings();
            foreach (var r in readers) r(s);
            return s;
        }

        /// <summary>
        /// Short hash of the terrain settings. Players can compare it in their logs: peers with
        /// different signatures generate different ground and will see floating or buried objects.
        /// </summary>
        internal static string Signature(HighlandsSettings s)
        {
            uint h = 2166136261;
            foreach (var f in typeof(HighlandsSettings).GetFields())
            {
                var v = f.GetValue(s);
                var text = f.Name + "=" + (v is float ? ((float)v).ToString("R", System.Globalization.CultureInfo.InvariantCulture) : v.ToString());
                foreach (char c in text) { h ^= c; h *= 16777619; }
            }
            return h.ToString("x8");
        }
    }
}
