// Stand-ins for the assembly_valheim members Content/ touches (built as an assembly named assembly_valheim).
// These names are NOT verified by this compile: they're written from knowledge of the game. Prefab names
// and components are checked separately by tests/Data/check_data.py.
public class Character : UnityEngine.MonoBehaviour { public string m_name; public float m_health; public enum Faction { Players } }
public class Humanoid : Character { }
public class ItemDrop : UnityEngine.MonoBehaviour
{
    public ItemData m_itemData;
    public class ItemData { public SharedData m_shared; public class SharedData { public string m_name; public float m_food, m_foodStamina, m_foodRegen, m_foodBurnTime; } }
}
public class Pickable : UnityEngine.MonoBehaviour { public UnityEngine.GameObject m_itemPrefab; }
public class Procreation : UnityEngine.MonoBehaviour { public UnityEngine.GameObject m_offspring; }
public class Growup : UnityEngine.MonoBehaviour { public UnityEngine.GameObject m_grownPrefab; }
public class Recipe : UnityEngine.ScriptableObject { }
public class CharacterDrop : UnityEngine.MonoBehaviour { public class Drop { } }
public class SpawnSystem : UnityEngine.MonoBehaviour { public class SpawnData { } }
public class CookingStation : UnityEngine.MonoBehaviour { public class ItemConversion { } }
public class Heightmap { public enum Biome { None = 0 } public enum BiomeArea { Edge = 1 } }
