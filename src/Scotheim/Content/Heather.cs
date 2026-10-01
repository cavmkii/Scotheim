using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace Scotheim.Content
{
    /// <summary>
    /// Heather (Calluna vulgaris): the purple of the Highland hills in late summer. Vanilla's nearest clutter is the
    /// Plains heath flowers, which are red. This copies that clutter as Scot_Heather with its petals hue-shifted to
    /// heather purple (stems and leaves keep their green), and adds it to the game's clutter list just before EWD
    /// reads that list (EWD snapshots it in a ZoneSystem.Start postfix), so expand_clutter_scotheim.yaml can use it
    /// by name. Game members are reached by name; a miss is logged and the heather stays unpainted or absent.
    /// </summary>
    static class Heather
    {
        internal const string Name = "Scot_Heather";
        const string Base = "instanced_heathflowers";
        const float Purple = 0.80f; // hue, about 288 degrees: heather's pinkish purple
        static GameObject prefab;

        [HarmonyPatch]
        static class AddToClutter
        {
            static MethodBase Target() => AccessTools.Method(AccessTools.TypeByName("ZoneSystem"), "Start");
            static bool Prepare()
            {
                if (Target() != null) return true;
                Plugin.Log.LogWarning("Heather skipped: ZoneSystem.Start wasn't found.");
                return false;
            }
            static MethodBase TargetMethod() => Target();

            static void Prefix()
            {
                try { Add(); }
                catch (Exception e) { Plugin.Log.LogError("Couldn't add heather: " + e); }
            }
        }

        static void Add()
        {
            var clutterType = AccessTools.TypeByName("ClutterSystem");
            var property = clutterType != null ? AccessTools.Property(clutterType, "instance") : null;
            var field = clutterType != null && property == null ? AccessTools.Field(clutterType, "m_instance") : null;
            var system = property != null ? property.GetValue(null, null) : field != null ? field.GetValue(null) : null;
            var list = system != null ? GameFields.Get(system, "m_clutter") as IList : null;
            if (list == null)
            {
                Plugin.Log.LogWarning("Heather skipped: the game's clutter list wasn't found.");
                return;
            }
            object template = null;
            foreach (var entry in list)
            {
                var p = GameFields.Get(entry, "m_prefab") as GameObject;
                if (p == null) continue;
                if (p.name == Name) return; // already added for this ClutterSystem
                if (p.name == Base && template == null) template = entry;
            }
            if (template == null)
            {
                Plugin.Log.LogWarning("Heather skipped: " + Base + " isn't in the clutter list.");
                return;
            }
            if (prefab == null) prefab = Make((GameObject)GameFields.Get(template, "m_prefab"));
            if (prefab == null) return;

            // A disabled entry: it only makes the prefab known. Where heather grows is set in expand_clutter_scotheim.yaml.
            var entryCopy = Activator.CreateInstance(template.GetType());
            GameFields.TrySetObject(entryCopy, prefab, "m_prefab");
            GameFields.TrySet(entryCopy, Name, "m_name");
            GameFields.TrySet(entryCopy, false, "m_enabled");
            GameFields.TrySet(entryCopy, 0, "m_amount");
            list.Add(entryCopy);
        }

        static GameObject Make(GameObject original)
        {
            var copy = PrefabManager.Instance.CreateClonedPrefab(Name, original);
            if (copy == null)
            {
                Plugin.Log.LogWarning("Heather skipped: couldn't copy " + Base + ".");
                return null;
            }
            int painted = 0;
            // Instanced clutter draws with its InstanceRenderer's material; plain renderers are covered too.
            var rendererType = AccessTools.TypeByName("InstanceRenderer");
            if (rendererType != null)
                foreach (var r in copy.GetComponentsInChildren(rendererType, true))
                {
                    var m = GameFields.Get(r, "m_material") as Material;
                    if (m == null) continue;
                    GameFields.TrySetObject(r, Paint(m), "m_material");
                    painted++;
                }
            foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++) if (shared[i] != null) { shared[i] = Paint(shared[i]); painted++; }
                r.sharedMaterials = shared;
            }
            Plugin.Log.LogInfo("Heather: " + painted + " materials made purple.");
            return copy;
        }

        static Material Paint(Material original)
        {
            var m = new Material(original) { name = original.name + " (heather)" };
            var source = m.HasProperty("_MainTex") ? m.mainTexture as Texture2D : null;
            if (source != null)
            {
                var texture = HueShift(source);
                if (texture != null) m.mainTexture = texture;
            }
            else if (m.HasProperty("_Color"))
            {
                m.color = Color.HSVToRGB(Purple, 0.45f, 0.8f);
            }
            return m;
        }

        // Moves red-to-orange pixels (the petals) to heather purple, keeping each pixel's own shading and alpha.
        static Texture2D HueShift(Texture2D source)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                var pixels = copy.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    float h, s, v;
                    Color.RGBToHSV(pixels[i], out h, out s, out v);
                    if (s < 0.25f || !(h < 0.13f || h > 0.9f)) continue; // greens, greys and browns stay
                    var c = Color.HSVToRGB(Purple, Mathf.Min(1f, s * 0.85f), v);
                    c.a = pixels[i].a;
                    pixels[i] = c;
                }
                copy.SetPixels(pixels);
                copy.Apply(true);
                copy.name = source.name + " (heather)";
                copy.wrapMode = source.wrapMode;
                copy.filterMode = source.filterMode;
                return copy;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Heather keeps its vanilla colour: couldn't repaint " + source.name + " (" + e.Message + ").");
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
