using Mono.Cecil;
using Realm.ModLoading;

namespace Realm.AssemblyLoading;

sealed class ModAssembly
{
    public ModAssembly(RwmodFile rwmod, RwmodFileEntry entry, AssemblyDescriptor descriptor, AssemblyDefinition asmDef)
    {
        Descriptor = descriptor;
        Rwmod = rwmod;
        Entry = entry;
        AsmDef = asmDef;
    }

    public readonly RwmodFile Rwmod;
    public readonly RwmodFileEntry Entry;
    public readonly AssemblyDescriptor Descriptor;
    public AssemblyDefinition AsmDef;

    public string FileName => Entry.Name;
    public string OriginalAssemblyName => AsmDef.Name.Name.Substring(0, AsmDef.Name.Name.IndexOf(AssemblyPool.IterationSeparator));
}
