using Mono.Cecil;
using Mutator.Patching;

namespace Mutator.IO;

static class Wrapper
{
    public static ExitStatus Wrap(string filePath)
    {
        List<string> files = new();

        if (File.Exists(filePath))
            files.Add(filePath);
        else if (Directory.Exists(filePath))
            files.AddRange(Directory.GetFiles(filePath, "*", SearchOption.AllDirectories));

        if (files.Count == 0) return ExitStatus.FileNotFound(filePath);

        files.RemoveAll(s => ExtGlobal.ModBlacklist.Contains(Path.GetFileNameWithoutExtension(s)));

        if (files.Count == 0) return ExitStatus.Success;

        RwmodFileHeader? header = GetAssemblyHeader(filePath) ?? new(Path.GetFileName(filePath), "");

        using Stream rwmodStream = File.Create(ExtIO.GetModPath(header.Name));

        header.Write(rwmodStream);

        ushort count = 0;

        foreach (string file in files) {
            var patchResult = Patcher.Patch(file);
            if (!patchResult.Successful) {
                return patchResult;
            }

            using Stream fileStream = File.OpenRead(file);

            RwmodOperations.WriteRwmodEntry(rwmodStream, new() {
                FileName = Path.GetFileName(file),
                Contents = fileStream
            });

            count++;
        }

        rwmodStream.Position = RwmodFileHeader.EntryCountByteOffset;
        rwmodStream.Write(BitConverter.GetBytes(count));

        Console.Write(header.Name);

        return ExitStatus.Success;
    }

    private static RwmodFileHeader? GetAssemblyHeader(string filePath)
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
            using var asm = AssemblyDefinition.ReadAssembly(filePath);

            return new RwmodFileHeader(asm.Name.Name, GetAuthor(asm)) {
                ModVersion = new(asm.Name.Version ?? new(0, 0, 1)),
                DisplayName = asm.Name.Name
            };
        } catch {
            return null;
        }
    }
}
