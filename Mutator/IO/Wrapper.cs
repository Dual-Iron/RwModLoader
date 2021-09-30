using Mono.Cecil;
using Mutator.Patching;

namespace Mutator.IO;

public class Wrapper
{
    public static async Task Wrap(string rwmodName, string filePath, bool shouldPatch)
    {
        string[] files = Array.Empty<string>();

        if (File.Exists(filePath))
            files = new[] { filePath };
        else if (Directory.Exists(filePath))
            files = Directory.GetFiles(filePath, "*", SearchOption.AllDirectories);

        if (files.Length == 0)
            throw ErrFileNotFound(filePath);

        string? rwmodPath = null;

        if (string.IsNullOrWhiteSpace(rwmodName)) {
            RwmodFileHeader header;

            if (files.Length == 1) {
                header = WrapAssembly(filePath) ?? throw Err(ExitCodes.InvalidRwmodType, "Expected a .NET assembly.");
                rwmodPath = GetModPath(header.Name);
            } else {
                string name = Path.GetFileNameWithoutExtension(filePath);
                header = new(name, "") {
                    DisplayName = name,
                    Homepage = ""
                };
                rwmodPath = GetModPath(name);
            }

            using FileStream file = File.Create(rwmodPath);
            header.Write(file);
        } else {
            rwmodPath = GetModPath(rwmodName);
        }

        using MemoryStream ms = new();

        ushort count = 0;

        foreach (string file in files) {
            if (ModBlacklist.Any(bl => file.EndsWith(bl + ".dll"))) {
                continue;
            }

            if (shouldPatch) {
                try {
                    AssemblyPatcher.Patch(file);
                } catch (AssemblyResolutionException e) {
                    throw ErrAbsentDependency(Path.GetFileName(file), e);
                }
            }

            using Stream fileStream = File.OpenRead(file);

            await RwmodOperations.WriteRwmodEntry(ms, new() {
                FileName = Path.GetFileName(file),
                Contents = fileStream
            });

            count++;
        }

        if (count == 0) {
            throw Err(ExitCodes.EmptyRwmod);
        }

        using FileStream rwmodStream = File.Open(rwmodPath, FileMode.Open, FileAccess.ReadWrite);

        RwmodFileHeader.Read(rwmodStream);

        ms.Position = 0;
        await ms.CopyToAsync(rwmodStream);

        rwmodStream.Position = RwmodFileHeader.EntryCountByteOffset;
        rwmodStream.Write(BitConverter.GetBytes(count));
    }

    private static RwmodFileHeader? WrapAssembly(string filePath)
    {
        static string GetAuthor(AssemblyDefinition asm)
        {
            foreach (var attribute in asm.CustomAttributes)
                if (attribute.AttributeType.FullName == "System.Reflection.AssemblyCompanyAttribute"
                    && attribute.ConstructorArguments.Count == 1
                    && attribute.ConstructorArguments[0].Value is string author
                    && author != asm.Name.Name) {
                    return author;
                }
            return "";
        }

        try {
            using AssemblyDefinition asm = AssemblyDefinition.ReadAssembly(filePath);

            return new RwmodFileHeader(asm.Name.Name, GetAuthor(asm)) {
                ModVersion = new(asm.Name.Version ?? new(0, 0, 1)),
                DisplayName = asm.Name.Name
            };
        } catch {
            return null;
        }
    }
}
