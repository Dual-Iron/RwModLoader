using System.Reflection;

namespace Realm.AssemblyLoading;

sealed class LoadedModAssembly
{
    public readonly Assembly Asm;
    public readonly string AsmName;
    public readonly string FileName;

    public LoadedModAssembly(Assembly asm, string asmName, string fileName)
    {
        Asm = asm;
        AsmName = asmName;
        FileName = fileName;
    }
}
