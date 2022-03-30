using Mono.Cecil;

namespace Backend.Patching;

sealed class RwMetadataResolver : MetadataResolver
{
    public RwMetadataResolver(IAssemblyResolver assemblyResolver) : base(assemblyResolver)
    {
    }

    public override TypeDefinition? Resolve(TypeReference type)
    {
        var ret = base.Resolve(type);

        // Try to resolve mscorlib references with System.Core as well.
        if (ret == null) {
            if (type.Scope.Name == "mscorlib") {
                type.Scope.Name = "System.Core";

                ret = base.Resolve(type);

                // If it fails, reset the scopename to avoid unintended side-effects.
                if (ret == null)
                    type.Scope.Name = "mscorlib";
            }
            else if (type.Scope.Name == "System.Core") {
                type.Scope.Name = "mscorlib";

                ret = base.Resolve(type);

                // If it fails, reset the scopename to avoid unintended side-effects.
                if (ret == null)
                    type.Scope.Name = "System.Core";
            }
        }

        return ret;
    }
}
