using System;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace Scotheim.Content
{
    /// <summary>
    /// Recolours the vanilla models the Highland clones borrow, so they read as their own things until
    /// proper models exist. Only colour and texture change, never shape.
    ///
    /// Every material is copied before it's changed, so vanilla creatures and items keep their look.
    /// Tints multiply the texture's own colours (values above 1 brighten). Body armour that Valheim paints
    /// onto the player's skin (m_armorMaterial) is tinted too, but never given a tiling texture, which would
    /// cover the skin as well. Items whose look changes get a freshly rendered icon.
    ///
    /// KITBASH later marks pieces whose shape is wrong and needs Jötunn's kitbashing (or a real model).
    /// </summary>
    static class Reskin
    {
        sealed class Look
        {
            public Color Tint = Color.white;
            public bool Tartan, Fleece; // replace the texture outright (a tint can't lighten a dark texture)
            public Look(float r, float g, float b) { Tint = new Color(r, g, b, 1f); }
            public Look() { }
        }

        static Look Tint(float r, float g, float b) => new Look(r, g, b);
        static Look TartanLook() => new Look { Tartan = true };
        static Look FleeceLook() => new Look { Fleece = true };

        static readonly Dictionary<string, Look> Creatures = new Dictionary<string, Look>
        {
            { "Scot_CuSith", Tint(0.4f, 0.75f, 0.45f) },          // the green fairy hound (0.3/0.55/0.35 read nearly black)
            { "Scot_CatSith", Tint(0.18f, 0.18f, 0.2f) },         // black. KITBASH later: still a wolf-man (Ulv); needs a cat model
            { "Scot_BeanNighe", Tint(1.25f, 1.25f, 1.3f) },       // pale
            { "Scot_HighlandCow", Tint(1.15f, 0.6f, 0.35f) },     // ginger. KITBASH later: lox body and horns; needs a cow model
            { "Scot_HighlandCalf", Tint(1.15f, 0.6f, 0.35f) },
            // A tint only turned the dark boar light brown, so the texture is replaced with a generated fleece.
            // KITBASH later: still a boar's shape (tusks included); needs a sheep model.
            { "Scot_Sheep", FleeceLook() },
            { "Scot_Lamb", FleeceLook() },
            { "Scot_RedDeer", Tint(1.15f, 0.8f, 0.65f) },         // redder coat
            { "Scot_PineMarten", Tint(0.55f, 0.4f, 0.3f) },       // dark brown. KITBASH later: a hare; needs a marten model
            { "Scot_HillWolf", Tint(0.85f, 0.8f, 0.72f) },        // grey-brown
            { "Scot_HillGiant", Tint(0.65f, 0.85f, 0.6f) },       // mossy granite
            { "Scot_Fuath", Tint(0.55f, 0.85f, 0.85f) },          // water-spirit blue-green
            { "Scot_Redcap", Tint(1.3f, 0.6f, 0.55f) },
            { "Scot_GreyMan", Tint(0.7f, 0.72f, 0.78f) },         // grey. KITBASH later: shaggier, taller still
            { "Scot_Sluagh", Tint(0.35f, 0.35f, 0.42f) },         // dark host. KITBASH later: ragged wings           // red. KITBASH later: a red cap
            { "Scot_EachUisge", Tint(0.45f, 0.6f, 0.45f) },       // kelp. KITBASH later: an abomination, not a water horse
        };

        static readonly Dictionary<string, Look> Items = new Dictionary<string, Look>
        {
            // materials
            { "Scot_Wool", Tint(1.05f, 1f, 0.88f) },
            { "Scot_BogIronOre", Tint(1.35f, 0.72f, 0.35f) },     // limonite: rusty orange
            { "Scot_Sinter", Tint(0.75f, 0.45f, 0.32f) },        // roasted ore: dark rust clinker
            // Things lying in the world (Reskin.Prop): the branch and nodules match their items.
            { "Scot_BogOakPickable", Tint(0.3f, 0.26f, 0.22f) },
            { "Scot_BogIronNodule", Tint(1.35f, 0.72f, 0.35f) },
            { "Scot_BogIronDeposit", Tint(1.25f, 0.7f, 0.4f) },
            { "Scot_BogIronDeposit_frac", Tint(1.25f, 0.7f, 0.4f) },
            { "Scot_PeatHag", Tint(0.16f, 0.13f, 0.11f) },        // near-black wet peat
            { "Scot_Hacksilver", Tint(1.25f, 1.3f, 1.4f) },
            { "Scot_Peat", Tint(0.65f, 0.45f, 0.3f) },            // brown turf, not coal
            { "Scot_DriedPeat", Tint(0.85f, 0.65f, 0.45f) },      // dried turf: lighter, dustier brown
            { "Scot_UisgeBeatha", Tint(1.25f, 0.85f, 0.45f) },    // amber       // scrap iron's look, made silvery
            { "Scot_BogIron", Tint(0.6f, 0.52f, 0.46f) },
            { "Scot_BogOak", Tint(0.3f, 0.26f, 0.22f) },
            { "Scot_Cairngorm", Tint(0.6f, 0.45f, 0.3f) },        // smoky quartz
            { "Scot_RowanWood", Tint(1f, 0.75f, 0.62f) },
            { "Scot_HighlandHide", Tint(1.15f, 0.62f, 0.38f) },
            { "Scot_MartenPelt", Tint(0.55f, 0.4f, 0.3f) },
            { "Scot_SithPelt", Tint(0.45f, 0.85f, 0.5f) },
            { "Scot_KelpieMane", Tint(0.35f, 0.55f, 0.4f) },
            { "Scot_WashersShroud", Tint(1.2f, 1.2f, 1.2f) },
            { "Scot_GiantHeartstone", Tint(1.35f, 0.55f, 0.35f) }, // ember-red
            { "Scot_TartanCloth", TartanLook() },
            // armour and capes
            { "Scot_BlueBonnet", Tint(0.45f, 0.55f, 1.1f) },
            { "Scot_Leine", Tint(1.2f, 0.95f, 0.5f) },            // saffron
            { "Scot_Kilt", Tint(0.45f, 0.6f, 0.7f) },             // KITBASH later: tartan can't go on a skin overlay; needs its own mesh
            { "Scot_BeltedPlaid", TartanLook() },
            { "Scot_Acton", Tint(0.8f, 0.78f, 0.68f) },
            { "Scot_Brigandine", Tint(0.7f, 0.65f, 0.6f) },       // bog iron
            { "Scot_Chausses", Tint(0.7f, 0.65f, 0.6f) },
            { "Scot_SaltireCape", Tint(0.35f, 0.5f, 1.15f) },     // KITBASH later: the white saltire cross needs a mesh or UV-mapped texture
            { "Scot_PictishChain", Tint(0.82f, 0.86f, 0.95f) },   // silver, not gold. KITBASH later: still a circlet, not a chain
            { "Scot_PictishCloak", Tint(0.55f, 0.4f, 0.3f) },     // marten fur
            { "Scot_SithRobe", Tint(0.5f, 0.9f, 0.55f) },
            { "Scot_SithLeggings", Tint(0.5f, 0.9f, 0.55f) },
            { "Scot_FairyFlag", Tint(1.1f, 0.9f, 0.6f) },         // the real flag is faded yellow-brown silk
            // Weapons keep their looks for now. KITBASH later: basket-hilted claymore, longer Wallace blade,
            // long-hafted sparth, hooked Lochaber axe, a real log for the caber, a square Pictish shield,
            // a studded targe and a holed stone for the Seer's stone.
        };

        static Texture2D tartan;

        internal static void Creature(string name, GameObject prefab)
        {
            LogParts(name, prefab);
            Look look;
            if (Creatures.TryGetValue(name, out look)) Apply(name, prefab, look);
        }

        // The model's parts, for planning what to hide or attach later (e.g. a boar's tusks on the sheep).
        static void LogParts(string name, GameObject prefab)
        {
            var parts = new List<string>();
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetType().Name == "ParticleSystemRenderer") continue;
                var materials = new List<string>();
                foreach (var m in renderer.sharedMaterials) if (m != null) materials.Add(m.name);
                var skinned = renderer as SkinnedMeshRenderer;
                parts.Add(renderer.name + (renderer.gameObject.activeSelf ? "" : " (off)") + " [" + string.Join(", ", materials.ToArray()) + "]" +
                    (skinned != null && skinned.rootBone != null ? " root " + skinned.rootBone.name + ", " + skinned.bones.Length + " bones" : ""));
            }
            Plugin.Log.LogInfo("Parts " + name + ": " + string.Join("; ", parts.ToArray()));
        }

        /// <summary>Recolours something placed in the world (a pickable or deposit) with the same looks as items.</summary>
        internal static void Prop(string name, GameObject prefab)
        {
            Look look;
            if (Items.TryGetValue(name, out look)) Apply(name, prefab, look);
        }

        /// <summary>Recolours an item, including the skin overlay armour uses, and renders it a new icon.</summary>
        internal static void Item(string name, GameObject prefab, ItemDrop.ItemData.SharedData shared)
        {
            Look look;
            if (!Items.TryGetValue(name, out look)) return;
            Apply(name, prefab, look);
            var overlay = GameFields.Get(shared, "m_armorMaterial") as Material;
            if (overlay != null)
                GameFields.TrySetObject(shared, Recolour(overlay, look, allowTexture: false), "m_armorMaterial");
            var icon = RenderManager.Instance.Render(prefab, RenderManager.IsometricRotation);
            if (icon != null) shared.m_icons = new[] { icon };
            else Plugin.Log.LogWarning("Reskin " + name + ": couldn't render an icon; it keeps the vanilla one.");
        }

        static void Apply(string name, GameObject prefab, Look look)
        {
            int changed = 0, untintable = 0;
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetType().Name == "ParticleSystemRenderer") continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    if (!materials[i].HasProperty("_Color") && !((look.Tartan || look.Fleece) && materials[i].HasProperty("_MainTex"))) { untintable++; continue; }
                    materials[i] = Recolour(materials[i], look, allowTexture: true);
                    changed++;
                }
                renderer.sharedMaterials = materials;
            }
            Plugin.Log.LogInfo("Reskin " + name + ": " + changed + " materials changed" +
                (untintable > 0 ? ", " + untintable + " without a colour to tint" : "") + ".");
        }

        static Material Recolour(Material original, Look look, bool allowTexture)
        {
            var copy = new Material(original) { name = original.name + "_scot" };
            if (copy.HasProperty("_Color")) copy.color = original.color * look.Tint;
            if (look.Tartan && allowTexture && copy.HasProperty("_MainTex"))
            {
                copy.mainTexture = Tartan();
                copy.mainTextureScale = new Vector2(3f, 3f);
                if (copy.HasProperty("_Color")) copy.color = Color.white;
            }
            if (look.Fleece && allowTexture && copy.HasProperty("_MainTex"))
            {
                copy.mainTexture = Fleece();
                copy.mainTextureScale = Vector2.one;
                copy.mainTextureOffset = Vector2.zero;
                if (copy.HasProperty("_Color")) copy.color = Color.white;
            }
            return copy;
        }

        static Texture2D fleece;

        // Cream wool: two octaves of smoothed value noise for clumps, and darker hollows between curls.
        static Texture2D Fleece()
        {
            if (fleece != null) return fleece;
            const int size = 256;
            var random = new System.Random(1745);
            Func<int, float[,]> grid = cells =>
            {
                var g = new float[cells + 1, cells + 1];
                for (int i = 0; i <= cells; i++) for (int j = 0; j <= cells; j++) g[i, j] = (float)random.NextDouble();
                for (int i = 0; i <= cells; i++) { g[i, cells] = g[i, 0]; g[cells, i] = g[0, i]; } // tile seamlessly
                return g;
            };
            var coarse = grid(16);
            var fine = grid(64);
            Func<float[,], int, int, int, float> sample = (g, cells, x, y) =>
            {
                float fx = x * cells / (float)size, fy = y * cells / (float)size;
                int ix = (int)fx, iy = (int)fy;
                float tx = Mathf.SmoothStep(0f, 1f, fx - ix), ty = Mathf.SmoothStep(0f, 1f, fy - iy);
                float a = Mathf.Lerp(g[ix, iy], g[ix + 1, iy], tx), b = Mathf.Lerp(g[ix, iy + 1], g[ix + 1, iy + 1], tx);
                return Mathf.Lerp(a, b, ty);
            };
            var wool = new Color(0.9f, 0.86f, 0.76f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float n = 0.65f * sample(coarse, 16, x, y) + 0.35f * sample(fine, 64, x, y);
                    float shade = 0.72f + 0.34f * Mathf.Pow(n, 0.8f);
                    pixels[y * size + x] = new Color(wool.r * shade, wool.g * shade, wool.b * shade, 1f);
                }
            fleece = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "scot_fleece", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            fleece.SetPixels(pixels);
            fleece.Apply(true);
            return fleece;
        }

        // A dark Government-style sett in Black Watch colours (blue, black, green), woven as a 2/2 twill:
        // threads alternate over and under in pairs, which gives tartan its diagonal texture. The thread
        // counts are a common rendering of the Black Watch sett, not an official registration.
        static Texture2D Tartan()
        {
            if (tartan != null) return tartan;
            var blue = new Color(0.1f, 0.14f, 0.36f);
            var black = new Color(0.04f, 0.04f, 0.05f);
            var green = new Color(0.07f, 0.24f, 0.12f);
            var half = new List<KeyValuePair<Color, int>>
            {
                new KeyValuePair<Color, int>(blue, 22), new KeyValuePair<Color, int>(black, 2), new KeyValuePair<Color, int>(blue, 2),
                new KeyValuePair<Color, int>(black, 2), new KeyValuePair<Color, int>(blue, 2), new KeyValuePair<Color, int>(black, 16),
                new KeyValuePair<Color, int>(green, 16), new KeyValuePair<Color, int>(black, 2), new KeyValuePair<Color, int>(green, 16),
                new KeyValuePair<Color, int>(black, 16), new KeyValuePair<Color, int>(blue, 16), new KeyValuePair<Color, int>(black, 2),
                new KeyValuePair<Color, int>(blue, 4),
            };
            // A symmetric sett repeats by mirroring around its first and last stripes.
            var threads = new List<Color>();
            foreach (var stripe in half) for (int i = 0; i < stripe.Value; i++) threads.Add(stripe.Key);
            for (int i = threads.Count - 2; i > 0; i--) threads.Add(threads[i]);
            int n = threads.Count;
            tartan = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "scot_tartan", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    pixels[y * n + x] = ((x + y) / 2) % 2 == 0 ? threads[x] : threads[y];
            tartan.SetPixels(pixels);
            tartan.Apply(true);
            return tartan;
        }
    }
}
