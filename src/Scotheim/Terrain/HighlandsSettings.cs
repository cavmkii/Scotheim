namespace Scotheim.Terrain
{
    /// <summary>
    /// All tunables for the Highlands landmass. Heights are metres relative to Valheim's sea level
    /// (the game's water plane sits at y = 30); distances are metres in world space.
    ///
    /// An instance is treated as immutable once a world starts generating: every peer must derive
    /// identical terrain, so the plugin snapshots config into a new instance at world init rather
    /// than letting live config edits leak into half-generated terrain.
    /// </summary>
    public sealed class HighlandsSettings
    {
        // Landmass: raised out of open ocean, so no vanilla land is reshaped.
        /// <summary>How many islands to place (fewer if the distance band runs out of open water).</summary>
        public int LandmassCount = 4;
        /// <summary>The smallest island's size relative to the largest; sizes shrink evenly between.</summary>
        public float LandmassMinScale = 0.65f;
        /// <summary>Pick sites automatically from the seed. Otherwise place a single island at LandmassX/Y.</summary>
        public bool LandmassAuto = true;
        public float LandmassX = 0f;
        public float LandmassY = 0f;
        /// <summary>Auto placement searches this distance band from the world centre (late game by default).</summary>
        public float LandmassMinRadius = 4500f;
        public float LandmassMaxRadius = 8500f;
        public float LandmassPreferredRadius = 7000f;
        /// <summary>Semi-axes of the largest island's ellipse: along the grain, and across it.</summary>
        public float LandmassLength = 2000f;
        public float LandmassWidth = 1150f;
        /// <summary>Base altitude of the interior before glens; above 50 becomes Munros.</summary>
        public float LandmassCoreHeight = 140f;
        /// <summary>How ragged the coastline is (noise on the ellipse distance).</summary>
        public float CoastRoughness = 0.4f;
        /// <summary>Higher = fewer, smaller massifs (Munros) and more open moor. Noise units, roughly -0.4..0.4.</summary>
        public float MassifThreshold = 0.2f;
        /// <summary>Base height (m above sea) where the Munros begin. Lower = more bare hill, same slopes.</summary>
        public float MunroMinHeight = 32f;
        /// <summary>Rough fraction of land below the Munros given to Caledonian Forest rather than Moor.</summary>
        public float ForestCover = 0.05f;

        // Glens: glacial troughs carved into the base height field, so biome assignment follows them.
        public float GlenStrength = 1f;
        /// <summary>Azimuth of the structural grain, degrees clockwise from north. The Great Glen runs ~040.</summary>
        public float GrainAzimuth = 40f;
        public float GlenSpacing = 1200f;
        public float GlenLength = 3000f;
        public float GlenHalfWidth = 220f;
        public float TributarySpacing = 3000f;
        public float TributaryHalfWidth = 110f;
        /// <summary>Glen floors are carved to a height between these (m above sea).</summary>
        public float GlenFloorMin = 12f;
        public float GlenFloorMax = 34f;
        /// <summary>Cross-profile exponent b in z ~ |x|^b. Measured glacial troughs mostly fall between 1.5 and 2.5.</summary>
        public float GlenProfileExponent = 2f;

        // Ribbon lochs: overdeepened stretches of glen floor that drop below sea level.
        public float LochFrequency = 0.3f;
        public float LochDepth = 4f;

        // Munros: rounded summits on the mountain massifs, with corries on the lee side.
        public float MassifLift = 1.0f;
        public float MunroSpacing = 480f;
        public float MunroHeight = 60f;
        public float SummitCapStart = 140f;
        public float SummitCapRange = 60f;
        /// <summary>Amplitude of small ridged relief on the hills (outcrops, broken ground). Vanilla's own jagged relief is discarded.</summary>
        public float CragAmplitude = 6f;
        public float CorrieChance = 0.55f;
        public float CorrieAzimuth = 45f;
        public float CorrieSpread = 35f;

        // Moorland (Meadows).
        public float MoorRollAmplitude = 2f;
        public float MoorRollScale = 320f;
        public float HummockAmplitude = 1.2f;
        public float HummockScale = 40f;
        public float LochanFrequency = 0.2f;
        public float LochanScale = 400f;
        public float LochanMaxHeight = 18f;
        public float LochanDepth = 2.5f;
        /// <summary>Big lochs placed on purpose on each island's moor (smaller islands get fewer). 0 turns them off.</summary>
        public int MoorLochsPerIsland = 2;
        /// <summary>Typical length of a moor loch (m); each varies from 75% to 125% of this, and is about a third as wide.</summary>
        public float MoorLochLength = 700f;
        /// <summary>Depth of a moor loch's middle (m); the margins are shallow.</summary>
        public float MoorLochDepth = 8f;

        // Drumlins (Black Forest).
        public float DrumlinAmplitude = 6f;
        public float DrumlinLength = 240f;
        public float DrumlinWidth = 85f;
        public float DrumlinCoverage = 0.35f;
    }
}
