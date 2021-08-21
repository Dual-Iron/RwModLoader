using Mono.Cecil;

namespace Realm.ModLoading
{
    public sealed class ModAssembly
    {
        public ModAssembly(string asmName, string fileName, string rwmodName, ModDescriptor descriptor, AssemblyDefinition asmDef)
        {
            Descriptor = descriptor;
            AsmName = asmName;
            FileName = fileName;
            RwmodName = rwmodName;
            AsmDef = asmDef;
        }

        public readonly string AsmName;
        public readonly string FileName;
        public readonly string RwmodName;
        public readonly ModDescriptor Descriptor;

        public AssemblyDefinition AsmDef;
    }
}
