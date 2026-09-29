using System;

namespace Scotheim.Terrain
{
    /// <summary>
    /// The terrain transform, in two stages.
    ///
    /// Stage A (<see cref="CarveBase"/>) edits Valheim's base height field, which is what the game
    /// uses to decide biomes: above 50 m of base altitude is Mountain. Carving glens here means glen
    /// floors drop out of the Mountain biome and become Meadows or Black Forest (depending on the
    /// distance ring). That gives bare hills with wooded, habitable glens between them. Vanilla's
    /// own biome height functions all build on the base height, so they inherit the glens for free.
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

        public HighlandsTerrain(HighlandsSettings settings, int worldSeed)
        {
            s = settings;
            seed = worldSeed;
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

        // ---------------------------------------------------------------- glens

        /// <summary>
        /// Normalised distance from the nearest glen axis: 0 on the axis, 1 at the trough shoulder
        /// and beyond. <paramref name="floorNoise"/> varies slowly along the glen and sets floor
        /// height and where lochs form.
        /// </summary>
        public float GlenShape(float x, float y, out float floorNoise)
        {
            floorNoise = Noise.Fbm2(x / FloorScale, y / FloorScale, seed + 3);

            float wx = WarpAmount * Noise.Perlin(x / WarpScale, y / WarpScale, seed + 11);
            float wy = WarpAmount * Noise.Perlin(x / WarpScale + 31.7f, y / WarpScale - 12.3f, seed + 12);
            float px = x + wx;
            float py = y + wy;

            // Main glens follow the grain: stretch the noise along it so zero-contours run long and straight.
            float u = px * alongX + py * alongY;
            float v = px * acrossX + py * acrossY;
            float shape = 1f;
            if (s.GlenHalfWidth > 0f)
            {
                float d1 = DistanceToZero(u / s.GlenLength, v / s.GlenSpacing, 1f / s.GlenLength, 1f / s.GlenSpacing, seed + 1);
                shape = Math.Min(shape, d1 / s.GlenHalfWidth);
            }

            // Tributaries: isotropic and sparse, so the network isn't all parallel lines. Where two glens
            // meet, the min() leaves a crease, so keep these few (they also eat Mountain area fast).
            if (s.TributaryHalfWidth > 0f)
            {
                float t = 1f / s.TributarySpacing;
                float d2 = DistanceToZero(px * t, py * t, t, t, seed + 2);
                shape = Math.Min(shape, d2 / s.TributaryHalfWidth);
            }
            return shape;
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

        public float RadialFade(float x, float y)
        {
            float r = (float)Math.Sqrt(x * x + y * y);
            return 1f - SmoothStep(s.GlenMaxRadius - 500f, s.GlenMaxRadius, r);
        }

        /// <summary>Stage A: carve glacial troughs into base altitude. Floors stay above the swamp band.</summary>
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
            float strength = s.GlenStrength * RadialFade(x, y);
            if (strength <= 0f) return altitude;

            float floorNoise;
            float shape = GlenShape(x, y, out floorNoise);
            if (shape >= 1f) return altitude;

            float floor = GlenFloor(floorNoise);
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
        /// wall at about 1.5 * depth / half-width (roughly 35-40 degrees at default settings).
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
            float strength = s.GlenStrength * RadialFade(x, y);
            if (strength <= 0f) return altitude;

            float floorNoise;
            float shape = GlenShape(x, y, out floorNoise);
            if (shape >= 1f) return altitude;

            float floor = GlenFloor(floorNoise);
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
            float domeWeight = SmoothStep(0f, 50f, excess);

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
