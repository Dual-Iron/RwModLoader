using Mono.Cecil;
using Realm.ModLoading;

namespace Realm.AssemblyLoading;

public sealed class ModAssembly
{
    public ModAssembly(RwmodFile rwmod, int entryIndex, AssemblyDescriptor descriptor, AssemblyDefinition asmDef)
    {
        Descriptor = descriptor;
        Rwmod = rwmod;
        EntryIndex = entryIndex;
        AsmDef = asmDef;
    }

    public readonly RwmodFile Rwmod;
    public readonly int EntryIndex;
    public readonly AssemblyDescriptor Descriptor;
    public AssemblyDefinition AsmDef;

    public string FileName => Rwmod.Entries[EntryIndex].FileName;
    public string OriginalAssemblyName => AsmDef.Name.Name.Substring(0, AsmDef.Name.Name.IndexOf(AssemblyPool.IterationSeparator));
}
