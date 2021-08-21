using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static Mutator.InstallerApi;

namespace Mutator.Packaging
{
    public static class Packager
    {
        private const CompressionLevel RwmodCompressionLevel = CompressionLevel.Optimal;

        private static void VerifyRwmodFile(string filePath)
        {
            if (Path.GetExtension(filePath) != ".rwmod") {
                throw Err("File is not a RWMOD file.");
            }
            if (!File.Exists(filePath)) {
                throw Err("No such file exists.");
            }
        }

        private static bool ShouldSkipEntry(ZipArchiveEntry entry, string outPath, bool mustBeExact)
        {
            string temp = Path.GetTempFileName();
            try {
                var existingFile = FileVersionInfo.GetVersionInfo(outPath);
                if (existingFile?.ProductVersion == null) return false;

                entry.ExtractToFile(temp);

                var newFile = FileVersionInfo.GetVersionInfo(temp);
                if (newFile?.ProductVersion == null) return true;

                if (mustBeExact)
                    return newFile.ProductMajorPart != existingFile.ProductMajorPart ||
                        newFile.ProductMinorPart != existingFile.ProductMinorPart ||
                        newFile.ProductBuildPart != existingFile.ProductBuildPart;
                else
                    return newFile.ProductMajorPart <= existingFile.ProductMajorPart ||
                        newFile.ProductMinorPart <= existingFile.ProductMinorPart ||
                        newFile.ProductBuildPart <= existingFile.ProductBuildPart;
            } catch {
                return true;
            } finally {
                File.Delete(temp);
            }
        }

        public static async Task Download(string filePath)
        {
            VerifyRwmodFile(filePath);

            using var input = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite);

            RwmodFileHeader header = RwmodFileHeader.ReadFrom(input);

            if (string.IsNullOrEmpty(header.RepositoryName)) {
                throw Err("Mod lacks a repository to fetch from.");
            }

            RepoFiles files = await GetFilesFromGitHubRepository(header.RepositoryAuthor, header.RepositoryName);

            if (files.Version > header.ModVersion.ToVersion()) {
                // Download new version of mod

                if (files.RwRoot) {
                    await DownloadWithProgress(await files.GetOnlineFileStream(0), input, Console.WriteLine);
                } else {
                    using var archive = new ZipArchive(input, ZipArchiveMode.Create, true, UseEncoding);

                    for (int i = 0; i < files.Count; i++) {
                        var entry = archive.CreateEntry(files.GetName(i));
                        using var entryStream = entry.Open();
                        var stream = await files.GetOnlineFileStream(i);

                        await DownloadWithProgress(stream, entryStream, Console.WriteLine);
                    }
                }

                // Update version
                input.Position = RwmodFileHeader.Position_Version;
                input.Write(new[] {
                    (byte)files.Version.Major,
                    (byte)files.Version.Minor,
                    (byte)files.Version.Build
                });
            }
        }

        public static async Task Update(string rwmodName, string filePath)
        {
            if (!File.Exists(filePath)) {
                throw Err("No such file.");
            }

            string rwmodPath = GetModPath(rwmodName);

            if (File.Exists(rwmodPath)) {
                using FileStream rwmodStream = File.Open(rwmodPath, FileMode.Open, FileAccess.ReadWrite);

                RwmodFileHeader.ReadFrom(rwmodStream);

                using ZipArchive archive = new(rwmodStream, ZipArchiveMode.Update, true, UseEncoding);

                string entryName = Path.GetFileName(filePath);

                ZipArchiveEntry? entry = archive.Entries.SingleOrDefault(e => e.Name == entryName);

                string? entryFullName = entry?.FullName;

                if (entry != null) {
                    entry.Delete();
                }

                entry = archive.CreateEntry(entryFullName ?? entryName, RwmodCompressionLevel);

                using Stream entryStream = entry.Open();
                using FileStream fs = File.OpenRead(filePath);
                await fs.CopyToAsync(entryStream);
            } else {
                Wrap(filePath);
                Include(Path.ChangeExtension(filePath, ".rwmod"));
            }
        }

        public static void Include(string filePath)
        {
            string path = Path.ChangeExtension(filePath, ".rwmod");

            if (!File.Exists(path)) {
                throw Err("No such RWMOD file.");
            }

            string rwmodsListingFolder = GetModsFolder().FullName;
            string newPath = Path.Combine(rwmodsListingFolder, Path.GetFileName(path));

            File.Move(path, newPath);
        }

        public static void Wrap(string filePath)
        {
            try {
                AssemblyName asm = AssemblyName.GetAssemblyName(filePath);
                using FileStream fs = File.Create(Path.ChangeExtension(filePath, ".rwmod"));

                WrapAssembly(filePath, asm, fs);
            } catch (BadImageFormatException) {
                try {
                    using ZipArchive archive = ZipFile.OpenRead(filePath);
                    using FileStream fs = File.Create(Path.ChangeExtension(filePath, ".rwmod"));

                    WrapZip(filePath, archive, fs);
                } catch (InvalidDataException) {
                    throw Err("File is not a .NET assembly or a ZIP file.");
                }
            }
        }

        private static void WrapAssembly(string filePath, AssemblyName asm, FileStream fs)
        {
            RwmodFileHeader
                .Create(modVersion: new(asm.Version ?? new(0, 0, 1)),
                        repositoryName: "",
                        repositoryAuthor: "wrapper",
                        displayName: asm.Name ?? Path.GetFileNameWithoutExtension(filePath),
                        modDependencies: new())
                .WriteTo(fs);

            using var archive = new ZipArchive(fs, ZipArchiveMode.Create, true);
            archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath), RwmodCompressionLevel);
        }

        private static void WrapZip(string filePath, ZipArchive inputArchive, FileStream fs)
        {
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(filePath);

            RwmodFileHeader
                .Create(modVersion: new((byte)versionInfo.ProductMajorPart, (byte)versionInfo.ProductMinorPart, (byte)versionInfo.ProductBuildPart),
                        repositoryName: "",
                        repositoryAuthor: "wrapper",
                        displayName: Path.GetFileNameWithoutExtension(filePath),
                        modDependencies: new())
                .WriteTo(fs);

            using var archive = new ZipArchive(fs, ZipArchiveMode.Create, true);
            foreach (var entry in inputArchive.Entries) {
                archive.CreateEntry(entry.Name, RwmodCompressionLevel);
            }
        }

        public static async Task Unwrap(string filePath)
        {
            VerifyRwmodFile(filePath);

            using var rwmodFile = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite);

            RwmodFileHeader.ReadFrom(rwmodFile);

            using var archive = new ZipArchive(rwmodFile, ZipArchiveMode.Read, true, UseEncoding);

            if (archive.Entries.Count == 0) {
                throw Err("Nothing to unpack.");
            }

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
                    if (!ShouldSkipEntry(entry, outPath, false)) {
                        string relative = Path.GetRelativePath(RwDir, outPath);
                        string restoration = Path.Combine(GetRestorationFolder().FullName, relative);

                        if (!File.Exists(restoration))
                            File.Copy(outPath, restoration);
                    }
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

            RwmodFileHeader.ReadFrom(rwmodFile);

            using var archive = new ZipArchive(rwmodFile, ZipArchiveMode.Read, true, UseEncoding);

            archive.ExtractToDirectory(Directory.CreateDirectory(directoryNameSafe).FullName);
        }

        public static void Restore(string filePath)
        {
            VerifyRwmodFile(filePath);

            string rwDirectory = RwDir;

            using var input = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite);

            long offset;

            using (var reader = new BinaryReader(input, UseEncoding, true))
                offset = reader.ReadInt64();

            input.Position = offset;

            using var archive = new ZipArchive(input, ZipArchiveMode.Read, true, UseEncoding);

            if (archive.Entries.Count == 0) {
                throw Err("Nothing to unpack.");
            }

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
                string restoration = Path.Combine(GetRestorationFolder().FullName, relative);

                if (File.Exists(restoration))
                    File.Move(restoration, outPath, true);
                else
                    File.Delete(outPath);
            }
        }
    }
}