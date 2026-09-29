using System;

namespace Scotheim.Terrain
{
    /// <summary>
    /// Seedable 2D gradient noise and integer hashing.
    ///
    /// We deliberately do not use UnityEngine.Mathf.PerlinNoise: it has no seed, and keeping this
    /// file free of Unity lets the offline preview tool run the exact same code the game runs.
    /// Everything here is pure and allocation-free, so it is safe to call from the worker threads
    /// Valheim uses for heightmap generation.
    /// </summary>
    public static class Noise
    {
        const int GradCount = 16;
        static readonly float[] GradX = new float[GradCount];
        static readonly float[] GradY = new float[GradCount];

        static Noise()
        {
            for (int i = 0; i < GradCount; i++)
            {
                double a = (i + 0.5) * 2.0 * Math.PI / GradCount;
                GradX[i] = (float)Math.Cos(a);
                GradY[i] = (float)Math.Sin(a);
            }
        }

        public static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)x * 0x85EBCA77u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)y * 0xC2B2AE3Du;
                h = (h << 17) | (h >> 15);
                h *= 0x27D4EB2Fu;
                h ^= h >> 15;
                h *= 0x85EBCA77u;
                h ^= h >> 13;
                h *= 0xC2B2AE3Du;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>Uniform value in [0, 1) for an integer lattice point.</summary>
        public static float Hash01(int x, int y, int seed)
        {
            return (Hash(x, y, seed) & 0xFFFFFF) / 16777216f;
        }

        /// <summary>Gradient noise, roughly in [-1, 1], zero at integer lattice points.</summary>
        public static float Perlin(float x, float y, int seed)
        {
            int x0 = FastFloor(x);
            int y0 = FastFloor(y);
            float fx = x - x0;
            float fy = y - y0;

            float n00 = Dot(Hash(x0, y0, seed), fx, fy);
            float n10 = Dot(Hash(x0 + 1, y0, seed), fx - 1f, fy);
            float n01 = Dot(Hash(x0, y0 + 1, seed), fx, fy - 1f);
            float n11 = Dot(Hash(x0 + 1, y0 + 1, seed), fx - 1f, fy - 1f);

            float u = Fade(fx);
            float v = Fade(fy);
            float a = n00 + (n10 - n00) * u;
            float b = n01 + (n11 - n01) * u;
            return (a + (b - a) * v) * 1.4142135f;
        }

        /// <summary>Two-octave gradient noise; slightly less blobby than a single octave.</summary>
        public static float Fbm2(float x, float y, int seed)
        {
            return (Perlin(x, y, seed) + 0.5f * Perlin(x * 2.03f + 17.1f, y * 2.03f - 9.7f, seed + 101)) / 1.5f;
        }

        public static int FastFloor(float f)
        {
            int i = (int)f;
            return f < i ? i - 1 : i;
        }

        static float Dot(uint h, float dx, float dy)
        {
            int g = (int)(h & (GradCount - 1));
            return GradX[g] * dx + GradY[g] * dy;
        }

        static float Fade(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }
    }
}
