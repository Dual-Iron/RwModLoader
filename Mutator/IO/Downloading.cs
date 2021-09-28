using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Mutator.IO;

public class Downloading
{
    public static Match GetModType(string body)
    {
        return Regex.Match(body, @"(mod|plugin|patcher)\s+for\s+realm", RegexOptions.IgnoreCase);
    }

    public static async Task Download(string url)
    {
        // TODO: Detect incompatibilities & dependencies

        await VerifyInternetConnection();

        string[] args = url.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (args.Length != 2) {
            throw Err(ExitCodes.InvalidArgs);
        }

        GitHubRelease release = await GetRelease(args[0], args[1]);

        // https://gist.github.com/Dual-Iron/35b71cdd5ffad8b5ad65a3f7214af390#creating-rwmod-files-from-a-github-repository

        if (release.Count == 0) {
            throw Err(ExitCodes.AbsentBinaries);
        }

        Match typeMatch = GetModType(release.Body);

        RwmodFlags flags;

        if (!typeMatch.Success)
#if IGNORE_COMPLIANCE
            flags = RwmodFlags.Mod;
#else
            throw Err(ExitCodes.RepoNotCompliant);
#endif
        else if (typeMatch.Groups[1].Value == "mod" || typeMatch.Groups[1].Value == "plugin")
            flags = RwmodFlags.Mod;
        else
            flags = RwmodFlags.Patcher;

        string localModPath = GetModPath(args[1]);

        RwmodVersion localModVersion = default;

        if (File.Exists(localModPath)) {
            using Stream existingMod = File.OpenRead(localModPath);
            localModVersion = RwmodFileHeader.Read(existingMod).ModVersion;
        }

        if (release.Version.ToVersion() <= localModVersion.ToVersion()) {
            return;
        }

        string tempDir = Path.GetTempFileName();

        File.Delete(tempDir);

        try {
            Directory.CreateDirectory(tempDir);

            if (release.Count == 1 && release.GetName(0).EndsWith(".zip")) {
                using Stream zipStream = await release.GetOnlineFileStream(0);
                using ZipArchive zip = new(zipStream, ZipArchiveMode.Read, true, UseEncoding);
                zip.ExtractToDirectory(tempDir);

                foreach (var file in Directory.EnumerateFiles(tempDir)) {
                    if (!file.EndsWith(".dll")) {
                        File.Delete(file);
                    }
                }
            } else {
                for (int i = 0; i < release.Count; i++) {
                    string entryName = release.GetName(i);
                    if (!entryName.EndsWith(".dll")) {
                        continue;
                    }

                    using Stream entryLocal = File.Create(Path.Combine(tempDir, entryName));
                    using Stream entryOnline = await release.GetOnlineFileStream(i);
                    await entryOnline.CopyToAsync(entryLocal);
                }
            }

            using (Stream output = File.Create(localModPath)) {
                new RwmodFileHeader(args[1], args[0]) {
                    Flags = flags,
                    DisplayName = args[1],
                    ModVersion = release.Version
                }.Write(output);
            }

            try {
                await Wrapper.Wrap(args[1], tempDir, true);
            } catch {
                File.Delete(localModPath);
                throw;
            }
        } finally {
            Directory.Delete(tempDir, true);
        }
    }
}
