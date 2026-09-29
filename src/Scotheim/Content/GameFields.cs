using System;
using System.Collections.Generic;
using System.Reflection;

namespace Scotheim.Content
{
    /// <summary>
    /// Sets game fields by name at runtime, for fields whose exact names couldn't be checked against the
    /// game assembly. A missing field is logged rather than breaking the build, and the log lists what took
    /// effect. Each call may try several candidate names; the first that exists wins.
    /// </summary>
    static class GameFields
    {
        static readonly HashSet<string> Reported = new HashSet<string>();

        internal static bool TrySet(object target, object value, params string[] names)
        {
            if (target == null) return false;
            var type = target.GetType();
            foreach (var name in names)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null) continue;
                field.SetValue(target, field.FieldType.IsEnum ? Enum.ToObject(field.FieldType, value) : Convert.ChangeType(value, field.FieldType));
                Report(type.Name + "." + name + " found");
                return true;
            }
            Report(type.Name + ": none of " + string.Join(", ", names) + " exists; that effect is skipped");
            return false;
        }

        internal static bool TrySetObject(object target, object value, params string[] names)
        {
            if (target == null) return false;
            var type = target.GetType();
            foreach (var name in names)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null || !field.FieldType.IsInstanceOfType(value)) continue;
                field.SetValue(target, value);
                Report(type.Name + "." + name + " found");
                return true;
            }
            Report(type.Name + ": none of " + string.Join(", ", names) + " exists; that effect is skipped");
            return false;
        }

        internal static object Get(object target, string name)
        {
            var field = target?.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(target);
        }

        // One line per distinct outcome, so the log isn't flooded by every item.
        static void Report(string message)
        {
            if (!Reported.Add(message)) return;
            if (message.EndsWith("skipped")) Plugin.Log.LogWarning("Game field check: " + message + ".");
            else Plugin.Log.LogInfo("Game field check: " + message + ".");
        }
    }
}
