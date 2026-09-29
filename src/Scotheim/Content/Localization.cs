using System.Collections.Generic;
using Jotunn.Managers;

namespace Scotheim.Content
{
    /// <summary>English names for the Highland clones. Tokens are derived from prefab names.</summary>
    static class Localization
    {
        internal static string ItemName(string prefab) => "$item_" + prefab.ToLowerInvariant();
        internal static string ItemDescription(string prefab) => "$item_" + prefab.ToLowerInvariant() + "_desc";
        internal static string CreatureName(string prefab) => "$enemy_" + prefab.ToLowerInvariant();

        static readonly Dictionary<string, string[]> ItemText = new Dictionary<string, string[]>
        {
            { "Scot_Wool", new[] { "Wool", "Coarse fleece from a blackface sheep." } },
            { "Scot_Mutton", new[] { "Raw mutton", "Hill-fed and lean. Cook it first." } },
            { "Scot_CookedMutton", new[] { "Roast mutton", "Heavy food for a long day on the hill." } },
            { "Scot_Beef", new[] { "Raw Highland beef", "Marbled from a winter on the moor." } },
            { "Scot_CookedBeef", new[] { "Roast Highland beef", "Slow to eat and slow to wear off." } },
            { "Scot_Blaeberries", new[] { "Blaeberries", "Small and sour. They stain everything they touch." } },
            { "Scot_HighlandHide", new[] { "Highland hide", "Shaggy red-brown hide with the hair still on." } },
            { "Scot_MartenPelt", new[] { "Pine marten pelt", "Soft, dark and small." } },
            { "Scot_SithPelt", new[] { "Sìth pelt", "Greener than any living beast's. It is cold to the touch." } },
            { "Scot_KelpieMane", new[] { "Kelpie mane", "Wet weed and horsehair. It never dries." } },
            { "Scot_WashersShroud", new[] { "Washer's shroud", "Linen she was washing at the ford. You don't ask whose." } },
            { "Scot_GiantHeartstone", new[] { "Giant's heartstone", "Warm, and heavier than its size." } },
            { "Scot_TartanCloth", new[] { "Tartan cloth", "Wool woven in a sett and dyed with blaeberry." } },
            { "Scot_Claymore", new[] { "Claymore", "Claidheamh mòr, the great sword. Two hands and a long reach." } },
            { "Scot_Dirk", new[] { "Dirk", "A long, single-edged knife, worn at the belt and drawn close in." } },
            { "Scot_LochaberAxe", new[] { "Lochaber axe", "A cleaver of a blade on a long shaft, with a hook for pulling riders down." } },
            { "Scot_Targe", new[] { "Targe", "Wood faced with hide and studded with iron. Small enough to fight behind." } },
            { "Scot_Knapskull", new[] { "Knapskull", "A plain steel cap." } },
            { "Scot_Acton", new[] { "Acton", "A quilted jack of wool and tartan. Light, and it doesn't slow you." } },
            { "Scot_Brigandine", new[] { "Brigandine", "Steel plates riveted inside a cloth coat." } },
            { "Scot_GaeBulg", new[] { "Gáe Bulg", "Cú Chulainn's barbed spear, taught to him by Scáthach on Skye. The wound festers." } },
            { "Scot_ClaiomhSolais", new[] { "Claíomh Solais", "The Sword of Light, won from a giant. It burns the dead." } },
            { "Scot_WallaceSword", new[] { "Wallace sword", "Longer than a man is tall, and slow. Nothing stands up after it lands." } },
        };

        static readonly Dictionary<string, string> CreatureText = new Dictionary<string, string>
        {
            { "Scot_RedDeer", "Red deer" },
            { "Scot_Sheep", "Blackface sheep" },
            { "Scot_Lamb", "Lamb" },
            { "Scot_HighlandCow", "Highland cow" },
            { "Scot_HighlandCalf", "Highland calf" },
            { "Scot_PineMarten", "Pine marten" },
            { "Scot_HillWolf", "Hill wolf" },
            { "Scot_CuSith", "Cù-sìth" },
            { "Scot_CatSith", "Cat-sìth" },
            { "Scot_EachUisge", "Each-uisge" },
            { "Scot_BeanNighe", "Bean-nighe" },
            { "Scot_Fuath", "Fuath" },
            { "Scot_HillGiant", "Hill giant" },
            { "Scot_Redcap", "Redcap" },
        };

        internal static void Register()
        {
            var english = new Dictionary<string, string>();
            foreach (var entry in ItemText)
            {
                english[ItemName(entry.Key).Substring(1)] = entry.Value[0];
                english[ItemDescription(entry.Key).Substring(1)] = entry.Value[1];
            }
            foreach (var entry in CreatureText)
                english[CreatureName(entry.Key).Substring(1)] = entry.Value;
            LocalizationManager.Instance.GetLocalization().AddTranslation("English", english);
        }
    }
}
