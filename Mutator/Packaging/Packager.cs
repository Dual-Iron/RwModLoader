using Mono.Cecil;
using Mutator.Patching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using static Mutator.InstallerApi;

namespace Mutator.Packaging
{
    public static class Packager
    {
        private const CompressionLevel RwmodCompressionLevel = CompressionLevel.NoCompression;

        public static readonly IEnumerable<string> ModBlacklist = new[] { "EnumExtender", "PublicityStunt", "AutoUpdate" };

        private static void VerifyRwmodFile(string filePath)
        {
            if (Path.GetExtension(filePath) != ".rwmod") {
                throw new($"The file {filePath} is not a RWMOD file.");
            }
            if (!File.Exists(filePath)) {
                throw new($"The file {filePath} does not exist.");
            }
        }

        public static async Task Download(string path)
        {
            await VerifyInternetConnection();

            RepoFiles files;
            RwmodFileHeader header;
            string name;

            if (File.Exists(path)) {
                // Get from file
                name = Path.GetFileNameWithoutExtension(path);

                using Stream input = File.OpenRead(path);

                header = RwmodFileHeader.Read(input);

                files = await GetFilesFromGitHubRepository(header.Author, header.Name);
            } else {
                // Get from repository
                string[] args = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (args.Length != 2) {
                    throw new($"The path {path} is not a RWMOD file or a repository.");
                }

                files = await GetFilesFromGitHubRepository(args[0], name = args[1]);

                using Stream output = File.Create(GetModPath(name));

                header = new RwmodFileHeader(default,
                                    default,
                                    name: name,
                                    author: args[0],
                                    displayName: name,
                                    $"https://github.com/{args[0]}/{args[1]}#readme");
                header.Write(output);
            }

            // TODO completely redo this lmao

            if (files.Version > header.ModVersion.ToVersion()) {
                string tempDir = Path.GetTempFileName();

                File.Delete(tempDir);

                tempDir = tempDir.ProofDirectory();

                try {
                    for (int i = 0; i < files.Count; i++) {
                        string onlineFileName = Path.GetFileName(files.GetName(i));

                        string localFileName = Path.Combine(tempDir, onlineFileName);

                        using (Stream fs = File.Create(localFileName)) {
                            using Stream online = await files.GetOnlineFileStream(i);
                            await DownloadWithProgress(online, fs, Console.WriteLine);
                        }

                        if (Path.GetExtension(onlineFileName) == ".zip") {
                            string tempExtract = Path.GetTempFileName();
                            File.Delete(tempExtract);

                            try {
                                ZipFile.ExtractToDirectory(localFileName, tempExtract);

                                foreach (var file in Directory.EnumerateFiles(tempExtract, "*", SearchOption.AllDirectories)) {
                                    string moveToPath = Path.Combine(tempDir, Path.GetRelativePath(tempExtract, file));
                                    File.Move(file, moveToPath.ProofDirectory(), true);
                                }
                            } finally {
                                File.Delete(localFileName);
                                if (Directory.Exists(tempExtract))
                                    Directory.Delete(tempExtract, true);
                            }
                        }
                    }

                    await Update(name, tempDir, true);
                } finally {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
            }
        }

        public static async Task Update(string rwmodName, string filePath, bool shouldPatch)
        {
            string relativeDir;
            string[] files;

            if (File.Exists(filePath)) {
                relativeDir = Path.GetDirectoryName(filePath) ?? "";
                files = new[] { filePath };
            } else if (Directory.Exists(filePath)) {
                relativeDir = filePath;
                files = Directory.GetFiles(filePath, "*", SearchOption.AllDirectories);
            } else
                throw new($"The file or folder {filePath} does not exist.");

            string rwmodPath = GetModPath(rwmodName);

            if (!File.Exists(rwmodPath)) {
                using FileStream rwmodHeaderStream = File.Create(rwmodPath);

                if (!WrapAssembly(filePath, rwmodHeaderStream) && (files.Length != 1 || !WrapAssembly(files[0], rwmodHeaderStream))) {
                    WrapDefault(filePath, rwmodHeaderStream);
                }
            }

            using MemoryStream ms = new();

            using (ZipArchive archive = new(ms, ZipArchiveMode.Create, true, UseEncoding)) {
                foreach (var file in files) {
                    if (ModBlacklist.Any(bl => file.EndsWith(bl + ".dll"))) {
                        continue;
                    }

                    if (shouldPatch) {
                        await AssemblyPatcher.Patch(rwmodName, file, false);
                    }

                    using Stream fileStream = File.OpenRead(file);
                    using Stream entryStream = archive.CreateEntry(Path.GetRelativePath(relativeDir, file), RwmodCompressionLevel).Open();

                    await fileStream.CopyToAsync(entryStream);
                }
            }

            ms.Position = 0;

            using FileStream rwmodStream = File.Open(rwmodPath, FileMode.Open, FileAccess.ReadWrite);

            RwmodFileHeader header = RwmodFileHeader.Read(rwmodStream);

            rwmodStream.SetLength(rwmodStream.Position);

            await ms.CopyToAsync(rwmodStream);
        }

        private static void WrapDefault(string filePath, FileStream rwmodHeaderStream)
        {
            new RwmodFileHeader(RwmodFileHeader.RwmodFlags.IsUnwrapped,
                                modVersion: new(0, 0, 0),
                                repositoryName: "",
                                author: "",
                                displayName: Path.GetFileNameWithoutExtension(filePath),
                                modDependencies: new())
                .Write(rwmodHeaderStream);
        }

        private static bool WrapAssembly(string filePath, FileStream rwmod)
        {
            static string GetAuthor(AssemblyDefinition asm)
            {
                foreach (var attribute in asm.CustomAttributes)
                    if (attribute.AttributeType.FullName == "System.Reflection.AssemblyCompanyAttribute"
                        && attribute.ConstructorArguments.Count == 1
                        && attribute.ConstructorArguments[0].Value is string author &&
                        author != asm.Name.Name) {
                        return author;
                    }
                return "";
            }

            if (!File.Exists(filePath)) {
                return false;
            }

            try {
                using AssemblyDefinition asm = AssemblyDefinition.ReadAssembly(filePath);

                new RwmodFileHeader(RwmodFileHeader.RwmodFlags.IsUnwrapped,
                                modVersion: new(asm.Name.Version ?? new(0, 0, 1)),
                                repositoryName: "",
                                author: GetAuthor(asm),
                                displayName: asm.Name.Name,
                                modDependencies: new())
                .Write(rwmod);

                return true;
            } catch { }

            return false;
        }

        public static async Task Extract(string filePath)
        {
            VerifyRwmodFile(filePath);

            string directory = Path.ChangeExtension(filePath, null);
            string directoryNameSafe = directory;

            int x = 2;
            while (File.Exists(directoryNameSafe) || Directory.Exists(directoryNameSafe)) {
                directoryNameSafe = directory + $" ({x++})";
            }

            using MemoryStream ms = new();
            using (Stream rwmodFileStream = File.Open(filePath, FileMode.Open, FileAccess.Read)) {
                RwmodFileHeader.Read(rwmodFileStream);

                await rwmodFileStream.CopyToAsync(ms);
            }
            ms.Position = 0;

            using var archive = new ZipArchive(ms, ZipArchiveMode.Read, true, UseEncoding);

            archive.ExtractToDirectory(directoryNameSafe.ProofDirectory());
        }

        public static async Task Unwrap(string filePath)
        {
            VerifyRwmodFile(filePath);

            string name = Path.GetFileNameWithoutExtension(filePath);

            using MemoryStream ms = new();
            using (Stream rwmodFile = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite)) {
                RwmodFileHeader header = RwmodFileHeader.Read(rwmodFile);

                header.Flags |= RwmodFileHeader.RwmodFlags.IsUnwrapped;
                header.WriteFlags(rwmodFile);
                
                await rwmodFile.CopyToAsync(ms);
            }
            ms.Position = 0;

            using ZipArchive archive = new(ms, ZipArchiveMode.Read, true, UseEncoding);

            string root = RwDir;

            bool rwRoot = archive.Entries.Count == 1 && archive.Entries[0].Name == "Rain World.zip";
            if (!rwRoot)
                root = Path.Combine(root, "BepInEx", "plugins");

            foreach (var entry in archive.Entries) {
                if (rwRoot && !(entry.FullName.StartsWith("BepInEx/") || entry.FullName.StartsWith("BepInEx\\"))) {
                    continue;
                }

                string outPath = Path.Combine(root, entry.FullName);

                if (File.Exists(outPath)) {
                    if (ShouldSkipEntry(entry, outPath, false)) {
                        continue;
                    }

                    string relative = Path.GetRelativePath(RwDir, outPath);
                    string restoration = Path.Combine(RestorationFolder(name).FullName, relative);

                    if (!File.Exists(restoration)) {
                        File.Copy(outPath, restoration.ProofDirectory());
                    }
                }

                using var entryStream = entry.Open();
                using var outputStream = File.Create(outPath.ProofDirectory());
                await entryStream.CopyToAsync(outputStream);
            }
        }

        public static async Task Restore(string filePath)
        {
            VerifyRwmodFile(filePath);

            string name = Path.GetFileNameWithoutExtension(filePath);

            string rwDirectory = RwDir;

            using MemoryStream ms = new();
            using (Stream rwmodFile = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite)) {
                RwmodFileHeader header = RwmodFileHeader.Read(rwmodFile);

                if (!header.Flags.HasFlag(RwmodFileHeader.RwmodFlags.IsUnwrapped)) {
                    return;
                }

                header.Flags &= ~RwmodFileHeader.RwmodFlags.IsUnwrapped;
                header.WriteFlags(rwmodFile);

                await rwmodFile.CopyToAsync(ms);
            }
            ms.Position = 0;

            using ZipArchive archive = new(ms, ZipArchiveMode.Read, true, UseEncoding);

            string root = rwDirectory;

            bool rwRoot = archive.Entries.Count == 1 && archive.Entries[0].Name == "Rain World.zip";
            if (!rwRoot)
                root = Path.Combine(root, "BepInEx", "plugins");

            foreach (var entry in archive.Entries) {
                if (rwRoot && !(entry.FullName.StartsWith("BepInEx/") || entry.FullName.StartsWith("BepInEx\\"))) {
                    continue;
                }

                string outPath = Path.Combine(root, entry.FullName);

                if (ShouldSkipEntry(entry, outPath, true)) {
                    continue;
                }

                string relative = Path.GetRelativePath(RwDir, outPath);
                string restoration = Path.Combine(RestorationFolder(name).FullName, relative);

                if (File.Exists(restoration)) {
                    File.Move(restoration, outPath.ProofDirectory(), true);
                } else
                    File.Delete(outPath);
            }
        }

        private static bool ShouldSkipEntry(ZipArchiveEntry entry, string outPath, bool mustBeExact)
        {
            string temp = Path.GetTempFileName();
            try {
                var existingFile = FileVersionInfo.GetVersionInfo(outPath);
                if (existingFile?.ProductVersion == null) return false;

                entry.ExtractToFile(temp, true);

                var newFile = FileVersionInfo.GetVersionInfo(temp);
                if (newFile?.ProductVersion == null) return true;

                if (mustBeExact)
                    return newFile.ProductMajorPart != existingFile.ProductMajorPart ||
                        newFile.ProductMinorPart != existingFile.ProductMinorPart ||
                        newFile.ProductBuildPart != existingFile.ProductBuildPart;
                else
                    return new Version(newFile.ProductMajorPart, newFile.ProductMinorPart, newFile.ProductBuildPart) <=
                        new Version(existingFile.ProductMajorPart, existingFile.ProductMinorPart, existingFile.ProductBuildPart);
            } catch {
                return true;
            } finally {
                File.Delete(temp);
            }
        }
    }
}