using Mutator.IO;

namespace Mutator;

public sealed class GitHubRelease
{
    private readonly IList<string> uris;

    public GitHubRelease(IList<string> uris, RwmodVersion version, string body)
    {
        this.uris = uris;
        Body = body;
        Version = version;
    }

    public int Count => uris.Count;

    public RwmodVersion Version { get; }
    public string Body { get; }

    public string GetUri(int index) => uris[index];

    public string GetName(int index)
    {
        string uri = uris[index];

        for (int i = uri.Length - 1; i >= 0; i--) {
            if (uri[i] == '/') {
                return uri[(i + 1)..];
            }
        }
        return uri;
    }

    public async Task<Stream> GetOnlineFileStream(int index)
    {
        return await (await Client.GetAsync(uris[index])).EnsureSuccessStatusCode().Content.ReadAsStreamAsync();
    }
}
