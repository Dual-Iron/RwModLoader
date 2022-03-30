using Mono.Cecil;
using System.Diagnostics.CodeAnalysis;

namespace Backend.Patching;

static class ExtPatching
{
    public static TypeReference ImportTypeFromCoreLib(this ModuleDefinition module, string ns, string name)
    {
        return module.ImportReference(new TypeReference(ns, name, module, module.TypeSystem.CoreLibrary));
    }

    public static TypeReference ImportTypeFromSysCore(this ModuleDefinition module, string ns, string name)
    {
        AssemblyNameReference? asmRef = module.AssemblyReferences.FirstOrDefault(a => a.Name == "System.Core");

        if (asmRef == null) {
            asmRef = new AssemblyNameReference("System.Core", new(3, 5));
            module.AssemblyReferences.Add(asmRef);
        }

        return module.ImportReference(new TypeReference(ns, name, module, asmRef));
    }

    public static MethodReference ImportCtor(this TypeReference declaring, params TypeReference[] parameters)
    {
        return declaring.ImportMethod(false, ".ctor", declaring.Module.TypeSystem.Void, parameters);
    }

    public static MethodReference ImportMethod(this TypeReference declaring, bool isStatic, string name, TypeReference returnType, params TypeReference[] parameters)
    {
        MethodReference method = new(name, returnType, declaring) { HasThis = !isStatic };

        foreach (var param in parameters) {
            method.Parameters.Add(new(param));
        }

        return declaring.Module.ImportReference(method);
    }

    public static bool SeekTree(this TypeReference type, string fullName, [MaybeNullWhen(false)] out TypeReference accepted)
    {
        return SeekTree(type, t => t.FullName == fullName, out accepted);
    }

    public static bool SeekTree(this TypeReference type, Predicate<TypeReference> accept, [MaybeNullWhen(false)] out TypeReference accepted)
    {
        while (type != null) {
            if (accept(type)) {
                accepted = type;
                return true;
            }

            try {
                type = type.Resolve().BaseType;
            }
            catch (AssemblyResolutionException) {
                break;
            }
        }

        accepted = null;
        return false;
    }
}
