using System;
using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Scotheim.Content
{
    /// <summary>
    /// Set bonuses for the four Highland armour sets. Each is a fresh SE_Stats (no inherited effects),
    /// borrowing only its icon from a vanilla set bonus. Four pieces make a set: head, chest, legs, cape.
    /// </summary>
    static class Sets
    {
        internal const int Size = 4;

        sealed class SetSpec
        {
            public string Id, Name, Tooltip, Icon;
            public Action<SE_Stats> Configure;
        }

        // Skill bonuses follow vanilla's +15 per set skill; regen and drain changes stay near vanilla sets too.
        static readonly SetSpec[] Specs =
        {
            new SetSpec
            {
                Id = "pictish", Name = "Woad", Icon = "SetEffect_TrollArmor",
                Tooltip = "Painted for the raid: quiet, quick and deadly with a spear.",
                Configure = se =>
                {
                    se.m_skillLevel = Skills.SkillType.Sneak; se.m_skillLevelModifier = 15f;
                    se.m_skillLevel2 = Skills.SkillType.Spears; se.m_skillLevelModifier2 = 15f;
                    se.m_runStaminaDrainModifier = -0.15f;
                },
            },
            new SetSpec
            {
                Id = "clansman", Name = "Freedom", Icon = "SetEffect_BerserkerArmor",
                Tooltip = "Light kit and a long blade. You can keep swinging.",
                Configure = se =>
                {
                    se.m_skillLevel = Skills.SkillType.Swords; se.m_skillLevelModifier = 15f;
                    se.m_skillLevel2 = Skills.SkillType.Axes; se.m_skillLevelModifier2 = 15f;
                    se.m_staminaRegenMultiplier = 1.25f;
                },
            },
            new SetSpec
            {
                Id = "manatarms", Name = "Schiltron", Icon = "SetEffect_DeepNorthMediumArmor",
                Tooltip = "Hold the line: better blocks and pole-arms, and arrows glance off.",
                Configure = se =>
                {
                    se.m_skillLevel = Skills.SkillType.Blocking; se.m_skillLevelModifier = 15f;
                    se.m_skillLevel2 = Skills.SkillType.Polearms; se.m_skillLevelModifier2 = 15f;
                    se.m_mods = new List<HitData.DamageModPair>
                    {
                        new HitData.DamageModPair { m_type = HitData.DamageType.Pierce, m_modifier = HitData.DamageModifier.Resistant },
                    };
                },
            },
            new SetSpec
            {
                Id = "sith", Name = "Glamour", Icon = "SetEffect_MageArmor",
                Tooltip = "The Sìth's glamour: eitr comes quicker, and eyes slide off you.",
                Configure = se =>
                {
                    se.m_skillLevel = Skills.SkillType.ElementalMagic; se.m_skillLevelModifier = 15f;
                    se.m_skillLevel2 = Skills.SkillType.Sneak; se.m_skillLevelModifier2 = 15f;
                    se.m_eitrRegenMultiplier = 1.5f;
                },
            },
        };

        internal static string NameToken(string id) => "$se_scot_" + id;
        internal static string TooltipToken(string id) => "$se_scot_" + id + "_tooltip";

        internal static IEnumerable<KeyValuePair<string, string>> English()
        {
            foreach (var spec in Specs)
            {
                yield return new KeyValuePair<string, string>(NameToken(spec.Id).Substring(1), spec.Name);
                yield return new KeyValuePair<string, string>(TooltipToken(spec.Id).Substring(1), spec.Tooltip);
            }
        }

        /// <summary>Registers the set bonuses and returns them by set id.</summary>
        internal static Dictionary<string, StatusEffect> AddBonuses()
        {
            var added = new Dictionary<string, StatusEffect>();
            foreach (var spec in Specs)
            {
                try
                {
                    var se = ScriptableObject.CreateInstance<SE_Stats>();
                    se.name = "SetEffect_Scot_" + spec.Id;
                    se.m_name = NameToken(spec.Id);
                    se.m_tooltip = TooltipToken(spec.Id);
                    var icon = PrefabManager.Cache.GetPrefab<StatusEffect>(spec.Icon);
                    if (icon != null) se.m_icon = icon.m_icon;
                    else Plugin.Log.LogWarning("Set bonus " + spec.Name + " has no icon: " + spec.Icon + " not found.");
                    spec.Configure(se);
                    ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, false));
                    added[spec.Id] = se;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("Couldn't add set bonus " + spec.Name + ": " + e);
                }
            }
            return added;
        }
    }
}
