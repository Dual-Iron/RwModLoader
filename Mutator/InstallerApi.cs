using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mutator
{

    public static class InstallerApi
    {
        private static string? rwDir;
        public static string RwDir {
            get {
                if (rwDir != null) {
                    return rwDir;
                }

                const int AppID = 312520;

                // Check simple, common path
                string samplePath = Path.GetFullPath(@"C:\Program Files (x86)\Steam\steamapps\common\Rain World");
                if (Directory.Exists(samplePath)) {
                    return samplePath;
                }

                // Use parent directory if it exists
                var currentDir = new DirectoryInfo(Environment.CurrentDirectory);
                do {
                    if (currentDir.Name == "Rain World") {
                        return currentDir.FullName;
                    }
                }
                while ((currentDir = currentDir.Parent) != null);

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
                            return rwDir = Path.Combine(steamPath, "steamapps", "common", installName);
                        }
                    }
                }

                throw new("Could not find the Rain World directory.");
            }
        }

        private static HttpClient? client;
        public static HttpClient Client => client ??= new();

        public static Encoding UseEncoding => Encoding.ASCII;

        internal static void Dispose()
        {
            client?.Dispose();
        }

        private static DirectoryInfo? rwmodUserFolder;

        public static DirectoryInfo RwmodsUserFolder => rwmodUserFolder ??= Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".rw"));

        public static DirectoryInfo ModsFolder => RwmodsUserFolder.CreateSubdirectory("mods");

        public static string GetModPath(string name) => Path.Combine(ModsFolder.FullName, Path.ChangeExtension(name, ".rwmod"));

        public static DirectoryInfo RestorationFolder(string rwmod) => RwmodsUserFolder.CreateSubdirectory("restore").CreateSubdirectory(rwmod);

        public static DirectoryInfo PatchBackupsFolder => RwmodsUserFolder.CreateSubdirectory("patch-backups");

        private static bool hasInternet;

        public static async Task VerifyInternetConnection()
        {
            if (hasInternet) return;

            try {
                (await Client.GetAsync("http://google.com/generate_204")).EnsureSuccessStatusCode();
            } catch {
                throw new("No internet connection.");
            }

            hasInternet = true;
        }

        public static async Task<RepoFiles> GetFilesFromGitHubRepository(string user, string repo)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{user}/{repo}/releases/latest");
            request.Headers.Add("Accept", "application/vnd.github.v3+json");
            request.Headers.Add("User-Agent", "downloader-" + Environment.ProcessId);

            string message = await GetMessage(request);

            var versionMatch = Regex.Match(message, @"""tag_name"":""(.+?)""");

            if (!versionMatch.Success || !Version.TryParse(versionMatch.Groups[1].Value.TrimStart('v', 'V'), out var version)) {
                throw new("Latest release had a tag that was not a simple version.");
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

            static async Task<string> GetMessage(HttpRequestMessage request)
            {
                try {
                    return await (await Client.SendAsync(request)).EnsureSuccessStatusCode().Content.ReadAsStringAsync();
                } catch (HttpRequestException e) {
                    if (e.StatusCode == HttpStatusCode.NotFound) {
                        throw new("GitHub repository does not exist, or it has no full releases. " + e.Message);
                    }
                    throw new("Connecting to GitHub API failed. " + e.Message);
                }
            }
        }

        /// <summary>
        /// Downloads a file with progress reporting.
        /// </summary>
        /// <param name="download">The stream to download from.</param>
        /// <param name="progressCopying">Fired every time progress updates, with a parameter ranging from 0-1 representing download progress.</param>
        /// <returns>The path to the downloaded file.</returns>
        public static async Task DownloadWithProgress(Stream download, Stream output, Action<float>? progressCopying)
        {
            byte[] buffer = new byte[81920];
            int totalRead = 0;
            int read;
            while ((read = await download.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0) {
                await output.WriteAsync(buffer.AsMemory(0, read));

                totalRead += read;
                progressCopying?.Invoke(totalRead / (float)download.Length);
            }
        }
    }
}
