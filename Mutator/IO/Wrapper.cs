using Mono.Cecil;
using Mutator.Patching;

namespace Mutator.IO;

public class Wrapper
{
    public static async Task Wrap(string rwmodName, string filePath)
    {
        string[] files = Array.Empty<string>();

        if (File.Exists(filePath))
            files = new[] { filePath };
        else if (Directory.Exists(filePath))
            files = Directory.GetFiles(filePath, "*", SearchOption.AllDirectories);

        if (files.Length == 0)
            throw ErrFileNotFound(filePath);

        Stream GetRwmodFileStream()
        {
            if (string.IsNullOrWhiteSpace(rwmodName)) {
                RwmodFileHeader header = GetHeader();

                Stream inferredRwmod = File.Create(GetModPath(header.Name));
                header.Write(inferredRwmod);
                return inferredRwmod;
            }

            string path = GetModPath(rwmodName);

            if (!File.Exists(path)) {
                RwmodFileHeader header = GetHeader();
                header.Name = rwmodName;

                Stream createdRwmod = File.Create(path);
                header.Write(createdRwmod);
                return createdRwmod;
            }

            Stream readRwmod = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
            RwmodFileHeader.Read(readRwmod);
            return readRwmod;

            RwmodFileHeader GetHeader()
            {
                return files.Length == 1
                    ? WrapAssembly(filePath) ?? throw Err(ExitCodes.InvalidRwmodType, "Expected a .NET assembly.")
                    : new(Path.GetFileName(filePath), "") { 
                        DisplayName = Path.GetFileName(filePath), 
                        Homepage = "" 
                    };
            }
        }

        using Stream rwmodStream = GetRwmodFileStream();

        ushort count = 0;

        foreach (string file in files) {
            if (ModBlacklist.Any(bl => file.EndsWith(bl + ".dll"))) {
                continue;
            }

            AssemblyPatcher.Patch(file);

            using Stream fileStream = File.OpenRead(file);

            await RwmodOperations.WriteRwmodEntry(rwmodStream, new() {
                FileName = Path.GetFileName(file),
                Contents = fileStream
            });

            count++;
        }

        if (count == 0) {
            throw Err(ExitCodes.EmptyRwmod);
        }

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
            string name, author;
            Version? version;

            using (var asm = AssemblyDefinition.ReadAssembly(filePath)) {
                name = asm.Name.Name;
                version = asm.Name.Version;
                author = GetAuthor(asm);
            }

            return new RwmodFileHeader(name, author) {
                ModVersion = new(version ?? new(0, 0, 1)),
                DisplayName = name
            };
        } catch {
            return null;
        }
    }
}
