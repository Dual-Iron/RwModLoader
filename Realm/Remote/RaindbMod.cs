using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Realm.Remote
{
    public sealed class RaindbMod
    {
        internal static void TEST_PrintAll()
        {
            foreach (var mod in Fetch()) {
                string repo = mod.Repo == "" ? $"by {mod.Author}" : $"at {mod.Author}/{mod.Repo}";
                Console.WriteLine($"{mod.Name,-30} {repo}");
            }
        }

        public static IEnumerable<RaindbMod> Fetch()
        {
            string err;
            using MemoryStream ms = new();
            using (Process p = Execution.Begin(Extensions.MutatorPath, "--raindb")) {
                p.StandardOutput.BaseStream.CopyTo(ms);
                err = p.StandardError.ReadToEnd();
                Execution.PolitelyKill(p);
            }

            ms.Position = 0;

            BinaryReader reader = new(ms, Encoding.ASCII);

            List<RaindbMod> mods = new();

            while (reader.PeekChar() != -1) {
                mods.Add(new(reader));
            }

            return mods;
        }

        public string Name;
        public string Description;
        public string Author;
        public string Repo;
        public string Url;
        public string IconUrl;
        public string VideoUrl;
        public string ModDependencies;

        public RaindbMod(BinaryReader reader)
        {
            Name = reader.ReadString();
            Description = reader.ReadString();
            Author = reader.ReadString();
            Repo = reader.ReadString();
            Url = reader.ReadString();
            IconUrl = reader.ReadString();
            VideoUrl = reader.ReadString();
            ModDependencies = reader.ReadString();
        }

        public override string ToString()
        {
            return Name ?? "Noname";
        }
    }
}
