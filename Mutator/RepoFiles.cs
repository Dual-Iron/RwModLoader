using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Mutator
{
    public sealed class RepoFiles
    {
        private readonly IList<string> uris;

        public RepoFiles(IList<string> uris, Version version)
        {
            this.uris = uris;
            Version = new(version.Major, version.Minor, version.Build, 0);
        }

        public bool RwRoot => uris.Count == 1 && uris[0].EndsWith("/Rain World.zip");

        public int Count => uris.Count;

        public Version Version { get; }

        public string GetUri(int index) => uris[index];

        public string GetName(int index)
        {
            string uri = uris[index];

            for (int i = uris.Count - 1; i >= 0; i--) {
                if (uri[i] == '/') {
                    return uri[(i + 1)..];
                }
            }
            return uri;
        }

        public async Task<Stream> GetOnlineFileStream(int index)
        {
            try {
                return await (await InstallerApi.Client.GetAsync(uris[index])).EnsureSuccessStatusCode().Content.ReadAsStreamAsync();
            } catch (HttpRequestException e) {
                throw InstallerApi.Err("Fetching the download file stream failed. " + e.Message);
            }
        }
    }
}
