using System;
using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Scotheim.Content
{
    /// <summary>
    /// The Highland midge (Culicoides impunctatus): it bites in still, damp air, mostly around dawn and dusk, and
    /// wind or smoke keep it off. Bog myrtle has long been rubbed on as a repellent (it's in commercial midge
    /// repellents today).
    ///
    /// On the moor and in the forest, when the wind is light and it's dawn or dusk, the local player gets Midges
    /// (slower stamina regen) unless they're by a fire (inside a heat area) or have used a bog myrtle salve.
    /// Game members are reached by name; a miss is logged and midges just don't bite.
    /// </summary>
    static class Midges
    {
        const float CalmWind = 0.35f;     // EnvMan wind intensity, 0-1
        static StatusEffect bitten, ward;
        static float next;
        static object heat;
        static System.Reflection.MethodInfo insideArea;

        internal static void Add(Dictionary<string, CustomItem> added)
        {
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = "Scot_Midges";
            se.m_name = "$se_scot_midges";
            se.m_tooltip = "$se_scot_midges_tooltip";
            se.m_ttl = 6f; // refreshed while they're biting
            var icon = PrefabManager.Cache.GetPrefab<StatusEffect>("Wet");
            if (icon != null) se.m_icon = icon.m_icon;
            se.m_staminaRegenMultiplier = 0.75f;
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, false));
            bitten = se;

            CustomItem salve, myrtle;
            if (!added.TryGetValue("Scot_MyrtleSalve", out salve) || !added.TryGetValue("Scot_BogMyrtle", out myrtle))
            {
                Plugin.Log.LogWarning("Bog myrtle salve skipped: its items weren't added.");
                return;
            }
            var protection = ScriptableObject.CreateInstance<SE_Stats>();
            protection.name = "Scot_MyrtleWard";
            protection.m_name = "$se_scot_myrtleward";
            protection.m_tooltip = "$se_scot_myrtleward_tooltip";
            protection.m_ttl = 1200f; // 20 minutes
            var icons = myrtle.ItemDrop.m_itemData.m_shared.m_icons;
            if (icons != null && icons.Length > 0) protection.m_icon = icons[0];
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(protection, false));
            GameFields.TrySetObject(salve.ItemDrop.m_itemData.m_shared, protection, "m_consumeStatusEffect");
            ward = protection;
        }

        /// <summary>Called every frame from the plugin.</summary>
        internal static void Tick()
        {
            if (bitten == null || Time.time < next) return;
            next = Time.time + 2f;
            var player = Player.m_localPlayer;
            if (player == null || !Biting(player.transform.position)) return;
            var seman = player.GetSEMan();
            if (ward != null && HasEffect(seman, ward.name)) return;
            GameFields.Call(seman, "AddStatusEffect", bitten, true);
        }

        static bool Biting(Vector3 position)
        {
            var biome = GameFields.Call(Instance("WorldGenerator"), "GetBiome", position);
            if (!(biome is Heightmap.Biome)) return false;
            bool custom;
            var highland = Patches.ExpandWorld.FromGame((Heightmap.Biome)biome, out custom);
            if (highland != Scotheim.Terrain.HighlandBiome.Moor && highland != Scotheim.Terrain.HighlandBiome.Forest) return false;

            var env = Instance("EnvMan");
            var wind = GameFields.Call(env, "GetWindIntensity") as float?;
            var day = GameFields.Call(env, "GetDayFraction") as float?;
            if (wind == null || day == null || wind.Value > CalmWind) return false;
            bool dawn = day.Value > 0.2f && day.Value < 0.33f, dusk = day.Value > 0.66f && day.Value < 0.8f;
            if (!dawn && !dusk) return false;
            return !NearFire(position);
        }

        // Smoke keeps them off: any fire's heat area counts (campfires, hearths, braziers).
        static bool NearFire(Vector3 position)
        {
            if (insideArea == null)
            {
                var areaType = Type.GetType("EffectArea, assembly_valheim");
                var typeEnum = areaType != null ? areaType.GetNestedType("Type") : null;
                if (typeEnum == null) return false;
                heat = Enum.Parse(typeEnum, "Heat");
                foreach (var method in areaType.GetMethods())
                    if (method.Name == "IsPointInsideArea" && method.GetParameters().Length == 3) insideArea = method;
                if (insideArea == null) return false;
            }
            var found = insideArea.Invoke(null, new object[] { position, heat, 0f });
            return found is bool ? (bool)found : found != null; // returns the area, or a bool in some versions
        }

        static bool HasEffect(object seman, string name)
        {
            var active = GameFields.Get(seman, "m_statusEffects") as System.Collections.IList;
            if (active != null)
                foreach (var effect in active)
                    if (effect is StatusEffect && ((StatusEffect)effect).name.StartsWith(name)) return true;
            return false;
        }

        static object Instance(string typeName)
        {
            var type = Type.GetType(typeName + ", assembly_valheim");
            if (type == null) return null;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;
            var property = type.GetProperty("instance", flags);
            if (property != null) return property.GetValue(null, null);
            var field = type.GetField("m_instance", flags) ?? type.GetField("instance", flags);
            return field != null ? field.GetValue(null) : null;
        }
    }
}
