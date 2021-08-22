using System.Reflection;

namespace Realm.AssemblyLoading
{
    public abstract partial class ModDescriptor
    {
        public sealed class Lib : ModDescriptor
        {
            public override bool IsPartiality => false;

            public override void Initialize(Assembly assembly) { }
            public override void Unload() { }
        }
    }
}
