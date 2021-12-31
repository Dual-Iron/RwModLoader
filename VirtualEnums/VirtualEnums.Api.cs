using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VirtualEnums;

/// <summary>
/// Holds methods for declaring enums.
/// </summary>
public static partial class VirtualEnumApi
{
    private static readonly HashSet<Assembly> cache = new();
    private static readonly Dictionary<Type, VirtualEnumData> virtualEnums = new();

    /// <summary>
    /// Clears all enum extensions.
    /// </summary>
    public static void Clear()
    {
        cache.Clear();
        virtualEnums.Clear();
    }

    /// <summary>
    /// Calls <see cref="UseType(Type)"/> on all types that start with "EnumExt_" in the assembly.
    /// </summary>
    /// <param name="asm">The assembly to search.</param>
    /// <param name="reflError">If a <see cref="ReflectionTypeLoadException"/> is thrown while iterating the assembly's types, it is stored here.</param>
    public static void UseAssembly(Assembly asm, out ReflectionTypeLoadException? reflError)
    {
        reflError = null;

        if (!cache.Add(asm)) {
            return;
        }

        var types = GetTypesSafely(ref reflError);

        foreach (var type in types) {
            if (type.Name.StartsWith("EnumExt_")) {
                UseType(type);
            }
        }

        IEnumerable<Type> GetTypesSafely(ref ReflectionTypeLoadException? reflError)
        {
            try {
                return asm.GetTypes();
            } catch (ReflectionTypeLoadException e) {
                reflError = e;
                return e.Types.Where(t => t != null);
            }
        }
    }

    /// <summary>
    /// For all public static fields with an enum type in <paramref name="type"/>, the field's enum type will be extended using the field's name through <see cref="AddDeclaration{T}(string)"/>.
    /// <para/> Example field declaration: <para/> <c>public static <see cref="BindingFlags"/> MyCustomFlag;</c>
    /// </summary>
    /// <param name="type">The type.</param>
    public static void UseType(Type type)
    {
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)) {
            if (field.FieldType.IsEnum) {
                long declared = AddDeclaration(field.FieldType, field.Name);
                object declaredAsEnumType = AsEnum(field.FieldType, declared);
                field.SetValue(null, declaredAsEnumType);
            }
        }
    }

    /// <summary>
    /// Gets <paramref name="value"/> as an unsigned integer. Cleaner method than using unchecked().
    /// </summary>
    public static ulong AsUnsigned(long value) => unchecked((ulong)value);

    /// <summary>
    /// Declares the enum member.
    /// </summary>
    /// <param name="name">The name of the enum value.</param>
    /// <returns>The long value of the declared enum. Use <see cref="AsUnsigned(long)"/> on the return if the underlying integral type of <typeparamref name="T"/> is unsigned.</returns>
    public static long AddDeclaration<T>(string name) where T : Enum => AddDeclaration(typeof(T), name);

    internal static long AddDeclaration(Type enumType, string name)
    {
        if (!virtualEnums.TryGetValue(enumType, out var data)) {
            virtualEnums[enumType] = data = new VirtualEnumData();

            Type underlying = Enum.GetUnderlyingType(enumType);
            Array enumvalues = Enum.GetValues(enumType);
            foreach (var objValue in enumvalues) {
                long value = AsLong(underlying, objValue);

                if (data.MaxValue < value)
                    data.MaxValue = value;
            }
        }

        data.EnumValues.Set(name, unchecked(++data.MaxValue));

        return data.MaxValue;
    }

    // Private nested so it can have public members that aren't visible to API.
    // Allows for faster reflection.
    private static class Caster
    {
        public static T Cast<T>(object o) => (T)o;
        public static object CastRuntime(Type t, object o)
        {
            return castMethod.MakeGenericMethod(t).Invoke(null, new[] { o });
        }
        private static readonly MethodInfo castMethod = typeof(Caster).GetMethod("Cast", BindingFlags.Public | BindingFlags.Static | BindingFlags.ExactBinding);
    }

    private static long AsLong(Type underlyingType, object enumValue)
    {
        var asUnderlyingType = Caster.CastRuntime(underlyingType, enumValue);

        if (underlyingType == typeof(int)) return unchecked((int)asUnderlyingType);
        if (underlyingType == typeof(short)) return unchecked((short)asUnderlyingType);
        if (underlyingType == typeof(long)) return (long)asUnderlyingType;
        if (underlyingType == typeof(sbyte)) return unchecked((sbyte)asUnderlyingType);

        if (underlyingType == typeof(uint)) return unchecked((uint)asUnderlyingType);
        if (underlyingType == typeof(ushort)) return unchecked((ushort)asUnderlyingType);
        if (underlyingType == typeof(ulong)) return unchecked((long)(ulong)asUnderlyingType);
        if (underlyingType == typeof(byte)) return unchecked((byte)asUnderlyingType);

        throw new InvalidOperationException("How");
    }

    /// <summary>
    /// Gets the integral value <paramref name="originalValue"/> as the underlying type of the enum <paramref name="enumType"/>.
    /// <para/> Can use FieldInfo.SetValue with this object.
    /// </summary>
    public static object AsEnum(Type enumType, long originalValue)
    {
        var underlyingType = Enum.GetUnderlyingType(enumType);

        if (underlyingType == typeof(int)) return Enum.ToObject(enumType, unchecked((int)originalValue));
        if (underlyingType == typeof(short)) return Enum.ToObject(enumType, unchecked((short)originalValue));
        if (underlyingType == typeof(long)) return Enum.ToObject(enumType, originalValue);
        if (underlyingType == typeof(sbyte)) return Enum.ToObject(enumType, unchecked((sbyte)originalValue));

        if (underlyingType == typeof(uint)) return Enum.ToObject(enumType, unchecked((uint)originalValue));
        if (underlyingType == typeof(ushort)) return Enum.ToObject(enumType, unchecked((ushort)originalValue));
        if (underlyingType == typeof(ulong)) return Enum.ToObject(enumType, unchecked((ulong)originalValue));
        if (underlyingType == typeof(byte)) return Enum.ToObject(enumType, unchecked((byte)originalValue));

        return Convert.ChangeType(originalValue, underlyingType);
    }

    private sealed class VirtualEnumData
    {
        public long MaxValue = long.MinValue;
        public readonly Map<string, long> EnumValues = new();
    }
}
