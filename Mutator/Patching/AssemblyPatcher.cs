using Mono.Cecil;
using Mono.Cecil.Cil;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Mutator.InstallerApi;

namespace Mutator.Patching
{
    public static partial class AssemblyPatcher
    {
        private static AssemblyDefinition GetBepAssemblyDef(string filePath)
        {
            DefaultAssemblyResolver resolver = new();

            resolver.AddSearchDirectory(Path.Combine(RwDir, "RainWorld_Data", "Managed"));
            resolver.AddSearchDirectory(Path.Combine(RwDir, "BepInEx", "core"));
            resolver.AddSearchDirectory(Path.Combine(RwDir, "BepInEx", "plugins"));
            resolver.AddSearchDirectory(Path.Combine(RwDir, "BepInEx", "rw"));

            return AssemblyDefinition.ReadAssembly(filePath, new() { AssemblyResolver = resolver, MetadataResolver = new RwMetadataResolver(resolver) });
        }

        public static async Task Patch(string filePath, bool shouldUpdate)
        {
            if (!File.Exists(filePath)) {
                throw new("Assembly file does not exist.");
            }

            using AssemblyDefinition asm = GetBepAssemblyDef(filePath);

            string? rwmodName = GetRwmodName(asm);
            bool backUp = false;

            string effectiveRwmodName = rwmodName ?? asm.Name.Name;

            if (rwmodName == null) {
                backUp = true;

                // Patch the fresh assembly.
                DoPatch(asm);
            } else if (!File.Exists(GetModPath(effectiveRwmodName))) {
                // Remove existing attributes if they exist.
                for (int i = asm.CustomAttributes.Count - 1; i >= 0; i--)
                    if (asm.CustomAttributes[i].AttributeType.Name == "RwmodAttribute")
                        asm.CustomAttributes.RemoveAt(i);
            } else return;

            AddRwmodAttribute(asm, effectiveRwmodName);

            using MemoryStream ms = new();
            asm.Write(ms);
            asm.Dispose();

            if (backUp) {
                File.Move(filePath, Path.Combine(PatchBackupsFolder.FullName, Path.GetFileName(filePath)), true);
            }

            await File.WriteAllBytesAsync(filePath, ms.ToArray());

            if (shouldUpdate)
                await Packaging.Packager.Update(effectiveRwmodName, filePath);
        }

        private static string? GetRwmodName(AssemblyDefinition asm)
        {
            foreach (var customAttribute in asm.CustomAttributes)
                if (customAttribute.AttributeType.Name == "RwmodAttribute" && customAttribute.ConstructorArguments.Count == 1 && customAttribute.ConstructorArguments[0].Value is string name) {
                    return name;
                }
            return null;
        }

        private static void AddRwmodAttribute(AssemblyDefinition asm, string rwmodName)
        {
            // If a valid attribute type already exists, use that and don't make another.
            if (asm.MainModule.GetType(asm.Name.Name + "+Realm.RwmodAttribute") is TypeDefinition rwmodAttributeType) {
                var existentCtor = rwmodAttributeType.Methods.FirstOrDefault(m => m.Name == ".ctor" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.String");
                if (existentCtor != null) {
                    // Add attribute itself to assembly
                    CustomAttribute assemblyAttributeReplacement = new(existentCtor);
                    assemblyAttributeReplacement.ConstructorArguments.Add(new(asm.MainModule.TypeSystem.String, rwmodName));
                    asm.CustomAttributes.Add(assemblyAttributeReplacement);
                    return;
                }

                // Invalid RwmodAttribute type, just discard it and redo.
                asm.MainModule.Types.Remove(rwmodAttributeType);
            }

            TypeReference attrReference = asm.MainModule.ImportTypeFromCoreLib("System", "Attribute");

            // namespace Realm { public sealed class RwmodAttribute : System.Attribute {
            rwmodAttributeType = new(
                rwmodName + "+Realm", "RwmodAttribute",
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

            ctor.Parameters.Add(new(asm.MainModule.TypeSystem.String));

            ctor.Body = new(ctor);

            ILProcessor il = ctor.Body.GetILProcessor();

            il.Append(Instruction.Create(OpCodes.Ldarg_0));
            il.Append(Instruction.Create(OpCodes.Call, attrReference.ImportCtor()));
            il.Append(Instruction.Create(OpCodes.Ret));

            rwmodAttributeType.Methods.Add(ctor);

            // }}

            // Add attribute itself to assembly
            CustomAttribute assemblyAttribute = new(ctor);
            assemblyAttribute.ConstructorArguments.Add(new(asm.MainModule.TypeSystem.String, rwmodName));
            asm.CustomAttributes.Add(assemblyAttribute);
        }

        private static void DoPatch(AssemblyDefinition asm)
        {
            string hooksAsmPath = Path.Combine(RwDir, "BepInEx", "rw", "HOOKS-Assembly-CSharp.dll");

            if (!File.Exists(hooksAsmPath)) {
                throw new("The file `HOOKS-Assembly-CSharp.dll` does not exist in `Rain World/BepInEx/rw`.");
            }

            LegacyReferenceTransformer? typeScanner = null;

            try {
                foreach (var module in asm.Modules) {
                    if (module.AssemblyReferences.Any(asmRef => asmRef.Name == "HOOKS-Assembly-CSharp")) {
                        typeScanner ??= new(GetBepAssemblyDef(hooksAsmPath));
                        typeScanner.Transform(module);
                    }

                    HotReloadPatcher.Patch(module);

                    AccessViolationPrevention.AddUnverifiableCodeAttribute(module);
                }
            } finally { typeScanner?.Dispose(); }

            AccessViolationPrevention.IgnoreAccessChecksAndSkipVerification(asm);
        }
    }
}
