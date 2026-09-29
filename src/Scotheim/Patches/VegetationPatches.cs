using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;

namespace Scotheim.Patches
{
    /// <summary>
    /// Scales vegetation density: fewer broadleaf trees on the moor, Scots pine over spruce in the
    /// forest. Only zones generated after this runs are affected; saved zones keep their trees.
    /// </summary>
    [HarmonyPatch(typeof(ZoneSystem), "Start")]
    static class VegetationPatches
    {
        struct Original { public float Min, Max; }

        // Keyed by entry, so re-entering a world re-applies from vanilla values instead of compounding.
        static readonly Dictionary<ZoneSystem.ZoneVegetation, Original> originals = new Dictionary<ZoneSystem.ZoneVegetation, Original>();

        static void Postfix(ZoneSystem __instance)
        {
            if (Plugin.Instance == null || !Plugin.Instance.VegetationEnabled.Value || __instance.m_vegetation == null) return;

            foreach (var rule in Parse(Plugin.Instance.VegetationRules.Value))
            {
                int matched = 0;
                foreach (var veg in __instance.m_vegetation)
                {
                    if (veg == null || veg.m_prefab == null || !veg.m_enable) continue;
                    if (!string.Equals(veg.m_prefab.name, rule.Prefab, StringComparison.OrdinalIgnoreCase)) continue;
                    if ((veg.m_biome & rule.Biome) == 0) continue;

                    Original o;
                    if (!originals.TryGetValue(veg, out o))
                    {
                        o = new Original { Min = veg.m_min, Max = veg.m_max };
                        originals[veg] = o;
                    }
                    veg.m_min = o.Min * rule.Multiplier;
                    veg.m_max = o.Max * rule.Multiplier;
                    matched++;
                }
                if (matched == 0)
                    Plugin.Log.LogWarning("Vegetation rule matched nothing: " + rule.Prefab + ":" + rule.Biome);
                else
                    Plugin.Log.LogInfo("Vegetation " + rule.Prefab + " in " + rule.Biome + " x" + rule.Multiplier + " (" + matched + " entries)");
            }
        }

        struct Rule { public string Prefab; public Heightmap.Biome Biome; public float Multiplier; }

        static IEnumerable<Rule> Parse(string text)
        {
            foreach (var part in (text ?? "").Split(','))
            {
                var bits = part.Trim().Split(':');
                if (bits.Length != 3) { if (part.Trim().Length > 0) Plugin.Log.LogWarning("Bad vegetation rule: " + part); continue; }
                Heightmap.Biome biome;
                float mult;
                try { biome = (Heightmap.Biome)Enum.Parse(typeof(Heightmap.Biome), bits[1].Trim(), true); }
                catch (ArgumentException) { Plugin.Log.LogWarning("Unknown biome in vegetation rule: " + part); continue; }
                if (!float.TryParse(bits[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out mult) || mult < 0f)
                { Plugin.Log.LogWarning("Bad multiplier in vegetation rule: " + part); continue; }
                yield return new Rule { Prefab = bits[0].Trim(), Biome = biome, Multiplier = mult };
            }
        }
    }
}
