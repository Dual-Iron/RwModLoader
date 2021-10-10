using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Mutator.Patching;

public static partial class AssemblyPatcher
{
    private const ushort version = 1;

    private static AssemblyDefinition GetBepAssemblyDef(string filePath)
    {
        DefaultAssemblyResolver resolver = new();

        resolver.AddSearchDirectory(Path.GetDirectoryName(filePath));
        resolver.AddSearchDirectory(Path.Combine(RwDir, "RainWorld_Data", "Managed"));
        resolver.AddSearchDirectory(Path.Combine(RwDir, "BepInEx", "core"));
        resolver.AddSearchDirectory(Path.Combine(RwDir, "BepInEx", "plugins"));

        return AssemblyDefinition.ReadAssembly(filePath, new() { AssemblyResolver = resolver, ReadWrite = true });
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

        using var asm = GetBepAssemblyDef(filePath);
        using var resolver = asm.MainModule.AssemblyResolver; // Ensure this gets disposed.

        if (IsPatched(asm)) {
            return;
        }

        // Patch the fresh assembly.
        DoPatch(asm, out var modType);

        // Mark that it's been patched.
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
                attr.ConstructorArguments[1].Value is CustomAttributeArgument[]
            );
    }

    private static void AddRwmodAttribute(AssemblyDefinition asm, IList<string> modTypes)
    {
        // If a valid attribute type already exists, remove it.
        if (asm.MainModule.GetType(asm.Name.Name + "+Realm", "RwmodAttribute") is TypeDefinition rwmodAttributeType) {
            asm.MainModule.Types.Remove(rwmodAttributeType);

            // Also remove all of the attribute declarations on its assembly.
            for (int i = asm.CustomAttributes.Count - 1; i >= 0; i--) {
                if (asm.CustomAttributes[i].AttributeType.Name == "RwmodAttribute") {
                    asm.CustomAttributes.RemoveAt(i);
                }
            }
        }

        TypeReference attrReference = asm.MainModule.ImportTypeFromCoreLib("System", "Attribute");

        // namespace Realm { public sealed class RwmodAttribute : System.Attribute {
        rwmodAttributeType = new(
            asm.Name.Name + "+Realm", "RwmodAttribute",
            TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit | TypeAttributes.AnsiClass,
            attrReference
            );

        asm.MainModule.Types.Add(rwmodAttributeType);

        ArrayType stringArr = asm.MainModule.TypeSystem.String.MakeArrayType();

        // public RwmodAttribute(string) : base() {}
        MethodDefinition ctor = new(
            ".ctor",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName,
            asm.MainModule.TypeSystem.Void
            );

        ctor.Parameters.Add(new("version", default, asm.MainModule.TypeSystem.UInt16));
        ctor.Parameters.Add(new("modTypes", default, stringArr));

        ctor.Body = new(ctor);

        ILProcessor il = ctor.Body.GetILProcessor();

        il.Append(Instruction.Create(OpCodes.Ldarg_0));
        il.Append(Instruction.Create(OpCodes.Call, attrReference.ImportCtor()));
        il.Append(Instruction.Create(OpCodes.Ret));

        rwmodAttributeType.Methods.Add(ctor);

        // }}

        // Add attribute to assembly
        CustomAttributeArgument[] modTypesArr = new CustomAttributeArgument[modTypes.Count];

        for (int i = 0; i < modTypesArr.Length; i++) {
            modTypesArr[i] = new(asm.MainModule.TypeSystem.String, modTypes[i]);
        }

        CustomAttribute assemblyAttribute = new(ctor);
        assemblyAttribute.ConstructorArguments.Add(new(asm.MainModule.TypeSystem.UInt16, version));
        assemblyAttribute.ConstructorArguments.Add(new(stringArr, modTypesArr));
        asm.CustomAttributes.Add(assemblyAttribute);
    }

    private static void DoPatch(AssemblyDefinition asm, out IList<string> modTypes)
    {
        string hooksAsmPath = Path.Combine(RwDir, "BepInEx", "core", "HOOKS-Assembly-CSharp.dll");

        if (!File.Exists(hooksAsmPath)) {
            throw ErrFileNotFound(hooksAsmPath);
        }

        LegacyReferenceTransformer? typeScanner = null;
        AssemblyDefinition? hooksAsm = null;

        List<string> modTypesList = new();

        try {
            foreach (var module in asm.Modules) {
                if (module.AssemblyReferences.Any(asmRef => asmRef.Name == "HOOKS-Assembly-CSharp")) {
                    typeScanner ??= new(hooksAsm = GetBepAssemblyDef(hooksAsmPath));
                    typeScanner.Transform(module);
                }

                foreach (TypeDefinition type in module.Types) {
                    if (!type.IsInterface && !type.IsAbstract && !type.IsValueType) {
                        if (type.SeekTree(t => t.FullName is "BepInEx.BaseUnityPlugin" or "Partiality.Modloader.PartialityMod", out var targetType)) {
                            modTypesList.Add(type.FullName);
                        }
                    }
                }

                AccessViolationPrevention.AddUnverifiableCodeAttribute(module);
            }
        } finally {
            if (hooksAsm != null) {
                hooksAsm.Dispose();
                hooksAsm.MainModule.AssemblyResolver.Dispose();
            }
        }

        AccessViolationPrevention.IgnoreAccessChecksAndSkipVerification(asm);

        modTypes = modTypesList.ToArray();
    }
}
