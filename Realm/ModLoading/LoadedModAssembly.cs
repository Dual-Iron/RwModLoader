using System.Reflection;

namespace Realm.ModLoading
{
    public class LoadedModAssembly
    {
        public readonly Assembly Asm;
        public readonly string AsmName;

        public LoadedModAssembly(Assembly asm, string asmName)
        {
            Asm = asm;
            AsmName = asmName;
        }
    }
}
