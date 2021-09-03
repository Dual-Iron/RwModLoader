using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Linq;
using static Mono.Cecil.Cil.Instruction;

namespace Mutator.Patching
{
    public static class HotReloadPatcher
    {
        public static void Patch(ModuleDefinition module)
        {
            foreach (TypeDefinition type in module.Types) {
                if (type.IsInterface || type.IsAbstract) {
                    continue;
                }

                if (type.SeekTree(t => t.FullName is "BepInEx.BaseUnityPlugin" or "Partiality.Modloader.PartialityMod", out var targetType)) {
                    if (targetType.FullName == "BepInEx.BaseUnityPlugin") {
                        PatchModType(type, true);
                    } else if (targetType.FullName == "Partiality.Modloader.PartialityMod") {
                        PatchModType(type, false);
                    }
                    return;
                }
            }
        }

        private static void PatchModType(TypeDefinition modType, bool bepInExMod)
        {
            const string disable = "OnDisable";

            MethodDefinition? unload = modType.Methods.FirstOrDefault(m => m.Name == disable && m.Parameters.Count == 0);

            if (unload == null) {
                unload = new(disable, MethodAttributes.Public | MethodAttributes.HideBySig | (bepInExMod ? 0 : MethodAttributes.Virtual), modType.Module.TypeSystem.Void);
                unload.Body = new(unload);
                unload.Body.Instructions.Add(Create(OpCodes.Ret));

                modType.Methods.Add(unload);
            }

            unload.Body.Instructions.RemoveAt(unload.Body.Instructions.Count - 1);

            ExpandUnloadMethod(unload);

            unload.Body.Instructions.Add(Create(OpCodes.Ret));
        }

        private static void ExpandUnloadMethod(MethodDefinition unload)
        {
            for (int i = unload.Module.Types.Count - 1; i >= 0; i--) {
                TypeDefinition type = unload.Module.Types[i];

                // Ignore user-forbidden code, it can handle itself.
                if (!type.Name.Contains('<')) {
                    var cctor = type.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic);
                    if (cctor != null || type.Fields.Any(f => f.IsStatic)) {
                        if (type.HasGenericParameters) {
                            GenericUnload(unload, type, cctor);
                        } else {
                            GenerateUnloadForType(unload, unload, type, type);
                        }
                    }
                }
            }
        }

        private static void GenericUnload(MethodDefinition unload, TypeDefinition type, MethodDefinition? cctor)
        {
            GenericInstanceType typeGeneric = new(type);

            foreach (GenericParameter genericParam in type.GenericParameters) {
                typeGeneric.GenericArguments.Add(genericParam);
            }

            MethodDefinition localUnload = new("<Unload>", MethodAttributes.Assembly | MethodAttributes.Static, unload.Module.TypeSystem.Void);
            localUnload.Body = new(localUnload);
            GenerateUnloadForType(unload, localUnload, type, typeGeneric);
            localUnload.Body.Instructions.Add(Create(OpCodes.Ret));
            type.Methods.Add(localUnload);

            if (cctor == null) {
                cctor = new(
                    ".cctor",
                    MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Static,
                    unload.Module.TypeSystem.Void
                    );
                cctor.Body = new(cctor);
                cctor.Body.Instructions.Add(Create(OpCodes.Ret));

                type.Methods.Add(cctor);
            }

            MethodReference genericLocalUnloadRef = new(localUnload.Name, localUnload.ReturnType) {
                DeclaringType = typeGeneric,
                HasThis = localUnload.HasThis,
                ExplicitThis = localUnload.ExplicitThis,
                CallingConvention = localUnload.CallingConvention,
            };

            ILProcessor il = cctor.Body.GetILProcessor();

            FieldDefinition unloadDelegate = GetUnloadDelegateField(unload);
            TypeReference actionRef = GetActionTypeRef(unload.Module);
            MethodReference actionCtor = actionRef.ImportCtor(
                unload.Module.ImportTypeFromCoreLib("System", "Object"),
                unload.Module.ImportTypeFromCoreLib("System", "IntPtr")
                );
            TypeReference delegateRef = unload.Module.ImportTypeFromCoreLib("System", "Delegate");
            MethodReference combine = delegateRef.ImportMethod(true, "Combine", delegateRef, delegateRef, delegateRef);

            // Remove ret
            il.RemoveAt(il.Body.Instructions.Count - 1);

            // <unload> = (Action)Delegate.Combine(<unload>, new Action(<Unload>));
            il.Emit(OpCodes.Ldsfld, unloadDelegate);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ldftn, genericLocalUnloadRef);
            il.Emit(OpCodes.Newobj, actionCtor);
            il.Emit(OpCodes.Call, combine);
            il.Emit(OpCodes.Castclass, actionRef);
            il.Emit(OpCodes.Stsfld, unloadDelegate);

            // Re-add ret
            il.Emit(OpCodes.Ret);
        }

        private static void GenerateUnloadForType(MethodDefinition unload, MethodDefinition localUnload, TypeDefinition type, TypeReference arguedTypeRef)
        {
            if (type.IsInterface) {
                return;
            }

            ILProcessor il = localUnload.Body.GetILProcessor();

            // Unload mono behaviours aggressively
            if (unload.DeclaringType != type && type.SeekTree("UnityEngine.MonoBehaviour", out var baseType)) {
                BasicILCursor ilCursor = new(localUnload.Body);

                TypeReference objectType = new("UnityEngine", "Object", unload.Module, baseType.Scope);

                MethodReference findObjectOfType = new("FindObjectOfType", type, objectType);
                GenericParameter genericParam = new(findObjectOfType);
                findObjectOfType.GenericParameters.Add(genericParam);
                findObjectOfType.ReturnType = genericParam;

                GenericInstanceMethod findObjectOfTypeGeneric = new(findObjectOfType);
                findObjectOfTypeGeneric.GenericArguments.Add(arguedTypeRef);

                MethodReference destroyImmediate = new("DestroyImmediate", unload.Module.TypeSystem.Void, objectType);
                destroyImmediate.Parameters.Add(new(objectType));

                ilCursor.Emit(
                    Create(OpCodes.Call, unload.Module.ImportReference(findObjectOfTypeGeneric)),
                    Create(OpCodes.Call, unload.Module.ImportReference(destroyImmediate))
                );
            }

            // Unload managed fields
            foreach (FieldDefinition field in type.Fields) {
                // Don't try to set constants or instance fields.
                if (field.Constant != null || !field.IsStatic) {
                    continue;
                }

                FieldReference fRef = new(field.Name, field.FieldType, arguedTypeRef);

                // Prepare jump beforehand.
                Instruction jmp = field.FieldType.IsValueType 
                    ? Create(OpCodes.Ldsflda, fRef) 
                    : Create(OpCodes.Ldnull);

                // Call IDisposable.Dispose() on any fields that are disposable.
                if (field.FieldType.SeekTree(t => t.Resolve().Interfaces.Any(n => n.InterfaceType.FullName == "System.IDisposable"), out var disposableImplementor)) {
                    il.Emit(OpCodes.Ldsfld, fRef);

                    // If it's a reference type, make sure the reference isn't null.
                    if (!disposableImplementor.IsValueType) {
                        il.Emit(OpCodes.Brfalse, jmp);
                        il.Emit(OpCodes.Ldsfld, fRef);
                    }
                    // Otherwise, it's a value type, so it must be boxed to call Dispose on the existing copy.
                    else il.Emit(OpCodes.Box, unload.Module.ImportReference(disposableImplementor));

                    TypeReference disposable = unload.Module.ImportTypeFromCoreLib("System", "IDisposable");
                    MethodReference dispose = new("Dispose", unload.Module.TypeSystem.Void, disposable) { 
                        HasThis = true
                    };

                    il.Emit(OpCodes.Callvirt, unload.Module.ImportReference(dispose));
                }

                if (field.FieldType.IsValueType) {
                    il.Append(jmp);
                    il.Emit(OpCodes.Initobj, field.FieldType);
                } else {
                    il.Append(jmp);
                    il.Emit(OpCodes.Stsfld, fRef);
                }
            }

            // Unload any potentially leaking gameobjects
            foreach (MethodDefinition method in type.Methods) {
                // Unload any fresh instances of gameobjects
                if (method.HasBody) {
                    for (int i = method.Body.Instructions.Count - 1; i >= 0; i--) {
                        CheckForUnityObjects(unload, method, i);
                    }
                }
            }
        }

        private static void CheckForUnityObjects(MethodDefinition unload, MethodDefinition method, int i)
        {
            Instruction instr = method.Body.Instructions[i];

            if (instr.OpCode.Code == Code.Newobj && instr.Operand is MethodReference ctor && ctor.DeclaringType.SeekTree("UnityEngine.Object", out var initializingObject)) {
                FieldDefinition unloadDelegate = GetUnloadDelegateField(unload);
                TypeDefinition unloadUnityObjectsType = GetUnloadUnityObjectsType(unload);
                TypeReference actionRef = GetActionTypeRef(unload.Module);
                MethodReference actionCtor = actionRef.ImportCtor(
                    unload.Module.ImportTypeFromCoreLib("System", "Object"),
                    unload.Module.ImportTypeFromCoreLib("System", "IntPtr")
                    );
                TypeReference delegateRef = unload.Module.ImportTypeFromCoreLib("System", "Delegate");
                MethodReference combine = delegateRef.ImportMethod(true, "Combine", delegateRef, delegateRef, delegateRef);

                BasicILCursor ilCursor = new(method.Body) { Index = i + 1 };

                // <unload> = (Action)Delegate.Combine(new Action(new <UnloadUnityObjects>(gameObject).Run), <unload>);
                ilCursor.Emit(
                    Create(OpCodes.Dup),
                    Create(OpCodes.Newobj, unloadUnityObjectsType.Methods.FirstOrDefault(m => m.IsConstructor)),
                    Create(OpCodes.Ldftn, unloadUnityObjectsType.Methods.FirstOrDefault(m => m.Name == "Run")),
                    Create(OpCodes.Newobj, actionCtor),
                    Create(OpCodes.Ldsfld, unloadDelegate),
                    Create(OpCodes.Call, combine),
                    Create(OpCodes.Castclass, actionRef),
                    Create(OpCodes.Stsfld, unloadDelegate)
                    );
            }
        }

        private static TypeDefinition GetUnloadUnityObjectsType(MethodDefinition unload)
        {
            const string name = "UnloadUnityObjectsCapture";
            string ns = unload.Module.Assembly.Name.Name + "+Realm";

            var unloadUnityObjectsType = unload.Module.GetType(ns + "." + name);
            if (unloadUnityObjectsType == null) {
                // Create delegate capture type for caching and destroying unity game objects
                unloadUnityObjectsType = new(
                    ns,
                    name,
                    TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit | TypeAttributes.AnsiClass,
                    unload.Module.TypeSystem.Object
                    );

                unload.Module.Types.Add(unloadUnityObjectsType);

                // Create UnityEngine.Object typeref
                AssemblyNameReference unityRef = unload.Module.AssemblyReferences.First(asmName => asmName.Name == "UnityEngine");
                TypeReference unityObjectReference = unload.Module.ImportReference(new TypeReference("UnityEngine", "Object", unload.Module, unityRef));

                // Create UnityEngine.Object field
                FieldDefinition objField = new("obj", FieldAttributes.Public, unityObjectReference);
                unloadUnityObjectsType.Fields.Add(objField);

                // Create ctor
                MethodDefinition ctor = new(".ctor", MethodAttributes.RTSpecialName | MethodAttributes.SpecialName | MethodAttributes.Public | MethodAttributes.HideBySig, unload.Module.TypeSystem.Void);
                ctor.Parameters.Add(new ParameterDefinition(objField.FieldType));
                ctor.Body = new MethodBody(ctor);
                ctor.Body.Instructions.Add(Create(OpCodes.Ldarg_0));
                ctor.Body.Instructions.Add(Create(OpCodes.Call, unload.Module.ImportTypeFromCoreLib("System", "Object").ImportCtor()));
                ctor.Body.Instructions.Add(Create(OpCodes.Ldarg_0));
                ctor.Body.Instructions.Add(Create(OpCodes.Ldarg_1));
                ctor.Body.Instructions.Add(Create(OpCodes.Stfld, objField));
                ctor.Body.Instructions.Add(Create(OpCodes.Ret));
                unloadUnityObjectsType.Methods.Add(ctor);

                // Creature UnityEngine.Object.DestroyImmediate methodref
                var destroyImmediateReference = unload.Module.ImportReference(new MethodReference("DestroyImmediate", unload.Module.TypeSystem.Void, unityObjectReference));
                destroyImmediateReference.Parameters.Add(new(unityObjectReference));

                // Add Kill method to destroy unity object
                MethodDefinition destroyMethod = new("Run", MethodAttributes.Public, unload.Module.TypeSystem.Void);
                destroyMethod.Body = new MethodBody(destroyMethod);
                destroyMethod.Body.Instructions.Add(Create(OpCodes.Ldarg_0));
                destroyMethod.Body.Instructions.Add(Create(OpCodes.Ldfld, objField));
                destroyMethod.Body.Instructions.Add(Create(OpCodes.Call, destroyImmediateReference));
                destroyMethod.Body.Instructions.Add(Create(OpCodes.Ret));
                unloadUnityObjectsType.Methods.Add(destroyMethod);
            }
            return unloadUnityObjectsType;
        }

        private static FieldDefinition GetUnloadDelegateField(MethodDefinition unload)
        {
            const string name = "<unload>";

            var unloadDelegate = unload.DeclaringType.Fields.FirstOrDefault(f => f.Name == name);
            if (unloadDelegate == null) {
                // Declare field if not present.
                TypeReference actionReference = GetActionTypeRef(unload.Module);

                unloadDelegate = new(name, FieldAttributes.Assembly | FieldAttributes.Static, actionReference);

                unload.DeclaringType.Fields.Add(unloadDelegate);

                // Invoke it in unload method
                BasicILCursor il = new(unload.Body);

                Instruction branchInstr = Create(OpCodes.Nop);
                il.Emit(
                    Create(OpCodes.Ldsfld, unloadDelegate),
                    Create(OpCodes.Brfalse_S, branchInstr),
                    Create(OpCodes.Ldsfld, unloadDelegate),
                    Create(OpCodes.Callvirt, actionReference.ImportMethod(false, "Invoke", unload.Module.TypeSystem.Void)),
                    Create(OpCodes.Ldnull),
                    Create(OpCodes.Stsfld, unloadDelegate),
                    branchInstr
                );
            }
            return unloadDelegate;
        }

        private static TypeReference GetActionTypeRef(ModuleDefinition module)
        {
            return module.ImportTypeFromSysCore("System", "Action");
        }
    }
}