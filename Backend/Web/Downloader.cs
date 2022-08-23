using Backend.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backend.Web;

static class Downloader
{
    public static async Task<ExitStatus> RdbList(int page, string search)
    {
        try {
            string searchQuery = search == "" ? "" : $"&search={Uri.EscapeDataString(search)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://rdb.dual-iron.xyz/mods?page={page}{searchQuery}");
            using var response = await ExtWeb.Client.SendAsync(request);

            Console.WriteLine(await response.Content.ReadAsStringAsync());

            return ExitStatus.Success;
        }
        catch (HttpRequestException e) {
            return ExitStatus.ConnectionFailed(e.Message);
        }
    }

    public static async Task<ExitStatus> Audb()
    {
        try {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://beestuff.pythonanywhere.com/audb/api/v2/rdb");
            using var response = await ExtWeb.Client.SendAsync(request);

            Console.WriteLine(await response.Content.ReadAsStringAsync());

            return ExitStatus.Success;
        }
        catch (HttpRequestException e) {
            return ExitStatus.ConnectionFailed(e.Message);
        }
    }

    public static async Task<ExitStatus> Download(string url, string file)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await ExtWeb.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        using var content = response.Content;

        if (!response.IsSuccessStatusCode) {
            return ExitStatus.ConnectionFailed($"({response.StatusCode}) {await content.ReadAsStringAsync()}");
        }

        using (var o = File.Create(file))
        using (var i = await content.ReadAsStreamAsync())
            await i.CopyToWithProgress(o, content.Headers.ContentLength!.Value, ExtWeb.PrintProgress);

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

            RdbEntry doc;
            try {
                using var stream = await content.ReadAsStreamAsync();

                doc = JsonSerializer.Deserialize(stream, SourceGenerationContext.Default.RdbEntry) ?? throw new();
            }
            catch {
                // Failed to read JSON so we're probably using an outdated client.
                return ExitStatus.OutdatedClient;
            }

            return await Rdb(doc);
        }
        catch (HttpRequestException e) {
            return ExitStatus.ConnectionFailed(e.Message);
        }
    }

    static async Task<ExitStatus> Rdb(RdbEntry entry)
    {
        if (SemVer.Parse(entry.version) is not SemVer ver) {
            return ExitStatus.InvalidVersion;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, entry.binary);
        using var response = await ExtWeb.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        using var content = response.Content;

        if (!response.IsSuccessStatusCode) {
            return ExitStatus.ConnectionFailed($"{response.StatusCode}: {await content.ReadAsStringAsync()}");
        }

        using var o = new TempFile();
        using (var i = await content.ReadAsStreamAsync())
            await i.CopyToWithProgress(o.Stream, content.Headers.ContentLength!.Value, ExtWeb.PrintProgress);

        o.Stream.Dispose();

        return Wrapper.Wrap(o.Path, () => new(RwmodHeader.FileFlags.RdbEntry, ver, entry.name, entry.owner, entry.homepage));
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
