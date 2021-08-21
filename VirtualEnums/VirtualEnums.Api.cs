using System;
using System.Collections.Generic;
using System.Reflection;

namespace VirtualEnums
{
    /// <summary>
    /// Holds methods for declaring enums.
    /// </summary>
    public static partial class VirtualEnumApi
    {
        private static readonly Dictionary<Type, VirtualEnumData> virtualEnums = new();

        public static void ReloadWith(IEnumerable<Assembly> assemblies, Action<Exception> errorHandler)
        {
            virtualEnums.Clear();

            foreach (var asm in assemblies) {
                var types = GetTypesSafely(asm);
                foreach (var type in types)
                    if (type.Name.StartsWith("EnumExt_"))
                        try {
                            UseType(type);
                        } catch (Exception e) {
                            errorHandler(e);
                        }
            }

            static IList<Type> GetTypesSafely(Assembly asm)
            {
                try {
                    return asm.GetTypes();
                } catch (ReflectionTypeLoadException e) {
                    var ret = new List<Type>(e.Types.Length);

                    foreach (Type type in e.Types)
                        if (type != null)
                            ret.Add(type);

                    ret.TrimExcess();

                    return ret;
                }
            }
        }

        /// <summary>
        /// For all public static fields with an enum type in <paramref name="type"/>, the field's enum type will be extended using the field's name through <see cref="AddDeclaration{T}(string)"/>.
        /// <para/> Example field declaration: <para/> <c>public static <see cref="AbstractPhysicalObject.AbstractObjectType"/> MyObjType;</c>
        /// </summary>
        /// <param name="type">The type.</param>
        /// <exception cref="ArgumentException">The type did not have any public static fields with enum types.</exception>
        public static void UseType(Type type)
        {
            bool used = false;

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static)) {
                if (field.FieldType.IsEnum) {
                    used = true;

                    long declared = AddDeclaration(field.FieldType, field.Name);
                    object declaredAsEnumType = AsEnum(field.FieldType, declared);
                    field.SetValue(null, declaredAsEnumType);
                }
            }

            if (!used) {
                throw new ArgumentException($"Type {type.FullName} should have one or more static fields whose types are enums.");
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

        // Private nested so that it can have public members not visible to API.
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

        private class VirtualEnumData
        {
            public long MaxValue = long.MinValue;
            public Map<string, long> EnumValues { get; } = new();
        }
    }
}
