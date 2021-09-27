using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mutator.Patching;

public static partial class AssemblyPatcher
{
    private const ushort version = 0;

    private static AssemblyDefinition GetBepAssemblyDef(string filePath, bool write)
    {
        DefaultAssemblyResolver resolver = new();

        resolver.AddSearchDirectory(Path.GetDirectoryName(filePath));
        resolver.AddSearchDirectory(Path.Combine(RwDir, "RainWorld_Data", "Managed"));
        resolver.AddSearchDirectory(Path.Combine(RwDir, "BepInEx", "core"));
        resolver.AddSearchDirectory(Path.Combine(RwDir, "BepInEx", "plugins"));

        return AssemblyDefinition.ReadAssembly(filePath, new() { AssemblyResolver = resolver, MetadataResolver = new RwMetadataResolver(resolver), ReadWrite = write });
    }

    public static void Patch(string filePath)
    {
        if (!File.Exists(filePath)) {
            throw ErrFileNotFound(filePath);
        }

        try {
            System.Reflection.AssemblyName.GetAssemblyName(filePath);
        } catch {
            return;
        }

        using AssemblyDefinition asm = GetBepAssemblyDef(filePath, true);
        using IAssemblyResolver resolver = asm.MainModule.AssemblyResolver; // Ensure this gets disposed.

        if (IsPatched(asm)) {
            return;
        }

        // Patch the fresh assembly.
        DoPatch(asm, out string modType);
        AddRwmodAttribute(asm, modType);

        File.Copy(filePath, Path.Combine(PatchBackupsFolder.FullName, Path.GetFileName(filePath)), true);

        asm.Write();
    }

    private static bool IsPatched(AssemblyDefinition asm)
    {
        return asm.CustomAttributes.Any(attr =>
                attr.AttributeType.Name == "RwmodAttribute" && 
                attr.ConstructorArguments.Count == 2 &&
                attr.ConstructorArguments[0].Value is ushort v && v == version &&
                attr.ConstructorArguments[1].Value is string
            );
    }

    private static void AddRwmodAttribute(AssemblyDefinition asm, string modType)
    {
        // If a valid attribute type already exists, remove it.
        if (asm.MainModule.GetType(asm.Name.Name + "+Realm", "RwmodAttribute") is TypeDefinition rwmodAttributeType) {
            asm.MainModule.Types.Remove(rwmodAttributeType);
        }

        TypeReference attrReference = asm.MainModule.ImportTypeFromCoreLib("System", "Attribute");

        // namespace Realm { public sealed class RwmodAttribute : System.Attribute {
        rwmodAttributeType = new(
            asm.Name.Name + "+Realm", "RwmodAttribute",
            TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit | TypeAttributes.AnsiClass,
            attrReference
            );

        asm.MainModule.Types.Add(rwmodAttributeType);

        // public RwmodAttribute(string) : base() {}
        MethodDefinition ctor = new(
            ".ctor",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName,
            asm.MainModule.TypeSystem.Void
            );

        ctor.Parameters.Add(new("version", default, asm.MainModule.TypeSystem.UInt16));
        ctor.Parameters.Add(new("modType", default, asm.MainModule.TypeSystem.String));

        ctor.Body = new(ctor);

        ILProcessor il = ctor.Body.GetILProcessor();

        il.Append(Instruction.Create(OpCodes.Ldarg_0));
        il.Append(Instruction.Create(OpCodes.Call, attrReference.ImportCtor()));
        il.Append(Instruction.Create(OpCodes.Ret));

        rwmodAttributeType.Methods.Add(ctor);

        // }}

        // Add attribute itself to assembly
        CustomAttribute assemblyAttribute = new(ctor);
        assemblyAttribute.ConstructorArguments.Add(new(asm.MainModule.TypeSystem.UInt16, version));
        assemblyAttribute.ConstructorArguments.Add(new(asm.MainModule.TypeSystem.String, modType));
        asm.CustomAttributes.Add(assemblyAttribute);
    }

    private static void DoPatch(AssemblyDefinition asm, out string modType)
    {
        modType = "";

        string hooksAsmPath = Path.Combine(RwDir, "BepInEx", "core", "HOOKS-Assembly-CSharp.dll");

        if (!File.Exists(hooksAsmPath)) {
            throw ErrFileNotFound(hooksAsmPath);
        }

        LegacyReferenceTransformer? typeScanner = null;
        AssemblyDefinition? hooksAsm = null;

        try {
            foreach (var module in asm.Modules) {
                if (module.AssemblyReferences.Any(asmRef => asmRef.Name == "HOOKS-Assembly-CSharp")) {
                    typeScanner ??= new(hooksAsm = GetBepAssemblyDef(hooksAsmPath, false));
                    typeScanner.Transform(module);
                }

                HotReloadPatcher.Patch(module, ref modType);

                AccessViolationPrevention.AddUnverifiableCodeAttribute(module);
            }
        } finally {
            if (hooksAsm != null) {
                hooksAsm.Dispose();
                hooksAsm.MainModule.AssemblyResolver.Dispose();
            }
        }

        AccessViolationPrevention.IgnoreAccessChecksAndSkipVerification(asm);
    }
}
