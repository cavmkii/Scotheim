// Offline preview of the Scotheim terrain transform.
//
// Compiles the plugin's own Terrain/*.cs (no Unity, no Valheim) and runs it over a *synthetic*
// base height field that imitates Valheim's ranges: sea at 0, Mountain above 50 m of base altitude,
// vanilla-ish relief per biome. It also imitates the game's 64 m corner-biome blending so biome
// seams look roughly as they would in game. It shows the shape of the transform, not the shape of
// any real seed.
//
// Build & run (Mono):
//   mcs -out:preview.exe tools/Preview/Preview.cs src/Scotheim/Terrain/*.cs
//   mono preview.exe out_dir [seed] [size_m] [px] [centre_x] [centre_y]
// Then: python3 tools/Preview/render.py out_dir
using System;
using System.IO;
using Scotheim.Terrain;

static class Preview
{
    const int Meadows = 1, Forest = 8, Mountain = 4;

    static int seed;
    static HighlandsTerrain hl;

    static float SyntheticBase(float x, float y)
    {
        float n = 0.55f * Noise.Perlin(x / 2600f, y / 2600f, seed + 900)
                + 0.30f * Noise.Perlin(x / 1100f, y / 1100f, seed + 901)
                + 0.15f * Noise.Perlin(x / 450f, y / 450f, seed + 902);
        return 32f + 110f * n;
    }

    static float Base(float x, float y, bool carve)
    {
        float b = SyntheticBase(x, y);
        return carve ? hl.CarveBase(x, y, b) : b;
    }

    static int BiomeAt(float x, float y, bool carve)
    {
        float b = Base(x, y, carve);
        if (b > HighlandsTerrain.MountainThreshold) return Mountain;
        return Noise.Perlin(x / 1500f, y / 1500f, seed + 950) > 0f ? Forest : Meadows;
    }

    // Rough stand-ins for vanilla's per-biome height functions.
    static float VanillaHeight(int biome, float x, float y, float b)
    {
        switch (biome)
        {
            case Mountain:
            {
                float ridge = 1f - Math.Abs(Noise.Perlin(x / 220f, y / 220f, seed + 960));
                float e = Math.Max(0f, b - 50f);
                return 50f + (b - 50f) * 2f + ridge * ridge * Math.Min(1f, e / 30f) * 45f;
            }
            case Forest:
                return b + 7f * Noise.Perlin(x / 180f, y / 180f, seed + 961);
            default:
                return b + 3f * Noise.Perlin(x / 140f, y / 140f, seed + 962);
        }
    }

    static float Height(int biome, float x, float y, bool mod)
    {
        float b = Base(x, y, mod);
        float h = VanillaHeight(biome, x, y, b);
        if (!mod) return h;
        float raw = SyntheticBase(x, y);
        switch (biome)
        {
            case Mountain: h = hl.ShapeMountain(x, y, raw); break;
            case Forest: h = hl.ShapeForest(x, y, h); break;
            default: h = hl.ShapeMoorland(x, y, h); break;
        }
        return biome == Mountain ? h : hl.ApplyLochs(x, y, h, raw);
    }

    static float BlendedHeight(float x, float y, bool mod, out int biome)
    {
        const float zone = 64f;
        float x0 = (float)Math.Floor(x / zone) * zone, y0 = (float)Math.Floor(y / zone) * zone;
        float tx = (x - x0) / zone, ty = (y - y0) / zone;
        int b00 = BiomeAt(x0, y0, mod), b10 = BiomeAt(x0 + zone, y0, mod);
        int b01 = BiomeAt(x0, y0 + zone, mod), b11 = BiomeAt(x0 + zone, y0 + zone, mod);
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
        float size = args.Length > 2 ? float.Parse(args[2]) : 6000f;
        int px = args.Length > 3 ? int.Parse(args[3]) : 900;
        Directory.CreateDirectory(outDir);
        hl = new HighlandsTerrain(new HighlandsSettings(), seed);

        // Default centre sits well inside the glen radius, where the mod is active.
        float cx = args.Length > 4 ? float.Parse(args[4]) : 1500f;
        float cy = args.Length > 5 ? float.Parse(args[5]) : 1500f;
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
                        int biome;
                        hw.Write(BlendedHeight(x, y, mod, out biome));
                        bw.Write((byte)biome);
                    }
                }
            }
        }
        File.WriteAllText(Path.Combine(outDir, "meta.txt"), px + " " + size + " " + ox + " " + oy + "\n");
        Console.WriteLine("wrote " + outDir);
    }
}
