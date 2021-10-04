using BepInEx;
using Mono.Cecil;
using Partiality.Modloader;
using Realm.Logging;
using Realm.ModLoading;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Realm.AssemblyLoading;

public sealed class AssemblyPool
{
    public const string IterationSeparator = ";;";

    private static readonly string[] ignore = new[] { "EnumExtender", "PublicityStunt", "AutoUpdate", "LogFix" };
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
            foreach (var fileEntry in rwmod.Entries) {
                count++;
                progressable.Progress = (float)count / (count + 1); // TODO make this an honest progress tracker

                AssemblyDefinition asm = AssemblyDefinition.ReadAssembly(
                    stream: fileEntry.GetStreamSplice(rwmod.Stream),
                    parameters: new() {
                        ReadSymbols = false,
                        ThrowIfSymbolsAreNotMatching = false,
                        AssemblyResolver = resolver,
                        InMemory = false,
                        ReadingMode = ReadingMode.Deferred
                    });

                string name = asm.Name.Name;

                // If the assembly is blacklisted or not a mod assembly, skip.
                if (ignore.Contains(name) || !IsPatched(asm, out var modType))
                    continue;

                // If an assembly with this name already exists,
                if (ret.modAssemblies.TryGetValue(name, out ModAssembly conflicting)) {
                    // If it's a different major version, there's a conflict.
                    if (asm.Name.Version.Major != conflicting.AsmDef.Name.Version.Major) {
                        progressable.Message(
                            messageType: MessageType.Fatal,
                            message: $"Two assemblies named {name} are incompatible: {asm.Name.Version} from {rwmod.Header.Name} and {conflicting.AsmDef.Name.Version} from {conflicting.Rwmod}."
                            );
                        continue;
                    }
                    // If it's a more recent version, skip.
                    else if (conflicting.AsmDef.Name.Version > asm.Name.Version)
                        continue;
                    // Otherwise, replace the existing one.
                }

                ret.modAssemblies[name] = new(rwmod, fileEntry.Index, GetDescriptor(asm, modType), asm);
                asm.Name.Name += IterationSeparator + ret.ID;
            }
        }

        return ret;

        static bool IsPatched(AssemblyDefinition definition, [MaybeNullWhen(false)] out string typeName)
        {
            foreach (var customAttribute in definition.CustomAttributes)
                if (customAttribute.AttributeType.Name == "RwmodAttribute"
                    && customAttribute.ConstructorArguments.Count == 2
                    && customAttribute.ConstructorArguments[1].Value is string n) {
                    typeName = n;
                    return true;
                }
            typeName = null;
            return false;
        }
    }

    private static ModDescriptor GetDescriptor(AssemblyDefinition definition, string modType)
    {
        if (string.IsNullOrEmpty(modType)) {
            return new ModDescriptor.Lib();
        }

        TypeDefinition type = definition.MainModule.GetType(modType);

        if (type is not null && !type.IsAbstract && !type.IsInterface && !type.IsValueType) {
            if (type.IsSubtypeOf(typeof(BaseUnityPlugin)))
                return new ModDescriptor.BepMod(type);
            if (type.IsSubtypeOf(typeof(PartialityMod)))
                return new ModDescriptor.PartMod(type.FullName);
        }

        Program.Logger.LogError($"Mutator's patcher provided a type name that wasn't a plugin or a partmod. {modType} from {definition.Name}");
        return new ModDescriptor.Lib();
    }

    private readonly Dictionary<string, ModAssembly> modAssemblies = new();

    public int ID { get; } = unchecked(id++);
    public int Count => modAssemblies.Count;
    public IEnumerable<string> Names => modAssemblies.Keys;
    public IEnumerable<ModAssembly> Assemblies => modAssemblies.Values;
    public ModAssembly this[string name] => modAssemblies[name];

    private AssemblyPool() { }

    public bool TryGetAssembly(string name, [MaybeNullWhen(false)] out ModAssembly modAssembly)
    {
        return modAssemblies.TryGetValue(name, out modAssembly);
    }
}
