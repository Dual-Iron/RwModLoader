using Mono.Cecil;
namespace Realm.AssemblyLoading
{
    public sealed class ModAssembly
    {
        public ModAssembly(string asmName, string path, string rwmodName, ModDescriptor descriptor, AssemblyDefinition asmDef)
        {
            Descriptor = descriptor;
            AsmName = asmName;
            Path = path;
            RwmodName = rwmodName;
            AsmDef = asmDef;
        }

        public readonly string AsmName;
        public readonly string Path;
        public readonly string RwmodName;
        public readonly ModDescriptor Descriptor;

        public AssemblyDefinition AsmDef;
    }
}
