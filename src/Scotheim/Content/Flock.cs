using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace Scotheim.Content
{
    /// <summary>
    /// Tamed sheep shed wool. Sheep already use their Procreation component for lambs (it can only make one kind of
    /// offspring, which is how hens lay eggs), so wool comes from this component instead: a tamed sheep that isn't
    /// hungry drops one wool every <see cref="Interval"/> seconds of world time. The last drop time is saved on the
    /// sheep, and only the client that owns (simulates) the sheep drops it, so it isn't duplicated in multiplayer.
    /// Game members are reached by name (GameFields); a miss is logged and the sheep just don't shed.
    /// </summary>
    static class Flock
    {
        internal const float Interval = 900f; // 15 minutes, half an in-game day

        internal static void Add(Dictionary<string, GameObject> creatures)
        {
            GameObject sheep;
            // Wool is looked up when a sheep spawns: Jötunn announces creatures before Scotheim's items exist.
            if (creatures.TryGetValue("Scot_Sheep", out sheep)) sheep.AddComponent<ShedWool>();
            foreach (var name in new[] { "Scot_Sheep", "Scot_Lamb" })
            {
                GameObject animal;
                if (creatures.TryGetValue(name, out animal)) Docile(animal);
            }
            // Highland cattle share the sheep's neutral faction, so the herds never fight each other. Unlike sheep they
            // keep their attack: a Highland cow still defends itself when attacked.
            foreach (var name in new[] { "Scot_HighlandCow", "Scot_HighlandCalf" })
            {
                GameObject animal;
                if (creatures.TryGetValue(name, out animal))
                    GameFields.TrySet(animal.GetComponent<Character>(), "AnimalsVeg", "m_faction");
            }
        }

        /// <summary>
        /// Sheep are boar clones, and boars fight. This keeps the boar body (so taming, breeding and wool still work)
        /// but makes it harmless: the deer's neutral faction, no attack items, and it runs when hurt.
        /// </summary>
        static void Docile(GameObject animal)
        {
            var character = animal.GetComponent<Character>();
            GameFields.TrySet(character, "AnimalsVeg", "m_faction");
            GameFields.TrySetObject(character, new GameObject[0], "m_defaultItems");
            GameFields.TrySetObject(character, new GameObject[0], "m_randomWeapon");
            var ai = animal.GetComponent("MonsterAI");
            if (ai == null)
            {
                // Lambs are piglet clones, which use the passive AnimalAI: they flee and never attack anyway.
                Plugin.Log.LogInfo(animal.name + " uses passive animal AI (no MonsterAI), so it already never attacks.");
                return;
            }
            GameFields.TrySet(ai, false, "m_enableHuntPlayer");
            GameFields.TrySet(ai, false, "m_attackPlayerObjects");
            GameFields.TrySet(ai, 1f, "m_fleeIfLowHealth"); // below full health it runs
        }
    }

    public class ShedWool : MonoBehaviour
    {
        const string Key = "scot_wool_time";
        static bool warned;
        float next;
        Component view, tameable;
        Character character;
        GameObject wool;

        void Start()
        {
            view = GetComponent("ZNetView");
            tameable = GetComponent("Tameable");
            character = GetComponent<Character>();
            wool = PrefabManager.Instance.GetPrefab("Scot_Wool");
            if (wool == null && !warned)
            {
                warned = true;
                Plugin.Log.LogWarning("Sheep won't shed: Scot_Wool wasn't found.");
            }
            next = Time.time + Random.Range(5f, 15f); // spread the checks out across a flock
        }

        void Update()
        {
            if (Time.time < next || view == null || tameable == null || wool == null) return;
            next = Time.time + 10f;
            if (!(GameFields.Call(view, "IsOwner") as bool? ?? false)) return;
            if (!(GameFields.Call(character, "IsTamed") as bool? ?? false)) return;
            if (GameFields.Call(tameable, "IsHungry") as bool? ?? true) return;

            var zdo = GameFields.Call(view, "GetZDO");
            var net = NetInstance();
            var now = net != null ? GameFields.Call(net, "GetTimeSeconds") as double? : null;
            if (zdo == null || now == null) return;
            var last = GameFields.Call(zdo, "GetFloat", Key, 0f) as float? ?? 0f;
            if (last == 0f)
            {
                // A sheep tamed just now starts its first fleece from here.
                GameFields.Call(zdo, "Set", Key, (float)now.Value);
                return;
            }
            if (now.Value - last < Flock.Interval) return;
            GameFields.Call(zdo, "Set", Key, (float)now.Value);
            Instantiate(wool, transform.position + Vector3.up * 0.5f + Random.insideUnitSphere * 0.3f, Quaternion.identity);
        }

        static object NetInstance()
        {
            var type = System.Type.GetType("ZNet, assembly_valheim");
            var property = type != null ? type.GetProperty("instance") : null;
            return property != null ? property.GetValue(null, null) : null;
        }
    }
}
