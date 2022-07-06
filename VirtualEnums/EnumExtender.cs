#pragma warning disable IDE0060 // Remove unused parameter
using System;
using System.Collections.Generic;
using System.Reflection;

namespace PastebinMachine.EnumExtender;

public static class EnumExtender
{
    // Added to EnumExtender originally
    public static void AddDeclaration(Type enm, string name)
    {
        VirtualEnums.VirtualEnumApi.AddDeclaration(enm, name);
    }
    public static void ExtendEnums(List<EnumValue> decls, Dictionary<Type, Type> enums, List<KeyValuePair<IReceiveEnumValue, object>> list2) { }
    public static void ExtendEnumsAgain() { }
    public static void Test() { }
    public static void PerformDMHooks() { }
    public static object ValueHook(object obj) => throw new NotImplementedException("Do not call this method.");
    public static object CallDelegate(object[] objs, Delegate del) => throw new NotImplementedException("Do not call this method.");
    public static object ReturnHook(object obj) => throw new NotImplementedException("Do not call this method.");

    // Added to EnumExtender July 7, 2022
    public static void CheckAllAssemblies() { }
    public static void CheckAssembly(Assembly asm) { }
    public static void CreateModule() { }
    public static void EnsureInit() { }
}

public class EnumValue
{
}

public interface IReceiveEnumValue
{
}
