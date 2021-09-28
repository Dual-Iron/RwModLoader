using Mono.Cecil;
using Realm.ModLoading;

namespace Realm.AssemblyLoading;

public sealed class ModAssembly
{
    public ModAssembly(RwmodFile rwmod, int entryIndex, ModDescriptor descriptor, AssemblyDefinition asmDef)
    {
        Descriptor = descriptor;
        Rwmod = rwmod;
        EntryIndex = entryIndex;
        AsmDef = asmDef;
    }

    public readonly RwmodFile Rwmod;
    public readonly int EntryIndex;
    public readonly ModDescriptor Descriptor;
    public AssemblyDefinition AsmDef;

    public string FileName => Rwmod.Entries[EntryIndex].FileName;
    public string OriginalAssemblyName => AsmDef.Name.Name.Split(new[] { AssemblyPool.IterationSeparator }, 0)[0];
}
