using BepInEx;
using Mono.Cecil;
using Realm.Logging;
using Realm.ModLoading;
using System.Diagnostics.CodeAnalysis;

namespace Realm.AssemblyLoading;

sealed class AssemblyPool
{
    public const string IterationSeparator = ";;";

    private static readonly string[] ignore = new[] { "EnumExtender", "PublicityStunt", "AutoUpdate", "LogFix", "BepInEx-Partiality-Wrapper" };
    private static int id;

    /// <summary>
    /// Reads assemblies and stores their assembly definitions. Never calls <see cref="IDisposable.Dispose"/> on the assembly streams.
    /// </summary>
    public static AssemblyPool Read(IProgressable progressable, IList<RwmodFile> rwmods)
    {
        DefaultAssemblyResolver resolver = new();
        resolver.AddSearchDirectory(Paths.BepInExAssemblyDirectory);
        resolver.AddSearchDirectory(Paths.ManagedPath);
        resolver.AddSearchDirectory(Paths.PluginPath);
        resolver.AddSearchDirectory(Paths.PatcherPluginPath);

        AssemblyPool ret = new();

        int count = -1;

        foreach (var rwmod in rwmods) {
            var dlls = rwmod.Entries.Where(f => f.FileName.EndsWith(".dll"));

            foreach (var fileEntry in dlls) {
                count++;
                progressable.Progress = count / (count + 1f);

                _ = TryAddAsm(ret, fileEntry, rwmod);
            }
        }

        return ret;

        ModAssembly? TryAddAsm(AssemblyPool ret, FileEntry fileEntry, RwmodFile rwmod)
        {
            AssemblyDefinition asm;

            try {
                asm = AssemblyDefinition.ReadAssembly(
                    stream: fileEntry.GetStreamSplice(rwmod.Stream),
                    parameters: new() { ReadSymbols = false, AssemblyResolver = resolver }
                    );
            } catch {
                return null;
            }

            string name = asm.Name.Name;

            // If the assembly is blacklisted or not a mod assembly, skip.
            if (ignore.Contains(name) || !IsPatched(asm, out var modTypes))
                return null;

            // If an assembly with this name already exists,
            if (ret.modAssemblies.TryGetValue(name, out ModAssembly conflicting)) {
                // If it's a different major version, there's a conflict.
                if (asm.Name.Version.Major != conflicting.AsmDef.Name.Version.Major) {
                    progressable.Message(
                        messageType: MessageType.Fatal,
                        message: $"Two assemblies named {name} are incompatible: {asm.Name.Version} from {rwmod.Header.Name}, and {conflicting.AsmDef.Name.Version} from {conflicting.Rwmod}."
                        );
                    return null;
                }
                // If it's a more recent version, skip.
                else if (conflicting.AsmDef.Name.Version > asm.Name.Version)
                    return null;
            }

            ret.modAssemblies[name] = new(rwmod, fileEntry.Index, new AssemblyDescriptor(asm, modTypes), asm);
            asm.Name.Name += IterationSeparator + ret.ID;
            asm.MainModule.Mvid = Guid.NewGuid();

            return ret.modAssemblies[name];
        }

        static bool IsPatched(AssemblyDefinition definition, [MaybeNullWhen(false)] out IList<string> typeNames)
        {
            foreach (var customAttribute in definition.CustomAttributes)
                if (customAttribute.AttributeType.Name == "RwmodAttribute"
                    && customAttribute.ConstructorArguments.Count == 2
                    && customAttribute.ConstructorArguments[1].Value is CustomAttributeArgument[] typeNamesArr) {
                    typeNames = new List<string>();
                    foreach (var typeName in typeNamesArr) {
                        if (typeName.Value is string s)
                            typeNames.Add(s);
                    }
                    return true;
                }
            typeNames = null;
            return false;
        }
    }

    private readonly Dictionary<string, ModAssembly> modAssemblies = new();

    public int ID { get; } = unchecked(id++);
    public int Count => modAssemblies.Count;
    public IEnumerable<string> Names => modAssemblies.Keys;
    public IEnumerable<ModAssembly> Assemblies => modAssemblies.Values;

    /// <param name="name">Assembly name with no iteration separator.</param>
    public ModAssembly this[string name] => modAssemblies[name];

    private AssemblyPool() { }

    public bool TryGetAssembly(string name, [MaybeNullWhen(false)] out ModAssembly modAssembly)
    {
        return modAssemblies.TryGetValue(name, out modAssembly);
    }
}
