﻿using System.Text.Json;

namespace Backend.Web;

static class SelfUpdater
{
    private static async Task<Result<SemVer, ExitStatus>> GetRepoVersion(string user, string repo)
    {
        var result = await Cache.Fetch<ExitStatus>($"tagname-{user}-{repo}", async () => {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{user}/{repo}/releases/latest");
            request.Headers.Add("Accept", "application/vnd.github.v3+json");
            request.Headers.Add("User-Agent", "downloader-" + Environment.ProcessId);

            using var response = await ExtWeb.Client.SendAsync(request);
            using var content = response.Content;

            if (!response.IsSuccessStatusCode) {
                return ExitStatus.ConnectionFailed(await content.ReadAsStringAsync());
            }

            var doc = await JsonDocument.ParseAsync(await content.ReadAsStreamAsync());
            var tagName = doc.RootElement.GetProperty("tag_name").GetString();

            if (tagName == null)
                return ExitStatus.InvalidVersion;

            return tagName;
        });

        if (result.MatchFailure(out var tagName, out var code)) {
            return code;
        }

        var version = SemVer.Parse(tagName);
        if (version == null) {
            return ExitStatus.InvalidVersion;
        }

        return version.Value;
    }

    public static async Task<ExitStatus> QuerySelfUpdate()
    {
        try {
            var result = await GetRepoVersion("Dual-Iron", "RwModLoader");

            if (result.MatchFailure(out var remoteVersion, out var code)) {
                return code;
            }

            SemVer localVersion = new(typeof(SelfUpdater).Assembly.GetName().Version!);

            bool needs = remoteVersion > localVersion;

            Console.Write(needs ? 'y' : 'n');

            return ExitStatus.Success;
        }
        catch (Exception e) {
            return ExitStatus.ConnectionFailed(e.Message);
        }
    }
}
