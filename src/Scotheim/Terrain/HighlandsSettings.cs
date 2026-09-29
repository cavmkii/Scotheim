namespace Scotheim.Terrain
{
    /// <summary>
    /// All tunables for the terrain transform. Heights are metres relative to Valheim's sea level
    /// (the game's water plane sits at y = 30); distances are metres in world space.
    ///
    /// An instance is treated as immutable once a world starts generating: every peer must derive
    /// identical terrain, so the plugin snapshots config into a new instance at world init rather
    /// than letting live config edits leak into half-generated terrain.
    /// </summary>
    public sealed class HighlandsSettings
    {
        // Glens: glacial troughs carved into the base height field, so biome assignment follows them.
        public float GlenStrength = 1f;
        /// <summary>Azimuth of the structural grain, degrees clockwise from north. The Great Glen runs ~040.</summary>
        public float GrainAzimuth = 40f;
        public float GlenSpacing = 1800f;
        public float GlenLength = 3000f;
        public float GlenHalfWidth = 240f;
        public float TributarySpacing = 5000f;
        public float TributaryHalfWidth = 110f;
        /// <summary>Glen floors are carved to a height between these. Keep FloorMin above 20 so floors never fall into the swamp band.</summary>
        public float GlenFloorMin = 22f;
        public float GlenFloorMax = 40f;
        /// <summary>Cross-profile exponent b in z ~ |x|^b. Measured glacial troughs mostly fall between 1.5 and 2.5.</summary>
        public float GlenProfileExponent = 2f;
        /// <summary>Glens fade out between (MaxRadius - 500) and MaxRadius from the world centre.</summary>
        public float GlenMaxRadius = 7000f;

        // Ribbon lochs: overdeepened stretches of glen floor that drop below sea level.
        public float LochFrequency = 0.3f;
        public float LochDepth = 7f;

        // Munros: rounded summits on the mountain massifs, with corries on the lee side.
        public float MassifLift = 1.0f;
        public float MunroSpacing = 480f;
        public float MunroHeight = 80f;
        public float SummitCapStart = 140f;
        public float SummitCapRange = 60f;
        /// <summary>Amplitude of small ridged relief on the hills (outcrops, broken ground). Vanilla's own jagged relief is discarded.</summary>
        public float CragAmplitude = 6f;
        public float CorrieChance = 0.55f;
        public float CorrieAzimuth = 45f;
        public float CorrieSpread = 35f;

        // Moorland (Meadows).
        public float MoorRollAmplitude = 5f;
        public float MoorRollScale = 320f;
        public float HummockAmplitude = 1.2f;
        public float HummockScale = 40f;
        public float LochanFrequency = 0.12f;
        public float LochanScale = 240f;
        public float LochanMaxHeight = 8f;
        public float LochanDepth = 2.5f;

        // Drumlins (Black Forest).
        public float DrumlinAmplitude = 6f;
        public float DrumlinLength = 240f;
        public float DrumlinWidth = 85f;
        public float DrumlinCoverage = 0.35f;
    }
}
