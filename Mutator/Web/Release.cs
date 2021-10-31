using Mutator.IO;
using System.Net;

namespace Mutator.Web;

sealed class Release
{
    private readonly IList<string> uris;

    public string Body { get; }
    public RwmodVersion Version { get; }

    public Release(IList<string> uris, RwmodVersion version, string body)
    {
        this.uris = uris;
        Body = body;
        Version = version;
    }

    public int Count => uris.Count;

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

    public async Task<Result<Stream, HttpStatusCode>> GetOnlineFileStream(int index)
    {
        var response = await ExtWeb.Client.GetAsync(uris[index]);

        if (response.IsSuccessStatusCode) {
            return await response.Content.ReadAsStreamAsync();
        }

        return response.StatusCode;
    }
}
