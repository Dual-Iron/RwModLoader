using System;
using System.Collections.Generic;

namespace PastebinMachine.EnumExtender
{
    public static class EnumExtender
    {
#pragma warning disable IDE0060 // Remove unused parameter
        // Used by ModLoader to ensure the reference to EnumExtender is the updated one
        public static void DoNothing() { }

        public static void AddDeclaration(Type enm, string name) => VirtualEnums.VirtualEnumApi.AddDeclaration(enm, name);
        public static void ExtendEnums(List<EnumValue> decls, Dictionary<Type, Type> enums, List<KeyValuePair<IReceiveEnumValue, object>> list2) { }
        public static void ExtendEnumsAgain() { }
        public static void Test() { }
        public static void PerformDMHooks() { }
        public static object ValueHook(object obj) => throw new NotImplementedException("That method can't be used anymore! Tell the mod authors to not use this.");
        public static object CallDelegate(object[] objs, Delegate del) => throw new NotImplementedException("That method can't be used anymore! Tell the mod authors to not use this.");
        public static object ReturnHook(object obj) => throw new NotImplementedException("That method can't be used anymore! Tell the mod authors to not use this.");
    }

    public class EnumValue
    {
    }

    public interface IReceiveEnumValue
    {
    }
}
