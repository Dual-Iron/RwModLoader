// Las Vegas
#nullable disable
using MonoMod.RuntimeDetour;
using System;
using System.Globalization;
using System.Reflection;

namespace VirtualEnums;

/// <summary>
/// Holds methods for declaring enums.
/// </summary>
public static partial class VirtualEnumApi
{
    static VirtualEnumApi() => ApplyHooks();

    private static MethodBase EnumMethod(string name) => typeof(Enum).GetMethod(name);
    private static MethodBase EnumMethod(string name, params Type[] types) => typeof(Enum).GetMethod(name, types);

    internal static void ApplyHooks()
    {
        new Hook(EnumMethod("Parse", typeof(Type), typeof(string), typeof(bool)), Parse).Apply();
        new Hook(EnumMethod("GetName"), GetName).Apply();
        new Hook(EnumMethod("GetNames"), GetNames).Apply();
        new Hook(EnumMethod("GetValues"), GetValues).Apply();
        new Hook(EnumMethod("IsDefined"), IsDefined).Apply();
    }

    private static readonly Func<Func<Type, string, bool, object>, Type, string, bool, object> Parse = (orig, type, value, ignoreCase) => {
        if (virtualEnums.TryGetValue(type, out var data)) {
            value = value.Trim();
            if (!ignoreCase) {
                if (data.EnumValues.Forward.TryGetValue(value, out var ret))
                    return Enum.ToObject(type, ret);
            } else {
                foreach (var kvp in data.EnumValues.Forward) {
                    if (string.Compare(kvp.Key, value, true, CultureInfo.InvariantCulture) == 0)
                        return Enum.ToObject(type, kvp.Value);
                }
            }
        }
        return orig(type, value, ignoreCase);
    };

    private static readonly Func<Func<Type, object, string>, Type, object, string> GetName = (orig, type, value) => {
        if (virtualEnums.TryGetValue(type, out var data)) {
            var asLong = AsLong(Enum.GetUnderlyingType(type), value);
            if (data.EnumValues.Reverse.TryGetValue(asLong, out var ret)) {
                return ret;
            }
        }
        return orig(type, value);
    };

    private static readonly Func<Func<Type, string[]>, Type, string[]> GetNames = (orig, type) => {
        var names = orig(type);
        if (virtualEnums.TryGetValue(type, out var data)) {
            var namesExtended = new string[names.Length + data.EnumValues.Count];

            names.CopyTo(namesExtended, 0);

            int count = names.Length;
            foreach (var kvp in data.EnumValues.Forward) {
                namesExtended[count++] = kvp.Key;
            }

            return namesExtended;
        }
        return names;
    };

    private static readonly Func<Func<Type, Array>, Type, Array> GetValues = (orig, type) => {
        var values = orig(type);
        if (virtualEnums.TryGetValue(type, out var data)) {
            var underlyingType = Enum.GetUnderlyingType(type);
            var valuesExtended = Array.CreateInstance(type, values.Length + data.EnumValues.Count);

            values.CopyTo(valuesExtended, 0);

            int count = values.Length;
            foreach (var kvp in data.EnumValues.Forward) {
                valuesExtended.SetValue(Convert.ChangeType(kvp.Value, underlyingType), count++);
            }

            return valuesExtended;
        }
        return values;
    };

    private static readonly Func<Func<Type, object, bool>, Type, object, bool> IsDefined = (orig, type, value) => {
        var ret = orig(type, value);
        if (!ret && virtualEnums.TryGetValue(type, out var data)) {
            var asLong = AsLong(Enum.GetUnderlyingType(type), value);
            if (data.EnumValues.Reverse.TryGetValue(asLong, out _))
                ret = true;
        }
        return ret;
    };
}
