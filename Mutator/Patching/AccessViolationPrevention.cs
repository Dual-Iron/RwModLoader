using Mono.Cecil;

namespace Mutator.Patching;

static class AccessViolationPrevention
{
    public static void SkipVerification(AssemblyDefinition asm)
    {
        // [assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
        var attr = new SecurityAttribute(asm.MainModule.ImportTypeFromCoreLib("System.Security.Permissions", "SecurityPermissionAttribute"));
        attr.Properties.Add(new("SkipVerification", new(asm.MainModule.TypeSystem.Boolean, true)));

        var dec = new SecurityDeclaration(SecurityAction.RequestMinimum);
        dec.SecurityAttributes.Add(attr);
        asm.SecurityDeclarations.Add(dec);
    }

    public static void AddUnverifiableCodeAttr(ModuleDefinition module)
    {
        // [module: UnverifiableCodeAttribute]
        module.CustomAttributes.Add(new(module.ImportTypeFromCoreLib("System.Security", "UnverifiableCodeAttribute").ImportCtor()));
    }
}
