using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public static string RwDir => rwDir ??= GetRwDirectory();

        private static HttpClient? client;
        public static HttpClient Client => client ??= new();

        public static Encoding UseEncoding => Encoding.ASCII;

        internal static void Dispose()
        {
            client?.Dispose();
        }

        private static string GetRwDirectory()
        {
            const int AppID = 312520;

            var currentDir = new DirectoryInfo(Environment.CurrentDirectory);
            do {
                if (currentDir.Name == "Rain World") {
                    return currentDir.FullName;
                }
                currentDir = currentDir.Parent;
            }
            while (currentDir != null);

            if (OperatingSystem.IsWindows()) {
                object? value =
                    Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) ??
                    Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", null);

                if (value is string steamPath) {
                    string appState = File.ReadAllText(Path.Combine(steamPath, "steamapps", $"appmanifest_{AppID}.acf"));
                    var installNameMatch = Regex.Match(appState, @"""installdir""\s*""(.*?)""");
                    if (installNameMatch.Success && installNameMatch.Groups.Count == 2) {
                        string installName = installNameMatch.Groups[1].Value;
                        return Path.Combine(steamPath, "steamapps", "common", installName);
                    }
                }
            }

            throw Err("Could not find the Rain World directory. Move the installer into the \"Rain World\" folder.");
        }

        /// <summary>
        /// Returns an exception.
        /// </summary>
        public static Exception Err(string err)
        {
            return new Exception(err);
        }

        public static DirectoryInfo GetRwmodsUserFolder()
        {
            string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".rw");

            if (!Directory.Exists(directoryPath)) {
                var path = Directory.CreateDirectory(directoryPath);
                path.Attributes |= FileAttributes.Hidden;
                return path;
            }

            return Directory.CreateDirectory(directoryPath);
        }

        public static DirectoryInfo GetModsFolder()
        {
            return GetRwmodsUserFolder().CreateSubdirectory("mods");
        }

        public static string GetModPath(string name)
        {
            return Path.Combine(GetModsFolder().FullName, Path.ChangeExtension(name, ".rwmod"));
        }

        public static DirectoryInfo GetRestorationFolder()
        {
            return GetRwmodsUserFolder().CreateSubdirectory("restore");
        }

        public static DirectoryInfo GetPatchBackupsFolder()
        {
            return GetRwmodsUserFolder().CreateSubdirectory("patch-backups");
        }

        // Checks for a valid internet connection.
        // The error, or null for success.
        public static string? CheckForInternetConnection()
        {
            try {
                using var response = Client.Send(new HttpRequestMessage(HttpMethod.Get, "http://google.com/generate_204"));
                if (response.IsSuccessStatusCode)
                    return null;
                return Task.Run(response.Content.ReadAsStringAsync).Result;
            } catch (WebException e) {
                return e.Message;
            }
        }

        public static async Task<RepoFiles> GetFilesFromGitHubRepository(string user, string repo)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{user}/{repo}/releases/latest");
            request.Headers.Add("Accept", "application/vnd.github.v3+json");
            request.Headers.Add("User-Agent", "downloader-" + Environment.ProcessId);

            string message = await GetMessage(request);

            var versionMatch = Regex.Match(message, @"""tag_name"":""(.+?)""");

            if (!versionMatch.Success || !Version.TryParse(versionMatch.Groups[1].Value.Trim('v', 'V'), out var version)) {
                throw Err("Latest release had a tag that was not a simple version.");
            }

            // Fetch the download link
            var downloadUriMatch = Regex.Match(message, @"""browser_download_url"":""(.+?)""");

            List<string> uris = new();

            while (downloadUriMatch.Success) {
                uris.Add(GetUri(downloadUriMatch));

                downloadUriMatch = downloadUriMatch.NextMatch();
            }

            if (uris.Count == 0) {
                throw Err("Latest release contained no binaries.");
            }

            return new(uris, version);

            static async Task<string> GetMessage(HttpRequestMessage request)
            {
                try {
                    return await (await Client.SendAsync(request)).EnsureSuccessStatusCode().Content.ReadAsStringAsync();
                } catch (HttpRequestException e) {
                    if (e.StatusCode == HttpStatusCode.NotFound) {
                        throw Err("GitHub repository did not exist, or it had no full releases. " + e.Message);
                    }
                    throw Err("Connecting to GitHub API failed. " + e.Message);
                }
            }

            static string GetUri(Match downloadUriMatch)
            {
                if (!downloadUriMatch.Success) {
                    throw Err($"GitHub repository's latest non-pre-release release did not have an asset.");
                }
                if (downloadUriMatch.Groups.Count != 2) {
                    throw Err("Fetching the download link did not result in exactly one match.");
                }
                return downloadUriMatch.Groups[1].Value;
            }
        }

        public static async Task<Stream> GetStreamFromGitHubRepository(string user, string repo)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{user}/{repo}/releases/latest");
            request.Headers.Add("Accept", "application/vnd.github.v3+json");
            request.Headers.Add("User-Agent", "downloader-" + Environment.ProcessId);

            string message = await GetMessage(request);

            // Fetch the download link
            var downloadUriMatch = Regex.Match(message, @"""browser_download_url"":""(.+?)""");

            // Get download stream
            return await GetDownloadStream(GetUri(downloadUriMatch));

            static async Task<string> GetMessage(HttpRequestMessage request)
            {
                try {
                    return await (await Client.SendAsync(request)).EnsureSuccessStatusCode().Content.ReadAsStringAsync();
                } catch (HttpRequestException e) {
                    if (e.StatusCode == HttpStatusCode.NotFound) {
                        throw Err("GitHub repository did not exist, or it had no full releases. " + e.Message);
                    }
                    throw Err("Connecting to GitHub API failed. " + e.Message);
                }
            }

            static string GetUri(Match downloadUriMatch)
            {
                if (!downloadUriMatch.Success) {
                    throw Err($"GitHub repository's latest non-pre-release release did not have an asset.");
                }
                if (downloadUriMatch.Groups.Count != 2) {
                    throw Err("Fetching the download link did not result in exactly one match.");
                }
                return downloadUriMatch.Groups[1].Value;
            }

            static async Task<Stream> GetDownloadStream(string downloadUri)
            {
                try {
                    return await (await Client.GetAsync(downloadUri)).EnsureSuccessStatusCode().Content.ReadAsStreamAsync();
                } catch (HttpRequestException e) {
                    throw Err("Fetching the download file stream failed. " + e.Message);
                }
            }
        }

        /// <summary>
        /// Downloads a .zip stream and unzips it into the specified directory.
        /// </summary>
        /// <param name="download">The stream to download from.</param>
        /// <param name="outputDirectory">The directory to unzip the archive into.</param>
        /// <param name="progressCopying">Fired every time progress updates, with a parameter ranging from 0-1 representing download progress.</param>
        public static async Task DownloadWithProgressAndUnzip(Stream download, string outputDirectory, Action<float>? progressCopying = null)
        {
            string tempFilePath = Path.GetTempFileName();

            using (var fileStream = new FileStream(tempFilePath, FileMode.Create))
                await DownloadWithProgress(download, fileStream, progressCopying);

            // Extract zip
            Process process = GetProcess(tempFilePath, outputDirectory);
            if (!process.WaitForExit(5000)) {
                process.Kill();
                throw Err($"Timed out while extracting zip archive. " + process.ExitCode);
            }

            if (process.ExitCode != 0) {
                throw Err("Extracting zip archive failed with error code " + process.ExitCode + ".");
            }

            File.Delete(tempFilePath);

            static Process GetProcess(string filePath, string output)
            {
                try {
                    ProcessStartInfo startInfo = new("powershell") {
                        CreateNoWindow = false,
                    };
                    startInfo.ArgumentList.Add("Expand-Archive");
                    startInfo.ArgumentList.Add("-Force");
                    startInfo.ArgumentList.Add('"' + filePath + '"');
                    startInfo.ArgumentList.Add('"' + output + '"');
                    return Process.Start(startInfo) ?? throw Err("Process was null!");
                } catch (Exception e) {
                    throw Err(e.Message);
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
                progressCopying?.Invoke((float)totalRead / download.Length);
            }
        }
    }
}
