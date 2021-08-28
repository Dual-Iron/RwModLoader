using Mono.Cecil;
using System;
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

        private static void VerifyRwmodFile(string filePath)
        {
            if (Path.GetExtension(filePath) != ".rwmod") {
                throw new($"The file {filePath} is not a RWMOD file.");
            }
            if (!File.Exists(filePath)) {
                throw new($"The file {filePath} does not exist.");
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

        public static void PrintHeader(string filePath)
        {
            VerifyRwmodFile(filePath);

            using Stream fs = File.OpenRead(filePath);

            var header = RwmodFileHeader.Read(fs);

            string authorName = string.IsNullOrEmpty(header.Author) ? "" : ": " + header.Author;
            string repoName = string.IsNullOrEmpty(header.RepositoryName) ? "" : "/" + header.RepositoryName;

            Console.WriteLine($"Flags={Convert.ToString((int)header.Flags, 2)} \"{header.DisplayName}\" v{header.ModVersion.Major}.{header.ModVersion.Minor}.{header.ModVersion.Patch}{authorName}{repoName}");
        }

        public static async Task Download(string filePath)
        {
            VerifyRwmodFile(filePath);

            using var input = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite);

            RwmodFileHeader header = RwmodFileHeader.Read(input);

            if (string.IsNullOrEmpty(header.RepositoryName)) {
                throw new("Mod lacks a repository to fetch from.");
            }

            await VerifyInternetConnection();

            // TODO HIGH: get dependencies from repository, not from RainDB
            RepoFiles files = await GetFilesFromGitHubRepository(header.Author, header.RepositoryName);

            if (files.Version > header.ModVersion.ToVersion()) {
                // Download new version of mod

                if (files.RwRoot) {
                    await DownloadWithProgress(await files.GetOnlineFileStream(0), input, Console.WriteLine);
                } else {
                    using ZipArchive archive = new(input, ZipArchiveMode.Create, true, UseEncoding);

                    for (int i = 0; i < files.Count; i++) {
                        var entry = archive.CreateEntry(files.GetName(i), RwmodCompressionLevel);
                        using var entryStream = entry.Open();
                        var stream = await files.GetOnlineFileStream(i);

                        await DownloadWithProgress(stream, entryStream, Console.WriteLine);
                    }
                }

                // Update version
                header.ModVersion = new((byte)files.Version.Major, (byte)files.Version.Minor, (byte)files.Version.Build);
                header.WriteVersion(input);
            }
        }

        public static async Task Update(string rwmodName, string filePath)
        {
            if (!File.Exists(filePath)) {
                throw new("No such file.");
            }

            string rwmodPath = GetModPath(rwmodName);

            if (File.Exists(rwmodPath)) {
                // Advance rwmod stream to zip file
                using FileStream rwmodStream = File.Open(rwmodPath, FileMode.Open, FileAccess.ReadWrite);

                using MemoryStream ms = new();

                await rwmodStream.CopyToAsync(ms);

                ms.Position = 0;

                // Get that stream as an archive, modify it, and update it
                using (ZipArchive archive = new(ms, ZipArchiveMode.Update, true, UseEncoding)) {

                    string fileName = Path.GetFileName(filePath);

                    ZipArchiveEntry? entry = archive.Entries.SingleOrDefault(e => e.Name == fileName);

                    if (entry != null) {
                        entry.Delete();
                    }

                    using var entryStream = archive.CreateEntry(entry?.FullName ?? fileName, RwmodCompressionLevel).Open();
                    using var fileStream = File.OpenRead(filePath);

                    await fileStream.CopyToAsync(entryStream);
                }

                // Copy ms back to original rwmod stream
                rwmodStream.Position = 0;
                ms.Position = 0;

                RwmodFileHeader.Read(rwmodStream);

                await ms.CopyToAsync(rwmodStream);
            } else {
                await Wrap(filePath);
                Include(Path.ChangeExtension(filePath, ".rwmod"));
            }
        }

        public static void Include(string filePath)
        {
            string path = Path.ChangeExtension(filePath, ".rwmod");

            if (!File.Exists(path)) {
                throw new("No such RWMOD file.");
            }

            string rwmodsListingFolder = ModsFolder.FullName;
            string newPath = Path.Combine(rwmodsListingFolder, Path.GetFileName(path));

            File.Move(path, newPath, true);
        }

        public static async Task Wrap(string filePath)
        {
            try {
                using AssemblyDefinition asmdef = AssemblyDefinition.ReadAssembly(filePath);
                using FileStream fs = File.Create(Path.ChangeExtension(filePath, ".rwmod"));

                await WrapAssembly(filePath, asmdef, fs);
            } catch (BadImageFormatException) {
                try {
                    using ZipArchive archive = ZipFile.OpenRead(filePath);
                    using FileStream fs = File.Create(Path.ChangeExtension(filePath, ".rwmod"));

                    await WrapZip(filePath, archive, fs);
                } catch (InvalidDataException) {
                    throw new("File is not a .NET assembly or a ZIP file.");
                }
            }
        }

        private static async Task WrapAssembly(string filePath, AssemblyDefinition asm, FileStream rwmod)
        {
            string GetAuthor()
            {
                foreach (var attribute in asm.CustomAttributes) {
                    if (attribute.AttributeType.FullName == "System.Reflection.AssemblyCompanyAttribute"
                        && attribute.ConstructorArguments.Count == 1
                        && attribute.ConstructorArguments[0].Value is string author &&
                        author != asm.Name.Name) {
                        return author;
                    }
                }
                return "";
            }

            new RwmodFileHeader(RwmodFileHeader.RwmodFlags.IsUnwrapped,
                                modVersion: new(asm.Name.Version ?? new(0, 0, 1)),
                                repositoryName: "",
                                author: GetAuthor(),
                                displayName: asm.Name.Name,
                                modDependencies: new())
                .Write(rwmod);

            asm.Dispose();

            using ZipArchive archive = new(rwmod, ZipArchiveMode.Create, true);
            using Stream entryStream = archive.CreateEntry(Path.GetFileName(filePath), RwmodCompressionLevel).Open();
            using Stream fileStream = File.OpenRead(filePath);

            await fileStream.CopyToAsync(entryStream);
        }

        private static async Task WrapZip(string filePath, ZipArchive inputArchive, FileStream rwmod)
        {
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(filePath);

            new RwmodFileHeader(RwmodFileHeader.RwmodFlags.IsUnwrapped,
                                modVersion: new((byte)versionInfo.ProductMajorPart, (byte)versionInfo.ProductMinorPart, (byte)versionInfo.ProductBuildPart),
                                repositoryName: "",
                                author: "",
                                displayName: Path.GetFileNameWithoutExtension(filePath),
                                modDependencies: new())
                .Write(rwmod);

            using ZipArchive archive = new(rwmod, ZipArchiveMode.Create, true);
            foreach (ZipArchiveEntry entry in inputArchive.Entries) {
                using Stream existingStream = entry.Open();
                using Stream newEntryStream = archive.CreateEntry(entry.Name, RwmodCompressionLevel).Open();

                await existingStream.CopyToAsync(newEntryStream);
            }
        }

        public static async Task Unwrap(string filePath)
        {
            VerifyRwmodFile(filePath);

            using var rwmodFile = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite);

            RwmodFileHeader header = RwmodFileHeader.Read(rwmodFile);

            header.Flags |= RwmodFileHeader.RwmodFlags.IsUnwrapped;
            header.WriteFlags(rwmodFile);

            using var archive = new ZipArchive(rwmodFile, ZipArchiveMode.Read, true, UseEncoding);

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
                    string restoration = Path.Combine(RestorationFolder.FullName, relative);

                    if (!File.Exists(restoration))
                        File.Copy(outPath, restoration);
                }

                using var entryStream = entry.Open();
                using var outputStream = File.Create(outPath);
                await entryStream.CopyToAsync(outputStream);
            }
        }

        public static void Extract(string filePath)
        {
            VerifyRwmodFile(filePath);

            string directory = Path.ChangeExtension(filePath, null);
            string directoryNameSafe = directory;

            int x = 2;
            while (File.Exists(directoryNameSafe) || Directory.Exists(directoryNameSafe)) {
                directoryNameSafe = directory + $" ({x++})";
            }

            using var rwmodFile = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite);
            using var archive = new ZipArchive(rwmodFile, ZipArchiveMode.Read, true, UseEncoding);

            archive.ExtractToDirectory(Directory.CreateDirectory(directoryNameSafe).FullName);
        }

        public static void Restore(string filePath)
        {
            VerifyRwmodFile(filePath);

            string rwDirectory = RwDir;

            using var rwmodFile = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite);

            RwmodFileHeader header = RwmodFileHeader.Read(rwmodFile);

            if (!header.Flags.HasFlag(RwmodFileHeader.RwmodFlags.IsUnwrapped)) {
                return;
            }

            header.Flags &= ~RwmodFileHeader.RwmodFlags.IsUnwrapped;
            header.WriteFlags(rwmodFile);

            using var archive = new ZipArchive(rwmodFile, ZipArchiveMode.Read, true, UseEncoding);

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
                string restoration = Path.Combine(RestorationFolder.FullName, relative);

                if (File.Exists(restoration))
                    File.Move(restoration, outPath, true);
                else
                    File.Delete(outPath);
            }
        }
    }
}