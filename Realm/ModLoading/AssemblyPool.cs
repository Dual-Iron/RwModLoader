using BepInEx;
using Mono.Cecil;
using Realm.Logging;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace Realm.ModLoading
{
    public sealed class AssemblyPool
    {
        public const string IterationSeparator = ";;";

        private static readonly string[] ignore = new[] { "EnumExtender" };

        public static AssemblyPool ReadMods(IProgressable progressable, string directory)
        {
            Directory.CreateDirectory(directory);

            string[] files = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories);

            if (files.Length == 0) {
                return new();
            }

            Patch(progressable, files);

            DefaultAssemblyResolver resolver = new();
            resolver.AddSearchDirectory(Paths.BepInExAssemblyDirectory);
            resolver.AddSearchDirectory(Paths.ManagedPath);
            resolver.AddSearchDirectory(Paths.PluginPath);
            resolver.AddSearchDirectory(Paths.PatcherPluginPath);
            resolver.AddSearchDirectory(Extensions.RwDepFolder);

            AssemblyPool ret = new();

            for (int i = 0; i < files.Length; i++) {
                string file = files[i];

                AssemblyDefinition definition = AssemblyDefinition.ReadAssembly(file, new() { AssemblyResolver = resolver, InMemory = false, ReadingMode = ReadingMode.Deferred });

                if (!ignore.Contains(definition.Name.Name) && TryGetRwmodName(definition, out var rwmodName)) {
                    if (ret.modAssemblies.ContainsKey(definition.Name.Name)) {
                        progressable.Message(MessageType.Fatal, "Two assemblies loaded with the same name: " + definition.Name.Name);
                    }
                    ret.modAssemblies[definition.Name.Name] = new(definition.Name.Name, file, rwmodName, GetDescriptor(definition), definition);
                    definition.Name.Name += IterationSeparator + ret.ID;
                } else {
                    definition.Dispose();
                }

                progressable.Progress = (float)(i + 1) / files.Length;

                static bool TryGetRwmodName(AssemblyDefinition definition, [MaybeNullWhen(false)] out string name)
                {
                    foreach (var customAttribute in definition.CustomAttributes)
                        if (customAttribute.AttributeType.Name == "RwmodAttribute"
                            && customAttribute.ConstructorArguments.Count == 1
                            && customAttribute.ConstructorArguments[0].Value is string n) {
                            name = n;
                            return true;
                        }
                    name = null;
                    return false;
                }
            }

            return ret;
        }

        private static ModDescriptor GetDescriptor(AssemblyDefinition definition)
        {
            foreach (var module in definition.Modules)
                foreach (var type in module.Types) {
                    if (type.IsAbstract || type.IsInterface || !type.HasMethods) {
                        continue;
                    }
                    // TODO LOW: resolve for types indirectly inheriting?
                    if (type.BaseType?.FullName == "BepInEx.BaseUnityPlugin") {
                        return new ModDescriptor.BepMod(type);
                    }
                    if (type.BaseType?.FullName == "Partiality.Modloader.PartialityMod") {
                        return new ModDescriptor.PartMod(type.FullName);
                    }
                }
            return new ModDescriptor.Lib();
        }

        private static void Patch(IProgressable progressable, string[] files)
        {
            StringBuilder args = new("--parallel");

            foreach (var file in files) {
                args.Append($" --patchup \"{file}\"");
            }

            ProcessResult result = ProcessResult.From(Extensions.MutatorPath, args.ToString());

            if (result.ExitCode != 0) {
                progressable.Message(MessageType.Fatal, $"Process exited with exit code {result.ExitCode?.ToString() ?? "TIMEOUT"}. \nE:{result.Error}\nO:{result.Output}");
            }
        }

        private static int id;

        private readonly Dictionary<string, ModAssembly> modAssemblies = new();

        private AssemblyPool() { }

        public int ID { get; } = unchecked(id++);

        public int Count => modAssemblies.Count;
        public IEnumerable<string> Names => modAssemblies.Keys;
        public IEnumerable<ModAssembly> Assemblies => modAssemblies.Values;
        public ModAssembly this[string name] => modAssemblies[name];

        public bool TryGetAssembly(string name, [MaybeNullWhen(false)] out ModAssembly modAssembly)
        {
            return modAssemblies.TryGetValue(name, out modAssembly);
        }

        public void Dispose()
        {
            foreach (var modAsm in modAssemblies.Values) {
                modAsm.AsmDef.Dispose();
            }
        }
    }
}
