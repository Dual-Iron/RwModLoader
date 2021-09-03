using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Realm.Remote
{
    public sealed class RaindbMod
    {
        public static List<RaindbMod> Fetch()
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

        public readonly string Name;
        public readonly string Description;
        public readonly string Author;
        public readonly string Repo;
        public readonly string Url;
        public readonly string IconUrl;
        public readonly string VideoUrl;
        public readonly string ModDependencies;

        public string ModHomepage => string.IsNullOrEmpty(Repo) ? Url : $"https://github.com/{Author}/{Repo}#readme";

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
