using Mono.Cecil;
using Backend.Patching;
using System.IO.Compression;

namespace Backend.IO;

static class Wrapper
{
    public static ExitStatus Wrap(string filePath)
    {
        RwmodHeader GenerateHeader()
        {
            return GetAssemblyHeader(filePath) ?? new(0, null, Path.GetFileNameWithoutExtension(filePath), "", "");
        }

        return Wrap(filePath, GenerateHeader);
    }

    public static ExitStatus Wrap(string filePath, Func<RwmodHeader> getHeader)
    {
        if (ExtIO.RwDir.MatchFailure(out var rwDir, out var rwDirErr)) {
            return rwDirErr;
        }

        List<string> files = new();

        string? temp = TryReadZip(filePath);

        using Disposable deleteTemp = new(() => {
            if (Directory.Exists(temp)) {
                Directory.Delete(temp, true);
            }
        });

        if (temp != null) {
            files.AddRange(Directory.GetFiles(temp, "*", SearchOption.AllDirectories));
        }
        else if (File.Exists(filePath)) {
            files.Add(filePath);
        }
        else if (Directory.Exists(filePath))
            files.AddRange(Directory.GetFiles(filePath, "*", SearchOption.AllDirectories));

        if (files.Count == 0) return ExitStatus.FileNotFound(filePath);

        files.RemoveAll(f => !CanWrap(rwDir, f));

        if (files.Count == 0) return ExitStatus.Success;

        RwmodHeader header = getHeader();

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

        Console.WriteLine(header.Name);

        // Delete files after they're wrapped. Wait until the program closes before making these changes,
        // because other dependent files may have yet to be patched.
        ExtGlobal.OnExit(() => {
            if (File.Exists(filePath)) {
                File.Delete(filePath);
            }
            else {
                Directory.Delete(filePath, true);
            }
        });
        
        return ExitStatus.Success;
    }

    private static bool CanWrap(string rwDir, string file)
    {
        if (ExtGlobal.ModBlacklist.Contains(Path.GetFileNameWithoutExtension(file))) {
            return false;
        }

        try {
            using AssemblyDefinition asm = AssemblyDefinition.ReadAssembly(file);

            // Move monomod assemblies to the `monomod` folder instead of wrapping them.
            bool isMonomodAssembly = asm.MainModule.AssemblyReferences.Any(a => a.Name == "MonoMod") && asm.MainModule.GetTypes().Any(t => t.Name.StartsWith("patch_"));
            if (isMonomodAssembly) {
                string filename = Path.GetFileNameWithoutExtension(file);

                Directory.CreateDirectory(Path.Combine(rwDir, "BepInEx", "monomod"));

                File.Move(file, Path.Combine(rwDir, "BepInEx", "monomod", $"Assembly-CSharp.{filename}.mm.dll"), true);

                return false;
            }
            // Wrap other assemblies.
            return true;
        }
        catch (BadImageFormatException) { }
        return false;
    }

    private static string? TryReadZip(string filePath)
    {
        string? temp = null;

        try {
            var file = ZipFile.OpenRead(filePath);

            // Create temporary directory
            temp = Path.GetTempFileName();
            File.Delete(temp);
            Directory.CreateDirectory(temp);

            // Extract to temporary directory
            file.ExtractToDirectory(temp);

            // Return those files
            return temp;
        }
        catch {
            if (Directory.Exists(temp)) {
                Directory.Delete(temp, true);
            }
            return null;
        }
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

            return new RwmodHeader(0, new SemVer(asm.Name.Version), Path.GetFileNameWithoutExtension(filePath), GetAuthor(asm), "");
        }
        catch {
            return null;
        }
    }
}
