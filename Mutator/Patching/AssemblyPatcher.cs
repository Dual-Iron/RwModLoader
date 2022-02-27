using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mutator.IO;

namespace Mutator.Patching;

static class Patcher
{
    private const ushort version = 1;

    private static AssemblyDefinition GetBepAssemblyDef(string rwDir, string filePath)
    {
        DefaultAssemblyResolver resolver = new();

        resolver.AddSearchDirectory(Path.GetDirectoryName(filePath));
        resolver.AddSearchDirectory(Path.Combine(rwDir, "RainWorld_Data", "Managed"));
        resolver.AddSearchDirectory(Path.Combine(rwDir, "BepInEx", "core"));
        resolver.AddSearchDirectory(Path.Combine(rwDir, "BepInEx", "plugins"));

        return AssemblyDefinition.ReadAssembly(filePath, new() { AssemblyResolver = resolver, ReadWrite = true });
    }

    public static ExitStatus Patch(string filePath)
    {
        if (!File.Exists(filePath)) {
            return ExitStatus.FileNotFound(filePath);
        }

        if (ExtIO.RwDir.MatchFailure(out var rwDir, out var err)) {
            return err;
        }

        try {
            System.Reflection.AssemblyName.GetAssemblyName(filePath);
        }
        catch {
            return ExitStatus.Success;
        }

        try {
            return PatchSafe(filePath, rwDir);
        }
        catch (IOException e) {
            return ExitStatus.IOError($"while patching {Path.GetFileName(filePath)}: {e.Message}");
        }
    }

    private static ExitStatus PatchSafe(string filePath, string rwDir)
    {
        using var asm = GetBepAssemblyDef(rwDir, filePath);
        using var resolver = asm.MainModule.AssemblyResolver; // Ensure this gets disposed.

        if (IsPatched(asm)) {
            return ExitStatus.Success;
        }

        if (DoPatch(rwDir, asm).MatchFailure(out var modTypes, out var err2)) {
            return err2;
        }

        // Mark that it's been patched.
        AddRwmodAttribute(asm, modTypes);

        File.Copy(filePath, Path.Combine(ExtIO.BackupsFolder.FullName, Path.GetFileName(filePath)), true);

        asm.Write();

        return ExitStatus.Success;
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

        // namespace Realm { sealed class RwmodAttribute : System.Attribute {
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

    private static Result<IList<string>, ExitStatus> DoPatch(string rwDir, AssemblyDefinition asm)
    {
        string hooksAsmPath = Path.Combine(rwDir, "BepInEx", "core", "HOOKS-Assembly-CSharp.dll");

        if (!File.Exists(hooksAsmPath)) {
            return ExitStatus.FileNotFound(hooksAsmPath);
        }

        LegacyReferenceTransformer? typeScanner = null;
        AssemblyDefinition? hooksAsm = null;

        List<string> modTypes = new();

        try {
            foreach (var module in asm.Modules) {
                if (module.AssemblyReferences.Any(asmRef => asmRef.Name == "HOOKS-Assembly-CSharp")) {
                    typeScanner ??= new(hooksAsm = GetBepAssemblyDef(rwDir, hooksAsmPath));
                    typeScanner.Transform(module);
                }

                foreach (TypeDefinition type in module.GetTypes()) {
                    if (!type.IsInterface && !type.IsAbstract && !type.IsValueType) {
                        if (type.SeekTree(t => t.FullName is "BepInEx.BaseUnityPlugin" or "Partiality.Modloader.PartialityMod", out var targetType)) {
                            modTypes.Add(type.FullName);
                        }
                    }
                }

                AccessViolationPrevention.AddUnverifiableCodeAttr(module);
            }
        }
        finally {
            if (hooksAsm != null) {
                hooksAsm.Dispose();
                hooksAsm.MainModule.AssemblyResolver.Dispose();
            }
        }

        AccessViolationPrevention.SkipVerification(asm);

        return modTypes;
    }
}
