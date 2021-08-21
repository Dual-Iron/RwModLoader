using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mutator.Patching
{
    public static class AccessViolationPrevention
    {
        public static void IgnoreAccessChecksAndSkipVerification(AssemblyDefinition asm)
        {
            ModuleDefinition selfModule = asm.MainModule;

            // Declare System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute and apply AttributeUsageAttribute to it
            TypeDefinition IactAttribute;
            {
                IactAttribute = new(
                    "System.Runtime.CompilerServices",
                    "IgnoresAccessChecksToAttribute",
                    TypeAttributes.Public | TypeAttributes.BeforeFieldInit,
                    selfModule.ImportTypeFromCoreLib("System", "Attribute")
                    );

                MethodReference attributeUsageAttributeCtor = selfModule.ImportTypeFromCoreLib("System", "AttributeUsageAttribute").ImportCtor();
                attributeUsageAttributeCtor.Parameters.Add(new(selfModule.ImportTypeFromCoreLib("System", "AttributeTargets")));

                IactAttribute.CustomAttributes.Add(new(
                    attributeUsageAttributeCtor,
                    new byte[] { 1, 0, 1, 0, 0, 0, 1, 0, 84, 2, 13, 65, 108, 108, 111, 119, 77, 117, 108, 116, 105, 112, 108, 101, 1 }
                    ));

                selfModule.Types.Add(IactAttribute);
            }

            // Backing field for AssemblyName
            FieldDefinition backingField;
            {
                backingField = new("<AssemblyName>k__BackingField", FieldAttributes.Private | FieldAttributes.InitOnly, selfModule.TypeSystem.String);
                backingField.CustomAttributes.Add(new(selfModule.ImportTypeFromCoreLib("System.Runtime.CompilerServices", "CompilerGeneratedAttribute").ImportCtor()));
                IactAttribute.Fields.Add(backingField);
            }

            //  public IgnoresAccessChecksToAttribute(string assemblyName) : base() {
            //      this.AssemblyName = assemblyName;
            //  }
            {
                MethodDefinition ctor;

                ctor = new(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, selfModule.TypeSystem.Void);

                ctor.Parameters.Add(new("assemblyName", ParameterAttributes.None, selfModule.TypeSystem.String));

                ILProcessor il = ctor.Body.GetILProcessor();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, selfModule.ImportTypeFromCoreLib("System", "Attribute").ImportCtor());
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Stfld, backingField);
                il.Emit(OpCodes.Ret);

                IactAttribute.Methods.Add(ctor);

                asm.CustomAttributes.Add(new(ctor, new byte[] { 1, 0, 15, 65, 115, 115, 101, 109, 98, 108, 121, 45, 67, 83, 104, 97, 114, 112, 0, 0 }));
            }

            // public string AssemblyName { get; }
            MethodDefinition get_AssemblyName;
            {
                get_AssemblyName = new("get_AssemblyName", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, selfModule.TypeSystem.String);

                ILProcessor il = get_AssemblyName.Body.GetILProcessor();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, backingField);
                il.Emit(OpCodes.Ret);

                get_AssemblyName.CustomAttributes.Add(new(selfModule.ImportTypeFromCoreLib("System.Runtime.CompilerServices", "CompilerGeneratedAttribute").ImportCtor()));

                IactAttribute.Methods.Add(get_AssemblyName);
            }

            // .property
            {
                PropertyDefinition AssemblyName = new("AssemblyName", PropertyAttributes.None, selfModule.TypeSystem.String) { GetMethod = get_AssemblyName };
                IactAttribute.Properties.Add(AssemblyName);
            }

            // [assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
            {
                var attr = new SecurityAttribute(selfModule.ImportTypeFromCoreLib("System.Security.Permissions", "SecurityPermissionAttribute"));
                attr.Properties.Add(new("SkipVerification", new(selfModule.TypeSystem.Boolean, true)));

                var dec = new SecurityDeclaration(SecurityAction.RequestMinimum);
                dec.SecurityAttributes.Add(attr);
                asm.SecurityDeclarations.Add(dec);
            }
        }

        public static void AddUnverifiableCodeAttribute(ModuleDefinition module)
        {
            // [module: UnverifiableCodeAttribute]
            module.CustomAttributes.Add(new(module.ImportTypeFromCoreLib("System.Security", "UnverifiableCodeAttribute").ImportCtor()));
        }
    }
}
