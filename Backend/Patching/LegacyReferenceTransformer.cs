using Mono.Cecil;

namespace Backend.Patching;

sealed class LegacyReferenceTransformer
{
    struct NameDetails
    {
        public PrefixType prefixType;
        public int memberIndex;
        public int memberSeparatorIndex;
    }

    enum PrefixType
    {
        None, Orig, Hook, Add, Remove
    }

    private readonly Dictionary<string, string> cache = new();
    private readonly AssemblyDefinition hooksAsm;

    public LegacyReferenceTransformer(AssemblyDefinition hooksAsm)
    {
        this.hooksAsm = hooksAsm;
    }

    /// <summary>
    /// Transforms references as necessary in the given assembly.
    /// </summary>
    /// <param name="module"></param>
    public void Transform(ModuleDefinition module)
    {
        if (module.AssemblyReferences.Any(asmName => asmName.Name == "HOOKS-Assembly-CSharp"))
            foreach (TypeDefinition type in module.GetTypes())
                TransformType(type);
    }

    private void TransformType(TypeDefinition type)
    {
        foreach (var method in type.Methods) {
            TransformMethodDefinition(method);
        }
        foreach (var field in type.Fields) {
            TransformTypeReference(field.FieldType);
        }
    }

    private void TransformMethodDefinition(MethodDefinition method)
    {
        if (method == null)
            return;

        TransformMethodReference(method);

        if (!method.HasBody)
            return;

        foreach (var instr in method.Body.Instructions) {
            // If the operand is referencing a type (referencing orig_ or hook_ delegate), check it!
            if (instr.Operand is TypeReference typeRef) {
                TransformTypeReference(typeRef);
            }
            // Or, if the operand is referencing a function (calling add_ or remove_ on events), check it, too!
            else if (instr.Operand is MethodReference methodRef) {
                TransformMethodReference(methodRef);
                DoTransform(methodRef);
            }
        }
    }

    private void TransformMethodReference(MethodReference method)
    {
        if (method == null)
            return;

        TransformTypeReference(method.DeclaringType);

        if (!ReplaceMonomodMethodReference(method)) {
            TransformTypeReference(method.ReturnType);

            foreach (var parameter in method.Parameters) {
                TransformTypeReference(parameter.ParameterType);
            }
        }
    }

    private bool ReplaceMonomodMethodReference(MethodReference method)
    {
        // If this is referencing a MonoMod method, match its new parameter types.
        if (method.DeclaringType.FullName.StartsWith("On.")) {
            // If we can find a new type & method, use it
            var newType = hooksAsm.MainModule.GetType(method.DeclaringType.FullName);
            if (newType == null)
                return false;

            var newMethod = newType.Methods.FirstOrDefault(m => m.Name == method.Name);
            if (newMethod == null)
                return false;

            method.ReturnType = method.Module.ImportReference(newMethod.ReturnType);

            for (int i = 0; i < newMethod.Parameters.Count; i++) {
                var newParamType = method.Module.ImportReference(newMethod.Parameters[i].ParameterType);
                if (i == method.Parameters.Count)
                    method.Parameters.Add(new(newParamType));
                else
                    method.Parameters[i] = new(newParamType);
            }

            while (method.Parameters.Count > newMethod.Parameters.Count) {
                method.Parameters.RemoveAt(method.Parameters.Count - 1);
            }

            return true;
        }
        return false;
    }

    private void TransformTypeReference(TypeReference type)
    {
        DoTransform(type);
    }

    public void DoTransform(MemberReference member)
    {
        if (member.DeclaringType != null && ShouldTransform(member, out var details)) {
            if (cache.TryGetValue(member.Name, out var ret)) {
                member.Name = ret;
            }
            else {
                member.Name = cache[member.Name] = Transform(member, details);
            }
        }
    }

    private bool ShouldTransform(MemberReference member, out NameDetails details)
    {
        details = default;

        // Check for telltale MonoMod prefixes.
        if (member.Name.StartsWith("orig_")) details.prefixType = PrefixType.Orig;
        else if (member.Name.StartsWith("hook_")) details.prefixType = PrefixType.Hook;
        else if (member.Name.StartsWith("add_")) details.prefixType = PrefixType.Add;
        else if (member.Name.StartsWith("remove_")) details.prefixType = PrefixType.Remove;
        else return false;

        // Only transform MonoMod signatures.
        if (member is TypeReference t && t.Scope.Name != "HOOKS-Assembly-CSharp" || member.DeclaringType.Scope.Name != "HOOKS-Assembly-CSharp") {
            return false;
        }

        // Check if `member` exists in the updated MonoMod assembly. If not, `member` needs transformed.
        var sharedParent = hooksAsm.MainModule.GetType(member.DeclaringType.FullName);
        var transform = sharedParent == null
            || member is TypeReference && !sharedParent.NestedTypes.Any(t => t.Name == member.Name)
            || member is MethodReference && !sharedParent.Methods.Any(m => m.Name == member.Name);

        if (transform) {
            // If `member.Name` has an underscore (not including the prefix), note that.
            int underscoreIndex = member.Name.LastIndexOf('_');
            if (underscoreIndex > details.prefixType.ToString().Length) {
                details.memberSeparatorIndex = underscoreIndex;
                details.memberIndex = int.Parse(member.Name[(underscoreIndex + 1)..]);
            }
            else {
                details.memberSeparatorIndex = member.Name.Length;
            }
            return true;
        }

        return false;
    }

    // Tries to find the right new member name for the deprecated reference.
    // Returns new member name as a string.
    private string Transform(MemberReference member, NameDetails details)
    {
        TypeDefinition newHooksType = hooksAsm.MainModule.GetType(member.DeclaringType.FullName);

        string culledName = member.Name[..details.memberSeparatorIndex];

        // Start with an index of 0. Count up with each identical hook name we find, and once the number matches the old number, we have the right overload.
        if (member is TypeReference) {
            int index = 0;
            foreach (var type in newHooksType.NestedTypes)
                if (type.Name.StartsWith(culledName) && index++ == details.memberIndex)
                    return type.Name;
        }
        else if (member is MethodReference) {
            int index = 0;
            if (details.prefixType == PrefixType.Add) {
                foreach (var @event in newHooksType.Events)
                    if (@event.AddMethod.Name.StartsWith(culledName))
                        if (index++ == details.memberIndex)
                            return @event.AddMethod.Name;
            }
            else {
                foreach (var @event in newHooksType.Events)
                    if (@event.RemoveMethod.Name.StartsWith(culledName))
                        if (index++ == details.memberIndex)
                            return @event.RemoveMethod.Name;
            }
        }

        return member.Name;
    }
}
