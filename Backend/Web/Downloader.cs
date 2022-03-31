﻿using Backend.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backend.Web;

static class Downloader
{
    public static async Task<ExitStatus> Download(string url, string file)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await ExtWeb.Client.SendAsync(request);
        using var content = response.Content;

        if (!response.IsSuccessStatusCode) {
            return ExitStatus.ConnectionFailed($"({response.StatusCode}) {await content.ReadAsStringAsync()}");
        }

        using (var outStream = File.Create(file))
        using (var inStream = await content.ReadAsStreamAsync())
            await inStream.CopyToAsync(outStream);

        return ExitStatus.Success;
    }

    public static async Task<ExitStatus> Rdb(string fullname)
    {
        try {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://rdb.dual-iron.xyz/mods/{fullname}");
            using var response = await ExtWeb.Client.SendAsync(request);
            using var content = response.Content;

            if ((int)response.StatusCode >= 500) {
                return ExitStatus.ServerError(response.StatusCode.ToString());
            }
            if (!response.IsSuccessStatusCode) {
                return ExitStatus.ConnectionFailed(response.StatusCode.ToString());
            }

            using var stream = await content.ReadAsStreamAsync();

            RdbEntry? doc = Try(() => JsonSerializer.Deserialize(stream, SourceGenerationContext.Default.RdbEntry),
                                   _ => null);

            return doc == null ? ExitStatus.OutdatedClient : await Rdb(doc);
        }
        catch (HttpRequestException e) {
            return ExitStatus.ConnectionFailed(e.Message);
        }
    }

    static async Task<ExitStatus> Rdb(RdbEntry entry)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, entry.binary);
        using var response = await ExtWeb.Client.SendAsync(request);
        using var content = response.Content;

        if (!response.IsSuccessStatusCode) {
            return ExitStatus.ConnectionFailed($"{response.StatusCode}; {await content.ReadAsStringAsync()}");
        }

        string path = Path.Combine(ExtIO.TempFolder.FullName, Path.GetRandomFileName());

        using var deleteTemp = new Disposable(() => File.Delete(path));

        using (var outStream = File.Create(path))
        using (var inStream = await content.ReadAsStreamAsync())
            await inStream.CopyToAsync(outStream);

        if (SemVer.Parse(entry.version) is not SemVer ver) {
            return ExitStatus.InvalidVersion;
        }

        return Wrapper.Wrap(path, () => new(RwmodHeader.FileFlags.IsRdbEntry, ver, entry.name, entry.owner, entry.homepage));
    }

    static TOut Try<TOut>(Func<TOut> @try, Func<Exception, TOut> @catch)
    {
        try {
            return @try();
        }
        catch (Exception e) {
            return @catch(e);
        }
    }
}

sealed class RdbEntry
{
    public string name = "";
    public string owner = "";
    public int updated;
    public int downloads;
    public string description = "";
    public string homepage = "";
    public string version = "";
    public string icon = "";
    public string binary = "";
}

[JsonSourceGenerationOptions(IncludeFields = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RdbEntry))]
[JsonSerializable(typeof(RdbEntry[]))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}