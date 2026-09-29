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
            { "Scot_BruceAxe", new[] { "Bruce's axe", "At Bannockburn the Bruce split Henry de Bohun's helm with one blow and broke the shaft doing it. Or so Barbour wrote sixty years on." } },
            { "Scot_ClaidheamhSoluis", new[] { "Claidheamh Soluis", "The Sword of Light of the West Highland tales, won from a giant. It burns the dead." } },
            { "Scot_WallaceSword", new[] { "Wallace sword", "Longer than a man is tall, and slow. Nothing stands up after it lands." } },
            { "Scot_FairyFlag", new[] { "Fairy Flag", "Faded silk from the Sìth, kept by the MacLeods. Unfurled, it is said to turn a battle. It falls as softly as it flies." } },
            { "Scot_PictishSpear", new[] { "Pictish spear", "The weapon the symbol stones show most." } },
            { "Scot_PictishShield", new[] { "Pictish shield", "A small shield with a silver boss, like those carved at Aberlemno." } },
            { "Scot_PictishChain", new[] { "Pictish silver chain", "Heavy silver links. Nobody knows quite how they were worn; here, on the head." } },
            { "Scot_PictishJerkin", new[] { "Pictish jerkin", "Hide and silver. No Pictish clothing survives, so this is a guess." } },
            { "Scot_PictishTrews", new[] { "Pictish trews", "Wool and hide, cut for running. A guess, like the jerkin." } },
            { "Scot_PictishCloak", new[] { "Pictish cloak", "Marten fur pinned with a silver brooch. The Romans said the Picts painted themselves; nobody has proved it either way." } },
            { "Scot_BlueBonnet", new[] { "Blue bonnet", "A knitted wool bonnet dyed with blaeberry. Two centuries too late for Wallace, but that never stopped the films." } },
            { "Scot_Leine", new[] { "Léine", "A long saffron shirt, belted, with hide over the shoulders." } },
            { "Scot_Kilt", new[] { "Kilt", "The little kilt is an 18th-century garment. It's warm, it's quick, and it's here anyway." } },
            { "Scot_BeltedPlaid", new[] { "Belted plaid", "Féileadh mòr: yards of tartan, pleated and belted, the rest thrown over the shoulder." } },
            { "Scot_Chausses", new[] { "Chausses", "Mail leggings over quilted hose, the Wars of Independence way." } },
            { "Scot_SaltireCape", new[] { "Saltire cape", "White cross on blue. The Guardians put St Andrew on their seal in 1286." } },
            { "Scot_SithCrown", new[] { "Crown of the Sìth", "Flowers that never wilt, woven by hands you didn't see." } },
            { "Scot_SithRobe", new[] { "Sìth robe", "Green that shifts when you look away. It's always wet at the hem." } },
            { "Scot_SithLeggings", new[] { "Sìth leggings", "Light as mist and as hard to keep hold of." } },
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
            foreach (var entry in Sets.English()) english[entry.Key] = entry.Value;
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
