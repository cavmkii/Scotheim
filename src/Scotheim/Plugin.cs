using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Scotheim.Patches;
using Scotheim.Terrain;

namespace Scotheim
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(ExpandWorld.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(JotunnGuid, BepInDependency.DependencyFlags.HardDependency)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "cavmkii.scotheim";
        public const string Name = "Scotheim";
        public const string Version = "0.3.0";
        const string JotunnGuid = "com.jotunn.jotunn";

        // Creatures and items (Content/). Kept out of this file so the patch harness can build without Jötunn.
        partial void RegisterContent();

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        // Terrain settings are bound here and snapshotted per world (see WorldGen.For).
        readonly HighlandsSettings defaults = new HighlandsSettings();
        internal ConfigEntry<bool> TerrainEnabled;
        readonly System.Collections.Generic.List<Action<HighlandsSettings>> readers =
            new System.Collections.Generic.List<Action<HighlandsSettings>>();

        void Awake()
        {
            Instance = this;
            Log = Logger;

            TerrainEnabled = Config.Bind("1 - General", "Enabled", true,
                "Add the Highlands landmass. Every player and the server must use identical settings, and it only " +
                "looks right on a NEW world: explored zones keep their saved objects at the old ground height.");

            var count = Config.Bind("2 - Landmass", "Count", defaults.LandmassCount,
                new ConfigDescription("How many Highland islands to place. Fewer are placed if the distance band runs out of open water.",
                    new AcceptableValueRange<int>(1, 8)));
            readers.Add(s => s.LandmassCount = count.Value);
            BindFloat("2 - Landmass", "MinScale", 0.3f, 1f, s => s.LandmassMinScale, (s, v) => s.LandmassMinScale = v,
                "Size of the smallest island relative to the largest; the others shrink evenly between.");
            var auto = Config.Bind("2 - Landmass", "AutoPlace", defaults.LandmassAuto,
                "Pick sites from the world seed: the most open stretches of ocean in the distance band below. " +
                "Turn off to place a single island at X/Y.");
            readers.Add(s => s.LandmassAuto = auto.Value);
            BindFloat("2 - Landmass", "X", -10000f, 10000f, s => s.LandmassX, (s, v) => s.LandmassX = v, "Centre, east-west (m). Used when AutoPlace is off.");
            BindFloat("2 - Landmass", "Y", -10000f, 10000f, s => s.LandmassY, (s, v) => s.LandmassY = v, "Centre, north-south (m). Used when AutoPlace is off.");
            BindFloat("2 - Landmass", "MinRadius", 1000f, 9500f, s => s.LandmassMinRadius, (s, v) => s.LandmassMinRadius = v, "AutoPlace searches from this distance from the world centre...");
            BindFloat("2 - Landmass", "MaxRadius", 1000f, 9500f, s => s.LandmassMaxRadius, (s, v) => s.LandmassMaxRadius = v, "...to this one. Default 5-8.5 km: past the Plains, alongside the Mistlands.");
            BindFloat("2 - Landmass", "PreferredRadius", 1000f, 9500f, s => s.LandmassPreferredRadius, (s, v) => s.LandmassPreferredRadius = v, "Among equally open sites, prefer this distance.");
            BindFloat("2 - Landmass", "Length", 500f, 5000f, s => s.LandmassLength, (s, v) => s.LandmassLength = v, "Half-length of the largest island along the grain (m).");
            BindFloat("2 - Landmass", "Width", 500f, 5000f, s => s.LandmassWidth, (s, v) => s.LandmassWidth = v, "Half-width of the largest island across the grain (m).");
            BindFloat("2 - Landmass", "CoreHeight", 60f, 250f, s => s.LandmassCoreHeight, (s, v) => s.LandmassCoreHeight = v, "Interior height before glens (m above sea). Above 50 becomes Munros.");
            BindFloat("2 - Landmass", "MassifThreshold", -0.5f, 0.5f, s => s.MassifThreshold, (s, v) => s.MassifThreshold = v, "Higher = fewer, smaller hill massifs (Munros) and more open moor.");
            BindFloat("2 - Landmass", "MunroMinHeight", 20f, 100f, s => s.MunroMinHeight, (s, v) => s.MunroMinHeight = v, "Base height (m) where the Munros begin. Lower = more bare hill.");
            BindFloat("2 - Landmass", "CoastRoughness", 0f, 0.6f, s => s.CoastRoughness, (s, v) => s.CoastRoughness = v, "0 = smooth ellipse; higher = more headlands, bays and islets.");
            BindFloat("2 - Landmass", "ForestCover", 0f, 1f, s => s.ForestCover, (s, v) => s.ForestCover = v, "Rough fraction of ground below the Munros that is Caledonian Forest rather than Moor.");

            BindFloat("3 - Glens", "Strength", 0f, 1f, s => s.GlenStrength, (s, v) => s.GlenStrength = v,
                "0 disables glens. Glens are cut into the base height the game uses to choose biomes, so glen floors drop out of the Munros.");
            BindFloat("3 - Glens", "GrainAzimuth", 0f, 180f, s => s.GrainAzimuth, (s, v) => s.GrainAzimuth = v,
                "Trend of the main glens, degrees clockwise from north. The Great Glen and Highland Boundary Fault run about 040.");
            BindFloat("3 - Glens", "Spacing", 400f, 5000f, s => s.GlenSpacing, (s, v) => s.GlenSpacing = v, "Typical distance between parallel glens (m).");
            BindFloat("3 - Glens", "Length", 500f, 10000f, s => s.GlenLength, (s, v) => s.GlenLength = v, "How far glens run straight along the grain (m).");
            BindFloat("3 - Glens", "HalfWidth", 50f, 600f, s => s.GlenHalfWidth, (s, v) => s.GlenHalfWidth = v,
                "Nominal axis-to-shoulder distance (m). The effective width comes out ~0.6-0.8x this.");
            BindFloat("3 - Glens", "TributarySpacing", 500f, 100000f, s => s.TributarySpacing, (s, v) => s.TributarySpacing = v, "Spacing of cross-grain side glens (m). Large = few.");
            BindFloat("3 - Glens", "TributaryHalfWidth", 0f, 400f, s => s.TributaryHalfWidth, (s, v) => s.TributaryHalfWidth = v, "0 disables side glens.");
            BindFloat("3 - Glens", "FloorMin", 0f, 49f, s => s.GlenFloorMin, (s, v) => s.GlenFloorMin = v, "Lowest glen floor (m above sea).");
            BindFloat("3 - Glens", "FloorMax", 20f, 49f, s => s.GlenFloorMax, (s, v) => s.GlenFloorMax = v, "Highest glen floor. Keep below 50, or glen floors stay Munros.");
            BindFloat("3 - Glens", "ProfileExponent", 1f, 4f, s => s.GlenProfileExponent, (s, v) => s.GlenProfileExponent = v,
                "b in z ~ |x|^b for the cross-profile. 2 = parabolic U; real troughs mostly measure 1.5-2.5.");
            BindFloat("3 - Glens", "LochFrequency", 0f, 1f, s => s.LochFrequency, (s, v) => s.LochFrequency = v, "Rough fraction of deep glen floor flooded as ribbon lochs.");
            BindFloat("3 - Glens", "LochDepth", 1f, 30f, s => s.LochDepth, (s, v) => s.LochDepth = v, "Loch depth below sea level (m).");

            BindFloat("4 - Munros", "MassifLift", 0.5f, 2.5f, s => s.MassifLift, (s, v) => s.MassifLift = v, "Multiplier on base height above the 50 m Munros threshold.");
            BindFloat("4 - Munros", "MunroSpacing", 150f, 1500f, s => s.MunroSpacing, (s, v) => s.MunroSpacing = v, "Grid spacing of rounded summits (m).");
            BindFloat("4 - Munros", "MunroHeight", 0f, 200f, s => s.MunroHeight, (s, v) => s.MunroHeight = v, "Maximum dome height above the massif (m).");
            BindFloat("4 - Munros", "SummitCapStart", 60f, 400f, s => s.SummitCapStart, (s, v) => s.SummitCapStart = v, "Heights above this are compressed (m above sea).");
            BindFloat("4 - Munros", "SummitCapRange", 0f, 300f, s => s.SummitCapRange, (s, v) => s.SummitCapRange = v, "Max extra height above SummitCapStart. 0 disables the cap.");
            BindFloat("4 - Munros", "CragAmplitude", 0f, 30f, s => s.CragAmplitude, (s, v) => s.CragAmplitude = v, "Small ridged relief (m). Vanilla's jagged relief is replaced, not blended.");
            BindFloat("4 - Munros", "CorrieChance", 0f, 1f, s => s.CorrieChance, (s, v) => s.CorrieChance = v, "Fraction of summits with a corrie.");
            BindFloat("4 - Munros", "CorrieAzimuth", 0f, 360f, s => s.CorrieAzimuth, (s, v) => s.CorrieAzimuth = v, "Direction corries face. Scottish corries mostly face N-NE.");
            BindFloat("4 - Munros", "CorrieSpread", 0f, 180f, s => s.CorrieSpread, (s, v) => s.CorrieSpread = v, "+/- random spread on corrie direction (degrees).");

            BindFloat("5 - Moor", "RollAmplitude", 0f, 20f, s => s.MoorRollAmplitude, (s, v) => s.MoorRollAmplitude = v, "Broad rolling relief (m).");
            BindFloat("5 - Moor", "RollScale", 50f, 2000f, s => s.MoorRollScale, (s, v) => s.MoorRollScale = v, "Wavelength of rolling relief (m).");
            BindFloat("5 - Moor", "HummockAmplitude", 0f, 5f, s => s.HummockAmplitude, (s, v) => s.HummockAmplitude = v, "Small-scale hummocks on moor and forest floor (m).");
            BindFloat("5 - Moor", "HummockScale", 10f, 200f, s => s.HummockScale, (s, v) => s.HummockScale = v, "Wavelength of hummocks (m).");
            BindFloat("5 - Moor", "LochanFrequency", 0f, 0.5f, s => s.LochanFrequency, (s, v) => s.LochanFrequency = v,
                "Rough fraction of low moor that becomes lochans. Only possible within LochanMaxHeight of sea level: Valheim has one water plane.");
            BindFloat("5 - Moor", "LochanMaxHeight", 0f, 30f, s => s.LochanMaxHeight, (s, v) => s.LochanMaxHeight = v, "Ground above this never becomes a lochan (m).");

            BindFloat("6 - Caledonian Forest", "DrumlinAmplitude", 0f, 20f, s => s.DrumlinAmplitude, (s, v) => s.DrumlinAmplitude = v, "Drumlin height (m).");
            BindFloat("6 - Caledonian Forest", "DrumlinCoverage", 0f, 1f, s => s.DrumlinCoverage, (s, v) => s.DrumlinCoverage = v, "Rough fraction of ground covered by drumlins.");

            Patches.ExpandWorld.WriteDefaultFiles();
            RegisterContent();
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
