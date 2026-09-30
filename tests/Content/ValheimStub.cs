// Stand-ins for the assembly_valheim members Content/ touches (built as an assembly named assembly_valheim).
// These names are NOT verified by this compile: they're written from knowledge of the game. Prefab names
// and components are checked separately by tests/Data/check_data.py.
public class Character : UnityEngine.MonoBehaviour { public string m_name; public float m_health; public enum Faction { Players } }
public class Humanoid : Character { }
public class ItemDrop : UnityEngine.MonoBehaviour
{
    public ItemData m_itemData;
    public class ItemData
    {
        public SharedData m_shared;
        public class SharedData
        {
            public string m_name; public UnityEngine.Sprite[] m_icons; public float m_food, m_foodStamina, m_foodRegen, m_foodBurnTime;
            public HitData.DamageTypes m_damages, m_damagesPerLevel;
            public float m_blockPower, m_blockPowerPerLevel, m_deflectionForce, m_timedBlockBonus, m_armor, m_armorPerLevel;
            public float m_maxDurability, m_durabilityPerLevel, m_weight, m_movementModifier, m_attackForce, m_eitrRegenModifier;
            public int m_maxQuality, m_setSize; public string m_setName; public StatusEffect m_setStatusEffect, m_equipStatusEffect;
            public System.Collections.Generic.List<HitData.DamageModPair> m_damageModifiers;
        }
    }
}
public class Pickable : UnityEngine.MonoBehaviour { public UnityEngine.GameObject m_itemPrefab; }
public class Procreation : UnityEngine.MonoBehaviour { public UnityEngine.GameObject m_offspring; }
public class Growup : UnityEngine.MonoBehaviour { public UnityEngine.GameObject m_grownPrefab; }
public class Recipe : UnityEngine.ScriptableObject { }
public class CharacterDrop : UnityEngine.MonoBehaviour { public class Drop { } }
public class SpawnSystem : UnityEngine.MonoBehaviour { public class SpawnData { } }
public class CookingStation : UnityEngine.MonoBehaviour { public class ItemConversion { } }
public class Heightmap { public enum Biome { None = 0 } public enum BiomeArea { Edge = 1 } }
public class StatusEffect : UnityEngine.ScriptableObject { public float m_ttl; public string m_name, m_tooltip; public UnityEngine.Sprite m_icon; public virtual void ModifySkillLevel(Skills.SkillType skill, ref float value) { } }
public class SE_Stats : StatusEffect
{
    public Skills.SkillType m_skillLevel, m_skillLevel2; public float m_skillLevelModifier, m_skillLevelModifier2;
    public float m_runStaminaDrainModifier, m_jumpStaminaUseModifier, m_speedModifier, m_healthRegenMultiplier = 1f, m_staminaRegenMultiplier = 1f, m_eitrRegenMultiplier = 1f;
    public System.Collections.Generic.List<HitData.DamageModPair> m_mods;
    public override void ModifySkillLevel(Skills.SkillType skill, ref float value) { }
}
public class Skills { public enum SkillType { None, Swords, Axes, Polearms, Spears, Blocking, ElementalMagic, BloodMagic, Sneak, Run, Knives, Crossbows, Clubs, Bows } }
public class HitData
{
    public struct DamageTypes { public float m_damage, m_blunt, m_slash, m_pierce, m_chop, m_pickaxe, m_fire, m_frost, m_lightning, m_poison, m_spirit; public void Modify(float multiplier) { } }
    public struct DamageModPair { public DamageType m_type; public DamageModifier m_modifier; }
    [System.Flags] public enum DamageType { Blunt = 1, Slash = 2, Pierce = 4 }
    public enum DamageModifier { Normal, Resistant, Weak }
}
