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
        private static AssemblyDefinition GetBepAssemblyDef(string filePath, bool write)
        {
            DefaultAssemblyResolver resolver = new();

            resolver.AddSearchDirectory(Path.GetDirectoryName(filePath));
            resolver.AddSearchDirectory(Path.Combine(RwDir, "RainWorld_Data", "Managed"));
            resolver.AddSearchDirectory(Path.Combine(RwDir, "BepInEx", "core"));
            resolver.AddSearchDirectory(Path.Combine(RwDir, "BepInEx", "plugins"));

            return AssemblyDefinition.ReadAssembly(filePath, new() { AssemblyResolver = resolver, MetadataResolver = new RwMetadataResolver(resolver), ReadWrite = write });
        }

        public static async Task Patch(string? rwmodName, string filePath, bool shouldUpdate)
        {
            if (!File.Exists(filePath)) {
                throw new("Assembly file does not exist.");
            }

            try {
                System.Reflection.AssemblyName.GetAssemblyName(filePath);
            } catch {
                return;
            }

            using (AssemblyDefinition asm = GetBepAssemblyDef(filePath, true)) {
                using var resolver = asm.MainModule.AssemblyResolver;
                
                string? rwmodNameAttribute = GetRwmodName(asm);

                rwmodName ??= rwmodNameAttribute ?? asm.Name.Name;

                if (rwmodNameAttribute == null) {
                    // Patch the fresh assembly.
                    DoPatch(asm);
                } else if (rwmodNameAttribute != rwmodName || !File.Exists(GetModPath(rwmodNameAttribute))) {
                    // Remove existing attributes if they exist.
                    for (int i = asm.CustomAttributes.Count - 1; i >= 0; i--)
                        if (asm.CustomAttributes[i].AttributeType.Name == "RwmodAttribute")
                            asm.CustomAttributes.RemoveAt(i);
                } else return;

                AddRwmodAttribute(asm, rwmodName);

                if (rwmodNameAttribute == null) {
                    File.Copy(filePath, Path.Combine(PatchBackupsFolder.FullName, Path.GetFileName(filePath)), true);
                }

                asm.Write();
            }

            if (shouldUpdate) {
                await Packaging.Packager.Update(rwmodName, filePath, false);
            }
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
            string hooksAsmPath = Path.Combine(RwDir, "BepInEx", "core", "HOOKS-Assembly-CSharp.dll");

            if (!File.Exists(hooksAsmPath)) {
                // TODO LOW: cringe hardcoding filepaths
                throw new("The file `HOOKS-Assembly-CSharp.dll` does not exist in `Rain World/BepInEx/core`.");
            }

            LegacyReferenceTransformer? typeScanner = null;
            AssemblyDefinition? hooksAsm = null;

            try {
                foreach (var module in asm.Modules) {
                    if (module.AssemblyReferences.Any(asmRef => asmRef.Name == "HOOKS-Assembly-CSharp")) {
                        typeScanner ??= new(hooksAsm = GetBepAssemblyDef(hooksAsmPath, false));
                        typeScanner.Transform(module);
                    }

                    HotReloadPatcher.Patch(module);

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
}
