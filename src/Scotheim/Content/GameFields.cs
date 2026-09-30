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
                field.SetValue(target, !field.FieldType.IsEnum ? Convert.ChangeType(value, field.FieldType)
                    : value is string ? Enum.Parse(field.FieldType, (string)value) : Enum.ToObject(field.FieldType, value));
                Report(type.Name + "." + name + " found");
                return true;
            }
            Report(type.Name + ": none of " + string.Join(", ", names) + " exists; that effect is skipped" + Similar(type, names));
            return false;
        }

        // Lists the type's fields that share a word with the names tried, so the next log shows the real name.
        static string Similar(Type type, string[] names)
        {
            var words = new HashSet<string>();
            foreach (var name in names)
                foreach (var part in System.Text.RegularExpressions.Regex.Split(name.Replace("m_", ""), "(?=[A-Z])"))
                    if (part.Length > 3) words.Add(part.ToLowerInvariant());
            var found = new List<string>();
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                foreach (var word in words)
                    if (field.Name.ToLowerInvariant().Contains(word)) { found.Add(field.Name); break; }
            return found.Count == 0 ? "" : " (fields with similar names: " + string.Join(", ", found.ToArray()) + ")";
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

        /// <summary>Calls the first public or private method with this name whose parameters accept the arguments.</summary>
        internal static object Call(object target, string name, params object[] args)
        {
            if (target == null) return null;
            var type = target as Type ?? target.GetType();
            var instance = target is Type ? null : target;
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name != name) continue;
                var parameters = method.GetParameters();
                if (parameters.Length < args.Length) continue;
                var call = new object[parameters.Length];
                bool fits = true;
                for (int i = 0; i < parameters.Length && fits; i++)
                {
                    if (i < args.Length)
                    {
                        fits = args[i] == null ? !parameters[i].ParameterType.IsValueType : parameters[i].ParameterType.IsInstanceOfType(args[i]);
                        call[i] = args[i];
                    }
                    else if (parameters[i].IsOptional) call[i] = parameters[i].DefaultValue;
                    else fits = false;
                }
                if (!fits) continue;
                Report(type.Name + "." + name + "() found");
                return method.Invoke(instance, call);
            }
            Report(type.Name + ": no method " + name + " fits; that effect is skipped");
            return null;
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
            if (message.Contains("that effect is skipped")) Plugin.Log.LogWarning("Game field check: " + message + ".");
            else Plugin.Log.LogInfo("Game field check: " + message + ".");
        }
    }
}
