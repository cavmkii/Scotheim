using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Scotheim.Content
{
    /// <summary>
    /// Munro bagging. Summit cairns (location scotheim_cairn) stand on high Munro ground; touching one adds
    /// it to the character's tally for this world. Bagging <see cref="ToCompleat"/> makes the character a
    /// "compleatist", which gives a lasting effect (re-applied whenever it's missing).
    ///
    /// The tally lives in the player's custom data, which the game saves with the character.
    /// </summary>
    static class Bagging
    {
        internal const int ToCompleat = 16; // expand_locations_scotheim.yaml places up to 30
        static StatusEffect compleatist;
        static float nextCheck;

        internal static void Add()
        {
            var cairn = PrefabManager.Instance.CreateClonedPrefab("Scot_SummitCairn", "HeathRockPillar");
            if (cairn == null)
            {
                Plugin.Log.LogWarning("Munro bagging skipped: HeathRockPillar not found.");
                return;
            }
            var destructible = cairn.GetComponent("Destructible");
            if (destructible != null) Object.DestroyImmediate(destructible);
            cairn.transform.localScale = new Vector3(0.7f, 0.3f, 0.7f); // squat: a heap, not a pillar
            // The game's RuneStone supplies hover and use (it implements Hoverable and Interactable for this
            // game version); CairnPatches below replaces what it shows and does when a SummitCairn is present.
            var runeType = System.Type.GetType("RuneStone, assembly_valheim");
            if (runeType == null)
            {
                Plugin.Log.LogWarning("Munro bagging skipped: the game's RuneStone component wasn't found.");
                return;
            }
            cairn.AddComponent(runeType);
            cairn.AddComponent<SummitCairn>();
            PrefabManager.Instance.AddPrefab(cairn);

            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = "Scot_Compleatist";
            se.m_name = "$se_scot_compleatist";
            se.m_tooltip = "$se_scot_compleatist_tooltip";
            var icon = PrefabManager.Cache.GetPrefab<StatusEffect>("SetEffect_FenringArmor");
            if (icon != null) se.m_icon = icon.m_icon;
            se.m_skillLevel = Skills.SkillType.Run; se.m_skillLevelModifier = 15f;
            se.m_runStaminaDrainModifier = -0.15f;
            se.m_jumpStaminaUseModifier = -0.15f;
            GameFields.TrySet(se, -0.5f, "m_fallDamageModifier");
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, false));
            compleatist = se;
        }

        static string Key => "scotheim_munros_" + Patches.WorldGen.CurrentSeed;

        static Dictionary<string, string> Data(Player player) =>
            GameFields.Get(player, "m_customData") as Dictionary<string, string>;

        internal static HashSet<string> Bagged(Player player)
        {
            var data = Data(player);
            string list;
            var set = new HashSet<string>();
            if (data != null && data.TryGetValue(Key, out list))
                foreach (var id in list.Split(',')) if (id.Length > 0) set.Add(id);
            return set;
        }

        internal static int Bag(Player player, string id)
        {
            var data = Data(player);
            var set = Bagged(player);
            if (data == null || !set.Add(id)) return -1;
            data[Key] = string.Join(",", new List<string>(set).ToArray());
            return set.Count;
        }

        /// <summary>Called every frame from the plugin: gives compleatists their effect if it's missing.</summary>
        internal static void Tick()
        {
            if (compleatist == null || Time.time < nextCheck) return;
            nextCheck = Time.time + 5f;
            var player = Player.m_localPlayer;
            if (player == null || Bagged(player).Count < ToCompleat) return;
            var seman = player.GetSEMan();
            var active = GameFields.Get(seman, "m_statusEffects") as System.Collections.IList;
            if (active != null)
                foreach (var effect in active)
                    if (effect is StatusEffect && ((StatusEffect)effect).name.StartsWith(compleatist.name)) return;
            GameFields.Call(seman, "AddStatusEffect", compleatist, false);
        }
    }

    /// <summary>Marks a summit cairn; the cairn's RuneStone calls these through <see cref="CairnPatches"/>.</summary>
    public class SummitCairn : MonoBehaviour
    {
        string Id
        {
            get
            {
                var p = transform.position;
                return Mathf.RoundToInt(p.x) + ":" + Mathf.RoundToInt(p.z);
            }
        }

        internal string HoverText()
        {
            var player = Player.m_localPlayer;
            var done = player != null && Bagging.Bagged(player).Contains(Id);
            var text = "Summit cairn\n" + (done ? "You've bagged this one." : "[<color=yellow><b>$KEY_Use</b></color>] Add a stone");
            return global::Localization.instance.Localize(text); // Localization is in assembly_guiutils
        }

        internal bool Interact(Humanoid user, bool hold)
        {
            if (hold) return false;
            var player = user as Player;
            if (player == null || player != Player.m_localPlayer) return false;
            int count = Bagging.Bag(player, Id);
            if (count < 0)
            {
                player.Message(MessageHud.MessageType.Center, "You've already left a stone here.");
                return true;
            }
            var message = count >= Bagging.ToCompleat
                ? (count == Bagging.ToCompleat ? "Compleatist! Every hill is bagged." : "Munros bagged: " + count)
                : "Munros bagged: " + count + " of " + Bagging.ToCompleat;
            player.Message(MessageHud.MessageType.Center, message);
            return true;
        }
    }

    /// <summary>
    /// Hover text and use for summit cairns, taken over from the game's RuneStone. Found by name so the
    /// game's exact member signatures don't have to be known here; a missing one is logged and skipped.
    /// </summary>
    static class CairnPatches
    {
        // A null target would make PatchAll throw and skip every later patch, so check first.
        static bool Report(MethodBase method, string name)
        {
            if (method == null) Plugin.Log.LogWarning("Summit cairns can't be used: " + name + " wasn't found.");
            return method != null;
        }

        [HarmonyPatch]
        static class Hover
        {
            static MethodBase Target() => AccessTools.Method(AccessTools.TypeByName("RuneStone"), "GetHoverText");
            static bool Prepare() => Report(Target(), "RuneStone.GetHoverText");
            static MethodBase TargetMethod() => Target();

            static bool Prefix(Component __instance, ref string __result)
            {
                var cairn = __instance.GetComponent<SummitCairn>();
                if (cairn == null) return true;
                __result = cairn.HoverText();
                return false;
            }
        }

        [HarmonyPatch]
        static class Use
        {
            static MethodBase Target() => AccessTools.Method(AccessTools.TypeByName("RuneStone"), "Interact");
            static bool Prepare() => Report(Target(), "RuneStone.Interact");
            static MethodBase TargetMethod() => Target();

            static bool Prefix(Component __instance, Humanoid __0, bool __1, ref bool __result)
            {
                var cairn = __instance.GetComponent<SummitCairn>();
                if (cairn == null) return true;
                __result = cairn.Interact(__0, __1);
                return false;
            }
        }
    }
}
