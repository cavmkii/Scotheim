using System;
using System.Collections.Generic;

namespace Scotheim.Terrain
{
    /// <summary>Where one island goes: centre, size, ellipse orientation, and how open the water there was.</summary>
    public sealed class LandmassSite
    {
        public float X, Y;
        /// <summary>Size relative to LandmassLength/LandmassWidth.</summary>
        public float Scale = 1f;
        /// <summary>Direction of the ellipse's long axis, degrees clockwise from north.</summary>
        public float Azimuth;
        /// <summary>Fraction of the footprint that was open water in the vanilla world (1 = no vanilla land touched).</summary>
        public float OpenWater;
        internal float Radius, Turn;
    }

    public enum HighlandBiome { None, Moor, Forest, Munros }

    /// <summary>
    /// The Highlands: new islands raised out of open ocean, holding three biomes.
    ///
    /// Stage A (<see cref="LandBase"/>, <see cref="CarveBase"/>) edits Valheim's base height field:
    /// each island is blended in over the sea floor (where islands meet, the higher ground wins, so
    /// shelves merge smoothly), then glens are carved in. Nothing changes outside the islands, so
    /// vanilla land is untouched. <see cref="Classify"/> then assigns the
    /// biomes from that carved base: Munros above 50 m, Caledonian Forest in glens and patches on
    /// the lower ground, Moor elsewhere.
    ///
    /// Stage B (<see cref="ShapeMountain"/>, <see cref="ShapeMoorland"/>, <see cref="ShapeForest"/>,
    /// <see cref="ApplyLochs"/>) reshapes the final per-biome height: rounded munros with corries,
    /// rolling moor with lochans, drumlin fields, and ribbon lochs in overdeepened glen floors.
    ///
    /// All inputs and outputs are "altitude": metres above the water plane. Pure and thread-safe.
    /// </summary>
    public sealed class HighlandsTerrain
    {
        // Measured quantiles of the noise functions at p = 0, 0.05, ..., 1 (400k samples).
        static readonly float[] PerlinQuantiles =
        {
            -0.979f, -0.511f, -0.404f, -0.327f, -0.265f, -0.216f, -0.172f, -0.129f, -0.086f, -0.042f, 0.001f,
            0.043f, 0.086f, 0.129f, 0.172f, 0.216f, 0.265f, 0.327f, 0.405f, 0.511f, 0.976f,
        };
        static readonly float[] Fbm2Quantiles =
        {
            -0.814f, -0.375f, -0.298f, -0.243f, -0.198f, -0.160f, -0.124f, -0.092f, -0.060f, -0.030f, 0.000f,
            0.030f, 0.061f, 0.092f, 0.124f, 0.160f, 0.198f, 0.243f, 0.298f, 0.376f, 0.887f,
        };

        /// <summary>Base altitude above which vanilla assigns Mountain.</summary>
        public const float MountainThreshold = 50f;

        const float FloorScale = 1100f;
        const float WarpScale = 900f;
        const float WarpAmount = 170f;

        readonly HighlandsSettings s;
        readonly int seed;

        // Grain direction (along) and its perpendicular (across), as (east, north) unit vectors.
        readonly float alongX, alongY, acrossX, acrossY;
        readonly float corrieDirBase;
        readonly float lochThreshold, lochanThreshold, drumlinThreshold;

        /// <summary>Base altitude below which vanilla assigns Ocean.</summary>
        public const float OceanThreshold = -26f;

        /// <summary>Base altitude of the island's lowland shelf, where the moor is.</summary>
        const float LowlandHeight = 14f;

        /// <summary>
        /// Vanilla ground above this (its land and the water just off its beaches) is never raised
        /// or reassigned; the landmass only rises out of water deeper than this.
        /// </summary>
        public const float VanillaShore = -2f;

        /// <summary>Vanilla ground below this counts as open water when scoring sites.</summary>
        const float OpenWater = -8f;

        /// <summary>One island: its centre, size and the axes of its ellipse.</summary>
        sealed class Island
        {
            public float X, Y, Length, Width;
            // The island's own axes. Usually the grain, but the site search may turn an island a little
            // to fit open water; glens, ridges and drumlins always follow the grain.
            public float AlongX, AlongY, AcrossX, AcrossY;
            // Beyond this ellipse distance the island can't reach, whatever the coast noise does.
            public float Reach;
        }

        readonly Island[] islands;
        readonly float forestThreshold;

        public HighlandsTerrain(HighlandsSettings settings, int worldSeed, IList<LandmassSite> sites)
        {
            s = settings;
            seed = worldSeed;
            islands = new Island[sites.Count];
            for (int i = 0; i < sites.Count; i++)
            {
                double la = sites[i].Azimuth * Math.PI / 180.0;
                float ax = (float)Math.Sin(la), ay = (float)Math.Cos(la);
                islands[i] = new Island
                {
                    X = sites[i].X, Y = sites[i].Y,
                    Length = settings.LandmassLength * sites[i].Scale,
                    Width = settings.LandmassWidth * sites[i].Scale,
                    AlongX = ax, AlongY = ay, AcrossX = ay, AcrossY = -ax,
                    Reach = 1.6f + 1.4f * settings.CoastRoughness,
                };
            }
            forestThreshold = Quantile(Fbm2Quantiles, 1f - settings.ForestCover);
            double g = settings.GrainAzimuth * Math.PI / 180.0;
            alongX = (float)Math.Sin(g);
            alongY = (float)Math.Cos(g);
            acrossX = alongY;
            acrossY = -alongX;
            corrieDirBase = (float)(settings.CorrieAzimuth * Math.PI / 180.0);
            lochThreshold = Quantile(Fbm2Quantiles, settings.LochFrequency);
            lochanThreshold = Quantile(Fbm2Quantiles, settings.LochanFrequency);
            drumlinThreshold = Quantile(PerlinQuantiles, 1f - settings.DrumlinCoverage);
        }

        public HighlandsSettings Settings { get { return s; } }

        // ---------------------------------------------------------------- landmass

        /// <summary>
        /// Distance from an island's centre in ellipse units (1 = the nominal coast), elongated roughly
        /// along the grain like the real Highlands. No noise; cheap enough for early-outs.
        /// </summary>
        static float EllipseDistance(Island island, float x, float y)
        {
            float dx = x - island.X, dy = y - island.Y;
            float u = (dx * island.AlongX + dy * island.AlongY) / island.Length;
            float v = (dx * island.AcrossX + dy * island.AcrossY) / island.Width;
            return (float)Math.Sqrt(u * u + v * v);
        }

        /// <summary>Ellipse distance with a ragged coast: headlands, bays and offshore islets.</summary>
        float CoastDistance(float x, float y, float plain)
        {
            return plain
                + s.CoastRoughness * Noise.Fbm2(x / 1100f, y / 1100f, seed + 61)
                + 0.5f * s.CoastRoughness * Noise.Perlin(x / 300f, y / 300f, seed + 62);
        }

        /// <summary>
        /// The island a point belongs to (the one whose coast it is deepest inside), and its coast
        /// distance there. Null when no island reaches the point.
        /// </summary>
        Island Dominant(float x, float y, out float coastDistance)
        {
            Island best = null;
            coastDistance = float.MaxValue;
            foreach (var island in islands)
            {
                float plain = EllipseDistance(island, x, y);
                if (plain > island.Reach) continue;
                float d = CoastDistance(x, y, plain);
                if (d < coastDistance) { coastDistance = d; best = island; }
            }
            return best;
        }

        /// <summary>
        /// Glen floor height at a point: the along-glen floor noise, dropping below sea level near
        /// the coast so glens that reach the shore flood as sea lochs.
        /// </summary>
        float EffectiveFloor(float x, float y, float floorNoise)
        {
            float floor = GlenFloor(floorNoise);
            float d;
            if (Dominant(x, y, out d) == null) return floor;
            return Lerp(floor, -s.LochDepth - 3f, SmoothStep(0.5f, 0.95f, d));
        }

        /// <summary>1 on an island and its shelf, fading to 0 in open ocean. Stage B only runs where this is above 0.5.</summary>
        public float LandWeight(float x, float y)
        {
            float d;
            if (Dominant(x, y, out d) == null) return 0f;
            return SmoothStep(1.6f, 1.25f, d);
        }

        /// <summary>
        /// Stage A, first half: raise the islands. Takes the vanilla base altitude and returns the
        /// pre-glen altitude. Only rises out of open water: where vanilla already has land or
        /// shallows, it is left alone, so the Highlands add to the world without replacing anything.
        /// Where two islands' shelves meet, the higher ground wins, which keeps heights continuous.
        /// </summary>
        public float LandBase(float x, float y, float vanillaAltitude)
        {
            float result = vanillaAltitude;
            // Full lift below ~-14 m. Real Valheim sea floor near land sits around -20 to -35 m (measured
            // in game), so a deeper cut-off would leave an island half-risen in ordinary open water.
            float depthWeight = SmoothStep(VanillaShore, VanillaShore - 12f, vanillaAltitude);
            if (depthWeight <= 0f) return result;
            foreach (var island in islands)
            {
                float plain = EllipseDistance(island, x, y);
                if (plain > island.Reach) continue;
                float d = CoastDistance(x, y, plain);
                float weight = SmoothStep(1.6f, 1.25f, d) * depthWeight;
                if (weight <= 0f) continue;

                // Long, gentle coastal ramp up to a flat lowland shelf (~14 m) covering most of the
                // interior, with separate NE-SW hill massifs standing out of it, kept off the coast.
                float shelf = SmoothStep(1.2f, 0.5f, d);
                float lowland = -34f + (LowlandHeight + 34f) * shelf;
                float height = lowland + (s.LandmassCoreHeight - LowlandHeight) * MassifWeight(x, y, d);
                result = Math.Max(result, Lerp(vanillaAltitude, height, weight));
            }
            return result;
        }

        /// <summary>0 on the lowland shelf, rising to 1 on the body of a hill massif.</summary>
        float MassifWeight(float x, float y, float coastDistance)
        {
            float profile = SmoothStep(1.1f, 0.4f, coastDistance);
            if (profile <= 0f) return 0f;
            float u = x * alongX + y * alongY, v = x * acrossX + y * acrossY;
            float ridges = Noise.Fbm2(u / 1400f, v / 650f, seed + 63);
            return SmoothStep(s.MassifThreshold - 0.2f, s.MassifThreshold + 0.25f, ridges) * profile;
        }

        /// <summary>
        /// Which Highland biome a point belongs to, from its carved base altitude. None off the
        /// islands, under the sea, or where vanilla already had land (that keeps its vanilla biome).
        /// </summary>
        public HighlandBiome Classify(float x, float y, float carvedBaseAltitude, float vanillaAltitude)
        {
            if (carvedBaseAltitude <= OceanThreshold || vanillaAltitude > VanillaShore) return HighlandBiome.None;
            float d;
            if (Dominant(x, y, out d) == null || SmoothStep(1.6f, 1.25f, d) <= 0.5f) return HighlandBiome.None;
            if (carvedBaseAltitude > s.MunroMinHeight) return HighlandBiome.Munros;

            // Moor is the open lowland shelf. Caledonian pinewood clothes the lower hill slopes below
            // the Munros, fills the glens, and survives in a few patches out on the moor.
            float hills = MassifWeight(x, y, d);
            if (hills > 0.2f) return HighlandBiome.Forest;
            // Glens only count as sheltered where they cut through hills: out on the flat moor a
            // glen line is barely carved and shouldn't grow a band of forest.
            float floorNoise;
            float glen = GlenShape(x, y, out floorNoise);
            float patch = Noise.Fbm2(x / 650f, y / 650f, seed + 71);
            float shelter = (1f - glen) * 0.25f * SmoothStep(0.02f, 0.12f, hills);
            return patch + shelter > forestThreshold ? HighlandBiome.Forest : HighlandBiome.Moor;
        }

        /// <summary>Size of the k-th island (0 = largest), shrinking evenly to LandmassMinScale.</summary>
        public static float IslandScale(HighlandsSettings s, int k)
        {
            if (s.LandmassCount <= 1) return 1f;
            return 1f - (1f - s.LandmassMinScale) * k / (s.LandmassCount - 1f);
        }

        /// <summary>
        /// Picks island sites in open water, largest first. Candidates lie on rings in the configured
        /// distance band, turned up to 30 degrees either way from the grain; each is scored by the
        /// fraction of its footprint that is open water rather than vanilla land or beach. Each island
        /// takes the best site that keeps clear of the ones already placed. Near-ties go to the
        /// orientation closest to the grain, then the preferred radius.
        /// Deterministic for a given seed and settings, so every peer finds the same sites.
        /// May return fewer than LandmassCount if the band runs out of room.
        /// </summary>
        public static List<LandmassSite> FindSites(HighlandsSettings s, Func<float, float, float> vanillaAltitude)
        {
            var chosen = new List<LandmassSite>();
            if (!s.LandmassAuto)
            {
                var manual = new LandmassSite { X = s.LandmassX, Y = s.LandmassY, Azimuth = s.GrainAzimuth };
                manual.OpenWater = Footprint(s, vanillaAltitude, manual, 14);
                chosen.Add(manual);
                return chosen;
            }

            // Coarse pass over every candidate at full size (conservative for smaller islands).
            var candidates = new List<LandmassSite>();
            float margin = 1.3f * s.LandmassLength;
            for (float r = s.LandmassMinRadius; r <= s.LandmassMaxRadius + 1f; r += 500f)
            {
                if (r + margin > 9700f) continue;
                for (int a = 0; a < 72; a++)
                {
                    double angle = a * 5.0 * Math.PI / 180.0;
                    float cx = (float)(Math.Sin(angle) * r), cy = (float)(Math.Cos(angle) * r);
                    // Keep out of Ashlands (south) and Deep North (north), which vanilla places beyond
                    // 12 km from points 4 km north and south of centre.
                    if (Dist(cx, cy, 0f, 4000f) + margin > 12000f) continue;
                    if (Dist(cx, cy, 0f, -4000f) + margin > 12000f) continue;
                    foreach (float turn in new[] { 0f, -15f, 15f, -30f, 30f })
                    {
                        var c = new LandmassSite { X = cx, Y = cy, Azimuth = s.GrainAzimuth + turn, Radius = r, Turn = Math.Abs(turn) };
                        c.OpenWater = Footprint(s, vanillaAltitude, c, 6);
                        candidates.Add(c);
                    }
                }
            }
            candidates.Sort((p, q) => q.OpenWater.CompareTo(p.OpenWater));

            for (int k = 0; k < s.LandmassCount; k++)
            {
                float scale = IslandScale(s, k);
                LandmassSite best = null;
                int considered = 0;
                foreach (var c in candidates)
                {
                    if (considered >= 20) break;
                    if (Crowds(s, c, scale, chosen)) continue;
                    considered++;
                    var sized = new LandmassSite { X = c.X, Y = c.Y, Azimuth = c.Azimuth, Radius = c.Radius, Turn = c.Turn, Scale = scale };
                    sized.OpenWater = Footprint(s, vanillaAltitude, sized, 14);
                    if (best == null || Better(sized, best, s)) best = sized;
                }
                if (best == null) break;
                chosen.Add(best);
            }
            return chosen;
        }

        /// <summary>True if an island of this size at this candidate would sit too close to one already placed.</summary>
        static bool Crowds(HighlandsSettings s, LandmassSite candidate, float scale, List<LandmassSite> chosen)
        {
            foreach (var other in chosen)
            {
                // Long axes plus a channel: footprints (1.2x the coast ellipse) never overlap.
                float gap = 1.25f * s.LandmassLength * (scale + other.Scale);
                if (Dist(candidate.X, candidate.Y, other.X, other.Y) < gap) return true;
            }
            return false;
        }

        static bool Better(LandmassSite a, LandmassSite b, HighlandsSettings s)
        {
            // Within 1% open water counts as a tie: prefer following the grain, then the preferred radius.
            if (Math.Abs(a.OpenWater - b.OpenWater) > 0.01f) return a.OpenWater > b.OpenWater;
            if (a.Turn != b.Turn) return a.Turn < b.Turn;
            return Math.Abs(a.Radius - s.LandmassPreferredRadius) < Math.Abs(b.Radius - s.LandmassPreferredRadius);
        }

        /// <summary>
        /// Vanilla base altitudes sampled over a site's footprint (as FindSite scores it), sorted.
        /// For diagnostics: how deep the real ocean is where the landmass goes.
        /// </summary>
        public static float[] FootprintAltitudes(HighlandsSettings s, Func<float, float, float> vanillaAltitude, LandmassSite site)
        {
            var list = new List<float>();
            SampleFootprint(s, site, 14, (x, y) => list.Add(vanillaAltitude(x, y)));
            list.Sort();
            return list.ToArray();
        }

        /// <summary>Visits a grid over the island's extent: the nominal coast ellipse grown by 20%.</summary>
        static void SampleFootprint(HighlandsSettings s, LandmassSite site, int n, Action<float, float> visit)
        {
            double g = site.Azimuth * Math.PI / 180.0;
            float ax = (float)Math.Sin(g), ay = (float)Math.Cos(g);
            for (int i = -n; i <= n; i++)
            {
                for (int j = -n; j <= n; j++)
                {
                    float u = i / (float)n, v = j / (float)n;
                    if (u * u + v * v > 1f) continue;
                    float along = u * 1.2f * s.LandmassLength * site.Scale, across = v * 1.2f * s.LandmassWidth * site.Scale;
                    visit(site.X + along * ax + across * ay, site.Y + along * ay - across * ax);
                }
            }
        }

        static float Footprint(HighlandsSettings s, Func<float, float, float> vanillaAltitude, LandmassSite site, int n)
        {
            // Fraction of the footprint that is clearly open water rather than vanilla land, beach or
            // shallows. Real Valheim ocean is shallow (median around -23 m near land), so a "deep water"
            // test can't tell sites apart; overlap with vanilla land is what breaks the island up.
            int water = 0, total = 0;
            SampleFootprint(s, site, n, (x, y) =>
            {
                total++;
                if (vanillaAltitude(x, y) < OpenWater) water++;
            });
            return total == 0 ? 0f : water / (float)total;
        }

        static float Dist(float ax, float ay, float bx, float by)
        {
            float dx = ax - bx, dy = ay - by;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        // ---------------------------------------------------------------- glens

        /// <summary>
        /// Normalised distance from the nearest glen axis: 0 on the axis, 1 at the trough shoulder
        /// and beyond. <paramref name="floorNoise"/> varies slowly along the glen and sets floor
        /// height and where lochs form.
        /// </summary>
        public float GlenShape(float x, float y, out float floorNoise)
        {
            float main, side;
            GlenDistances(x, y, out floorNoise, out main, out side);
            return Math.Min(1f, Math.Min(main / s.GlenHalfWidth, side / s.TributaryHalfWidth));
        }

        /// <summary>
        /// Distances (m) to the nearest main-glen and side-glen axes; float.MaxValue for a disabled set.
        /// </summary>
        void GlenDistances(float x, float y, out float floorNoise, out float main, out float side)
        {
            floorNoise = Noise.Fbm2(x / FloorScale, y / FloorScale, seed + 3);
            main = side = float.MaxValue;

            float wx = WarpAmount * Noise.Perlin(x / WarpScale, y / WarpScale, seed + 11);
            float wy = WarpAmount * Noise.Perlin(x / WarpScale + 31.7f, y / WarpScale - 12.3f, seed + 12);
            float px = x + wx;
            float py = y + wy;

            // Main glens follow the grain: stretch the noise along it so zero-contours run long and straight.
            if (s.GlenHalfWidth > 0f)
            {
                float u = px * alongX + py * alongY;
                float v = px * acrossX + py * acrossY;
                main = DistanceToZero(u / s.GlenLength, v / s.GlenSpacing, 1f / s.GlenLength, 1f / s.GlenSpacing, seed + 1);
            }

            // Tributaries: isotropic and sparse, so the network isn't all parallel lines. Where two glens
            // meet, the min() leaves a crease, so keep these few.
            if (s.TributaryHalfWidth > 0f)
            {
                float t = 1f / s.TributarySpacing;
                side = DistanceToZero(px * t, py * t, t, t, seed + 2);
            }
        }

        // Steepest glen wall we aim for: tan(35 degrees).
        const float MaxWallSlope = 0.70f;

        /// <summary>
        /// Glen shape (0 on the axis, 1 at the shoulder) for a trough <paramref name="depth"/> metres
        /// deep. The profile's steepest wall is about 1.5 * depth / width, and the effective width comes
        /// out ~0.7x nominal, so a fixed width makes deep glens cliff-sided. Each glen is widened until
        /// that stays under MaxWallSlope (deeper ice cut wider troughs too). Widening each set before
        /// taking the nearer one keeps the result continuous where main and side glens meet.
        /// </summary>
        float TroughShape(float main, float side, float depth)
        {
            float minWidth = 1.5f * depth / (MaxWallSlope * 0.7f);
            float a = main == float.MaxValue ? float.MaxValue : main / Math.Max(s.GlenHalfWidth, minWidth);
            float b = side == float.MaxValue ? float.MaxValue : side / Math.Max(s.TributaryHalfWidth, minWidth);
            return Math.Min(a, b);
        }

        /// <summary>
        /// First-order distance (metres) from a point to the zero set of noise sampled at (a, b),
        /// where da/dworld and db/dworld are the scale factors along each noise axis.
        /// </summary>
        static float DistanceToZero(float a, float b, float scaleA, float scaleB, int noiseSeed)
        {
            const float e = 0.01f;
            float n = Noise.Perlin(a, b, noiseSeed);
            float ga = (Noise.Perlin(a + e, b, noiseSeed) - n) / e * scaleA;
            float gb = (Noise.Perlin(a, b + e, noiseSeed) - n) / e * scaleB;
            float grad = (float)Math.Sqrt(ga * ga + gb * gb);
            if (grad < 1e-7f) return float.MaxValue;
            return Math.Abs(n) / grad;
        }

        /// <summary>Glen floor altitude for a given floor-noise value.</summary>
        public float GlenFloor(float floorNoise)
        {
            return Lerp(s.GlenFloorMin, s.GlenFloorMax, SmoothStep(-0.35f, 0.35f, floorNoise));
        }

        /// <summary>Stage A, second half: carve glacial troughs into the raised landmass.</summary>
        public float CarveBase(float x, float y, float altitude)
        {
            return Carve(x, y, altitude, altitude, false);
        }

        /// <summary>
        /// Carve glacial troughs into an arbitrary surface. The floor is the glen floor, or the loch bed
        /// in loch stretches when <paramref name="withLochs"/> is set. <paramref name="rawBaseAltitude"/>
        /// decides how deeply the glen cuts here: glens cut into uplands but don't trench low ground they
        /// merely cross, and lochs only form where the glen cut deep.
        /// </summary>
        float Carve(float x, float y, float altitude, float rawBaseAltitude, bool withLochs)
        {
            if (s.GlenStrength <= 0f) return altitude;
            float strength = s.GlenStrength * LandWeight(x, y);
            if (strength <= 0f) return altitude;

            float floorNoise, main, side;
            GlenDistances(x, y, out floorNoise, out main, out side);
            float floor = EffectiveFloor(x, y, floorNoise);
            float shape = TroughShape(main, side, Math.Max(altitude, rawBaseAltitude) - floor);
            if (shape >= 1f) return altitude;
            float relief = SmoothStep(floor + 4f, floor + 24f, rawBaseAltitude);
            if (withLochs)
                floor = Lerp(floor, -s.LochDepth, LochMask(floor, floorNoise, rawBaseAltitude));
            if (altitude <= floor || relief <= 0f) return altitude;

            float carved = altitude - (altitude - floor) * CarveProfile(shape);
            return Lerp(altitude, carved, strength * relief);
        }

        float LochMask(float floor, float floorNoise, float rawBaseAltitude)
        {
            if (s.LochFrequency <= 0f) return 0f;
            return SmoothStep(floor + 10f, floor + 30f, rawBaseAltitude)
                 * SmoothStep(lochThreshold + 0.06f, lochThreshold - 0.06f, floorNoise);
        }

        /// <summary>
        /// Fraction of the way from the original surface down to the floor: 1 on the axis, 0 past the
        /// shoulder. Power-law floor (z ~ |x|^b near the axis); the outer power flattens the slope to
        /// zero at the shoulder, so it doesn't read as a crease, while keeping the steepest part of the
        /// wall at about 1.5 * depth / half-width, which <see cref="TroughShape"/> keeps near 35 degrees.
        /// </summary>
        float CarveProfile(float shape)
        {
            float p = 1f - (float)Math.Pow(shape, s.GlenProfileExponent);
            return p * (float)Math.Sqrt(p);
        }

        /// <summary>
        /// Stage B, Meadows and Black Forest: ribbon lochs. Stage A kept glen floors above the swamp
        /// band; here, in loch stretches, lower the floor to the loch bed. For a surface already carved
        /// to the glen floor, carving to the bed instead differs by exactly (floor - bed) * profile, so
        /// subtracting that gives the same wall as a single carve rather than compounding two.
        /// </summary>
        public float ApplyLochs(float x, float y, float altitude, float rawBaseAltitude)
        {
            if (s.GlenStrength <= 0f || s.LochFrequency <= 0f) return altitude;
            float strength = s.GlenStrength * LandWeight(x, y);
            if (strength <= 0f) return altitude;

            float floorNoise, main, side;
            GlenDistances(x, y, out floorNoise, out main, out side);
            float floor = EffectiveFloor(x, y, floorNoise);
            // Same width as stage A used for this surface, so the two carves compose exactly.
            float shape = TroughShape(main, side, rawBaseAltitude - floor);
            if (shape >= 1f) return altitude;
            float mask = LochMask(floor, floorNoise, rawBaseAltitude);
            if (mask <= 0f) return altitude;
            float relief = SmoothStep(floor + 4f, floor + 24f, rawBaseAltitude);
            float bed = Lerp(floor, -s.LochDepth, mask);
            return altitude - strength * relief * (floor - bed) * CarveProfile(shape);
        }

        // ---------------------------------------------------------------- munros

        /// <summary>
        /// Stage B, Mountain biome. Builds the upland from the *uncarved* base (a lifted massif carrying
        /// rounded domes, corries cut into their lee flanks, soft-capped summits, a little rock), then
        /// carves the glens, lochs included, through that finished surface in a single pass.
        ///
        /// Building from the carved base instead would fade the domes out partway down each glen wall
        /// (their weight depends on height), stacking the dome relief onto the wall and producing
        /// 60-70 degree cliffs. Vanilla's mountain relief is discarded entirely.
        /// </summary>
        public float ShapeMountain(float x, float y, float rawBaseAltitude)
        {
            float excess = rawBaseAltitude - MountainThreshold;
            float massif = excess > 0f ? MountainThreshold + excess * s.MassifLift : rawBaseAltitude;
            // Domes only on the body of the massif; at biome edges, stay close to base height so the
            // game's corner blending has nothing abrupt to smooth over.
            float domeWeight = SmoothStep(0f, 70f, excess);

            float h = massif;
            if (domeWeight > 0f)
            {
                if (s.MunroHeight > 0f)
                    h += domeWeight * Munros(x, y);
                if (s.CragAmplitude > 0f)
                {
                    float ridge = 1f - Math.Abs(Noise.Perlin(x / 70f, y / 70f, seed + 31));
                    h += domeWeight * s.CragAmplitude * (ridge * ridge - 0.45f);
                }
            }

            if (h > s.SummitCapStart && s.SummitCapRange > 0f)
                h = s.SummitCapStart + s.SummitCapRange * (float)Math.Tanh((h - s.SummitCapStart) / s.SummitCapRange);

            return Carve(x, y, h, rawBaseAltitude, true);
        }

        float Munros(float x, float y)
        {
            float cell = s.MunroSpacing;
            int cx = Noise.FastFloor(x / cell);
            int cy = Noise.FastFloor(y / cell);
            const float elong = 1.3f;

            float domes = 0f;
            float corrieCut = float.MaxValue;
            for (int j = -1; j <= 1; j++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    int gx = cx + i, gy = cy + j;
                    float sx = (gx + 0.2f + 0.6f * Noise.Hash01(gx, gy, seed + 21)) * cell;
                    float sy = (gy + 0.2f + 0.6f * Noise.Hash01(gx, gy, seed + 22)) * cell;
                    float radius = cell * (0.5f + 0.2f * Noise.Hash01(gx, gy, seed + 23));
                    float height = s.MunroHeight * (0.55f + 0.45f * Noise.Hash01(gx, gy, seed + 24));

                    float dx = x - sx, dy = y - sy;
                    // Slightly elongated along the grain, like the ridges of the real Highlands.
                    float du = (dx * alongX + dy * alongY) / (radius * elong);
                    float dv = (dx * acrossX + dy * acrossY) / (radius / elong);
                    float q2 = du * du + dv * dv;
                    if (q2 < 1f)
                    {
                        float k = 1f - q2;
                        domes += height * k * k;
                    }

                    // Corries face NE in Scotland (shaded from sun, lee of the prevailing SW wind,
                    // so snow collected there). Place a bowl on that flank and cut the terrain down
                    // to it. The bowl surface rises steeply outward, so it bites into the upslope side
                    // (a headwall) and leaves the downslope side open (the lip).
                    if (Noise.Hash01(gx, gy, seed + 25) < s.CorrieChance)
                    {
                        float a = corrieDirBase + (Noise.Hash01(gx, gy, seed + 26) - 0.5f) * 2f * s.CorrieSpread * (float)(Math.PI / 180.0);
                        float offset = radius * 0.42f;
                        float ccx = sx + (float)Math.Sin(a) * offset;
                        float ccy = sy + (float)Math.Cos(a) * offset;
                        float bowlRadius = radius * 0.42f;
                        float dirX = (float)Math.Sin(a), dirY = (float)Math.Cos(a);
                        float rx = (x - ccx) / bowlRadius, ry = (y - ccy) / bowlRadius;
                        // Split into outward (downslope) and sideways components. Stretch and tilt the
                        // floor outward so the bowl opens onto the hillside instead of forming a crater
                        // where neighbouring domes overlap.
                        float rOut = rx * dirX + ry * dirY;
                        float rSide = rx * dirY - ry * dirX;
                        float outward = rOut > 0f ? rOut : 0f;
                        float rOutScaled = rOut > 0f ? rOut / 2.2f : rOut;
                        float r2 = rOutScaled * rOutScaled + rSide * rSide;
                        if (r2 < 4f)
                        {
                            float bowl = height * (0.30f - 0.12f * Math.Min(outward, 2.5f) + 1.4f * r2);
                            if (bowl < corrieCut) corrieCut = bowl;
                        }
                    }
                }
            }

            if (corrieCut < float.MaxValue)
                domes = SmoothMin(domes, corrieCut, 4f);
            return domes;
        }

        // ---------------------------------------------------------------- lowlands

        /// <summary>Stage B, Meadows: rolling moorland with hummocks and the odd lochan.</summary>
        public float ShapeMoorland(float x, float y, float altitude)
        {
            // Leave beaches and the seabed alone.
            float land = SmoothStep(-2f, 4f, altitude);
            float h = altitude + land * (
                s.MoorRollAmplitude * Noise.Fbm2(x / s.MoorRollScale, y / s.MoorRollScale, seed + 41) +
                s.HummockAmplitude * Noise.Perlin(x / s.HummockScale, y / s.HummockScale, seed + 42));

            if (s.LochanFrequency > 0f)
            {
                float n = Noise.Fbm2(x / s.LochanScale, y / s.LochanScale, seed + 43);
                float lochan = SmoothStep(lochanThreshold + 0.05f, lochanThreshold - 0.05f, n);
                // Valheim has one water plane, so lochans can only exist on low ground.
                lochan *= SmoothStep(s.LochanMaxHeight, s.LochanMaxHeight - 5f, h);
                if (lochan > 0f && h > -s.LochanDepth)
                    h = Lerp(h, -s.LochanDepth, lochan);
            }
            return h;
        }

        /// <summary>Stage B, Black Forest: drumlin swarms aligned with the grain, plus hummocky moraine.</summary>
        public float ShapeForest(float x, float y, float altitude)
        {
            float land = SmoothStep(-2f, 4f, altitude);
            float u = x * alongX + y * alongY;
            float v = x * acrossX + y * acrossY;
            float n = Noise.Perlin(u / s.DrumlinLength, v / s.DrumlinWidth, seed + 51);
            float d = n > drumlinThreshold ? (n - drumlinThreshold) / (1f - drumlinThreshold) : 0f;
            d = d > 1f ? 1f : d;
            d = d * d * (3f - 2f * d);
            float hummocks = s.HummockAmplitude * Noise.Perlin(x / s.HummockScale, y / s.HummockScale, seed + 52);
            return altitude + land * (s.DrumlinAmplitude * d + hummocks);
        }

        // ---------------------------------------------------------------- helpers

        static float Quantile(float[] table, float p)
        {
            if (p <= 0f) return table[0] - 1f;
            if (p >= 1f) return table[table.Length - 1] + 1f;
            float f = p * (table.Length - 1);
            int i = (int)f;
            return Lerp(table[i], table[i + 1], f - i);
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        /// <summary>Hermite smoothstep; edge0 may exceed edge1 to get a falling edge.</summary>
        public static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = (x - edge0) / (edge1 - edge0);
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }

        static float SmoothMin(float a, float b, float k)
        {
            float h = 0.5f + 0.5f * (b - a) / k;
            if (h <= 0f) return b;
            if (h >= 1f) return a;
            return Lerp(b, a, h) - k * h * (1f - h);
        }
    }
}
