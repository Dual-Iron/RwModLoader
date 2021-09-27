using Mono.Cecil;

namespace Realm.AssemblyLoading;

public sealed class ModAssembly
{
    public ModAssembly(string rwmod, string fileName, ModDescriptor descriptor, AssemblyDefinition asmDef)
    {
        Descriptor = descriptor;
        Rwmod = rwmod;
        FileName = fileName;
        AsmDef = asmDef;
    }

    public readonly string Rwmod;
    public readonly string FileName;
    public readonly ModDescriptor Descriptor;
    public AssemblyDefinition AsmDef;
}
