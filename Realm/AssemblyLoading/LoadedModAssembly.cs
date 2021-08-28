using System.Reflection;

namespace Realm.AssemblyLoading
{
    public class LoadedModAssembly
    {
        public readonly Assembly Asm;
        public readonly string AsmName;
        public readonly string Path;

        public LoadedModAssembly(Assembly asm, string asmName, string path)
        {
            Asm = asm;
            AsmName = asmName;
            Path = path;
        }
    }
}
