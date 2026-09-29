// Offline preview of the Scotheim Highlands.
//
// Compiles the plugin's own Terrain/*.cs (no Unity, no Valheim) and runs it over a *synthetic*
// world: vanilla-like continents in an ocean, with Valheim's thresholds (Ocean below -26 m of base
// altitude, Mountain above 50 m). It runs the real site search, raises the landmass, assigns the
// three Highland biomes and imitates the game's 64 m corner-biome blending. It shows the shape of
// the transform, not any real seed.
//
// Build & run (Mono):
//   mcs -out:preview.exe tools/Preview/Preview.cs src/Scotheim/Terrain/*.cs
//   mono preview.exe out_dir [seed] [size_m] [px] [centre_x centre_y]
// With no centre given, the window is centred on the landmass.
// Then: python3 tools/Preview/render.py out_dir
using System;
using System.IO;
using Scotheim.Terrain;

static class Preview
{
    // Biome codes shared with render.py. Vanilla stand-ins, then the Highlands.
    const byte Ocean = 0, Meadows = 1, Forest = 8, Mountain = 4, Moor = 101, Caledonian = 108, Munros = 104;

    static int seed;
    static HighlandsTerrain hl;

    static float VanillaBase(float x, float y)
    {
        float n = 0.55f * Noise.Perlin(x / 2600f, y / 2600f, seed + 900)
                + 0.30f * Noise.Perlin(x / 1100f, y / 1100f, seed + 901)
                + 0.15f * Noise.Perlin(x / 450f, y / 450f, seed + 902);
        return -20f + 150f * n;
    }

    static float Base(float x, float y, bool mod)
    {
        float b = VanillaBase(x, y);
        return mod ? hl.CarveBase(x, y, hl.LandBase(x, y, b)) : b;
    }

    static byte BiomeAt(float x, float y, bool mod)
    {
        float b = Base(x, y, mod);
        if (mod)
        {
            switch (hl.Classify(x, y, b, VanillaBase(x, y)))
            {
                case HighlandBiome.Moor: return Moor;
                case HighlandBiome.Forest: return Caledonian;
                case HighlandBiome.Munros: return Munros;
            }
        }
        if (b <= HighlandsTerrain.OceanThreshold) return Ocean;
        if (b > HighlandsTerrain.MountainThreshold) return Mountain;
        return Noise.Perlin(x / 1500f, y / 1500f, seed + 950) > 0f ? Forest : Meadows;
    }

    // Rough stand-ins for vanilla's per-biome height functions.
    static float VanillaHeight(int terrain, float x, float y, float b)
    {
        switch (terrain)
        {
            case Mountain:
            {
                float ridge = 1f - Math.Abs(Noise.Perlin(x / 220f, y / 220f, seed + 960));
                float e = Math.Max(0f, b - 50f);
                return 50f + (b - 50f) * 2f + ridge * ridge * Math.Min(1f, e / 30f) * 45f;
            }
            case Forest:
                return b + 7f * Noise.Perlin(x / 180f, y / 180f, seed + 961);
            case Ocean:
                return b;
            default:
                return b + 3f * Noise.Perlin(x / 140f, y / 140f, seed + 962);
        }
    }

    static float Height(byte biome, float x, float y, bool mod)
    {
        float b = Base(x, y, mod);
        // Highland biomes use their "terrain" biome's vanilla function first, as with EWD.
        int terrain = biome == Moor ? Meadows : biome == Caledonian ? Forest : biome == Munros ? Mountain : biome;
        float h = VanillaHeight(terrain, x, y, b);
        if (!mod) return h;
        float preGlen = hl.LandBase(x, y, VanillaBase(x, y));
        switch (biome)
        {
            case Munros: return hl.ShapeMountain(x, y, preGlen);
            case Caledonian: return hl.ApplyLochs(x, y, hl.ShapeForest(x, y, h), preGlen);
            case Moor: return hl.ApplyLochs(x, y, hl.ShapeMoorland(x, y, h), preGlen);
            default: return h;
        }
    }

    static float BlendedHeight(float x, float y, bool mod, out byte biome)
    {
        const float zone = 64f;
        float x0 = (float)Math.Floor(x / zone) * zone, y0 = (float)Math.Floor(y / zone) * zone;
        float tx = (x - x0) / zone, ty = (y - y0) / zone;
        byte b00 = BiomeAt(x0, y0, mod), b10 = BiomeAt(x0 + zone, y0, mod);
        byte b01 = BiomeAt(x0, y0 + zone, mod), b11 = BiomeAt(x0 + zone, y0 + zone, mod);
        biome = BiomeAt(x, y, mod);
        if (b00 == b10 && b00 == b01 && b00 == b11) return Height(b00, x, y, mod);
        float h00 = Height(b00, x, y, mod), h10 = Height(b10, x, y, mod);
        float h01 = Height(b01, x, y, mod), h11 = Height(b11, x, y, mod);
        float a = h00 + (h10 - h00) * tx, c = h01 + (h11 - h01) * tx;
        return a + (c - a) * ty;
    }

    static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : "preview_out";
        seed = args.Length > 1 ? int.Parse(args[1]) : 12345;
        float size = args.Length > 2 ? float.Parse(args[2]) : 8000f;
        int px = args.Length > 3 ? int.Parse(args[3]) : 800;
        Directory.CreateDirectory(outDir);

        var settings = new HighlandsSettings();
        float sx, sy;
        float open = HighlandsTerrain.FindSite(settings, VanillaBase, out sx, out sy);
        hl = new HighlandsTerrain(settings, seed, sx, sy);
        Console.WriteLine(string.Format("site ({0:F0}, {1:F0}), {2:F0} m from centre, {3:P0} open ocean",
            sx, sy, Math.Sqrt(sx * sx + sy * sy), open));

        float cx = args.Length > 5 ? float.Parse(args[4]) : sx;
        float cy = args.Length > 5 ? float.Parse(args[5]) : sy;
        float ox = cx - size / 2f, oy = cy - size / 2f;
        foreach (bool mod in new[] { false, true })
        {
            string name = mod ? "after" : "before";
            using (var hw = new BinaryWriter(File.Create(Path.Combine(outDir, name + ".f32"))))
            using (var bw = new BinaryWriter(File.Create(Path.Combine(outDir, name + ".biome"))))
            {
                for (int j = px - 1; j >= 0; j--)
                {
                    for (int i = 0; i < px; i++)
                    {
                        float x = ox + (i + 0.5f) * size / px, y = oy + (j + 0.5f) * size / px;
                        byte biome;
                        hw.Write(BlendedHeight(x, y, mod, out biome));
                        bw.Write(biome);
                    }
                }
            }
        }
        File.WriteAllText(Path.Combine(outDir, "meta.txt"), px + " " + size + " " + ox + " " + oy + "\n");
        Console.WriteLine("wrote " + outDir);
    }
}
