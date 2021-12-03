using Mono.Cecil;
using Mutator.Patching;

namespace Mutator.IO;

static class Wrapper
{
    public static ExitStatus Wrap(string filePath)
    {
        if (ExtIO.RwDir.MatchFailure(out var rwDir, out var rwDirErr)) {
            return rwDirErr;
        }

        List<string> files = new();

        if (File.Exists(filePath))
            files.Add(filePath);
        else if (Directory.Exists(filePath))
            files.AddRange(Directory.GetFiles(filePath, "*", SearchOption.AllDirectories));

        if (files.Count == 0) return ExitStatus.FileNotFound(filePath);

        files.RemoveAll(s => ExtGlobal.ModBlacklist.Contains(Path.GetFileNameWithoutExtension(s)));

        if (files.Count == 0) return ExitStatus.Success;

        files.RemoveAll(file => CheckForMonomod(rwDir, file));

        RwmodHeader? header = GetAssemblyHeader(filePath) ?? new(0, new(0, 1, 0), Path.GetFileName(filePath), "", "");

        using Stream rwmodStream = File.Create(ExtIO.GetModPath(header.Name));

        header.Write(rwmodStream);

        foreach (string file in files) {
            var patchResult = Patcher.Patch(file);
            if (!patchResult.Successful) {
                return patchResult;
            }

            using Stream fileStream = File.OpenRead(file);

            if (fileStream.Length > uint.MaxValue) return ExitStatus.FileTooLarge(file);

            RwmodFileEntry entry = new(Path.GetFileName(file), (uint)fileStream.Length);

            entry.Write(rwmodStream);

            RwmodIO.CopyStream(fileStream, rwmodStream, fileStream.Length);
        }

        Console.Write(header.Name);

        return ExitStatus.Success;
    }

    private static bool CheckForMonomod(string rwDir, string file)
    {
        bool isMonomodAssembly = false;

        using (AssemblyDefinition asm = AssemblyDefinition.ReadAssembly(file))
            isMonomodAssembly = asm.MainModule.AssemblyReferences.Any(a => a.Name == "MonoMod") && asm.MainModule.Types.Any(t => t.Name.StartsWith("patch_"));

        if (isMonomodAssembly) {
            string filename = Path.GetFileNameWithoutExtension(file);

            Directory.CreateDirectory(Path.Combine(rwDir, "BepInEx", "monomod"));

            File.Move(file, Path.Combine(rwDir, "BepInEx", "monomod", $"Assembly-CSharp.{filename}.mm.dll"), true);

            return true;
        }
        return false;
    }

    private static RwmodHeader? GetAssemblyHeader(string filePath)
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

            return new RwmodHeader(0, new SemVer(asm.Name.Version), asm.Name.Name, GetAuthor(asm), "");
        }
        catch {
            return null;
        }
    }
}
