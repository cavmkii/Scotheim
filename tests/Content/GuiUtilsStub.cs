// Stand-in for the game's assembly_guiutils, where the game's Localization class lives (built as an
// assembly named assembly_guiutils, so a reference to the wrong assembly fails here as it does in the real build).
public class Localization { public static Localization instance => null; public string Localize(string text) => text; }
