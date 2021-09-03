using Mutator.Packaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mutator
{
    public static class ModList
    {
        private static List<RaindbMod> mods = null!;

        public static async Task<ReadOnlyCollection<RaindbMod>> GetMods()
        {
            if (mods == null) {
                mods = new();
                await Setup();
            }
            return new(mods);
        }

        private static async Task Setup()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://raw.githubusercontent.com/AndrewFM/RainDB/master/index.html");
            request.Headers.Add("Accept", "text/plain");
            request.Headers.Add("User-Agent", "downloader-" + Environment.ProcessId);

            using var response = await InstallerApi.Client.SendAsync(request);

            try {
                response.EnsureSuccessStatusCode();
            } catch (HttpRequestException e) {
                if (e.StatusCode == HttpStatusCode.NotFound) {
                    throw new("The RainDB GitHub repository did not exist. " + e.Message);
                }
                throw new("Connecting to GitHub API failed. " + e.Message);
            }

            string content = await response.Content.ReadAsStringAsync();

            const string firstPhrase = "// TOOLS and MODDING UTILITIES";
            const string secondPhrase = "// VERSION 1.5 MODS";
            const string lastPhrase = "// VERSION 1.01 MODS";

            int position = content.IndexOf(firstPhrase);
            if (position == -1) {
                throw new("Couldn't find tools and utilities section on RainDB.");
            }

            int nextPosition = content.IndexOf(secondPhrase, position);
            if (nextPosition == -1) {
                throw new("Couldn't find v1.5 mods section on RainDB.");
            }

            int lastPosition = content.IndexOf(lastPhrase, nextPosition);
            if (lastPosition == -1) {
                throw new("Couldn't find v1.01 mods section on RainDB.");
            }

            position += firstPhrase.Length;
            nextPosition += secondPhrase.Length;

            ConstructMods(content, ref position, nextPosition);
            ConstructMods(content, ref nextPosition, lastPosition);
        }

        private static List<RaindbMod> ConstructMods(string content, ref int position, int nextPosition)
        {
            var fields = new Dictionary<string, string>();

            int key = 0, keyEnd = 0, value = 0;

            while (++position < nextPosition) {
                // Find JSON quotes, mark down everything in-between
                if (content[position] == '"') {
                    if (key == 0)
                        key = position + 1;
                    else if (keyEnd == 0)
                        keyEnd = position;
                    else if (value == 0)
                        value = position + 1;
                    else {
                        fields[content[key..keyEnd]] = content[value..position];
                        key = keyEnd = value = 0;
                    }
                } else if (content[position] == '\n') {
                    if (keyEnd > key)
                        fields[content[key..keyEnd]] = "";
                    key = keyEnd = value = 0;
                } else if (content[position] == '}') {
                    AddMod();
                }
            }

            return mods;

            void AddMod()
            {
                if (fields.ContainsKey("mod") &&
                    fields.TryGetValue("type", out var type) && !type.Contains("Region") && !type.Contains("Map Edits") &&
                    fields.TryGetValue("url", out var url) &&
                    fields.TryGetValue("name", out var name) && !Packager.ModBlacklist.Contains(name) &&
                    fields.TryGetValue("desc", out var desc)) {
                    var mod = new RaindbMod {
                        Url = url,
                        Name = name,
                        Description = desc
                    };

                    var repoMatch = Regex.Match(url, @"github\.com/([A-Za-z0-9_.\-~]+)/([A-Za-z0-9_.\-~]+)");
                    if (repoMatch.Success) {
                        // Github repo!
                        mod.Author = repoMatch.Groups[1].Value;
                        mod.Repo = repoMatch.Groups[2].Value;
                    } else {
                        // Not a Github repo
                        fields.TryGetValue("author", out mod.Author);
                    }

                    if (fields.TryGetValue("thumb", out var thumb))
                        mod.IconUrl = "https://raindb.net/" + thumb;

                    fields.TryGetValue("video", out mod.VideoUrl);

                    if (fields.TryGetValue("req", out var req)) {
                        var reqMatch = Regex.Match(req, @"Requires (.+) to be installed.");
                        if (reqMatch.Success && reqMatch.Groups[1].Success) {
                            var split = reqMatch.Groups[1].Value.Split(new[] { ", ", "and ", " and " }, 100, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var modReq in split)
                                if (Packager.ModBlacklist.Contains(modReq))
                                    mod.ModDependencies += modReq + ";";
                        }
                    }

                    mods.Add(mod);
                }
                fields.Clear();
            }
        }
    }
}
