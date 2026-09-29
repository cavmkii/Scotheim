// Minimal BepInEx surface for compiling Content/ (built as an assembly named BepInEx, as Jotunn.dll expects).
namespace BepInEx
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class BepInPlugin : System.Attribute { public BepInPlugin(string guid, string name, string version) { } public string GUID { get; } public string Name { get; } }
    public abstract class BaseUnityPlugin : UnityEngine.MonoBehaviour { }
}
namespace BepInEx.Logging { public class ManualLogSource { public void LogInfo(object o) { } public void LogWarning(object o) { } public void LogError(object o) { } } }
