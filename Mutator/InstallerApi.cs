﻿using Microsoft.Win32;
using Mutator.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Mutator;

public static partial class InstallerApi
{
    private static string? rwDir;
    public static string RwDir => rwDir ??= GetRwDir();

    private static string GetRwDir()
    {
        const int AppID = 312520;

        // Check for explicit path override
        if (File.Exists("path.txt")) {
            if (File.ReadLines("path.txt").FirstOrDefault() is string firstLine) {
                var path = Path.GetFullPath(firstLine.Trim());
                if (path.Length > 0 && Directory.Exists(path) && File.Exists(Path.Combine(path, "RainWorld.exe"))) {
                    return path;
                }
            }
            throw Err(ExitCodes.AbsentRainWorldFolder, $"The \"path.txt\" file in \"{Environment.CurrentDirectory}\" is invalid. The first line should be the path to your Rain World folder.");
        }

        // Check simple, common paths
        string[] commonPaths = new[] {
            @"C:\Program Files (x86)\Steam\steamapps\common\Rain World",
            @"C:\Program Files\Steam\steamapps\common\Rain World"
        };

        foreach (var path in commonPaths) {
            if (Directory.Exists(path)) {
                return path;
            }
        }

        // Find path rigorously
        if (OperatingSystem.IsWindows()) {
            object? value =
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) ??
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", null);

            if (value is string steamPath) {
                string appState = File.ReadAllText(Path.Combine(steamPath, "steamapps", $"appmanifest_{AppID}.acf"));
                var installNameMatch = Regex.Match(appState, @"""installdir""\s*""(.*?)""");
                if (installNameMatch.Success && installNameMatch.Groups.Count == 2) {
                    string installName = installNameMatch.Groups[1].Value;
                    string path = Path.Combine(steamPath, "steamapps", "common", installName);

                    if (Directory.Exists(path)) {
                        File.WriteAllText("path.txt", path + "\n");
                        return path;
                    }
                }
            }
        }

        throw Err(ExitCodes.AbsentRainWorldFolder, $"Could not find the Rain World directory. Please add a \"path.txt\" file to \"{Environment.CurrentDirectory}\" that has the path your Rain World folder.");
    }

    private static HttpClient? client;
    public static HttpClient Client => client ??= new();

    public static Encoding UseEncoding => Encoding.ASCII;
    public const CompressionLevel RwmodCompressionLevel = CompressionLevel.NoCompression;

    public static readonly IEnumerable<string> ModBlacklist = new[] { "EnumExtender", "PublicityStunt", "AutoUpdate", "LogFix", "BepInEx-Partiality-Wrapper" };

    internal static void Dispose()
    {
        client?.Dispose();
    }

    private static DirectoryInfo? rwmodUserFolder;

    public static DirectoryInfo RwmodsUserFolder => rwmodUserFolder ??= Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".rw"));

    public static DirectoryInfo ModsFolder => RwmodsUserFolder.CreateSubdirectory("mods");

    public static DirectoryInfo PatchBackupsFolder => RwmodsUserFolder.CreateSubdirectory("patch-backups");

    public static string GetModPath(string name) => Path.Combine(ModsFolder.FullName, Path.ChangeExtension(name, ".rwmod"));

    public static async Task<GitHubRelease> GetRelease(string user, string repo)
    {
        string message = await FetchFromCache($"body-{user}-{repo}", () => {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{user}/{repo}/releases/latest");
            request.Headers.Add("Accept", "application/vnd.github.v3+json");
            request.Headers.Add("User-Agent", "downloader-" + Environment.ProcessId);
            return GetMessage(request);
        });

        var versionMatch = Regex.Match(message, @"""tag_name"":""(.+?)""");

        if (!versionMatch.Success || !RwmodVersion.TryParse(versionMatch.Groups[1].Value, out var version)) {
            throw Err(ExitCodes.InvalidReleaseTag, $"{user}/{repo}");
        }

        // Fetch the download link
        Match downloadUriMatch = Regex.Match(message, @"""browser_download_url"":""(.+?)""");
        List<string> uris = new();

        while (downloadUriMatch.Success) {
            uris.Add(downloadUriMatch.Groups[1].Value);

            downloadUriMatch = downloadUriMatch.NextMatch();
        }

        Match descriptionMatch = Regex.Match(message, @"""body"":""(.+?)""");
        string description = descriptionMatch.Success ? descriptionMatch.Groups[1].Value : "";

        return new(uris, version, description);

        async Task<string> GetMessage(HttpRequestMessage request)
        {
            try {
                using var httpResponseMessage = await Client.SendAsync(request);
                return await httpResponseMessage.EnsureSuccessStatusCode().Content.ReadAsStringAsync();
            } catch (HttpRequestException e) {
                if (e.StatusCode == HttpStatusCode.NotFound) {
                    throw Err(ExitCodes.AbsentRelease, $"{user}/{repo}");
                }
                throw;
            }
        }
    }

    public static async Task<string> FetchFromCache(string key, Func<Task<string>> getValue)
    {
        // ([string: key][string: value])*

        FileInfo webcache = new(Path.Combine(RwmodsUserFolder.FullName, "webcache.dat"));

        if (webcache.CreationTimeUtc < DateTime.UtcNow - new TimeSpan(hours: 1, 0, 0)) {
            webcache.Delete();
        }

        using Stream fs = webcache.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite);

        try {
            using BinaryReader reader = new(fs, UseEncoding, true);
            while (fs.Position < fs.Length) {
                string entryKey = reader.ReadString();
                string entryValue = reader.ReadString();

                if (key == entryKey) {
                    return entryValue;
                }
            }
        } catch (EndOfStreamException e) {
            Console.Error.WriteLine("Corrupt webcache! " + e.Message);
            fs.SetLength(0);
        }

        string value = await getValue();

        using BinaryWriter writer = new(fs, UseEncoding, true);
        writer.Write(key);
        writer.Write(value);

        return value;
    }
}
